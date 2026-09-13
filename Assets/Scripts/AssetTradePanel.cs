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

    [Header("Keypad")]
    [SerializeField] private GameObject keypadRoot; // 키패드
    [SerializeField] private TMP_InputField amountInput; // 입력된 금액
    [SerializeField] private TMP_Text keypadErrorText;  // 투자 가능 금액 표시

    private const int SliderSteps = 1000; //슬라이드 진행 정도

    private bool isBuying = true;
    private long selectedAmount;

    /*
    function Zone
    */
    private long TradeLimit
    {
        get
        {
            // isBuying이 True일 경우 가용 현금을, False일 경우 자산 타입에 따라 해당 자산의 보유 액수를 limit으로 지정
            long limit = isBuying ? GameManager.Instance.availableCash : AssetManager.Instance.GetBalance(assetType);

            return Math.Max(0L, limit);
        }
    }

    private void Awake()
    {
        amountSlider.minValue = 0;
        amountSlider.maxValue = SliderSteps;
        // 슬라이더에서 선택할 수 있는 값을 정수로만 제한.
        amountSlider.wholeNumbers = true;
    }

    private void OnEnable()
    {
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
        string assetName = assetType == AssetType.Bank ? "은행" : assetType == AssetType.Stock ? "주식" : "레버리지";
        // 자산군에 따른 액션 단어 분류
        string action = assetType == AssetType.Bank ? (isBuying ? "예금" : "출금") : (isBuying ? "매수" : "매도");

        titleText.text = $"{assetName} - {action}";

        balanceText.text = $"보유 현금: {GameManager.Instance.availableCash:N0}원\n" + $"보유 자산: {AssetManager.Instance.GetBalance(assetType):N0}원";

        limitText.text = $"{action} 가능 금액: {TradeLimit:N0}원";
        confirmText.text = action;

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
        bool success = AssetManager.Instance.TryTrade(assetType, isBuying, selectedAmount, out string message);

        if (success)
        {
            selectedAmount = 0;
        }

        Refresh();
        feedbackText.text = message;
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
