using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class NewspaperPanelUI : MonoBehaviour
{
    [Header("Article")]
    [SerializeField] private TMP_Text dateText;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text bodyText;

    [Header("Continue")]
    [SerializeField] private Button continueButton;
    [SerializeField] private TMP_Text continueButtonText;

    private Action onContinue;
    private bool readyToContinue;

    public bool IsConfigured => dateText != null && titleText != null && bodyText != null && continueButton != null && continueButtonText != null;

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
    public void Show(NewsArticleData article, bool isFinalMonth, Action completed, string buttonLabel = null)
    {
        gameObject.SetActive(true);

        // 같은 PanelLayer의 다른 패널보다 앞에 표시.
        transform.SetAsLastSibling();

        DateTime date = GameManager.Instance.CurrentGameDate;
        dateText.text = $"{date.Year}년 {date.Month}월";
        titleText.text = $"오늘의 경제 소식 : " + article.title;
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
