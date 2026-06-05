using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using System;
using System.Collections;
using System.Collections.Generic;

// 서버 JSON 포맷 매핑 구조
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
    // 형이 인스펙터에서 딸깍 선택할 수 있는 열거형 정의
    public enum SelectedInstrument { Drums, Bass, Piano }

    [Header("[ 개발자 전용 에디터 설정 ]")]
    [Tooltip("인게임 버튼 안 누르고 인스펙터에서 고른 악기로 바로 시작하려면 체크!")]
    public bool useInspectorSelection = false;
    public SelectedInstrument testInstrument = SelectedInstrument.Drums; // 인스펙터 노출 변수

    [Header("[ 기존 서버 설정 ]")]
    public string url = "http://116.127.190.78:8000/generate-chart";
    public AudioClip targetAudioClip;

    [Header("UI 연출 관련")]
    public GameObject loadingPanel;
    public RectTransform panelContent;
    public Text statusText;
    public RectTransform loadingSpinner;

    [Header("[악기 선택 UI - 인게임용]")]
    public GameObject instrumentSelectPanel;
    public Button drumButton;
    public Button bassButton;
    public Button pianoButton;

    private bool isAnalyzing = false;
    private ServerResponse fullServerData;

    void Start()
    {
        if (panelContent != null) panelContent.localScale = Vector3.zero;
        if (loadingPanel != null) loadingPanel.SetActive(false);
        if (instrumentSelectPanel != null) instrumentSelectPanel.SetActive(false);

        // 인게임 버튼 리스너 연결
        if (drumButton != null) drumButton.onClick.AddListener(() => OnSelectInstrument("Drums"));
        if (bassButton != null) bassButton.onClick.AddListener(() => OnSelectInstrument("Bass"));
        if (pianoButton != null) pianoButton.onClick.AddListener(() => OnSelectInstrument("Piano"));

        string path = System.IO.Path.Combine(Application.streamingAssetsPath, "song.mp3");
        if (System.IO.File.Exists(path))
        {
            StartCoroutine(AnalyzeAndPlay(path, targetAudioClip));
        }
        else
        {
            Debug.LogError($"파일이 없어! 경로 확인: {path}");
        }
    }

    IEnumerator AnalyzeAndPlay(string path, AudioClip clip)
    {
        if (loadingPanel != null) loadingPanel.SetActive(true);
        if (statusText != null) statusText.text = "서버 연결 중...";
        yield return StartCoroutine(ScaleRoutine(Vector3.zero, Vector3.one, 0.4f));

        isAnalyzing = true;
        StartCoroutine(RotateSpinner());

        byte[] audioData = System.IO.File.ReadAllBytes(path);
        WWWForm form = new WWWForm();
        form.AddBinaryData("file", audioData, "song.wav");

        using (UnityWebRequest www = UnityWebRequest.Post(url, form))
        {
            if (statusText != null) statusText.text = "AI 채보 분석 중...\n(CPU 구동으로 약 1분 소요)";
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                if (statusText != null) statusText.text = "분석 완료!";
                isAnalyzing = false;

                fullServerData = JsonUtility.FromJson<ServerResponse>(www.downloadHandler.text);

                // ★ 변경 포인트: 인스펙터 우선 모드가 켜져 있으면 UI 안 띄우고 바로 다이렉트 패스!
                if (useInspectorSelection)
                {
                    Debug.Log($"[에디터 테스트] 인스펙터에 설정된 {testInstrument} 트랙으로 즉시 시작합니다.");
                    OnSelectInstrument(testInstrument.ToString());
                }
                else
                {
                    // 꺼져있으면 평소대로 유저한테 인게임 버튼 팝업 띄움
                    if (loadingSpinner != null) loadingSpinner.gameObject.SetActive(false);
                    if (instrumentSelectPanel != null) instrumentSelectPanel.SetActive(true);
                }
            }
            else
            {
                if (statusText != null) statusText.text = "분석 실패ㅠ";
                isAnalyzing = false;
                Debug.LogError("서ver 연결 실패: " + www.error);
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