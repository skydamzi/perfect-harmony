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

    // 전체 세션 기록용
    private List<float> allFrameTimes = new List<float>();
    private bool showResult = false;

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
        // 결과창 모드면 업데이트 중단
        if (showResult) return;

        // 부드럽게 평균낸 deltaTime
        deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;

        // 샘플 저장 (실시간용)
        frameTimes.Add(Time.unscaledDeltaTime);
        if (frameTimes.Count > sampleCount)
            frameTimes.RemoveAt(0);

        // 전체 기록 저장 (초반 6초 안정화 시간 제외)
        if (Time.timeSinceLevelLoad > 6.0f)
        {
            allFrameTimes.Add(Time.unscaledDeltaTime);
        }

        // 현재 FPS
        float ms = deltaTime * 1000f;
        float fps = 1f / deltaTime;

        // ▼ 10% / 1% Low FPS 
        float low10 = CalcPercentLowFPS(frameTimes, 10);
        float low1 = CalcPercentLowFPS(frameTimes, 1);

        // ▼ Lowest FPS 계산
        float lowest = CalcLowestFPS(frameTimes);

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
    /// 게임 종료 시 호출: 전체 세션 통계 표시
    /// </summary>
    public void ShowSessionResult()
    {
        showResult = true;

        if (allFrameTimes.Count == 0) return;

        float avgFrameTime = 0f;
        foreach (float t in allFrameTimes) avgFrameTime += t;
        avgFrameTime /= allFrameTimes.Count;
        float avgFPS = 1f / avgFrameTime;

        float low1 = CalcPercentLowFPS(allFrameTimes, 1);
        float low01 = CalcPercentLowFPS(allFrameTimes, 0.1f); // 0.1% Low
        float lowest = CalcLowestFPS(allFrameTimes);

        string resultText = 
            $"=== Performance Result ===\n" +
            $"Avg FPS: {avgFPS:0.0}\n" +
            $"1% Low:  {low1:0.0}\n" +
            $"0.1% Low: {low01:0.0}\n" +
            $"Lowest:  {lowest:0.0}\n" +
            $"Total Frames: {allFrameTimes.Count}";

        if (uiText != null)
        {
            // 캔버스의 Sorting Order를 강제로 높여서 최상단에 표시
            Canvas parentCanvas = uiText.canvas;
            if (parentCanvas != null)
            {
                // 루트 캔버스가 아니라면(중첩 캔버스라면) 오버라이드 활성화
                if (!parentCanvas.isRootCanvas)
                {
                    parentCanvas.overrideSorting = true;
                }
                parentCanvas.sortingOrder = 32000; // 매우 높은 값으로 설정
            }

            // UI를 최상단으로 올림 (Z-order/Hierarchy 순서 변경)
            uiText.transform.SetAsLastSibling();

            uiText.enabled = true;
            uiText.text = resultText;
            
            // 중앙으로 이동 및 스타일 변경
            uiText.alignment = TextAnchor.MiddleCenter;
            if (uiText.rectTransform != null)
            {
                uiText.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                uiText.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                uiText.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                uiText.rectTransform.anchoredPosition = Vector2.zero;
            }
            uiText.fontSize += 4; // 폰트 조금 키움
            uiText.color = Color.black; // 글자색 검은색으로 변경

            // 가독성을 위한 Outline(외곽선) 추가
            Outline outline = uiText.GetComponent<Outline>();
            if (outline == null) outline = uiText.gameObject.AddComponent<Outline>();
            outline.effectColor = Color.white;
            outline.effectDistance = new Vector2(2, -2);

            // 다른 모든 캔버스 비활성화 (이 캔버스 제외)
            Canvas[] allCanvases = FindObjectsOfType<Canvas>();
            foreach (Canvas c in allCanvases)
            {
                // 자기 자신의 캔버스는 끄지 않음
                if (c == parentCanvas) continue;
                
                // 캔버스 게임오브젝트를 비활성화하여 숨김
                c.gameObject.SetActive(false);
            }
        }
    }


    /// <summary>
    /// percent% Low FPS 계산
    /// </summary>
    private float CalcPercentLowFPS(List<float> list, float percent)
    {
        if (list.Count == 0) return 0f;

        List<float> sorted = new List<float>(list);
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
    private float CalcLowestFPS(List<float> list)
    {
        if (list.Count == 0) return 0f;

        float maxFrameTime = 0f;
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] > maxFrameTime)
                maxFrameTime = list[i];
        }

        return 1f / maxFrameTime;
    }
}
