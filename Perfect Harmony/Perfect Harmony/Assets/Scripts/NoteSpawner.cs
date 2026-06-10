using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class SpawnEvent
{
    public float beatNumber;
    public NoteLane lane;
}

public class NoteSpawner : MonoBehaviour
{
    [Header("Spawning Settings")]
    public List<SpawnEvent> p1SpawnEvents = new List<SpawnEvent>();
    public List<SpawnEvent> p2SpawnEvents = new List<SpawnEvent>();
    public GameObject notePrefab;

    [Header("Spawn Positions")]
    public Transform[] spawnPositions;
    public Transform[] targetPositions;

    private bool isSpawning = false;
    private int p1Index = 0;
    private int p2Index = 0;

    void Start()
    {
        if (p1SpawnEvents == null) p1SpawnEvents = new List<SpawnEvent>();
        if (p2SpawnEvents == null) p2SpawnEvents = new List<SpawnEvent>();
    }

    void Update()
    {
        bool canSpawn = RhythmGameManager.Instance.isPlaying || RhythmGameManager.Instance.isCountingDown;

        if (isSpawning && canSpawn)
        {
            // Player 1 (Local) Spawning
            if (p1Index < p1SpawnEvents.Count)
            {
                SpawnEvent nextEvent = p1SpawnEvents[p1Index];
                float nextEventTime = RhythmGameManager.Instance.BeatToTime(nextEvent.beatNumber);

                if (RhythmGameManager.Instance.songPosition + RhythmGameManager.Instance.spawnOffset >= nextEventTime)
                {
                    CreateNoteInstance((int)nextEvent.lane, nextEvent);
                    p1Index++;
                }
            }

            // Player 2 (Remote) Spawning
            if (p2Index < p2SpawnEvents.Count)
            {
                SpawnEvent nextEvent = p2SpawnEvents[p2Index];
                float nextEventTime = RhythmGameManager.Instance.BeatToTime(nextEvent.beatNumber);

                if (RhythmGameManager.Instance.songPosition + RhythmGameManager.Instance.spawnOffset >= nextEventTime)
                {
                    // Remote lane is offset by 4
                    CreateNoteInstance((int)nextEvent.lane + 4, nextEvent);
                    p2Index++;
                }
            }
        }
    }

    public void StartSpawning()
    {
        isSpawning = true;
        p1Index = 0;
        p2Index = 0;
    }

    public void StopSpawning()
    {
        isSpawning = false;
    }

    // Deprecated or redirect to CreateNoteInstance for compatibility
    private void SpawnNote(SpawnEvent spawnEvent)
    {
        // This function was creating duplicates. Now we handle it in Update separately.
    }

    private void CreateNoteInstance(int laneIndex, SpawnEvent spawnEvent)
    {
        if (spawnPositions == null || laneIndex >= spawnPositions.Length || laneIndex >= targetPositions.Length) return;

        Transform spawnPos = spawnPositions[laneIndex];
        Transform targetPos = targetPositions[laneIndex];

        if (spawnPos == null || targetPos == null) return;

        GameObject noteObj = Instantiate(notePrefab, spawnPos.position, Quaternion.identity);
        FallingNote note = noteObj.GetComponent<FallingNote>();

        if (note == null) note = noteObj.AddComponent<FallingNote>();

        // 노트 설정
        note.lane = (NoteLane)laneIndex;
        note.beatNumber = spawnEvent.beatNumber;
        note.spawnTime = Time.time;
        note.targetPosition = targetPos;
        note.spawnPosition = spawnPos;

        InputHandler inputHandler = FindFirstObjectByType<InputHandler>();
        if (inputHandler != null)
        {
            inputHandler.AddNoteToLane(note, (NoteLane)laneIndex);
            inputHandler.AddNoteToFallingList(note);
        }
    }

    public void ClearSpawnEvents()
    {
        p1SpawnEvents.Clear();
        p2SpawnEvents.Clear();
    }

    public void AddSpawnEvent(float beatNumber, NoteLane lane)
    {
        SpawnEvent newEvent = new SpawnEvent { beatNumber = beatNumber, lane = lane };
        p1SpawnEvents.Add(newEvent);
        p2SpawnEvents.Add(newEvent); // Add to both for default behavior
        
        p1SpawnEvents.Sort((e1, e2) => e1.beatNumber.CompareTo(e2.beatNumber));
        p2SpawnEvents.Sort((e1, e2) => e1.beatNumber.CompareTo(e2.beatNumber));
    }
}