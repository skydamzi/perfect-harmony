using System;
using UnityEngine;

[Serializable]
public class MessagePacket
{
    public PacketType type;
    public string playerId;
    public string roomId;
    public float timestamp;
    
    // Flattened data fields to avoid nested JSON strings
    public int lane;
    public int score;
    public int combo;
    public int timingResult;
    public float hitTime;
    public float serverTime; // Used for sync
    
    public MessagePacket(PacketType type, string playerId, string roomId)
    {
        this.type = type;
        this.playerId = playerId;
        this.roomId = roomId;
        this.timestamp = Time.time;
    }

    // Helper method to create a score packet
    public static MessagePacket CreateScorePacket(string playerId, string roomId, int score, int combo, int result)
    {
        MessagePacket p = new MessagePacket(PacketType.PlayerScore, playerId, roomId);
        p.score = score;
        p.combo = combo;
        p.timingResult = result;
        return p;
    }

    // Helper method to create a hit packet
    public static MessagePacket CreateHitPacket(string playerId, string roomId, int lane, int result, float hitTime)
    {
        MessagePacket p = new MessagePacket(PacketType.NoteHit, playerId, roomId);
        p.lane = lane;
        p.timingResult = result;
        p.hitTime = hitTime;
        return p;
    }

    // Helper method to create a sync start packet
    public static MessagePacket CreateSyncStartPacket(string playerId, string roomId, float serverStartTime)
    {
        MessagePacket p = new MessagePacket(PacketType.GameStart, playerId, roomId);
        p.serverTime = serverStartTime;
        return p;
    }
}
