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
        
        // Subscribe to UDP manager events
        if (UDPManager.Instance != null)
        {
            UDPManager.Instance.OnPacketReceived += HandlePacketReceived;
        }
    }

    private void OnDestroy()
    {
        if (UDPManager.Instance != null)
        {
            UDPManager.Instance.OnPacketReceived -= HandlePacketReceived;
        }
    }

    // Handle packets received by UDPManager
    private void HandlePacketReceived(MessagePacket packet, IPEndPoint senderEndpoint)
    {
        // Only process if we are the host
        if (!multiplayerManager.isHost) return;

        // Store client endpoint if it's a new connection
        if (!clientEndpoints.ContainsKey(packet.playerId))
        {
            clientEndpoints[packet.playerId] = senderEndpoint;
            Debug.Log($"New client connected: {packet.playerId} at {senderEndpoint}");
        }
        else
        {
            // Update endpoint just in case (e.g. port change)
            clientEndpoints[packet.playerId] = senderEndpoint;
        }
        
        // Process the packet
        ProcessServerPacket(packet, senderEndpoint);
    }

    // Process packets on the server side
    private void ProcessServerPacket(MessagePacket packet, IPEndPoint senderEndpoint)
    {
        switch (packet.type)
        {
            case PacketType.Connect:
                // Acknowledge connection to the sender so they know they are connected
                UDPManager.Instance.SendPacketTo(packet, senderEndpoint);
                
                // Notify other players about the new connection
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
    public void BroadcastToAll(MessagePacket packet)
    {
        foreach (var kvp in clientEndpoints)
        {
            UDPManager.Instance.SendPacketTo(packet, kvp.Value);
        }
    }

    // Broadcast message to all except one client
    public void BroadcastToAllExcept(MessagePacket packet, string excludedClientId)
    {
        foreach (var kvp in clientEndpoints)
        {
            if (kvp.Key != excludedClientId)
            {
                UDPManager.Instance.SendPacketTo(packet, kvp.Value);
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
}