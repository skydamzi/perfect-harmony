using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

public class MultiplayerHost : MonoBehaviour
{
    [Header("Host Settings")]
    public MultiplayerManager multiplayerManager;
    
    // Store connected clients and their endpoints
    private Dictionary<string, IPEndPoint> clientEndpoints = new Dictionary<string, IPEndPoint>();

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }

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
        
        // MultiplayerHost is legacy in central server architecture
        Debug.Log("MultiplayerHost (Legacy) initialized. In central server mode, this component is dormant.");
        
        // Subscribe to UDP manager events (optional, for local debugging)
        if (UDPManager.Instance != null)
        {
            // UDPManager.Instance.OnPacketReceived += HandlePacketReceived;
        }

        // Heartbeat not needed for clients in central server mode
        // StartCoroutine(HeartbeatRoutine());
    }

    private System.Collections.IEnumerator HeartbeatRoutine()
    {
        yield break;
    }

    private void OnDestroy()
    {
        if (UDPManager.Instance != null)
        {
            // UDPManager.Instance.OnPacketReceived -= HandlePacketReceived;
        }
    }

    // Handle packets received by UDPManager
    private void HandlePacketReceived(MessagePacket packet, IPEndPoint senderEndpoint)
    {
        // Legacy: Central server handles relaying now.
    }

    // Process packets on the server side
    private void ProcessServerPacket(MessagePacket packet, IPEndPoint senderEndpoint)
    {
        // Legacy: Central server handles relaying now.
    }

    // Broadcast message - Redirected to central server
    public void BroadcastToAll(MessagePacket packet)
    {
        if (UDPManager.Instance != null)
        {
            packet.roomId = multiplayerManager.currentRoomId;
            UDPManager.Instance.SendPacket(packet);
        }
    }

    private System.Collections.IEnumerator BroadcastGameStartRoutine(MessagePacket packet)
    {
        yield break;
    }

    // Broadcast message except one client - Redirected to central server
    public void BroadcastToAllExcept(MessagePacket packet, string excludedClientId)
    {
        if (UDPManager.Instance != null)
        {
            packet.roomId = multiplayerManager.currentRoomId;
            UDPManager.Instance.SendPacket(packet);
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
}