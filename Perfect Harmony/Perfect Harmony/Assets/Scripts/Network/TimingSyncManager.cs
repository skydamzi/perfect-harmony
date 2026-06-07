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

        if (mpManager != null) mpManager.udpManager.OnPacketReceived += HandlePacket;

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

    private void HandlePacket(MessagePacket p, System.Net.IPEndPoint sender)
    {
        if (!string.IsNullOrEmpty(p.roomId) && p.roomId != mpManager.currentRoomId && p.roomId != "Global") return;

        // [핵심 수정] 오직 핑(Ping) 패킷에 담긴 서버 시각으로만 동기화 수행
        // GameStart처럼 반복 전송되는 패킷에 담긴 타임스탬프는 지연 오차가 발생하므로 무시함
        if (p.type == PacketType.Ping && p.relayTimestamp > 0)
        {
            ProcessPrecisionSync(p);
        }
    }

    private void ProcessPrecisionSync(MessagePacket p)
    {
        double localRecvTime = (double)Time.realtimeSinceStartup;
        
        // RTT 계산 (시스템 틱 기준)
        double rtt = (double)(System.DateTime.UtcNow.Ticks - p.systemTimestamp) / 10000000.0;
        packetExchangeLatency = (float)(rtt * 1000.0);

        // NTP 공식 적용
        double estimatedServerNow = p.relayTimestamp + (rtt / 2.0);
        double currentOffset = estimatedServerNow - localRecvTime;

        offsetHistory.Add(currentOffset);
        if (offsetHistory.Count > 15) offsetHistory.RemoveAt(0); // 샘플 수 증가 (안정성)

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
}
