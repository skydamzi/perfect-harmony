using System.Collections.Generic;
using UnityEngine;

public class TimingSyncManager : MonoBehaviour
{
    public static TimingSyncManager Instance { get; private set; }

    [Header("Timing Sync Settings")]
    public float syncInterval = 1.0f; // Send sync packets every second
    public float maxSyncHistory = 10; // Number of sync records to keep for calculation
    public float networkTimeOffset = 0f; // Calculated offset between local and server time
    public float packetExchangeLatency = 0f; // Round-trip time in milliseconds

    [Header("Rhythm Sync")]
    public float serverSongPosition = 0f;
    public float serverSongStartTime = 0f;
    public int serverCurrentBeat = 0;

    private List<SyncRecord> syncHistory = new List<SyncRecord>();
    
    private MultiplayerManager mpManager;
    private RhythmGameManager rhythmGameManager;

    private class SyncRecord
    {
        public float localTime; // When we sent/received the sync
        public float serverTime; // Server's time from the packet
        public float serverSongPosition; // Server's song position
        public int serverBeat; // Server's current beat

        public SyncRecord(float local, float server, float songPos, int beat)
        {
            localTime = local;
            serverTime = server;
            serverSongPosition = songPos;
            serverBeat = beat;
        }
    }

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
        mpManager = FindFirstObjectByType<MultiplayerManager>();
        rhythmGameManager = FindFirstObjectByType<RhythmGameManager>();

        if (mpManager != null)
        {
            mpManager.udpManager.OnPacketReceived += HandlePacketReceived;
        }

        // Start sync timer
        InvokeRepeating("SendSyncPacket", 0f, syncInterval);
        InvokeRepeating("SendPingPacket", 0.5f, 1.0f);
    }

    // Refresh references to scene objects (called by AutoSetup)
    public void RefreshReferences()
    {
        mpManager = FindFirstObjectByType<MultiplayerManager>();
        rhythmGameManager = FindFirstObjectByType<RhythmGameManager>();
        Debug.Log("TimingSyncManager references refreshed.");
    }

    // Send synchronization packet to server
    private void SendSyncPacket()
    {
        if (mpManager != null && mpManager.udpManager != null && !mpManager.isHost)
        {
            // Create sync data containing local timing information
            SyncData syncData = new SyncData(
                Time.time, // Current local time
                rhythmGameManager != null ? rhythmGameManager.songPosition : 0f,
                rhythmGameManager != null ? rhythmGameManager.currentBeat : 0
            );
            
            MessagePacket packet = new MessagePacket(PacketType.SyncTime, mpManager.localPlayerId, syncData);
            mpManager.udpManager.SendPacket(packet);
        }
    }

    // Send ping packet to server for latency measurement
    private void SendPingPacket()
    {
        if (mpManager != null && mpManager.udpManager != null && !mpManager.isHost)
        {
            MessagePacket packet = new MessagePacket(PacketType.Ping, mpManager.localPlayerId, null);
            mpManager.udpManager.SendPacket(packet);
        }
    }

    // Handle received packets
    private void HandlePacketReceived(MessagePacket packet, System.Net.IPEndPoint sender)
    {
        switch (packet.type)
        {
            case PacketType.SyncTime:
                ProcessSyncPacket(packet);
                break;
            case PacketType.SyncGameState:
                ProcessGameStatePacket(packet);
                break;
            case PacketType.Ping:
                ProcessPingPacket(packet);
                break;
        }
    }

    // Process a sync packet from server
    private void ProcessSyncPacket(MessagePacket packet)
    {
        SyncData syncData = packet.GetData<SyncData>();
        if (syncData != null)
        {
            // Add to sync history
            SyncRecord record = new SyncRecord(Time.time, syncData.serverTime, syncData.serverSongPosition, syncData.serverBeat);
            syncHistory.Add(record);
            
            // Keep only the latest sync records
            if (syncHistory.Count > maxSyncHistory)
            {
                syncHistory.RemoveAt(0);
            }
            
            // Calculate time offset with the server
            CalculateTimeOffset();
            
            // Update server's current state
            serverSongPosition = syncData.serverSongPosition;
            serverCurrentBeat = syncData.serverBeat;
            
            // If we're the host, broadcast this back to all clients
            if (mpManager.isHost)
            {
                // Update the sync data with current server state
                SyncData updatedSyncData = new SyncData(
                    Time.time, // Server's current time
                    rhythmGameManager != null ? rhythmGameManager.songPosition : 0f,
                    rhythmGameManager != null ? rhythmGameManager.currentBeat : 0
                );
                
                MessagePacket responsePacket = new MessagePacket(PacketType.SyncTime, mpManager.localPlayerId, updatedSyncData);
                
                // Using MultiplayerHost to broadcast if it exists
                MultiplayerHost host = FindFirstObjectByType<MultiplayerHost>();
                if (host != null)
                {
                    host.BroadcastToAllExcept(responsePacket, mpManager.localPlayerId);
                }
            }
        }
    }

    // Process game state sync packet
    private void ProcessGameStatePacket(MessagePacket packet)
    {
        GameStateData gameStateData = packet.GetData<GameStateData>();
        if (gameStateData != null)
        {
            // Update local game state based on server's state
            serverSongPosition = gameStateData.songPosition;
            serverCurrentBeat = gameStateData.currentBeat;
            serverSongStartTime = gameStateData.startTime;
        }
    }

    // Process ping packet (Pong)
    private void ProcessPingPacket(MessagePacket packet)
    {
        // Only process if it's our own ping echoed back
        if (mpManager != null && packet.playerId == mpManager.localPlayerId)
        {
            // Use high-precision system ticks for RTT calculation (10,000 ticks = 1 ms)
            // This avoids frame-rate quantization (e.g. 16.6ms at 60fps)
            double rttMs = (System.DateTime.UtcNow.Ticks - packet.systemTimestamp) / 10000.0;
            packetExchangeLatency = (float)rttMs; 
        }
    }

    // Calculate the time offset between local and server time
    private void CalculateTimeOffset()
    {
        if (syncHistory.Count < 2) return;
        
        // Calculate average round-trip time and time offset
        float totalTimeOffset = 0f;
        int validCalculations = 0;
        
        for (int i = 1; i < syncHistory.Count; i++)
        {
            SyncRecord prev = syncHistory[i - 1];
            SyncRecord current = syncHistory[i];
            
            // Calculate round trip time
            float localRoundTripTime = current.localTime - prev.localTime;
            
            // Calculate offset between local and server times
            float timeOffset = (current.serverTime - current.localTime + prev.serverTime - prev.localTime) / 2f;
            
            totalTimeOffset += timeOffset;
            validCalculations++;
        }
        
        if (validCalculations > 0)
        {
            networkTimeOffset = totalTimeOffset / validCalculations;
        }
    }

    // Get server time adjusted for network offset
    public float GetAdjustedServerTime()
    {
        return Time.time + networkTimeOffset;
    }

    // Get the time difference between server and local
    public float GetTimeOffset()
    {
        return networkTimeOffset;
    }

    // Get server's song position (for client-side prediction)
    public float GetServerSongPosition(float localTime = -1f)
    {
        if (localTime < 0) localTime = Time.time;
        
        // Predict server's position based on the last known state and elapsed time
        float timeSinceLastSync = localTime - (syncHistory.Count > 0 ? syncHistory[syncHistory.Count - 1].localTime : localTime);
        float predictedSongPosition = serverSongPosition + timeSinceLastSync;
        
        return predictedSongPosition;
    }

    // Update game state based on server sync
    public void UpdateGameStateFromServer()
    {
        if (rhythmGameManager != null && mpManager != null && !mpManager.isHost)
        {
            // Only update if we're a client
            rhythmGameManager.songPosition = GetServerSongPosition();
            rhythmGameManager.currentBeat = serverCurrentBeat;
        }
    }

    private void OnDestroy()
    {
        if (mpManager != null && mpManager.udpManager != null)
        {
            mpManager.udpManager.OnPacketReceived -= HandlePacketReceived;
        }
        
        CancelInvoke("SendSyncPacket");
    }
}

[System.Serializable]
public class SyncData
{
    public float serverTime;
    public float serverSongPosition;
    public int serverBeat;
    
    public SyncData(float time, float songPos, int beat)
    {
        serverTime = time;
        serverSongPosition = songPos;
        serverBeat = beat;
    }
}

[System.Serializable]
public class GameStateData
{
    public float startTime;
    public float songPosition;
    public int currentBeat;
    public float beatProgress;
    
    public GameStateData(float start, float pos, int beat, float progress)
    {
        startTime = start;
        songPosition = pos;
        currentBeat = beat;
        beatProgress = progress;
    }
}