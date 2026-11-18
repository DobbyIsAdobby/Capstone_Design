using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AssetTradePanel : MonoBehaviour
{
    /*
    Inspector Zone
    */
    [Header("Asset")]
    [SerializeField] private AssetType assetType;

    [Header("Trade UI")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text balanceText;  // 보유 현금 및 자산 잔고
    [SerializeField] private TMP_Text limitText;    // 투자 가능 금액
    [SerializeField] private TMP_Text amountText;   // 금액 지정
    [SerializeField] private TMP_Text confirmText; 
    [SerializeField] private TMP_Text feedbackText;
    [SerializeField] private Slider amountSlider;   // 금액 지정 슬라이더
    [SerializeField] private Button confirmButton;  // 확인 버튼

    [Header("Sector Selection")]
    [SerializeField] private GameObject sectorButtonsRoot;

    // 기술주, 바이오주, 철강주, 엔터주
    [SerializeField] private Button[] sectorButtons;

    [SerializeField] private Color selectedSectorColor = new Color32(237, 187, 80, 255);

    private MarketSector selectedSector = MarketSector.Technology;

    private Color[] normalSectorColors;

    [Header("Keypad")]
    [SerializeField] private GameObject keypadRoot; // 키패드
    [SerializeField] private TMP_InputField amountInput; // 입력된 금액
    [SerializeField] private TMP_Text keypadErrorText;  // 투자 가능 금액 표시

    [Header("Market Chart")]
    [SerializeField] private MarketChartPanelUI marketChartPanel;

    private const int SliderSteps = 100000; //슬라이드 진행 정도

    private bool isBuying = true;
    private long selectedAmount;

    /*
    function Zone
    */
    /// <summary>
    /// 기존 자산군에 따른 자산 타입 분류를 간편화하기 위한 메서드
    /// </summary>
    /// <returns></returns>
    private string GetAssetName()
    {
        switch (assetType)
        {
            case AssetType.Bank:
                return "은행";
            case AssetType.Stock:
                return "주식";
            case AssetType.Leverage:
                return "레버리지";
            case AssetType.StockInverse:
                return "주식 인버스";
            case AssetType.LeverageInverse:
                return "레버리지 인버스";

            default:
                return "알 수 없는 자산";
        }
    }

    private long TradeLimit
    {
        get
        {
            // isBuying이 True일 경우 가용 현금을, False일 경우 자산 타입에 따라 해당 자산의 보유 액수를 limit으로 지정
            long limit = isBuying ? GameManager.Instance.availableCash : AssetManager.Instance.GetBalance(assetType, selectedSector);

            return Math.Max(0L, limit);
        }
    }

    private void Awake()
    {
        amountSlider.minValue = 0;
        amountSlider.maxValue = SliderSteps;
        // 슬라이더에서 선택할 수 있는 값을 정수로만 제한.
        amountSlider.wholeNumbers = true;

        // 은행에는 종목 선택 UI를 사용하지 않음.
        if (assetType == AssetType.Bank)
        {
            if (sectorButtonsRoot != null)
                sectorButtonsRoot.SetActive(false);

            return;
        }

        if (sectorButtonsRoot == null || sectorButtons == null || sectorButtons.Length != MarketSectorUtility.SectorCount)
        {
            throw new InvalidOperationException("종목 버튼 Root와 네 개의 버튼을 연결하세요.");
        }

        normalSectorColors = new Color[sectorButtons.Length];

        for (int i = 0; i < sectorButtons.Length; i++)
        {
            if (sectorButtons[i] == null || sectorButtons[i].image == null)
                throw new InvalidOperationException("종목 버튼의 Image를 확인하세요.");

            normalSectorColors[i] = sectorButtons[i].image.color;
        }
    }

    private void OnEnable()
    {
        // 패널을 열면 기술주부터 보여주기
        selectedSector = MarketSector.Technology;

        isBuying = true;
        selectedAmount = 0;

        keypadRoot.SetActive(false);
        feedbackText.text = "";

        Refresh();
    }

    public void SetBuyMode()
    {
        SetMode(true);
    }

    public void SetSellMode()
    {
        SetMode(false);
    }

    private void SetMode(bool buying)
    {
        isBuying = buying;
        selectedAmount = 0;

        keypadRoot.SetActive(false);
        feedbackText.text = "";

        Refresh();
    }

    public void Refresh()
    {
        // 자산군에 따른 자산 타입 분류
        //string assetName = assetType == AssetType.Bank ? "은행" : assetType == AssetType.Stock ? "주식" : "레버리지";
        string assetName = GetAssetName();
        // 자산군에 따른 액션 단어 분류
        string action = assetType == AssetType.Bank ? (isBuying ? "예금" : "출금") : (isBuying ? "매수" : "매도");

        string displayName = assetType == AssetType.Bank ? assetName : $"{assetName}";

        titleText.text = $"{displayName} - {action}";

        long balance = AssetManager.Instance.GetBalance(assetType, selectedSector);

        string balanceLabel = assetType == AssetType.Bank ? "보유 예금" : "선택 종목 보유액";

        balanceText.text = $"보유 현금: {GameManager.Instance.availableCash:N0}원\n" + $"{balanceLabel}: {balance:N0}원";

        limitText.text = $"{action} 가능 금액: {TradeLimit:N0}원";
        confirmText.text = action;

        RefreshSectorButtons();
        SetSelectedAmount(selectedAmount);
    }

    public void OnSliderChanged(float value)
    {
        long limit = TradeLimit;

        // 0~1000을 거래 가능 금액에 비례해서 연산
        long amount = (long)((decimal)value / SliderSteps * limit);

        SetSelectedAmount(amount);
    }

    private void SetSelectedAmount(long amount)
    {
        long limit = TradeLimit;

        selectedAmount = Math.Max(0L, Math.Min(amount, limit));

        amountText.text = $"{selectedAmount:N0}원";

        float sliderValue = limit == 0 ? 0f : (float)((decimal)selectedAmount / limit * SliderSteps);

        amountSlider.SetValueWithoutNotify(sliderValue);

        bool canAct = GameManager.Instance.CanAct;

        amountSlider.interactable = canAct && limit > 0;
        confirmButton.interactable = canAct && selectedAmount > 0;
    }

    public void ConfirmTrade()
    {
        bool success = AssetManager.Instance.TryTrade(assetType, selectedSector, isBuying, selectedAmount, out string message);

        if (success)
        {
            selectedAmount = 0;
        }

        Refresh();
        feedbackText.text = message;
    }

    /// <summary>
    /// 종목 버튼에서 0~3을 전달.
    /// </summary>
    /// <param name="sectorIndex"></param>
    public void SelectSector(int sectorIndex)
    {
        if (assetType == AssetType.Bank)
            return;

        if (sectorIndex < 0 || sectorIndex >= MarketSectorUtility.SectorCount)
        {
            return;
        }

        if (!GameManager.Instance.CanAct)
            return;

        // 안내 중에는 설명 대상과 무관한 종목 변경을 막음.
        if (SpotlightTutorialManager.Instance != null && SpotlightTutorialManager.Instance.IsBusy)
        {
            return;
        }

        selectedSector = (MarketSector)sectorIndex;

        // 이전 종목에서 입력한 금액을 새 종목 거래에 재사용하지 않음.
        selectedAmount = 0;
        keypadRoot.SetActive(false);
        feedbackText.text = "";

        Refresh();
    }

    private void RefreshSectorButtons()
    {
        if (assetType == AssetType.Bank)
            return;

        for (int i = 0; i < sectorButtons.Length; i++)
        {
            sectorButtons[i].image.color = i == (int)selectedSector ? selectedSectorColor : normalSectorColors[i];
        }
    }

    /// <summary>
    /// 현재 거래 패널의 상품 유형과 선택 종목으로 차트 오픈.
    /// </summary>
    public void OpenMarketChart()
    {
        // 은행 예금은 고정 이자이므로, 투자상품 차트 대상이 아님.
        if (assetType == AssetType.Bank)
            return;

        if (GameManager.Instance == null ||
            !GameManager.Instance.CanAct)
        {
            return;
        }

        if (SpotlightTutorialManager.Instance != null &&
            SpotlightTutorialManager.Instance.IsBusy)
        {
            return;
        }

        if (marketChartPanel == null)
        {
            Debug.LogError("수익률 차트 패널을 연결하세요.", this);
            return;
        }

        // 키패드가 차트 뒤에 남지 않게 닫음.
        keypadRoot.SetActive(false);

        marketChartPanel.Open(assetType, selectedSector);
    }

    public void OpenKeypad()
    {
        if(!GameManager.Instance.CanAct) return;

        amountInput.SetTextWithoutNotify(selectedAmount.ToString(CultureInfo.InvariantCulture));

        keypadErrorText.text = "";
        keypadRoot.SetActive(true);
        amountInput.ActivateInputField();
    }

    public void AppendDigit(string digit)
    {
        if(digit.Length != 1 || digit[0] < '0' || digit[0] > '9')
        {
            return;
        }

        string current = amountInput.text;

        if(current == "0")
        {
            current = "";
        }

        if(current.Length >= 19) return;

        amountInput.text = current + digit;
    }

    public void Backspace()
    {
        string current = amountInput.text;

        if(current.Length > 0)
        {
            amountInput.text = current.Substring(0, current.Length -1);
        }
    }

    public void ClearInput()
    {
        amountInput.text = "";
    }

    public void ApplyInput()
    {
        bool valid = long.TryParse(amountInput.text, NumberStyles.None, CultureInfo.InvariantCulture, out long amount);

        if(!valid || amount <= 0)
        {
            keypadErrorText.text = "1원 이상의 정수를 입력하세요.";
            return;
        }

        if(amount > TradeLimit)
        {
            keypadErrorText.text = $"최대 {TradeLimit:N0}원까지 입력할 수 있습니다.";
            return;
        }

        SetSelectedAmount(amount);
        keypadRoot.SetActive(false);
    }

    public void CancelInput()
    {
        keypadRoot.SetActive(false);
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }
}
