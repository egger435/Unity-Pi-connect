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
    [Header("FRP������IP")]
    public string serverIP;

    [Header("UDP��Ϣ����")]
    public int MSG_SEND_PORT;  // unity��frp������������Ϣ�˿�
    private UdpClient udpSendClient;

    private readonly string frpSetupName = "frpc_setup.bat";

    [Header("UI�ؼ�")]
    public Text sendText;
    public Text logText;

    private string appPath;
    private string rootDir;

    void Start()
    {
        // udp���Ϳͻ��˳�ʼ��
        logText.text += "\n" + "���ڽ���UDPͨ��...";
        SendClientInit();
    }

    // ����frp�ͻ���bat�ļ�
    public void FRPClientBatStart()
    {
        logText.text += "\n" + "����frp�ͻ��ˣ���鿴����̨...����������رտͻ���";
        try
        {
            appPath = Application.dataPath;
            rootDir = Path.GetDirectoryName(appPath);

            string batPath = Path.Combine(rootDir, frpSetupName);

            if (!File.Exists(batPath))
            {
                UnityEngine.Debug.LogError($"�������ļ������ڣ�{batPath}");
                logText.text += "\n" + $"�������ļ������ڣ�{batPath}";
                return;
            }

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = batPath, 
                WorkingDirectory = rootDir, 
                UseShellExecute = true, 
                CreateNoWindow = false 
                // �������ش��ڣ�������Ϊ��
                // CreateNoWindow = true,
                // UseShellExecute = false
            };

            Process process = Process.Start(startInfo);
            if (process != null)
            {
                UnityEngine.Debug.Log($"�ɹ�����������{batPath}");
                UnityEngine.Debug.Log("FRP�ͻ����ѽ���");
            }
            else
            {
                UnityEngine.Debug.LogError("����������ʧ�ܣ�����Ϊnull");
            }
        }
        catch (System.Exception ex)
        {
            UnityEngine.Debug.LogError($"�������������{ex.Message}");
        }
    }

    // udp���Ϳͻ��˳�ʼ��
    private void SendClientInit()
    {
        try
        {
            IPAddress[] addresses = Dns.GetHostAddresses(serverIP);
            IPAddress ipv4Address = null;
            foreach (var addr in addresses)
            {
                UnityEngine.Debug.Log("������ַ: " + addr + " (" + addr.AddressFamily + ")");
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
                UnityEngine.Debug.Log("UDP ���ӵ�: " + endPoint.Address + ":" + endPoint.Port);
                logText.text = "UDP ���ӵ�: " + endPoint.Address + ":" + endPoint.Port;
            }
            else
            {
                UnityEngine.Debug.LogError("δ�ҵ� IPv4 ��ַ: " + serverIP);
                logText.text = "δ�ҵ� IPv4 ��ַ: " + serverIP;
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("UDP send start failed:" + e.Message);
            logText.text = "UDP send start failed:" + e.Message;
        }
    }

    // ��������
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
