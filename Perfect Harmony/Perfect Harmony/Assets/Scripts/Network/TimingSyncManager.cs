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
        // 모든 계산은 double 정밀도의 초(seconds) 단위로 통일
        double t1 = (double)p.systemTimestamp / 10000000.0;
        double t4 = (double)System.DateTime.UtcNow.Ticks / 10000000.0;
        
        // 서버에서 온 T2, T3 (이미 초 단위임)
        double t2 = p.startTime;
        double t3 = p.relayTimestamp;

        double rtt = t4 - t1;
        double serverProcessing = t3 - t2;
        
        // 순수 네트워크 지연 (편도)
        double networkLatency = Mathf.Max(0, (float)((rtt - serverProcessing) / 2.0));
        packetExchangeLatency = (float)(networkLatency * 1000.0 * 2.0);

        // NTP 공식: Offset = ((T2 - T1) + (T3 - T4)) / 2
        double currentOffset = ((t2 - t1) + (t3 - t4)) / 2.0;

        offsetHistory.Add(currentOffset);
        if (offsetHistory.Count > 15) offsetHistory.RemoveAt(0);

        double sum = 0;
        foreach (double o in offsetHistory) sum += o;
        networkTimeOffset = sum / offsetHistory.Count;
        
        syncCount++;
        // Debug.Log($"[Sync] RTT: {rtt:F4}s, Proc: {serverProcessing:F4}s, Offset: {networkTimeOffset:F4}s");
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
