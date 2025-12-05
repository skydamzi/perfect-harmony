using System.Collections.Generic;
using System.Net;
using UnityEngine;

public class GameStateSyncManager : MonoBehaviour
{
    public static GameStateSyncManager Instance { get; private set; }

    [Header("Game State Sync")]
    public float stateSyncInterval = 0.0167f; // High frequency sync (~60Hz)
    public float syncSmoothSpeed = 10.0f; // Faster smoothing to react quickly to sync packets

    [Header("Note Spawning")]
    public List<NoteData> serverNoteQueue = new List<NoteData>();

    private MultiplayerManager mpManager;
    private RhythmGameManager rhythmGameManager;
    private TimingSyncManager timingSyncManager;
    private NoteSpawner noteSpawner;

    private float targetSongStartTime;
    private bool hasSyncedStart = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
    }

    private void Start()
    {
        mpManager = FindFirstObjectByType<MultiplayerManager>();
        rhythmGameManager = FindFirstObjectByType<RhythmGameManager>();
        timingSyncManager = FindFirstObjectByType<TimingSyncManager>();

        if (mpManager != null)
        {
            mpManager.udpManager.OnPacketReceived += HandlePacketReceived;
        }

        // Start the state sync timer if we're the host
        if (mpManager != null && mpManager.isHost)
        {
            InvokeRepeating("SendGameStateSync", 0f, stateSyncInterval);
        }
    }

    // Refresh references to scene objects (called by AutoSetup)
    public void RefreshReferences()
    {
        mpManager = FindFirstObjectByType<MultiplayerManager>();
        rhythmGameManager = FindFirstObjectByType<RhythmGameManager>();
        timingSyncManager = FindFirstObjectByType<TimingSyncManager>();
        noteSpawner = FindFirstObjectByType<NoteSpawner>();
        
        hasSyncedStart = false;
        if (serverNoteQueue != null) serverNoteQueue.Clear();
        
        Debug.Log("GameStateSyncManager references refreshed.");
    }

    // Send current game state to all players (host only)
    private void SendGameStateSync()
    {
        if (mpManager != null && mpManager.isHost && mpManager.udpManager != null && mpManager.gameStarted)
        {
            // We send the Host's songPosition directly
            GameStateData gameStateData = new GameStateData(
                rhythmGameManager.actualSongStartTime,
                rhythmGameManager.songPosition,
                rhythmGameManager.currentBeat,
                rhythmGameManager.beatProgress
            );
            
            MessagePacket packet = new MessagePacket(PacketType.SyncGameState, mpManager.localPlayerId, gameStateData);
            
            // Broadcast to all players using the host
            MultiplayerHost host = FindFirstObjectByType<MultiplayerHost>();
            if (host != null)
            {
                host.BroadcastToAllExcept(packet, mpManager.localPlayerId);
            }
        }
    }

    // Handle received packets
    private void HandlePacketReceived(MessagePacket packet, System.Net.IPEndPoint sender)
    {
        switch (packet.type)
        {
            case PacketType.SyncGameState:
                ProcessGameStatePacket(packet);
                break;
            case PacketType.NoteSpawn:
                ProcessNoteSpawnPacket(packet);
                break;
            case PacketType.GameStart:
                ProcessGameStartPacket(packet);
                break;
        }
    }

    // Process game state packet from server
    private void ProcessGameStatePacket(MessagePacket packet)
    {
        GameStateData gameStateData = packet.GetData<GameStateData>();
        if (gameStateData != null && mpManager != null && !mpManager.isHost && rhythmGameManager != null)
        {
            // The server says: "At this exact moment (packet arrival), my songPosition is X"
            // (Ideally we account for packet travel time, but for now assume fast connection)
            
            // Current time on client
            float currentTime = Time.time;
            
            // The songPosition the server has.
            float serverSongPos = gameStateData.songPosition;

            // Calculate what actualSongStartTime SHOULD be to achieve this songPosition right now.
            // formula: songPosition = currentTime - actualSongStartTime
            // therefore: actualSongStartTime = currentTime - songPosition
            float calculatedStartTime = currentTime - serverSongPos;

            // Adjust for network latency (RTT / 2) if TimingSyncManager is available
            if (timingSyncManager != null)
            {
                // If we know latency, the server actually sent this 'latency' seconds ago.
                // So the server is actually further ahead by 'latency' seconds.
                // But simpler approach: timingSyncManager already tracks offset.
                // Let's rely on the direct calculation above for visual sync, 
                // as it forces the client to match the server's playback cursor.
            }

            if (!hasSyncedStart)
            {
                // Hard snap for the first sync
                rhythmGameManager.actualSongStartTime = calculatedStartTime;
                targetSongStartTime = calculatedStartTime;
                hasSyncedStart = true;
            }
            else
            {
                // Smoothly drift towards the correct start time
                targetSongStartTime = calculatedStartTime;
            }
        }
    }

    // Process note spawn packet
    private void ProcessNoteSpawnPacket(MessagePacket packet)
    {
        NoteData noteData = packet.GetData<NoteData>();
        if (noteData != null)
        {
            serverNoteQueue.Add(noteData);
            
            // If we're client, we should spawn the note
            if (mpManager != null && !mpManager.isHost)
            {
                SpawnNoteForClient(noteData);
            }
        }
    }

    // Process game start packet
    private void ProcessGameStartPacket(MessagePacket packet)
    {
        // Clear any previous game state and note queue when game starts
        serverNoteQueue.Clear();
        hasSyncedStart = false;
    }

    // Spawn note for client based on server's note data
    private void SpawnNoteForClient(NoteData noteData)
    {
        if (noteSpawner == null)
        {
            noteSpawner = FindFirstObjectByType<NoteSpawner>();
        }

        if (noteSpawner == null || rhythmGameManager == null)
        {
            Debug.LogWarning("NoteSpawner or RhythmGameManager not found for client note spawning");
            return;
        }

        // The server sends the base lane (0-3).
        // Just like the host, the client should spawn TWO notes:
        // 1. Lane X (Left/Host side)
        // 2. Lane X+4 (Right/Guest side)

        int baseLane = noteData.lane;
        
        // Spawn Left (Host) Note
        CreateClientNoteInstance(baseLane, noteData);
        
        // Spawn Right (Guest) Note
        CreateClientNoteInstance(baseLane + 4, noteData);
    }

    private void CreateClientNoteInstance(int laneIndex, NoteData noteData)
    {
        if (laneIndex >= noteSpawner.spawnPositions.Length || laneIndex >= noteSpawner.targetPositions.Length)
        {
             // Out of bounds
             return;
        }

        Transform spawnPos = noteSpawner.spawnPositions[laneIndex];
        Transform targetPos = noteSpawner.targetPositions[laneIndex];

        // Instantiate the note
        GameObject noteObj = Instantiate(noteSpawner.notePrefab, spawnPos.position, Quaternion.identity);
        FallingNote note = noteObj.GetComponent<FallingNote>();

        if (note == null)
        {
            note = noteObj.AddComponent<FallingNote>();
        }

        // Set up the note properties using the note data
        note.lane = (NoteLane)laneIndex;
        note.beatNumber = noteData.beatNumber;
        note.spawnTime = noteData.spawnTime;
        note.targetPosition = targetPos;
        note.spawnPosition = spawnPos;

        // Calculate the actual target time based on server timing
        note.targetTime = rhythmGameManager.actualSongStartTime + rhythmGameManager.BeatToTime(note.beatNumber);
        
        // Register with InputHandler so we can hit it (if it's our side)
        InputHandler inputHandler = FindFirstObjectByType<InputHandler>();
        if (inputHandler != null)
        {
            inputHandler.AddNoteToLane(note, (NoteLane)laneIndex);
            inputHandler.AddNoteToFallingList(note);
        }
    }

    // Update is called once per frame
    private void Update()
    {
        // Smoothly adjust start time to match server
        if (hasSyncedStart && rhythmGameManager != null && mpManager != null && !mpManager.isHost)
        {
            // If the difference is large, snap immediately
            if (Mathf.Abs(rhythmGameManager.actualSongStartTime - targetSongStartTime) > 0.5f)
            {
                rhythmGameManager.actualSongStartTime = targetSongStartTime;
            }
            else
            {
                // Lerp for smooth correction
                rhythmGameManager.actualSongStartTime = Mathf.Lerp(rhythmGameManager.actualSongStartTime, targetSongStartTime, Time.deltaTime * syncSmoothSpeed);
            }
        }
    }

    // Send a note spawn event from the host
    public void SendNoteSpawn(NoteData noteData)
    {
        if (mpManager != null && mpManager.isHost && mpManager.udpManager != null)
        {
            MessagePacket packet = new MessagePacket(PacketType.NoteSpawn, mpManager.localPlayerId, noteData);
            
            // Broadcast to all players using the host
            MultiplayerHost host = FindFirstObjectByType<MultiplayerHost>();
            if (host != null)
            {
                host.BroadcastToAllExcept(packet, mpManager.localPlayerId);
            }
        }
    }

    // Send game state to a specific player (useful for late joiners)
    public void SendGameStateToPlayer(string playerId, IPEndPoint endpoint)
    {
        if (rhythmGameManager != null && mpManager != null && mpManager.isHost)
        {
            GameStateData gameStateData = new GameStateData(
                rhythmGameManager.actualSongStartTime,
                rhythmGameManager.songPosition,
                rhythmGameManager.currentBeat,
                rhythmGameManager.beatProgress
            );
            
            MessagePacket packet = new MessagePacket(PacketType.SyncGameState, mpManager.localPlayerId, gameStateData);
            
            // Send directly to the specific player
            MultiplayerHost host = FindFirstObjectByType<MultiplayerHost>();
            if (host != null)
            {
                host.BroadcastToAllExcept(packet, playerId);
            }
        }
    }

    private void OnDestroy()
    {
        if (mpManager != null && mpManager.udpManager != null)
        {
            mpManager.udpManager.OnPacketReceived -= HandlePacketReceived;
        }
        
        CancelInvoke("SendGameStateSync");
    }
}