using System.Collections.Generic;
using UnityEngine;

public class FrameCounter : MonoBehaviour
{
    private float deltaTime = 0f;

    [SerializeField] private int size = 25;
    [SerializeField] private Color color = Color.red;

    // 프레임 저장용
    private List<float> frameTimes = new List<float>();
    private const int sampleCount = 200;   // 저장할 샘플 수

    void Update()
    {
        // 부드럽게 평균낸 deltaTime
        deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;

        // 샘플 저장
        frameTimes.Add(Time.unscaledDeltaTime);
        if (frameTimes.Count > sampleCount)
            frameTimes.RemoveAt(0);
    }

    private void OnGUI()
    {
        GUIStyle style = new GUIStyle();

        Rect rect = new Rect(30, 30, Screen.width, Screen.height);
        style.alignment = TextAnchor.UpperLeft;
        style.fontSize = size;
        style.normal.textColor = color;

        // 현재 FPS
        float ms = deltaTime * 1000f;
        float fps = 1f / deltaTime;

        // ▼ 10% / 1% Low FPS
        float low10 = CalcPercentLowFPS(10);
        float low1 = CalcPercentLowFPS(1);

        // ▼ Lowest FPS 계산
        float lowest = CalcLowestFPS();

        string text =
            $"{fps:0.} FPS ({ms:0.0} ms)\n" +
            $"10% Low: {low10:0.} FPS\n" +
            $"1% Low:  {low1:0.} FPS\n" +
            $"Lowest:  {lowest:0.} FPS";

        GUI.Label(rect, text, style);
    }

    /// <summary>
    /// percent% Low FPS 계산
    /// </summary>
    private float CalcPercentLowFPS(float percent)
    {
        if (frameTimes.Count == 0) return 0f;

        List<float> sorted = new List<float>(frameTimes);
        sorted.Sort(); // 오름차순: 큰 값일수록 FPS 낮음

        int count = Mathf.FloorToInt(sorted.Count * (percent / 100f));
        count = Mathf.Clamp(count, 1, sorted.Count);

        float sum = 0f;
        for (int i = 0; i < count; i++)
            sum += sorted[i];

        float avgFrameTime = sum / count;
        return 1f / avgFrameTime;
    }

    /// <summary>
    /// 최저 FPS = 가장 오래 걸린 프레임
    /// </summary>
    private float CalcLowestFPS()
    {
        if (frameTimes.Count == 0) return 0f;

        float maxFrameTime = 0f;
        for (int i = 0; i < frameTimes.Count; i++)
        {
            if (frameTimes[i] > maxFrameTime)
                maxFrameTime = frameTimes[i];
        }

        return 1f / maxFrameTime;
    }
}
