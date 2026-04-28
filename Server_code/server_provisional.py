import asyncio
import json
import time
import threading
import argparse
import base64
import numpy as np
import cv2

import collections
import collections.abc
collections.MutableMapping = collections.abc.MutableMapping
collections.Mapping = collections.abc.Mapping

from websockets.server import WebSocketServerProtocol, serve
from websockets.exceptions import ConnectionClosed

from dronekit import connect, VehicleMode, LocationGlobal, Command
from pymavlink import mavutil

print("Cargando modelo YOLOv8...")
from ultralytics import YOLO
modelo_yolo = YOLO('yolov8n.pt')
print("¡Modelo YOLO cargado y listo para buscar personas!")
# ---------------------------------------------

puerto_base = 5760
PUERTOS = []
DRONES = []
clients = set() 

def conectar_vehiculo(puerto):
    print(f"--- [Hilo {puerto}] Intentando conectar...")
    connection_string = f"tcp:127.0.0.1:{puerto}"

    try:
        vehicle = connect(connection_string, wait_ready=True)
        vehicle.mi_id = puerto
        vehicle.simulated_battery = 100.0 
        
        print(f"[Hilo {puerto}] ¡CONECTADO Y LISTO!")
        vehicle.parameters["ARMING_CHECK"] = 0
        DRONES.append(vehicle)

        while True:
            time.sleep(10)
    except Exception as e:
        print(f" [Hilo {puerto}] Error: {e}")

async def telemetry_loop():
    while True:
        paquete_envio = []
        for dron in DRONES:
            try:
                if dron.armed:
                    # Gasta 0.033 cada tick -> 5 minutos de vuelo
                    dron.simulated_battery = max(0.0, dron.simulated_battery - 0.033)

                datos = {
                    "latitud": dron.location.global_relative_frame.lat,
                    "longitud": dron.location.global_relative_frame.lon,
                    "altitud": dron.location.global_relative_frame.alt,
                    "rumbo": dron.heading,
                    "id": dron.mi_id,
                    "level": dron.simulated_battery,
                    "flightMode": dron.mode.name,
                    "isArmed": dron.armed
                }
                paquete_envio.append(datos)
            except Exception as e:
                pass 

        if paquete_envio and clients:
            envio = json.dumps(paquete_envio)
            await asyncio.gather(
                *[ws.send(envio) for ws in list(clients) if ws.open],
                return_exceptions=True,
            )

        await asyncio.sleep(0.1)

async def ejecutar_rtl_desde_suelo(vehicle, altitud_despegue=20.0):
    print(f"[Dron {vehicle.mi_id}] RTL en suelo. Armando y subiendo...")
    vehicle.mode = VehicleMode("GUIDED")
    vehicle.armed = True

    while not vehicle.armed:
        await asyncio.sleep(1)

    if hasattr(vehicle, 'mi_base_guardada'):
        lat, lon = vehicle.mi_base_guardada
        vehicle._master.mav.command_long_send(
            vehicle._master.target_system, vehicle._master.target_component,
            mavutil.mavlink.MAV_CMD_DO_SET_HOME, 0, 0, 0, 0, 0, lat, lon, 0
        )

    vehicle.simple_takeoff(altitud_despegue)

    while True:
        alt_actual = vehicle.location.global_relative_frame.alt
        if alt_actual >= altitud_despegue * 0.95:
            break
        await asyncio.sleep(1)

    vehicle.mode = VehicleMode("RTL")
    print(f"[Dron {vehicle.mi_id}] Volviendo a la base...")

async def ejecutar_mision(vehicle, altitud_despegue=20.0):
    print(f"[Dron {vehicle.mi_id}] Preparando para misión. Cambiando a GUIDED...")
    vehicle.mode = VehicleMode("GUIDED")
    vehicle.armed = True

    while not vehicle.armed:
        await asyncio.sleep(1)

    if hasattr(vehicle, 'mi_base_guardada'):
        lat, lon = vehicle.mi_base_guardada
        vehicle._master.mav.command_long_send(
            vehicle._master.target_system, vehicle._master.target_component,
            mavutil.mavlink.MAV_CMD_DO_SET_HOME, 0, 0, 0, 0, 0, lat, lon, 0
        )

    print(f"[Dron {vehicle.mi_id}] ¡Armado! Iniciando despegue a {altitud_despegue}m...")
    vehicle.simple_takeoff(altitud_despegue)

    while True:
        alt_actual = vehicle.location.global_relative_frame.alt
        if alt_actual >= altitud_despegue * 0.95:
            print(f"[Dron {vehicle.mi_id}] Altitud alcanzada.")
            break
        await asyncio.sleep(1)

    vehicle.mode = VehicleMode("AUTO")

async def ejecutar_resume(vehicle, altitud_despegue=20.0):
    print(f"[Dron {vehicle.mi_id}] Reanudando misión. Preparando despegue...")
    vehicle.mode = VehicleMode("GUIDED")
    vehicle.armed = True

    while not vehicle.armed:
        await asyncio.sleep(1)

    if hasattr(vehicle, 'mi_base_guardada'):
        lat, lon = vehicle.mi_base_guardada
        vehicle._master.mav.command_long_send(
            vehicle._master.target_system, vehicle._master.target_component,
            mavutil.mavlink.MAV_CMD_DO_SET_HOME, 0, 0, 0, 0, 0, lat, lon, 0
        )

    print(f"[Dron {vehicle.mi_id}] Armado. Subiendo a {altitud_despegue}m para continuar...")
    vehicle.simple_takeoff(altitud_despegue)

    while True:
        alt_actual = vehicle.location.global_relative_frame.alt
        if alt_actual >= altitud_despegue * 0.95:
            break
        await asyncio.sleep(1)

    if hasattr(vehicle, 'wp_guardado'):
        wp_a_retomar = max(1, vehicle.wp_guardado)
        vehicle.commands.next = wp_a_retomar
        print(f"[Dron {vehicle.mi_id}] 📖 Retomando misión desde el punto {wp_a_retomar}...")
    
    print(f"[Dron {vehicle.mi_id}] Cambiando a AUTO.")
    vehicle.mode = VehicleMode("AUTO")


async def handler(websocket: WebSocketServerProtocol):
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

            target_drone = next((d for d in DRONES if d.mi_id == drone_id), None)
            if target_drone is None:
                continue

            # --- NUEVO COMANDO: VISIÓN ARTIFICIAL ---
            if cmd == "process_image":
                imagen_base64 = data.get("image")
                if imagen_base64:
                    try:
                        # 1. Convertimos el texto (Base64) a una imagen real de OpenCV
                        img_bytes = base64.b64decode(imagen_base64)
                        img_np = np.frombuffer(img_bytes, dtype=np.uint8)
                        frame = cv2.imdecode(img_np, cv2.IMREAD_COLOR)

                        # 2. Le pasamos la imagen a YOLO
                        # verbose=False es para que no llene la consola de texto en cada frame
                        resultados = modelo_yolo(frame, verbose=False)

                        # 3. Comprobamos si hay alguna persona (Clase 0 en YOLO = 'person')
                        persona_detectada = False
                        for resultado in resultados:
                            for caja in resultado.boxes:
                                if int(caja.cls[0]) == 0:  # Si la clase es 0
                                    persona_detectada = True
                                    break

                        # 4. Si hay una persona, avisamos
                        if persona_detectada:
                            lat = target_drone.location.global_relative_frame.lat
                            lon = target_drone.location.global_relative_frame.lon
                            
                            print(f"🚨 [Dron {drone_id}] ¡PERSONA DETECTADA en Lat: {lat}, Lon: {lon}!")
                            
                            # (Opcional por ahora) Le mandamos un chivatazo a Unity
                            alerta = json.dumps([{
                                "type": "alert",
                                "message": "person_detected",
                                "id": drone_id,
                                "lat": lat,
                                "lon": lon
                            }])
                            await websocket.send(alerta)

                    except Exception as e:
                        print(f"Error procesando imagen: {e}")
            # ----------------------------------------

            elif cmd == "return_to_launch":
                if hasattr(target_drone, 'commands'):
                    target_drone.wp_guardado = target_drone.commands.next
                    print(f"[Dron {drone_id}] 🔖 Batería baja. Guardando progreso en el punto: {target_drone.wp_guardado}")

                if not target_drone.armed or target_drone.location.global_relative_frame.alt < 2.0:
                    asyncio.create_task(ejecutar_rtl_desde_suelo(target_drone, 20.0))
                else:
                    if hasattr(target_drone, 'mi_base_guardada'):
                        lat, lon = target_drone.mi_base_guardada
                        target_drone._master.mav.command_long_send(
                            target_drone._master.target_system, target_drone._master.target_component,
                            mavutil.mavlink.MAV_CMD_DO_SET_HOME, 0, 0, 0, 0, 0, lat, lon, 0
                        )
                    target_drone.mode = VehicleMode("RTL")
                    print(f"[Dron {drone_id}] Ejecutando RTL normal para ir a recargar")

            elif cmd == "set_speed":
                speed = data.get("speed")
                if speed is not None:
                    target_drone._master.mav.command_long_send(
                        target_drone._master.target_system, target_drone._master.target_component,
                        mavutil.mavlink.MAV_CMD_DO_CHANGE_SPEED, 0, 1, float(speed), -1, 0, 0, 0, 0,
                    )

            elif cmd == "set_home":
                lat, lon = data.get("lat"), data.get("lon")
                if lat is not None and lon is not None:
                    target_drone.mi_base_guardada = (float(lat), float(lon))
                    target_drone._master.mav.command_long_send(
                        target_drone._master.target_system,
                        target_drone._master.target_component,
                        mavutil.mavlink.MAV_CMD_DO_SET_HOME,
                        0, 0, 0, 0, 0, float(lat), float(lon), 0,
                    )
                    target_drone.home_location = LocationGlobal(float(lat), float(lon), 0)
                    print(f"[Dron {drone_id}] 🏠 Base fijada y guardada en {lat}, {lon}")

            elif cmd == "set_battery_level":
                level = data.get("level")
                if level is not None:
                    target_drone.simulated_battery = float(level)

            elif cmd == "upload_mission":
                wps = data.get("waypoints", [])
                cmds = target_drone.commands
                cmds.clear()
                for wp in wps:
                    cmds.add(Command(0, 0, 0, mavutil.mavlink.MAV_FRAME_GLOBAL_RELATIVE_ALT, mavutil.mavlink.MAV_CMD_NAV_WAYPOINT, 0, 0, 0, 0, 0, 0, wp.get("lat"), wp.get("lon"), wp.get("alt")))
                cmds.upload()

            elif cmd == "start_mission":
                asyncio.create_task(ejecutar_mision(target_drone, 20.0))

            elif cmd == "resume_mission":
                print(f"[Dron {drone_id}] Orden de REANUDAR recibida tras la recarga.")
                asyncio.create_task(ejecutar_resume(target_drone, 20.0))

    except ConnectionClosed:
        print("🔴 Unity desconectado.")
    finally:
        clients.discard(websocket)

async def main() -> None:
    print("---- LANZANDO HILOS DE CONEXION ----")
    for puerto in PUERTOS:
        hilo = threading.Thread(target=conectar_vehiculo, args=(puerto,), daemon=True)
        hilo.start()

    print("---- ARRANCANDO EL SERVIDOR WEBSOCKET ----")
    async with serve(handler, "0.0.0.0", 8765):
        print("Servidor WebSocket en ws://0.0.0.0:8765")
        asyncio.create_task(telemetry_loop())
        await asyncio.Future() 

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
        for dron in DRONES:
            dron.close()
        print("¡Todo cerrado!")