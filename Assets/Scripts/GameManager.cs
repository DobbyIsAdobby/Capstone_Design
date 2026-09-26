using System.Collections.Generic;
using UnityEngine;

public class GameManager : Singleton<GameManager>
{
    /*
    Inspector Zone
    */
    //public static GameManager Instance;

    [Header("Game Time")]
    public int currentMonth = 1;
    public int maxMonth = 120; // 총 10년 (120턴)

    [Header("Player Status")]
    public long availableCash = 5000000; // 초기 자본금 500만 원
    public float stressLevel = 0f; // 스트레스 지수
    public long TotalAsset //총 자산은 투자 자산과 현재 현금에 따라 변동되므로 프로퍼티로 지정.
    {
        get
        {
            // 가용 현금 + (AssetManager에서 가져온 투자 자산 평가액 총합) => 총 보유 자산
            long investedValue = 0;
            if(AssetManager.Instance != null)
            {
                investedValue = AssetManager.Instance.GetTotalInvestedValue();
            }

            return availableCash + investedValue;
        }
    }
    public int currentMonthOvertimeCount = 0; // 이번 달 야근 횟수
    public readonly int maxOvertimePerMonth = 30; // 한 달 최대 야근 가능 횟수(불변성 적용)

    [Header("Stress")]
    [SerializeField, Min(1)]
    private float baseMaxStress = 100f;
    public float MaxStress => baseMaxStress + (ShopManager.Instance != null ? ShopManager.Instance.MaxStressBonus : 0f);

    [Header("Income & Expense")]
    public long monthlySalary = 3000000;
    public long fixedExpense = 1500000; // 고정 지출 (월세, 생활비 등) => 추후 인플레이션에 따른 턴마다 증액 함수 필요.

    [Header("Monthly Receipt")]
    [SerializeField] private MonthlyReceiptPanel receiptPanel;
    private readonly List<ReceiptLine> monthlyLines = new List<ReceiptLine>();
    
    private bool hasMonthBaseLine;

    /*
    function Zone
    */

    // 싱글톤 패턴
    /*private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }*/

    // 자동 구현 프로퍼티
    /// <summary>
    /// 게임 오버가 됐는지 확인. 기본값 : false
    /// </summary>
    public bool IsGameOver{ get; private set; }
    /// <summary>
    /// 파산 여부. 일반적 게임 종료와 구분
    /// </summary>
    public bool IsBankrupt { get; private set;}
    /// <summary>
    /// 결과창에서 사용할 파산 사유
    /// </summary>
    public string BankruptcyCause { get; private set; } = "";
    /// <summary>
    /// 미납금액은 마이너스된 현금을 양수로 표시한 값
    /// </summary>
    public long UnpaidAmount => availableCash < 0 ? -availableCash : 0;
    /// <summary>
    /// 정산 중 처음으로 현금 부족을 발생시킨 지출.
    /// </summary>
    private string pendingBankruptcyCause = "";
    /// <summary>
    /// 세팅을 진행해도 되는지 확인. 기본값 : false
    /// </summary>
    public bool IsSetting{ get; private set; }
    /// <summary>
    /// 초기 자산 설정이 끝난 후를 월 시작 기준으로, 게임 오버 여부에 따라 행동 제약을 걸음.
    /// </summary>
    public bool CanAct => hasMonthBaseLine && !IsGameOver && !IsSetting;
    /// <summary>
    /// 총 자산 확인
    /// </summary>
    public long MonthStartTotalAsset { get; private set; }

    private void Start()
    {
        BeginMonthRecord();
    }

    /// <summary>
    /// 지난 턴 영수증 내역 초기화 및 총 자산 업데이트를 진행하는 함수
    /// </summary>
    private void BeginMonthRecord()
    {
        monthlyLines.Clear();
        MonthStartTotalAsset = TotalAsset;
        hasMonthBaseLine = true;
    }

    /// <summary>
    /// 실제 현금 변경과 해당 내역 기록을 함께 처리. 지불 가능 여부는 호출한 행동에서 먼저 검사 진행.
    /// </summary>
    /// <param name="amount"></param>
    /// <param name="label"></param>
    /// <param name="type"></param>
    public void ApplyCashChange(long amount, string label, ReceiptLineType type = ReceiptLineType.Change)
    {
        availableCash += amount;
        RecordMonthlyChange(label, amount, type);
    }

    /// <summary>
    /// 생활비, 할부금, 유지비, 이벤트, 병원비용 잔액 검증 함수
    /// </summary>
    /// <param name="amount"></param>
    /// <param name="label"></param>
    /// <param name="type"></param>
    public void ApplyMandatoryExpense(long amount, string label, ReceiptLineType type = ReceiptLineType.Change)
    {
        if (amount <= 0)
            return;

        // 잔액이 부족해도 비용 전액 차감 및 청구서 기록.
        ApplyCashChange(-amount, label, type);

        // 여기서는 파산을 확정하지 않고 원인만 보관.
        if (availableCash < 0 &&
            string.IsNullOrEmpty(pendingBankruptcyCause))
        {
            pendingBankruptcyCause = label;
        }
    }

    // 정산이 끝난 뒤 파산 확인용 함수
    private void ResolveBankruptcyAfterSettlement()
    {
        if (availableCash >= 0)
        {
            pendingBankruptcyCause = "";
            return;
        }

        string cause = string.IsNullOrEmpty(pendingBankruptcyCause) ? "필수 지출 정산" : pendingBankruptcyCause;

        TriggerBankruptcy(cause);
    }

    /// <summary>
    /// 평가손익처럼 현금 이동 없이 자산이 변할 때도 사용.
    /// </summary>
    /// <param name="label"></param>
    /// <param name="amount"></param>
    /// <param name="type"></param>
    public void RecordMonthlyChange(string label, long amount, ReceiptLineType type = ReceiptLineType.Change)
    {
        if(amount == 0 && type == ReceiptLineType.Change)
        {
            return;
        }

        // 동일 내역은 월 합계로 묶기
        for(int i = 0; i < monthlyLines.Count; i++)
        {
            ReceiptLine previous = monthlyLines[i];

            if(previous.Name == label && previous.Type == type)
            {
                monthlyLines[i] = new ReceiptLine(label, previous.Amount + amount, type);
                return;
            }
        }

        monthlyLines.Add(new ReceiptLine(label, amount, type));
    }

    /// <summary>
    /// UI [턴 종료] 버튼에 연결할 함수 - 게임 루프 변경으로 인한(현재 턴 청구서(영수증) 출력 이후 다음달 이동 가능) 로직 전면 개편. - 청구서 영수증 혼용해서 쓸거같음 코드 볼때 헷갈리지 마세요
    /// </summary>
    public void OnClickNextMonth()
    {
        // 프로토타이핑 용 임시 로직
        /*if (currentMonth >= maxMonth)
        {
            TriggerEnding();
            return;
        }

        ProcessMonthlySettlement();
        currentMonth++;
        UpdateUI();*/

        if(!CanAct) return;

        if(receiptPanel == null || !receiptPanel.IsConfigured)
        {
            Debug.LogError("청구서 panel의 Inspector가 연결됐는지 확인하세요.");
            return;
        }

        IsSetting = true;

        ProcessMonthlySettlement();

        // 고정지출을 먼저 표시하고 나머지는 기록 순서에 따라 출력
        List<ReceiptLine> orderedLines = new List<ReceiptLine>();

        foreach(ReceiptLine line in monthlyLines)
        {
            if(line.Type == ReceiptLineType.FixedExpense)
            {
                orderedLines.Add(line);
            }
        }

        foreach(ReceiptLine line in monthlyLines)
        {
            if(line.Type != ReceiptLineType.FixedExpense)
            {
                orderedLines.Add(line);
            }
        }

        long totalAfterSettlement = TotalAsset;
        long assetChange = totalAfterSettlement - MonthStartTotalAsset;

        long recordedChange = 0;

        foreach(ReceiptLine line in orderedLines)
        {
            recordedChange += line.Amount;
        }

        if(recordedChange != assetChange)
        {
            Debug.LogWarning($"월별 기록 불일치 : 내역 합계 {recordedChange:N0}원 / " + $"실제 자산 증감 {assetChange:N0}원");
        }

        MonthlyReceiptData result = new MonthlyReceiptData(orderedLines, assetChange, totalAfterSettlement, UnpaidAmount, IsBankrupt, currentMonth >= maxMonth);

        receiptPanel.Show(result, CompleteMonthReceipt);
        UpdateUI();

        // 현재 함수에서는 IsSetting을 헤제하거나 다음 턴으로 더이상 넘어가지 않음.
    }

    // 수익률을 다시 계산하지 않고 기존 계산 전후 잔고 차이를 기록하도록 수정.
    private void ProcessMonthlySettlement()
    {
        /*
        // 1. 기본 수입 및 지출 정산
        availableCash += monthlySalary - fixedExpense;

        // 2. 자산 수익률 정산
        AssetManager.Instance.CalculateMonthlyReturns();

        // 3. 턴 기반 이벤트 및 마진콜(게임오버) 체크
        EventManager.Instance.ResolvePendingPenalty();

        if(IsGameOver) return;

        // 마지막 달에는 다음 달에 납부할 청구서를 생성하지 않음
        if(currentMonth < maxMonth)
        {
            EventManager.Instance.CheckMonthlyEvent(currentMonth);
        }*/

        pendingBankruptcyCause = "";

        // 급여 지급
        ApplyCashChange(monthlySalary, "급여");

        // 자산 수익률 계산
        AssetManager assets = AssetManager.Instance;

        long bankBefore = assets.bankBalance;
        long stockBefore = assets.stockBalance;
        long leverageBefore = assets.leverageBalance;

        assets.CalculateMonthlyReturns();

        long bankProfit = assets.bankBalance - bankBefore;
        long stockProfit = assets.stockBalance - stockBefore;
        long leverageProfit = assets.leverageBalance - leverageBefore;

        RecordMonthlyChange("예금 이자", bankProfit);
        RecordMonthlyChange("주식 평가손익", stockProfit);
        RecordMonthlyChange("레버리지 평가손익", leverageProfit);

        // 생활비
        ApplyMandatoryExpense(fixedExpense, "생활비", ReceiptLineType.FixedExpense);

        // 상점 보유 효과, 할부금, 유지비
        if (ShopManager.Instance != null)
        {
            ShopManager.Instance.ProcessMonthlySettlement(currentMonth, stockProfit + leverageProfit);
        }

        // 이번 달 납부 대상 이벤트
        EventManager.Instance.ResolvePendingPenalty();

        // 모든 정산을 반영한 뒤 파산 판정
        ResolveBankruptcyAfterSettlement();
    }

    private void CompleteMonthReceipt()
    {
        if(!IsSetting) return;

        if (IsGameOver)
        {
            IsSetting = false;
            UpdateUI();
            return;
        }

        if(currentMonth >= maxMonth)
        {
            TriggerEnding();
            IsSetting = false;
            UpdateUI();
            return;
        }

        // 기존 규칙은 유지 : 마감한 달의 이벤트를 확인하고 다음 달 납부 대상으로 등록함.
        EventManager.Instance.CheckMonthlyEvent(currentMonth);

        currentMonth++;
        currentMonthOvertimeCount = 0;

        BeginMonthRecord();

        IsSetting = false;
        UpdateUI();
    }

    /// <summary>
    /// 게임 오버(파산) 처리 함수
    /// </summary>
    /// <param name="cause"></param>
    public void TriggerBankruptcy(string cause)
    {
        if (IsGameOver)
            return;

        IsBankrupt = true;
        IsGameOver = true;
        BankruptcyCause = cause;

        Debug.Log($"파산 사유: {BankruptcyCause}\n" + $"정산 후 현금: {availableCash:N0}원\n" + $"미납금액: {UnpaidAmount:N0}원\n" + $"총자산: {TotalAsset:N0}원");

        UpdateUI();
    }

    /// <summary>
    /// UI [야근하기] 버튼에 연결할 함수
    /// </summary>
    public void OnClickOvertimeWork()
    {
        //게임 오버 상태라면 더 이상 클릭되지 않도록
        if (!CanAct) return;

        //야근 횟수 제한
        if(currentMonthOvertimeCount >= maxOvertimePerMonth)
        {
            Debug.Log("이번 달 가능한 야근 횟수를 초과하였습니다.");
            return;
        }

        ApplyCashChange(50000, "야근 수입");      //탭 1회당 5만 원 추가 -> 로직 변경으로 수정
        stressLevel += 5f;           //탭 1회당 스트레스 5% 증가
        currentMonthOvertimeCount++; //탭 1회당 야근 횟수 1회 추가

        CheckStressPenalty(); //스트레스 패널티 적용 여부 판단
        UpdateUI();
    }

    private void CheckStressPenalty()
    {
        if (stressLevel < MaxStress)
            return;

        pendingBankruptcyCause = "";

        ApplyMandatoryExpense(3000000, "병원비", ReceiptLineType.Change);

        ResolveBankruptcyAfterSettlement();

        if (!IsGameOver)
        {
            stressLevel = 50f;

            Debug.Log($"병원비 납부 완료. 남은 현금: {availableCash:N0}원");
        }
    }

    private void UpdateUI()
    {
        //UI Manager를 통해 화면 전체 텍스트를 갱신
        if(UIManager.Instance != null)
        {
            UIManager.Instance.RefreshUI();
        }
        Debug.Log($"현재 턴 : {currentMonth} / 잔고 : {availableCash} / 스트레스 : {stressLevel}%");
    }

    private void TriggerEnding()
    {
        if(IsGameOver) return;

        IsGameOver = true;

        Debug.Log("Game Over. Moving to Ending Window.");
    }
}
