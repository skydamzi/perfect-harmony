using UnityEngine;
using System.Collections.Generic;

// Structure to define when and where notes should spawn
[System.Serializable]
public class SpawnEvent
{
    public int beatNumber; // When to spawn the note (in beats)
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

        // Get the spawn and target positions for this lane
        int laneIndex = (int)spawnEvent.lane;

        if (laneIndex >= spawnPositions.Length || laneIndex >= targetPositions.Length)
        {
            Debug.LogError($"Lane index {laneIndex} is out of bounds for spawn/target positions!");
            return;
        }

        Transform spawnPos = spawnPositions[laneIndex];
        Transform targetPos = targetPositions[laneIndex];

        // Instantiate the note
        GameObject noteObj = Instantiate(notePrefab, spawnPos.position, Quaternion.identity);
        FallingNote note = noteObj.GetComponent<FallingNote>();

        if (note == null)
        {
            note = noteObj.AddComponent<FallingNote>();
        }

        // Set up the note properties
        note.lane = spawnEvent.lane;
        note.beatNumber = spawnEvent.beatNumber;
        note.spawnTime = Time.time;
        note.targetPosition = targetPos;
        note.spawnPosition = spawnPos; // Important: set the spawn position

        // Add to input handler's tracking
        InputHandler inputHandler = FindFirstObjectByType<InputHandler>();
        if (inputHandler != null)
        {
            inputHandler.AddNoteToLane(note, spawnEvent.lane);
            inputHandler.AddNoteToFallingList(note);
        }
    }
    
    // Add a new spawn event
    public void AddSpawnEvent(int beatNumber, NoteLane lane)
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