import serial
import socket
import struct
import threading
import time
import cv2
from picamera2 import Picamera2 # pyright: ignore[reportMissingImports]

# UDP 指令接收端配置
UDP_CTRL_IP    = "0.0.0.0"
UDP_CTRL_PORT  = 12345          # 树莓派端监听frp服务器端口, 和树莓派frpc.toml中的localPort一致

# UDP 视频流发送端配置
SEND_VIDEO     = True
SERVER_IP      = "服务器公网IP"
UDP_VIDEO_PORT = 13300          # 树莓派端向frp服务器发送消息的端口, 和Unity端frpc.toml中的remotePort一致
RESOLUTION     = (1024, 512)    # 视频分辨率
FPS            = 30             # 视频帧率
CHUNK_SIZE     = 1024           # 视频帧分片大小
JPEG_QUALITY   = 50             # JPEG压缩质量

global log_index
log_index = 0

ser = serial.Serial("/dev/ttyS0", baudrate=115200, timeout=0.01)  # 建立串口通信
print(f"[SERIAL] Serial start; l_i: {log_index}")
log_index += 1

# 建立socket客户端
sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
sock.bind((UDP_CTRL_IP, UDP_CTRL_PORT))
print(f"[CTRL] UDP listening on {UDP_CTRL_PORT}; l_i: {log_index}")
log_index += 1

# 视频流传输线程    
def video_stream_thread():
    global log_index
    # 摄像头初始化
    picam = Picamera2()
    config = picam.create_video_configuration(
        main={"size":RESOLUTION, "format":"RGB888"}
    )
    picam.configure(config)
    picam.set_controls({"FrameRate":FPS})
    picam.start()
    time.sleep(1)
    encode_param = [int(cv2.IMWRITE_JPEG_QUALITY), JPEG_QUALITY]
    
    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    print(f"[VIDEO] Start sending to {SERVER_IP}:{UDP_VIDEO_PORT}; l_i: {log_index}")
    log_index += 1
    frame_id = 0
    
    while True:
        array = picam.capture_array()
        
        # 压缩为JPEG
        _, jpeg_data = cv2.imencode('.jpg', array, encode_param)
        frame_data = jpeg_data.tobytes()

        # 帧数据分片
        chunks = [frame_data[i:i+CHUNK_SIZE] for i in range(0, len(frame_data), CHUNK_SIZE)]
        total_chunks = len(chunks)
        
        # 发送帧标识和帧分片
        for idx, chunk in enumerate(chunks):
            header = struct.pack('>IHH',  # 大端
                                 frame_id, 
                                 total_chunks, 
                                 idx
                                 )  
            sock.sendto(header + chunk, (SERVER_IP, UDP_VIDEO_PORT))
        
        frame_id += 1

# 接收控制数据
def recv_cmd():
    global log_index
    data, addr = sock.recvfrom(32)
    cmd = data.decode("utf-8").strip()
    print(f"[CTRL] Received cmd: '{cmd}' from {addr}; l_i: {log_index}")
    log_index += 1

    ser_cmd = '@' + cmd + '*'  # 加上串口指令首部尾部
    ser.write(ser_cmd.encode("utf-8"))
    ser.flush()
    print(f"[SERIAL] Sent to STM32: {ser_cmd}; l_i: {log_index}")
    log_index += 1

def main():
    if SEND_VIDEO:
        # 视频流发送子线程
        t_video = threading.Thread(target=video_stream_thread, daemon=True)
        t_video.start()

    # 主线程接收控制数据
    while True:
        recv_cmd()

if __name__ == "__main__":
    main()
    