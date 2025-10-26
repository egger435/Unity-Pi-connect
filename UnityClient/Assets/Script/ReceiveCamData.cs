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
    [Header("��Ƶ������")]
    public int MSG_RECEIVE_PORT; // Unity����frp�������˿�
    public RawImage display;             
    private Texture2D texture;

    private UdpClient udpReceiveClient;  // ��Ƶ������UDP�ͻ���
    private IPEndPoint remoteIp = null;  
    private const uint MAGIC_NUMBER = 0xEAEAEFEF;  // ֡ͷ��ʶ
    private int expectedLength = 0;      // Ԥ��֡��
    private byte[] buffer = new byte[0]; // ֡������
    private ConcurrentQueue<byte[]> dataQueue = new ConcurrentQueue<byte[]>(); // ���ڴ洢���յ�������Ƭ�ε��̰߳�ȫ����
    private Thread recvThread;

    // ���б�ʶ
    private bool gettingVidStream = false;
    private bool isThreadRunning = false;

    // ֡�ʼ���
    private float frameTimer;
    private int frameCount;
    private float currentFps;

    [Header("��־����Ϣ")]
    public Text logText;
    public Text fpsText;
    public bool showFps;

    void Start()
    {
        logText.text += "\n" + "����UDP���տͻ���...";
        udpReceiveClient = new UdpClient(MSG_RECEIVE_PORT);

        // ��ͼ��ʼ��
        texture = new Texture2D(128, 64, TextureFormat.RGB24, false);
        texture.Apply();
        display.texture = texture;

        // ֡����ʾ��ʼ��
        if (fpsText != null && showFps)
            fpsText.text = "FPS: 0";
    }

    private void FixedUpdate()
    {
        // ʵʱ������Ƶ����ʾ����
        if (texture != null && gettingVidStream)
        {
            display.texture = texture;
        }
    }

    // �����߳��ж�ȡ���ݲ�����
    void Update()
    {
        if (!gettingVidStream) return;

        while (dataQueue.TryDequeue(out byte[] data))
        {
            ProcessReceiveData(data);
        }

        // ����֡��
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

    // �󶨰�ť�¼�����ʼ������Ƶ��
    public void StartGetVidStream()
    {
        if (gettingVidStream) return;

        // �����߳����б�ʶ
        logText.text += "\n" + "��ʼ������ݮ����Ƶ��...";
        gettingVidStream = true;
        isThreadRunning = true;

        // ���������߳�
        recvThread = new Thread(ReceiveUDPdata);
        recvThread.IsBackground = true;
        recvThread.Start();
    }

    // ���߳̽���UDP����
    private void ReceiveUDPdata()
    {
        while (isThreadRunning)
        {
            try
            {
                byte[] data = udpReceiveClient.Receive(ref remoteIp);
                if (data != null && data.Length > 0)
                {
                    dataQueue.Enqueue(data);  // �����յ���UDP���ݷ������
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"���߳̽��մ���:{e.Message}");
            }
        }
    }

    // ������յ�������
    private void ProcessReceiveData(byte[] data)
    {
        try
        {
            if (expectedLength == 0 && data.Length >= 8)  // ���֡ͷ
            {
                uint magic = BitConverter.ToUInt32(data, 0);
                if (magic == MAGIC_NUMBER)
                {
                    expectedLength = BitConverter.ToInt32(data, 4);
                    buffer = new byte[0];
                    //Debug.Log($"��⵽֡ͷ��Ԥ��֡����: {expectedLength} �ֽ�");
                }
                else
                {
                    //Debug.LogWarning($"��Ч֡ͷ: {magic}, ���� {data.Length} �ֽ�");
                }
            }
            else if (expectedLength > 0)  // ƴ��֡����
            {
                buffer = Combine(buffer, data);
                //Debug.Log($"��ǰ�����С: {buffer.Length} �ֽ�");

                // ֡������������Ⱦ
                if (buffer.Length >= expectedLength)
                {
                    texture.LoadRawTextureData(buffer);
                    texture.Apply();
                    //Debug.Log($"����֡������ɣ���ʾͼ�񣬴�С: {buffer.Length} �ֽ�");
                    expectedLength = 0;
                    buffer = new byte[0];
                    frameCount++;
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"���ݴ������:{e.Message}");
        }
    }

    // �ֽ�����ϲ�
    private byte[] Combine(byte[] a, byte[] b)
    {
        byte[] c = new byte[a.Length + b.Length];
        Buffer.BlockCopy(a, 0, c, 0, a.Length);
        Buffer.BlockCopy(b, 0, c, a.Length, b.Length);
        return c;
    }
}
