using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class GameStarter : MonoBehaviour
{
    void Start()
    {
        // 1. 매니저들 셋업만 해두고 서버 응답을 기다림
        RunAutoSetup();
    }

    // 서버 분석이 완료되면 ChartManager(ChartRequester)에서 이 함수를 호출함
    public void StartGameAfterAnalysis()
    {
        MultiplayerManager mpManager = FindFirstObjectByType<MultiplayerManager>();
        if (mpManager != null && mpManager.gameStarted)
        {
            // 멀티플레이어인 경우: 서버에 내 채보 분석이 끝났음을 알리고 모두가 준비될 때까지 대기
            mpManager.SendChartReady();
            Debug.Log("<color=cyan>채보 분석 완료! 다른 플레이어의 준비를 기다립니다...</color>");
        }
        else if (RhythmGameManager.Instance != null)
        {
            // 싱글플레이어인 경우: 기존처럼 즉시 카운트다운 시작
            RhythmGameManager.Instance.StartCountdown();
            Debug.Log("<color=green>채보 로드 완료! 카운트다운 시작!</color>");
        }
        else
        {
            Debug.LogError("RhythmGameManager Instance를 찾을 수 없습니다!");
        }
    }

    private void RunAutoSetup()
    {
        // 필요한 모든 매니저 컴포넌트 자동 생성 및 연결
        EnsureManagerExists<LaneSetup>("LaneSetup", SetupLaneSetupComponent);
        EnsureManagerExists<NoteSpawner>("NoteSpawner", SetupNoteSpawnerComponent);
        EnsureManagerExists<RhythmGameController>("RhythmGameController", SetupRhythmGameControllerComponent);
        EnsureManagerExists<ScoreManager>("ScoreManager", SetupScoreManagerComponent);
        EnsureManagerExists<InputHandler>("InputHandler", SetupInputHandlerComponent);
        EnsureManagerExists<RhythmGameManager>("RhythmGameManager", SetupRhythmGameManagerComponent);

        SetupNetworkManagers();
        Debug.Log("Auto setup completed. Waiting for Server Analysis...");
    }

    // --- 헬퍼 함수 모음 ---

    private T EnsureManagerExists<T>(string objectName, System.Action<T> setupAction) where T : Component
    {
        T component = FindFirstObjectByType<T>();
        if (component == null)
        {
            GameObject obj = new GameObject(objectName);
            component = obj.AddComponent<T>();
            setupAction?.Invoke(component);
        }
        return component;
    }

    private void SetupLaneSetupComponent(LaneSetup laneSetup)
    {
        if (laneSetup.spawnPositions == null || laneSetup.spawnPositions.Length != 8)
            laneSetup.spawnPositions = new Transform[8];

        if (laneSetup.targetPositions == null || laneSetup.targetPositions.Length != 8)
            laneSetup.targetPositions = new Transform[8];

        for (int i = 0; i < 8; i++)
        {
            float x = (i < 4) ? -6.0f + (i * 1.5f) : 1.5f + ((i - 4) * 1.5f);

            if (laneSetup.spawnPositions[i] == null)
            {
                GameObject spawnPos = new GameObject($"SpawnPos_Lane{i + 1}");
                spawnPos.transform.SetParent(laneSetup.transform);
                spawnPos.transform.position = new Vector3(x, 5, 0);
                laneSetup.spawnPositions[i] = spawnPos.transform;
            }

            if (laneSetup.targetPositions[i] == null)
            {
                GameObject targetPos = new GameObject($"TargetPos_Lane{i + 1}");
                targetPos.transform.SetParent(laneSetup.transform);
                targetPos.transform.position = new Vector3(x, -3, 0);
                laneSetup.targetPositions[i] = targetPos.transform;
            }
        }
        laneSetup.laneSpacing = 1.5f;
        laneSetup.spawnHeight = 5.0f;
        laneSetup.targetHeight = -3.0f;
    }

    private void SetupNoteSpawnerComponent(NoteSpawner noteSpawner)
    {
        if (noteSpawner.notePrefab == null)
        {
            noteSpawner.notePrefab = Resources.Load<GameObject>("FallingNote") ?? Resources.Load<GameObject>("Prefebs/FallingNote");
        }

        LaneSetup laneSetup = FindFirstObjectByType<LaneSetup>();
        if (laneSetup != null)
        {
            noteSpawner.spawnPositions = laneSetup.spawnPositions;
            noteSpawner.targetPositions = laneSetup.targetPositions;
        }
    }

    private void SetupRhythmGameControllerComponent(RhythmGameController controller)
    {
        controller.inputHandler = FindFirstObjectByType<InputHandler>();
        controller.noteSpawner = FindFirstObjectByType<NoteSpawner>();
        controller.scoreManager = FindFirstObjectByType<ScoreManager>();
    }

    private void SetupScoreManagerComponent(ScoreManager scoreManager)
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas != null)
        {
            foreach (Transform child in canvas.transform)
            {
                if (child.name == "ScoreText") scoreManager.scoreText = child.GetComponent<UnityEngine.UI.Text>();
                else if (child.name == "ComboText") scoreManager.comboText = child.GetComponent<UnityEngine.UI.Text>();
                else if (child.name == "TimingText") scoreManager.timingText = child.GetComponent<UnityEngine.UI.Text>();
            }
        }
    }

    private void SetupInputHandlerComponent(InputHandler inputHandler)
    {
        if (inputHandler.laneKeys == null || inputHandler.laneKeys.Length == 0)
            inputHandler.laneKeys = new KeyCode[] { KeyCode.D, KeyCode.F, KeyCode.J, KeyCode.K };
    }

    private void SetupRhythmGameManagerComponent(RhythmGameManager rhythmManager)
    {
        if (rhythmManager.beatsPerMinute <= 0) rhythmManager.beatsPerMinute = 120f;
    }

    private void SetupNetworkManagers()
    {
        MultiplayerManager mpManager = FindFirstObjectByType<MultiplayerManager>();
        if (mpManager != null && mpManager.gameStarted)
        {
            if (FindFirstObjectByType<MultiplayerInputHandler>() == null) new GameObject("MultiplayerInputHandler").AddComponent<MultiplayerInputHandler>();
            GameStateSyncManager stateSync = FindFirstObjectByType<GameStateSyncManager>() ?? new GameObject("GameStateSyncManager").AddComponent<GameStateSyncManager>();
            stateSync.RefreshReferences();
            TimingSyncManager timeSync = FindFirstObjectByType<TimingSyncManager>() ?? new GameObject("TimingSyncManager").AddComponent<TimingSyncManager>();
            timeSync.RefreshReferences();
        }
    }
}