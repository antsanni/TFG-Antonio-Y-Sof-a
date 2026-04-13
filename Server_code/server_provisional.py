import asyncio
import json
import time
import threading
import argparse

from websockets.server import WebSocketServerProtocol, serve
from websockets.exceptions import ConnectionClosed

from dronekit import connect, VehicleMode, LocationGlobal, Command
from pymavlink import mavutil

puerto_base = 5760
PUERTOS = []
DRONES = []
clients = set() # Guardamos los clientes de Unity conectados

def conectar_vehiculo(puerto):
    """Se conecta a un dron por TCP y lo añade a la flota"""
    print(f"--- [Hilo {puerto}] Intentando conectar...")
    connection_string = f"tcp:127.0.0.1:{puerto}"

    try:
        vehicle = connect(connection_string, wait_ready=True)
        # --- ATRIBUTOS PERSONALIZADOS DEL DRON ---
        vehicle.mi_id = puerto
        vehicle.simulated_battery = 100.0 # Batería propia de este dron
        
        print(f"[Hilo {puerto}] ¡CONECTADO Y LISTO!")
        vehicle.parameters["ARMING_CHECK"] = 0
        DRONES.append(vehicle)

        # Mantenemos el hilo vivo
        while True:
            time.sleep(10)

    except Exception as e:
        print(f" [Hilo {puerto}] Error: {e}")


async def telemetry_loop():
    """Bucle que lee los datos de TODOS los drones y los envía a Unity"""
    while True:
        paquete_envio = []
        for dron in DRONES:
            try:
                # Simulamos que la batería baja si está armado
                if dron.armed:
                    dron.simulated_battery = max(0.0, dron.simulated_battery - 0.05)

                datos = {
                    "latitud": dron.location.global_relative_frame.lat,
                    "longitud": dron.location.global_relative_frame.lon,
                    "altitud": dron.location.global_relative_frame.alt,
                    "rumbo": dron.heading,
                    "id": dron.mi_id,
                    # Datos nuevos añadidos para Unity
                    "level": dron.simulated_battery,
                    "flightMode": dron.mode.name,
                    "isArmed": dron.armed
                }
                paquete_envio.append(datos)
            except Exception as e:
                pass # Ignorar fallos temporales de lectura

        # Si hay clientes conectados y datos, enviamos el JSON
        if paquete_envio and clients:
            envio = json.dumps(paquete_envio)
            await asyncio.gather(
                *[ws.send(envio) for ws in list(clients) if ws.open],
                return_exceptions=True,
            )

        await asyncio.sleep(0.1) # Enviar datos 10 veces por segundo

async def ejecutar_mision(vehicle, altitud_despegue=15.0):
    """Rutina asíncrona para armar, despegar y pasar a modo AUTO sin bloquear el WebSocket"""
    print(f"[Dron {vehicle.mi_id}] Preparando para misión. Cambiando a GUIDED...")
    vehicle.mode = VehicleMode("GUIDED")
    
    # Armamos motores
    vehicle.armed = True

    # Esperamos a que se arme (usando asyncio.sleep para no bloquear los demás drones)
    while not vehicle.armed:
        await asyncio.sleep(1)

    print(f"[Dron {vehicle.mi_id}] ¡Armado! Iniciando despegue a {altitud_despegue}m...")
    vehicle.simple_takeoff(altitud_despegue)

    # Esperamos a que alcance el 95% de la altitud objetivo
    while True:
        alt_actual = vehicle.location.global_relative_frame.alt
        if alt_actual >= altitud_despegue * 0.95:
            print(f"[Dron {vehicle.mi_id}] Altitud alcanzada ({alt_actual:.1f}m).")
            break
        await asyncio.sleep(1)

    # Pasamos a modo AUTO para que siga los waypoints cargados
    print(f"[Dron {vehicle.mi_id}] Cambiando a modo AUTO. ¡Iniciando ruta!")
    vehicle.mode = VehicleMode("AUTO")

async def handler(websocket: WebSocketServerProtocol):
    """Recibe los comandos de Unity y se los aplica al dron correcto"""
    clients.add(websocket)
    print("🟢 Unity conectado. Esperando comandos...")
    
    try:
        async for message in websocket:
            try:
                data = json.loads(message)
            except json.JSONDecodeError:
                continue

            cmd = data.get("command")
            drone_id = data.get("id")

            # 1. Buscamos a qué dron va dirigido el comando
            target_drone = next((d for d in DRONES if d.mi_id == drone_id), None)

            if target_drone is None:
                print(f"⚠ Comando ignorado: Dron {drone_id} no encontrado en DRONES.")
                continue

            # 2. Ejecutamos el comando sobre EL DRON ESPECÍFICO (target_drone)
            if cmd == "return_to_launch":
                target_drone.mode = VehicleMode("RTL")
                print(f"[Dron {drone_id}] Ejecutando RTL")

            elif cmd == "set_speed":
                speed = data.get("speed")
                if speed is not None:
                    target_drone._master.mav.command_long_send(
                        target_drone._master.target_system,
                        target_drone._master.target_component,
                        mavutil.mavlink.MAV_CMD_DO_CHANGE_SPEED,
                        0, 1, float(speed), -1, 0, 0, 0, 0,
                    )
                    print(f"[Dron {drone_id}] Velocidad ajustada a {speed}")

            elif cmd == "set_home":
                lat, lon, alt = data.get("lat"), data.get("lon"), data.get("alt", 0)
                if lat is not None and lon is not None:
                    target_drone._master.mav.command_long_send(
                        target_drone._master.target_system,
                        target_drone._master.target_component,
                        mavutil.mavlink.MAV_CMD_DO_SET_HOME,
                        0, 1, 0, 0, 0, float(lat), float(lon), float(alt),
                    )
                    target_drone.home_location = LocationGlobal(float(lat), float(lon), float(alt))
                    print(f"[Dron {drone_id}] Base fijada en {lat}, {lon}")

            elif cmd == "set_battery_level":
                level = data.get("level")
                if level is not None:
                    target_drone.simulated_battery = float(level)
                    print(f"[Dron {drone_id}] Batería recargada al {level}% desde Unity.")

            elif cmd == "upload_mission":
                wps = data.get("waypoints", [])
                print(f"[Dron {drone_id}] Recibida misión de {len(wps)} puntos. Cargando en memoria...")
                
                # Obtenemos la lista de comandos del dron y la limpiamos
                cmds = target_drone.commands
                cmds.clear()

                # Recorremos el JSON y añadimos cada punto como un Waypoint
                for wp in wps:
                    lat = wp.get("lat")
                    lon = wp.get("lon")
                    alt = wp.get("alt")
                    
                    # Creamos el comando MAVLink para el waypoint
                    cmds.add(
                        Command(0, 0, 0, 
                                mavutil.mavlink.MAV_FRAME_GLOBAL_RELATIVE_ALT, 
                                mavutil.mavlink.MAV_CMD_NAV_WAYPOINT, 
                                0, 0, 0, 0, 0, 0, 
                                lat, lon, alt)
                    )
                
                # Subimos la misión físicamente al dron
                cmds.upload()
                print(f"[Dron {drone_id}] ¡Misión subida correctamente!")

            elif cmd == "start_mission":
                print(f"[Dron {drone_id}] Orden de inicio de misión recibida.")
                # Lanzamos la tarea de despegue en segundo plano
                asyncio.create_task(ejecutar_mision(target_drone, 15.0))

    except ConnectionClosed:
        print("🔴 Unity desconectado.")
    finally:
        clients.discard(websocket)


async def main() -> None:
    print("---- LANZANDO HILOS DE CONEXION ----")
    for puerto in PUERTOS:
        print(f"Lanzando hilo para puerto {puerto}")
        hilo = threading.Thread(target=conectar_vehiculo, args=(puerto,), daemon=True)
        hilo.start()

    print("---- ARRANCANDO EL SERVIDOR WEBSOCKET ----")
    async with serve(handler, "0.0.0.0", 8765):
        print("Servidor WebSocket en ws://0.0.0.0:8765")
        
        # Arrancamos el bucle de telemetría en paralelo
        asyncio.create_task(telemetry_loop())
        
        await asyncio.Future() # Mantiene el servidor vivo


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Servidor de drones")
    parser.add_argument("-n", "--numero", type=int, help="Numero de drones que quieres utilizar", default=1)
    args = parser.parse_args()

    for i in range(args.numero):
        PUERTOS.append(puerto_base + (i * 10))

    try:
        asyncio.run(main())
    except KeyboardInterrupt:
        pass
    finally:
        print("Cerrando la conexión con los vehículos...")
        for dron in DRONES:
            dron.close()
        print("¡Todo cerrado!")