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

        // [핵심 추가] 실제 재생을 위해 로컬 오디오 파일을 AudioClip으로 로드합니다.
        string fileUrl = "file://" + path;
        using (UnityWebRequest clipWww = UnityWebRequestMultimedia.GetAudioClip(fileUrl, AudioType.MPEG))
        {
            if (statusText != null) statusText.text = "Loading audio data...";
            yield return clipWww.SendWebRequest();
            if (clipWww.result == UnityWebRequest.Result.Success)
            {
                targetAudioClip = DownloadHandlerAudioClip.GetContent(clipWww);
                Debug.Log("[ChartRequester] AudioClip loaded successfully.");
            }
            else
            {
                Debug.LogWarning("[ChartRequester] Failed to load AudioClip: " + clipWww.error);
            }
        }

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

                // Load charts for all players
                LoadAllPlayerCharts();
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

    private void LoadAllPlayerCharts()
    {
        if (fullServerData == null) return;

        string hostInstrument = "Piano";
        string guestInstrument = "Piano";

        if (MultiplayerManager.Instance != null)
        {
            // 슬롯 기준으로 악기 찾기 (ID 정렬 시 첫 번째가 호스트)
            List<string> ids = new List<string>(MultiplayerManager.Instance.connectedPlayers.Keys);
            ids.Sort();

            if (ids.Count > 0) 
                hostInstrument = MultiplayerManager.Instance.connectedPlayers[ids[0]].selectedInstrument;
            if (ids.Count > 1) 
                guestInstrument = MultiplayerManager.Instance.connectedPlayers[ids[1]].selectedInstrument;
        }

        Debug.Log($"[ChartRequester] Slot-based Assignment - Host: {hostInstrument}, Guest: {guestInstrument}");

        List<SpawnEvent> hostChart = GetChartForInstrument(hostInstrument);
        List<SpawnEvent> guestChart = GetChartForInstrument(guestInstrument);

        // 로컬 플레이어의 악기 정보를 SongData에 담아둠 (호환성 유지)
        string localInstrument = MultiplayerManager.Instance != null ? MultiplayerManager.Instance.selectedInstrument : "Piano";
        List<SpawnEvent> localChart = GetChartForInstrument(localInstrument);

        SongData aiSong = ScriptableObject.CreateInstance<SongData>();
        aiSong.songTitle = $"AI Generated Chart (H:{hostInstrument} G:{guestInstrument})";
        aiSong.beatsPerMinute = fullServerData.beatsPerMinute;
        aiSong.audioClip = targetAudioClip;
        aiSong.chartData = localChart; 
        aiSong.noteSpeed = 2.0f;

        RhythmGameManager.Instance.LoadSong(aiSong);

        // [핵심 수정] 이제 p1은 항상 호스트(0-3번 라인), p2는 항상 게스트(4-7번 라인)로 고정합니다.
        NoteSpawner spawner = FindFirstObjectByType<NoteSpawner>();
        if (spawner != null)
        {
            spawner.p1SpawnEvents = new List<SpawnEvent>(hostChart);
            spawner.p2SpawnEvents = new List<SpawnEvent>(guestChart);
            Debug.Log($"[ChartRequester] Injected charts: P1(Host:{hostChart.Count}), P2(Guest:{guestChart.Count})");
        }

        StartCoroutine(FinishAndStartGame());
    }

    private List<SpawnEvent> GetChartForInstrument(string instrumentType)
    {
        if (fullServerData == null) return new List<SpawnEvent>();

        switch (instrumentType)
        {
            case "Drums": return fullServerData.tracks.drums;
            case "Bass": return fullServerData.tracks.bass;
            case "Piano": return fullServerData.tracks.piano;
            default: return fullServerData.tracks.piano;
        }
    }

    // Deprecated but kept for compatibility
    public void OnSelectInstrument(string instrumentType)
    {
        LoadAllPlayerCharts();
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