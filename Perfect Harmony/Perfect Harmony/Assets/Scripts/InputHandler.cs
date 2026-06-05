using UnityEngine;
using System.Collections.Generic;

public class InputHandler : MonoBehaviour
{
    public static InputHandler Instance { get; private set; }

    [Header("Input Settings")]
    public KeyCode[] laneKeys;

    [Header("References")]
    public RhythmGameController gameController;

    private MultiplayerManager mpManager;
    private MultiplayerInputHandler mpInputHandler;
    private List<FallingNote>[] activeNotesInLanes;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        if (laneKeys == null || laneKeys.Length == 0)
        {
            laneKeys = new KeyCode[] { KeyCode.D, KeyCode.F, KeyCode.J, KeyCode.K };
        }

        activeNotesInLanes = new List<FallingNote>[8];
        for (int i = 0; i < activeNotesInLanes.Length; i++)
        {
            activeNotesInLanes[i] = new List<FallingNote>();
        }
    }

    void Update()
    {
        if (mpManager == null) mpManager = FindFirstObjectByType<MultiplayerManager>();

        for (int i = 0; i < laneKeys.Length; i++)
        {
            if (Input.GetKeyDown(laneKeys[i]))
            {
                int targetLaneIndex = i;

                if (mpManager != null && mpManager.gameStarted)
                {
                    if (mpManager.GetPlayerSlot() != 0) targetLaneIndex = i + 4;
                    if (mpInputHandler == null) mpInputHandler = FindFirstObjectByType<MultiplayerInputHandler>();
                    if (mpInputHandler != null) mpInputHandler.ProcessLocalInput(i);
                    ProcessLaneInput((NoteLane)targetLaneIndex);
                }
                else
                {
                    ProcessLaneInput((NoteLane)targetLaneIndex);
                }
            }
        }
    }

    private void ProcessLaneInput(NoteLane lane)
    {
        FallingNote closestNote = FindClosestNoteInHitWindow(lane);

        if (closestNote != null)
        {
            float currentPos = RhythmGameManager.Instance.songPosition;
            float targetPos = RhythmGameManager.Instance.BeatToTime(closestNote.beatNumber);

            TimingResult timingResult = RhythmGameManager.Instance.CheckTiming(currentPos, targetPos);

            closestNote.HitNote(timingResult);
            RemoveNoteFromLane(closestNote, lane);
        }
    }

    private FallingNote FindClosestNoteInHitWindow(NoteLane lane)
    {
        FallingNote closestNote = null;
        float closestDistance = float.MaxValue;
        float currentPos = RhythmGameManager.Instance.songPosition;

        foreach (FallingNote note in activeNotesInLanes[(int)lane])
        {
            if (note != null && !note.isHit && !note.isMissed)
            {
                float targetPos = RhythmGameManager.Instance.BeatToTime(note.beatNumber);
                float distance = Mathf.Abs(currentPos - targetPos);

                if (distance < RhythmGameManager.Instance.okayWindow && distance < closestDistance)
                {
                    closestNote = note;
                    closestDistance = distance;
                }
            }
        }
        return closestNote;
    }

    // --- ����Ʈ ���� �� ��Ƽ�� �Լ��� ---

    public void AddNoteToLane(FallingNote note, NoteLane lane)
    {
        if ((int)lane < activeNotesInLanes.Length)
            activeNotesInLanes[(int)lane].Add(note);
    }

    public void RemoveNoteFromLane(FallingNote note, NoteLane lane)
    {
        if ((int)lane < activeNotesInLanes.Length)
            activeNotesInLanes[(int)lane].Remove(note);
    }

    public void AddNoteToFallingList(FallingNote note)
    {
        if (note != null && (int)note.lane < activeNotesInLanes.Length)
            activeNotesInLanes[(int)note.lane].Add(note);
    }

    public void UnregisterNote(FallingNote note)
    {
        NoteLane lane = note.lane;
        if ((int)lane < activeNotesInLanes.Length)
            activeNotesInLanes[(int)lane].Remove(note);
    }

    // [���Ⱑ ���� �ذ� ����Ʈ]
    public List<FallingNote> GetActiveNotesInLane(NoteLane lane)
    {
        int index = (int)lane;
        if (index >= 0 && index < activeNotesInLanes.Length)
        {
            return activeNotesInLanes[index];
        }
        return new List<FallingNote>();
    }
}