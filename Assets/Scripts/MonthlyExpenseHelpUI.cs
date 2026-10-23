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
    [SerializeField] private TMP_Text expenseButtonText;

    [SerializeField] private Color normalTextColor = Color.black;
    [SerializeField] private Color insufficientTextColor = new Color32(200, 0, 0, 255); // R G B A

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
        
        // 게임과 계산에 필요한 Manager가 준비된 경우에만 표시
        if (game == null || !game.CanAct)
        {
            return;
        }

        if (!TryGetCurrentMonthExpense(out long total))
            return;

        // HelpText에 총액만 표시.
        helpText.text = $"총 지출금:\n{total:N0}원";

        expenseHelpRoot.SetActive(true);

        // 같은 PanelLayer의 다른 패널보다 앞에 표시
        expenseHelpRoot.transform.SetAsLastSibling();
    }

    /// <summary>
    /// 안내 패널과 버튼 색상에서 공통으로 사용할 이번달 총 지출액.
    /// 필요한 Manager가 준비되지 않았다면 false를 반환함
    /// </summary>
    /// <param name="total"></param>
    /// <returns></returns>
    private bool TryGetCurrentMonthExpense(out long total)
    {
        total = 0;

        GameManager game = GameManager.Instance;
        ShopManager shop = ShopManager.Instance;
        LoanManager loan = LoanManager.Instance;
        EventManager events = EventManager.Instance;
        HousingCostManager housing = HousingCostManager.Instance;
        RealEstateManager realEstate = RealEstateManager.Instance;

        if (game == null || shop == null || loan == null || !loan.IsConfigured || events == null || housing == null)
        {
            return false;
        }

        if (realEstate == null || !realEstate.IsInitialized)
            return false;


        if (!housing.TryGetCurrent(out HousingMonthlyCost housingCost))
            return false;

        // 유지비는 ShopManager에서 현재 턴 인플레이션을 적용해 반환.
        shop.GetCurrentMonthExpenses(out long installment, out long maintenance);

        loan.GetCurrentMonthExpenses(out long interest, out long principal);

        // 이번 달 납부할 부동산 이자와 만기 원금.
        realEstate.GetCurrentMonthExpenses(out long propertyInterest, out long propertyPrincipal);

        total = checked(
            housingCost.Total +
            installment +
            maintenance +
            interest +
            principal +
            propertyInterest + 
            propertyPrincipal + 
            System.Math.Max(0L, events.pendingPenaltyAmount));

        return true;
    }

    /// <summary>
    /// 보유 현금이 이번 달 지출보다 부족하면 버튼 글자를 빨간색으로 표시함.
    /// 현금이 다시 충분해지면 기본색으로 복원
    /// </summary>
    public void RefreshExpenseButtonColor()
    {
        if (expenseButtonText == null)
            return;
        
        if (!TryGetCurrentMonthExpense(out long total))
        {
            expenseButtonText.color = normalTextColor;
            return;
        }

        bool isInsufficient = GameManager.Instance.availableCash < total;

        expenseButtonText.color = isInsufficient ? insufficientTextColor : normalTextColor;
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
