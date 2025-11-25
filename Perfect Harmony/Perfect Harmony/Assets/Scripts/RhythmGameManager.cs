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
    public float songPosition; // Current position in the song in seconds
    public float beatProgress; // Progress from 0 to 1 within the current beat
    public int currentBeat;
    public int currentMeasure;

    [Header("Timing")]
    public float spawnOffset = 2.0f; // How many seconds ahead to spawn notes

    private float beatTime; // Time of the next beat
    public float songStartTime; // Public access for external scripts

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
        songStartTime = Time.time;
    }

    private void Update()
    {
        if (isPlaying)
        {
            // Calculate current song position
            songPosition = Time.time - songStartTime;

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
        isPlaying = true;
        songStartTime = Time.time;
        songPosition = 0f; // Reset song position to 0 at start
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