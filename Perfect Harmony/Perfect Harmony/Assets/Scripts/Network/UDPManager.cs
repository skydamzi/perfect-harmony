using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

public class UDPManager : MonoBehaviour
{
    public static UDPManager Instance { get; private set; }

    private UdpClient udpClient;
    private Thread receiveThread;
    private bool isRunning = false;

    [Header("Network Settings")]
    public string serverIP = "127.0.0.1"; // Default to localhost
    public int port = 8080;
    public bool isServer = false;

    public Action<MessagePacket> OnPacketReceived;

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
                MessagePacket packet = JsonUtility.FromJson<MessagePacket>(System.Text.Encoding.UTF8.GetString(data));
                
                // Invoke the callback on the main thread
                if (OnPacketReceived != null)
                {
                    OnPacketReceived(packet);
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

    // Send a packet
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

    // Send a packet to a specific endpoint (for server use)
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
        return SystemInfo.deviceUniqueIdentifier; // Use device ID as player ID
    }

    // Stop the UDP connection
    public void StopConnection()
    {
        isRunning = false;
        
        if (receiveThread != null && receiveThread.IsAlive)
        {
            receiveThread.Abort(); // Note: Abort is not recommended in production, but acceptable for prototype
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