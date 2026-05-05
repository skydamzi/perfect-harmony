using UnityEngine;
using UnityEngine.Networking;
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
    public AudioClip targetAudioClip; // 재생할 오디오 클립 (인스펙터에서 필수 등록!)

    void Start()
    {
        // StreamingAssets/song.wav 파일 분석 시작
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
        byte[] audioData = System.IO.File.ReadAllBytes(path);
        WWWForm form = new WWWForm();
        form.AddBinaryData("file", audioData, "song.wav");

        using (UnityWebRequest www = UnityWebRequest.Post(url, form))
        {
            Debug.Log("<color=yellow>서버 분석 중...</color>");
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                ServerResponse res = JsonUtility.FromJson<ServerResponse>(www.downloadHandler.text);

                // 1. 임시 SongData 에셋 생성
                SongData aiSong = ScriptableObject.CreateInstance<SongData>();
                aiSong.songTitle = "AI Generated Chart";
                aiSong.beatsPerMinute = res.beatsPerMinute;
                aiSong.audioClip = clip;
                aiSong.chartData = res.chartData;
                aiSong.noteSpeed = 2.0f;

                // 2. RhythmGameManager에 주입
                RhythmGameManager.Instance.LoadSong(aiSong);

                // 3. GameStarter에게 시작 신호 보내기
                GameStarter starter = FindFirstObjectByType<GameStarter>();
                if (starter != null) starter.StartGameAfterAnalysis();
                else RhythmGameManager.Instance.StartCountdown();
            }
            else
            {
                Debug.LogError("서버 연결 실패: " + www.error);
            }
        }
    }
}