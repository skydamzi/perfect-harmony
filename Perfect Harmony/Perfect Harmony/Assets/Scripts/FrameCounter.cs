using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class FrameCounter : MonoBehaviour
{
    private float deltaTime = 0f;

    [Header("Setting")]
    public bool showFPS = true; // 프레임카운터 on/off

    [Header("UI Reference")]
    public Text uiText;

    // 프레임 저장용
    private List<float> frameTimes = new List<float>();
    private const int sampleCount = 200;   // 저장할 샘플 수

    void Start()
    {
        if (uiText != null)
        {
            // 텍스트 정렬을 하단 중앙으로 변경
            uiText.alignment = TextAnchor.LowerCenter;

            // UI 위치를 화면 하단 중앙으로 이동
            if (uiText.rectTransform != null)
            {
                uiText.rectTransform.anchorMin = new Vector2(0.5f, 0f);
                uiText.rectTransform.anchorMax = new Vector2(0.5f, 0f);
                uiText.rectTransform.pivot = new Vector2(0.5f, 0f);
                uiText.rectTransform.anchoredPosition = new Vector2(0, 10f); // 하단에서 약간 위로
            }
        }
    }

    void Update()
    {
        // 부드럽게 평균낸 deltaTime
        deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;

        // 샘플 저장
        frameTimes.Add(Time.unscaledDeltaTime);
        if (frameTimes.Count > sampleCount)
            frameTimes.RemoveAt(0);

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
        if (uiText != null)
        {
            uiText.enabled = showFPS;
            if (showFPS)
                uiText.text = text;
        }
    }


    /// <summary>
    /// percent% Low FPS 계산
    /// </summary>
    private float CalcPercentLowFPS(float percent)
    {
        if (frameTimes.Count == 0) return 0f;

        List<float> sorted = new List<float>(frameTimes);
        sorted.Sort(); // 오름차순: 값이 작을수록(빠를수록) 앞, 클수록(느릴수록) 뒤

        int count = Mathf.FloorToInt(sorted.Count * (percent / 100f));
        count = Mathf.Clamp(count, 1, sorted.Count);

        float sum = 0f;
        // 수정: Low FPS는 '프레임 시간이 긴(느린)' 프레임들의 평균이므로
        // 오름차순 정렬된 리스트의 '뒤쪽(큰 값)'을 가져와야 함.
        for (int i = 0; i < count; i++)
        {
            sum += sorted[sorted.Count - 1 - i];
        }

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
