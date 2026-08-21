using UnityEngine;

public class GameManager : MonoBehaviour
{
    /*
    Inspector Zone
    */
    public static GameManager Instance;

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

    [Header("Income & Expense")]
    public long monthlySalary = 3000000;
    public long fixedExpense = 1500000; // 고정 지출 (월세, 생활비 등) => 추후 인플레이션에 따른 턴마다 증액 함수 필요.

    /*
    function Zone
    */

    // 싱글톤 패턴
    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // UI [턴 종료] 버튼에 연결할 함수
    public void OnClickNextMonth()
    {
        if (currentMonth >= maxMonth)
        {
            TriggerEnding();
            return;
        }

        ProcessMonthlySettlement();
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
        //EventManager.Instance.CheckRandomEvent();
    }

    // UI [야근하기] 버튼에 연결할 함수
    public void OnClickOvertimeWork()
    {
        availableCash += 50000; //탭 1회당 5만 원 추가
        stressLevel += 5f;      //탭 1회당 스트레스 5% 증가

        CheckStressPenalty(); //스트레스 패널티 적용 여부 판단
        UpdateUI();
    }

    private void CheckStressPenalty()
    {
        if (stressLevel >= 100f)
        {
            Debug.LogWarning("Stress Gauge is 100%. Panalty Active.");
            availableCash -= 3000000; // 병원비 청구
            stressLevel = 50f; // 스트레스 50%로 진행 => 추후 밸런싱 조절 필요.
        }
    }

    private void UpdateUI()
    {
        //UIManager.Instance.RefreshUI(currentMonth, availableCash, stressLevel);
        Debug.Log($"현재 턴 : {currentMonth} / 잔고 : {availableCash} / 스트레스 : {stressLevel}%");
    }

    private void TriggerEnding()
    {
        Debug.Log("Game Over. Moving to Ending Window.");
    }
}
