public enum PacketType
{
    // Connection related
    Connect,
    Disconnect,
    
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
    
    // Sync related
    SyncTime,
    SyncGameState
}