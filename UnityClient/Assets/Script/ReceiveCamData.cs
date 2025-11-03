using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Net;
using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Concurrent;
using System.Threading;

public class ReceiveCamData : MonoBehaviour
{
    [Header("配置文本组件")]
    public Text MsgResvPortText;

    [Header("视频流接收")]
    private int MSG_RECEIVE_PORT = 00000; // Unity监听frp服务器端口
    public RawImage display;             
    private Texture2D texture;

    private UdpClient udpReceiveClient;  // 视频流接收UDP客户端
    private IPEndPoint remoteIp = null;  
    private const uint MAGIC_NUMBER = 0xEAEAEFEF;  // 帧头标识
    private int expectedLength = 0;      // 预测帧长
    private byte[] buffer = new byte[0]; // 帧处理缓存
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
        texture = new Texture2D(128, 64, TextureFormat.RGB24, false);
        texture.Apply();
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
    private void ProcessReceiveData(byte[] data)
    {
        try
        {
            if (expectedLength == 0 && data.Length >= 8)  // 检测帧头
            {
                uint magic = BitConverter.ToUInt32(data, 0);
                if (magic == MAGIC_NUMBER)
                {
                    expectedLength = BitConverter.ToInt32(data, 4);
                    buffer = new byte[0];
                    //Debug.Log($"检测到帧头，预期帧长度: {expectedLength} 字节");
                }
                else
                {
                    //Debug.LogWarning($"无效帧头: {magic}, 丢弃 {data.Length} 字节");
                }
            }
            else if (expectedLength > 0)  // 拼接帧数据
            {
                buffer = Combine(buffer, data);
                //Debug.Log($"当前缓冲大小: {buffer.Length} 字节");

                // 帧数据完整后渲染
                if (buffer.Length >= expectedLength)
                {
                    texture.LoadRawTextureData(buffer);
                    texture.Apply();
                    //Debug.Log($"完整帧接收完成，显示图像，大小: {buffer.Length} 字节");
                    expectedLength = 0;
                    buffer = new byte[0];
                    frameCount++;
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"数据处理错误:{e.Message}");
        }
    }

    // 字节数组合并
    private byte[] Combine(byte[] a, byte[] b)
    {
        byte[] c = new byte[a.Length + b.Length];
        Buffer.BlockCopy(a, 0, c, 0, a.Length);
        Buffer.BlockCopy(b, 0, c, a.Length, b.Length);
        return c;
    }
}
