using System;
using UnityEngine;

[Serializable]
public class MessagePacket
{
    public PacketType type;
    public string playerId;
    public string roomId; // Added for central server routing
    public float timestamp;
    public long systemTimestamp; // High-precision timestamp in ticks
    public string payload; // Serialized JSON string of the data object
    
    public MessagePacket(PacketType type, string playerId, object data)
    {
        this.type = type;
        this.playerId = playerId;
        this.roomId = "";
        this.timestamp = Time.time;
        this.systemTimestamp = System.DateTime.UtcNow.Ticks;
        
        if (data != null)
        {
            this.payload = JsonUtility.ToJson(data);
        }
        else
        {
            this.payload = "";
        }
    }

    public MessagePacket(PacketType type, string playerId, string roomId, object data)
    {
        this.type = type;
        this.playerId = playerId;
        this.roomId = roomId;
        this.timestamp = Time.time;
        this.systemTimestamp = System.DateTime.UtcNow.Ticks;
        
        if (data != null)
        {
            this.payload = JsonUtility.ToJson(data);
        }
        else
        {
            this.payload = "";
        }
    }
    
    public T GetData<T>()
    {
        if (string.IsNullOrEmpty(payload)) return default(T);
        return JsonUtility.FromJson<T>(payload);
    }
}