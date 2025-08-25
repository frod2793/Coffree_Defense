using UnityEngine;

/// <summary>
/// WebGL 및 다양한 해상도에서 카메라의 16:9 비율을 유지하고 레터박스를 적용합니다.
/// 실시간으로 화면 크기 변경에 대응합니다.
/// </summary>
[RequireComponent(typeof(Camera))]
public class cameraResolation : MonoBehaviour
{
    private Camera cam;
    private int lastScreenWidth = 0;
    private int lastScreenHeight = 0;

    void Awake()
    {
        cam = GetComponent<Camera>();
        // 레터박스 영역을 검은색으로 채우도록 설정합니다.
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
    }

    void Update()
    {
        // 화면 해상도가 변경되었는지 확인하여 불필요한 계산을 방지합니다.
        if (Screen.width != lastScreenWidth || Screen.height != lastScreenHeight)
        {
            SetCameraAspectRatio();
            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;
        }
    }

    /// <summary>
    /// 화면 비율에 맞춰 카메라의 Viewport Rect를 조절합니다.
    /// </summary>
    private void SetCameraAspectRatio()
    {
        // 목표 비율 (16:9)
        float targetAspect = 16.0f / 9.0f;

        // 현재 화면 비율
        float windowAspect = (float)Screen.width / (float)Screen.height;

        // 목표 비율보다 화면이 넓은 경우 (좌우에 레터박스 추가)
        if (windowAspect > targetAspect)
        {
            float scaleWidth = targetAspect / windowAspect;
            cam.rect = new Rect((1.0f - scaleWidth) / 2.0f, 0.0f, scaleWidth, 1.0f);
        }
        // 목표 비율보다 화면이 좁은 경우 (상하에 레터박스 추가)
        else
        {
            float scaleHeight = windowAspect / targetAspect;
            cam.rect = new Rect(0.0f, (1.0f - scaleHeight) / 2.0f, 1.0f, scaleHeight);
        }
    }
}
