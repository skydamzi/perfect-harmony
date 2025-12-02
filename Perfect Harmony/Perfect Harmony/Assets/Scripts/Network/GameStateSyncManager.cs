using System.Collections.Generic;
using System.Net;
using UnityEngine;

public class GameStateSyncManager : MonoBehaviour
{
    public static GameStateSyncManager Instance { get; private set; }

    [Header("Game State Sync")]
    public float stateSyncInterval = 0.5f; // Send state sync every 0.5 seconds
    public float stateInterpolationTime = 0.1f; // Time to interpolate between states

    [Header("Note Spawning")]
    public List<NoteData> serverNoteQueue = new List<NoteData>();

    private MultiplayerManager mpManager;
    private RhythmGameManager rhythmGameManager;
    private TimingSyncManager timingSyncManager;
    private NoteSpawner noteSpawner;

    private Queue<GameStateData> stateQueue = new Queue<GameStateData>();
    private GameStateData targetGameState;
    private float interpolationTimer = 0f;
    private bool isInterpolating = false;

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

    // Send current game state to all players (host only)
    private void SendGameStateSync()
    {
        if (mpManager != null && mpManager.isHost && mpManager.udpManager != null && mpManager.gameStarted)
        {
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
        if (packet.data is GameStateData gameStateData && mpManager != null && !mpManager.isHost)
        {
            // Add to the state queue for interpolation
            stateQueue.Enqueue(gameStateData);
            
            // Keep only the last few states
            if (stateQueue.Count > 5)
            {
                stateQueue.Dequeue();
            }
            
            // If we're not currently interpolating, start interpolation
            if (!isInterpolating && stateQueue.Count >= 2)
            {
                targetGameState = stateQueue.Dequeue();
                interpolationTimer = 0f;
                isInterpolating = true;
            }
        }
    }

    // Process note spawn packet
    private void ProcessNoteSpawnPacket(MessagePacket packet)
    {
        if (packet.data is NoteData noteData)
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
        stateQueue.Clear();
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

        // Get the spawn and target positions for this lane
        int laneIndex = noteData.lane;
        if (laneIndex >= noteSpawner.spawnPositions.Length || laneIndex >= noteSpawner.targetPositions.Length)
        {
            Debug.LogError($"Lane index {laneIndex} is out of bounds for spawn/target positions!");
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
        NoteLane laneEnum = (NoteLane)laneIndex;
        note.lane = laneEnum;
        note.beatNumber = noteData.beatNumber;
        note.spawnTime = noteData.spawnTime;
        note.targetPosition = targetPos;
        note.spawnPosition = spawnPos;

        // Calculate the actual target time based on server timing
        note.targetTime = rhythmGameManager.actualSongStartTime + rhythmGameManager.BeatToTime(note.beatNumber);
    }

    // Update is called once per frame
    private void Update()
    {
        // Interpolate game state if needed (for clients)
        if (isInterpolating && targetGameState != null && mpManager != null && !mpManager.isHost)
        {
            interpolationTimer += Time.deltaTime;
            
            if (rhythmGameManager != null)
            {
                // Calculate interpolation factor (0 to 1)
                float t = Mathf.Clamp01(interpolationTimer / stateInterpolationTime);
                
                // Interpolate song position
                rhythmGameManager.songPosition = Mathf.Lerp(rhythmGameManager.songPosition, targetGameState.songPosition, t);
                
                // Update beat based on interpolated song position
                rhythmGameManager.currentBeat = Mathf.FloorToInt(rhythmGameManager.songPosition / rhythmGameManager.beatDuration);
                rhythmGameManager.beatProgress = (rhythmGameManager.songPosition % rhythmGameManager.beatDuration) / rhythmGameManager.beatDuration;
                
                // Check if interpolation is complete
                if (t >= 1f)
                {
                    // Apply final values
                    rhythmGameManager.songPosition = targetGameState.songPosition;
                    rhythmGameManager.currentBeat = targetGameState.currentBeat;
                    rhythmGameManager.beatProgress = targetGameState.beatProgress;
                    
                    isInterpolating = false;
                    
                    // Check if there are more states to interpolate
                    if (stateQueue.Count > 0)
                    {
                        targetGameState = stateQueue.Dequeue();
                        interpolationTimer = 0f;
                        isInterpolating = true;
                    }
                }
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