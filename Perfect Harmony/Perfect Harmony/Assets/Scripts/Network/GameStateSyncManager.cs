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
    }

    // Refresh references to scene objects (called by AutoSetup)
    public void RefreshReferences()
    {
        mpManager = FindFirstObjectByType<MultiplayerManager>();
        rhythmGameManager = FindFirstObjectByType<RhythmGameManager>();
        timingSyncManager = FindFirstObjectByType<TimingSyncManager>();
        noteSpawner = FindFirstObjectByType<NoteSpawner>();
        
        hasSyncedStart = false;
        Debug.Log("GameStateSyncManager references refreshed.");
    }

    // Send current game state
    private void SendGameStateSync()
    {
        if (mpManager != null && mpManager.udpManager != null && mpManager.gameStarted)
        {
            MessagePacket packet = new MessagePacket(PacketType.SyncGameState, mpManager.localPlayerId, mpManager.currentRoomId);
            packet.startTime = rhythmGameManager.actualSongStartTime;
            packet.songPosition = rhythmGameManager.songPosition;
            packet.currentBeat = rhythmGameManager.currentBeat;
            packet.beatProgress = rhythmGameManager.beatProgress;

            mpManager.udpManager.SendPacket(packet);
        }
    }

    // Handle received packets
    private void HandlePacketReceived(MessagePacket packet, System.Net.IPEndPoint sender, double arrivalTimestamp)
    {
        if (!string.IsNullOrEmpty(packet.roomId) && packet.roomId != mpManager.currentRoomId && packet.roomId != "Global")
            return;

        switch (packet.type)
        {
            case PacketType.SyncGameState: ProcessGameStatePacket(packet); break;
            case PacketType.NoteSpawn: ProcessNoteSpawnPacket(packet); break;
        }
    }

    // Process game state packet from server
    private void ProcessGameStatePacket(MessagePacket packet)
    {
        if (packet.playerId == mpManager.localPlayerId) return;

        if (rhythmGameManager != null)
        {
            // [시간 통일] Time.realtimeSinceStartup 대신 서버 시간을 사용합니다.
            double currentServerTime = TimingSyncManager.Instance.GetCurrentServerTime();
            float serverSongPos = packet.songPosition;
            double calculatedStartTime = currentServerTime - (double)serverSongPos;

            if (!hasSyncedStart)
            {
                rhythmGameManager.actualSongStartTime = calculatedStartTime;
                targetSongStartTime = (float)calculatedStartTime;
                hasSyncedStart = true;
            }
            else
            {
                targetSongStartTime = (float)calculatedStartTime;
            }
        }
    }

    // Process note spawn packet
    private void ProcessNoteSpawnPacket(MessagePacket packet)
    {
        if (packet.playerId == mpManager.localPlayerId) return;
        SpawnNoteForClient(packet);
    }

    // Spawn note for client based on server's note data
    private void SpawnNoteForClient(MessagePacket noteData)
    {
        if (noteSpawner == null) noteSpawner = FindFirstObjectByType<NoteSpawner>();
        if (noteSpawner == null || rhythmGameManager == null) return;

        int baseLane = noteData.lane;
        CreateClientNoteInstance(baseLane, noteData);
        CreateClientNoteInstance(baseLane + 4, noteData);
    }

    private void CreateClientNoteInstance(int laneIndex, MessagePacket noteData)
    {
        if (laneIndex >= noteSpawner.spawnPositions.Length || laneIndex >= noteSpawner.targetPositions.Length) return;

        Transform spawnPos = noteSpawner.spawnPositions[laneIndex];
        Transform targetPos = noteSpawner.targetPositions[laneIndex];

        GameObject noteObj = Instantiate(noteSpawner.notePrefab, spawnPos.position, Quaternion.identity);
        FallingNote note = noteObj.GetComponent<FallingNote>() ?? noteObj.AddComponent<FallingNote>();

        note.lane = (NoteLane)laneIndex;
        note.beatNumber = noteData.beatNumber;
        note.spawnTime = noteData.spawnTime;
        note.targetPosition = targetPos;
        note.spawnPosition = spawnPos;
        note.targetTime = (float)(rhythmGameManager.actualSongStartTime + (double)rhythmGameManager.BeatToTime(note.beatNumber));
        
        InputHandler inputHandler = FindFirstObjectByType<InputHandler>();
        if (inputHandler != null)
        {
            inputHandler.AddNoteToLane(note, (NoteLane)laneIndex);
            inputHandler.AddNoteToFallingList(note);
        }
    }

    private void Update()
    {
        // 중앙 서버의 절대 시각 동기화를 사용하므로 P2P 상태 동기화에 따른 클럭 보정은 비활성화
        /*
        if (rhythmGameManager != null && (rhythmGameManager.isCountingDown || !rhythmGameManager.isPlaying)) return;

        if (hasSyncedStart && rhythmGameManager != null && mpManager != null)
        {
            float currentActual = (float)rhythmGameManager.targetServerStartTime;
            if (Mathf.Abs(currentActual - targetSongStartTime) > 0.5f)
            {
                rhythmGameManager.targetServerStartTime = (double)targetSongStartTime;
            }
            else
            {
                float lerped = Mathf.Lerp(currentActual, targetSongStartTime, Time.deltaTime * syncSmoothSpeed);
                rhythmGameManager.targetServerStartTime = (double)lerped;
            }
        }
        */
    }

    public void SendNoteSpawn(MessagePacket noteData)
    {
        if (mpManager != null && mpManager.udpManager != null) mpManager.udpManager.SendPacket(noteData);
    }

    private void OnDestroy()
    {
        if (mpManager != null && mpManager.udpManager != null) mpManager.udpManager.OnPacketReceived -= HandlePacketReceived;
        CancelInvoke("SendGameStateSync");
    }
}
