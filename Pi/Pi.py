import socket
import struct
import threading
import time
from picamera2 import Picamera2

# UDP 指令接收端配置
UDP_CTRL_IP = "0.0.0.0"
UDP_CTRL_PORT = 12345   # 树莓派端监听frp服务器端口, 和树莓派frpc.toml中的localPort一致

# UDP 视频流发送端配置
SERVER_IP = "服务器公网ip"
UDP_VIDEO_PORT = 13300  # 树莓派端向frp服务器发送消息的端口, 和Unity端frpc.toml中的remotePort一致
RESOLUTION = (128, 64)  # 视频分辨率
FPS = 30                # 视频帧率
CHUNK_SIZE = 1024       # 视频帧分片大小
MAGIC_NUM = 0xEAEAEFEF  # 帧头标识

# 视频流传输线程    
def video_stream_thread():
    # 摄像头初始化
    picam = Picamera2()
    config = picam.create_video_configuration(
        main={"size":RESOLUTION, "format":"RGB888"}
    )
    picam.configure(config)
    picam.set_controls({"FrameRate":FPS})
    picam.start()
    time.sleep(1)
    
    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    print(f"[VIDEO] Start sending to {SERVER_IP}:{UDP_VIDEO_PORT}")
    
    while True:
        array = picam.capture_array()
        frame_data = array.tobytes()[::-1]  # # 将帧字节数组取反再发送
            
        # 发送帧头
        header = struct.pack("II", MAGIC_NUM, len(frame_data))
        sock.sendto(header, (SERVER_IP, UDP_VIDEO_PORT))
        
        # 发送帧分片
        for i in range(0, len(frame_data), CHUNK_SIZE):
            chunk = frame_data[i:i + CHUNK_SIZE]
            sock.sendto(chunk, (SERVER_IP, UDP_VIDEO_PORT))
        
def main():
    # 视频流发送子线程
    t_video = threading.Thread(target=video_stream_thread, daemon=True)
    t_video.start()
    
    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    sock.bind((UDP_CTRL_IP, UDP_CTRL_PORT))
    print(f"[CTRL] UDP listening on {UDP_CTRL_PORT}")
    
    # 主线程接收控制数据
    while True:
        data, addr = sock.recvfrom(32)
        cmd = data.decode("utf-8").strip()
        print(f"[CTRL] Received cmd: '{cmd}' from {addr}")

if __name__ == "__main__":
    main()
    