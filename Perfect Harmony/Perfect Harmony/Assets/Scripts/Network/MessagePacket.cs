using System;
using UnityEngine;

[Serializable]
public class MessagePacket
{
    public PacketType type;
    public string playerId;
    public string roomId;
    public float timestamp;
    public long systemTimestamp; // Added back for precision timing
    
    // Flattened data fields
    [Header("Note/Input Data")]
    public int lane;
    public float hitTime;
    public float beatNumber;
    public float spawnTime;
    
    [Header("Score Data")]
    public int score;
    public int combo;
    public int timingResult;
    
    [Header("Sync/State Data")]
    public float serverTime;
    public float songPosition;
    public int currentBeat;
    public float beatProgress;
    public float startTime;

    // Constructors
    public MessagePacket(PacketType type, string playerId, string roomId)
    {
        this.type = type;
        this.playerId = playerId;
        this.roomId = roomId;
        this.timestamp = Time.time;
        this.systemTimestamp = DateTime.UtcNow.Ticks;
    }

    // Constructor to satisfy 4-argument calls (backward compatibility)
    public MessagePacket(PacketType type, string playerId, string roomId, object dummy) : this(type, playerId, roomId)
    {
        // Dummy object is ignored in flat structure
    }
}
