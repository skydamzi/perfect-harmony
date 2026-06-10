using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RhythmGameManager : MonoBehaviour
{
    public static RhythmGameManager Instance { get; private set; }

    [Header("Audio")]
    public AudioSource audioSource;
    public SongData selectedSong;

    [Header("Sync Status")]
    public bool isPlaying = false;
    public bool isCountingDown = false;
    public double targetServerStartTime; // 서버 기준 곡 시작 절대 시각 (UTC)
    public float songPosition;           // 현재 곡의 진행 시간 (초)
    public double actualSongStartTime;   // [복구] 기존 스크립트 참조용

    [Header("Song Settings")]
    public float beatsPerMinute = 120f;
    public float beatDuration;
    public float spawnOffset = 2.0f; // [복구]
    public float startDelay = 3.5f; // 서버와 동일하게 맞춤

    [Header("Timing Windows")] // [복구]
    public float perfectWindow = 0.1f;
    public float goodWindow = 0.2f;
    public float okayWindow = 0.3f;

    [Header("Game State")] // [복구]
    public int currentBeat;     
    public float beatProgress;  
    public int currentMeasure;

    [Header("UI")]
    public Text countdownText;

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else { Instance = this; DontDestroyOnLoad(gameObject); }
    }

    private void Update()
    {
        if (!isCountingDown && !isPlaying) return;

        // 서버 시간을 가져와서 곡의 현재 위치 계산
        double currentServerTime = TimingSyncManager.Instance.GetCurrentServerTime();
        songPosition = (float)(currentServerTime - targetServerStartTime);
        
        // [복구] 비트 계산
        currentBeat = Mathf.FloorToInt(songPosition / beatDuration);
        currentMeasure = Mathf.FloorToInt(currentBeat / 4); // 대충 4박자 기준
        beatProgress = (songPosition % beatDuration) / beatDuration;

        if (isCountingDown)
        {
            float remaining = (float)(targetServerStartTime - currentServerTime);
            if (countdownText != null)
            {
                if (remaining > 0) countdownText.text = Mathf.CeilToInt(remaining).ToString();
                else countdownText.text = "GO!";
            }

            if (remaining <= 0) StartSong();
        }

        if (isPlaying && audioSource.clip != null)
        {
            if (songPosition > audioSource.clip.length + 1.0f) FinishGame();
        }
    }

    public void StartSyncCountdown(double serverStartTime)
    {
        targetServerStartTime = serverStartTime;
        // 이제 actualSongStartTime도 서버 시각(Unix) 기준으로 저장합니다.
        actualSongStartTime = serverStartTime;
        
        isCountingDown = true;
        isPlaying = false;
        
        // 노트 스포너 시작
        NoteSpawner spawner = FindFirstObjectByType<NoteSpawner>();
        if (spawner != null) spawner.StartSpawning();
        
        Debug.Log($"[Sync] Game Start Scheduled at Server Unix Time: {serverStartTime}");
    }

    public void StartCountdown() // 싱글플레이용
    {
        targetServerStartTime = TimingSyncManager.Instance.GetCurrentServerTime() + (double)startDelay;
        actualSongStartTime = targetServerStartTime;
        isCountingDown = true;
        isPlaying = false;
        
        NoteSpawner spawner = FindFirstObjectByType<NoteSpawner>();
        if (spawner != null) spawner.StartSpawning();
    }

    public void StartSong()
    {
        if (isPlaying) return;
        isCountingDown = false;
        isPlaying = true;

        if (audioSource != null && audioSource.clip != null)
        {
            audioSource.time = Mathf.Max(0, songPosition);
            audioSource.Play();
        }
        
        if (countdownText != null) countdownText.text = "";
    }

    // [복구] 판정 함수
    public TimingResult CheckTiming(float hitTime, float targetTime)
    {
        float diff = Mathf.Abs(hitTime - targetTime);
        if (diff <= perfectWindow) return TimingResult.Perfect;
        if (diff <= goodWindow) return TimingResult.Good;
        if (diff <= okayWindow) return TimingResult.Okay;
        return TimingResult.Miss;
    }

    public void FinishGame()
    {
        isPlaying = false;
        if (audioSource != null) audioSource.Stop();
        // 결과창 표시 로직 등...
    }

    public void LoadSong(SongData song)
    {
        selectedSong = song;
        beatsPerMinute = song.beatsPerMinute;
        beatDuration = 60f / beatsPerMinute;
        if (audioSource != null) audioSource.clip = song.audioClip;

        // [핵심 추가] 채보 데이터를 NoteSpawner에게 전달합니다.
        NoteSpawner spawner = FindFirstObjectByType<NoteSpawner>();
        if (spawner != null && song.chartData != null)
        {
            spawner.p1SpawnEvents = new List<SpawnEvent>(song.chartData);
            spawner.p2SpawnEvents = new List<SpawnEvent>(song.chartData); // Default both to local chart for single play
            Debug.Log($"[LoadSong] {song.chartData.Count} notes injected into NoteSpawner.");
        }
    }

    public float BeatToTime(float beatNumber) => beatNumber * beatDuration;
}
