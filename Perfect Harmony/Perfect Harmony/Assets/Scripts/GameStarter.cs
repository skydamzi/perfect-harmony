using UnityEngine;
using System.Collections;
using System.Collections.Generic;  // Added this for the List<T>

public class GameStarter : MonoBehaviour
{
    void Start()
    {
        // First run the auto setup to ensure all managers exist
        RunAutoSetup();

        // Wait a frame to ensure all singletons are properly initialized
        StartCoroutine(DelayedStart());
    }

    private System.Collections.IEnumerator DelayedStart()
    {
        yield return new WaitForEndOfFrame();

        // Start the countdown before the game begins
        if(RhythmGameManager.Instance != null)
        {
            RhythmGameManager.Instance.StartCountdown();
            Debug.Log("Countdown started. Game will start in 3 seconds!");
        }
        else
        {
            Debug.LogError("RhythmGameManager instance not found!");
        }
    }

    private void RunAutoSetup()
    {
        // Create all required managers if they don't exist
        EnsureManagerExists<LaneSetup>("LaneSetup", SetupLaneSetupComponent);
        EnsureManagerExists<NoteSpawner>("NoteSpawner", SetupNoteSpawnerComponent);
        EnsureManagerExists<RhythmGameController>("RhythmGameController", SetupRhythmGameControllerComponent);
        EnsureManagerExists<ScoreManager>("ScoreManager", SetupScoreManagerComponent);
        EnsureManagerExists<InputHandler>("InputHandler", SetupInputHandlerComponent);
        EnsureManagerExists<RhythmGameManager>("RhythmGameManager", SetupRhythmGameManagerComponent);

        Debug.Log("Auto setup completed. Game ready!");
    }

    // Helper method to ensure a manager exists
    private T EnsureManagerExists<T>(string objectName, System.Action<T> setupAction) where T : Component
    {
        T component = FindFirstObjectByType<T>();
        if (component == null)
        {
            GameObject obj = new GameObject(objectName);
            component = obj.AddComponent<T>();

            // Run the specific setup action for this component
            setupAction?.Invoke(component);
        }

        return component;
    }

    // Setup actions for each component
    private void SetupLaneSetupComponent(LaneSetup laneSetup)
    {
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

    private void SetupNoteSpawnerComponent(NoteSpawner noteSpawner)
    {
        // Assign the note prefab
        if (noteSpawner.notePrefab == null)
        {
            // Try to find the FallingNote prefab
            noteSpawner.notePrefab = Resources.Load<GameObject>("FallingNote");
            if (noteSpawner.notePrefab == null)
            {
                // Search in the Prefebs folder
                noteSpawner.notePrefab = Resources.Load<GameObject>("Prefebs/FallingNote");
            }

            // If still not found, try to get from scene objects
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


        // RhythmGameManager에 이미 곡(SongData)이 선택되어 있다면, 랜덤 패턴을 만들지 마라!
        RhythmGameManager manager = RhythmGameManager.Instance;

        // 만약 매니저가 있고, 매니저에 선택된 곡이 있고, 그 곡에 채보 데이터가 있다면?
        if (manager != null && manager.selectedSong != null && manager.selectedSong.chartData.Count > 0)
        {
            // 아무것도 하지 않음 (이미 Manager가 LoadSong에서 데이터를 넣어줬을 테니까)
            Debug.Log("GameStarter: SongData가 감지되어 기본 패턴 생성을 건너뜁니다.");
        }
        else if (noteSpawner.spawnEvents.Count == 0)
        {
            // 데이터가 없을 때만 테스트용  패턴 생성
            CreateMusicBasedSpawnPattern(noteSpawner);
        }
    }

    // Create a musically structured pattern based on 4/4 rhythm with varied note placement
    // 곡데이터 없을때 테스트패턴
    private void CreateMusicBasedSpawnPattern(NoteSpawner noteSpawner)
    {
        // Start with a 16-beat pattern that reflects common 4/4 drum patterns
        // Each measure has 4 beats (0-3, 4-7, 8-11, 12-15)

        // Measure 1: Basic beat pattern
        noteSpawner.AddSpawnEvent(0, NoteLane.Lane1); // Strong beat (kick)
        noteSpawner.AddSpawnEvent(1, NoteLane.Lane3); // Snare-like
        noteSpawner.AddSpawnEvent(2, NoteLane.Lane1); // Kick
        noteSpawner.AddSpawnEvent(3, NoteLane.Lane4); // Hi-hat or accent

        // Measure 2: Slightly varied pattern
        noteSpawner.AddSpawnEvent(4, NoteLane.Lane1); // Kick
        noteSpawner.AddSpawnEvent(5, NoteLane.Lane2); // Different snare
        noteSpawner.AddSpawnEvent(6, NoteLane.Lane1); // Kick
        noteSpawner.AddSpawnEvent(7, NoteLane.Lane4); // Accent

        // Measure 3: More complex rhythm
        noteSpawner.AddSpawnEvent(8, NoteLane.Lane1);  // Kick on 1
        noteSpawner.AddSpawnEvent(9, NoteLane.Lane3);  // Off-beat
        noteSpawner.AddSpawnEvent(10, NoteLane.Lane1); // Kick
        noteSpawner.AddSpawnEvent(11, NoteLane.Lane2); // Different accent

        // Measure 4: Resolution pattern
        noteSpawner.AddSpawnEvent(12, NoteLane.Lane1); // Strong kick
        noteSpawner.AddSpawnEvent(13, NoteLane.Lane4); // Hi-hat
        noteSpawner.AddSpawnEvent(14, NoteLane.Lane2); // Build up
        noteSpawner.AddSpawnEvent(15, NoteLane.Lane1); // Strong finish

        // Extended pattern for more complexity (second 16 beats)
        // Measure 5: Syncopated rhythm
        noteSpawner.AddSpawnEvent(16, NoteLane.Lane1); // Kick
        noteSpawner.AddSpawnEvent(17, NoteLane.Lane2); // Off-beat
        noteSpawner.AddSpawnEvent(18, NoteLane.Lane1); // Kick
        noteSpawner.AddSpawnEvent(19, NoteLane.Lane4); // Accent

        // Measure 6: Different pattern
        noteSpawner.AddSpawnEvent(20, NoteLane.Lane1); // Kick
        noteSpawner.AddSpawnEvent(21, NoteLane.Lane3); // Snare
        noteSpawner.AddSpawnEvent(22, NoteLane.Lane2); // Off-beat
        noteSpawner.AddSpawnEvent(23, NoteLane.Lane4); // Accent

        // Measure 7: Building up
        noteSpawner.AddSpawnEvent(24, NoteLane.Lane1); // Kick
        noteSpawner.AddSpawnEvent(25, NoteLane.Lane2); // Upbeat
        noteSpawner.AddSpawnEvent(26, NoteLane.Lane3); // Mid beat
        noteSpawner.AddSpawnEvent(27, NoteLane.Lane4); // High beat

        // Measure 8: Climax pattern
        noteSpawner.AddSpawnEvent(28, NoteLane.Lane1); // Strong beat
        noteSpawner.AddSpawnEvent(29, NoteLane.Lane3); // Counter beat
        noteSpawner.AddSpawnEvent(30, NoteLane.Lane2); // Build up
        noteSpawner.AddSpawnEvent(31, NoteLane.Lane1); // Strong resolution
    }

    private void SetupRhythmGameControllerComponent(RhythmGameController controller)
    {
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

    private void SetupScoreManagerComponent(ScoreManager scoreManager)
    {
        // Try to connect UI elements
        ConnectUIToScoreManager(scoreManager);
    }

    private void ConnectUIToScoreManager(ScoreManager scoreManager)
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
                    scoreManager.scoreText = child.GetComponent<UnityEngine.UI.Text>();
                }
                else if (child.name == "ComboText" && scoreManager.comboText == null)
                {
                    scoreManager.comboText = child.GetComponent<UnityEngine.UI.Text>();
                }
                else if (child.name == "TimingText" && scoreManager.timingText == null)
                {
                    scoreManager.timingText = child.GetComponent<UnityEngine.UI.Text>();
                }
            }
        }
    }

    private void SetupInputHandlerComponent(InputHandler inputHandler)
    {
        // Ensure key setup
        if (inputHandler.laneKeys == null || inputHandler.laneKeys.Length == 0)
        {
            inputHandler.laneKeys = new KeyCode[] { KeyCode.D, KeyCode.F, KeyCode.J, KeyCode.K };
        }
    }

    private void SetupRhythmGameManagerComponent(RhythmGameManager rhythmManager)
    {
        // Set default BPM if needed
        if (rhythmManager.beatsPerMinute <= 0)
        {
            rhythmManager.beatsPerMinute = 120f;
        }
    }
}