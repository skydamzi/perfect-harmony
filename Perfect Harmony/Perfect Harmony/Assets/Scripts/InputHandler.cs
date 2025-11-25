using UnityEngine;
using System.Collections.Generic;

public class InputHandler : MonoBehaviour
{
    public static InputHandler Instance { get; private set; }

    [Header("Input Settings")]
    public KeyCode[] laneKeys; // Keys for each lane (e.g., D, F, J, K)

    [Header("References")]
    public RhythmGameController gameController;

    private List<FallingNote>[] activeNotesInLanes; // Active notes in each lane that can be hit

    void Awake()
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

    void Start()
    {
        if (laneKeys == null || laneKeys.Length == 0)
        {
            // Default keys for 4 lanes
            laneKeys = new KeyCode[] { KeyCode.D, KeyCode.F, KeyCode.J, KeyCode.K };
        }

        // Initialize the active notes array for each lane
        activeNotesInLanes = new List<FallingNote>[System.Enum.GetValues(typeof(NoteLane)).Length];
        for (int i = 0; i < activeNotesInLanes.Length; i++)
        {
            activeNotesInLanes[i] = new List<FallingNote>();
        }
    }

    void Update()
    {
        // Check for key presses
        for (int i = 0; i < laneKeys.Length && i < activeNotesInLanes.Length; i++)
        {
            if (Input.GetKeyDown(laneKeys[i]))
            {
                NoteLane lane = (NoteLane)i;
                ProcessLaneInput(lane);
            }
        }
    }

    // Process input for a specific lane
    private void ProcessLaneInput(NoteLane lane)
    {
        // Find the closest note in this lane that's in the hit window
        FallingNote closestNote = FindClosestNoteInHitWindow(lane);

        if (closestNote != null)
        {
            // Calculate timing result
            TimingResult timingResult = RhythmGameManager.Instance.CheckTiming(
                Time.time,
                closestNote.targetTime
            );

            // Hit the note with the timing result
            closestNote.HitNote(timingResult);

            // Remove the note from the active list
            RemoveNoteFromLane(closestNote, lane);
        }
        else
        {
            // Player pressed a key but no note was in range - this could be an early/misinput
            OnEmptyInput(lane);
        }
    }

    // Find the closest note in the specified lane that's in the hit window
    private FallingNote FindClosestNoteInHitWindow(NoteLane lane)
    {
        FallingNote closestNote = null;
        float closestDistance = float.MaxValue;

        foreach (FallingNote note in activeNotesInLanes[(int)lane])
        {
            if (note != null && !note.isHit && !note.isMissed)
            {
                float distance = Mathf.Abs(Time.time - note.targetTime);
                if (distance < RhythmGameManager.Instance.okayWindow && distance < closestDistance)
                {
                    closestNote = note;
                    closestDistance = distance;
                }
            }
        }

        return closestNote;
    }

    // Add a note to the active list for a lane
    public void AddNoteToLane(FallingNote note, NoteLane lane)
    {
        if ((int)lane < activeNotesInLanes.Length)
        {
            activeNotesInLanes[(int)lane].Add(note);
        }
    }

    // Remove a note from the active list for a lane
    public void RemoveNoteFromLane(FallingNote note, NoteLane lane)
    {
        if ((int)lane < activeNotesInLanes.Length)
        {
            activeNotesInLanes[(int)lane].Remove(note);
        }
    }

    // Handle input when no note is in range
    private void OnEmptyInput(NoteLane lane)
    {
        // Optional: Add logic for when player presses a key with no note in range
        // For example, this could be used to track early inputs or missed notes
    }

    // Add a note to the falling notes list (used by NoteSpawner)
    public void AddNoteToFallingList(FallingNote note)
    {
        if (note != null && (int)note.lane < activeNotesInLanes.Length)
        {
            activeNotesInLanes[(int)note.lane].Add(note);
        }
    }

    // Remove a note from the active list when it's destroyed
    public void RemoveNote(FallingNote note)
    {
        NoteLane lane = note.lane;
        if ((int)lane < activeNotesInLanes.Length)
        {
            activeNotesInLanes[(int)lane].Remove(note);
        }
    }
}