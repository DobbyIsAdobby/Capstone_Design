using UnityEngine;

public class GameManager : Singleton<GameManager>
{
    /*
    Inspector Zone
    */
    //public static GameManager Instance;

    [Header("Game Time")]
    public int currentMonth = 1;
    public int maxMonth = 12; // 총 10년 (120턴)

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

    [Header("Income & Expense")]
    public long monthlySalary = 3000000;
    public long fixedExpense = 1500000; // 고정 지출 (월세, 생활비 등) => 추후 인플레이션에 따른 턴마다 증액 함수 필요.

    /*
    function Zone
    */

    // 싱글톤 패턴
    /*private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }*/

    /// <summary>
    /// UI [턴 종료] 버튼에 연결할 함수
    /// </summary>
    public void OnClickNextMonth()
{
    if (currentMonth > maxMonth)
    {
        TriggerEnding();
        return;
    }

    ProcessMonthlySettlement();

    if (currentMonth >= maxMonth)
    {
        TriggerEnding();
        return;
    }

    currentMonth++;
    UpdateUI();
}

    private void ProcessMonthlySettlement()
    {
        // 1. 기본 수입 및 지출 정산
        availableCash += (monthlySalary - fixedExpense);

        // 2. 자산 수익률 정산 (추후 AssetManager 연결)
        AssetManager.Instance.CalculateMonthlyReturns();

        // 3. 턴 기반 이벤트 및 마진콜(게임오버) 체크 (추후 EventManager 연결)
        EventManager.Instance.ResolvePendingPenalty();

        // 만약 현금 부족으로 파산했다면, 아래 로직을 더 이상 실행하지 않음.
        if(currentMonth > maxMonth) return;

        // 4. 새로운 이벤트(청구서) 발생 체크 (발생 시 다음 달에 납부하도록 경고)
        EventManager.Instance.CheckMonthlyEvent(currentMonth);

        // 5. 다음 달로 넘어가면 야근 횟수 초기화
        currentMonthOvertimeCount = 0;
    }

    /// <summary>
    /// 게임 오버(파산) 처리 함수
    /// </summary>
    /// <param name="cause"></param>
    public void TriggerBankruptcy(string cause)
    {
        Debug.LogError("파산하셨습니다.");
        Debug.LogError($"파산 사유 : {cause}");

        //추가 조작을 막기 위해 턴을 강제로 maxMonth이상으로 올리고 추후 UI 팝업을 띄울 예정
        currentMonth = maxMonth + 1;

        //UIManager.Instance.ShowGameOverPanel(cause);
    }

    /// <summary>
    /// UI [야근하기] 버튼에 연결할 함수
    /// </summary>
    public void OnClickOvertimeWork()
    {
        //게임 오버 상태라면 더 이상 클릭되지 않도록
        if (currentMonth > maxMonth) return;

        //야근 횟수 제한
        if(currentMonthOvertimeCount >= maxOvertimePerMonth)
        {
            Debug.Log("이번 달 가능한 야근 횟수를 초과하였습니다.");
            return;
        }

        availableCash += 50000;      //탭 1회당 5만 원 추가
        stressLevel += 5f;           //탭 1회당 스트레스 5% 증가
        currentMonthOvertimeCount++; //탭 1회당 야근 횟수 1회 추가

        CheckStressPenalty(); //스트레스 패널티 적용 여부 판단
        UpdateUI();
    }

    private void CheckStressPenalty()
    {
        if (stressLevel >= 100f)
        {
            long hospitalBill = 3000000; // 병원비 300만 원
            Debug.LogWarning("Stress Gauge is 100%. Penalty Active.");

            if (availableCash >= hospitalBill)
            {
                // 현금이 충분할 경우 병원비 지불 및 스트레스 완화
                availableCash -= hospitalBill;
                stressLevel = 50f; // 치료를 받았으므로 50%로 완화
                Debug.Log($"응급실 비용 {hospitalBill:N0}원 지불 완료. 남은 현금: {availableCash:N0}원");
            }
            else
            {
                // 현금이 부족한 경우 파산(게임 오버) 처리
                long shortage = hospitalBill - availableCash;
                Debug.LogError($"병원비가 {shortage:N0}원 부족하여 파산했습니다.");
                
                // TriggerBankruptcy를 호출하여 파산 사유 전달
                TriggerBankruptcy("응급실 병원비 미납");
            }
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
        Debug.Log("Game Over. Moving to Ending Window.");
    }
}
