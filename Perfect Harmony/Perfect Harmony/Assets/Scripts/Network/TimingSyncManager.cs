using System.Collections.Generic;
using UnityEngine;

public class TimingSyncManager : MonoBehaviour
{
    public static TimingSyncManager Instance { get; private set; }

    [Header("Timing Sync Settings")]
    public float syncInterval = 1.0f; // Send sync packets every second
    public float maxSyncHistory = 10; // Number of sync records to keep for calculation
    public double networkTimeOffset = 0; // [정밀도] Calculated offset between local and server time
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
        public double serverTime; // [정밀도] Server's time from the packet
        public float serverSongPosition; // Server's song position
        public int serverBeat; // Server's current beat

        public SyncRecord(float local, double server, float songPos, int beat)
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
        if (mpManager != null && mpManager.udpManager != null && !mpManager.IsAuthority)
        {
            MessagePacket packet = new MessagePacket(PacketType.SyncTime, mpManager.localPlayerId, mpManager.currentRoomId);
            packet.serverTime = (double)Time.realtimeSinceStartup;
            packet.songPosition = rhythmGameManager != null ? rhythmGameManager.songPosition : 0f;
            packet.currentBeat = rhythmGameManager != null ? rhythmGameManager.currentBeat : 0;
            
            mpManager.udpManager.SendPacket(packet);
        }
    }

    // Send ping packet to server for latency measurement
    private void SendPingPacket()
    {
        if (mpManager != null && mpManager.udpManager != null)
        {
            MessagePacket packet = new MessagePacket(PacketType.Ping, mpManager.localPlayerId, mpManager.currentRoomId);
            mpManager.udpManager.SendPacket(packet);
        }
    }

    // Handle received packets
    private void HandlePacketReceived(MessagePacket packet, System.Net.IPEndPoint sender)
    {
        // Filter by room
        if (!string.IsNullOrEmpty(packet.roomId) && packet.roomId != mpManager.currentRoomId && packet.roomId != "Global")
            return;

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
        // [중앙 서버 기준 동기화] 모든 유저는 서버가 찍어준 relayTimestamp를 기준으로 오차 계산
        if (packet.relayTimestamp > 0)
        {
            SyncRecord record = new SyncRecord(Time.realtimeSinceStartup, packet.relayTimestamp, packet.songPosition, packet.currentBeat);
            syncHistory.Add(record);
            
            if (syncHistory.Count > maxSyncHistory)
            {
                syncHistory.RemoveAt(0);
            }
            
            CalculateTimeOffset();
        }

        // [참가자 전용] 방장의 상태(노래 위치 등)를 추가로 참고
        if (!mpManager.IsAuthority && packet.playerId != mpManager.localPlayerId)
        {
            serverSongPosition = packet.songPosition;
            serverCurrentBeat = packet.currentBeat;
        }
    }

    // Process game state sync packet
    private void ProcessGameStatePacket(MessagePacket packet)
    {
        if (mpManager.IsAuthority) return;

        // Update local game state based on server's state
        serverSongPosition = packet.songPosition;
        serverCurrentBeat = packet.currentBeat;
        serverSongStartTime = (float)packet.startTime;
    }

    // Process ping packet (Pong)
    private void ProcessPingPacket(MessagePacket packet)
    {
        // Only process if it's our own ping echoed back
        if (mpManager != null && packet.playerId == mpManager.localPlayerId)
        {
            double rttMs = (System.DateTime.UtcNow.Ticks - packet.systemTimestamp) / 10000.0;
            packetExchangeLatency = (float)rttMs; 

            // [추가] 핑 패킷에 포함된 서버 시간으로도 동기화 수행 (더 빈번한 갱신)
            if (packet.relayTimestamp > 0)
            {
                // 왕복 시간의 절반을 더해 서버의 '현재' 시간을 추정
                double estimatedServerNow = packet.relayTimestamp + (packetExchangeLatency / 2000.0);
                SyncRecord record = new SyncRecord(Time.realtimeSinceStartup, estimatedServerNow, 0, 0);
                syncHistory.Add(record);
                if (syncHistory.Count > maxSyncHistory) syncHistory.RemoveAt(0);
                CalculateTimeOffset();
            }
        }
    }

    // Calculate the time offset between local and server time
    private void CalculateTimeOffset()
    {
        if (syncHistory.Count < 2) return;
        
        double totalTimeOffset = 0;
        int validCalculations = 0;
        
        for (int i = 1; i < syncHistory.Count; i++)
        {
            SyncRecord prev = syncHistory[i - 1];
            SyncRecord current = syncHistory[i];
            
            // 오차 계산: (중앙 서버 시각 - 내 로컬 시각)
            double timeOffset = (current.serverTime - current.localTime + prev.serverTime - prev.localTime) / 2.0;
            
            totalTimeOffset += timeOffset;
            validCalculations++;
        }
        
        if (validCalculations > 0)
        {
            networkTimeOffset = totalTimeOffset / validCalculations;
        }
    }

    // Get server time adjusted for network offset
    public double GetAdjustedServerTime()
    {
        // 방장 포함 모든 클라이언트가 서버 시계에 자신을 맞춤
        return (double)Time.realtimeSinceStartup + networkTimeOffset;
    }

    // Get the time difference between server and local
    public double GetTimeOffset()
    {
        return networkTimeOffset;
    }

    // Get server's song position (for client-side prediction)
    public float GetServerSongPosition(float localTime = -1f)
    {
        if (localTime < 0) localTime = Time.realtimeSinceStartup;
        
        // Predict server's position based on the last known state and elapsed time
        float timeSinceLastSync = localTime - (syncHistory.Count > 0 ? syncHistory[syncHistory.Count - 1].localTime : localTime);
        float predictedSongPosition = serverSongPosition + timeSinceLastSync;
        
        return predictedSongPosition;
    }

    // Update game state based on server sync
    public void UpdateGameStateFromServer()
    {
        if (rhythmGameManager != null && mpManager != null && !mpManager.IsAuthority)
        {
            // Only update if we're a guest (non-authority)
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
