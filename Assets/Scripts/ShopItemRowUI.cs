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
        priceText.text = $"비용 : {item.Price:N0}원";
        effectText.text = item.Description;

        purchaseButton.onClick.RemoveListener(OnClickPurchase);
        purchaseButton.onClick.AddListener(OnClickPurchase);

        Refresh();
    }

    public void Refresh()
    {
        bool limited =
            ShopManager.Instance.HasReachedPurchaseLimit(item);

        purchaseButton.interactable = !limited;

        purchaseButtonText.text = limited ? (item.Category == ShopCategory.Prestige ? "보유 중" : "구매 완료") : "구매\n하기";
    }

    private void OnClickPurchase()
    {
        onSelected?.Invoke(item);
    }
}
