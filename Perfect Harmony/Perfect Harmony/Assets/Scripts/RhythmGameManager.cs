using System.Collections.Generic;
using UnityEngine;


public class RhythmGameManager : MonoBehaviour
{
    public static RhythmGameManager Instance { get; private set; }

    [Header("Current Song")]
    public SongData selectedSong;
    public AudioSource audioSource;

    [Header("Game Settings (Sync)")]
    public bool isPlaying = false;
    public bool isCountingDown = false;
    public float songPosition;         // 현재 곡의 시간 (기준점 대비)
    public float songStartTime;        // 카운트다운 시작 버튼 누른 시점 (Time.time)
    public float actualSongStartTime;  // [복구] 곡이 실제로 시작되는 시점 (songStartTime + startDelay)

    [Header("Multiplayer Sync")]
    private float targetServerStartTime = 0f;
    private bool isSyncStart = false;

    [Header("Game Settings")]
    public float beatsPerMinute = 120f;
    public float beatDuration;
    public int beatsPerMeasure = 4;
    public float spawnOffset = 2.0f;
    public float startDelay = 3.0f;

    [Header("Timing Windows")]
    public float perfectWindow = 0.1f;
    public float goodWindow = 0.2f;
    public float okayWindow = 0.3f;

    [Header("Game State")]
    public int currentBeat;     // [복구] 현재 몇 번째 비트인지
    public float beatProgress;  // [복구] 현재 비트 내 진행도 (0~1)
    public int currentMeasure;

    [Header("UI References")]
    public UnityEngine.UI.Text countdownText;

    private float beatTime;

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
    }

    private void Start()
    {
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (selectedSong != null) LoadSong(selectedSong);
        else beatDuration = 60f / beatsPerMinute;
    }

    private void Update()
    {
        if (!isCountingDown && !isPlaying) return;

        // [핵심] 절대 기준점 설정 (이게 틀어지면 리듬 다 깨짐)
        if (isSyncStart && TimingSyncManager.Instance != null)
        {
            // 네트워크 동기화 시간 기준: 서버의 시작 시점을 내 로컬 시간으로 변환
            // serverTime = localTime + offset  =>  localTime = serverTime - offset
            actualSongStartTime = targetServerStartTime - TimingSyncManager.Instance.GetTimeOffset();
            songStartTime = actualSongStartTime - startDelay;
        }
        else
        {
            // 싱글 플레이어 기준
            actualSongStartTime = songStartTime + startDelay;
        }

        songPosition = Time.time - actualSongStartTime;

        // 비트 및 진행도 계산 (멀티플레이어 동기화용 변수들 업데이트)
        currentBeat = Mathf.FloorToInt(songPosition / beatDuration);
        currentMeasure = Mathf.FloorToInt(currentBeat / beatsPerMeasure);
        beatProgress = (songPosition % beatDuration) / beatDuration;

        if (isCountingDown)
        {
            float remainingTime = actualSongStartTime - Time.time;
            if (countdownText != null)
            {
                if (remainingTime > 0)
                {
                    countdownText.text = Mathf.CeilToInt(remainingTime).ToString();
                }
                else
                {
                    countdownText.text = "시작!";
                }
            }

            if (remainingTime <= 0) StartSong();
        }

        if (isPlaying)
        {
            if (audioSource.clip != null && songPosition > audioSource.clip.length + 1.0f)
            {
                FinishGame();
            }
        }
    }

    public void LoadSong(SongData song)
    {
        selectedSong = song;
        beatsPerMinute = song.beatsPerMinute;
        spawnOffset = song.noteSpeed;
        beatDuration = 60f / beatsPerMinute;
        if (song.audioClip != null) audioSource.clip = song.audioClip;

        NoteSpawner noteSpawner = FindFirstObjectByType<NoteSpawner>();
        if (noteSpawner != null)
        {
            noteSpawner.ClearSpawnEvents();
            if (song.chartData != null)
                noteSpawner.spawnEvents = new List<SpawnEvent>(song.chartData);
        }
    }

    public void StartCountdown()
    {
        isSyncStart = false;
        songStartTime = Time.time;
        isCountingDown = true;
        isPlaying = false;

        NoteSpawner noteSpawner = FindFirstObjectByType<NoteSpawner>();
        if (noteSpawner != null) noteSpawner.StartSpawning();
    }

    public void StartCountdownSync(float targetServerStart)
    {
        isSyncStart = true;
        targetServerStartTime = targetServerStart;
        isCountingDown = true;
        isPlaying = false;

        NoteSpawner noteSpawner = FindFirstObjectByType<NoteSpawner>();
        if (noteSpawner != null) noteSpawner.StartSpawning();
        
        Debug.Log($"[Sync] Scheduled game start at Server Time: {targetServerStart}");
    }

    public void StartSong()
    {
        if (isPlaying) return;
        isCountingDown = false;
        isPlaying = true;
        if (audioSource != null && audioSource.clip != null) audioSource.Play();
        if (countdownText != null) countdownText.text = "";
    }

    // [복구] InputHandler랑 Multiplayer에서 쓰고 있는 판정 함수
    public TimingResult CheckTiming(float hitTime, float targetTime)
    {
        float timeDifference = Mathf.Abs(hitTime - targetTime);
        if (timeDifference <= perfectWindow) return TimingResult.Perfect;
        else if (timeDifference <= goodWindow) return TimingResult.Good;
        else if (timeDifference <= okayWindow) return TimingResult.Okay;
        else return TimingResult.Miss;
    }

    public void FinishGame()
    {
        isPlaying = false;
        if (audioSource != null) audioSource.Stop();
        FrameCounter fc = FindFirstObjectByType<FrameCounter>();
        if (fc != null) fc.ShowSessionResult();
    }

    public float BeatToTime(float beatNumber) => beatNumber * beatDuration;
}