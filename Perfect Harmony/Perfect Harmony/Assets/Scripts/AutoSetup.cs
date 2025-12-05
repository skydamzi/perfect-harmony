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
        SetupSpriteEffectManager();
        SetupNetworkManagers();
        SetupGameStarter();

        Debug.Log("AutoSetup completed. Game should be ready to play!");
    }

    void SetupNetworkManagers()
    {
        // Check if we are in a multiplayer game
        MultiplayerManager mpManager = FindFirstObjectByType<MultiplayerManager>();
        if (mpManager != null && mpManager.gameStarted)
        {
            // 1. Setup MultiplayerInputHandler (Scene specific)
            MultiplayerInputHandler mpInput = FindFirstObjectByType<MultiplayerInputHandler>();
            if (mpInput == null)
            {
                GameObject obj = new GameObject("MultiplayerInputHandler");
                mpInput = obj.AddComponent<MultiplayerInputHandler>();
            }
            
            // 2. Setup GameStateSyncManager (Persistent)
            GameStateSyncManager stateSync = FindFirstObjectByType<GameStateSyncManager>();
            if (stateSync == null)
            {
                GameObject obj = new GameObject("GameStateSyncManager");
                stateSync = obj.AddComponent<GameStateSyncManager>();
            }
            // Important: Refresh references to link to new scene objects
            stateSync.RefreshReferences();

            // 3. Setup TimingSyncManager (Persistent)
            TimingSyncManager timeSync = FindFirstObjectByType<TimingSyncManager>();
            if (timeSync == null)
            {
                GameObject obj = new GameObject("TimingSyncManager");
                timeSync = obj.AddComponent<TimingSyncManager>();
            }
            // Important: Refresh references to link to new scene objects
            timeSync.RefreshReferences();
            
            Debug.Log("Network Managers Setup & Refreshed for Multiplayer");
        }
    }

    void SetupSpriteEffectManager()
    {
        SpriteEffectManager effectManager = FindFirstObjectByType<SpriteEffectManager>();
        if (effectManager == null)
        {
            GameObject obj = new GameObject("SpriteEffectManager");
            effectManager = obj.AddComponent<SpriteEffectManager>();
        }

        if (effectManager.hitSpritePrefab == null)
        {
            // Resources 폴더에서 로드 시도 (혹시 이동시켰을 경우를 대비)
            effectManager.hitSpritePrefab = Resources.Load<GameObject>("HitSprite2D");
            
            // 여전히 없다면 경고 출력
            if (effectManager.hitSpritePrefab == null)
            {
                Debug.LogWarning("SpriteEffectManager: HitSpritePrefab이 할당되지 않았습니다! 인스펙터에서 HitSprite2D 프리팹을 할당해주세요.");
            }
        }
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

        // Create required arrays if they don't exist (Size 8 for 2 players)
        if (laneSetup.spawnPositions == null || laneSetup.spawnPositions.Length != 8)
        {
            laneSetup.spawnPositions = new Transform[8];
        }

        if (laneSetup.targetPositions == null || laneSetup.targetPositions.Length != 8)
        {
            laneSetup.targetPositions = new Transform[8];
        }

        // Create spawn and target positions
        // Lanes 0-3: Left side (Host)
        // Lanes 4-7: Right side (Guest)
        for (int i = 0; i < 8; i++)
        {
            // Default positions ONLY for creation (User will move them)
            float x = 0;
            if (i < 4) x = -6.0f + (i * 1.5f); 
            else x = 1.5f + ((i - 4) * 1.5f);

            // Create spawn position IF MISSING
            if (laneSetup.spawnPositions[i] == null)
            {
                GameObject spawnPos = new GameObject($"SpawnPos_Lane{i+1}");
                spawnPos.transform.SetParent(laneSetup.transform);
                spawnPos.transform.position = new Vector3(x, 5, 0);
                laneSetup.spawnPositions[i] = spawnPos.transform;
            }
            // Do NOT update position if it exists (User control)

            // Create target position IF MISSING
            if (laneSetup.targetPositions[i] == null)
            {
                GameObject targetPos = new GameObject($"TargetPos_Lane{i+1}");
                targetPos.transform.SetParent(laneSetup.transform);
                targetPos.transform.position = new Vector3(x, -3, 0);
                laneSetup.targetPositions[i] = targetPos.transform;
            }
            // Do NOT update position if it exists (User control)
        }

        // Set values for spacing and heights
        laneSetup.laneSpacing = 1.5f; // Adjusted for tighter fit
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