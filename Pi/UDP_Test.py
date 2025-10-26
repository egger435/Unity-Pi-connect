import socket
import serial
import time

ser = serial.Serial('/dev/ttyS0', baudrate=115200, timeout=0.01)  # 串口，目前没用
UDP_IP = '0.0.0.0'
UDP_PORT = 12345  # 树莓派端监听frp服务器端口, 和树莓派frpc.toml中的localPort一致

sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
sock.bind((UDP_IP, UDP_PORT))

print(f"UDP start, lision port:{UDP_PORT}")

while True:
    data, addr = sock.recvfrom(32)
    cmd = data.decode('utf-8').strip()
    print(f"receive cmd:{cmd} from {addr}")