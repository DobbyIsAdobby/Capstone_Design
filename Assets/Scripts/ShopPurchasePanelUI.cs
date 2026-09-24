using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ShopPurchasePanelUI : MonoBehaviour
{
    [Header("Product")]
    [SerializeField] private Image productIcon;
    [SerializeField] private TMP_Text productNameText;
    [SerializeField] private TMP_Text effectText;
    [SerializeField] private TMP_Text priceText;
    [SerializeField] private TMP_Text cashText;

    [Header("Maintenance")]
    [SerializeField] private GameObject maintenanceRow;
    [SerializeField] private TMP_Text maintenanceText;

    [Header("Installment")]
    [SerializeField] private GameObject installmentSection;
    [SerializeField] private TMP_Text paymentPeriodText;
    [SerializeField] private Slider paymentPeriodSlider;
    [SerializeField] private TMP_Text maxPeriodText;
    [SerializeField] private TMP_Text paymentScheduleText;

    [Header("Payment Summary")]
    [SerializeField] private TMP_Text paymentSummaryText;
    [SerializeField] private TMP_Text feedbackText;
    [SerializeField] private Button purchaseButton;
    [SerializeField] private TMP_Text purchaseButtonText;
    [SerializeField] private Color successColor = new Color(0.1f, 0.5f, 0.2f);
    [SerializeField] private Color errorColor = Color.red;

    private ShopItemData item;
    private ShopPanelUI owner;
    private int selectedMonths;

    public void Open(ShopItemData data, Sprite icon, ShopPanelUI shopPanel)
    {
        item = data;
        owner = shopPanel;
        selectedMonths = 0;

        gameObject.SetActive(true);
        transform.SetAsLastSibling();

        productIcon.sprite = icon;
        productIcon.enabled = icon != null;

        productNameText.text = item.DisplayName;
        effectText.text = item.Description + GetPendingEffectNote(item);
        priceText.text = $"{item.Price:N0}원";

        bool prestige = item.Category == ShopCategory.Prestige;

        maintenanceRow.SetActive(prestige);
        installmentSection.SetActive(prestige);

        int maxMonths =
            ShopManager.Instance.GetMaxInstallmentMonths(item);

        paymentPeriodSlider.wholeNumbers = true;
        paymentPeriodSlider.minValue = 0;
        paymentPeriodSlider.maxValue = Mathf.Max(1, maxMonths);
        paymentPeriodSlider.SetValueWithoutNotify(0);

        maxPeriodText.text = maxMonths > 0 ? $"{maxMonths}개월" : "일시불 전용";

        feedbackText.text = "";
        Refresh();
    }

    public void Close()
    {
        item = null;
        owner = null;
        gameObject.SetActive(false);
    }

    public void OnPaymentPeriodChanged(float value)
    {
        if (item == null)
            return;

        int maxMonths = ShopManager.Instance.GetMaxInstallmentMonths(item);

        selectedMonths = Mathf.Clamp(Mathf.RoundToInt(value), 0, maxMonths);

        feedbackText.text = "";
        Refresh();
    }

    public void OnClickPurchase()
    {
        if (item == null)
            return;

        bool success = ShopManager.Instance.TryBuy(
            item.Id, selectedMonths, out string message);

        feedbackText.text = message;
        feedbackText.color = success ? successColor : errorColor;

        owner.Refresh();
        Refresh();
    }

    private void Refresh()
    {
        ShopManager shop = ShopManager.Instance;
        GameManager game = GameManager.Instance;

        cashText.text = $"{game.availableCash:N0}원";

        bool configured = shop.TryGetPaymentSettings(
            item, out bool allowInstallment, out long maintenance);

        maintenanceText.text = configured ? $"{maintenance:N0}원" : "설정 필요";

        bool limited = shop.HasReachedPurchaseLimit(item);

        // 설정 오류도 클릭 시 구매 패널에서 안내.
        purchaseButton.interactable = !limited && game.CanAct;

        purchaseButtonText.text = limited ? (item.Category == ShopCategory.Prestige ? "보유 중" : "구매 완료") : "구매하기";

        paymentPeriodSlider.interactable = configured && allowInstallment && !limited;

        paymentPeriodText.text = selectedMonths == 0 ? "결제 방식: 일시불" : $"결제 방식: {selectedMonths}개월 할부";

        long immediateCost = selectedMonths == 0 ? item.Price : 0;
        paymentSummaryText.text = $"{immediateCost:N0}원";

        if (!configured)
        {
            paymentScheduleText.text = "상품의 결제 규칙을 확인하세요.";
            return;
        }

        if (!allowInstallment)
        {
            paymentScheduleText.text = "이 상품은 일시불로만 구매할 수 있습니다.";
            return;
        }

        if (selectedMonths == 0)
        {
            paymentScheduleText.text = "구매 즉시 상품 가격 전액을 결제합니다.";
            return;
        }

        long monthly = shop.GetMonthlyPayment(item, selectedMonths);
        long last = shop.GetLastPayment(item, selectedMonths);

        paymentScheduleText.text = $"다음 달 마감부터 {selectedMonths}회 납부\n" + $"회당 {monthly:N0}원";

        if (last != monthly)
            paymentScheduleText.text += $"\n마지막 회차 {last:N0}원";
    }

    private static string GetPendingEffectNote(ShopItemData data)
    {
        bool ap = false;
        bool information = false;
        bool career = false;

        foreach (ShopEffectData effect in data.Effects)
        {
            if (effect.Type == ShopEffectType.AP || effect.Type == ShopEffectType.MAX_AP)
                ap = true;

            if (effect.Type == ShopEffectType.FREE_LOW_GRADE_INFO)
                information = true;

            if (effect.Type == ShopEffectType.CAREER_EXP)
                career = true;
        }

        string note = "";

        if (ap) note += "\n AP 효과 연결 예정";
        if (information) note += "\n 정보 시스템 연결 예정";
        if (career) note += "\n 직급 경험치 연결 예정";

        return note;
    }
}
