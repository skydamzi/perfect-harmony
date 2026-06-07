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

        // [수정] 모든 타이밍 기준을 realtimeSinceStartup으로 통일
        float inputTime = Time.realtimeSinceStartup;
        mpManager.SendPlayerInput(laneIndex, inputTime);
        
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
        // [수정] 이제 상대방의 입력을 직접 판정(Prediction)하지 않습니다.
        // 대신 상대방이 보낸 결과 패킷(HandleRemoteNoteHit)만 믿고 처리합니다.
        // 이렇게 해야 기기간 핑 차이로 인해 이펙트가 일찍 터지는 현상을 막을 수 있습니다.
        Debug.Log($"Remote input signal received from {playerId} on lane {laneIndex}");
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

    // Handle explicit note hit packet from server (Best for visual sync)
    public void HandleRemoteNoteHit(int laneIndex, TimingResult timingResult, float beatNumber)
    {
        // Determine position for effects based on lane index
        if (inputHandler == null) return;
        
        NoteSpawner noteSpawner = FindFirstObjectByType<NoteSpawner>();
        if (noteSpawner != null && laneIndex < noteSpawner.targetPositions.Length)
        {
            Vector3 targetPos = noteSpawner.targetPositions[laneIndex].position;
            
            // 1. Spawn Hit Particles
            if (SpriteEffectManager.Instance != null)
            {
                SpriteEffectManager.Instance.SpawnHitSprites(timingResult, targetPos);
            }

            // 2. Find and destroy the specific note in that lane based on beatNumber
            FallingNote noteToRemove = FindNoteByBeatNumber((NoteLane)laneIndex, beatNumber);
            if (noteToRemove != null)
            {
                noteToRemove.isHit = true;
                Destroy(noteToRemove.gameObject);
                inputHandler.RemoveNoteFromLane(noteToRemove, (NoteLane)laneIndex);
            }
        }
    }

    private FallingNote FindNoteByBeatNumber(NoteLane lane, float beatNumber)
    {
        if (inputHandler == null) return null;
        
        var activeNotes = inputHandler.GetActiveNotesInLane(lane);
        foreach (FallingNote note in activeNotes)
        {
            // Use a small epsilon for float comparison
            if (note != null && !note.isHit && !note.isMissed && Mathf.Abs(note.beatNumber - beatNumber) < 0.01f)
            {
                return note;
            }
        }

        return null;
    }
}
