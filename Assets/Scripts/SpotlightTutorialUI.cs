using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems; // User Interaction을 감지하고, 이를 UI/Object에 전달하여 Interaction을 처리하는 namespace.
using UnityEngine.UI;

public sealed class SpotlightTutorialUI : MonoBehaviour
{
    [Serializable]
    public sealed class TargetBinding
    {
        public string id;
        public RectTransform target;

        // 실제 클릭을 요구하는 WAIT_EVENT 대상에만 연결
        public Button button;

        // 스크롤 내부 대상에만 연결
        public ScrollRect scroll;

        // 하단 메뉴 페이지를 맞추는 용도로 사용
        public UnityEvent prepare = new UnityEvent();
    }

    [Header("Targets")]
    [SerializeField] private TargetBinding[] targets;

    [Header("Canvas")]
    [SerializeField] private RectTransform canvasRect;

    [Header("Dim Images")]
    [SerializeField] private RectTransform topDim;
    [SerializeField] private RectTransform bottomDim;
    [SerializeField] private RectTransform leftDim;
    [SerializeField] private RectTransform rightDim;

    [Header("Focus")]
    [SerializeField] private RectTransform focusBorder;
    [SerializeField] private SpotlightInputBlocker inputBlocker;
    [SerializeField] private float padding = 12f;

    [Header("Guide")]
    [SerializeField] private RectTransform guidePanel;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private TMP_Text progressText;
    [SerializeField] private Button nextButton;
    [SerializeField] private Button skipButton;

    private readonly Dictionary<string, TargetBinding> targetMap = new Dictionary<string, TargetBinding>();

    private readonly Vector3[] corners = new Vector3[4];

    private RectTransform currentTarget;
    private bool initialized;

    private EventSystem inputSystem;
    private bool originalNavigation;
    private bool navigationBlocked;

    public bool NextRequested { get; private set; }
    public bool SkipRequested { get; private set; }

    /// <summary>
    /// Canvas가 비활성 상태여도 Manager에서 직접 호출
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    public void Initialize()
    {
        if (initialized)
            return;

        if (canvasRect == null ||
            topDim == null ||
            bottomDim == null ||
            leftDim == null ||
            rightDim == null ||
            focusBorder == null ||
            inputBlocker == null ||
            guidePanel == null ||
            messageText == null ||
            progressText == null ||
            nextButton == null ||
            skipButton == null ||
            targets == null)
        {
            throw new InvalidOperationException("SpotlightTutorialUI의 Inspector 연결을 확인하세요.");
        }

        targetMap.Clear();

        foreach (TargetBinding binding in targets)
        {
            if (binding == null || string.IsNullOrWhiteSpace(binding.id) || binding.target == null || targetMap.ContainsKey(binding.id))
            {
                throw new InvalidOperationException("튜토리얼 대상 ID, RectTransform 또는 중복을 확인하세요.");
            }

            targetMap.Add(binding.id, binding);
        }

        nextButton.onClick.AddListener(RequestNext);
        skipButton.onClick.AddListener(RequestSkip);

        initialized = true;
        Hide();
    }

    private void RequestNext()
    {
        // 행동 대기 중이거나 안내가 숨겨진 상태에서는 배경을 눌러 다음 단계로 넘어갈 수 없음.
        if (nextButton == null || !nextButton.isActiveAndEnabled || !nextButton.IsInteractable())
        {
            return;
        }

        NextRequested = true;
    }

    private void RequestSkip()
    {
        SkipRequested = true;
    }

    public TargetBinding GetTarget(string id)
    {
        // 빈 ID는 특정 대상 없이 대사만 표시하는 단계
        if (string.IsNullOrEmpty(id))
            return null;

        if (!targetMap.TryGetValue(id, out TargetBinding binding))
        {
            throw new InvalidOperationException($"연결되지 않은 튜토리얼 대상입니다: {id}");
        }

        return binding;
    }

    /// <summary>
    /// 메뉴 페이지 및 스크롤 위치를 맞춤.
    /// 부모 패널을 여는 것은 앞선 WAIT_EVENT 단계에서 처리함
    /// </summary>
    /// <param name="binding"></param>
    /// <exception cref="InvalidOperationException"></exception>
    public void PrepareTarget(TargetBinding binding)
    {
        if (binding == null)
            return;

        binding.prepare.Invoke();

        if (!binding.target.gameObject.activeInHierarchy)
        {
            throw new InvalidOperationException($"대상 패널이 열리지 않았습니다: {binding.id}");
        }

        Canvas.ForceUpdateCanvases();

        ScrollRect scroll = binding.scroll;

        if (scroll == null)
            return;

        if (scroll.content == null || scroll.viewport == null)
        {
            throw new InvalidOperationException($"ScrollRect 연결을 확인하세요: {binding.id}");
        }

        scroll.StopMovement();

        // Viewport 좌표에서 대상의 중심 위치를 계산.
        Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport, binding.target);

        Vector2 delta = scroll.viewport.rect.center - (Vector2)bounds.center;

        if (!scroll.horizontal)
            delta.x = 0f;

        if (!scroll.vertical)
            delta.y = 0f;

        // Viewport 좌표의 이동량을 Content 부모 좌표로 변환
        Vector3 worldDelta = scroll.viewport.TransformVector(delta);
        Vector3 localDelta = scroll.content.parent.InverseTransformVector(worldDelta);

        scroll.content.anchoredPosition += (Vector2)localDelta;

        Canvas.ForceUpdateCanvases();

        // 정상 스크롤 범위를 벗어나지 않도록 제한
        if (scroll.vertical)
        {
            scroll.verticalNormalizedPosition = Mathf.Clamp01(scroll.verticalNormalizedPosition);
        }

        if (scroll.horizontal)
        {
            scroll.horizontalNormalizedPosition = Mathf.Clamp01(scroll.horizontalNormalizedPosition);
        }

        Canvas.ForceUpdateCanvases();
    }

    public void Show(RectTransform target, string message, bool showNext, string progress, bool allowTargetInput)
    {
        NextRequested = false;
        SkipRequested = false;

        currentTarget = target;

        messageText.text = message;
        progressText.text = progress ?? "";

        // Next Button이 가이드 배경이므로 항상 표시해야함
        // 행동 대기 중에도 설명, 진행도, 건너뛰기는 보여야함
        nextButton.gameObject.SetActive(true);

        // 설명 단계에서만 배경 클릭으로 다음 대사로 넘어감
        nextButton.interactable = showNext;

        // 부모 배경의 Button 비활성 상태와 관계없이, 건너뛰기 버튼은 계속 사용할 수 있도록 유지
        skipButton.gameObject.SetActive(true);
        skipButton.interactable = true;

        inputBlocker.SetAllowedTarget(allowTargetInput ? target : null);

        gameObject.SetActive(true);

        // 준비 중 숨겼던 안내창을 안내 표시 시 다시 활성화
        guidePanel.gameObject.SetActive(true);

        // 이전에 선택된 버튼에 키보드 Submit이 들어가지 않도록 해야함
        if (!navigationBlocked && EventSystem.current != null)
        {
            inputSystem = EventSystem.current;
            originalNavigation = inputSystem.sendNavigationEvents;

            inputSystem.sendNavigationEvents = false;
            inputSystem.SetSelectedGameObject(null);

            navigationBlocked = true;
        }

        RefreshGeometry();
    }

    /// <summary>
    /// 다음 안내를 준비하는 동안 안내창만 숨김.
    /// 배경 가림막과 입력 차단은 유지.
    /// </summary>
    public void ShowPreparing()
    {
        // 이전 클릭 요청을 초기화하고 모든 대상의 입력을 차단
        // 준비 중에는 안내할 대상이 없으므로 전체 화면을 가림
        Show(null, "", false, "", false);

        // 안내창 숨김. GuidePanel 비활성
        guidePanel.gameObject.SetActive(false);
    }

    public void SetProgress(string value)
    {
        progressText.text = value;
    }

    public void BlockTargetInput()
    {
        inputBlocker.SetAllowedTarget(null);
    }

    public void Hide()
    {
        if (inputBlocker != null)
            inputBlocker.SetAllowedTarget(null);

        RestoreNavigation();
        gameObject.SetActive(false);
    }

    private void RestoreNavigation()
    {
        if (navigationBlocked && inputSystem != null)
        {
            inputSystem.SetSelectedGameObject(null);
            inputSystem.sendNavigationEvents = originalNavigation;
        }

        navigationBlocked = false;
    }

    private void OnDisable()
    {
        RestoreNavigation();
    }

    private void LateUpdate()
    {
        // 튜토리얼 Canvas가 활성화된 동안만 추적함
        RefreshGeometry();
    }

    /// <summary>
    /// Canvas 로컬 좌표를 실제 위치로 변환해서 배치.
    /// 대상 RectTransform의 Pivot은 중앙으로 설정해야 함.
    /// </summary>
    /// <param name="rect"></param>
    /// <param name="canvasPosition"></param>
    /// <param name="size"></param>
    private void Place(RectTransform rect, Vector2 canvasPosition, Vector2 size)
    {
        if (rect.sizeDelta != size)
            rect.sizeDelta = size;

        // Canvas 좌표를 월드 좌표로 변환
        // DimRoot처럼 중간 부모가 있어도 같은 화면 위치에 배치됨
        Vector3 worldPosition = canvasRect.TransformPoint(new Vector3(canvasPosition.x, canvasPosition.y, 0f));

        if (rect.position != worldPosition)
            rect.position = worldPosition;
    }

    /// <summary>
    /// 가이드 패널을 화면 안쪽에 배치.
    /// 대상이 아래에 있으면 안내창을 위에, 위에 있으면 아래로.
    /// 대상이 없으면 화면 중앙에 표시.
    /// </summary>
    /// <param name="targetCenterY"></param>
    private void PlaceGuidePanel(float? targetCenterY)
    {
        Rect screen = canvasRect.rect;

        const float margin = 30f;

        // GuidePanel의 크기가 배경 크기와 같아야 함
        float halfWidth = guidePanel.rect.width * 0.5f;
        float halfHeight = guidePanel.rect.height * 0.5f;

        float x = screen.center.x;
        float y = screen.center.y;

        if (targetCenterY.HasValue)
        {
            bool targetIsBelowCenter = targetCenterY.Value < screen.center.y;

            y = targetIsBelowCenter ? screen.yMax - margin - halfHeight : screen.yMin + margin + halfHeight;
        }

        // 패널 중심이 이동할 수 있는 범위를 계산
        float minimumX = screen.xMin + margin + halfWidth;
        float maximumX = screen.xMax - margin - halfWidth;

        float minimumY = screen.yMin + margin + halfHeight;
        float maximumY = screen.yMax - margin - halfHeight;

        // 패널이 화면보다 작은 경우에는 전체가 화면 안에 들어오도록 제한
        // 화면보다 큰 경우에는 중앙에 배치. 이때는 패널 크기 조정이 필요함.
        x = minimumX <= maximumX ? Mathf.Clamp(x, minimumX, maximumX) : screen.center.x;

        y = minimumY <= maximumY ? Mathf.Clamp(y, minimumY, maximumY) : screen.center.y;

        Vector3 worldPosition = canvasRect.TransformPoint(new Vector3(x, y, 0f));

        if (guidePanel.position != worldPosition)
            guidePanel.position = worldPosition;
    }

    private void RefreshGeometry()
    {
        Rect screen = canvasRect.rect;

        if (currentTarget == null || !currentTarget.gameObject.activeInHierarchy)
        {
            // 대상이 없으면 화면 전체를 하나의 가림막으로 덮음.
            Place(topDim, screen.center, screen.size);
            Place(bottomDim, Vector2.zero, Vector2.zero);
            Place(leftDim, Vector2.zero, Vector2.zero);
            Place(rightDim, Vector2.zero, Vector2.zero);

            focusBorder.gameObject.SetActive(false);

            // Canvas의 중앙에 안내창을 배치.
            PlaceGuidePanel(null);
            return;
        }

        Canvas sourceCanvas = currentTarget.GetComponentInParent<Canvas>().rootCanvas;

        Camera sourceCamera = sourceCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : sourceCanvas.worldCamera;

        currentTarget.GetWorldCorners(corners);

        Vector2 minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);

        Vector2 maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);

        foreach (Vector3 corner in corners)
        {
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(sourceCamera, corner);

            // TutorialCanvas는 Screen Space - Overlay임. 따라서 camera는 null
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, null, out Vector2 localPoint);

            minimum = Vector2.Min(minimum, localPoint);
            maximum = Vector2.Max(maximum, localPoint);
        }

        minimum -= Vector2.one * padding;
        maximum += Vector2.one * padding;

        minimum.x = Mathf.Clamp(minimum.x, screen.xMin, screen.xMax);
        minimum.y = Mathf.Clamp(minimum.y, screen.yMin, screen.yMax);
        maximum.x = Mathf.Clamp(maximum.x, minimum.x, screen.xMax);
        maximum.y = Mathf.Clamp(maximum.y, minimum.y, screen.yMax);

        float middleY = (minimum.y + maximum.y) * 0.5f;

        Place(
            topDim,
            new Vector2(screen.center.x, (maximum.y + screen.yMax) * 0.5f),
            new Vector2(screen.width, screen.yMax - maximum.y));

        Place(
            bottomDim,
            new Vector2(screen.center.x, (screen.yMin + minimum.y) * 0.5f),
            new Vector2(screen.width, minimum.y - screen.yMin));

        Place(
            leftDim,
            new Vector2((screen.xMin + minimum.x) * 0.5f, middleY),
            new Vector2(minimum.x - screen.xMin, maximum.y - minimum.y));

        Place(
            rightDim,
            new Vector2((maximum.x + screen.xMax) * 0.5f, middleY),
            new Vector2(screen.xMax - maximum.x, maximum.y - minimum.y));

        focusBorder.gameObject.SetActive(true);

        Place(focusBorder, (minimum + maximum) * 0.5f, maximum - minimum);

        // 대상 위치와 화면 경계를 기준으로 안내창 배치.
        PlaceGuidePanel(middleY);
    }
}
