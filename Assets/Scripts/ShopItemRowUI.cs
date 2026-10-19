using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ShopItemRowUI : MonoBehaviour
{
    [SerializeField] private Image productIcon;             // 아이템 이미지 아이콘
    [SerializeField] private TMP_Text productNameText;      // 아이템 이름
    [SerializeField] private TMP_Text priceText;            // 아이템 가격
    [SerializeField] private TMP_Text effectText;           // 아이템 효과
    [SerializeField] private Button purchaseButton;         // 아이템 구매 
    [SerializeField] private TMP_Text purchaseButtonText;   // 아이템 구매 

    [Header("Prestige Appearance")]
    [SerializeField] private GameObject prestigeDecoration;
    [SerializeField] private Image purchaseButtonBackground;

    // #D8BF78
    [SerializeField] private Color prestigePurchaseColor = new Color32(216, 191, 120, 255);

    [SerializeField] private Color prestigePurchaseTextColor = Color.white;

    private bool appearanceCached;
    private Color originalPurchaseColor;
    private Color originalPurchaseTextColor;

    private ShopItemData item;
    private Action<ShopItemData> onSelected;

    /// <summary>
    /// Unity UI 툴킷 데이터 바인딩 사용 -> 이름으로 UI 오브젝트를 찾아와 자동 할당 가능.
    /// </summary>
    /// <param name="data"></param>
    /// <param name="icon"></param>
    /// <param name="callback"></param>
    public void Bind(ShopItemData data, Sprite icon, Action<ShopItemData> callback)
    {
        item = data;
        onSelected = callback;

        productIcon.sprite = icon;
        productIcon.enabled = icon != null;

        productNameText.text = item.DisplayName;
        //priceText.text = $"비용 : {item.Price:N0}원";
        effectText.text = item.Description;

        purchaseButton.onClick.RemoveListener(OnClickPurchase);
        purchaseButton.onClick.AddListener(OnClickPurchase);

        // 상품 데이터를 연결할 때 해당 상품의 외형도 적용.
        ApplyItemAppearance();

        Refresh();
    }

    public void Refresh()
    {
        // 목록이 갱신될 때도 현재 턴 가격을 표시. - Bind에서는 더이상 쓰지 않음 - 턴에 따라 실시간으로 가격이 바뀌기 때문.
        long price = ShopManager.Instance.GetPurchasePrice(item);
        priceText.text = $"비용 : {price:N0}원";

        bool limited =
            ShopManager.Instance.HasReachedPurchaseLimit(item);

        purchaseButton.interactable = !limited;

        purchaseButtonText.text = limited ? (item.Category == ShopCategory.Prestige ? "보유 중" : "구매 완료") : "구매\n";
    }

    private void OnClickPurchase()
    {
        onSelected?.Invoke(item);
    }

    /// <summary>
    /// 과시성 상품이면 장식을 표시하고 구매 버튼에 전용 색상을 적용.
    /// 일회성 상품이면 기존 모습으로.
    /// </summary>
    private void ApplyItemAppearance()
    {
        if (!appearanceCached)
        {
            originalPurchaseColor = purchaseButtonBackground.color;
            originalPurchaseTextColor = purchaseButtonText.color;
            appearanceCached = true;
        }

        // SHOP_006~SHOP_010이 해당하는 과시성 분류로 판정.
        // 상품이 추가되어도 같은 분류라면 자동으로 같은 외형을 사용.
        bool isPrestige = item.Category == ShopCategory.Prestige;

        prestigeDecoration.SetActive(isPrestige);

        purchaseButtonBackground.color = isPrestige ? prestigePurchaseColor : originalPurchaseColor;

        purchaseButtonText.color = isPrestige ? prestigePurchaseTextColor : originalPurchaseTextColor;
    }
}
