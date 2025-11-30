using System;

[Serializable]
public class MessagePacket
{
    public PacketType type;
    public string playerId;
    public float timestamp;
    public object data;
    
    public MessagePacket(PacketType type, string playerId, object data)
    {
        this.type = type;
        this.playerId = playerId;
        this.timestamp = UnityEngine.Time.time; // Use Unity's time for consistency
        this.data = data;
    }
}