using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ShopPanelUI : MonoBehaviour
{
    [Serializable]
    private sealed class ItemIcon
    {
        [SerializeField] private string itemId;
        [SerializeField] private Sprite sprite;

        public string ItemId => itemId;
        public Sprite Sprite => sprite;
    }

    [Header("Product List")]
    [SerializeField] private TMP_Text cashText;
    [SerializeField] private ScrollRect productScroll;
    [SerializeField] private Transform content;
    [SerializeField] private ShopItemRowUI rowPrefab;

    [Header("Tab")]
    [SerializeField] private Image consumableTabBackground;
    [SerializeField] private Image prestigeTabBackground;
    [SerializeField] private Color selectedColor = Color.white;
    [SerializeField] private Color normalColor = Color.gray;

    [Header("Purchase Panel")]
    [SerializeField] private ShopPurchasePanelUI purchasePanel;

    [Header("Product Icon")]
    [SerializeField] private List<ItemIcon> itemIcons = new List<ItemIcon>();

    private readonly List<ShopItemRowUI> rows = new List<ShopItemRowUI>();

    public void Open()
    {
        if (GameManager.Instance == null || !GameManager.Instance.CanAct)
            return;

        if (DataManager.Instance == null || !DataManager.Instance.IsShopLoaded || ShopManager.Instance == null)
        {
            Debug.LogError("상점 데이터와 Manager 초기화를 확인하세요.");
            return;
        }

        gameObject.SetActive(true);
        transform.SetAsLastSibling();

        purchasePanel.Close();
        ShowConsumables();
    }

    public void Close()
    {
        purchasePanel.Close();
        gameObject.SetActive(false);
    }

    public void ShowConsumables()
    {
        ShowCategory(ShopCategory.Consumable);
    }

    public void ShowPrestige()
    {
        ShowCategory(ShopCategory.Prestige);
    }

    private void ShowCategory(ShopCategory category)
    {
        purchasePanel.Close();

        foreach (ShopItemRowUI row in rows)
        {
            row.gameObject.SetActive(false);
            Destroy(row.gameObject);
        }

        rows.Clear();

        foreach (ShopItemData item in DataManager.Instance.ShopItems)
        {
            if (item.Category != category)
                continue;

            ShopItemRowUI row = Instantiate(rowPrefab, content, false);

            row.Bind(item, FindIcon(item.Id), OpenPurchase);
            row.gameObject.SetActive(true);
            rows.Add(row);
        }

        consumableTabBackground.color =
            category == ShopCategory.Consumable
                ? selectedColor : normalColor;

        prestigeTabBackground.color =
            category == ShopCategory.Prestige
                ? selectedColor : normalColor;

        Refresh();

        Canvas.ForceUpdateCanvases();
        productScroll.StopMovement();
        productScroll.verticalNormalizedPosition = 1f;
    }

    private Sprite FindIcon(string id)
    {
        foreach (ItemIcon entry in itemIcons)
        {
            if (entry != null && entry.ItemId == id)
                return entry.Sprite;
        }

        return null;
    }

    private void OpenPurchase(ShopItemData item)
    {
        purchasePanel.Open(item, FindIcon(item.Id), this);
    }

    public void Refresh()
    {
        cashText.text =
            $"보유 현금: {GameManager.Instance.availableCash:N0}원";

        foreach (ShopItemRowUI row in rows)
            row.Refresh();
    }
}
