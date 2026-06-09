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

    private static readonly System.DateTime UnixEpoch = new System.DateTime(1970, 1, 1, 0, 0, 0, System.DateTimeKind.Utc);

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

    public static double GetUnixTime()
    {
        return (System.DateTime.UtcNow - UnixEpoch).TotalSeconds;
    }

    private void SendPing()
    {
        if (mpManager != null && mpManager.udpManager != null)
        {
            // T1: systemTimestamp (Precision UTC Ticks) - We'll use GetUnixTime for calculation
            MessagePacket p = new MessagePacket(PacketType.Ping, mpManager.localPlayerId, mpManager.currentRoomId);
            p.startTime = GetUnixTime(); // Use startTime field as T1 for server roundtrip
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
        double t1 = p.startTime; 
        // T4: 수신 (Client)
        double t4 = GetUnixTime();
        // T2: 서버 수신, T3: 서버 발신
        double t2 = p.startTime; // [주의] 서버 코드 확인: packet["startTime"] = recv_time 이었음!
        double t3 = p.relayTimestamp;

        // [중요] 서버 코드가 packet["startTime"]을 recv_time으로 덮어씀.
        // 클라이언트는 T1을 다른 필드에 보관하거나, 서버 코드를 믿고 t2를 수신 시각으로 사용.
        
        // NTP 공식: Offset = ((T2 - T1) + (T3 - T4)) / 2
        // 하지만 서버가 T1을 T2로 덮어썼으므로, RTT 기반으로 단순 계산하거나 
        // T1을 별도 보관해야 함. (여기서는 MessagePacket의 timestamp 필드를 T1으로 활용 가능)
        
        double localT1 = (double)p.systemTimestamp / 10000000.0; // ticks to seconds
        // systemTimestamp는 DateTime.UtcNow.Ticks (Unix Epoch 아님)
        // 보정 필요
        double unixT1 = (double)(p.systemTimestamp - UnixEpoch.Ticks) / 10000000.0;

        double currentOffset = ((t2 - unixT1) + (t3 - t4)) / 2.0;
        double currentRtt = (t4 - unixT1) - (t3 - t2);
        
        rtt = (float)(currentRtt * 1000.0);
        
        offsetHistory.Add(currentOffset);
        if (offsetHistory.Count > 15) offsetHistory.RemoveAt(0);

        double sum = 0;
        foreach (double o in offsetHistory) sum += o;
        networkTimeOffset = sum / offsetHistory.Count;
        
        syncCount++;
    }

    public double GetCurrentServerTime()
    {
        // 현재 내 컴퓨터의 UTC 시각 + 오프셋 = 서버의 UTC 시각
        return GetUnixTime() + networkTimeOffset;
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
