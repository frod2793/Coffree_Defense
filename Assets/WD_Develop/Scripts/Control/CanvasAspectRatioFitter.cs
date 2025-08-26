using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Canvas의 비율을 카메라의 레터박스에 맞춰 동적으로 조절합니다.
/// 실시간으로 화면 크기 변경에 대응합니다.
/// Canvas Scaler 컴포넌트가 필요합니다.
/// </summary>
[RequireComponent(typeof(CanvasScaler))]
public class CanvasAspectRatioFitter : MonoBehaviour
{
    private CanvasScaler canvasScaler;
    private int lastScreenWidth = 0;
    private int lastScreenHeight = 0;

    void Awake()
    {
        canvasScaler = GetComponent<CanvasScaler>();
        if (canvasScaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize || 
            canvasScaler.screenMatchMode != CanvasScaler.ScreenMatchMode.MatchWidthOrHeight)
        {
            Debug.LogWarning($"[CanvasAspectRatioFitter] '{gameObject.name}'의 Canvas Scaler 설정이 올바르지 않습니다. Ui Scale Mode는 'Scale With Screen Size'로, Screen Match Mode는 'Match Width Or Height'로 설정해주세요.");
        }
    }

    void Update()
    {
        // 화면 해상도가 변경되었는지 확인하여 불필요한 계산을 방지합니다.
        if (Screen.width != lastScreenWidth || Screen.height != lastScreenHeight)
        {
            SetCanvasMatchMode();
            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;
        }
    }

    /// <summary>
    /// 화면 비율에 맞춰 Canvas Scaler의 Match 모드를 조절합니다.
    /// </summary>
    private void SetCanvasMatchMode()
    {
        // 목표 비율 (16:9)
        float targetAspect = 16.0f / 9.0f;

        // 현재 화면 비율
        float windowAspect = (float)Screen.width / (float)Screen.height;

        // 목표 비율보다 화면이 넓으면 (Pillarbox), 높이를 기준으로 UI를 맞춥니다. (Match = 1)
        if (windowAspect > targetAspect)
        {
            canvasScaler.matchWidthOrHeight = 1;
        }
        // 목표 비율보다 화면이 좁으면 (Letterbox), 너비를 기준으로 UI를 맞춥니다. (Match = 0)
        else
        { 
            canvasScaler.matchWidthOrHeight = 0;
        }
    }
}
