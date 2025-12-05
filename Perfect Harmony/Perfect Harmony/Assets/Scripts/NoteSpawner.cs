using UnityEngine;
using System.Collections.Generic;

// Structure to define when and where notes should spawn
[System.Serializable]
public class SpawnEvent
{
    public float beatNumber; // When to spawn the note (in beats)
    public NoteLane lane; // Which lane to spawn the note in
}

public class NoteSpawner : MonoBehaviour
{
    [Header("Spawning Settings")]
    public List<SpawnEvent> spawnEvents; // List of when and where to spawn notes
    public GameObject notePrefab; // Prefab for the falling notes
    
    [Header("Spawn Positions")]
    public Transform[] spawnPositions; // Transforms where notes will spawn for each lane
    public Transform[] targetPositions; // Transforms where notes should be hit for each lane
    
    // Using spawnOffset from RhythmGameManager instead of having a duplicate
    
    private bool isSpawning = false;
    private int currentEventIndex = 0;
    
    void Start()
    {
        if (spawnEvents == null)
            spawnEvents = new List<SpawnEvent>();
    }

    void Update()
    {
        // Only spawn notes if the game is actively playing
        if (isSpawning && currentEventIndex < spawnEvents.Count && RhythmGameManager.Instance.isPlaying)
        {
            SpawnEvent nextEvent = spawnEvents[currentEventIndex];
            float nextEventTime = RhythmGameManager.Instance.BeatToTime(nextEvent.beatNumber);

            // Check if it's time to spawn the next note
            // Use the actual song position to determine when to spawn
            if (RhythmGameManager.Instance.songPosition + RhythmGameManager.Instance.spawnOffset >= nextEventTime)
            {
                SpawnNote(nextEvent);
                currentEventIndex++;
            }
        }
    }
    
    // Start spawning notes
    public void StartSpawning()
    {
        isSpawning = true;
        currentEventIndex = 0;
    }
    
    // Stop spawning notes
    public void StopSpawning()
    {
        isSpawning = false;
    }
    
    // Spawn a note based on the spawn event
    private void SpawnNote(SpawnEvent spawnEvent)
    {
        if (notePrefab == null)
        {
            Debug.LogError("Note prefab is not assigned!");
            return;
        }

        // Determine the base lane index (0-3)
        int baseLaneIndex = (int)spawnEvent.lane;

        // We want to spawn TWO notes:
        // 1. For Player 1 (Left side: Lanes 0-3)
        // 2. For Player 2 (Right side: Lanes 4-7)
        
        // --- Spawn for Player 1 ---
        CreateNoteInstance(baseLaneIndex, spawnEvent);

        // --- Spawn for Player 2 ---
        // Map 0->4, 1->5, 2->6, 3->7
        int p2LaneIndex = baseLaneIndex + 4;
        CreateNoteInstance(p2LaneIndex, spawnEvent);

        // Check if we're in multiplayer mode and are the host
        // Note: We only send the original "base" lane (0-3) data. 
        // The client will receive it and ALSO spawn two notes locally (if we update client logic),
        // OR we can assume the client just follows the same logic if they use this spawner.
        MultiplayerManager mpManager = FindFirstObjectByType<MultiplayerManager>();
        GameStateSyncManager gameStateSyncManager = FindFirstObjectByType<GameStateSyncManager>();
        if (mpManager != null && mpManager.isHost && mpManager.gameStarted)
        {
            // Send the note spawn event to other players
            if (gameStateSyncManager != null)
            {
                // We send the raw beat/lane. The receiver should interpret how to display it.
                NoteData noteData = new NoteData(spawnEvent.beatNumber, baseLaneIndex, Time.time);
                gameStateSyncManager.SendNoteSpawn(noteData);
            }
        }
    }

    private void CreateNoteInstance(int laneIndex, SpawnEvent spawnEvent)
    {
        if (spawnPositions == null || laneIndex >= spawnPositions.Length || laneIndex >= targetPositions.Length)
        {
            // Lane might not be set up yet or out of bounds
            return;
        }

        Transform spawnPos = spawnPositions[laneIndex];
        Transform targetPos = targetPositions[laneIndex];

        if (spawnPos == null || targetPos == null) return;

        // Instantiate the note
        GameObject noteObj = Instantiate(notePrefab, spawnPos.position, Quaternion.identity);
        FallingNote note = noteObj.GetComponent<FallingNote>();

        if (note == null)
        {
            note = noteObj.AddComponent<FallingNote>();
        }

        // Set up the note properties
        note.lane = (NoteLane)laneIndex; // Cast might be weird for >3, but InputHandler handles int casting
        note.beatNumber = spawnEvent.beatNumber;
        note.spawnTime = Time.time;
        note.targetPosition = targetPos;
        note.spawnPosition = spawnPos;

        // Add to input handler's tracking
        InputHandler inputHandler = FindFirstObjectByType<InputHandler>();
        if (inputHandler != null)
        {
            // Note: NoteLane enum only has 4 values likely. We need to be careful.
            // We should probably cast to (NoteLane) for 0-3, but for 4-7 it's technically undefined in the enum
            // IF the enum is small. Let's check NoteLane.cs. It has Lane1..Lane4.
            // However, InputHandler usually casts enum to int. 
            // Let's assume InputHandler array is big enough (we will fix InputHandler next).
            inputHandler.AddNoteToLane(note, (NoteLane)laneIndex);
            inputHandler.AddNoteToFallingList(note);
        }
    }
    
    // Add a new spawn event
    public void AddSpawnEvent(float beatNumber, NoteLane lane)
    {
        SpawnEvent newEvent = new SpawnEvent
        {
            beatNumber = beatNumber,
            lane = lane
        };
        
        spawnEvents.Add(newEvent);
        
        // Sort events by beat number to ensure proper spawning order
        spawnEvents.Sort((e1, e2) => e1.beatNumber.CompareTo(e2.beatNumber));
    }
    
    // Clear all spawn events
    public void ClearSpawnEvents()
    {
        spawnEvents.Clear();
    }
}