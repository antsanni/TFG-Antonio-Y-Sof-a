import socket

sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
sock.bind(("0.0.0.0", 14550))

print("Esperando paquetes MAVLink en UDP:14550...")
while True:
    data, addr = sock.recvfrom(2048)
    print(f"Recibidos {len(data)} bytes desde {addr}")