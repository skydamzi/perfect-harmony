using UnityEngine;
using UnityEngine.UI;

public class AutoSetup : MonoBehaviour
{
    void Start()
    {
        SetupLaneSetup();
        SetupNoteSpawner();
        SetupRhythmGameController();
        SetupScoreManager();
        SetupInputHandler();
        SetupRhythmGameManager();
        SetupGameStarter();

        Debug.Log("AutoSetup completed. Game should be ready to play!");
    }

    void SetupLaneSetup()
    {
        LaneSetup laneSetup = FindFirstObjectByType<LaneSetup>();
        if (laneSetup == null)
        {
            // Create LaneSetup if it doesn't exist
            GameObject laneSetupObj = new GameObject("LaneSetup");
            laneSetup = laneSetupObj.AddComponent<LaneSetup>();
        }

        // Create required arrays if they don't exist
        if (laneSetup.spawnPositions == null || laneSetup.spawnPositions.Length != 4)
        {
            laneSetup.spawnPositions = new Transform[4];
        }

        if (laneSetup.targetPositions == null || laneSetup.targetPositions.Length != 4)
        {
            laneSetup.targetPositions = new Transform[4];
        }

        // Create spawn and target positions
        for (int i = 0; i < 4; i++)
        {
            float x = -3 + i * 2; // -3, -1, 1, 3

            // Create spawn position
            if (laneSetup.spawnPositions[i] == null)
            {
                GameObject spawnPos = new GameObject($"SpawnPos_Lane{i+1}");
                spawnPos.transform.SetParent(laneSetup.transform);
                spawnPos.transform.position = new Vector3(x, 5, 0);
                laneSetup.spawnPositions[i] = spawnPos.transform;
            }

            // Create target position
            if (laneSetup.targetPositions[i] == null)
            {
                GameObject targetPos = new GameObject($"TargetPos_Lane{i+1}");
                targetPos.transform.SetParent(laneSetup.transform);
                targetPos.transform.position = new Vector3(x, -3, 0);
                laneSetup.targetPositions[i] = targetPos.transform;
            }
        }

        // Set values for spacing and heights
        laneSetup.laneSpacing = 2.0f;
        laneSetup.spawnHeight = 5.0f;
        laneSetup.targetHeight = -3.0f;
    }

    void SetupNoteSpawner()
    {
        NoteSpawner noteSpawner = FindFirstObjectByType<NoteSpawner>();
        if (noteSpawner == null)
        {
            // Create NoteSpawner if it doesn't exist
            GameObject noteSpawnerObj = new GameObject("NoteSpawner");
            noteSpawner = noteSpawnerObj.AddComponent<NoteSpawner>();
        }

        // Assign the note prefab
        if (noteSpawner.notePrefab == null)
        {
            // Try to find the FallingNote prefab in Prefebs folder
            noteSpawner.notePrefab = Resources.Load<GameObject>("FallingNote");
            if (noteSpawner.notePrefab == null)
            {
                // Try to load directly from the Prefebs folder
                GameObject prefab = GameObject.Instantiate(Resources.Load<GameObject>("Prefebs/FallingNote")) as GameObject;
                if (prefab != null)
                {
                    noteSpawner.notePrefab = prefab;
                }
            }

            // If still not found, try getting from existing instances
            if (noteSpawner.notePrefab == null)
            {
                FallingNote existingNote = FindFirstObjectByType<FallingNote>();
                if (existingNote != null)
                {
                    noteSpawner.notePrefab = existingNote.gameObject;
                }
            }
        }

        // Connect the lane positions
        LaneSetup laneSetup = FindFirstObjectByType<LaneSetup>();
        if (laneSetup != null)
        {
            noteSpawner.spawnPositions = laneSetup.spawnPositions;
            noteSpawner.targetPositions = laneSetup.targetPositions;
        }

        // Add some default spawn events if none exist
        if (noteSpawner.spawnEvents.Count == 0)
        {
            // Create a simple pattern for 16 beats
            for (int beat = 0; beat < 16; beat++)
            {
                NoteLane lane = (NoteLane)(beat % 4);
                noteSpawner.AddSpawnEvent(beat, lane);
            }
        }
    }

    void SetupRhythmGameController()
    {
        RhythmGameController controller = FindFirstObjectByType<RhythmGameController>();
        if (controller == null)
        {
            // Create RhythmGameController if it doesn't exist
            GameObject controllerObj = new GameObject("RhythmGameController");
            controller = controllerObj.AddComponent<RhythmGameController>();

            // Find and assign the required components
            controller.inputHandler = FindFirstObjectByType<InputHandler>();
            controller.noteSpawner = FindFirstObjectByType<NoteSpawner>();
            controller.scoreManager = FindFirstObjectByType<ScoreManager>();

            // Try to find the note prefab again if not set
            if (controller.notePrefab == null)
            {
                NoteSpawner noteSpawner = FindFirstObjectByType<NoteSpawner>();
                if (noteSpawner != null)
                {
                    controller.notePrefab = noteSpawner.notePrefab;
                }
            }
        }
    }

    void SetupScoreManager()
    {
        ScoreManager scoreManager = FindFirstObjectByType<ScoreManager>();
        if (scoreManager == null)
        {
            // Create ScoreManager if it doesn't exist
            GameObject scoreManagerObj = new GameObject("ScoreManager");
            scoreManager = scoreManagerObj.AddComponent<ScoreManager>();
        }

        // Try to connect UI elements
        ConnectUIToScoreManager(scoreManager);
    }

    void ConnectUIToScoreManager(ScoreManager scoreManager)
    {
        // Find Canvas and UI elements
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas != null)
        {
            // Find child UI elements
            foreach (Transform child in canvas.transform)
            {
                if (child.name == "ScoreText" && scoreManager.scoreText == null)
                {
                    scoreManager.scoreText = child.GetComponent<Text>();
                }
                else if (child.name == "ComboText" && scoreManager.comboText == null)
                {
                    scoreManager.comboText = child.GetComponent<Text>();
                }
                else if (child.name == "TimingText" && scoreManager.timingText == null)
                {
                    scoreManager.timingText = child.GetComponent<Text>();
                }
            }
        }
    }

    void SetupInputHandler()
    {
        InputHandler inputHandler = FindFirstObjectByType<InputHandler>();
        if (inputHandler == null)
        {
            GameObject inputHandlerObj = new GameObject("InputHandler");
            inputHandler = inputHandlerObj.AddComponent<InputHandler>();
        }

        // Ensure key setup
        if (inputHandler.laneKeys == null || inputHandler.laneKeys.Length == 0)
        {
            inputHandler.laneKeys = new KeyCode[] { KeyCode.D, KeyCode.F, KeyCode.J, KeyCode.K };
        }
    }

    void SetupRhythmGameManager()
    {
        RhythmGameManager rhythmManager = FindFirstObjectByType<RhythmGameManager>();
        if (rhythmManager == null)
        {
            GameObject rhythmManagerObj = new GameObject("RhythmGameManager");
            rhythmManager = rhythmManagerObj.AddComponent<RhythmGameManager>();
        }

        // Set default BPM if needed
        if (rhythmManager.beatsPerMinute <= 0)
        {
            rhythmManager.beatsPerMinute = 120f;
        }
    }

    void SetupGameStarter()
    {
        GameStarter gameStarter = FindFirstObjectByType<GameStarter>();
        if (gameStarter == null)
        {
            GameObject gameStarterObj = new GameObject("GameStarter");
            gameStarter = gameStarterObj.AddComponent<GameStarter>();
        }
    }
}