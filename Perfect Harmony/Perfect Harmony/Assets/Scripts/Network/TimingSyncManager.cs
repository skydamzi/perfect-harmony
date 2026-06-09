using System.Collections.Generic;
using UnityEngine;

public class TimingSyncManager : MonoBehaviour
{
    public static TimingSyncManager Instance { get; private set; }

    [Header("Sync Settings")]
    public float pingInterval = 1.0f;
    public int minSyncsToStart = 5;

    [Header("Current Status")]
    public double networkTimeOffset = 0; // ServerTime - LocalTime
    public float rtt = 0f;
    public int syncCount = 0;

    private List<double> offsetHistory = new List<double>();
    private MultiplayerManager mpManager;

    public bool IsSynced => syncCount >= minSyncsToStart;

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else { Instance = this; DontDestroyOnLoad(gameObject); }
    }

    private void Start()
    {
        mpManager = FindFirstObjectByType<MultiplayerManager>();
        if (mpManager != null && mpManager.udpManager != null)
            mpManager.udpManager.OnPacketReceived += HandlePacket;

        InvokeRepeating("SendPing", 0.5f, pingInterval);
    }

    private void SendPing()
    {
        if (mpManager != null && mpManager.udpManager != null)
        {
            // T1: systemTimestamp (Precision UTC Ticks)
            MessagePacket p = new MessagePacket(PacketType.Ping, mpManager.localPlayerId, mpManager.currentRoomId);
            mpManager.udpManager.SendPacket(p);
        }
    }

    private void HandlePacket(MessagePacket p, System.Net.IPEndPoint sender, double arrivalTimestamp)
    {
        // 내 핑 응답만 처리 (T2, T3 포함됨)
        if (p.type == PacketType.Ping && p.playerId == mpManager.localPlayerId && p.relayTimestamp > 0)
        {
            CalculateOffset(p);
        }
    }

    private void CalculateOffset(MessagePacket p)
    {
        // T1: 발신 (Client)
        double t1 = (double)p.systemTimestamp / 10000000.0;
        // T4: 수신 (Client)
        double t4 = (double)System.DateTime.UtcNow.Ticks / 10000000.0;
        // T2: 서버 수신, T3: 서버 발신
        double t2 = p.startTime;
        double t3 = p.relayTimestamp;

        // NTP 공식: Offset = ((T2 - T1) + (T3 - T4)) / 2
        double currentOffset = ((t2 - t1) + (t3 - t4)) / 2.0;
        double currentRtt = (t4 - t1) - (t3 - t2);
        
        rtt = (float)(currentRtt * 1000.0);
        
        offsetHistory.Add(currentOffset);
        if (offsetHistory.Count > 15) offsetHistory.RemoveAt(0);

        double sum = 0;
        foreach (double o in offsetHistory) sum += o;
        networkTimeOffset = sum / offsetHistory.Count;
        
        syncCount++;
    }

    // 서버의 절대 시각을 내 로컬 시간(realtimeSinceStartup)으로 변환할 때 쓰는 오프셋이 아님!
    // 서버 시각(UTC) - 내 시각(UTC)의 차이임.
    
    public double GetCurrentServerTime()
    {
        // 현재 내 컴퓨터의 UTC 시각 + 오프셋 = 서버의 UTC 시각
        double localNow = (double)System.DateTime.UtcNow.Ticks / 10000000.0;
        return localNow + networkTimeOffset;
    }

    public void RefreshReferences()
    {
        mpManager = FindFirstObjectByType<MultiplayerManager>();
        if (mpManager != null && mpManager.udpManager != null)
        {
            mpManager.udpManager.OnPacketReceived -= HandlePacket;
            mpManager.udpManager.OnPacketReceived += HandlePacket;
        }
    }

    private void OnDestroy()
    {
        if (mpManager != null && mpManager.udpManager != null)
            mpManager.udpManager.OnPacketReceived -= HandlePacket;
    }
}
