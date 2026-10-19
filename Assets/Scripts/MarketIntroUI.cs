using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MarketIntroUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject loadingRoot;
    [SerializeField] private TMP_Text storyText;

    // 대사에 맞는 그림을 표시할 StoryImage 컴포넌트.
    [SerializeField] private Image storyImage;

    [SerializeField] private Button startMonthButton;

    [Header("Story")]
    [SerializeField, Min(0.1f)]
    private float secondsPerLine = 3f;

    // 배열 순서대로 대사와 이미지를 함께 표시합니다.
    [SerializeField]
    private StoryPage[] storyPages = new StoryPage[8];

    // Inspector에서 대사와 이미지를 한 Element 안에 설정하기 위한 자료형.
    [Serializable]
    private class StoryPage
    {
        [SerializeField, TextArea(2, 4)]
        private string dialogue;

        [SerializeField]
        private Sprite illustration;

        // 빈 대사도 허용. 그림만 보여주는 장면을 구성할 수 있음.
        public string Dialogue => dialogue ?? string.Empty;

        public Sprite Illustration => illustration;
    }

    // 스토리와 시장 생성의 완료 상태를 따로 관리.
    private bool storyCompleted;
    private bool marketReady;
    private bool failed;

    private Coroutine storyRoutine;

    // GameManager는 이 값이 true가 될 때까지 게임 시작을 기다림
    public bool StartRequested { get; private set; }

    // Inspector 연결 누락을 게임 시작 전에 검사
    public bool IsConfigured
    {
        get
        {
            if (loadingRoot == null || storyText == null || storyImage == null || startMonthButton == null)
            {
                return false;
            }

            if (storyPages == null || storyPages.Length == 0)
                return false;

            foreach (StoryPage page in storyPages)
            {
                // 대사는 비어 있어도 되지만 이미지는 반드시 필요함.
                if (page == null || page.Illustration == null)
                    return false;
            }

            return true;
        }
    }

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
        storyImage.sprite = null;
        storyImage.enabled = false;

        startMonthButton.interactable = false;
        startMonthButton.gameObject.SetActive(false);

        loadingRoot.SetActive(true);
        loadingRoot.transform.SetAsLastSibling();

        storyRoutine = StartCoroutine(PlayStory());
    }

    /// <summary>
    /// 각 장면의 대사와 이미지를 동시에 표시.
    /// 지정된 시간이 지나면 다음 Element로 넘어감.
    /// </summary>
    /// <returns></returns>
    private IEnumerator PlayStory()
    {
        foreach (StoryPage page in storyPages)
        {
            // 같은 프레임에 대사와 이미지를 함께 변경.
            storyText.text = page.Dialogue;
            storyImage.sprite = page.Illustration;
            storyImage.enabled = true;

            // 스토리 재생 방식을 유지합니다.
            // Inspector의 Seconds Per Line만큼 보여준 뒤 넘어감.
            yield return new WaitForSecondsRealtime(Mathf.Max(0.1f, secondsPerLine));
        }

        // 마지막 장면도 지정된 시간만큼 표시한 뒤 완료 처리.
        storyCompleted = true;
        storyRoutine = null;

        // 시장 생성까지 완료되어야 시작 버튼이 표시됨.
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
