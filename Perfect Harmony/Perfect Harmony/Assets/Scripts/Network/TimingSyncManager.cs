using System.Collections.Generic;
using UnityEngine;

public class TimingSyncManager : MonoBehaviour
{
    public static TimingSyncManager Instance { get; private set; }

    [Header("Timing Sync Settings")]
    public float syncInterval = 1.0f; 
    public int minSyncsToStart = 5;    // 최소 5번의 핑이 오가야 동기화 완료로 간주
    public double networkTimeOffset = 0; 
    public float packetExchangeLatency = 0f; 

    [Header("Rhythm Sync")]
    public float serverSongPosition = 0f;
    public int serverCurrentBeat = 0;
    public float serverSongStartTime = 0f;

    private List<double> offsetHistory = new List<double>();
    private int syncCount = 0;
    
    private MultiplayerManager mpManager;
    private RhythmGameManager rhythmGameManager;

    public bool IsSynced => syncCount >= minSyncsToStart;

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else { Instance = this; DontDestroyOnLoad(gameObject); }
    }

    private void Start()
    {
        mpManager = FindFirstObjectByType<MultiplayerManager>();
        rhythmGameManager = FindFirstObjectByType<RhythmGameManager>();

        if (mpManager != null) mpManager.udpManager.OnPacketReceived += HandlePacket;

        InvokeRepeating("SendPingPacket", 0f, 1.0f);
        InvokeRepeating("SendSyncPacket", 0.5f, 2.0f);
    }

    public void RefreshReferences()
    {
        mpManager = FindFirstObjectByType<MultiplayerManager>();
        rhythmGameManager = FindFirstObjectByType<RhythmGameManager>();
    }

    private void SendSyncPacket()
    {
        if (mpManager == null || mpManager.udpManager == null || mpManager.IsAuthority) return;
        
        MessagePacket p = new MessagePacket(PacketType.SyncTime, mpManager.localPlayerId, mpManager.currentRoomId);
        p.songPosition = rhythmGameManager != null ? rhythmGameManager.songPosition : 0f;
        mpManager.udpManager.SendPacket(p);
    }

    private void SendPingPacket()
    {
        if (mpManager != null && mpManager.udpManager != null)
        {
            MessagePacket p = new MessagePacket(PacketType.Ping, mpManager.localPlayerId, mpManager.currentRoomId);
            mpManager.udpManager.SendPacket(p);
        }
    }

    private void HandlePacket(MessagePacket p, System.Net.IPEndPoint sender)
    {
        if (!string.IsNullOrEmpty(p.roomId) && p.roomId != mpManager.currentRoomId && p.roomId != "Global") return;

        // 중앙 서버가 찍어준 relayTimestamp가 있으면 무조건 시각 동기화 수행
        if (p.relayTimestamp > 0)
        {
            ProcessPrecisionSync(p);
        }

        if (p.type == PacketType.SyncGameState && !mpManager.IsAuthority)
        {
            serverSongPosition = p.songPosition;
            serverCurrentBeat = p.currentBeat;
            serverSongStartTime = (float)p.startTime;
        }
    }

    private void ProcessPrecisionSync(MessagePacket p)
    {
        double localRecvTime = (double)Time.realtimeSinceStartup;
        
        // 1. RTT (왕복 시간) 계산: 현재 시각 - 패킷 생성 시각 (시스템 틱 활용)
        double rtt = (double)(System.DateTime.UtcNow.Ticks - p.systemTimestamp) / 10000000.0; // Seconds
        packetExchangeLatency = (float)(rtt * 1000.0); // Milliseconds

        // 2. NTP 공식: Offset = (ServerTime + RTT/2) - LocalRecvTime
        // 서버가 패킷을 쏜 시점(relayTimestamp)에 RTT의 절반(이동시간)을 더해 현재의 실제 서버 시간을 추정
        double estimatedServerNow = p.relayTimestamp + (rtt / 2.0);
        double currentOffset = estimatedServerNow - localRecvTime;

        // 3. 필터링: 급격한 변화 방지 (이동 평균)
        offsetHistory.Add(currentOffset);
        if (offsetHistory.Count > 10) offsetHistory.RemoveAt(0);

        double sum = 0;
        foreach (double o in offsetHistory) sum += o;
        networkTimeOffset = sum / offsetHistory.Count;
        
        syncCount++;
    }

    public double GetAdjustedServerTime()
    {
        return (double)Time.realtimeSinceStartup + networkTimeOffset;
    }

    public double GetTimeOffset()
    {
        return networkTimeOffset;
    }

    private void OnDestroy()
    {
        if (mpManager != null && mpManager.udpManager != null) mpManager.udpManager.OnPacketReceived -= HandlePacket;
    }
}
