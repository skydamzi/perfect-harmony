using System;
using UnityEngine;

[Serializable]
public class MessagePacket
{
    public PacketType type;
    public string playerId;
    public string roomId;
    
    // Timing fields
    public float timestamp;        // Local realtimeSinceStartup of sender
    public long systemTimestamp;   // Precision UtcTicks of sender
    public double relayTimestamp;   // Injected by Central Server (absolute reference)
    public double serverTime;       // Scheduled future time (Network Time)
    
    // Gameplay fields (Flattened)
    public int lane;
    public float hitTime;
    public float beatNumber;
    public float spawnTime;
    public int score;
    public int combo;
    public int timingResult;
    public string instrument; // Selected instrument (Piano, Drums, etc.)
    
    // State fields
    public float songPosition;
    public int currentBeat;
    public float beatProgress; // [복구] GameStateSyncManager 등에서 사용
    public double startTime;    // [정밀도 업그레이드]

    public MessagePacket(PacketType type, string playerId, string roomId)
    {
        this.type = type;
        this.playerId = playerId;
        this.roomId = roomId;
        this.timestamp = Time.realtimeSinceStartup;
        this.systemTimestamp = DateTime.UtcNow.Ticks;
    }

    // [복구] 호환성용 4개 인자 생성자
    public MessagePacket(PacketType type, string playerId, string roomId, object dummy) : this(type, playerId, roomId)
    {
    }

    // Specialized packet creators
    public static MessagePacket CreateScore(string id, string room, int score, int combo, int res)
    {
        MessagePacket p = new MessagePacket(PacketType.PlayerScore, id, room);
        p.score = score; p.combo = combo; p.timingResult = res;
        return p;
    }

    public static MessagePacket CreateHit(string id, string room, int lane, int res, float beatNumber)
    {
        MessagePacket p = new MessagePacket(PacketType.NoteHit, id, room);
        p.lane = lane; p.timingResult = res; p.beatNumber = beatNumber;
        return p;
    }

    public static MessagePacket CreateSyncStart(string id, string room, double targetNetworkTime)
    {
        MessagePacket p = new MessagePacket(PacketType.GameStart, id, room);
        p.serverTime = targetNetworkTime;
        return p;
    }
}
