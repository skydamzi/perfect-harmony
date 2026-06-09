using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using System;
using System.Collections;
using System.Collections.Generic;

[Serializable]
public class InstrumentTracks
{
    public List<SpawnEvent> drums;
    public List<SpawnEvent> bass;
    public List<SpawnEvent> piano;
}

[Serializable]
public class ServerResponse
{
    public float beatsPerMinute;
    public InstrumentTracks tracks;
}

public class ChartRequester : MonoBehaviour
{
    public enum SelectedInstrument { Drums, Bass, Piano }

    [Header("[ AI Analysis Settings ]")]
    public string url = "http://116.127.190.78:8000/generate-chart";
    public AudioClip targetAudioClip;

    [Header("UI References")]
    public GameObject loadingPanel;
    public RectTransform panelContent;
    public Text statusText;
    public RectTransform loadingSpinner;
    public GameObject instrumentSelectPanel; // Kept for backward compatibility but bypassed

    private bool isAnalyzing = false;
    private ServerResponse fullServerData;

    void Start()
    {
        if (panelContent != null) panelContent.localScale = Vector3.zero;
        if (loadingPanel != null) loadingPanel.SetActive(false);
        if (instrumentSelectPanel != null) instrumentSelectPanel.SetActive(false);

        string path = System.IO.Path.Combine(Application.streamingAssetsPath, "song.mp3");
        if (System.IO.File.Exists(path))
        {
            StartCoroutine(AnalyzeAndPlay(path, targetAudioClip));
        }
        else
        {
            Debug.LogError($"Audio file not found at: {path}");
        }
    }

    IEnumerator AnalyzeAndPlay(string path, AudioClip clip)
    {
        if (loadingPanel != null) loadingPanel.SetActive(true);
        if (statusText != null) statusText.text = "Initializing analysis...";
        yield return StartCoroutine(ScaleRoutine(Vector3.zero, Vector3.one, 0.4f));

        isAnalyzing = true;
        StartCoroutine(RotateSpinner());

        byte[] audioData = System.IO.File.ReadAllBytes(path);
        WWWForm form = new WWWForm();
        form.AddBinaryData("file", audioData, "song.wav");

        using (UnityWebRequest www = UnityWebRequest.Post(url, form))
        {
            if (statusText != null) statusText.text = "AI analyzing audio...\n(May take up to 1 minute)";
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                if (statusText != null) statusText.text = "Analysis Complete!";
                isAnalyzing = false;

                fullServerData = JsonUtility.FromJson<ServerResponse>(www.downloadHandler.text);

                // MultiplayerManager에서 선택된 악기 정보 가져오기 (없으면 Piano 기본)
                string selected = "Piano";
                if (MultiplayerManager.Instance != null)
                {
                    selected = MultiplayerManager.Instance.selectedInstrument;
                }

                Debug.Log($"[Auto-Select] Lobby choice: {selected}. Starting game...");
                OnSelectInstrument(selected);
            }
            else
            {
                if (statusText != null) statusText.text = "Analysis Failed";
                isAnalyzing = false;
                Debug.LogError("Server Error: " + www.error);
                yield return new WaitForSeconds(1f);
                if (loadingPanel != null) loadingPanel.SetActive(false);
            }
        }
    }

    public void OnSelectInstrument(string instrumentType)
    {
        if (fullServerData == null) return;

        List<SpawnEvent> selectedChartData = null;

        switch (instrumentType)
        {
            case "Drums":
                selectedChartData = fullServerData.tracks.drums;
                break;
            case "Bass":
                selectedChartData = fullServerData.tracks.bass;
                break;
            case "Piano":
                selectedChartData = fullServerData.tracks.piano;
                break;
        }

        SongData aiSong = ScriptableObject.CreateInstance<SongData>();
        aiSong.songTitle = $"AI Generated Chart ({instrumentType})";
        aiSong.beatsPerMinute = fullServerData.beatsPerMinute;
        aiSong.audioClip = targetAudioClip;
        aiSong.chartData = selectedChartData;
        aiSong.noteSpeed = 2.0f;

        RhythmGameManager.Instance.LoadSong(aiSong);
        StartCoroutine(FinishAndStartGame());
    }

    private IEnumerator FinishAndStartGame()
    {
        if (instrumentSelectPanel != null) instrumentSelectPanel.SetActive(false);
        yield return new WaitForSeconds(0.2f);

        yield return StartCoroutine(ScaleRoutine(Vector3.one, Vector3.zero, 0.3f));
        if (loadingPanel != null) loadingPanel.SetActive(false);

        GameStarter starter = FindFirstObjectByType<GameStarter>();
        if (starter != null) starter.StartGameAfterAnalysis();
        else RhythmGameManager.Instance.StartCountdown();
    }

    IEnumerator RotateSpinner()
    {
        if (loadingSpinner == null) yield break;
        loadingSpinner.gameObject.SetActive(true);

        while (isAnalyzing)
        {
            loadingSpinner.Rotate(0, 0, -200f * Time.deltaTime);
            yield return null;
        }
    }

    IEnumerator ScaleRoutine(Vector3 start, Vector3 end, float duration)
    {
        if (panelContent == null) yield break;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float easedT = t * t * (3f - 2f * t);
            panelContent.localScale = Vector3.Lerp(start, end, easedT);
            yield return null;
        }
        panelContent.localScale = end;
    }
}