using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ICanvasRaycastFilter : 유니티 UI Sys에서 UI elements의 Raycast를 커스텀하게 필터링하기 위해서 사용하는 인터페이스.
/// Spotlight 방식 튜토리얼을 구현하기 위해선 커스텀 Raycasting이 필수적이라 채용함.
/// </summary>
[RequireComponent(typeof(Image))]
public sealed class SpotlightInputBlocker : MonoBehaviour, ICanvasRaycastFilter
{
    private RectTransform allowedTarget;

    public void SetAllowedTarget(RectTransform target)
    {
        allowedTarget = target;
    }

    public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
    {
        // 허용 대상이 없으면 전체 화면 입력을 차단
        if (allowedTarget == null || !allowedTarget.gameObject.activeInHierarchy)
        {
            return true;
        }

        Canvas targetCanvas = allowedTarget.GetComponentInParent<Canvas>().rootCanvas;

        Camera targetCamera = targetCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : targetCanvas.worldCamera;

        bool insideTarget = RectTransformUtility.RectangleContainsScreenPoint(allowedTarget, screenPoint, targetCamera);

        // true: 이 Image가 클릭을 차단
        // false: 아래에 있는 버튼으로 클릭을 통과
        return !insideTarget;
    }
}
