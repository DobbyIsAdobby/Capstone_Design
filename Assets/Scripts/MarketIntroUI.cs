using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MarketIntroUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject loadingRoot;
    [SerializeField] private TMP_Text storyText;
    [SerializeField] private Button startMonthButton;

    [Header("Story")]
    [SerializeField, Min(0.1f)]
    private float secondsPerLine = 3f;

    [SerializeField, TextArea(2, 4)]
    private string[] storyLines =
    {
        "아~ 드디어 전역이다. 군 적금 덕분에 3천만 원이나 모았네.",
        "요즘 군 적금이 좋다더니 진짜 괜찮네.",
        "",
        "몇 달 뒤...",
        "뭐야, 벌써 천만 원이나 썼어? 이제 2천만 원밖에 안 남았잖아!",
        "이제부터라도 제대로 자산관리를 해보자.",
        "5년 뒤에는 어떤 모습이 되어 있을까?",
        "목표는 내 집 마련. 이번에는 제대로 해보자."
    };

    // 스토리와 시장 생성의 완료 상태를 따로 관리.
    private bool storyCompleted;
    private bool marketReady;
    private bool failed;

    private Coroutine storyRoutine;

    // GameManager는 이 값이 true가 될 때까지 게임 시작을 기다림
    public bool StartRequested { get; private set; }

    // Inspector 연결 누락을 게임 시작 전에 검사
    public bool IsConfigured =>
        loadingRoot != null &&
        storyText != null &&
        startMonthButton != null &&
        storyLines != null &&
        storyLines.Length > 0;

    private void Awake()
    {
        if (startMonthButton != null)
        {
            startMonthButton.onClick.AddListener(OnClickStart);
            startMonthButton.interactable = false;
            startMonthButton.gameObject.SetActive(false);
        }

        // 새 게임에서는 BeginIntro가 다시 표시됨
        // 불러오기에서는 이 패널을 표시하지 않음
        if (loadingRoot != null)
            loadingRoot.SetActive(false);
    }

    /// <summary>
    /// 새 게임에서 스토리 출력.
    /// 시장 생성은 GameManager가 별도로 시작
    /// </summary>
    public void BeginIntro()
    {
        StopStory();

        storyCompleted = false;
        marketReady = false;
        failed = false;
        StartRequested = false;

        storyText.text = "";

        startMonthButton.interactable = false;
        startMonthButton.gameObject.SetActive(false);

        loadingRoot.SetActive(true);
        loadingRoot.transform.SetAsLastSibling();

        storyRoutine = StartCoroutine(PlayStory());
    }

    private IEnumerator PlayStory()
    {
        foreach (string line in storyLines)
        {
            // 한 문장을 표시한 뒤 지정된 시간만큼 대기.
            storyText.text = line;

            // 게임 시간 배율과 무관하게 스토리 표시 시간을 계산함
            yield return new WaitForSecondsRealtime(Mathf.Max(0.1f, secondsPerLine));
        }

        // 마지막 문장까지 표시한 후 완료 처리
        storyCompleted = true;
        storyRoutine = null;

        RefreshStartButton();
    }

    /// <summary>
    /// 시장 생성이 성공했을 때 GameManager가 호출됨
    /// 스토리가 끝나지 않았다면 버튼은 아직 표시하지 않음 
    /// - 기기간 성능 고려. 두개의 조건을 모두 달성해야지만 버튼이 활성화 되게끔
    /// </summary>
    public void NotifyMarketReady()
    {
        marketReady = true;
        RefreshStartButton();
    }

    /// <summary>
    /// 시장 생성 실패 시 스토리를 중단하고 시작을 막음.
    /// 실패 문구는 MarketGenerator의 ProgressText를 사용.
    /// </summary>
    public void NotifyMarketFailed()
    {
        failed = true;
        marketReady = false;

        StopStory();
        RefreshStartButton();
    }

    private void RefreshStartButton()
    {
        // 두 작업이 모두 성공적으로 끝나야 시작할 수 있음
        bool canStart = marketReady && storyCompleted && !failed && !StartRequested;

        startMonthButton.gameObject.SetActive(canStart);
        startMonthButton.interactable = canStart;
    }

    private void OnClickStart()
    {
        // 버튼 상태뿐 아니라 실행 조건도 확인해야함
        if (!marketReady || !storyCompleted || failed || StartRequested)
        {
            return;
        }

        // 여기서는 게임을 초기화하지 않고 시작 요청만 전달
        StartRequested = true;
        startMonthButton.interactable = false;
    }

    /// <summary>
    /// GameManager가 첫 달 초기화를 마친 뒤 호출
    /// </summary>
    public void Hide()
    {
        StopStory();
        loadingRoot.SetActive(false);
    }

    private void StopStory()
    {
        if (storyRoutine == null)
            return;

        StopCoroutine(storyRoutine);
        storyRoutine = null;
    }

    private void OnDestroy()
    {
        if (startMonthButton != null)
            startMonthButton.onClick.RemoveListener(OnClickStart);
    }
}
