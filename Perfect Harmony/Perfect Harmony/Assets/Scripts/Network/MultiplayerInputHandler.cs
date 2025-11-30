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
        
        // Calculate score based on timing result
        int scoreToAdd = GetScoreForTimingResult(timingResult);
        
        // Update score
        if (scoreManager != null)
        {
            // For remote player, we don't update the local score directly
            // Instead we trust the server's judgment and score update
            scoreManager.ProcessHit(timingResult);
        }

        // Send score update to server
        if (mpManager != null)
        {
            mpManager.SendPlayerScore(scoreManager.currentScore, scoreManager.currentCombo, timingResult);
        }
        
        // Report to game controller
        gameController.OnNoteHit(timingResult, note);
        
        // Remove the note from active lanes
        if (inputHandler != null)
        {
            inputHandler.RemoveNoteFromLane(note, note.lane);
        }
        
        // Destroy the note
        Destroy(note.gameObject, 0.1f);
        
        Debug.Log($"Remote player {playerId} hit note with {timingResult}, score: {scoreToAdd}");
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
            
            Debug.Log($"Player {playerId} score updated: {score}, combo: {combo}");
        }
    }

    // Handle server note hit result
    public void HandleServerNoteResult(string playerId, bool isHit)
    {
        Debug.Log($"Server confirmed {(isHit ? "hit" : "miss")} for player {playerId}");
    }
}