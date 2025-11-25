using UnityEngine;

public class FallingNote : MonoBehaviour
{
    [Header("Note Properties")]
    public NoteLane lane; // The lane this note belongs to
    public int beatNumber; // The beat this note should be hit
    public float spawnTime; // When this note was spawned
    public float targetTime; // When this note should be hit (based on beatNumber)

    [Header("Movement")]
    public float fallSpeed = 5.0f; // Increased for better visibility
    public Transform targetPosition; // Where the note should be hit
    public Transform spawnPosition; // Where the note starts from

    private float startTime;
    public bool isHit = false;
    public bool isMissed = false;

    void Start()
    {
        startTime = Time.time;
        spawnTime = Time.time;

        // Calculate when this note should reach the target based on beat number
        targetTime = RhythmGameManager.Instance.BeatToTime(beatNumber);

        // Set the initial position to the spawn position if available
        if (spawnPosition != null)
        {
            transform.position = spawnPosition.position;
        }

        // Register this note with the input handler
        InputHandler inputHandler = FindFirstObjectByType<InputHandler>();
        if (inputHandler != null)
        {
            inputHandler.AddNoteToLane(this, lane);
        }
    }

    void Update()
    {
        if (RhythmGameManager.Instance.isPlaying && !isHit && !isMissed)
        {
            // Calculate the current song time relative to when this note should appear
            float currentTime = Time.time;
            float noteAppearTime = RhythmGameManager.Instance.BeatToTime(beatNumber) - RhythmGameManager.Instance.spawnOffset;
            float noteTargetTime = RhythmGameManager.Instance.BeatToTime(beatNumber);

            if (currentTime >= noteAppearTime)
            {
                // Calculate the time duration for the note to travel from spawn to target
                float travelDuration = RhythmGameManager.Instance.spawnOffset; // Time from spawn to target

                // Calculate progress from 0 to 1 over the travel duration
                float timeSinceAppear = currentTime - noteAppearTime;
                float progress = timeSinceAppear / travelDuration;

                // Clamp progress between 0 and 1 to prevent going past target
                progress = Mathf.Clamp01(progress);

                transform.position = Vector3.Lerp(spawnPosition.position, targetPosition.position, progress);

                // Check if note is missed based on target time
                if (currentTime > noteTargetTime + RhythmGameManager.Instance.okayWindow && !isMissed)
                {
                    MissNote();
                }
            }
        }
    }

    // Called when player hits the note at the right time
    public void HitNote(TimingResult timingResult)
    {
        if (isHit || isMissed) return;

        isHit = true;

        // Report hit to game manager
        RhythmGameController.Instance.OnNoteHit(timingResult, this);

        // Visual/auditory feedback based on timing
        HandleHitVisuals(timingResult);

        // Destroy the note after a short delay
        Destroy(gameObject, 0.1f);
    }

    // Called when the note goes past the hit window without being hit
    public void MissNote()
    {
        if (isHit || isMissed) return;

        isMissed = true;

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