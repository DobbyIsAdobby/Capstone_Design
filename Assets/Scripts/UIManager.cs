using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class UIManager : Singleton<UIManager>
{
    /*
    Inspector Zone
    */
    //public static UIManager Instance;

    [Header("Main Player HUD")]
    public TextMeshProUGUI monthText;           // 현재 턴 (n/120)
    [SerializeField]
    public TMP_Text dateText;                   // 현재 날짜 (2030/1)
    public TextMeshProUGUI totalAssetText;      // 총 자산
    public TextMeshProUGUI cashText;            // 보유 현금
    public TextMeshProUGUI stressText;          // 현재 피로도
    public TextMeshProUGUI apText;              // 현재 AP
    public TextMeshProUGUI overtimeCountText;   // 현재 야근 횟수

    [Header("Monthly Expense")]
    [SerializeField] private MonthlyExpenseHelpUI monthlyExpenseHelpUI;

    [Header("Overtime Income Effect")]
    [SerializeField] private FloatingMoneyText incomeTextPrefab;    // 야근 수당 프리팹
    [SerializeField] private RectTransform floatingTextLayer;       // 띄우는 레이어
    [SerializeField] private RectTransform incomeSpawnPoint;        // 시작 위치

    [Header("Asset Balances HUD")]
    public TextMeshProUGUI bankBalanceText;     // 은행 보유 자산
    public TextMeshProUGUI stockBalanceText;    // 주식 보유 자산
    public TextMeshProUGUI leverageBalanceText; // 레버리지 보유 자산

    [Header("Asset Balance Tickers")]
    [SerializeField] private LifeEventTickerUI bankBalanceTicker;
    [SerializeField] private LifeEventTickerUI stockBalanceTicker;
    [SerializeField] private LifeEventTickerUI leverageBalanceTicker;

    [Header("Event Notification HUD")]
    [SerializeField] private LifeEventTickerUI lifeEventTicker;    // 발생 이벤트

    [Header("Trade Panels")]
    [SerializeField] private AssetTradePanel[] tradePanels;

    [Header("Asset_Navigation Panel")]
    [SerializeField] private AssetNavigationPanel assetNavigationPanel;

    [Header("Information Panel")]
    [SerializeField] private InformationPanel informationPanel;

    [Header("Job HUD")]
    [SerializeField] private TMP_Text jobGradeText;

    [Header("AP / Stress Icons")]
    [SerializeField] private Image apIcon;
    [SerializeField] private Image stressIcon;

    // 낮은 수치 이미지부터 높은 수치 이미지까지 순서대로 연결
    [SerializeField] private Sprite[] apStageSprites = new Sprite[5];
    [SerializeField] private Sprite[] stressStageSprites = new Sprite[5];

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
        float stressGauge = GameManager.Instance.stressLevel;
        float apGauge = GameManager.Instance.CurrentAP;
        
        // 피로도와 AP 아이콘 갱신
        UpdateStatusIcon(apIcon, apStageSprites, apGauge);
        UpdateStatusIcon(stressIcon, stressStageSprites, stressGauge);

        // 1. 메인 상태바 갱신
        monthText.text = $"{GameManager.Instance.currentMonth}/{GameManager.Instance.maxMonth}턴";
        // 현재 턴에서 날짜를 계산함. UI 갱신만으로 날짜가 증가하지 않음
        if(dateText != null)
        {
            System.DateTime date = GameManager.Instance.CurrentGameDate;
            dateText.text = $"{date.Year}년 {date.Month}월";
        }
        totalAssetText.text = $"순 자산 : {GameManager.Instance.TotalAsset:N0} 원";
        cashText.text = $"보유 현금 : {GameManager.Instance.availableCash:N0} 원";
        stressText.text = $"{stressGauge:0.#}%";
        stressText.color = stressGauge < 50f ? Color.black : (stressGauge < 75f) ? Color.orange : Color.red;
        apText.text = $"{apGauge:0.#}%";
        apText.color = apGauge < 50f ? Color.red : (apGauge < 75f) ? Color.orange : Color.black; 
        
        overtimeCountText.text = $"{GameManager.Instance.currentMonthOvertimeCount} / {GameManager.Instance.maxOvertimePerMonth} 회";

        // 2. 투자 자산 갱신 -- 투자 자산군 추가로 인한 로직 수정
        AssetManager assets = AssetManager.Instance;

        if (assets != null)
        {
            // 앞쪽 잔액은 현재 보유액을 표시.
            long stockGroupBalance = assets.stockBalance + assets.stockInverseBalance;

            long leverageGroupBalance = assets.leverageBalance + assets.leverageInverseBalance;

            // 괄호는 직전 월말 정산 손익을 표시.
            string bankMessage = $"예금: {assets.bankBalance:N0}원 " + FormatProfit(assets.LastBankProfit);

            string stockMessage = $"주식 합계: {stockGroupBalance:N0}원 " + FormatProfit(assets.LastStockGroupProfit);

            string leverageMessage = $"레버리지 합계: {leverageGroupBalance:N0}원 " + FormatProfit(assets.LastLeverageGroupProfit);

            // Show를 통해 문구와 스크롤 길이를 같이 갱신함.
            if (bankBalanceTicker != null)
                bankBalanceTicker.Show(bankMessage);
            else
                bankBalanceText.text = bankMessage;

            if (stockBalanceTicker != null)
                stockBalanceTicker.Show(stockMessage);
            else
                stockBalanceText.text = stockMessage;

            if (leverageBalanceTicker != null)
                leverageBalanceTicker.Show(leverageMessage);
            else
                leverageBalanceText.text = leverageMessage;

            // 현금이나 이번 달 지출이 변경될 때 버튼 글자색도 갱신해야함.
            if (monthlyExpenseHelpUI != null)
            {
                monthlyExpenseHelpUI.RefreshExpenseButtonColor();
            }
        }

        // 3. 생애 주기 이벤트 경고 갱신
        // EventManager가 활성화 되어있고, 유예된 금액이 0원 이상일 경우
        EventManager eventManager = EventManager.Instance;

        if (lifeEventTicker != null)
        {
            bool hasPendingEvent = eventManager != null && eventManager.pendingPenaltyAmount > 0;

            if (hasPendingEvent)
            {
                string message = $"<color=red>[이벤트 발생]</color> " + $"{eventManager.pendingEventName} · " + $"예정 지출 " + $"<color=orange>{eventManager.pendingPenaltyAmount:N0}원</color>";

                // 같은 내용으로 UI가 갱신돼도 이동 위치를 유지
                lifeEventTicker.Show(message);
            }
            else
            {
                // EventText뿐 아니라 배경과 경고 아이콘도 숨김
                lifeEventTicker.Hide();
            }
        }

        foreach(AssetTradePanel panel in tradePanels)
        {
            if(panel != null && panel.isActiveAndEnabled)
            {
                panel.Refresh();
            }
        }

        // 자산 관리소 패널 내 자산 내역 갱신
        if(assetNavigationPanel != null && assetNavigationPanel.isActiveAndEnabled)
        {
            assetNavigationPanel.Refresh();
        }

        // 정보 커뮤니티 패널 내 정보 패널 갱신
        if (informationPanel != null && informationPanel.isActiveAndEnabled)
        {
            informationPanel.Refresh();
        }

        // 직급 갱신
        JobManager job = JobManager.Instance;

        if (jobGradeText != null && job != null && job.IsConfigured)
        {
            jobGradeText.text = job.IsMaxGrade ? $"{job.GradeName}\nMAX" : $"{job.GradeName}\n";
        }
    }
    /// <summary>
    /// 수치를 20 단위로 나누어 5단계 이미지를 선택.
    /// 100~120처럼 범위를 초과하는 값은 마지막 이미지를 사용
    /// </summary>
    /// <param name="target"></param>
    /// <param name="sprites"></param>
    /// <param name="value"></param>
    private void UpdateStatusIcon(Image target, Sprite[] sprites, float value)
    {
        if (target == null || sprites == null || sprites.Length != 5)
            return;

        // 0~19.99 → 0, 20~39.99 → 1, ... 80 이상 → 4
        int index = Mathf.Clamp(Mathf.FloorToInt(Mathf.Max(0f, value) / 20f), 0, 4);

        Sprite selected = sprites[index];

        if (selected == null)
            return;

        // 단계가 동일하다면 Sprite를 다시 할당하지 않음.
        if (target.sprite != selected)
        {
            target.sprite = selected;
        }
    }

    /// <summary>
    /// 야근 수당이 지급된 후 호출함.
    /// 클릭할 때마다 별도의 텍스트가 intanciate되도록
    /// </summary>
    /// <param name="amount"></param>
    public void ShowOvertimeIncome(long amount)
    {
        if (incomeTextPrefab == null || floatingTextLayer == null || incomeSpawnPoint == null)
        {
            return;
        }

        FloatingMoneyText effect = Instantiate(incomeTextPrefab, floatingTextLayer, false);

        // 같은 Canvas 안에서 발생 지점의 위치에 맞춤
        // 서로 부모가 달라도 화면상 같은 위치에서 생성됨.
        effect.transform.position = incomeSpawnPoint.position;

        effect.Play(amount);
    }

    /// <summary>
    /// 양수 + 초록색 | 음수 - 빨간색 | 0 - 검정색
    /// </summary>
    /// <param name="profit"></param>
    /// <returns></returns>
    private static string FormatProfit(long profit)
    {
        // 양수 +
        // 음수는 숫자 서식에서 -가 자동으로 붙음
        string amount = profit > 0 ? $"+{profit:N0}원" : $"{profit:N0}원";

        if (profit > 0)
        {
            // RGB(0, 200, 0) = #00C800
            // 괄호를 포함한 부분만 초록색으로 표시.
            return $"<color=#00C800>({amount})</color>";
        }

        if (profit < 0)
        {
            // RGB(200, 0, 0) = #C80000
            // 괄호를 포함한 부분만 빨간색으로 표시.
            return $"<color=#C80000>({amount})</color>";
        }

        // 손익이 0이면 텍스트 색상을 유지.
        return $"({amount})";
    }
}
