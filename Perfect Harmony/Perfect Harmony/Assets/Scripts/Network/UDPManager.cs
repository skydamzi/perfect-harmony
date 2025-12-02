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

    // Struct to hold packet and sender info
    private struct PacketInfo
    {
        public MessagePacket packet;
        public IPEndPoint sender;
    }

    // Queue to store packets received from the background thread
    private Queue<PacketInfo> packetQueue = new Queue<PacketInfo>();
    private object queueLock = new object();

    [Header("Network Settings")]
    public string serverIP = "127.0.0.1"; // Default to localhost
    public int port = 8080;
    public bool isServer = false;

    public Action<MessagePacket, IPEndPoint> OnPacketReceived;

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
        }
    }

    private void Start()
    {
        if (isServer)
        {
            StartServer();
        }
        else
        {
            StartClient();
        }
    }

    private void Update()
    {
        // Process packets on the main thread
        lock (queueLock)
        {
            while (packetQueue.Count > 0)
            {
                PacketInfo info = packetQueue.Dequeue();
                if (OnPacketReceived != null)
                {
                    OnPacketReceived(info.packet, info.sender);
                }
            }
        }
    }

    // Initialize and start server
    public void InitializeServer()
    {
        StopConnection();
        isServer = true;
        StartServer();
    }

    // Initialize and start client with specific IP
    public void InitializeClient(string ip)
    {
        StopConnection();
        serverIP = ip;
        isServer = false;
        StartClient();
    }

    // Start the UDP server
    private void StartServer()
    {
        try
        {
            udpClient = new UdpClient(port);
            isRunning = true;
            receiveThread = new Thread(new ThreadStart(ReceiveLoop));
            receiveThread.IsBackground = true;
            receiveThread.Start();
            Debug.Log($"UDP Server started on port {port}");
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to start UDP server: {e.Message}");
        }
    }

    // Start the UDP client
    private void StartClient()
    {
        try
        {
            udpClient = new UdpClient();
            // Bind to any available port, not just an ephemeral one, to ensure we can receive replies
            // But for clients, typically we don't bind to a specific port unless necessary.
            // However, udpClient.Connect() implicitly binds.
            
            udpClient.Connect(serverIP, port);
            isRunning = true;
            receiveThread = new Thread(new ThreadStart(ReceiveLoop));
            receiveThread.IsBackground = true;
            receiveThread.Start();
            Debug.Log($"UDP Client connected to {serverIP}:{port}");
            
            // Send connection packet to server
            SendPacket(new MessagePacket(PacketType.Connect, GetPlayerId(), null));
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to start UDP client: {e.Message}");
        }
    }

    // Main receive loop
    private void ReceiveLoop()
    {
        while (isRunning)
        {
            try
            {
                IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
                byte[] data = udpClient.Receive(ref remoteEP);
                
                if (data.Length > 0)
                {
                    string json = System.Text.Encoding.UTF8.GetString(data);
                    MessagePacket packet = JsonUtility.FromJson<MessagePacket>(json);
                    
                    // Enqueue the packet safely to be processed on the main thread
                    lock (queueLock)
                    {
                        packetQueue.Enqueue(new PacketInfo { packet = packet, sender = remoteEP });
                    }
                }
            }
            catch (Exception e)
            {
                if (isRunning) // Only log error if we're still supposed to be running
                {
                    Debug.LogError($"Error receiving UDP packet: {e.Message}");
                }
            }
        }
    }

    // Send a packet (Client -> Server, or Server -> Connected Client if connected)
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

    // Send a packet to a specific endpoint (Server -> Specific Client)
    public void SendPacketTo(MessagePacket packet, IPEndPoint endpoint)
    {
        if (udpClient != null)
        {
            try
            {
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(packet));
                udpClient.Send(bytes, bytes.Length, endpoint);
            }
            catch (Exception e)
            {
                Debug.LogError($"Error sending UDP packet: {e.Message}");
            }
        }
    }

    // Get a unique player ID
    private string GetPlayerId()
    {
        if (MultiplayerManager.Instance != null && !string.IsNullOrEmpty(MultiplayerManager.Instance.localPlayerId))
        {
            return MultiplayerManager.Instance.localPlayerId;
        }
        // Fallback with random for local testing
        return SystemInfo.deviceUniqueIdentifier + "_" + UnityEngine.Random.Range(0, 10000);
    }

    // Stop the UDP connection
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
        
        Debug.Log("UDP connection stopped");
    }

    private void OnApplicationQuit()
    {
        StopConnection();
    }
}