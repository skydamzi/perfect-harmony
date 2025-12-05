using UnityEngine;
using System.Collections.Generic;

public class InputHandler : MonoBehaviour
{
    public static InputHandler Instance { get; private set; }

    [Header("Input Settings")]
    public KeyCode[] laneKeys; // Keys for each lane (e.g., D, F, J, K)

    [Header("References")]
    public RhythmGameController gameController;

    private MultiplayerManager mpManager;
    private MultiplayerInputHandler mpInputHandler;
    private List<FallingNote>[] activeNotesInLanes; // Active notes in each lane that can be hit

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
            // Default keys for 4 lanes
            laneKeys = new KeyCode[] { KeyCode.D, KeyCode.F, KeyCode.J, KeyCode.K };
        }

        // Initialize the active notes array for each lane
        // We need 8 lanes now (0-3 for P1, 4-7 for P2)
        // NoteLane enum might only have 4 entries, so we use a fixed size of 8
        activeNotesInLanes = new List<FallingNote>[8];
        for (int i = 0; i < activeNotesInLanes.Length; i++)
        {
            activeNotesInLanes[i] = new List<FallingNote>();
        }
    }

    void Update()
    {
        // Lazy load references
        if (mpManager == null) mpManager = FindFirstObjectByType<MultiplayerManager>();

        // Check for key presses
        // laneKeys.Length is typically 4 (D, F, J, K)
        for (int i = 0; i < laneKeys.Length; i++)
        {
            if (Input.GetKeyDown(laneKeys[i]))
            {
                // Determine which lane index to trigger based on player role
                int targetLaneIndex = i; // Default to 0-3

                if (mpManager != null && mpManager.gameStarted)
                {
                    // If I am NOT the host (meaning I am the guest/client), 
                    // my inputs should target the Right side (Lanes 4-7)
                    if (!mpManager.isHost)
                    {
                        targetLaneIndex = i + 4;
                    }
                    
                    // Note: In a real network scenario, we might want to send "I pressed Key 0" 
                    // and let the server decide it's Lane 4. 
                    // But for local simulation/visual feedback, we map it here.

                    // Lazy load input handler
                    if (mpInputHandler == null) mpInputHandler = FindFirstObjectByType<MultiplayerInputHandler>();

                    // Send input to server in multiplayer mode
                    // We send the 'i' (0-3) because the other side knows who sent it
                    if (mpInputHandler != null)
                    {
                        mpInputHandler.ProcessLocalInput(i); 
                    }
                    
                    // Process locally on the CORRECT lane (0-3 or 4-7)
                    ProcessLaneInput((NoteLane)targetLaneIndex);
                }
                else
                {
                    // Single player mode - process normally on Left side (0-3)
                    ProcessLaneInput((NoteLane)targetLaneIndex);
                }
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

    // Get active notes in a specific lane (for multiplayer)
    public List<FallingNote> GetActiveNotesInLane(NoteLane lane)
    {
        if ((int)lane < activeNotesInLanes.Length)
        {
            return activeNotesInLanes[(int)lane];
        }
        return new List<FallingNote>(); // Return empty list if out of bounds
    }

    // Unregister a note (added for FallingNote compatibility)
    public void UnregisterNote(FallingNote note)
    {
        NoteLane lane = note.lane;
        if ((int)lane < activeNotesInLanes.Length)
        {
            activeNotesInLanes[(int)lane].Remove(note);
        }
    }
}