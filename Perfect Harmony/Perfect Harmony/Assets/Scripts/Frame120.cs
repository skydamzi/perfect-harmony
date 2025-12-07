using UnityEngine;
using System.Collections;

public class Frame120 : MonoBehaviour
    {
    private void Start()
    {
        // VSync를 끄고 프레임을 60으로 고정
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 60;
    }
}
