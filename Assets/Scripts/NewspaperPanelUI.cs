using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class NewspaperPanelUI : MonoBehaviour
{
    [Header("Article")]
    [SerializeField] private TMP_Text newspaperNameText;
    [SerializeField] private TMP_Text dateText;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text bodyText;

    [Header("Continue")]
    [SerializeField] private Button continueButton;
    [SerializeField] private TMP_Text continueButtonText;

    private Action onContinue;
    private bool readyToContinue;

    // 신문 이름을 포함해 화면 출력에 필요한 참조가 모두 연결되었는지 검사.
    public bool IsConfigured =>
        newspaperNameText != null &&
        dateText != null &&
        titleText != null &&
        bodyText != null &&
        continueButton != null &&
        continueButtonText != null;

    private void Awake()
    {
        // Inspector의 On Click에는 중복으로 연결xxxxxxxxxx
        if (continueButton != null)
            continueButton.onClick.AddListener(OnClickContinue);
    }

    /// <summary>
    /// 선택된 기사를 표시.
    /// 이 메서드는 턴 증가나 정산을 실행하지 않음.
    /// </summary>
    /// <param name="article"></param>
    /// <param name="isFinalMonth"></param>
    /// <param name="completed"></param>
    /// <param name="buttonLabel"></param>
    public void Show(NewsArticleData article, bool isFinalMonth, Action completed, string buttonLabel = null, string sectionTitle = "경제 소식")
    {
        gameObject.SetActive(true);

        // 같은 PanelLayer의 다른 패널보다 앞에 표시.
        transform.SetAsLastSibling();

        DateTime date = GameManager.Instance.CurrentGameDate;
        dateText.text = $"{date.Year}년 {date.Month}월";

        // GameManager가 부동산 기사를 표시할 때 전달하는 구분값.
        bool isRealEstateNews = sectionTitle == "부동산 소식";

        // 같은 패널을 재사용하므로 매번 신문 이름을 지정.
        // 부동산 신문 다음에 경제, 물가 기사가 나와도 이름이 남지 않게 함.
        newspaperNameText.text = isRealEstateNews ? "달빛 부동산일보" : "달빛 경제일보";

        // 부동산 기사에는 JSON의 제목만 표시.
        // 경제, 물가 기사의 제목 표시는 기존 방식을 유지.
        titleText.text = isRealEstateNews ? article.title : $"이달의 {sectionTitle} : {article.title}";

        // 본문은 전달받은 기사 내용을 그대로 표시.
        bodyText.text = article.text;

        // 추가 기사가 있으면 "물가 상승 소식 확인" 같은 문구를 전달.
        // 별도 문구가 없으면 기존 동작 유지.
        continueButtonText.text = buttonLabel ?? (isFinalMonth ? "최종 결과 확인" : "새로운 달 시작");

        onContinue = completed;
        readyToContinue = true;
        continueButton.interactable = true;
    }

    private void OnClickContinue()
    {
        if (!readyToContinue)
            return;

        // 연속 클릭으로 턴이 두 번 진행되는 것을 막아야함
        readyToContinue = false;
        continueButton.interactable = false;

        // 패널 비활성화 전에 콜백을 별도로 보관.
        Action callback = onContinue;
        onContinue = null;

        gameObject.SetActive(false);

        // 다음 달 진행 또는 엔딩 판정은 GameManager가 담당.
        callback?.Invoke();
    }

    private void OnDisable()
    {
        readyToContinue = false;
        onContinue = null;
    }

    private void OnDestroy()
    {
        if (continueButton != null)
            continueButton.onClick.RemoveListener(OnClickContinue);
    }
}
