using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Collections.Generic;
using UnityEngine;

public class UDPManager : MonoBehaviour
{
    public static UDPManager Instance { get; private set; }

    private UdpClient udpClient;
    private Thread receiveThread;
    private bool isRunning = false;
    private System.Diagnostics.Stopwatch netStopwatch;
    private double baselineOffset; // Time.realtimeSinceStartup at the moment stopwatch started

    // Struct to hold raw byte data and sender info
    private struct PacketInfo
    {
        public byte[] rawData;
        public IPEndPoint sender;
        public double arrivalTimestamp; // Measured in the background thread
    }

    // Queue to store raw bytes received from the background thread
    private Queue<PacketInfo> packetQueue = new Queue<PacketInfo>();
    private object queueLock = new object();

    [Header("Network Settings")]
    public string serverIP = "116.127.190.78"; // External Central Server IP
    public int port = 8080;
    public bool isServer = false; // Always false for central server clients

    public Action<MessagePacket, IPEndPoint, double> OnPacketReceived;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            
            // Start thread-safe stopwatch for high-precision timing
            netStopwatch = new System.Diagnostics.Stopwatch();
            netStopwatch.Start();
            baselineOffset = (double)Time.realtimeSinceStartup;
        }
    }

    private void Start()
    {
        StartClient();
    }

    private void Update()
    {
        // Process packets on the main thread
        lock (queueLock)
        {
            while (packetQueue.Count > 0)
            {
                PacketInfo info = packetQueue.Dequeue();
                
                try
                {
                    string rawJson = System.Text.Encoding.UTF8.GetString(info.rawData).Replace("\0", "").Trim();
                    if (string.IsNullOrEmpty(rawJson)) continue;

                    // Support for multiple JSON objects in a single UDP datagram
                    int braceCount = 0;
                    int startIndex = 0;
                    for (int i = 0; i < rawJson.Length; i++)
                    {
                        if (rawJson[i] == '{') braceCount++;
                        else if (rawJson[i] == '}')
                        {
                            braceCount--;
                            if (braceCount == 0)
                            {
                                string singleJson = rawJson.Substring(startIndex, i - startIndex + 1);
                                ProcessSinglePacket(singleJson, info.sender, info.arrivalTimestamp);
                                startIndex = i + 1;
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[UDP] Packet Processing Error: {e.Message}");
                }
            }
        }
    }

    private void ProcessSinglePacket(string json, IPEndPoint sender, double arrivalTimestamp)
    {
        try
        {
            MessagePacket packet = JsonUtility.FromJson<MessagePacket>(json);
            if (packet != null && OnPacketReceived != null)
            {
                if (string.IsNullOrEmpty(packet.playerId)) return;
                OnPacketReceived(packet, sender, arrivalTimestamp);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[UDP] JSON Parse Fail: {e.Message} | Data: {json}");
        }
    }

    public void InitializeServer()
    {
        StopConnection();
        isServer = true;
        StartServer();
    }

    public void InitializeClient(string ip)
    {
        StopConnection();
        serverIP = ip;
        isServer = false;
        StartClient();
    }

    public void InitializeClient(string ip, int newPort)
    {
        StopConnection();
        serverIP = ip;
        port = newPort;
        isServer = false;
        StartClient();
    }

    private void StartServer()
    {
        try
        {
            udpClient = new UdpClient(port);
            if (Application.platform == RuntimePlatform.WindowsEditor || Application.platform == RuntimePlatform.WindowsPlayer)
            {
                const int SIO_UDP_CONNRESET = -1744830452;
                try { udpClient.Client.IOControl(SIO_UDP_CONNRESET, new byte[] { 0 }, null); } catch {}
            }

            isRunning = true;
            receiveThread = new Thread(new ThreadStart(ReceiveLoop));
            receiveThread.IsBackground = true;
            receiveThread.Start();
            Debug.Log($"UDP Local Server started on port {port}");
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to start UDP server: {e.Message}");
        }
    }

    private void StartClient()
    {
        try
        {
            udpClient = new UdpClient();
            if (Application.platform == RuntimePlatform.WindowsEditor || Application.platform == RuntimePlatform.WindowsPlayer)
            {
                const int SIO_UDP_CONNRESET = -1744830452;
                try { udpClient.Client.IOControl(SIO_UDP_CONNRESET, new byte[] { 0 }, null); } catch {}
            }

            udpClient.Connect(serverIP, port);
            isRunning = true;
            receiveThread = new Thread(new ThreadStart(ReceiveLoop));
            receiveThread.IsBackground = true;
            receiveThread.Start();
            Debug.Log($"UDP Client connected to Central Server: {serverIP}:{port}");
            
            string currentRoomId = (MultiplayerManager.Instance != null) ? MultiplayerManager.Instance.currentRoomId : "";
            SendPacket(new MessagePacket(PacketType.Connect, GetPlayerId(), currentRoomId, null));
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to start UDP client: {e.Message}");
        }
    }

    private void ReceiveLoop()
    {
        while (isRunning)
        {
            try
            {
                IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
                byte[] data = udpClient.Receive(ref remoteEP);
                
                if (data != null && data.Length > 0)
                {
                    // THREAD SAFETY: Use Stopwatch instead of Time.realtimeSinceStartup
                    double now = baselineOffset + netStopwatch.Elapsed.TotalSeconds;
                    lock (queueLock)
                    {
                        packetQueue.Enqueue(new PacketInfo { 
                            rawData = data, 
                            sender = remoteEP, 
                            arrivalTimestamp = now 
                        });
                    }
                }
            }
            catch (SocketException se)
            {
                if (se.SocketErrorCode == SocketError.ConnectionReset) continue; 
                if (isRunning) Debug.LogError($"Socket Error: {se.Message}");
            }
            catch (Exception e)
            {
                if (isRunning) Debug.LogError($"Error receiving UDP packet: {e.Message}");
            }
        }
    }

    public void SendPacket(MessagePacket packet)
    {
        if (udpClient != null)
        {
            try
            {
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(packet));
                udpClient.Send(bytes, bytes.Length);
            }
            catch (Exception e)
            {
                Debug.LogError($"Error sending UDP packet: {e.Message}");
            }
        }
    }

    public void SendPacketTo(MessagePacket packet, IPEndPoint endpoint)
    {
        if (udpClient != null)
        {
            try
            {
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(packet));
                if (isServer) udpClient.Send(bytes, bytes.Length, endpoint);
                else udpClient.Send(bytes, bytes.Length);
            }
            catch (Exception e)
            {
                Debug.LogError($"Error sending UDP packet: {e.Message}");
            }
        }
    }

    private string GetPlayerId()
    {
        if (MultiplayerManager.Instance != null && !string.IsNullOrEmpty(MultiplayerManager.Instance.localPlayerId))
        {
            return MultiplayerManager.Instance.localPlayerId;
        }
        return SystemInfo.deviceUniqueIdentifier + "_" + UnityEngine.Random.Range(0, 10000);
    }

    public void StopConnection()
    {
        isRunning = false;
        if (receiveThread != null && receiveThread.IsAlive)
        {
            receiveThread.Abort(); 
            receiveThread = null;
        }
        if (udpClient != null)
        {
            udpClient.Close();
            udpClient = null;
        }
    }

    private void OnApplicationQuit()
    {
        StopConnection();
    }
}
