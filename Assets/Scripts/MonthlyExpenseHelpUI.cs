using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MonthlyExpenseHelpUI : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private GameObject expenseHelpRoot;

    [Header("Buttons")]
    [SerializeField] private Button expenseHelpButton;
    [SerializeField] private Button closeButton;

    [Header("Text")]
    [SerializeField] private TMP_Text helpText;

    private void Awake()
    {
        // 버튼 이벤트는 코드로 연결. - Inspector 내 On Click 연결 XXX
        expenseHelpButton.onClick.AddListener(Open);
        closeButton.onClick.AddListener(Close);

        // 게임 시작 시 안내창 숨김.
        expenseHelpRoot.SetActive(false);
    }

    public void Open()
    {
        GameManager game = GameManager.Instance;
        ShopManager shop = ShopManager.Instance;
        LoanManager loan = LoanManager.Instance;
        EventManager events = EventManager.Instance;

        // 게임과 계산에 필요한 Manager가 준비된 경우에만 표시
        if (game == null || !game.CanAct || shop == null || loan == null || !loan.IsConfigured || events == null)
        {
            return;
        }

        // 이번 마감에 청구할 할부금과 유지비 조회.
        shop.GetCurrentMonthExpenses(out long installment, out long maintenance);

        // 이번 마감에 청구할 대출 이자와 만기 원금 조회.
        loan.GetCurrentMonthExpenses(out long interest, out long principal);

        // 지출 예정액 합산
        long total =
            System.Math.Max(0L, game.fixedExpense) +
            installment +
            maintenance +
            interest +
            principal +
            System.Math.Max(0L, events.pendingPenaltyAmount);

        // HelpText에 총액만 표시.
        helpText.text = $"총 지출금:\n{total:N0}원";

        expenseHelpRoot.SetActive(true);

        // 같은 PanelLayer의 다른 패널보다 앞에 표시
        expenseHelpRoot.transform.SetAsLastSibling();
    }

    public void Close()
    {
        expenseHelpRoot.SetActive(false);
    }

    private void OnDestroy()
    {
        // 이 스크립트에 등록한 이벤트만 해제.
        if (expenseHelpButton != null)
            expenseHelpButton.onClick.RemoveListener(Open);

        if (closeButton != null)
            closeButton.onClick.RemoveListener(Close);
    }
}
