using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

public class MultiplayerHost : MonoBehaviour
{
    [Header("Host Settings")]
    public MultiplayerManager multiplayerManager;
    
    private UdpClient serverUdpClient;
    private Dictionary<string, IPEndPoint> clientEndpoints = new Dictionary<string, IPEndPoint>();
    private bool isServerRunning = false;
    
    private Thread serverThread;

    private void Start()
    {
        // Get or create multiplayer manager
        if (multiplayerManager == null)
        {
            multiplayerManager = FindFirstObjectByType<MultiplayerManager>();
            if (multiplayerManager == null)
            {
                GameObject mpManagerObj = new GameObject("MultiplayerManager");
                multiplayerManager = mpManagerObj.AddComponent<MultiplayerManager>();
            }
        }
        
        multiplayerManager.isHost = true;
        
        StartServer();
    }

    private void StartServer()
    {
        try
        {
            serverUdpClient = new UdpClient(multiplayerManager.udpManager.port);
            isServerRunning = true;
            
            serverThread = new Thread(new ThreadStart(ServerReceiveLoop));
            serverThread.IsBackground = true;
            serverThread.Start();
            
            Debug.Log($"Multiplayer Host server started on port {multiplayerManager.udpManager.port}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to start multiplayer host server: {e.Message}");
        }
    }

    // Server's receive loop to handle all client connections
    private void ServerReceiveLoop()
    {
        while (isServerRunning)
        {
            try
            {
                IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
                byte[] data = serverUdpClient.Receive(ref remoteEP);
                MessagePacket packet = JsonUtility.FromJson<MessagePacket>(System.Text.Encoding.UTF8.GetString(data));
                
                // Store client endpoint if it's a new connection
                if (!clientEndpoints.ContainsKey(packet.playerId))
                {
                    clientEndpoints[packet.playerId] = remoteEP;
                    Debug.Log($"New client connected: {packet.playerId} at {remoteEP}");
                }
                
                // Process the packet based on type
                ProcessServerPacket(packet, remoteEP);
            }
            catch (System.Exception e)
            {
                if (isServerRunning)
                {
                    Debug.LogError($"Error in server receive loop: {e.Message}");
                }
            }
        }
    }

    // Process packets on the server side
    private void ProcessServerPacket(MessagePacket packet, IPEndPoint senderEndpoint)
    {
        switch (packet.type)
        {
            case PacketType.Connect:
                // Already handled by endpoint storage
                BroadcastToAllExcept(packet, packet.playerId);
                break;
                
            case PacketType.PlayerInput:
                // Relay player input to all other players
                BroadcastToAllExcept(packet, packet.playerId);
                break;
                
            case PacketType.PlayerScore:
                // Relay score update to all players
                BroadcastToAll(packet);
                break;
                
            case PacketType.NoteHit:
            case PacketType.NoteMiss:
                // Relay note result to all players
                BroadcastToAllExcept(packet, packet.playerId);
                break;
                
            case PacketType.Disconnect:
                // Handle disconnection
                if (clientEndpoints.ContainsKey(packet.playerId))
                {
                    clientEndpoints.Remove(packet.playerId);
                }
                BroadcastToAll(packet);
                break;
                
            default:
                // Relay other packets to all players
                BroadcastToAllExcept(packet, packet.playerId);
                break;
        }
    }

    // Broadcast message to all connected clients
    private void BroadcastToAll(MessagePacket packet)
    {
        List<string> disconnectedClients = new List<string>();
        
        foreach (var kvp in clientEndpoints)
        {
            try
            {
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(packet));
                serverUdpClient.Send(bytes, bytes.Length, kvp.Value);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to send packet to {kvp.Key}: {e.Message}");
                disconnectedClients.Add(kvp.Key);
            }
        }
        
        // Remove disconnected clients
        foreach (string clientId in disconnectedClients)
        {
            if (clientEndpoints.ContainsKey(clientId))
            {
                clientEndpoints.Remove(clientId);
            }
        }
    }

    // Broadcast message to all except one client
    public void BroadcastToAllExcept(MessagePacket packet, string excludedClientId)
    {
        List<string> disconnectedClients = new List<string>();

        foreach (var kvp in clientEndpoints)
        {
            if (kvp.Key != excludedClientId)
            {
                try
                {
                    byte[] bytes = System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(packet));
                    serverUdpClient.Send(bytes, bytes.Length, kvp.Value);
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Failed to send packet to {kvp.Key}: {e.Message}");
                    disconnectedClients.Add(kvp.Key);
                }
            }
        }

        // Remove disconnected clients
        foreach (string clientId in disconnectedClients)
        {
            if (clientEndpoints.ContainsKey(clientId))
            {
                clientEndpoints.Remove(clientId);
            }
        }
    }

    // Start the game for all players
    public void StartGameForAllPlayers()
    {
        if (multiplayerManager.HasRequiredPlayers())
        {
            MessagePacket packet = new MessagePacket(PacketType.GameStart, SystemInfo.deviceUniqueIdentifier, null);
            BroadcastToAll(packet);
        }
        else
        {
            Debug.LogWarning("Not enough players to start the game!");
        }
    }

    // Stop the game for all players
    public void StopGameForAllPlayers()
    {
        MessagePacket packet = new MessagePacket(PacketType.GameStop, SystemInfo.deviceUniqueIdentifier, null);
        BroadcastToAll(packet);
    }

    // Stop the server
    public void StopServer()
    {
        isServerRunning = false;
        
        if (serverThread != null && serverThread.IsAlive)
        {
            serverThread.Abort();
            serverThread = null;
        }
        
        if (serverUdpClient != null)
        {
            serverUdpClient.Close();
            serverUdpClient = null;
        }
        
        Debug.Log("Multiplayer Host server stopped");
    }

    private void OnApplicationQuit()
    {
        StopServer();
    }
}