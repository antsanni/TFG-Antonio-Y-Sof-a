import asyncio
import json
import time
import threading
import argparse

from websockets.server import WebSocketServerProtocol, serve
from websockets.exceptions import ConnectionClosed

from dronekit import connect, VehicleMode, LocationGlobal, Command
from pymavlink import mavutil

#puerto_base = 14550
puerto_base = 5760
PUERTOS = []
DRONES = []

def conectar_vehiculo(puerto):

    print(f"--- [Hilo {puerto}] Intentando conectar...")
    #connection_string = f"udp:0.0.0.0:{puerto}"
    connection_string = f"tcp:127.0.0.1:{puerto}"

    try:
        vehicle = connect(connection_string, wait_ready=True)
        vehicle.mi_id = puerto
        print(f"[Hilo {puerto}] ¡CONECTADO Y LISTO!")

        vehicle.parameters["ARMING_CHECK"] = 0
        DRONES.append(vehicle)

        while True:
            alt = vehicle.location.global_relative_frame.alt
            print(f"[Dron {puerto}] Altitud: {alt}")
            time.sleep(5)

    except Exception as e:
        print(f" [Hilo {puerto}] Error: {e}")


async def handler(websocket):
    print("Unity conectado. Enviando datos...")
    while True:
        paquete_envio = []
        for dron in DRONES:
            try:
                datos = {
                    "latitud": dron.location.global_relative_frame.lat,
                    "longitud": dron.location.global_relative_frame.lon,
                    "altitud": dron.location.global_relative_frame.alt,
                    "rumbo": dron.heading,
                    "id": dron.mi_id
                }
                paquete_envio.append(datos)

            except Exception as e:
                pass

        if len(paquete_envio) > 0:
            envio = json.dumps(paquete_envio)
            await websocket.send(envio)

        await asyncio.sleep(0.05)



async def main() -> None:

    print("---- LANZANDO HILOS DE CONEXION ----")
    for puerto in PUERTOS:
        print(f"ESTAMOS CON EL PUERTO {puerto}")
        hilo = threading.Thread(target=conectar_vehiculo, args=(puerto,))
        hilo.start()
        print(f"Hilo lanzado para puerto {puerto}")

    print("---- ARRANCANDO EL SERVIDOR WEBSOCKET ----")
    async with serve(handler, "0.0.0.0", 8765):
        print("Servidor WebSocket en ws://0.0.0.0:8765")
        await asyncio.Future()
        


if __name__ == "__main__":

    parser = argparse.ArgumentParser(description="Servidor de drones")
    parser.add_argument("-n", "--numero", type=int, help="Numero de drones que quieres utilizar", default=1)
    args = parser.parse_args()

    for i in range(args.numero):
        PUERTOS.append(puerto_base + (i * 10))

    try:
        asyncio.run(main())
    finally:
        print("Cerrando la conexión con los vehículos")
        for dron in DRONES:
            dron.close()
        print("¡Todo cerrado!")
