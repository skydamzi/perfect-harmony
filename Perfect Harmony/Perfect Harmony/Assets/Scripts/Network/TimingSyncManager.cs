using System.Collections.Generic;
using UnityEngine;

public class TimingSyncManager : MonoBehaviour
{
    public static TimingSyncManager Instance { get; private set; }

    [Header("Timing Sync Settings")]
    public float syncInterval = 1.0f; 
    public int minSyncsToStart = 5;    
    public double networkTimeOffset = 0; 
    public float packetExchangeLatency = 0f; 

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

        if (mpManager != null && mpManager.udpManager != null) 
            mpManager.udpManager.OnPacketReceived += HandlePacket;

        InvokeRepeating("SendPingPacket", 0f, 1.0f);
    }

    private void SendPingPacket()
    {
        if (mpManager != null && mpManager.udpManager != null)
        {
            MessagePacket p = new MessagePacket(PacketType.Ping, mpManager.localPlayerId, mpManager.currentRoomId);
            mpManager.udpManager.SendPacket(p);
        }
    }

    private void HandlePacket(MessagePacket p, System.Net.IPEndPoint sender, double arrivalTimestamp)
    {
        if (!string.IsNullOrEmpty(p.roomId) && p.roomId != mpManager.currentRoomId && p.roomId != "Global") return;

        if (p.type == PacketType.Ping && p.relayTimestamp > 0 && p.playerId == mpManager.localPlayerId)
        {
            ProcessPrecisionSync(p, arrivalTimestamp);
        }
    }

    private void ProcessPrecisionSync(MessagePacket p, double arrivalTimestamp)
    {
        // T1: 발신 시각 (Client), T4: 수신 시각 (Client)
        // T2: 서버 수신 시각 (p.startTime), T3: 서버 발신 시각 (p.relayTimestamp)
        
        double t1 = (double)p.systemTimestamp / 10000000.0;
        double t4 = (double)System.DateTime.UtcNow.Ticks / 10000000.0;
        
        double rtt = t4 - t1;
        double serverProcessing = p.relayTimestamp - p.startTime;
        
        // 순수 네트워크 왕복 시간 (지연시간)
        double networkRtt = Mathf.Max(0, (float)(rtt - serverProcessing));
        packetExchangeLatency = (float)(networkRtt * 1000.0);

        // NTP 공식 기반 정밀 동기화
        // localRecvTime (T4의 로컬 realtimeSinceStartup)
        double localRecvTime = (arrivalTimestamp > 0) ? arrivalTimestamp : (double)Time.realtimeSinceStartup;
        
        // 서버의 현재 시각 추정 (T4 시점) = T3 + (네트워크 편도 지연)
        double estimatedServerNow = p.relayTimestamp + (networkRtt / 2.0);
        double currentOffset = estimatedServerNow - localRecvTime;

        offsetHistory.Add(currentOffset);
        if (offsetHistory.Count > 15) offsetHistory.RemoveAt(0);

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

    public void RefreshReferences()
    {
        mpManager = FindFirstObjectByType<MultiplayerManager>();
        rhythmGameManager = FindFirstObjectByType<RhythmGameManager>();

        if (mpManager != null && mpManager.udpManager != null)
        {
            mpManager.udpManager.OnPacketReceived -= HandlePacket;
            mpManager.udpManager.OnPacketReceived += HandlePacket;
        }
        
        Debug.Log("[Sync] TimingSyncManager References Refreshed");
    }

    private void OnDestroy()
    {
        if (mpManager != null && mpManager.udpManager != null) 
            mpManager.udpManager.OnPacketReceived -= HandlePacket;
    }
}
