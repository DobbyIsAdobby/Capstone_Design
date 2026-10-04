using System;
using System.Collections.Generic;
using UnityEngine;

public class LoanManager : Singleton<LoanManager>
{
    [SerializeField] private LoanRules rules;   //Scriptable Obj

    private readonly List<LoanContract> contracts = new List<LoanContract>();   // 대출 계약

    private int nextContractId = 1;     // 계약 건수마다 ++됨.
    private int lastSettlementTurn = -1;

    public LoanRules Rules => rules;
    public bool IsConfigured { get; private set; }

    // 외부에는 목록 수정 기능을 제공하지 않음 - IReadOnlyList 인터페이스 사용
    public IReadOnlyList<LoanContract> Contracts => contracts.AsReadOnly();

    protected override void OnSingletonAwake()
    {
        if (rules == null)
        {
            Debug.LogError("LoanManager에 LoanRules를 연결하세요.", this);
            return;
        }

        IsConfigured = rules.Validate(out string error);

        if (!IsConfigured)
            Debug.LogError(error, this);
    }

    public long OutstandingPrincipal
    {
        get
        {
            long total = 0;

            foreach (LoanContract contract in contracts)
            {
                if (!contract.IsClosed)
                    total += contract.Principal;
            }

            return total;
        }
    }

    public long DepositBalance => AssetManager.Instance != null ? AssetManager.Instance.bankBalance : 0L;

    // 대출금을 다시 예금해서 대출 구간을 상승시키지 못하게 막음
    public long EligibleDeposit => Math.Max(0L, DepositBalance - OutstandingPrincipal);

    public long TotalLoanLimit => IsConfigured ? rules.GetLimit(EligibleDeposit) : 0L;

    public long AvailableLoanAmount => Math.Max(0L, TotalLoanLimit - OutstandingPrincipal);

    public int MaxAvailableDuration
    {
        get
        {
            GameManager game = GameManager.Instance;

            if (!IsConfigured || game == null)
                return 0;

            return Math.Max(0, Math.Min(rules.MaxDuration, game.maxMonth - game.currentMonth));
        }
    }

    /// <summary>
    /// 대출 내역(건수) 확인
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public LoanContract FindContract(int id)
    {
        foreach (LoanContract contract in contracts)
        {
            if (contract.Id == id)
                return contract;
        }

        return null;
    }

    /// <summary>
    /// 대출 시스템 사용 가능 여부
    /// </summary>
    /// <param name="requestedTurn"></param>
    /// <param name="reason"></param>
    /// <returns></returns>
    private bool CanOperate(int requestedTurn, out string reason)
    {
        reason = "";
        GameManager game = GameManager.Instance;

        if (!IsConfigured || AssetManager.Instance == null)
        {
            reason = "대출 시스템 설정을 확인하세요.";
            return false;
        }

        if (game == null || !game.CanAct)
        {
            reason = "지금은 대출 업무를 이용할 수 없습니다.";
            return false;
        }

        if (requestedTurn != game.currentMonth)
        {
            reason = "턴이 변경되었습니다. 다시 선택하세요.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// 대출 가능 여부 확인
    /// </summary>
    /// <param name="amount"></param>
    /// <param name="duration"></param>
    /// <param name="requestedTurn"></param>
    /// <param name="reason"></param>
    /// <returns></returns>
    public bool CanBorrow(long amount, int duration, int requestedTurn, out string reason)
    {
        if (!CanOperate(requestedTurn, out reason))
            return false;

        if (MaxAvailableDuration < 1)
        {
            reason = "마지막 턴에는 신규 대출이 불가능합니다.";
            return false;
        }

        if (duration < 1 || duration > MaxAvailableDuration)
        {
            reason = "선택할 수 없는 대출 기간입니다.";
            return false;
        }

        if (amount < rules.MinimumLoanAmount || amount % rules.AmountStep != 0)
        {
            reason = $"최소 {rules.MinimumLoanAmount:N0}원, " + $"{rules.AmountStep:N0}원 단위로 입력하세요.";
            return false;
        }

        if (amount > AvailableLoanAmount)
        {
            reason = "추가 대출 가능액을 초과했습니다.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// 대출 진행
    /// </summary>
    /// <param name="amount"></param>
    /// <param name="duration"></param>
    /// <param name="requestedTurn"></param>
    /// <param name="message"></param>
    /// <returns></returns>
    public bool TryBorrow(long amount, int duration, int requestedTurn, out string message)
    {
        // 확인창을 연 뒤 조건이 변경됐을 수 있음 - 검사 시행
        if (!CanBorrow(amount, duration, requestedTurn, out message))
        {
            return false;
        }

        GameManager game = GameManager.Instance;

        var contract = new LoanContract(nextContractId++, amount, game.currentMonth, duration, rules.AnnualRate);

        // 계약과 현금을 모두 변경한 뒤 호출 측에서 화면 갱신
        contracts.Add(contract);

        game.ApplyCashChange(amount, "대출금 입금", ReceiptLineType.Financing);

        message = $"대출 #{contract.Id} 실행 완료\n" + $"{amount:N0}원 지급\n" + $"만기: {contract.MaturityTurn}턴 마감";

        return true;
    }

    /// <summary>
    /// 중도 상환 가능 여부 확인
    /// </summary>
    /// <param name="contractId"></param>
    /// <param name="requestedTurn"></param>
    /// <param name="payment"></param>
    /// <param name="reason"></param>
    /// <returns></returns>
    public bool CanRepay(int contractId, int requestedTurn, out long payment, out string reason)
    {
        payment = 0;

        if (!CanOperate(requestedTurn, out reason))
            return false;

        LoanContract contract = FindContract(contractId);

        if (contract == null || contract.IsClosed)
        {
            reason = "이미 종료되었거나 존재하지 않는 계약입니다.";
            return false;
        }

        payment = contract.Principal + contract.GetInterestDue(requestedTurn);

        if (GameManager.Instance.availableCash < payment)
        {
            reason = $"상환에 필요한 현금이 부족합니다.\n" + $"필요 금액: {payment:N0}원";

            return false;
        }

        return true;
    }

    /// <summary>
    /// 중도 상환 실행
    /// </summary>
    /// <param name="contractId"></param>
    /// <param name="requestedTurn"></param>
    /// <param name="message"></param>
    /// <returns></returns>
    public bool TryRepay(int contractId, int requestedTurn, out string message)
    {
        if (!CanRepay(contractId, requestedTurn, out _, out message))
        {
            return false;
        }

        GameManager game = GameManager.Instance;
        LoanContract contract = FindContract(contractId);

        long interest = contract.GetInterestDue(requestedTurn);

        // 자발적인 상환은 현금이 충분한 경우에만
        game.ApplyCashChange(-contract.Principal, "대출 원금", ReceiptLineType.Financing);

        if (interest > 0)
        {
            game.ApplyCashChange(-interest, "대출 이자", ReceiptLineType.FixedExpense);
        }

        // 종료된 계약은 이후 월말 청구 대상에서 제외됨.
        contract.MarkInterestPaid(requestedTurn);
        contract.Close(requestedTurn);

        message = $"대출 #{contract.Id} 전액 상환 완료";
        return true;
    }

    /// <summary>
    /// 월 정산 처리
    /// </summary>
    /// <param name="turn"></param>
    public void ProcessMonthlySettlement(int turn)
    {
        GameManager game = GameManager.Instance;

        if (!IsConfigured || game == null || !game.IsSetting || game.IsGameOver || game.currentMonth != turn)
        {
            return;
        }

        // 동일 턴의 중복 청구를 막음
        if (turn <= lastSettlementTurn)
            return;

        foreach (LoanContract contract in contracts)
        {
            if (contract.IsClosed || turn < contract.StartTurn)
                continue;

            if (contract.LastInterestPaidTurn < turn)
            {
                game.ApplyMandatoryExpense(contract.MonthlyInterest, "대출 이자", ReceiptLineType.FixedExpense);
                contract.MarkInterestPaid(turn);
            }

            if (turn >= contract.MaturityTurn)
            {
                // 현금이 부족해도 원금 전액을 청구
                // 파산 판정은 모든 계약을 처리한 다음 GameManager에서 진행
                game.ApplyMandatoryExpense(contract.Principal, "대출 원금", ReceiptLineType.Financing);

                // 부족액은 이미 마이너스 현금으로 포함됨
                // 원금을 활성 채무에도 남겨 두면 이중 차감됨
                contract.Close(turn);
            }
        }

        lastSettlementTurn = turn;
    }

    /// <summary>
    /// 현재 턴 마감에 청구할 대출 이자와 만기 원금을 조회.
    /// 납부 완료 처리나 계약 종료 처리는 하지 않음.
    /// </summary>
    /// <param name="interestTotal"></param>
    /// <param name="principalTotal"></param>
    public void GetCurrentMonthExpenses(out long interestTotal, out long principalTotal)
    {
        interestTotal = 0;
        principalTotal = 0;

        GameManager game = GameManager.Instance;

        if (game == null || !IsConfigured)
            return;

        int turn = game.currentMonth;

        if (turn <= lastSettlementTurn)
            return;

        foreach (LoanContract contract in contracts)
        {
            // 중도 상환 등으로 종료된 계약은 제외
            if (contract.IsClosed || turn < contract.StartTurn)
                continue;

            // 계약에 저장된 이자를 사용
            // 여기서 연이율을 다시 계산하면 반올림 등이 달라질 수 있음
            interestTotal += contract.GetInterestDue(turn);

            // 이번 턴 마감에 만기라면 원금 전액도 현금으로 필요함
            if (turn >= contract.MaturityTurn)
            {
                principalTotal += contract.Principal;
            }
        }
    }

    public LoanSaveData CaptureSave()
    {
        var data = new LoanSaveData
        {
            nextContractId = nextContractId,
            lastSettlementTurn = lastSettlementTurn
        };

        foreach (LoanContract contract in contracts)
        {
            data.contracts.Add(new LoanContractSaveData
            {
                id = contract.Id,
                principal = contract.Principal,
                startTurn = contract.StartTurn,
                duration = contract.Duration,
                annualRate = SaveNumber.Write(contract.AnnualRate),
                lastInterestPaidTurn = contract.LastInterestPaidTurn,
                isClosed = contract.IsClosed,
                closedTurn = contract.ClosedTurn
            });
        }

        return data;
    }

    public void RestoreSave(LoanSaveData data)
    {
        var restored = new List<LoanContract>();

        foreach (LoanContractSaveData saved in data.contracts)
        {
            var contract = new LoanContract(
                saved.id,
                saved.principal,
                saved.startTurn,
                saved.duration,
                SaveNumber.Read(saved.annualRate));

            contract.MarkInterestPaid(saved.lastInterestPaidTurn);

            if (saved.isClosed)
                contract.Close(saved.closedTurn);

            restored.Add(contract);
        }

        contracts.Clear();
        contracts.AddRange(restored);

        nextContractId = data.nextContractId;
        lastSettlementTurn = data.lastSettlementTurn;
    }
}
