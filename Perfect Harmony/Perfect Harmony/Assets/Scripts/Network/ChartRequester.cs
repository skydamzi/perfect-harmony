using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

[System.Serializable]
public class ServerResponse
{
    public float beatsPerMinute;
    public List<SpawnEvent> chartData;
}

public class ChartRequester : MonoBehaviour
{
    public string url = "http://127.0.0.1:8000/generate-chart";
    public AudioClip targetAudioClip;

    [Header("UI 연출 관련")]
    public GameObject loadingPanel;
    public RectTransform panelContent;
    public Text statusText;
    public RectTransform loadingSpinner; // 빙글빙글 돌릴 이미지 (RectTransform)

    private bool isAnalyzing = false; // 회전 제어용 플래그

    void Start()
    {
        if (panelContent != null) panelContent.localScale = Vector3.zero;
        if (loadingPanel != null) loadingPanel.SetActive(false);

        string path = System.IO.Path.Combine(Application.streamingAssetsPath, "song.wav");
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
        // --- 1. 패널 등장 ---
        if (loadingPanel != null) loadingPanel.SetActive(true);
        if (statusText != null) statusText.text = "서버 연결 중...";
        yield return StartCoroutine(ScaleRoutine(Vector3.zero, Vector3.one, 0.4f));

        // --- 2. 회전 시작 ---
        isAnalyzing = true;
        StartCoroutine(RotateSpinner()); // 별도 코루틴으로 회전 시작

        byte[] audioData = System.IO.File.ReadAllBytes(path);
        WWWForm form = new WWWForm();
        form.AddBinaryData("file", audioData, "song.wav");

        using (UnityWebRequest www = UnityWebRequest.Post(url, form))
        {
            if (statusText != null) statusText.text = "AI 채보 분석 중...";
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                if (statusText != null) statusText.text = "분석 완료!";
                isAnalyzing = false; // 회전 멈춤

                ServerResponse res = JsonUtility.FromJson<ServerResponse>(www.downloadHandler.text);

                SongData aiSong = ScriptableObject.CreateInstance<SongData>();
                aiSong.songTitle = "AI Generated Chart";
                aiSong.beatsPerMinute = res.beatsPerMinute;
                aiSong.audioClip = clip;
                aiSong.chartData = res.chartData;
                aiSong.noteSpeed = 2.0f;

                RhythmGameManager.Instance.LoadSong(aiSong);

                yield return new WaitForSeconds(0.5f);

                // --- 3. 패널 퇴장 (축소) ---
                yield return StartCoroutine(ScaleRoutine(Vector3.one, Vector3.zero, 0.3f));
                if (loadingPanel != null) loadingPanel.SetActive(false);

                GameStarter starter = FindFirstObjectByType<GameStarter>();
                if (starter != null) starter.StartGameAfterAnalysis();
                else RhythmGameManager.Instance.StartCountdown();
            }
            else
            {
                if (statusText != null) statusText.text = "분석 실패ㅠ";
                isAnalyzing = false;
                Debug.LogError("서버 연결 실패: " + www.error);
                yield return new WaitForSeconds(1f);
                if (loadingPanel != null) loadingPanel.SetActive(false);
            }
        }
    }

    // 스피너 돌리는 코루틴
    IEnumerator RotateSpinner()
    {
        if (loadingSpinner == null) yield break;

        while (isAnalyzing)
        {
            // -360도로 돌려야 시계 방향으로 돈다. 속도 조절은 뒤에 곱하는 숫자(200f)로 해라.
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