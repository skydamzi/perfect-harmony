using System.Collections.Generic;
using UnityEngine;

public class FrameCounter : MonoBehaviour
{
    private float deltaTime = 0f;

    [SerializeField] private int size = 25;
    [SerializeField] private Color color = Color.red;

    // 프레임 저장용
    private List<float> frameTimes = new List<float>();
    private const int sampleCount = 200;   // 샘플 개수 (원하면 늘릴 수 있음)

    void Update()
    {
        // 현재 프레임 처리 시간 (지수평활)
        deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;

        // 프레임 저장
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

        float ms = deltaTime * 1000f;
        float fps = 1.0f / deltaTime;

        // === 10% Low + 1% Low 계산 ===
        float low10 = CalcPercentLowFPS(10);
        float low1 = CalcPercentLowFPS(1);

        string text =
            $"{fps:0.} FPS ({ms:0.0} ms)\n" +
            $"10% Low: {low10:0.} FPS\n" +
            $"1% Low:  {low1:0.} FPS";

        GUI.Label(rect, text, style);
    }

    /// <summary>
    /// percent% Low FPS 계산 (예: 10 → 10% Low, 1 → 1% Low)
    /// </summary>
    private float CalcPercentLowFPS(float percent)
    {
        if (frameTimes.Count == 0) return 0f;

        // 작은 순서대로 정렬 (프레임 타임이 크면 FPS 낮음)
        List<float> sorted = new List<float>(frameTimes);
        sorted.Sort();  // 오름차순

        int count = Mathf.FloorToInt(sorted.Count * (percent / 100f));
        count = Mathf.Clamp(count, 1, sorted.Count);

        float sum = 0f;
        for (int i = 0; i < count; i++)
            sum += sorted[i];

        float avgFrameTime = sum / count;
        float fps = 1.0f / avgFrameTime;

        return fps;
    }
}
