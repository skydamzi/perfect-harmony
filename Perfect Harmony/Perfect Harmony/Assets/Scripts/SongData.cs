using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewSongData", menuName = "Rhythm Game/Song Data")]
public class SongData : ScriptableObject
{
    [Header("기본 정보")]
    public string songTitle;   // 노래 제목
    public string songInfo;  //노래 정보
    [Header("음악 설정")]
    public AudioClip audioClip; // 음악 파일
    public float beatsPerMinute;         // 곡의 BPM (예: 120)

    [Header("Noet speed")]
    public float noteSpeed = 2.0f; // 노트가 떨어지는 시간 (작을수록 빠름, 기본 2.0)
    [Header("채보 데이터")]
    public List<SpawnEvent> chartData;
}