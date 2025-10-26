using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Net;
using System.Net.Sockets;
using UnityEngine.UI;
using System.Threading;
using System.Diagnostics;
using System.IO;

public class UDPCtrl : MonoBehaviour
{
    [Header("FRP服务器IP")]
    public string serverIP;

    [Header("UDP消息发送")]
    public int MSG_SEND_PORT;  // unity向frp服务器发送消息端口
    private UdpClient udpSendClient;

    private readonly string frpSetupName = "frpc_setup.bat";

    [Header("UI控件")]
    public Text sendText;
    public Text logText;

    private string appPath;
    private string rootDir;

    void Start()
    {
        // udp发送客户端初始化
        logText.text += "\n" + "正在建立UDP通信...";
        SendClientInit();
    }

    // 启动frp客户端bat文件
    public void FRPClientBatStart()
    {
        logText.text += "\n" + "启动frp客户端，请查看控制台...启动后请勿关闭客户端";
        try
        {
            appPath = Application.dataPath;
            rootDir = Path.GetDirectoryName(appPath);

            string batPath = Path.Combine(rootDir, frpSetupName);

            if (!File.Exists(batPath))
            {
                UnityEngine.Debug.LogError($"批处理文件不存在：{batPath}");
                logText.text += "\n" + $"批处理文件不存在：{batPath}";
                return;
            }

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = batPath, 
                WorkingDirectory = rootDir, 
                UseShellExecute = true, 
                CreateNoWindow = false 
                // 若需隐藏窗口，可设置为：
                // CreateNoWindow = true,
                // UseShellExecute = false
            };

            Process process = Process.Start(startInfo);
            if (process != null)
            {
                UnityEngine.Debug.Log($"成功启动批处理：{batPath}");
                UnityEngine.Debug.Log("FRP客户端已建立");
            }
            else
            {
                UnityEngine.Debug.LogError("启动批处理失败，进程为null");
            }
        }
        catch (System.Exception ex)
        {
            UnityEngine.Debug.LogError($"启动批处理出错：{ex.Message}");
        }
    }

    // udp发送客户端初始化
    private void SendClientInit()
    {
        try
        {
            IPAddress[] addresses = Dns.GetHostAddresses(serverIP);
            IPAddress ipv4Address = null;
            foreach (var addr in addresses)
            {
                UnityEngine.Debug.Log("解析地址: " + addr + " (" + addr.AddressFamily + ")");
                if (addr.AddressFamily == AddressFamily.InterNetwork)
                {
                    ipv4Address = addr;
                    break;
                }
            }
            if (ipv4Address != null)
            {
                IPEndPoint endPoint = new IPEndPoint(ipv4Address, MSG_SEND_PORT);
                udpSendClient = new UdpClient(AddressFamily.InterNetwork);
                UnityEngine.Debug.Log("UDP 连接到: " + endPoint.Address + ":" + endPoint.Port);
                logText.text = "UDP 连接到: " + endPoint.Address + ":" + endPoint.Port;
            }
            else
            {
                UnityEngine.Debug.LogError("未找到 IPv4 地址: " + serverIP);
                logText.text = "未找到 IPv4 地址: " + serverIP;
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("UDP send start failed:" + e.Message);
            logText.text = "UDP send start failed:" + e.Message;
        }
    }

    // 发送命令
    public void SendCommand()
    {
        try
        {
            IPEndPoint endPoint = new IPEndPoint(IPAddress.Parse(serverIP), MSG_SEND_PORT);
            byte[] data = System.Text.Encoding.UTF8.GetBytes(sendText.text);
            udpSendClient.Send(data, data.Length, endPoint);
            UnityEngine.Debug.Log("send:" + sendText.text);
            logText.text = logText.text + "\nsend:" + sendText.text;
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("UDP send failed:" + e.Message);
            logText.text = "UDP send failed:" + e.Message;
        }
    }
}
