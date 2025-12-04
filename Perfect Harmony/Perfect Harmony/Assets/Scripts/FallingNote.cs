using UnityEngine;

public class FallingNote : MonoBehaviour
{
    [Header("Note Properties")]
    public NoteLane lane; // The lane this note belongs to
    public float beatNumber; // The beat this note should be hit
    public float spawnTime; // When this note was spawned
    public float targetTime; // When this note should be hit (based on beatNumber)

    [Header("Movement")]
    public float fallSpeed = 5.0f; // Increased for better visibility
    public Transform targetPosition; // Where the note should be hit
    public Transform spawnPosition; // Where the note starts from

    private float startTime;
    public bool isHit = false;
    public bool isMissed = false;

    // OnNoteHit method that should be called from external components
    public void OnNoteHit(TimingResult timingResult)
    {
        if (!isHit && !isMissed)
        {
            isHit = true;

            // Trigger sprite effect based on timing result
            if (SpriteEffectManager.Instance != null)
            {
                SpriteEffectManager.Instance.SpawnHitSprites(timingResult, transform.position);
            }

            // Notify input handler to unregister this note
            if (InputHandler.Instance != null)
            {
                InputHandler.Instance.UnregisterNote(this);
            }

            // Report hit to game manager
            if (RhythmGameController.Instance != null)
            {
                RhythmGameController.Instance.OnNoteHit(timingResult, this);
            }

            // Visual/auditory feedback based on timing
            // Change color based on timing result
            SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                switch (timingResult)
                {
                    case TimingResult.Perfect:
                        spriteRenderer.color = Color.yellow; // Perfect hits show yellow
                        break;
                    case TimingResult.Good:
                        spriteRenderer.color = Color.green; // Good hits show green
                        break;
                    case TimingResult.Okay:
                        spriteRenderer.color = Color.blue; // Okay hits show blue
                        break;
                }
            }

            // Destroy the note after a short delay
            Destroy(gameObject, 0.1f);
        }
    }

    void Start()
    {
        startTime = Time.time;
        spawnTime = Time.time;

        // Calculate when this note should reach the target based on beat number
        // Add the delay time to get the actual target time
        targetTime = RhythmGameManager.Instance.actualSongStartTime + RhythmGameManager.Instance.BeatToTime(beatNumber);

        // Set the initial position to the spawn position if available
        if (spawnPosition != null)
        {
            transform.position = spawnPosition.position;
        }

        // Registration is now handled by NoteSpawner to prevent double registration
        // InputHandler inputHandler = FindFirstObjectByType<InputHandler>();
        // if (inputHandler != null)
        // {
        //     inputHandler.AddNoteToLane(this, lane);
        // }
    }

    void Update()
    {
        if (RhythmGameManager.Instance.isPlaying && !isHit && !isMissed)
        {
            // Calculate the current song time relative to when this note should appear
            float currentTime = Time.time;
            // Calculate when this note should appear based on its beat number from the actual song start
            float noteAppearTimeInSong = RhythmGameManager.Instance.BeatToTime(beatNumber) - RhythmGameManager.Instance.spawnOffset;
            // Add the delay time to when the song actually started to get the actual appearance time
            float actualNoteAppearTime = RhythmGameManager.Instance.actualSongStartTime + noteAppearTimeInSong;

            float noteTargetTimeInSong = RhythmGameManager.Instance.BeatToTime(beatNumber);
            float actualNoteTargetTime = RhythmGameManager.Instance.actualSongStartTime + noteTargetTimeInSong;

            if (currentTime >= actualNoteAppearTime)
            {
                // Calculate the time duration for the note to travel from spawn to target
                float travelDuration = RhythmGameManager.Instance.spawnOffset; // Time from spawn to target

                // Calculate progress from 0 to 1 (and beyond) over the travel duration
                float timeSinceAppear = currentTime - actualNoteAppearTime;
                float progress = timeSinceAppear / travelDuration;

                // Allow progress to go beyond 1 so note falls past target
                // progress = Mathf.Clamp01(progress); 

                transform.position = Vector3.LerpUnclamped(spawnPosition.position, targetPosition.position, progress);

                // Check if note is missed based on target time
                if (currentTime > actualNoteTargetTime + RhythmGameManager.Instance.okayWindow && !isMissed)
                {
                    MissNote();
                }
            }
        }
    }

    // Called when player hits the note at the right time
    public void HitNote(TimingResult timingResult)
    {
        // Simply call OnNoteHit since it contains all the required logic
        OnNoteHit(timingResult);
    }

    // Called when the note goes past the hit window without being hit
    public void MissNote()
    {
        if (isHit || isMissed) return;

        isMissed = true;

        // Notify input handler to unregister this note
        if (InputHandler.Instance != null)
        {
            InputHandler.Instance.UnregisterNote(this);
        }

        // Report miss to game manager
        RhythmGameController.Instance.OnNoteMissed(this);

        // Visual feedback for miss
        HandleMissVisuals();

        // Destroy the note after a short delay
        Destroy(gameObject, 0.1f);
    }

    // Visual feedback when note is hit
    private void HandleHitVisuals(TimingResult timingResult)
    {
        // Change color based on timing result
        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            switch (timingResult)
            {
                case TimingResult.Perfect:
                    spriteRenderer.color = Color.yellow; // Perfect hits show yellow
                    break;
                case TimingResult.Good:
                    spriteRenderer.color = Color.green; // Good hits show green
                    break;
                case TimingResult.Okay:
                    spriteRenderer.color = Color.blue; // Okay hits show blue
                    break;
            }
        }
    }

    // Visual feedback when note is missed
    private void HandleMissVisuals()
    {
        // Change color to red to indicate miss
        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            spriteRenderer.color = Color.red;
        }
    }
}