using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EventFlowUI : MonoBehaviour
{
    [Header("Event Root")]
    [SerializeField] private GameObject root;

    [Header("Dialogue")]
    [SerializeField] private GameObject dialogueRoot;
    [SerializeField] private Button dialogueButton;
    [SerializeField] private TMP_Text dialogueText;

    [Header("Notice & Payment Result")]
    [SerializeField] private GameObject noticeRoot;
    [SerializeField] private TMP_Text noticeTitleText;
    [SerializeField] private TMP_Text noticeMainText;
    [SerializeField] private TMP_Text paymentTitleText;
    [SerializeField] private TMP_Text paymentText;
    [SerializeField] private Button noticeConfirmButton;

    [Header("BreakingNews")]
    [SerializeField] private GameObject breakingRoot;
    [SerializeField] private TMP_Text clickGuideText;
    [SerializeField] private Button screenClickButton;

    [SerializeField, Min(0f)]
    private float breakingDelay = 3f;

    [Header("Event Newspaper")]
    [SerializeField] private GameObject newspaperRoot;
    [SerializeField] private TMP_Text newspaperTitleText;
    [SerializeField] private TMP_Text newspaperBodyText;
    [SerializeField] private TMP_Text newspaperDateText;
    [SerializeField] private Button newspaperConfirmButton;

    [Header("Income/Expense Money Color")]
    [SerializeField]
    private Color incomeColor = new Color32(0, 200, 0, 255);

    [SerializeField]
    private Color expenseColor = new Color32(200, 0, 0, 255);

    [Header("Inflation Notice Layout")]
    [SerializeField] private GameObject noticeMainDivider;

    [SerializeField]
    private Vector2 inflationBodyPosition = new Vector2(0f, 25f);

    [SerializeField]
    private Vector2 inflationBodySize = new Vector2(700f, 450f);

    // 일반 금액 안내창으로 돌아갈 때 복원할 원래 배치.
    private bool noticeLayoutCached;
    private Vector2 originalNoticeBodyPosition;
    private Vector2 originalNoticeBodySize;
    private bool originalMainDividerActive;

    // 현재 클릭을 기다리고 있는 버튼.
    // null이면 버튼을 눌러도 진행하지 않음.
    private Button waitingButton;
    private bool clicked;

    public bool IsConfigured =>
        root != null &&
        dialogueRoot != null &&
        dialogueButton != null &&
        dialogueText != null &&
        noticeRoot != null &&
        noticeTitleText != null &&
        noticeMainText != null &&
        paymentTitleText != null &&
        paymentText != null &&
        noticeConfirmButton != null &&
        breakingRoot != null &&
        clickGuideText != null &&
        screenClickButton != null &&
        newspaperRoot != null &&
        newspaperTitleText != null &&
        newspaperBodyText != null &&
        newspaperDateText != null &&
        newspaperConfirmButton != null &&
        noticeMainDivider != null;

    private void Awake()
    {
        CacheNoticeLayout();

        // 버튼 이벤트는 코드에서 연결.
        // Inspector의 On Click에는 같은 동작을 등록하지 않음.
        RegisterButton(dialogueButton);
        RegisterButton(noticeConfirmButton);
        RegisterButton(screenClickButton);
        RegisterButton(newspaperConfirmButton);

        CloseAll();
    }

    /// <summary>
    /// 현재 직접 배치한 일반 안내창의 좌표와 크기를 보관.
    /// </summary>
    private void CacheNoticeLayout()
    {
        if (noticeLayoutCached || noticeMainText == null)
            return;

        RectTransform rect = noticeMainText.rectTransform;

        originalNoticeBodyPosition = rect.anchoredPosition;
        originalNoticeBodySize = rect.sizeDelta;

        originalMainDividerActive = noticeMainDivider != null && noticeMainDivider.activeSelf;

        noticeLayoutCached = true;
    }

    /// <summary>
    /// 지출, 수입 안내에서 원래 배치를 다시 사용.
    /// </summary>
    private void RestoreNoticeLayout()
    {
        CacheNoticeLayout();

        if (!noticeLayoutCached)
            return;

        noticeMainText.rectTransform.anchoredPosition = originalNoticeBodyPosition;

        noticeMainText.rectTransform.sizeDelta = originalNoticeBodySize;

        if (paymentTitleText != null)
            paymentTitleText.gameObject.SetActive(true);

        if (noticeMainDivider != null)
            noticeMainDivider.SetActive(originalMainDividerActive);
    }

    /// <summary>
    /// 물가가 적용된 턴 시작에 message와 targets를 표시.
    /// 금액을 지급하거나 차감하지 않음.
    /// </summary>
    /// <param name="news"></param>
    /// <returns></returns>
    public IEnumerator ShowInflationNotice(InflationNewsData news)
    {
        RestoreNoticeLayout();
        OpenPage(noticeRoot);

        noticeTitleText.text = "물가 안내";

        string[] targetLines = Array.ConvertAll(news.notice.targets, target => "- " + target);

        noticeMainText.text = news.notice.message + "\n\n인상 대상\n" + string.Join("\n", targetLines);

        // 대상 목록이 들어갈 공간 확보.
        noticeMainText.rectTransform.anchoredPosition = inflationBodyPosition;

        noticeMainText.rectTransform.sizeDelta = inflationBodySize;

        // PaymentText는 PaymentTitleText의 자식이므로 함께 숨겨짐.
        paymentTitleText.gameObject.SetActive(false);
        noticeMainDivider.SetActive(false);

        yield return WaitForClick(noticeConfirmButton);

        // 이후 지출, 수입 결과 안내가 정상 배치로 나오도록 복원.
        RestoreNoticeLayout();
    }

    private void RegisterButton(Button button)
    {
        if (button != null)
            button.onClick.AddListener(OnClickContinue);
    }

    private void UnregisterButton(Button button)
    {
        if (button != null)
            button.onClick.RemoveListener(OnClickContinue);
    }

    private void OnDestroy()
    {
        UnregisterButton(dialogueButton);
        UnregisterButton(noticeConfirmButton);
        UnregisterButton(screenClickButton);
        UnregisterButton(newspaperConfirmButton);
    }

    private void OnClickContinue()
    {
        // 3초 대기 중이거나, 이미 클릭을 처리한 경우에는 무시.
        if (waitingButton == null || clicked)
            return;

        clicked = true;

        // 연속 클릭으로 다음 화면까지 넘어가지 않도록 즉시 차단.
        waitingButton.interactable = false;
    }

    /// <summary>
    /// 대사 배경에 hint_text를 표시하고 클릭을 기다림.
    /// </summary>
    /// <param name="message"></param>
    /// <returns></returns>
    public IEnumerator ShowDialogue(string message)
    {
        OpenPage(dialogueRoot);
        dialogueText.text = message;

        yield return WaitForClick(dialogueButton);
    }

    /// <summary>
    /// 지출 이벤트가 발생한 달에 납부 예정 금액을 안내.
    /// 현금 차감은 하지 않음.
    /// </summary>
    /// <param name="item"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public IEnumerator ShowExpenseNotice(FixedEventData item)
    {
        if (item == null || item.value >= 0)
            throw new ArgumentException("지출 이벤트만 안내할 수 있습니다.");

        yield return ShowNotice("지출 안내", item.notice_text, "이번 달 마감 지출금 추가", item.value);
    }

    /// <summary>
    /// 지난달 마감에 이미 정산한 결과를 다음 턴에 안내.
    /// 여기서 돈을 다시 지급하거나 차감하면 안 됨.
    /// </summary>
    /// <param name="item"></param>
    /// <returns></returns>
    public IEnumerator ShowSettlementResult(FixedEventData item)
    {
        bool isIncome = item.value > 0;

        yield return ShowNotice(isIncome ? "수입 결과" : "지출 결과", item.event_text, isIncome ? "지난달 지급받은 금액" : "지난달 지출한 금액",
            item.value);
    }

    /// <summary>
    /// 하이어라키의 네 텍스트를 각각 갱신.
    /// </summary>
    /// <param name="title"></param>
    /// <param name="message"></param>
    /// <param name="paymentTitle"></param>
    /// <param name="amount"></param>
    /// <returns></returns>
    private IEnumerator ShowNotice(string title, string message, string paymentTitle, long amount)
    {
        RestoreNoticeLayout();

        OpenPage(noticeRoot);

        noticeTitleText.text = title;
        noticeMainText.text = message;
        paymentTitleText.text = paymentTitle;

        paymentText.text = amount > 0 ? $"+{amount:N0} 원" : $"{amount:N0} 원";

        paymentText.color = amount > 0 ? incomeColor : expenseColor;

        yield return WaitForClick(noticeConfirmButton);
    }

    /// <summary>
    /// 긴급속보 >> 3초 대기 >> 화면 클릭 >> 이벤트 신문.
    /// 신문 확인 후에도 같은 턴을 유지.
    /// </summary>
    /// <param name="item"></param>
    /// <param name="date"></param>
    /// <returns></returns>
    public IEnumerator ShowBreaking(RandomMarketEventData item, DateTime date)
    {
        OpenPage(breakingRoot);

        // 처음 3초 동안은 클릭할 수 없고 안내 문구도 숨김.
        screenClickButton.interactable = false;
        clickGuideText.gameObject.SetActive(false);

        yield return new WaitForSeconds(breakingDelay);

        clickGuideText.gameObject.SetActive(true);

        yield return WaitForClick(screenClickButton);

        // 클릭한 다음 이벤트 신문으로 전환.
        OpenPage(newspaperRoot);

        newspaperTitleText.text = item.title;
        newspaperBodyText.text = item.event_text;
        newspaperDateText.text = $"{date.Year}년 {date.Month}월";

        // NewsNameText의 "긴급 속보"와 버튼의 "확인"은 Inspector에 입력한 문구를 그대로 사용.
        yield return WaitForClick(newspaperConfirmButton);
    }

    /// <summary>
    /// 표시할 화면 하나만 활성화.
    /// 나머지 화면이 클릭을 가로채지 않도록 비활성화.
    /// </summary>
    /// <param name="page"></param>
    /// <exception cref="InvalidOperationException"></exception>
    private void OpenPage(GameObject page)
    {
        if (!IsConfigured)
            throw new InvalidOperationException("EventFlowUI의 Inspector 연결을 확인하세요.");

        DisableButtons();

        waitingButton = null;
        clicked = false;

        dialogueRoot.SetActive(false);
        noticeRoot.SetActive(false);
        breakingRoot.SetActive(false);
        newspaperRoot.SetActive(false);

        root.SetActive(true);

        // 같은 PanelLayer의 다른 패널보다 앞에 표시.
        root.transform.SetAsLastSibling();

        page.SetActive(true);
    }

    /// <summary>
    /// 지정한 버튼 하나만 활성화하고 클릭을 기다림.
    /// </summary>
    /// <param name="button"></param>
    /// <returns></returns>
    private IEnumerator WaitForClick(Button button)
    {
        clicked = false;
        waitingButton = button;
        button.interactable = true;

        while (!clicked)
            yield return null;

        button.interactable = false;
        waitingButton = null;
        clicked = false;

        // 같은 프레임의 입력이 다음 화면까지 처리되지 않도록 분리.
        yield return null;
    }

    private void DisableButtons()
    {
        if (dialogueButton != null)
            dialogueButton.interactable = false;

        if (noticeConfirmButton != null)
            noticeConfirmButton.interactable = false;

        if (screenClickButton != null)
            screenClickButton.interactable = false;

        if (newspaperConfirmButton != null)
            newspaperConfirmButton.interactable = false;
    }

    /// <summary>
    /// 모든 이벤트 화면을 닫음.
    /// 불러오기 때도 사용.
    /// </summary>
    public void CloseAll()
    {
        RestoreNoticeLayout();

        DisableButtons();

        waitingButton = null;
        clicked = false;

        if (dialogueRoot != null)
            dialogueRoot.SetActive(false);

        if (noticeRoot != null)
            noticeRoot.SetActive(false);

        if (breakingRoot != null)
            breakingRoot.SetActive(false);

        if (newspaperRoot != null)
            newspaperRoot.SetActive(false);

        if (root != null)
            root.SetActive(false);
    }
}
