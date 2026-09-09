using UnityEngine;
using TMPro;

public class UIManager : Singleton<UIManager>
{
    /*
    Inspector Zone
    */
    //public static UIManager Instance;

    [Header("Main Player HUD")]
    public TextMeshProUGUI monthText;           // 현재 턴 (n/120)
    public TextMeshProUGUI totalAssetText;      // 총 자산
    public TextMeshProUGUI cashText;            // 보유 현금
    public TextMeshProUGUI stressText;          // 현재 피로도
    public TextMeshProUGUI overtimeCountText;   // 현재 야근 횟수

    [Header("Asset Balances HUD")]
    public TextMeshProUGUI bankBalanceText;     // 은행 보유 자산
    public TextMeshProUGUI stockBalanceText;    // 주식 보유 자산
    public TextMeshProUGUI leverageBalanceText; // 레버리지 보유 자산

    [Header("Event Notification HUD")]
    public TextMeshProUGUI warningPanelText;    // 발생 이벤트

    /*
    function Zone
    */

    // 싱글톤 패턴
    /*private void Awake()
    {
        if(Instance == null) Instance = this;
        else Destroy(gameObject);
    }*/

    private void Start()
    {
        // 게임 시작 시 최초 1회 UI 갱신
        RefreshUI();
    }

    /// <summary>
    /// 플레이어의 행동(클릭, 턴 종료)이 발생할 때마다 호출되어 화면의 정보를 최신화하는 함수
    /// </summary>
    public void RefreshUI()
    {
        // 1. 메인 상태바 갱신
        monthText.text = $"진행도 : {GameManager.Instance.currentMonth} / {GameManager.Instance.maxMonth} 개월";
        totalAssetText.text = $"총 자산 : {GameManager.Instance.TotalAsset:N0} 원";
        cashText.text = $"보유 현금 : {GameManager.Instance.availableCash:N0} 원";
        stressText.text = $"피로도 지수 : {GameManager.Instance.stressLevel} %";
        overtimeCountText.text = $"이번 달 야근 : {GameManager.Instance.currentMonthOvertimeCount} / {GameManager.Instance.maxOvertimePerMonth} 회";

        // 2. 투자 자산 갱신
        if(AssetManager.Instance != null)
        {
            bankBalanceText.text = $"예금 잔고 : {AssetManager.Instance.bankBalance:N0} 원";
            stockBalanceText.text = $"주식 평가액 : {AssetManager.Instance.stockBalance:N0} 원";
            leverageBalanceText.text = $"레버리지 평가액 : {AssetManager.Instance.leverageBalance:N0} 원";
        }

        // 3. 생애 주기 이벤트 경고 갱신
        // EventManager가 활성화 되어있고, 유예된 금액이 0원 이상일 경우
        if(EventManager.Instance != null && EventManager.Instance.pendingPenaltyAmount > 0)
        {
            warningPanelText.text = $"<color=red>[이벤트 발생]</color> {EventManager.Instance.pendingEventName}\n 다음 달 결산까지 <color=yellow>{EventManager.Instance.pendingPenaltyAmount:N0}원</color>을 \n보유하고 있어야합니다.";
            warningPanelText.gameObject.SetActive(true); //경고창 활성화
        }
        else
        {
            warningPanelText.gameObject.SetActive(false); //경고창 비활성화
        }
    }
}
