using UnityEngine;

public class RhythmGameController : MonoBehaviour
{
    public static RhythmGameController Instance { get; private set; }

    [Header("Game References")]
    public InputHandler inputHandler;
    public NoteSpawner noteSpawner;
    public ScoreManager scoreManager;

    [Header("Prefabs")]
    public GameObject notePrefab; // Prefab for the falling notes

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
        if (inputHandler == null)
            inputHandler = FindFirstObjectByType<InputHandler>();

        if (noteSpawner == null)
            noteSpawner = FindFirstObjectByType<NoteSpawner>();

        if (scoreManager == null)
            scoreManager = FindFirstObjectByType<ScoreManager>();
    }

    // Called when a note is successfully hit
    public void OnNoteHit(TimingResult timingResult, FallingNote note)
    {
        if (scoreManager != null)
        {
            scoreManager.ProcessHit(timingResult);
        }

        // Add any additional logic for note hits
        Debug.Log($"Note hit! Timing: {timingResult}");
    }

    // Called when a note is missed
    public void OnNoteMissed(FallingNote note)
    {
        if (scoreManager != null)
        {
            scoreManager.ProcessMiss();
        }

        // Add any additional logic for missed notes
        Debug.Log("Note missed!");
    }

    // Start the game
    public void StartGame()
    {
        if (RhythmGameManager.Instance != null)
        {
            RhythmGameManager.Instance.StartSong();

            // Start note spawning if available
            if (noteSpawner != null)
            {
                noteSpawner.StartSpawning();
            }
        }
    }
}