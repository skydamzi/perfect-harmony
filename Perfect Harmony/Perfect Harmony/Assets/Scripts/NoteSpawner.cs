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
    public List<SpawnEvent> spawnEvents;
    public GameObject notePrefab;

    [Header("Spawn Positions")]
    public Transform[] spawnPositions;
    public Transform[] targetPositions;

    private bool isSpawning = false;
    private int currentEventIndex = 0;

    void Start()
    {
        if (spawnEvents == null)
            spawnEvents = new List<SpawnEvent>();
    }

    void Update()
    {
        // [핵심 수정] isPlaying 뿐만 아니라 isCountingDown 일 때도 노트가 생성되어야 함!
        bool canSpawn = RhythmGameManager.Instance.isPlaying || RhythmGameManager.Instance.isCountingDown;

        if (isSpawning && currentEventIndex < spawnEvents.Count && canSpawn)
        {
            SpawnEvent nextEvent = spawnEvents[currentEventIndex];
            float nextEventTime = RhythmGameManager.Instance.BeatToTime(nextEvent.beatNumber);

            // 매니저에서 계산해주는 마이너스 값을 포함한 songPosition 사용
            if (RhythmGameManager.Instance.songPosition + RhythmGameManager.Instance.spawnOffset >= nextEventTime)
            {
                SpawnNote(nextEvent);
                currentEventIndex++;
            }
        }
    }

    public void StartSpawning()
    {
        isSpawning = true;
        currentEventIndex = 0;
    }

    public void StopSpawning()
    {
        isSpawning = false;
    }

    private void SpawnNote(SpawnEvent spawnEvent)
    {
        if (notePrefab == null)
        {
            Debug.LogError("Note prefab is not assigned!");
            return;
        }

        int baseLaneIndex = (int)spawnEvent.lane;

        // Player 1, 2 동시에 소환
        CreateNoteInstance(baseLaneIndex, spawnEvent);
        int p2LaneIndex = baseLaneIndex + 4;
        CreateNoteInstance(p2LaneIndex, spawnEvent);

        // 멀티플레이어 동기화 로직
        MultiplayerManager mpManager = FindFirstObjectByType<MultiplayerManager>();
        GameStateSyncManager gameStateSyncManager = FindFirstObjectByType<GameStateSyncManager>();
        if (mpManager != null && mpManager.isHost && mpManager.gameStarted)
        {
            if (gameStateSyncManager != null)
            {
                NoteData noteData = new NoteData(spawnEvent.beatNumber, baseLaneIndex, Time.time);
                gameStateSyncManager.SendNoteSpawn(noteData);
            }
        }
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

        // [수정] spawnTime을 Time.time으로 박으면 카운트다운 싱크가 깨질 수 있음.
        // 노래의 논리적 시간(songPosition)을 기준으로 타겟 도착 시간을 계산하는 게 안전함.
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

    public void AddSpawnEvent(float beatNumber, NoteLane lane)
    {
        SpawnEvent newEvent = new SpawnEvent { beatNumber = beatNumber, lane = lane };
        spawnEvents.Add(newEvent);
        spawnEvents.Sort((e1, e2) => e1.beatNumber.CompareTo(e2.beatNumber));
    }

    public void ClearSpawnEvents()
    {
        spawnEvents.Clear();
    }
}