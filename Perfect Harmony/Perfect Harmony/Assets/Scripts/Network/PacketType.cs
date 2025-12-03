public enum PacketType
{
    // Connection related
    Connect,
    Disconnect,
    Ping,
    
    // Game state related
    GameStart,
    GameStop,
    
    // Rhythm related
    NoteSpawn,
    NoteHit,
    NoteMiss,
    
    // Player related
    PlayerInput,
    PlayerScore,
    PlayerReady,
    
    // Sync related
    SyncTime,
    SyncGameState
}