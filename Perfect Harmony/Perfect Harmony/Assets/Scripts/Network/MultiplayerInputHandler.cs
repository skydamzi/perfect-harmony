using UnityEngine;

public class MultiplayerInputHandler : MonoBehaviour
{
    [Header("Multiplayer References")]
    public MultiplayerManager mpManager;
    public InputHandler inputHandler;
    public RhythmGameController gameController;
    public ScoreManager scoreManager;

    [Header("Timing Tolerance")]
    public float timingTolerance = 0.1f; // Additional tolerance for network timing

    private TimingSyncManager timingSyncManager;
    private GameStateSyncManager gameStateSyncManager;

    private void Start()
    {
        // Get references if not set
        if (mpManager == null) mpManager = FindFirstObjectByType<MultiplayerManager>();
        if (inputHandler == null) inputHandler = FindFirstObjectByType<InputHandler>();
        if (gameController == null) gameController = FindFirstObjectByType<RhythmGameController>();
        if (scoreManager == null) scoreManager = FindFirstObjectByType<ScoreManager>();

        timingSyncManager = FindFirstObjectByType<TimingSyncManager>();
        gameStateSyncManager = FindFirstObjectByType<GameStateSyncManager>();
    }

    // Process input from local player and send to server
    public void ProcessLocalInput(int laneIndex)
    {
        if (mpManager == null || mpManager.udpManager == null) return;

        // Send input data to server
        PlayerInputData inputData = new PlayerInputData(laneIndex, Time.time);
        mpManager.SendPlayerInput(laneIndex, Time.time);
        
        // Show local feedback immediately
        ProcessLocalInputFeedback(laneIndex);
    }

    // Process input feedback locally (visual/audio feedback)
    private void ProcessLocalInputFeedback(int laneIndex)
    {
        // Play local input sound or visual effect
        Debug.Log($"Local input processed for lane {laneIndex}");
    }

    // Process input from remote player
    public void ProcessRemoteInput(int laneIndex, float inputTime, string playerId)
    {
        // Calculate timing result based on server time
        TimingResult timingResult = TimingResult.Miss; // Default to miss
        
        RhythmGameManager rhythmGameManager = FindFirstObjectByType<RhythmGameManager>();
        if (rhythmGameManager != null)
        {
            // Find the closest note in the specified lane that's in the hit window
            FallingNote closestNote = FindClosestNoteInHitWindow((NoteLane)laneIndex, inputTime);

            if (closestNote != null)
            {
                // Calculate timing accuracy
                timingResult = rhythmGameManager.CheckTiming(inputTime, closestNote.targetTime);

                // Process the hit
                ProcessRemoteNoteHit(closestNote, timingResult, playerId);
            }
        }
        
        Debug.Log($"Remote input from {playerId} on lane {laneIndex}, timing: {timingResult}");
    }

    // Find the closest note in the specified lane that's in the hit window
    private FallingNote FindClosestNoteInHitWindow(NoteLane lane, float inputTime)
    {
        if (inputHandler == null) return null;
        
        FallingNote closestNote = null;
        float closestDistance = float.MaxValue;

        var activeNotes = inputHandler.GetActiveNotesInLane(lane);
        foreach (FallingNote note in activeNotes)
        {
            if (note != null && !note.isHit && !note.isMissed)
            {
                float distance = Mathf.Abs(inputTime - note.targetTime);
                if (distance < GetTimingWindowForResult(TimingResult.Okay) && distance < closestDistance)
                {
                    closestNote = note;
                    closestDistance = distance;
                }
            }
        }

        return closestNote;
    }

    // Get timing window based on result type
    private float GetTimingWindowForResult(TimingResult result)
    {
        if (RhythmGameManager.Instance != null)
        {
            switch (result)
            {
                case TimingResult.Perfect: return RhythmGameManager.Instance.perfectWindow;
                case TimingResult.Good: return RhythmGameManager.Instance.goodWindow;
                case TimingResult.Okay: return RhythmGameManager.Instance.okayWindow;
                default: return RhythmGameManager.Instance.okayWindow;
            }
        }
        return 0.3f; // Default okay window
    }

    // Process remote note hit
    private void ProcessRemoteNoteHit(FallingNote note, TimingResult timingResult, string playerId)
    {
        if (note == null || gameController == null) return;

        // Mark the note as hit
        note.isHit = true;
        
        // 1. Visual Feedback: Spawn Hit Particles at the note's position
        if (SpriteEffectManager.Instance != null)
        {
            SpriteEffectManager.Instance.SpawnHitSprites(timingResult, note.transform.position);
        }
        
        // 2. Visual Feedback: Change note color/visuals before destroying (optional, acts as hit confirmation)
        // FallingNote.OnNoteHit does this, but we want to avoid double-counting logic inside it if any.
        // However, FallingNote.OnNoteHit also notifies InputHandler, ScoreManager, etc.
        // Since we handle Score explicitly here, let's just do visuals.
        
        // Calculate score based on timing result
        int scoreToAdd = GetScoreForTimingResult(timingResult);
        
        // Update score
        if (scoreManager != null)
        {
            // For remote player, we don't update the local score directly
            // Instead we trust the server's judgment and score update (which comes via PlayerScore packet usually)
            // BUT, for immediate feedback, we might want to update a "Remote Score" display if we had one.
            // Current ScoreManager seems single-player focused or shared.
            // If we want to see the remote player's score update, we rely on HandleRemoteScoreUpdate.
        }

        // Send score update to server
        if (mpManager != null)
        {
            // Wait, if *I* calculated the remote hit (which shouldn't happen, usually remote sends their own score),
            // Actually, ProcessRemoteNoteHit is called when WE receive an input packet and simulate the hit.
            // But usually, the client sends "I hit this note" (Score Packet) OR "I pressed this key" (Input Packet).
            // If we are processing Input Packet, we are simulating the hit.
            
            // NOTE: In this architecture, usually each player judges their OWN hits and sends Score/Hit packets.
            // If we are simulating remote input, we are just visualizing.
        }
        
        // Report to game controller (optional, mostly for events)
        // gameController.OnNoteHit(timingResult, note); // Careful not to double count score
        
        // Remove the note from active lanes
        if (inputHandler != null)
        {
            inputHandler.RemoveNoteFromLane(note, note.lane);
        }
        
        // Destroy the note immediately with feedback
        Destroy(note.gameObject);
        
        Debug.Log($"Remote player {playerId} hit note with {timingResult}");
    }

    // Get score value for timing result
    private int GetScoreForTimingResult(TimingResult result)
    {
        if (scoreManager == null) return 0;
        
        switch (result)
        {
            case TimingResult.Perfect: return scoreManager.perfectScore;
            case TimingResult.Good: return scoreManager.goodScore;
            case TimingResult.Okay: return scoreManager.okayScore;
            default: return 0;
        }
    }

    // Handle remote player score update
    public void HandleRemoteScoreUpdate(string playerId, int score, int combo, TimingResult timingResult)
    {
        if (mpManager != null && mpManager.connectedPlayers.ContainsKey(playerId))
        {
            mpManager.connectedPlayers[playerId].score = score;
            mpManager.connectedPlayers[playerId].combo = combo;
            
            // We could show combo popup here for remote player
            Debug.Log($"Player {playerId} score updated: {score}, combo: {combo}");
        }
    }

    // Handle explicit note hit packet from server (Best for visual sync)
    public void HandleRemoteNoteHit(int laneIndex, TimingResult timingResult)
    {
        // Determine position for effects based on lane index
        if (inputHandler == null) return;
        
        // Find the target position for this lane to spawn effects there
        // We can access LaneSetup or NoteSpawner through references
        NoteSpawner noteSpawner = FindFirstObjectByType<NoteSpawner>();
        if (noteSpawner != null && laneIndex < noteSpawner.targetPositions.Length)
        {
            Vector3 targetPos = noteSpawner.targetPositions[laneIndex].position;
            
            // 1. Spawn Hit Particles
            if (SpriteEffectManager.Instance != null)
            {
                SpriteEffectManager.Instance.SpawnHitSprites(timingResult, targetPos);
            }

            // 2. Find and destroy the closest note in that lane (Visual cleanup)
            // Because the remote player already hit it, we should remove it from our screen too
            FallingNote noteToRemove = FindClosestNoteInHitWindow((NoteLane)laneIndex, Time.time); // Use generic time
            if (noteToRemove != null)
            {
                noteToRemove.isHit = true;
                Destroy(noteToRemove.gameObject);
                inputHandler.RemoveNoteFromLane(noteToRemove, (NoteLane)laneIndex);
            }
        }
    }

    // Handle server note hit result
    public void HandleServerNoteResult(string playerId, bool isHit)
    {
        Debug.Log($"Server confirmed {(isHit ? "hit" : "miss")} for player {playerId}");
    }
}