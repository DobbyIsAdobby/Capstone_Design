using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class JobMinigamePanelUI : MonoBehaviour
{
    [Header("Status")]
    [SerializeField] private TMP_Text gradeText;
    [SerializeField] private TMP_Text salaryText;
    [SerializeField] private TMP_Text experienceText;
    [SerializeField] private Slider experienceSlider;
    [SerializeField] private TMP_Text apText;
    [SerializeField] private TMP_Text instructionText;
    [SerializeField] private TMP_Text resultText;

    [Header("Minigame")]
    [SerializeField] private RectTransform track;
    [SerializeField] private RectTransform successZone;
    [SerializeField] private RectTransform marker;

    [Header("Button")]
    [SerializeField] private Button closeButton;
    [SerializeField] private Button actionButton;
    [SerializeField] private TMP_Text actionButtonText;

    [Header("Delay")]
    [SerializeField, Min(0f)] private float retryDelay = 0.35f;

    private bool running;
    private bool hasResult;
    private bool waitingForRetry;

    private float retryAvailableTime;
    private float phase;
    private float markerPosition;
    private float targetMinimum;
    private float targetMaximum;

    private JobManager Job => JobManager.Instance;

    private void Awake()
    {
        // 내부 버튼은 코드에서 연결 - 더 이상 복잡하게 따로 연결할 필요 없음
        // Inspector의 On Click에 넣으면 중복됨
        closeButton.onClick.AddListener(Close);
        actionButton.onClick.AddListener(OnAction);

        experienceSlider.interactable = false;
    }

    public void Open()
    {
        GameManager game = GameManager.Instance;

        if (game == null || !game.CanAct || Job == null || !Job.IsConfigured)
        {
            return;
        }

        gameObject.SetActive(true);
        transform.SetAsLastSibling();

        running = false;
        hasResult = false;
        waitingForRetry = false;
        retryAvailableTime = 0f;

        resultText.text = "";

        // 실제 레이아웃 너비를 계산한 다음 게임 영역을 배치
        Canvas.ForceUpdateCanvases();

        PrepareGeometry();
        markerPosition = 0f;

        UpdateVisuals();
        RefreshStatus();
    }

    private void PrepareGeometry()
    {
        float halfWidth = Job.Rules.SuccessWidth * 0.5f;

        targetMinimum = 0.5f - halfWidth;
        targetMaximum = 0.5f + halfWidth;

        marker.anchorMin = new Vector2(0.5f, 0.5f);
        marker.anchorMax = new Vector2(0.5f, 0.5f);
        marker.pivot = new Vector2(0.5f, 0.5f);

        successZone.anchorMin = new Vector2(0.5f, 0.5f);
        successZone.anchorMax = new Vector2(0.5f, 0.5f);
        successZone.pivot = new Vector2(0.5f, 0.5f);
    }

    private void Update()
    {
        // 일시정지 중에는 이동과 재시도 상태를 갱신하지 않음.
        if (PauseController.IsPaused)
            return;

        if (running)
        {
            phase += Time.deltaTime * Job.Rules.TravelSpeed;

            // 0 → 1 → 0으로 반복 이동
            markerPosition = Mathf.PingPong(phase, 1f);

            UpdateVisuals();
            return;
        }

        // 정지 버튼을 연속 클릭해 다음 게임까지 바로 시작되는 것을 방지
        if (waitingForRetry && Time.time >= retryAvailableTime)
        {
            waitingForRetry = false;
            RefreshStatus();
        }
    }

    private void UpdateVisuals()
    {
        float radius = marker.rect.width * 0.5f;

        // 원이 Track 밖으로 나가지 않도록 반지름만큼 여백을 둠. - UI라 collider가 없음
        float travelWidth = Mathf.Max(0f, track.rect.width - radius * 2f);

        float left = -travelWidth * 0.5f;

        marker.anchoredPosition = new Vector2(left + markerPosition * travelWidth, 0f);

        float targetCenter = (targetMinimum + targetMaximum) * 0.5f;

        successZone.anchoredPosition = new Vector2(left + targetCenter * travelWidth, 0f);

        successZone.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, travelWidth * (targetMaximum - targetMinimum));

        successZone.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, track.rect.height);
    }

    private void OnAction()
    {
        // 일시정지 상태에서 시작 or 판정 버튼이 실행되지 않게 함
        if(PauseController.IsPaused)
            return;

        if (running)
        {
            StopRound();
            return;
        }

        if (waitingForRetry)
            return;

        StartRound();
    }

    private void StartRound()
    {
        Canvas.ForceUpdateCanvases();

        // AP를 차감하기 전에 게임 영역이 유효한지 확인
        if (track.rect.width <= marker.rect.width)
        {
            resultText.text = "Track과 Marker 크기를 확인하세요.";
            return;
        }

        PrepareGeometry();

        if (!Job.TryStart(out string reason))
        {
            resultText.text = reason;
            RefreshStatus();
            return;
        }

        phase = 0f;
        markerPosition = 0f;

        running = true;
        hasResult = false;
        resultText.text = "";

        UpdateVisuals();
        RefreshStatus();

        if (UIManager.Instance != null)
            UIManager.Instance.RefreshUI();
    }

    private void StopRound()
    {
        if (!running)
            return;

        running = false;

        // 화면에 표시한 원 중심 위치를 판정
        bool success = markerPosition >= targetMinimum && markerPosition <= targetMaximum;

        Job.Finish(success, out string message);

        hasResult = true;
        resultText.text = message;

        waitingForRetry = retryDelay > 0f;
        retryAvailableTime = Time.time + retryDelay;

        RefreshStatus();

        if (UIManager.Instance != null)
            UIManager.Instance.RefreshUI();
    }

    public void RefreshStatus()
    {
        if (Job == null || !Job.IsConfigured)
            return;

        GameManager game = GameManager.Instance;

        gradeText.text = $"현재 직급: {Job.GradeName}";
        salaryText.text = $"월급 {Job.MonthlySalary:N0}원";
        apText.text = $"AP {game.CurrentAP} / {game.MaxAP}";

        experienceSlider.minValue = 0f;
        experienceSlider.maxValue = 1f;

        if (Job.IsMaxGrade)
        {
            experienceText.text = "최고 직급 달성";
            experienceSlider.SetValueWithoutNotify(1f);
        }
        else
        {
            experienceText.text = $"경험치 {Job.CurrentExperience} / " + $"{Job.RequiredExperience}";

            experienceSlider.SetValueWithoutNotify((float)Job.CurrentExperience / Job.RequiredExperience);
        }

        instructionText.text = "원의 중심을 노란 구간에 맞춰 멈추세요.\n" + $"성공 +{Job.Rules.SuccessExperience} / " + $"실패 +{Job.Rules.FailureExperience}";

        closeButton.interactable = !running;

        if (running)
        {
            actionButton.interactable = true;
            actionButtonText.text = "멈추기";
            return;
        }

        bool canStart = Job.CanStart(out string reason);

        actionButton.interactable = canStart && !waitingForRetry;

        actionButtonText.text = Job.IsMaxGrade ? "최고 직급 달성" : $"{(hasResult ? "다시 도전" : "시작")}";

        if (!canStart && !Job.IsMaxGrade)
            apText.text += $"\n{reason}";
    }

    public void Close()
    {
        // 일시정지 중에는 미니게임 패널을 닫지 못함
        if (PauseController.IsPaused)
            return;

        // 플레이 중 닫고 다시 열어서 AP 없이 재시도하지 못하게 막음
        if (running)
            return;

        gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        waitingForRetry = false;

        if (!running)
            return;

        running = false;

        if (Job != null)
            Job.AbortAttempt();

        if (UIManager.Instance != null)
            UIManager.Instance.RefreshUI();
    }

    private void OnDestroy()
    {
        closeButton.onClick.RemoveListener(Close);
        actionButton.onClick.RemoveListener(OnAction);
    }
}
