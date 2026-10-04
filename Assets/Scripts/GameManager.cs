using System.Collections;
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
    // Inspector에 기존 값이 남을 수 있으므로 Start에서도 60으로 맞춰줘야함.
    public int maxMonth = MarketModelConfig.TurnCount;
    //1턴 = 2030/1 -> 경과 개월은 현재 턴의 -1
    public System.DateTime CurrentGameDate => new System.DateTime(2030,1,1).AddMonths(currentMonth - 1);

    [Header("Player Status")]
    public long availableCash = 5000000; // 초기 자본금 500만 원
    public float stressLevel = 0f; // 스트레스 지수
    
    /// <summary>
    /// 대출을 차감하기 전의 자산 합계
    /// </summary>
    public long GrossAsset => availableCash + (AssetManager.Instance != null ? AssetManager.Instance.GetTotalInvestedValue() : 0L);

    /// <summary>
    /// TotalAsset을 참조하는 HUD/청구서는 순자산을 사용함.
    /// </summary>
    public long TotalAsset => GrossAsset - (LoanManager.Instance != null ? LoanManager.Instance.OutstandingPrincipal : 0L);

    public int currentMonthOvertimeCount = 0; // 이번 달 야근 횟수
    public readonly int maxOvertimePerMonth = 30; // 한 달 최대 야근 가능 횟수(불변성 적용)

    [Header("Stress")]
    [SerializeField, Min(1)]
    private float baseMaxStress = 100f;
    public float MaxStress => baseMaxStress + (ShopManager.Instance != null ? ShopManager.Instance.MaxStressBonus : 0f);

    [Header("AP")]
    [SerializeField, Min(1)]
    private int baseMaxAP = 100;
    [SerializeField, Min(0)]
    private int overtimeAPCost = 1;


    [Header("Income & Expense")]
    public long monthlySalary  => JobManager.Instance != null ? JobManager.Instance.MonthlySalary : 0L; // 고정된 값이 아닌 현재 직급의 급여를 사용함 - 직급 시스템 연결 완료
    public long fixedExpense = 1500000; // 고정 지출 (월세, 생활비 등) => 추후 인플레이션에 따른 턴마다 증액 함수 필요.

    [Header("Monthly Receipt")]
    [SerializeField] private MonthlyReceiptPanel receiptPanel;
    private readonly List<ReceiptLine> monthlyLines = new List<ReceiptLine>();
    
    private bool hasMonthBaseLine;

    [Header("Market Generation")]
    [SerializeField] private MarketGenerator marketGenerator;

    [Header("Opening Story")]
    [SerializeField] private MarketIntroUI marketIntroUI;

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
    /// 동일한 종료 상황에서 Scene 이동이 중복 요청되는 것을 막음.
    /// </summary>
    private bool endingTransitionRequested;
    
    /*
    /// <summary>
    /// 시장과 월 기록, 직급 게임이 준비됐고, 정산,게임 종료 상태가 아닐 때만 행동 - 각 행동에 따라 분리해야할 속성이 많아짐에 따라 3개로 분리하였음.
    /// </summary>
    public bool CanAct =>
        hasMonthBaseLine &&
        !IsGameOver &&
        !IsSetting &&
        DataManager.Instance != null &&
        DataManager.Instance.IsMarketReady &&
        JobManager.Instance != null &&
        JobManager.Instance.IsConfigured &&
        !JobManager.Instance.IsPlaying;
    */

    /// <summary>
    /// 일시정지 메뉴를 열 수 있는 기본 게임 상태. 
    /// 미니게임 도중에도 일시정지 자체는 허용함.
    /// </summary>
    public bool CanOpenPause => 
        hasMonthBaseLine &&
        !IsGameOver &&
        !IsSetting &&
        DataManager.Instance != null &&
        DataManager.Instance.IsMarketReady &&
        JobManager.Instance != null &&
        JobManager.Instance.IsConfigured;

    /// <summary>
    /// 구매, 거래, 야근 등 일반 행동이 가능한 상태. 
    /// 일시정지와 미니게임 진행 중에는 행동할 수 없음.
    /// </summary>
    public bool CanAct =>
        CanOpenPause &&
        !PauseController.IsPaused &&
        !JobManager.Instance.IsPlaying;

    /// <summary>
    /// 저장할 수 있는 stable한 게임 상태. 
    /// 일시정지 중 저장은 허용하지만 진행 중인 미니게임이 존재할 경우 이는 저장을 허용하지 않음.
    /// </summary>
    public bool CanSaveCurrentState =>
        CanOpenPause &&
        !JobManager.Instance.IsPlaying;

    /// <summary>
    /// 총 자산 확인
    /// </summary>
    public long MonthStartTotalAsset { get; private set; }
    /// <summary>
    /// 현재 AP 조회
    /// </summary>
    public int CurrentAP { get; private set; }
    // JSON 내에서 자동차를 구매시 최대 AP를 20 증가하는 로직이 존재함.
    // 소유 목록으로 계산하므로 화면을 열 때마다 중복 증가하지 않도록 함.
    public int MaxAP => baseMaxAP + (ShopManager.Instance != null ? ShopManager.Instance.MaxAPBonus : 0);
    // 같은 턴에 초기화 함수가 중복 호출되어도 AP를 다시 채우지 않음.
    private int lastAPResetTurn = -1;

    /*
    private void Start()
    {
        BeginMonthRecord();
    }
    */

    /// <summary>
    /// 시장 생성이 완료될 때까지 기다린 뒤 플레이를 시작함. 
    /// IEnumerator Start는 유니티가 코루틴으로 실행함.
    /// </summary>
    /// <returns></returns>
    private IEnumerator Start()
    {
        // 아직 플레이를 시작하지 않았으므로 행동할 수 없음
        hasMonthBaseLine = false;

        // Inspector에 60으로 재조정할 것.
        maxMonth = MarketModelConfig.TurnCount;

        // LobbyScene에서 저장 데이터를 전달했다면 새 게임 초기화 대신 전달받은 데이터를 복원함.
        if (SceneTransitionManager.TryTakePendingLoad(out GameSaveData saved))
        {
            try
            {
                if (SceneTransitionManager.Instance == null)
                {
                    throw new System.InvalidOperationException("SceneTransitionManager가 없습니다.");
                }

                // GameScene에 연결된 설정으로 다시 검사
                SceneTransitionManager.Instance.ValidateSave(saved);

                // 각 Manager 상태를 복원하고 마지막에 HUD를 갱신
                GameSaveCoordinator.Restore(saved);
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception, this);

                // 불러오기 실패를 새 게임 시작으로 처리하지 않음.
                // 실패 안내를 가지고 로비로 돌아감.
                if (SceneTransitionManager.Instance != null)
                {
                    SceneTransitionManager.Instance.ReturnAfterInitialLoadFailure(exception.Message);
                }
            }

            // 코루틴 종료
            // 아래의 ONNX 생성과 BeginMonthRecord가 실행되면 저장된 시장, AP·청구서가 초기화되므로 반드시 필요함.
            yield break;
        }

        // LoanManager 누락으로 채무 계산이나 청구가 생략되지 않도록
        if (LoanManager.Instance == null || !LoanManager.Instance.IsConfigured)
        {
            Debug.LogError("LoanManager와 LoanRules 설정을 확인하세요.");
            yield break;
        }

        // JobManager 연결이 생략되지 않도록
        if (JobManager.Instance == null ||
            !JobManager.Instance.IsConfigured)
        {
            Debug.LogError("JobManager와 JobRules 설정을 확인하세요.");
            yield break;
        }

        // 수익률 자동 생성 모델 적용이 생략되지 않도록
        if (marketGenerator == null || !marketGenerator.isActiveAndEnabled)
        {
            Debug.LogError("활성화된 MarketGenerator를 GameManager에 연결하세요.");

            yield break;
        }

        if (WealthTierManager.Instance == null || !WealthTierManager.Instance.IsConfigured)
        {
            Debug.LogError("WealthTierManager의 씬 참조와 5개 티어 에셋을 확인하세요.");
            yield break;
        }

        // 스토리 UI가 준비되지 않았다면 새 게임 시작을 중단
        if (marketIntroUI == null || !marketIntroUI.isActiveAndEnabled || !marketIntroUI.IsConfigured)
        {
            Debug.LogError("MarketIntroUI와 로딩 패널 연결을 확인하세요.");
            yield break;
        }

        // 스토리와 시장 생성을 함께 시작
        marketIntroUI.BeginIntro();
        marketGenerator.Begin();

        // 시장 생성은 Update에서 여러 프레임에 나누어 진행됨
        // 기다리는 동안 스토리 코루틴도 계속 실행됨
        while (!marketGenerator.IsFinished)
            yield return null;

        // 생성이 끝났더라도 실패했다면 게임 시작을 허용하지 않음.
        if (!marketGenerator.Succeeded)
        {
            marketIntroUI.NotifyMarketFailed();

            Debug.LogError($"시장 생성 실패로 게임을 시작할 수 없습니다: " + marketGenerator.Error);

            yield break;
        }

        // 시장 생성 성공을 알림.
        // 스토리까지 끝났을 때만 시작 버튼이 표시됨.
        marketIntroUI.NotifyMarketReady();

        // 유저가 직접 시작 버튼을 누를 때까지 대기
        // 이 동안 hasMonthBaseLine은 false이므로 게임 행동은 차단됨
        while (!marketIntroUI.StartRequested)
            yield return null;

        // 클릭 이후에 첫 달의 AP, 정보, 재산 등급 등을 초기화
        BeginMonthRecord();
        UpdateUI();

        // 첫 달 준비가 끝난 후 로딩 패널을 닫음
        marketIntroUI.Hide();
    }

    /// <summary>
    /// 새로운 턴에 진입하면 최대 AP로 초기화.
    /// 지난 턴에 남은 AP에 추가하면 안됨 - 이월 금지
    /// </summary>
    private void BeginAPTurn()
    {
        if (lastAPResetTurn == currentMonth)
            return;

        lastAPResetTurn = currentMonth;
        CurrentAP = MaxAP;
    }

    /// <summary>
    /// AP 보유량에 따라 현재 사용 가능한지만 검사
    /// 버튼 표시와 행동 실행 여부 양쪽에서 사용함.
    /// </summary>
    /// <param name="amount"></param>
    /// <param name="reason"></param>
    /// <returns></returns>
    public bool CanSpendAP(int amount, out string reason)
    {
        reason = "";

        if (!CanAct)
        {
            reason = "지금은 행동할 수 없습니다.";
            return false;
        }

        if (amount < 0)
        {
            reason = "AP 소모량 설정이 올바르지 않습니다.";
            return false;
        }

        if (CurrentAP < amount)
        {
            reason = $"AP가 부족합니다. 필요 {amount}, 보유 {CurrentAP}";
            return false;
        }

        return true;
    }

    /// <summary>
    /// AP가 충분할 때만 차감함.
    /// 부족하면 현재 AP를 유지하고 실패를 반환
    /// </summary>
    /// <param name="amount"></param>
    /// <param name="reason"></param>
    /// <returns></returns>
    public bool TrySpendAP(int amount, out string reason)
    {
        if (!CanSpendAP(amount, out reason))
            return false;

        CurrentAP -= amount;
        return true;
    }

    /// <summary>
    /// AP 회복 효과를 적용할 경우 최대치를 넘지 않도록 함.
    /// 화면 갱신은 구매 전체 처리가 끝난 뒤 호출
    /// </summary>
    /// <param name="amount"></param>
    public void RecoverAP(int amount)
    {
        if (amount <= 0)
            return;

        int recoverable = Mathf.Max(0, MaxAP - CurrentAP);
        CurrentAP += Mathf.Min(amount, recoverable);
    }


    /// <summary>
    /// 지난 턴 영수증 내역 초기화 및 총 자산 업데이트를 진행하는 함수
    /// </summary>
    private void BeginMonthRecord()
    {
        monthlyLines.Clear();
        MonthStartTotalAsset = TotalAsset;
        hasMonthBaseLine = true;

        // 첫 턴 및 다음 턴 진입 시 최대치로 초기화.
        // 같은 턴에 중복 호출되어도 다시 회복하지 않음.
        BeginAPTurn();

        // 새로운 턴마다 이전 정보를 초기화하고, 휴대폰을 갖고있으면 하급 정보를 미리 공개.
        if(RumorManager.Instance != null)
        {
            RumorManager.Instance.BeginTurn();
        }

        // 새 턴의 순자산으로 외형과 등급을 확정.
        WealthTierManager.Instance.ApplyAtTurnStart(currentMonth, TotalAsset);
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

        // 현금이나 자산을 변경하기 전에 이번 턴 데이터 존재 여부를 확인함
        // 급여부터 반영한 뒤 데이터 누락을 발견하는 상황을 방지하고자 함. 
        // out _ => out 파라미터 값을 무시하고 결과 여부만 확인하고자 함
        if(!DataManager.Instance.TryGetGeneratedMarketRates(currentMonth, out _))
        {
            Debug.LogError($"{currentMonth}턴 시장 데이터가 없어 정산을 중단합니다.");
            return; // - 얘 왜 빠져있었음?
        }

        if (LoanManager.Instance == null || !LoanManager.Instance.IsConfigured)
        {
            Debug.LogError("대출 시스템 설정 오류로 정산을 중단합니다.");
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
            // 원금 거래는 청구서에 표시하지만 수익 합계에서는 제외함
            if (line.Type == ReceiptLineType.Financing)
                continue;
                
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
        long stockInverseBefore = assets.stockInverseBalance;
        long leverageInverseBefore = assets.leverageInverseBalance;

        assets.CalculateMonthlyReturns();

        long bankProfit = assets.bankBalance - bankBefore;
        long stockProfit = assets.stockBalance - stockBefore;
        long leverageProfit = assets.leverageBalance - leverageBefore;
        long stockInverseProfit = assets.stockInverseBalance - stockInverseBefore;
        long leverageInverseProfit = assets.leverageInverseBalance - leverageInverseBefore;

        RecordMonthlyChange("예금 이자", bankProfit);
        RecordMonthlyChange("주식 손익", stockProfit);
        RecordMonthlyChange("레버리지 손익", leverageProfit);
        // 청구서의 긴 라벨 문제를 피하도록 짧게 표기합니다.
        RecordMonthlyChange("주식 인버스", stockInverseProfit);
        RecordMonthlyChange("레버리지 인버스", leverageInverseProfit);

        // 생활비
        ApplyMandatoryExpense(fixedExpense, "생활비", ReceiptLineType.FixedExpense);

        //전체 자산 상품 이익(노트북 보너스 계산용)
        long marketProfit = stockProfit + leverageProfit + stockInverseProfit + leverageInverseProfit;

        // 상점 보유 효과, 할부금, 유지비
        if (ShopManager.Instance != null)
        {
            ShopManager.Instance.ProcessMonthlySettlement(currentMonth, marketProfit);
        }

        // 이번 달 납부 대상 이벤트
        EventManager.Instance.ResolvePendingPenalty();

        // 대출 이자 및 만기 원금 청구
        LoanManager.Instance.ProcessMonthlySettlement(currentMonth);

        // 모든 정산을 반영한 뒤 파산 판정
        ResolveBankruptcyAfterSettlement();

        // 정상 진행 중인 경우에만 월 고정 경험치를 지급
        // 이번 턴 급여는 이미 지급됐으므로, 승급 급여는 다음 턴부터 적용
        if (!IsGameOver)
        {
            JobManager.Instance.ProcessMonthlyExperience(currentMonth);
        }
    }

    private void CompleteMonthReceipt()
    {
        if (!IsSetting)
            return;

        // 월말 정산에서 파산했다면 청구서 확인 후 배드엔딩으로 이동.
        if (IsGameOver)
        {
            IsSetting = false;
            RequestEndingScene(EndingType.Bad);
            return;
        }

        // 60턴을 정상적으로 마쳤다면 노말, 해피엔딩을 판정.
        if (currentMonth >= maxMonth)
        {
            IsSetting = false;
            TriggerEnding();
            return;
        }

        // 마지막 턴이 아니면 기존 다음 달 진행 로직을 유지.
        EventManager.Instance.CheckMonthlyEvent(currentMonth);

        currentMonth++;
        currentMonthOvertimeCount = 0;

        BeginMonthRecord();

        IsSetting = false;
        UpdateUI();
    }

    /// <summary>
    /// 병원비,월말 정산에서 호출하는 파산 진입점.
    /// </summary>
    /// <param name="cause"></param>
    public void TriggerBankruptcy(string cause)
    {
        EndGameEarly(cause, true);
    }

    /// <summary>
    /// 추후 과로사 등 파산 이외의 중도 종료 상황에서 호출.
    /// 사망 조건 자체를 판정하는 메서드는 아님.
    /// </summary>
    /// <param name="cause"></param>
    public void TriggerBadEnding(string cause)
    {
        EndGameEarly(cause, false);
    }

    /// <summary>
    /// 중도 종료 상태를 설정.
    /// 월말 정산 중이라면 청구서 확인 후 엔딩으로 이동.
    /// </summary>
    /// <param name="cause"></param>
    /// <param name="bankruptcy"></param>
    private void EndGameEarly(string cause, bool bankruptcy)
    {
        if (IsGameOver)
            return;

        IsGameOver = true;
        IsBankrupt = bankruptcy;

        // 파산 사유 Field에 사망 원인을 넣지는 않음.
        BankruptcyCause = bankruptcy ? cause : "";

        Debug.Log(
            $"중도 종료 사유: {cause}\n" +
            $"보유 현금: {availableCash:N0}원\n" +
            $"미납금액: {UnpaidAmount:N0}원\n" +
            $"순자산: {TotalAsset:N0}원");

        UpdateUI();

        // 월말 파산은 기존 청구서를 먼저 확인하게 함.
        // 야근 중 병원비 파산 등 정산 외 종료는 바로 엔딩으로 이동.
        if (!IsSetting)
        {
            RequestEndingScene(EndingType.Bad);
        }
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

        // 야근 가능 횟수를 먼저 검사한 후 AP를 차감함.
        // 횟수 초과로 인해 실행하지 못한 야근은 현금/피로도/야근 횟수가 변경되지 않음.
        if (!TrySpendAP(overtimeAPCost, out string reason))
        {
            Debug.Log(reason);
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

    /// <summary>
    /// 마지막 턴 정산과 청구서 확인을 마친 뒤 호출.
    /// </summary>
    private void TriggerEnding()
    {
        if (IsGameOver)
            return;

        ShopManager shop = ShopManager.Instance;
        WealthTierManager wealth = WealthTierManager.Instance;

        // 필수 참조 누락을 노말엔딩으로 잘못 처리하면 안됨
        if (shop == null || wealth == null || !wealth.IsConfigured)
        {
            Debug.LogError("엔딩 판정에 필요한 상점·재산 등급 설정을 확인하세요.");
            return;
        }

        bool hasPenthouse = shop.HasHappyEndingItem;

        // 마지막 정산이 반영된 순자산으로 판정.
        bool isDiamond = wealth.MeetsDiamondRequirement(TotalAsset);

        EndingType ending = hasPenthouse && isDiamond ? EndingType.Happy : EndingType.Normal;

        IsGameOver = true;
        RequestEndingScene(ending);
    }

    /// <summary>
    /// 엔딩 이동 요청
    /// </summary>
    /// <param name="ending"></param>
    private void RequestEndingScene(EndingType ending)
    {
        if (endingTransitionRequested)
            return;

        endingTransitionRequested = true;
        StartCoroutine(MoveToEndingScene(ending));
    }

    private IEnumerator MoveToEndingScene(EndingType ending)
    {
        // 현재 버튼 이벤트나 정산 호출이 끝난 다음, Scene 이동
        // 파산을 발생시킨 메서드가 아직 실행 중일 수 있기 때문
        yield return null;

        SceneTransitionManager transition = SceneTransitionManager.Instance;

        if (transition == null)
        {
            endingTransitionRequested = false;
            Debug.LogError("SceneTransitionManager가 없습니다.");
            yield break;
        }

        if (!transition.TryMoveToEnding(ending, out string error))
        {
            endingTransitionRequested = false;
            Debug.LogError($"엔딩 씬 이동 실패: {error}");
        }
    }

    public PlayerSaveData CaptureSave()
    {
        var data = new PlayerSaveData
        {
            currentMonth = currentMonth,
            availableCash = availableCash,
            stressLevel = stressLevel,
            currentAP = CurrentAP,
            overtimeCount = currentMonthOvertimeCount,
            lastAPResetTurn = lastAPResetTurn,
            monthStartTotalAsset = MonthStartTotalAsset
        };

        foreach (ReceiptLine line in monthlyLines)
        {
            data.monthlyLines.Add(new ReceiptLineSaveData
            {
                name = line.Name,
                amount = line.Amount,
                type = line.Type
            });
        }

        return data;
    }

    public void RestoreSave(PlayerSaveData data)
    {
        // 모든 Manager 복원이 끝나기 전까지 일반 행동을 막음
        hasMonthBaseLine = false;

        currentMonth = data.currentMonth;
        maxMonth = MarketModelConfig.TurnCount;

        availableCash = data.availableCash;
        stressLevel = data.stressLevel;
        CurrentAP = data.currentAP;

        currentMonthOvertimeCount = data.overtimeCount;
        lastAPResetTurn = data.lastAPResetTurn;
        MonthStartTotalAsset = data.monthStartTotalAsset;

        monthlyLines.Clear();

        foreach (ReceiptLineSaveData line in data.monthlyLines)
        {
            monthlyLines.Add(
                new ReceiptLine(line.name, line.amount, line.type));
        }

        // 현재 저장 규칙에서는 종료,정산 상태를 저장하지 않음
        IsGameOver = false;
        IsBankrupt = false;
        IsSetting = false;
        
        endingTransitionRequested = false;

        BankruptcyCause = "";
        pendingBankruptcyCause = "";
    }

    public void CompleteSaveRestore()
    {
        // 다른 Manager까지 전부 복원한 다음 한 번만 호출
        hasMonthBaseLine = true;
        UpdateUI();
    }
}
