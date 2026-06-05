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

        if (mpManager != null && mpManager.udpManager != null)
        {
            mpManager.udpManager.OnPacketReceived += HandlePacketReceived;
        }

        // In central server architecture, clients don't send periodic game state syncs.
        // The server is responsible for timing or relaying authority.
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

    // Send current game state (In central server mode, this might be requested or handled by server)
    private void SendGameStateSync()
    {
        if (mpManager != null && mpManager.udpManager != null && mpManager.gameStarted)
        {
            GameStateData gameStateData = new GameStateData(
                rhythmGameManager.actualSongStartTime,
                rhythmGameManager.songPosition,
                rhythmGameManager.currentBeat,
                rhythmGameManager.beatProgress
            );
            
            MessagePacket packet = new MessagePacket(PacketType.SyncGameState, mpManager.localPlayerId, mpManager.currentRoomId, gameStateData);
            mpManager.udpManager.SendPacket(packet);
        }
    }

    // Handle received packets
    private void HandlePacketReceived(MessagePacket packet, System.Net.IPEndPoint sender)
    {
        // Filter by room
        if (!string.IsNullOrEmpty(packet.roomId) && packet.roomId != mpManager.currentRoomId && packet.roomId != "Global")
            return;

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
        // Don't process our own sync packets if we sent them
        if (packet.playerId == mpManager.localPlayerId) return;

        GameStateData gameStateData = packet.GetData<GameStateData>();
        if (gameStateData != null && rhythmGameManager != null)
        {
            // The server says: "At this exact moment (packet arrival), my songPosition is X"
            float currentTime = Time.time;
            float serverSongPos = gameStateData.songPosition;
            float calculatedStartTime = currentTime - serverSongPos;

            if (!hasSyncedStart)
            {
                rhythmGameManager.actualSongStartTime = calculatedStartTime;
                targetSongStartTime = calculatedStartTime;
                hasSyncedStart = true;
            }
            else
            {
                targetSongStartTime = calculatedStartTime;
            }
        }
    }

    // Process note spawn packet
    private void ProcessNoteSpawnPacket(MessagePacket packet)
    {
        if (packet.playerId == mpManager.localPlayerId) return;

        NoteData noteData = packet.GetData<NoteData>();
        if (noteData != null)
        {
            serverNoteQueue.Add(noteData);
            SpawnNoteForClient(noteData);
        }
    }

    // Process game start packet
    private void ProcessGameStartPacket(MessagePacket packet)
    {
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
            return;
        }

        int baseLane = noteData.lane;
        CreateClientNoteInstance(baseLane, noteData);
        CreateClientNoteInstance(baseLane + 4, noteData);
    }

    private void CreateClientNoteInstance(int laneIndex, NoteData noteData)
    {
        if (laneIndex >= noteSpawner.spawnPositions.Length || laneIndex >= noteSpawner.targetPositions.Length)
             return;

        Transform spawnPos = noteSpawner.spawnPositions[laneIndex];
        Transform targetPos = noteSpawner.targetPositions[laneIndex];

        GameObject noteObj = Instantiate(noteSpawner.notePrefab, spawnPos.position, Quaternion.identity);
        FallingNote note = noteObj.GetComponent<FallingNote>();

        if (note == null) note = noteObj.AddComponent<FallingNote>();

        note.lane = (NoteLane)laneIndex;
        note.beatNumber = noteData.beatNumber;
        note.spawnTime = noteData.spawnTime;
        note.targetPosition = targetPos;
        note.spawnPosition = spawnPos;
        note.targetTime = rhythmGameManager.actualSongStartTime + rhythmGameManager.BeatToTime(note.beatNumber);
        
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
        // Smoothly adjust start time to match server/host authority
        if (hasSyncedStart && rhythmGameManager != null && mpManager != null)
        {
            if (Mathf.Abs(rhythmGameManager.actualSongStartTime - targetSongStartTime) > 0.5f)
            {
                rhythmGameManager.actualSongStartTime = targetSongStartTime;
            }
            else
            {
                rhythmGameManager.actualSongStartTime = Mathf.Lerp(rhythmGameManager.actualSongStartTime, targetSongStartTime, Time.deltaTime * syncSmoothSpeed);
            }
        }
    }

    // Send a note spawn event to the server for relay
    public void SendNoteSpawn(NoteData noteData)
    {
        if (mpManager != null && mpManager.udpManager != null)
        {
            MessagePacket packet = new MessagePacket(PacketType.NoteSpawn, mpManager.localPlayerId, mpManager.currentRoomId, noteData);
            mpManager.udpManager.SendPacket(packet);
        }
    }

    // This method is no longer used in client-server mode as server handles sync
    public void SendGameStateToPlayer(string playerId, IPEndPoint endpoint)
    {
        // Server responsibility
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