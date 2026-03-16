using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Net;
using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.IO;

// 帧缓冲类，用于存储分片数据和帧信息
class FrameBuffer
{
    public int totalChunks;
    public byte[][] chunks;
    public int receivedChunks;
    public float createTime;
}

public class ReceiveCamData : MonoBehaviour
{
    [Header("配置文本组件")]
    public Text MsgResvPortText;

    [Header("视频流接收")]
    private int MSG_RECEIVE_PORT = 13300; // Unity监听frp服务器端口, 与frpc.toml中的localPort一致
    public RawImage display;             
    private Texture2D texture;

    private UdpClient udpReceiveClient;
    private IPEndPoint remoteIp = null;
    private Dictionary<int, FrameBuffer> frameBuffers = new Dictionary<int, FrameBuffer>();  // 存储帧数据字典
    private float frameTimeout = 1f;  // 帧数据接收超时时间1s
    private ConcurrentQueue<byte[]> dataQueue = new ConcurrentQueue<byte[]>(); // 用于存储接收到的数据片段的线程安全队列
    private Thread recvThread;

    // 运行标识
    private bool gettingVidStream = false;
    private bool isThreadRunning = false;

    // 帧率计算
    private float frameTimer;
    private int frameCount;
    private float currentFps;

    [Header("日志与信息")]
    public Text logText;
    public Text fpsText;
    public bool showFps;

    void Start()
    {
        // 贴图初始化
        texture = new Texture2D(1024, 512, TextureFormat.RGB24, false, true);
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.Apply(false);
        display.texture = texture;

        // 帧率显示初始化
        if (fpsText != null && showFps)
            fpsText.text = "FPS: 0";
    }

    private void FixedUpdate()
    {
        // 实时更新视频流显示画面
        if (texture != null && gettingVidStream)
        {
            display.texture = texture;
        }
    }

    // 在主线程中读取数据并处理
    void Update()
    {
        if (!gettingVidStream) return;

        while (dataQueue.TryDequeue(out byte[] data))
        {
            ProcessReceiveData(data);
        }

        // 计算帧率
        if (!showFps) return;
        frameTimer += Time.deltaTime;
        if (frameTimer >= 0.5f)
        {
            currentFps = (float)(frameCount / 0.5);
            frameCount = 0;
            frameTimer = 0;

            fpsText.text = $"FPS: {currentFps:F1}";
        }
    }

    private void OnApplicationQuit()
    {
        gettingVidStream = false;
        isThreadRunning = false;
        recvThread?.Join(1000);
        udpReceiveClient?.Close();
    }

    // 配置确认
    public void ResvSetupConfirm()
    {
        if (MsgResvPortText == null)
            return;

        MSG_RECEIVE_PORT = int.Parse(MsgResvPortText.text);
    }

    // 建立接收客户端
    public void SetupStart()
    {
        logText.text += "\n" + "建立UDP接收客户端...";
        udpReceiveClient = new UdpClient(MSG_RECEIVE_PORT);
        udpReceiveClient.Client.ReceiveBufferSize = 1024 * 1024;
    }

    // 绑定按钮事件，开始接收视频流
    public void StartGetVidStream()
    {
        if (gettingVidStream) return;

        // 更新线程运行标识
        logText.text += "\n" + "开始接收树莓派视频流...";
        gettingVidStream = true;
        isThreadRunning = true;

        // 启动接收线程
        recvThread = new Thread(ReceiveUDPdata);
        recvThread.IsBackground = true;
        recvThread.Priority = System.Threading.ThreadPriority.AboveNormal;
        recvThread.Start();
    }

    // 子线程接收UDP数据
    private void ReceiveUDPdata()
    {
        while (isThreadRunning)
        {
            try
            {
                byte[] data = udpReceiveClient.Receive(ref remoteIp);
                if (data != null && data.Length > 0)
                {
                    dataQueue.Enqueue(data);  // 将接收到的UDP数据放入队列
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"子线程接收错误:{e.Message}");
            }
        }
    }

    // 处理接收到的数据
    private void ProcessReceiveData(byte[] packet)
    {
        try
        {
            if (packet.Length < 8) return;

            // 解析帧ID、总分片数和分片ID
            int frameID = (packet[0] << 24) | (packet[1] << 16) | (packet[2] << 8) | packet[3];
            int totalChunks = (packet[4] << 8) | packet[5];
            int chunkIndex = (packet[6] << 8) | packet[7];

            // 提取分片数据
            byte[] data = new byte[packet.Length - 8];
            Array.Copy(packet, 8, data, 0, data.Length);

            // 删去超时帧数据
            List<int> toDelete = new List<int>();
            foreach (var kvp in frameBuffers)
            {
                if (Time.realtimeSinceStartup - kvp.Value.createTime > frameTimeout)
                {
                    toDelete.Add(kvp.Key);
                }
            }
            foreach (int id in toDelete)
            {
                frameBuffers.Remove(id);
            }

            // 若是新的帧则创建帧缓冲
            if (!frameBuffers.ContainsKey(frameID))
            {
                frameBuffers[frameID] = new FrameBuffer
                {
                    totalChunks = totalChunks,
                    chunks = new byte[totalChunks][],
                    receivedChunks = 0,
                    createTime = Time.realtimeSinceStartup
                };
            }

            // 处理分片数据
            FrameBuffer fb = frameBuffers[frameID];
            if (chunkIndex < 0 || chunkIndex >= totalChunks) return;

            if (fb.chunks[chunkIndex] != null) return;  // 已经接收过该分片

            fb.chunks[chunkIndex] = data;
            fb.receivedChunks++;

            // 已经接收完一帧数据, 进行合并和解码
            if (fb.receivedChunks == fb.totalChunks)
            {
                MemoryStream ms = new MemoryStream();
                for (int i = 0; i < totalChunks; i++)
                {
                    ms.Write(fb.chunks[i], 0, fb.chunks[i].Length);
                }

                byte[] fullFrame = ms.ToArray();
                frameBuffers.Remove(frameID);

                texture.LoadImage(fullFrame);
                texture.Apply(false);
                frameCount++;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"数据处理错误:{e.Message}");
        }
    }
}
