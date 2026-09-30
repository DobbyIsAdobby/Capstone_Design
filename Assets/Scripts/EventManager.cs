using UnityEngine;

public class EventManager : Singleton<EventManager>
{
    /*
    Inspector Zone
    */
    //public static EventManager Instance;

    [Header("Pending Penalty")]
    public long pendingPenaltyAmount = 0; //유예된 청구서 금액
    public string pendingEventName = "";  //유예된 청구서 사유

    /*
    function Zone
    */

    //싱글톤 패턴
    /*private void Awake()
    {
        if(Instance == null) Instance = this;
        else Destroy(gameObject);
    }*/

    /// <summary>
    /// GameManager에서 턴이 넘어갈 때마다 호출하는 함수
    /// </summary>
    public void CheckMonthlyEvent(int currentMonth)
    {
        // 1. 확정적 생애 주기 이벤트 (프로토타입용 => 추후 밸런싱 필요)
        if (currentMonth == 24) // 2년 차
        {
            SetPendingEvent("집주인의 전세 보증금 인상 통보", 15000000); // 1,500만 원
        }
        else if (currentMonth == 60) // 5년 차
        {
            SetPendingEvent("결혼 및 주거 독립 자금 지출", 30000000); // 3,000만 원
        }
        else if (currentMonth == 96) // 8년 차
        {
            SetPendingEvent("자녀 양육비 및 교육비 목돈 지출", 20000000); // 2,000만 원
        }
        // 2. 무작위 돌발 지출 (예: 5% 확률로 150만 원 지출)
        else if (Random.value <= 0.05f) 
        {
            SetPendingEvent("경조사 및 갑작스러운 질병 발생", 1500000); // 150만 원
        }
    }
    
    /// <summary>
    /// 이벤트를 즉시 차감하지 않고, 다음 턴까지 유예(대기)시키는 함수
    /// </summary>
    private void SetPendingEvent(string name, long cost)
    {
        pendingEventName = name;
        pendingPenaltyAmount = cost;
        Debug.LogWarning($"[이벤트 발생] 다음 달 턴 종료 전까지 {cost:N0}원을 마련해야 합니다.");

        //추후 UIManager로 경고 UI 호출
        //UIManager.Instance.ShowWarningPopup(name, cost);
    }

    /// <summary>
    /// 다음 턴 결산 시 호출되어, 실제로 돈을 차감하고 파산 여부를 묻는 함수 - 해당 파트도 청구서 추가로 인한 수정
    /// </summary>
    public void ResolvePendingPenalty()
    {
        // 처리할 이벤트 비용이 없으면 종료.
        if (pendingPenaltyAmount <= 0)
            return;

        // 초기화하기 전에 이번에 처리할 내용을 복사.
        string eventName = pendingEventName;
        long amount = pendingPenaltyAmount;

        // 대기 중인 이벤트 청구를 초기화.
        pendingPenaltyAmount = 0;
        pendingEventName = "";

        // 복사한 값으로 실제 차감 및 청구서 기록.
        GameManager.Instance.ApplyMandatoryExpense(amount, $"이벤트: {eventName}", ReceiptLineType.Change);
    }
}
