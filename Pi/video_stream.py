from picamera2 import Picamera2, Preview
import socket
import time
import struct

UDP_IP = "服务器公网ip"
UDP_PORT = 13300  # 树莓派端向frp服务器发送消息的端口, 和Unity端frpc.toml中的remotePort一致
CHUNK_SIZE = 1024
FPS = 30
MAGIC_NUM = 0xEAEAEFEF  # 自定义帧头

picam = Picamera2()
config = picam.create_video_configuration(main={'size':(128,64), 'format':'RGB888'})
picam.configure(config)
picam.set_controls({'FrameRate':FPS})
picam.start()
time.sleep(1)

sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
print("start send video...")

ii = 0
try:
    while True:
        array = picam.capture_array()
        frame_data = array.tobytes()[::-1]  # 将帧字节数组取反再发送
        
        header = struct.pack("II", MAGIC_NUM, len(frame_data))
        sock.sendto(header, (UDP_IP, UDP_PORT))
        
        for i in range(0, len(frame_data), CHUNK_SIZE):
            chunk = frame_data[i:i + CHUNK_SIZE]
            sock.sendto(chunk, (UDP_IP, UDP_PORT))
        ii += 1
        print(f"send frame:{ii}, size:{len(frame_data)}byte")

except KeyboardInterrupt:
    print('end send')
finally:
    picam.stop()
    sock.close()