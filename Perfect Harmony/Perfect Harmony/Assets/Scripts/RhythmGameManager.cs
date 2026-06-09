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

    [Header("Song Settings")]
    public float beatsPerMinute = 120f;
    public float beatDuration;
    public float startDelay = 3.5f; // 서버와 동일하게 맞춤

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
        isCountingDown = true;
        isPlaying = false;
        
        // 노트 스포너 시작
        NoteSpawner spawner = FindFirstObjectByType<NoteSpawner>();
        if (spawner != null) spawner.StartSpawning();
        
        Debug.Log($"[Sync] Game Start Scheduled at Server Time: {serverStartTime}");
    }

    private void StartSong()
    {
        if (isPlaying) return;
        isCountingDown = false;
        isPlaying = true;

        if (audioSource != null && audioSource.clip != null)
        {
            // [정밀 보정] 패킷 지연으로 인해 시작이 늦었을 수 있으므로 
            // 현재 곡 위치(songPosition)에 맞춰서 오디오 재생 시작
            audioSource.time = Mathf.Max(0, songPosition);
            audioSource.Play();
        }
        
        if (countdownText != null) countdownText.text = "";
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
    }

    public float BeatToTime(float beatNumber) => beatNumber * beatDuration;
}
