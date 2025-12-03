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
                    foreach (Action<MessagePacket, IPEndPoint> handler in OnPacketReceived.GetInvocationList())
                    {
                        try
                        {
                            handler(info.packet, info.sender);
                        }
                        catch (Exception e)
                        {
                            Debug.LogError($"Error in packet handler ({handler.Method.Name}): {e}");
                        }
                    }
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
            
            // Fix for Windows UDP SIO_UDP_CONNRESET (10054 error)
            if (Application.platform == RuntimePlatform.WindowsEditor || Application.platform == RuntimePlatform.WindowsPlayer)
            {
                const int SIO_UDP_CONNRESET = -1744830452;
                try { udpClient.Client.IOControl(SIO_UDP_CONNRESET, new byte[] { 0 }, null); } catch {}
            }

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
            
            // Fix for Windows UDP SIO_UDP_CONNRESET (10054 error)
            if (Application.platform == RuntimePlatform.WindowsEditor || Application.platform == RuntimePlatform.WindowsPlayer)
            {
                const int SIO_UDP_CONNRESET = -1744830452;
                try { udpClient.Client.IOControl(SIO_UDP_CONNRESET, new byte[] { 0 }, null); } catch {}
            }

            // Note: We DO NOT use Connect() anymore. We will send to 'serverIP' explicitly.
            // This avoids "socket already connected" or "socket not connected" confusion.
            
            isRunning = true;
            receiveThread = new Thread(new ThreadStart(ReceiveLoop));
            receiveThread.IsBackground = true;
            receiveThread.Start();
            Debug.Log($"UDP Client started (Targeting Server: {serverIP}:{port})");
            
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
                    // Debug.Log($"[UDP] Raw: {json}"); // Enable if needed
                    MessagePacket packet = JsonUtility.FromJson<MessagePacket>(json);
                    
                    if (packet.type != PacketType.Ping)
                        Debug.Log($"[UDP] Received {packet.type} from {remoteEP}");

                    // Enqueue the packet safely to be processed on the main thread
                    lock (queueLock)
                    {
                        packetQueue.Enqueue(new PacketInfo { packet = packet, sender = remoteEP });
                    }
                }
            }
            catch (SocketException se)
            {
                // Ignore ConnectionReset (10054)
                if (se.SocketErrorCode == SocketError.ConnectionReset) continue;
                if (isRunning) Debug.LogError($"Socket Error: {se.Message}");
            }
            catch (Exception e)
            {
                if (isRunning) Debug.LogError($"Error receiving UDP packet: {e.Message}");
            }
        }
    }

    // Send a packet (Client -> Server, or fallback)
    public void SendPacket(MessagePacket packet)
    {
        if (udpClient != null)
        {
            try
            {
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(packet));
                
                // Since we are not Connected, we MUST specify the destination.
                // For SendPacket(), the destination is the 'serverIP'.
                IPEndPoint serverEp = new IPEndPoint(IPAddress.Parse(serverIP), port);
                udpClient.Send(bytes, bytes.Length, serverEp);
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
                if (packet.type != PacketType.Ping)
                    Debug.Log($"Sending {packet.type} to {endpoint.Address}:{endpoint.Port}");
                
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(packet));
                // Always use the endpoint version as our socket is now always unconnected
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