import asyncio
import json
import time

from websockets.server import WebSocketServerProtocol, serve
from websockets.exceptions import ConnectionClosed

from dronekit import connect, VehicleMode, LocationGlobal, Command
from pymavlink import mavutil

# Estado y parámetros globales mínimos
current_speed = 5.0
simulated_battery_level = 100.0
current_mission_index = 0

# Modelo de energía de batería (Wh)
BATTERY_CELLS = 12
CELL_NOMINAL_V = 3.7
BATTERY_CAPACITY_AH = 22.0

# Valores de respaldo
V_FALLBACK_NOMINAL = 44.4
V_CLAMP_MIN = 42.0
V_CLAMP_MAX = 50.4

I_FALLBACK_HOVER  = 18.0
I_FALLBACK_CRUISE = 25.0

BATTERY_NOMINAL_WH = BATTERY_CELLS * CELL_NOMINAL_V * BATTERY_CAPACITY_AH
battery_wh_remaining = BATTERY_NOMINAL_WH

TELEMETRY_DT = 0.2
USE_NOMINAL_V_IF_NONE = True

# Conexión y configuración inicial del vehículo
CONN_STR = "udp:0.0.0.0:14550"
print(f"Conectando con el vehículo en: {CONN_STR}")
vehicle = connect(CONN_STR, wait_ready=True)

print("Configurando parámetros para control externo completo desde App.")
while not vehicle.is_armable:
    time.sleep(0.2)
vehicle.parameters["TERRAIN_ENABLE"] = 0
vehicle.parameters["EK3_SRC1_POSZ"] = 3
vehicle.parameters["EK3_ALT_SOURCE"] = 1
vehicle.parameters["RNGFND1_TYPE"] = 0
vehicle.parameters["RNGFND2_TYPE"] = 0
vehicle.parameters["ARMING_CHECK"] = 0
vehicle.parameters["WPNAV_SPEED"] = 2000
time.sleep(0.5)
print("Parámetros críticos desactivados.")

clients: set[WebSocketServerProtocol] = set()


async def telemetry_loop() -> None:
    """Emite telemetría periódica y simula descarga de batería basada en energía."""
    global simulated_battery_level, battery_wh_remaining

    while True:
        loc = vehicle.location.global_relative_frame
        att = vehicle.attitude
        bat = vehicle.battery

        est_time_min = None
        v = bat.voltage
        i = bat.current
        if v is not None and i is not None and v * max(0.0, i) > 1e-6:
            power_w_now = v * max(0.0, i)
            est_time_min = (battery_wh_remaining / power_w_now) * 60.0

        payload = json.dumps(
            {
                "type": "telemetry",
                "lat": loc.lat,
                "lon": loc.lon,
                "alt": loc.alt,
                "yaw": att.yaw,
                "groundspeed": vehicle.groundspeed,
                "battery": {
                    "voltage": bat.voltage,
                    "current": bat.current,
                    "level": simulated_battery_level,
                    "energy_wh": battery_wh_remaining,
                    "eta_min": est_time_min,
                },
                "armed": vehicle.armed,
                "mode": vehicle.mode.name,
            }
        )

        if clients:
            await asyncio.gather(
                *[ws.send(payload) for ws in list(clients) if ws.open],
                return_exceptions=True,
            )

        if vehicle.armed:
            voltage = bat.voltage if bat.voltage is not None else V_FALLBACK_NOMINAL
            voltage = min(max(voltage, V_CLAMP_MIN), V_CLAMP_MAX)

            if bat.current is not None:
                current = bat.current
            else:
                if vehicle.groundspeed < 2.0:   
                    current = I_FALLBACK_HOVER
                else:                           
                    current = I_FALLBACK_CRUISE

            if voltage is not None:
                power_w = max(0.0, voltage * current)
                dWh = power_w * TELEMETRY_DT / 3600.0
                battery_wh_remaining = max(0.0, battery_wh_remaining - dWh)
                simulated_battery_level = 100.0 * (battery_wh_remaining / BATTERY_NOMINAL_WH)

        await asyncio.sleep(TELEMETRY_DT)


async def set_speed(speed: float, ws: WebSocketServerProtocol) -> None:
    """Ajusta la velocidad de navegación por MAVLink y confirma al cliente."""
    try:
        speed = float(speed)
    except (TypeError, ValueError):
        await ws.send(json.dumps({"status": "error", "message": "Parámetro 'speed' inválido"}))
        return

    if not (0.5 <= speed <= 20.0):
        await ws.send(json.dumps({"status": "error", "message": "Velocidad fuera de rango (0.5 – 20 m/s)."}))
        return

    global current_speed
    current_speed = speed

    vehicle._master.mav.command_long_send(
        vehicle._master.target_system,
        vehicle._master.target_component,
        mavutil.mavlink.MAV_CMD_DO_CHANGE_SPEED,
        0,
        1,
        speed,
        -1,
        0,
        0,
        0,
        0,
    )

    await ws.send(json.dumps({"status": "success", "message": f"Velocidad ajustada a {speed:.1f} m/s"}))


async def relaunch_mission() -> None:
    """Relanza la misión desde el último índice guardado tras recarga/despegue si procede."""
    print("🔄 Relanzando misión tras carga...")

    cmds = vehicle.commands
    cmds.download()
    cmds.wait_ready()

    if cmds.count == 0:
        print("⚠ No hay misión para relanzar.")
        return

    vehicle.mode = VehicleMode("GUIDED")
    while vehicle.mode.name != "GUIDED":
        await asyncio.sleep(0.1)

    if not vehicle.armed:
        print("✅ Armando motores para continuar misión...")
        vehicle.armed = True
        for _ in range(50):
            if vehicle.armed:
                break
            await asyncio.sleep(0.2)
        else:
            print("❌ No se pudo armar el dron.")
            return

    if vehicle.location.global_relative_frame.alt < 1.0:
        target_alt = cmds[vehicle.commands.next].z
        print(f"🚀 Despegando hasta {target_alt} m...")
        vehicle.simple_takeoff(target_alt)
        while vehicle.location.global_relative_frame.alt < target_alt * 0.95:
            await asyncio.sleep(0.5)

    vehicle.commands.next = current_mission_index
    vehicle._master.mav.mission_set_current_send(
        vehicle._master.target_system,
        vehicle._master.target_component,
        current_mission_index,
    )
    vehicle._master.mav.command_long_send(
        vehicle._master.target_system,
        vehicle._master.target_component,
        mavutil.mavlink.MAV_CMD_MISSION_START,
        0,
        current_mission_index,
        0,
        0,
        0,
        0,
        0,
        0,
    )
    vehicle._master.mav.command_long_send(
        vehicle._master.target_system,
        vehicle._master.target_component,
        mavutil.mavlink.MAV_CMD_DO_CHANGE_SPEED,
        0,
        1,
        current_speed,
        -1,
        0,
        0,
        0,
        0,
    )

    vehicle.mode = VehicleMode("AUTO")
    while vehicle.mode.name != "AUTO":
        await asyncio.sleep(0.1)

    print("✅ Misión relanzada.")


async def set_home(lat: float, lon: float, alt: float, ws: WebSocketServerProtocol) -> None:
    """Establece una nueva base (home) vía MAVLink y sincroniza el valor local."""
    try:
        lat = float(lat)
        lon = float(lon)
        alt = float(alt)
    except (TypeError, ValueError):
        await ws.send(json.dumps({"status": "error", "message": "Parámetros de home inválidos"}))
        return

    try:
        vehicle._master.mav.command_long_send(
            vehicle._master.target_system,
            vehicle._master.target_component,
            mavutil.mavlink.MAV_CMD_DO_SET_HOME,
            0,
            1,
            0,
            0,
            0,
            lat,
            lon,
            alt,
        )
        vehicle.home_location = LocationGlobal(lat, lon, alt)
        await ws.send(json.dumps({"status": "success", "message": "Nueva base establecida", "home": {"lat": lat, "lon": lon, "alt": alt}}))
    except Exception as e:
        await ws.send(json.dumps({"status": "error", "message": f"Error al fijar la base: {e}"}))


async def upload_mission_takeoff_guided(wps: list[dict], ws: WebSocketServerProtocol) -> None:
    """Carga waypoints, despega en GUIDED y arranca la misión en AUTO."""
    if not wps:
        await ws.send(json.dumps({"status": "error", "message": "Waypoints vacíos"}))
        return
    if len(wps) > 650:
        await ws.send(json.dumps({"status": "error", "message": "Máx. 650 waypoints"}))
        return

    if vehicle.mode.name != "GUIDED":
        vehicle.mode = VehicleMode("GUIDED")
        while vehicle.mode.name != "GUIDED":
            await asyncio.sleep(0.1)

    cmds = vehicle.commands
    cmds.clear()
    cmds.upload()
    await asyncio.sleep(0.5)
    cmds.clear()
    cmds.upload()
    await asyncio.sleep(0.5)

    for _ in range(10):
        cmds.download()
        cmds.wait_ready()
        if cmds.count == 0:
            break
        print(f"Esperando limpieza: aún hay {cmds.count} comandos")
        await asyncio.sleep(0.5)
    else:
        await ws.send(json.dumps({"status": "error", "message": "No se pudo limpiar la misión anterior correctamente."}))
        return

    for wp in wps:
        cmds.add(
            Command(
                0,
                0,
                0,
                mavutil.mavlink.MAV_FRAME_GLOBAL_RELATIVE_ALT,
                mavutil.mavlink.MAV_CMD_NAV_WAYPOINT,
                0,
                1,
                0,
                0,
                0,
                0,
                wp["lat"],
                wp["lon"],
                wp["alt"],
            )
        )
    cmds.upload()

    while not vehicle.is_armable:
        await asyncio.sleep(0.2)

    print(f"DEBUG: WPNAV_SPEED actual: {vehicle.parameters['WPNAV_SPEED']} cm/s")
    print("Armando motores...")
    vehicle.armed = True

    arm_timeout = 10
    for _ in range(int(arm_timeout / TELEMETRY_DT)):
        if vehicle.armed:
            break
        await asyncio.sleep(TELEMETRY_DT)
    else:
        await ws.send(json.dumps({"status": "error", "message": "No se pudo armar el dron tras cargar el terreno."}))
        return

    target_alt = wps[0]["alt"]
    already_airborne = vehicle.armed and vehicle.location.global_relative_frame.alt > 1.0

    if not already_airborne:
        print("Iniciando despegue...")
        vehicle.simple_takeoff(target_alt)
        while vehicle.location.global_relative_frame.alt < target_alt * 0.95:
            await asyncio.sleep(0.5)
    else:
        print("El dron ya está en el aire. Se omite el despegue.")

    vehicle.commands.next = 0
    vehicle._master.mav.mission_set_current_send(
        vehicle._master.target_system,
        vehicle._master.target_component,
        0,
    )
    vehicle._master.mav.command_long_send(
        vehicle._master.target_system,
        vehicle._master.target_component,
        mavutil.mavlink.MAV_CMD_MISSION_START,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
    )
    vehicle._master.mav.command_long_send(
        vehicle._master.target_system,
        vehicle._master.target_component,
        mavutil.mavlink.MAV_CMD_DO_CHANGE_SPEED,
        0,
        1,
        current_speed,
        -1,
        0,
        0,
        0,
        0,
    )

    vehicle.mode = VehicleMode("AUTO")
    while vehicle.mode.name != "AUTO":
        await asyncio.sleep(0.1)

    await ws.send(
        json.dumps(
            {
                "status": "success",
                "message": f"Despegue completado a {target_alt} m; misión de {len(wps)} WPs en ejecución (AUTO)",
            }
        )
    )


def get_status() -> dict:
    """Entrega el estado básico del vehículo para consultas puntuales."""
    loc = vehicle.location.global_relative_frame
    bat = vehicle.battery
    return {
        "location": {"lat": loc.lat, "lon": loc.lon, "alt": loc.alt},
        "battery": {"voltage": bat.voltage, "current": bat.current, "level": simulated_battery_level},
        "mode": vehicle.mode.name,
        "armed": vehicle.armed,
    }


async def handler(ws: WebSocketServerProtocol) -> None:
    """Gestiona los mensajes del cliente y ejecuta los comandos solicitados."""
    global current_mission_index, simulated_battery_level, battery_wh_remaining
    clients.add(ws)
    try:
        async for message in ws:
            print("📥 Recibido mensaje:", message)
            try:
                data = json.loads(message)
            except json.JSONDecodeError:
                await ws.send(json.dumps({"status": "error", "message": "JSON inválido"}))
                continue

            cmd = data.get("command")
            if cmd == "upload_mission":
                await upload_mission_takeoff_guided(data.get("waypoints", []), ws)

            elif cmd == "set_speed":
                speed = data.get("speed")
                if speed is not None:
                    await set_speed(speed, ws)
                else:
                    await ws.send(json.dumps({"status": "error", "message": "Parámetro 'speed' obligatorio"}))

            elif cmd == "return_to_launch":
                current_mission_index = vehicle.commands.next
                vehicle.mode = VehicleMode("RTL")
                await ws.send(json.dumps({"status": "success", "message": "Regresando a casa"}))

            elif cmd == "set_home":
                lat, lon = data.get("lat"), data.get("lon")
                alt = data.get("alt", 0)
                if lat is not None and lon is not None:
                    await set_home(lat, lon, alt, ws)
                else:
                    await ws.send(json.dumps({"status": "error", "message": "Parámetros 'lat' y 'lon' obligatorios"}))

            elif cmd == "set_battery_level":
                level = data.get("level")
                if level is not None:
                    simulated_battery_level = float(level)
                    battery_wh_remaining = BATTERY_NOMINAL_WH * (simulated_battery_level / 100.0)
                    await ws.send(json.dumps({"status": "success", "message": f"Nivel de batería simulado: {level}%"}))
                else:
                    await ws.send(json.dumps({"status": "error", "message": "Falta el parámetro 'level'"}))

            elif cmd == "resume_mission":
                await relaunch_mission()
                await ws.send(json.dumps({"status": "success", "message": "Misión reanudada"}))

            elif cmd == "get_status":
                await ws.send(json.dumps({"status": "success", "data": get_status()}))

            else:
                await ws.send(json.dumps({"status": "error", "message": "Comando no reconocido"}))
    except ConnectionClosed:
        pass
    finally:
        clients.discard(ws)


async def main() -> None:
    """Arranca el servidor WebSocket y el bucle de telemetría."""
    async with serve(handler, "0.0.0.0", 8765):
        print("Servidor WebSocket en ws://0.0.0.0:8765")
        asyncio.create_task(telemetry_loop())
        await asyncio.Future()


if __name__ == "__main__":
    try:
        asyncio.run(main())
    finally:
        print("Cerrando la conexión con el vehículo…")
        vehicle.close()
