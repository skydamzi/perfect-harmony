using UnityEngine;

public class RhythmGameManager : MonoBehaviour
{
    public static RhythmGameManager Instance { get; private set; }

    [Header("Game Settings")]
    public float beatsPerMinute = 120f;
    public float beatDuration; // Calculated from BPM
    public int beatsPerMeasure = 4;

    [Header("Timing Windows")]
    public float perfectWindow = 0.1f;
    public float goodWindow = 0.2f;
    public float okayWindow = 0.3f;

    [Header("Game State")]
    public bool isPlaying = false;
    public bool isCountingDown = false; // Whether we're in the countdown phase
    public float songPosition; // Current position in the song in seconds
    public float beatProgress; // Progress from 0 to 1 within the current beat
    public int currentBeat;
    public int currentMeasure;

    [Header("Timing")]
    public float spawnOffset = 2.0f; // How many seconds ahead to spawn notes
    public float startDelay = 3.0f; // Delay before starting the song (default 3 seconds)

    [Header("UI References")]
    public UnityEngine.UI.Text countdownText; // UI text to show countdown

    private float beatTime; // Time of the next beat
    public float songStartTime; // Public access for external scripts
    public float actualSongStartTime; // Time when the song actually starts (with delay) - made public for access

    private void Awake()
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

    private void Start()
    {
        beatDuration = 60f / beatsPerMinute;
        beatTime = Time.time;
    }

    private void Update()
    {
        // Handle countdown before song starts
        if (isCountingDown)
        {
            float countdownTime = Time.time - songStartTime;
            float remainingTime = startDelay - countdownTime;

            // Update countdown text if it exists
            if (countdownText != null)
            {
                if (remainingTime > 2)
                {
                    countdownText.text = "3";
                }
                else if (remainingTime > 1)
                {
                    countdownText.text = "2";
                }
                else if (remainingTime > 0)
                {
                    countdownText.text = "1";
                }
                else if (remainingTime < 0)
                {
                    countdownText.text = "시작!";
                }
            }

            if (countdownTime >= startDelay + 1f)
            {
                // Countdown finished, start the song
                StartSong();

                // Clear the countdown text
                if (countdownText != null)
                {
                    countdownText.text = "";
                }
            }
            return;
        }

        if (isPlaying)
        {
            // Calculate current song position
            songPosition = Time.time - actualSongStartTime;

            // Calculate which beat we're on
            currentBeat = Mathf.FloorToInt(songPosition / beatDuration);
            currentMeasure = Mathf.FloorToInt(currentBeat / beatsPerMeasure);

            // Calculate progress within the current beat (0 to 1)
            beatProgress = (songPosition % beatDuration) / beatDuration;

            // Update beat time for next beat
            if (Time.time >= beatTime)
            {
                beatTime += beatDuration;
            }
        }
    }

    public void StartSong()
    {
        // Only proceed if we're not already playing
        if (isPlaying) return;

        isCountingDown = false;
        isPlaying = true;
        actualSongStartTime = Time.time; // The actual time when the song starts after the delay
        songPosition = 0f; // Reset song position to 0 at start

        // Start note spawning after the song officially starts
        NoteSpawner noteSpawner = FindFirstObjectByType<NoteSpawner>();
        if (noteSpawner != null)
        {
            noteSpawner.StartSpawning();
        }
    }

    // Start the countdown before the song actually plays
    public void StartCountdown()
    {
        songStartTime = Time.time; // Set the initial start time for the countdown
        isCountingDown = true;
        isPlaying = false; // We're not playing yet, just counting down
    }

    public void StopSong()
    {
        isPlaying = false;
    }

    // Convert beat number to time in seconds
    public float BeatToTime(int beatNumber)
    {
        return beatNumber * beatDuration;
    }

    // Convert time in seconds to beat number
    public int TimeToBeat(float time)
    {
        return Mathf.FloorToInt(time / beatDuration);
    }

    // Check timing accuracy based on when the note was hit vs when it should be hit
    public TimingResult CheckTiming(float hitTime, float targetTime)
    {
        float timeDifference = Mathf.Abs(hitTime - targetTime);

        if (timeDifference <= perfectWindow)
            return TimingResult.Perfect;
        else if (timeDifference <= goodWindow)
            return TimingResult.Good;
        else if (timeDifference <= okayWindow)
            return TimingResult.Okay;
        else
            return TimingResult.Miss; // This shouldn't happen in normal circumstances
    }
}