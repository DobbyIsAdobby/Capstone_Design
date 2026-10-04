using TMPro;
using UnityEngine;

public class LifeEventTickerUI : MonoBehaviour
{
    [Header("Connect")]
    [SerializeField] private RectTransform viewport;
    [SerializeField] private TMP_Text eventText;

    [Header("Movement")]
    [Tooltip("초당 이동하는 UI 좌표 거리입니다.")]
    [SerializeField, Min(1f)] private float scrollSpeed = 60f;

    [Tooltip("문구 처음을 보여주는 대기 시간입니다.")]
    [SerializeField, Min(0f)] private float startPause = 1.5f;

    [Tooltip("문구 끝을 보여주는 대기 시간입니다.")]
    [SerializeField, Min(0f)] private float endPause = 1.5f;

    private string currentMessage = "";

    private bool needsRebuild = true;
    private bool shouldScroll;
    private bool reachedEnd;

    private float cachedViewportWidth = -1f;
    private float currentX;
    private float endX;
    private float pauseRemaining;

    /// <summary>
    /// 이벤트 패널을 표시 
    /// 같은 문구를 다시 받아도 이동을 처음부터 시작하지 않음.
    /// </summary>
    /// <param name="message"></param>
    public void Show(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            Hide();
            return;
        }

        // 한 줄 안내문으로 표시
        message = message.Replace("\r", " ").Replace("\n", " ");

        bool wasHidden = !gameObject.activeSelf;
        bool changed = currentMessage != message;

        currentMessage = message;

        if (wasHidden)
            gameObject.SetActive(true);

        // 이전 코드에서 텍스트만 비활성화했을 가능성도 처리
        if (!eventText.gameObject.activeSelf)
        {
            eventText.gameObject.SetActive(true);
            needsRebuild = true;
        }

        if (!changed && !wasHidden)
            return;

        eventText.text = currentMessage;
        needsRebuild = true;
    }

    public void Hide()
    {
        currentMessage = "";
        needsRebuild = true;

        // 배경과 WarningCircle까지 함께 숨김
        if (gameObject.activeSelf)
            gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        // 부모가 다시 활성화되거나 화면이 다시 표시될 때 재측정
        needsRebuild = true;
    }

    private void LateUpdate()
    {
        // 일시정지 중에는 현재 위치와 대기 시간을 유지함
        if (PauseController.IsPaused)
            return;

        if (viewport == null || eventText == null)
            return;

        float viewportWidth = viewport.rect.width;

        // 활성화 직후 레이아웃 크기가 아직 계산되지 않았다면 기다림
        if (viewportWidth <= 0f)
            return;

        // 화면 비율이 바뀌면 새 표시 영역에 맞춰 다시 계산
        if (needsRebuild || Mathf.Abs(viewportWidth - cachedViewportWidth) > 0.5f)
        {
            Rebuild(viewportWidth);
        }

        if (!shouldScroll)
            return;

        if (pauseRemaining > 0f)
        {
            pauseRemaining -= Time.unscaledDeltaTime;
            return;
        }

        if (reachedEnd)
        {
            // 끝부분을 충분히 보여준 다음 처음으로 돌아감
            currentX = 0f;
            reachedEnd = false;
            pauseRemaining = startPause;

            ApplyPosition();
            return;
        }

        currentX = Mathf.MoveTowards(currentX, endX, scrollSpeed * Time.unscaledDeltaTime);

        ApplyPosition();

        if (currentX <= endX)
        {
            reachedEnd = true;
            pauseRemaining = endPause;
        }
    }

    private void Rebuild(float viewportWidth)
    {
        needsRebuild = false;
        cachedViewportWidth = viewportWidth;

        eventText.enableAutoSizing = false;
        eventText.alignment = TextAlignmentOptions.MidlineLeft;
        eventText.overflowMode = TextOverflowModes.Overflow;

        // Rich Text 태그를 포함한 문자열의 실제 표시 너비를 구함
        float preferredWidth = eventText.GetPreferredValues(currentMessage, Mathf.Infinity, Mathf.Infinity).x;

        // 끝 글자가 반올림 오차로 잘리지 않도록 약간의 여유를 둠
        float textWidth = Mathf.Ceil(preferredWidth) + 2f;

        RectTransform textRect = eventText.rectTransform;

        // 가로는 왼쪽 기준, 세로는 Viewport 높이에 맞춤
        textRect.anchorMin = new Vector2(0f, 0f);
        textRect.anchorMax = new Vector2(0f, 1f);
        textRect.pivot = new Vector2(0f, 0.5f);

        textRect.sizeDelta = new Vector2(Mathf.Max(viewportWidth, textWidth), 0f);

        shouldScroll = textWidth > viewportWidth;

        // 마지막에는 문구 끝이 Viewport 오른쪽에 맞도록 이동
        endX = shouldScroll ? viewportWidth - textWidth : 0f;

        currentX = 0f;
        reachedEnd = false;
        pauseRemaining = startPause;

        ApplyPosition();
    }

    private void ApplyPosition()
    {
        eventText.rectTransform.anchoredPosition = new Vector2(currentX, 0f);
    }
}
