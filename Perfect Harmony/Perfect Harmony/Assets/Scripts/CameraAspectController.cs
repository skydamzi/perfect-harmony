using UnityEngine;

public class CameraAspectController : MonoBehaviour
{
    public float targetAspect = 16.0f / 9.0f; // 16:9

    void Start()
    {
        UpdateCameraRect();
    }

    void Update()
    {
       
        UpdateCameraRect();
    }

    void UpdateCameraRect()
    {
        // 현재 화면비율
        float windowAspect = (float)Screen.width / (float)Screen.height;

        // 비율 차이 계산
        float scaleHeight = windowAspect / targetAspect;

        Camera camera = GetComponent<Camera>();

        // 위아래에 검은부분(레터박스) 생기는 비율
        if (scaleHeight < 1.0f)
        {
            Rect rect = camera.rect;

            rect.width = 1.0f;
            rect.height = scaleHeight;
            rect.x = 0;
            rect.y = (1.0f - scaleHeight) / 2.0f;

            camera.rect = rect;
        }
        else // 좌우에 검은부분(레터박스) 생기는 비율
        {
            float scaleWidth = 1.0f / scaleHeight;

            Rect rect = camera.rect;

            rect.width = scaleWidth;
            rect.height = 1.0f;
            rect.x = (1.0f - scaleWidth) / 2.0f;
            rect.y = 0;

            camera.rect = rect;
        }
    }
}