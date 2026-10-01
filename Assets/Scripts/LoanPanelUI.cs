using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LoanPanelUI : MonoBehaviour
{
    [Header("Bank Panel")]
    [SerializeField] private AssetTradePanel bankPanel;

    [Header("Common")]
    [SerializeField] private TMP_Text summaryText;
    [SerializeField] private TMP_Text feedbackText;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private Button applyTabButton;
    [SerializeField] private Button repaymentTabButton;

    [Header("Application")]
    [SerializeField] private GameObject applicationRoot;
    [SerializeField] private Button amountButton;
    [SerializeField] private TMP_Text amountText;
    [SerializeField] private Slider amountSlider;
    [SerializeField] private TMP_Text durationText;
    [SerializeField] private Slider durationSlider;
    [SerializeField] private TMP_Text previewText;
    [SerializeField] private Button executeButton;

    [Header("Contract List")]
    [SerializeField] private GameObject repaymentRoot;
    [SerializeField] private GameObject emptyText;
    [SerializeField] private Transform loanContent;
    [SerializeField] private LoanContractRowUI rowPrefab;

    [Header("Help")]
    [SerializeField] private Button helpButton;
    [SerializeField] private GameObject helpRoot;
    [SerializeField] private TMP_Text helpText;
    [SerializeField] private Button helpCloseButton;

    [Header("Confirm")]
    [SerializeField] private GameObject confirmationRoot;
    [SerializeField] private TMP_Text confirmationText;
    [SerializeField] private Button acceptButton;
    [SerializeField] private Button denyButton;
    [SerializeField] private Button confirmationCloseButton;

    [Header("Keypad")]
    [SerializeField] private GameObject keypadRoot;
    [SerializeField] private TMP_InputField amountInput;
    [SerializeField] private TMP_Text keypadErrorText;

    private readonly List<LoanContractRowUI> rows = new List<LoanContractRowUI>();

    private long selectedAmount;
    private int selectedDuration = 1;
    private bool showingRepayment;

    private enum PendingAction
    {
        None,
        Borrow,
        Repay
    }

    private PendingAction pendingAction;
    private int pendingTurn;
    private long pendingAmount;
    private int pendingDuration;
    private int pendingContractId;

    private LoanManager Manager => LoanManager.Instance;

    private void Awake()
    {
        // Inspector On Click/On Value Changed에는 적용 x - 자동으로 적용됨 - 연결할게 너무 많음..
        closeButton.onClick.AddListener(Close);
        cancelButton.onClick.AddListener(Close);
        applyTabButton.onClick.AddListener(ShowApplication);
        repaymentTabButton.onClick.AddListener(ShowRepayment);

        amountSlider.onValueChanged.AddListener(OnAmountChanged);
        durationSlider.onValueChanged.AddListener(OnDurationChanged);
        executeButton.onClick.AddListener(RequestBorrow);

        helpButton.onClick.AddListener(OpenHelp);
        helpCloseButton.onClick.AddListener(CloseHelp);

        acceptButton.onClick.AddListener(Confirm);
        denyButton.onClick.AddListener(CloseConfirmation);
        confirmationCloseButton.onClick.AddListener(CloseConfirmation);

        amountSlider.wholeNumbers = true;
        durationSlider.wholeNumbers = true;
    }

    public void Open()
    {
        if (GameManager.Instance == null || !GameManager.Instance.CanAct)
        {
            return;
        }

        if (Manager == null || !Manager.IsConfigured)
        {
            Debug.LogError("LoanManager 설정을 확인하세요.");
            return;
        }

        gameObject.SetActive(true);
        transform.SetAsLastSibling();
    }

    private void OnEnable()
    {
        selectedAmount = 0;
        selectedDuration = 1;
        showingRepayment = false;
        feedbackText.text = "";

        helpRoot.SetActive(false);
        keypadRoot.SetActive(false);
        CloseConfirmation();

        Refresh();
    }

    private void OnDisable()
    {
        pendingAction = PendingAction.None;
    }

    public void Close()
    {
        gameObject.SetActive(false);

        // 대출 지급 또는 상환 후 은행의 현금 갱신.
        if (bankPanel != null && bankPanel.isActiveAndEnabled)
            bankPanel.Refresh();
    }

    public void Refresh()
    {
        if (Manager == null || !Manager.IsConfigured || GameManager.Instance == null)
        {
            return;
        }

        GameManager game = GameManager.Instance;
        LoanRules rules = Manager.Rules;

        // 확인 도중 턴이 바뀌거나 정산이 시작되면 이전 요청을 취소
        if (pendingAction != PendingAction.None && (pendingTurn != game.currentMonth || !game.CanAct))
        {
            CloseConfirmation();
            feedbackText.text = "상태가 변경되었습니다. 다시 선택하세요.";
        }

        summaryText.text =
            $"예금액 {Manager.DepositBalance:N0}원\n" +
            $"대출 잔액 {Manager.OutstandingPrincipal:N0}원\n" +
            $"심사용 예금 {Manager.EligibleDeposit:N0}원\n" +
            $"추가 대출 가능 {Manager.AvailableLoanAmount:N0}원";

        applicationRoot.SetActive(!showingRepayment);
        repaymentRoot.SetActive(showingRepayment);

        // 슬라이더에는 큰 금액 대신 금액 단위의 개수를 넣음.
        long maximumSteps = Manager.AvailableLoanAmount / rules.AmountStep;

        long maximumSelectable = maximumSteps * rules.AmountStep;

        selectedAmount = Math.Max(0L, Math.Min(selectedAmount, maximumSelectable));

        selectedAmount = selectedAmount / rules.AmountStep * rules.AmountStep;

        amountSlider.minValue = 0;
        amountSlider.maxValue = maximumSteps;
        amountSlider.SetValueWithoutNotify((float)(selectedAmount / rules.AmountStep));

        int maximumDuration = Manager.MaxAvailableDuration;

        if (maximumDuration > 0)
        {
            selectedDuration = Mathf.Clamp(selectedDuration, 1, maximumDuration);

            durationSlider.minValue = 1;
            durationSlider.maxValue = maximumDuration;
            durationSlider.SetValueWithoutNotify(selectedDuration);
        }
        else
        {
            selectedDuration = 0;
            durationSlider.minValue = 0;
            durationSlider.maxValue = 0;
            durationSlider.SetValueWithoutNotify(0);
        }

        bool canSelect = game.CanAct && maximumDuration > 0 && maximumSelectable >= rules.MinimumLoanAmount;

        amountSlider.interactable = canSelect;
        amountButton.interactable = canSelect;

        // 기간 선택지가 한 개이면 슬라이더 드래그는 필요하지 않음. - ex 59턴째에 대출신청
        // 기간 선택지가 한 개일 때도 해당 기간으로 신청은 가능.
        durationSlider.interactable = canSelect && maximumDuration > 1;

        amountText.text = $"{selectedAmount:N0}원";
        durationText.text = maximumDuration > 0 ? $"대출 기간: {selectedDuration}턴" : "신규 대출 불가";

        long interest = rules.CalculateInterest(selectedAmount);

        previewText.text = maximumDuration > 0
            ? $"월 금리 {rules.AnnualRate * 100m:0.##}%\n" +
              $"매월 이자 {interest:N0}원\n" +
              $"만기 {game.currentMonth + selectedDuration}턴 마감\n" +
              $"이자 총 {selectedDuration + 1}회\n" +
              $"만기 납부액 {selectedAmount + interest:N0}원"
            : "마지막 턴에는 신규 대출을 신청할 수 없습니다.";

        executeButton.interactable = Manager.CanBorrow(selectedAmount, selectedDuration, game.currentMonth, out _);

        if (showingRepayment)
            RebuildRows();
    }

    private void ShowApplication()
    {
        showingRepayment = false;
        feedbackText.text = "";
        Refresh();
    }

    private void ShowRepayment()
    {
        showingRepayment = true;
        feedbackText.text = "";
        Refresh();
    }

    private void OnAmountChanged(float value)
    {
        selectedAmount = (long)Math.Round(value) * Manager.Rules.AmountStep;

        Refresh();
    }

    private void OnDurationChanged(float value)
    {
        selectedDuration = Mathf.RoundToInt(value);
        Refresh();
    }

    private void RebuildRows()
    {
        foreach (LoanContractRowUI row in rows)
        {
            if (row == null)
                continue;

            // Destroy는 프레임 끝에 처리되므로 먼저 비활성화
            row.gameObject.SetActive(false);
            Destroy(row.gameObject);
        }

        rows.Clear();

        foreach (LoanContract contract in Manager.Contracts)
        {
            if (contract.IsClosed)
                continue;

            LoanContractRowUI row = Instantiate(rowPrefab, loanContent, false);

            row.gameObject.SetActive(true);

            row.Bind(contract, GameManager.Instance.currentMonth, RequestRepay);

            rows.Add(row);
        }

        emptyText.SetActive(rows.Count == 0);
    }

    private void RequestBorrow()
    {
        int turn = GameManager.Instance.currentMonth;

        if (!Manager.CanBorrow(selectedAmount, selectedDuration, turn, out string reason))
        {
            feedbackText.text = reason;
            Refresh();
            return;
        }

        pendingAction = PendingAction.Borrow;
        pendingTurn = turn;
        pendingAmount = selectedAmount;
        pendingDuration = selectedDuration;

        long interest = Manager.Rules.CalculateInterest(pendingAmount);

        ShowConfirmation(
            "대출을 실행하시겠습니까?\n\n" +
            $"원금 {pendingAmount:N0}원\n" +
            $"기간 {pendingDuration}턴\n" +
            $"월 이자 {interest:N0}원\n" +
            $"만기 {turn + pendingDuration}턴 마감",
            false);
    }

    private void RequestRepay(int contractId)
    {
        int turn = GameManager.Instance.currentMonth;

        if (!Manager.CanRepay(contractId, turn, out long payment, out string reason))
        {
            feedbackText.text = reason;
            return;
        }

        LoanContract contract = Manager.FindContract(contractId);

        pendingAction = PendingAction.Repay;
        pendingTurn = turn;
        pendingContractId = contractId;

        ShowConfirmation(
            $"대출 #{contractId}을 전액 상환하시겠습니까?\n\n" +
            $"원금 {contract.Principal:N0}원\n" +
            $"이번 턴 이자 {contract.GetInterestDue(turn):N0}원\n" +
            $"총 상환액 {payment:N0}원",
            false);
    }

    private void Confirm()
    {
        if (pendingAction == PendingAction.None)
            return;

        PendingAction action = pendingAction;

        // 먼저 요청을 비워 연속 클릭으로 중복 실행되지 않게끔
        pendingAction = PendingAction.None;
        acceptButton.interactable = false;

        bool success;
        string message;

        if (action == PendingAction.Borrow)
        {
            success = Manager.TryBorrow(pendingAmount, pendingDuration, pendingTurn, out message);
        }
        else
        {
            success = Manager.TryRepay(pendingContractId, pendingTurn, out message);
        }

        if (success)
            selectedAmount = 0;

        feedbackText.text = message;

        // 모든 현금/계약 변경이 끝난 뒤 화면 갱신
        if (UIManager.Instance != null)
            UIManager.Instance.RefreshUI();

        if (bankPanel != null && bankPanel.isActiveAndEnabled)
            bankPanel.Refresh();

        Refresh();

        ShowConfirmation(message, true);
    }

    private void ShowConfirmation(string text, bool noticeOnly)
    {
        confirmationText.text = text;
        acceptButton.gameObject.SetActive(!noticeOnly);
        denyButton.gameObject.SetActive(!noticeOnly);
        confirmationCloseButton.gameObject.SetActive(noticeOnly);
        acceptButton.interactable = true;

        confirmationRoot.SetActive(true);
        confirmationRoot.transform.SetAsLastSibling();
    }

    private void CloseConfirmation()
    {
        pendingAction = PendingAction.None;
        confirmationRoot.SetActive(false);
    }

    private void OpenHelp()
    {
        helpText.text = Manager.Rules.GetHelpText();
        helpRoot.SetActive(true);
        helpRoot.transform.SetAsLastSibling();
    }

    private void CloseHelp()
    {
        helpRoot.SetActive(false);
    }

    public void OpenKeypad()
    {
        GameManager game = GameManager.Instance;

        if (game == null || !game.CanAct || Manager == null || !Manager.IsConfigured)
        {
            return;
        }

        // 최신 한도와 기간을 반영한 뒤 입력창 열기
        Refresh();

        if (Manager.MaxAvailableDuration < 1 || Manager.AvailableLoanAmount < Manager.Rules.MinimumLoanAmount)
        {
            feedbackText.text = "현재 신청 가능한 대출이 없습니다.";
            return;
        }

        // 기존 자산 거래 키패드와 동일한 입력 초기화 방식
        amountInput.SetTextWithoutNotify(selectedAmount.ToString(CultureInfo.InvariantCulture));

        keypadErrorText.text = "";
        keypadRoot.SetActive(true);
        keypadRoot.transform.SetAsLastSibling();

        amountInput.ActivateInputField();
    }

    public void AppendDigit(string digit)
    {
        // Inspector에서 전달받은 문자열이 숫자 한 자리인지 검사
        if (string.IsNullOrEmpty(digit) || digit.Length != 1 || digit[0] < '0' || digit[0] > '9')
        {
            return;
        }

        string current = amountInput.text;

        if (current == "0")
            current = "";

        // long 범위를 넘는 값은 ApplyInput의 TryParse에서 거부
        if (current.Length >= 19)
            return;

        amountInput.text = current + digit;
    }

    public void Backspace()
    {
        string current = amountInput.text;

        if (current.Length > 0)
        {
            amountInput.text = current.Substring(0, current.Length - 1);
        }
    }

    public void ClearInput()
    {
        amountInput.text = "";
    }

    public void ApplyInput()
    {
        GameManager game = GameManager.Instance;

        if (game == null || !game.CanAct || Manager == null || !Manager.IsConfigured)
        {
            keypadErrorText.text = "지금은 대출 금액을 입력할 수 없습니다.";
            return;
        }

        bool valid = long.TryParse(amountInput.text, NumberStyles.None, CultureInfo.InvariantCulture, out long amount);

        if (!valid || amount <= 0)
        {
            keypadErrorText.text = "1원 이상의 정수를 입력하세요.";
            return;
        }

        // 입력 UI는 기존 키패드와 동일하지만, 금액 검증은 대출 규칙을 사용
        // 최소 금액, 입력 단위, 추가 한도, 기간을 함께 검사
        if (!Manager.CanBorrow(amount, selectedDuration, game.currentMonth, out string reason))
        {
            keypadErrorText.text = reason;
            return;
        }

        // 여기서는 신청 금액만 선택
        // 실제 대출 실행은 이후 확인창의 '예'에서 처리
        selectedAmount = amount;

        keypadRoot.SetActive(false);
        Refresh();
    }

    public void CancelInput()
    {
        // 취소 시 기존 선택 금액은 유지
        keypadRoot.SetActive(false);
    }
}
