using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RealEstatePanelUI : MonoBehaviour
{
    [Header("List")]
    [SerializeField] private GameObject root;
    [SerializeField] private TMP_Text cashText;
    [SerializeField] private GameObject emptyText;
    [SerializeField] private Transform content;
    [SerializeField] private RealEstateRowUI rowPrefab;
    [SerializeField] private Sprite[] icons;

    [Header("Detail Common")]
    [SerializeField] private GameObject detailRoot;
    [SerializeField] private ScrollRect detailScroll;
    [SerializeField] private Image propertyImage;
    [SerializeField] private TMP_Text propertyNameText;
    [SerializeField] private TMP_Text priceValueText;
    [SerializeField] private TMP_Text rentValueText;
    [SerializeField] private TMP_Text cashValueText;

    [Header("Purchase & Loan")]
    [SerializeField] private GameObject purchaseSection;
    [SerializeField] private GameObject loanSection;
    [SerializeField] private Button loanModeButton;
    [SerializeField] private TMP_Text amountText;
    [SerializeField] private Slider amountSlider;
    [SerializeField] private TMP_Text durationText;
    [SerializeField] private Slider durationSlider;
    [SerializeField] private TMP_Text paymentValueText;
    [SerializeField] private TMP_Text afterPurchaseCashText;

    [Header("Owner")]
    [SerializeField] private GameObject ownershipSection;
    [SerializeField] private TMP_Text ownershipText;
    [SerializeField] private TMP_Text paymentSummaryText;

    [Header("ActionButton")]
    [SerializeField] private Button actionButton;
    [SerializeField] private TMP_Text actionButtonText;

    [Header("Dialog Panel")]
    [SerializeField] private PropertyDialogUI dialog;

    [Header("Keypad Panel")]
    [SerializeField] private GameObject keypadRoot;
    [SerializeField] private TMP_InputField amountInput;
    [SerializeField] private TMP_Text keypadErrorText;

    private enum DetailAction
    {
        Purchase,
        Repay,
        RequestSale,
        CancelSale
    }

    private readonly List<RealEstateRowUI> rows = new();

    private DetailAction currentAction;
    private bool ownedOnly;
    private bool useLoan;

    private int selectedIndex = -1;
    private int duration = 1;
    private long selectedLoan;

    private RealEstateManager Manager => RealEstateManager.Instance;
    private GameManager Game => GameManager.Instance;

    private bool CanUse =>
        Manager != null &&
        Manager.IsInitialized &&
        Game != null &&
        Game.CanAct &&
        TutorialManager.IsUnlocked(TutorialFeature.RealEstate);

    private bool HasSelection =>
        Manager != null &&
        Manager.IsInitialized &&
        selectedIndex >= 0 &&
        selectedIndex < Manager.Count;

    private void Awake()
    {
        root.SetActive(false);
        detailRoot.SetActive(false);
        keypadRoot.SetActive(false);

        amountSlider.wholeNumbers = true;
        durationSlider.wholeNumbers = true;
    }

    public void Open()
    {
        if (!CanUse)
            return;

        ownedOnly = false;

        root.SetActive(true);
        root.transform.SetAsLastSibling();

        RefreshList();
    }

    public void Close()
    {
        CloseDetail();
        root.SetActive(false);
    }

    public void ShowMarket()
    {
        if (!CanUse)
            return;

        ownedOnly = false;
        RefreshList();
    }

    public void ShowOwned()
    {
        if (!CanUse)
            return;

        ownedOnly = true;
        RefreshList();
    }

    private Sprite GetIcon(int index)
    {
        return icons != null && index < icons.Length
            ? icons[index]
            : null;
    }

    private void RefreshList()
    {
        if (Manager == null || !Manager.IsInitialized)
            return;

        cashText.text = $"보유 현금 : {Game.availableCash:N0}원";

        foreach (RealEstateRowUI row in rows)
        {
            // 레이아웃에서 제외.
            row.gameObject.SetActive(false);
            Destroy(row.gameObject);
        }

        rows.Clear();

        for (int index = 0; index < Manager.Count; index++)
        {
            if (ownedOnly && !Manager.Property(index).owned)
                continue;

            RealEstateRowUI row = Instantiate(rowPrefab, content);
            row.Bind(index, GetIcon(index), OpenDetail);
            rows.Add(row);
        }

        emptyText.SetActive(rows.Count == 0);
    }

    private void OpenDetail(int index)
    {
        if (!CanUse)
            return;

        selectedIndex = index;
        useLoan = false;
        selectedLoan = 0;
        duration = 1;

        detailRoot.SetActive(true);
        detailRoot.transform.SetAsLastSibling();

        RefreshDetail();

        // 새로운 상품을 열 때만 상세 스크롤을 맨 위로 이동.
        // 값이 갱신될 때마다 이동시키면 유저가 내린 스크롤이 되돌아감.
        Canvas.ForceUpdateCanvases();
        detailScroll.StopMovement();
        detailScroll.verticalNormalizedPosition = 1f;
    }

    public void CloseDetail()
    {
        selectedIndex = -1;

        keypadRoot.SetActive(false);
        detailRoot.SetActive(false);
    }

    private void RefreshDetail()
    {
        if (!HasSelection)
            return;

        RealEstateDefinition definition = Manager.Definition(selectedIndex);
        RealEstatePropertyState state = Manager.Property(selectedIndex);

        propertyImage.sprite = GetIcon(selectedIndex);
        propertyImage.enabled = propertyImage.sprite != null;
        propertyNameText.text = definition.name;

        priceValueText.text = $"{Manager.Price(selectedIndex):N0}원";
        rentValueText.text =
            $"{Manager.Rent(selectedIndex, Game.currentMonth):N0}원";
        cashValueText.text = $"{Game.availableCash:N0}원";

        purchaseSection.SetActive(!state.owned);
        ownershipSection.SetActive(state.owned);

        if (state.owned)
            RefreshOwnership(state);
        else
            RefreshPurchase();
    }

    /// <summary>
    /// Action 버튼 하나에 역할, 문구, 사용 가능 여부를 지정.
    /// </summary>
    /// <param name="action"></param>
    /// <param name="label"></param>
    /// <param name="allowed"></param>
    private void SetAction(DetailAction action, string label, bool allowed)
    {
        currentAction = action;
        actionButtonText.text = label;
        actionButton.interactable = CanUse && allowed;
    }

    private void RefreshPurchase()
    {
        long limit = Manager.LoanLimit(selectedIndex);
        long step = Manager.LoanRules.AmountStep;

        bool canBorrow =
            Manager.MaxDuration >= 1 &&
            limit >= Manager.LoanRules.MinimumLoanAmount;

        loanModeButton.interactable = CanUse && canBorrow;

        if (!canBorrow)
            useLoan = false;

        selectedLoan = useLoan ? Math.Max(0L, Math.Min(selectedLoan, limit)) : 0L;

        // 대출 입력 단위에 맞춤.
        selectedLoan = selectedLoan / step * step;

        duration = Mathf.Clamp(duration, 1, Mathf.Max(1, Manager.MaxDuration));

        loanSection.SetActive(useLoan);

        // 슬라이더의 값은 금액 자체가 아닌 '입력 단위의 개수'.
        amountSlider.minValue = 0;
        amountSlider.maxValue = Mathf.Max(1f, (float)(limit / step));
        amountSlider.SetValueWithoutNotify((float)(selectedLoan / step));

        durationSlider.minValue = 1;
        durationSlider.maxValue = Mathf.Max(1, Manager.MaxDuration);
        durationSlider.SetValueWithoutNotify(duration);

        amountText.text = $"{selectedLoan:N0}원";
        durationText.text = $"{duration}개월";

        long payment = Manager.Price(selectedIndex) - selectedLoan;

        paymentValueText.text = $"{payment:N0}원";
        afterPurchaseCashText.text = $"{Game.availableCash - payment:N0}원";

        if (selectedLoan > 0)
        {
            // 대출 계약과 같은 계산 클래스로 이자를 미리 보여줌.
            var preview = new LoanContract(
                0,
                selectedLoan,
                Game.currentMonth,
                duration,
                Manager.LoanRules.AnnualRate);

            paymentSummaryText.text =
                $"연 이자율 : {preview.AnnualRate:P1}\n" +
                $"월 이자 : {preview.MonthlyInterest:N0}원\n" +
                $"만기 : {preview.MaturityTurn}턴\n" +
                $"이번 달부터 만기 달까지 이자 {duration + 1}회 납부";
        }
        else
        {
            paymentSummaryText.text = "매입 금액 전액을 보유 현금으로 지출합니다.";
        }

        string error = Manager.BuyError(
            selectedIndex,
            selectedLoan,
            duration,
            Game.currentMonth);

        SetAction(DetailAction.Purchase, "매입하기", error == null);

        if (error != null)
            paymentSummaryText.text += $"\n{error}";
    }

    private void RefreshOwnership(RealEstatePropertyState state)
    {
        ownershipText.text = $"매입 가격 : {state.purchasePrice:N0}원";

        if (state.saleRequested)
        {
            ownershipText.text += "\n남은 부동산 대출이 없습니다.";

            paymentSummaryText.text =
                "매각 대기 중입니다.\n" +
                "매각되는 턴의 시세로 대금이 지급됩니다.\n" +
                "대기 중에도 임대료가 지급됩니다.";

            SetAction(DetailAction.CancelSale, "매각 취소", true);
            return;
        }

        LoanContract loan = Manager.GetLoan(selectedIndex);

        if (loan != null)
        {
            long interest = loan.GetInterestDue(Game.currentMonth);
            long requiredCash = checked(loan.Principal + interest);

            ownershipText.text +=
                $"\n남은 대출 원금 : {loan.Principal:N0}원" +
                $"\n이번 달 이자 : {interest:N0}원" +
                $"\n만기 : {loan.MaturityTurn}턴" +
                $"\n전액 상환 금액 : {requiredCash:N0}원";

            paymentSummaryText.text =
                "대출을 전액 상환하면 매각을 신청할 수 있습니다.";

            bool enoughCash = Game.availableCash >= requiredCash;

            if (!enoughCash)
            {
                paymentSummaryText.text += $"\n상환에 필요한 현금이 " + $"{requiredCash - Game.availableCash:N0}원 부족합니다.";
            }

            SetAction(
                DetailAction.Repay,
                "대출 전액 상환",
                enoughCash);

            return;
        }

        ownershipText.text += "\n남은 부동산 대출이 없습니다.";

        bool canRequestSale = Game.currentMonth < 60;

        paymentSummaryText.text = canRequestSale
            ? "최초 매각 신청 시 1~3턴의 대기 기간이 결정됩니다.\n" +
              "실제 매각되는 턴의 시세가 적용됩니다."
            : "마지막 턴에는 새 매각을 신청할 수 없습니다.";

        SetAction(
            DetailAction.RequestSale,
            "매각 신청",
            canRequestSale);
    }

    public void SelectCashMode()
    {
        if (!CanUse || !HasSelection)
            return;

        useLoan = false;
        selectedLoan = 0;

        RefreshDetail();
    }

    public void SelectLoanMode()
    {
        if (!CanUse || !HasSelection || Manager.MaxDuration < 1)
            return;

        long limit = Manager.LoanLimit(selectedIndex);

        if (limit < Manager.LoanRules.MinimumLoanAmount)
            return;

        useLoan = true;
        selectedLoan = limit;

        RefreshDetail();
    }

    public void OnAmountChanged(float value)
    {
        if (!CanUse || !HasSelection || !useLoan)
            return;

        selectedLoan =
            (long)Mathf.RoundToInt(value) * Manager.LoanRules.AmountStep;

        RefreshDetail();
    }

    public void OnDurationChanged(float value)
    {
        if (!CanUse || !HasSelection || !useLoan)
            return;

        duration = Mathf.RoundToInt(value);
        RefreshDetail();
    }

    public void OnClickAction()
    {
        if (!CanUse || !HasSelection)
            return;

        // 클릭 직전의 소유권, 대출, 현금으로 다시 판단.
        RefreshDetail();

        if (!actionButton.interactable)
            return;

        switch (currentAction)
        {
            case DetailAction.Purchase:
                RequestPurchase();
                break;

            case DetailAction.Repay:
                RequestRepayment();
                break;

            case DetailAction.RequestSale:
                RequestSale();
                break;

            case DetailAction.CancelSale:
                RequestCancelSale();
                break;
        }
    }

    private void ConfirmAction(string message, Func<string> action)
    {
        dialog.Show(message, () =>
        {
            // 거래 및 최종 검사는 Manager가 수행.
            // 반환값이 null이면 성공, 문자열이면 실패 사유.
            string error = action();

            RefreshList();
            RefreshDetail();

            dialog.Show(
                error ?? "처리가 완료되었습니다.",
                null,
                false);
        }, true);
    }

    private void RequestPurchase()
    {
        int index = selectedIndex;
        int turn = Game.currentMonth;
        int months = duration;
        long loanAmount = useLoan ? selectedLoan : 0L;
        long price = Manager.Price(index);

        // 확인창을 연 시점의 선택값을 캡처.
        string message =
            $"{Manager.Definition(index).name}을 매입하시겠습니까?\n\n" +
            $"매입 가격 : {price:N0}원\n" +
            $"대출 금액 : {loanAmount:N0}원\n" +
            $"지출 현금 : {price - loanAmount:N0}원";

        ConfirmAction(message, () => Manager.Buy(index, loanAmount, months, turn));
    }

    private void RequestRepayment()
    {
        int index = selectedIndex;
        int turn = Game.currentMonth;

        LoanContract loan = Manager.GetLoan(index);
        if (loan == null)
            return;

        long interest = loan.GetInterestDue(turn);

        string message =
            "부동산 대출을 전액 상환하시겠습니까?\n\n" +
            $"원금 : {loan.Principal:N0}원\n" +
            $"이번 달 이자 : {interest:N0}원\n" +
            $"합계 : {loan.Principal + interest:N0}원";

        // 상환만 수행. 상환 이후 매각을 자동 신청하지 않음.
        ConfirmAction(message, () => Manager.Repay(index, turn));
    }

    private void RequestSale()
    {
        int index = selectedIndex;
        int turn = Game.currentMonth;

        ConfirmAction(
            "매각을 신청하시겠습니까?\n\n" +
            "매각되는 턴의 시세로 대금이 지급됩니다.\n" +
            "게임 종료까지 매각되지 않으면 보유 자산으로 남습니다.",
            () => Manager.RequestSale(index, turn));
    }

    private void RequestCancelSale()
    {
        int index = selectedIndex;
        int turn = Game.currentMonth;

        ConfirmAction(
            "매각 신청을 취소하시겠습니까?\n\n" +
            "재신청해도 남은 대기 기간은 다시 추첨하지 않습니다.",
            () => Manager.CancelSale(index, turn));
    }

    public void OpenKeypad()
    {
        if (!CanUse || !HasSelection || !useLoan)
            return;

        amountInput.text =
            selectedLoan.ToString(CultureInfo.InvariantCulture);

        keypadErrorText.text = "";

        keypadRoot.SetActive(true);
        keypadRoot.transform.SetAsLastSibling();
    }

    public void AppendDigit(string digit)
    {
        if (!keypadRoot.activeSelf ||
            string.IsNullOrEmpty(digit) ||
            digit.Length != 1 ||
            digit[0] < '0' ||
            digit[0] > '9')
        {
            return;
        }

        string current = amountInput.text;

        if (current == "0")
            current = "";

        if (current.Length < 19)
            amountInput.text = current + digit;
    }

    public void Backspace()
    {
        string current = amountInput.text;

        if (current.Length > 0)
        {
            amountInput.text =
                current.Substring(0, current.Length - 1);
        }
    }

    public void ClearInput()
    {
        amountInput.text = "";
    }

    public void ApplyInput()
    {
        if (!CanUse || !HasSelection || !useLoan)
        {
            CancelInput();
            return;
        }

        bool parsed = long.TryParse(
            amountInput.text,
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out long amount);

        if (!parsed || amount < 0)
        {
            keypadErrorText.text = "올바른 금액을 입력하세요.";
            return;
        }

        long limit = Manager.LoanLimit(selectedIndex);
        long step = Manager.LoanRules.AmountStep;

        if (amount > limit)
        {
            keypadErrorText.text = $"대출 한도는 {limit:N0}원입니다.";
            return;
        }

        if (amount % step != 0 || (amount > 0 && amount < Manager.LoanRules.MinimumLoanAmount))
        {
            keypadErrorText.text = $"{step:N0}원 단위로 입력하세요.\n" + $"최소 대출 금액 : " + $"{Manager.LoanRules.MinimumLoanAmount:N0}원";

            return;
        }

        selectedLoan = amount;
        keypadRoot.SetActive(false);

        RefreshDetail();
    }

    public void CancelInput()
    {
        keypadRoot.SetActive(false);
    }
}
