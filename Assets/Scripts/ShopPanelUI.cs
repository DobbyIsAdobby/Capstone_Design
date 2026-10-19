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
    [SerializeField] private Button consumableTabButton;
    [SerializeField] private Button prestigeTabButton;
    [SerializeField] private TMP_Text prestigeTabText;

    [Header("Category Appearance")]
    [SerializeField] private Image viewportBackground;

    // 선택된 탭 배경이 올라가는 거리입니다.
    [SerializeField] private float selectedTabOffsetY = 30f;

    [SerializeField] private float selectedTabHeightIncrease = 20f;

    // #384E6B
    [SerializeField] private Color prestigeViewportColor = new Color32(56, 78, 107, 255);

    // #EDBB50
    [SerializeField] private Color prestigeSelectedTextColor = new Color32(237, 187, 80, 255);

    private bool tabDefaultsCached;

    private Vector2 consumableOriginalPosition;
    private Vector2 prestigeOriginalPosition;
    private Vector2 consumableButtonOriginalPosition;
    private Vector2 prestigeButtonOriginalPosition;

    private Vector2 consumableOriginalSize;
    private Vector2 prestigeOriginalSize;

    private Color consumableOriginalColor;
    private Color prestigeOriginalColor;
    private Color viewportOriginalColor;
    private Color prestigeTextOriginalColor;

    private Sprite consumableOriginalSprite;
    private Sprite prestigeOriginalSprite;
    private Sprite consumableSelectedSprite;
    private Sprite prestigeSelectedSprite;


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

        // 상품 목록과 동일한 카테고리를 기준으로 선택 외형을 갱신.
        ApplyCategoryAppearance(category);

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

    /// <summary>
    /// 선택 효과를 적용하기 전의 위치, 색상, 이미지를 한번만 저장.
    /// 상점을 다시 열어도 그대로있도록
    /// </summary>
    private void CacheTabDefaults()
    {
        if (tabDefaultsCached)
            return;

        consumableOriginalPosition = consumableTabBackground.rectTransform.anchoredPosition;

        prestigeOriginalPosition = prestigeTabBackground.rectTransform.anchoredPosition;

        consumableButtonOriginalPosition = ((RectTransform)consumableTabButton.transform).anchoredPosition;

        prestigeButtonOriginalPosition = ((RectTransform)prestigeTabButton.transform).anchoredPosition;

        consumableOriginalSize = consumableTabBackground.rectTransform.sizeDelta;
        prestigeOriginalSize = prestigeTabBackground.rectTransform.sizeDelta;

        consumableOriginalColor = consumableTabBackground.color;
        prestigeOriginalColor = prestigeTabBackground.color;
        viewportOriginalColor = viewportBackground.color;
        prestigeTextOriginalColor = prestigeTabText.color;

        consumableOriginalSprite = consumableTabBackground.sprite;
        prestigeOriginalSprite = prestigeTabBackground.sprite;

        // Button의 Selected Sprite에 연결된 이미지.
        consumableSelectedSprite = consumableTabButton.spriteState.selectedSprite;

        prestigeSelectedSprite = prestigeTabButton.spriteState.selectedSprite;

        // 클릭 포커스 변경으로 이미지가 자동 복구되지 않도록 함.
        // 이후 선택 이미지는 아래 ApplyTabAppearance에서 관리.
        consumableTabButton.transition = Selectable.Transition.None;
        prestigeTabButton.transition = Selectable.Transition.None;

        tabDefaultsCached = true;
    }

    /// <summary>
    /// 현재 카테고리에 맞게 Viewport와 탭의 외형을 적용.
    /// </summary>
    /// <param name="category"></param>
    private void ApplyCategoryAppearance(ShopCategory category)
    {
        CacheTabDefaults();

        bool isPrestige = category == ShopCategory.Prestige;

        viewportBackground.color = isPrestige ? prestigeViewportColor : viewportOriginalColor;

        prestigeTabText.color = isPrestige ? prestigeSelectedTextColor : prestigeTextOriginalColor;

        ApplyTabAppearance(
            consumableTabBackground,
            !isPrestige,
            consumableOriginalPosition,
            consumableOriginalSize,
            consumableOriginalColor,
            consumableOriginalSprite,
            consumableSelectedSprite);

        ApplyTabAppearance(
            prestigeTabBackground,
            isPrestige,
            prestigeOriginalPosition,
            prestigeOriginalSize,
            prestigeOriginalColor,
            prestigeOriginalSprite,
            prestigeSelectedSprite);

        // 배경과 동일하게 버튼도 이동.
        // 버튼의 자식 텍스트와 클릭 영역이 함께 이동.
        RectTransform consumableButtonRect = (RectTransform)consumableTabButton.transform;

        RectTransform prestigeButtonRect = (RectTransform)prestigeTabButton.transform;

        // 원래 위치를 기준으로 계산하므로 반복 클릭해도 이동량이 누적되지 않음.
        consumableButtonRect.anchoredPosition = consumableButtonOriginalPosition + (!isPrestige ? Vector2.up * selectedTabOffsetY : Vector2.zero);

        prestigeButtonRect.anchoredPosition = prestigeButtonOriginalPosition + (isPrestige ? Vector2.up * selectedTabOffsetY : Vector2.zero);
    }

    /// <summary>
    /// 선택: 원래 위치에서 Y +15, 알파 1, 높이 +20, 선택 이미지.
    /// 해제: 저장한 원래 위치와 높이, 색상과 알파, 기본 이미지.
    /// </summary>
    /// <param name="background"></param>
    /// <param name="selected"></param>
    /// <param name="originalPosition"></param>
    /// <param name="originalColor"></param>
    /// <param name="originalSprite"></param>
    /// <param name="selectedSprite"></param>
    private void ApplyTabAppearance(Image background, bool selected, Vector2 originalPosition, Vector2 originalSize, Color originalColor, Sprite originalSprite, Sprite selectedSprite)
    {
        RectTransform rect = background.rectTransform;

        // 선택 시 배경을 Y축으로 15 올림
        // 해제하면 원래 위치로 되돌아감
        rect.anchoredPosition = originalPosition + (selected ? Vector2.up * selectedTabOffsetY : Vector2.zero);


        // 기본 크기를 기준으로 계산해 반복 클릭 시 값 누적을 방지.
        rect.sizeDelta = new Vector2(originalSize.x, originalSize.y + (selected ? selectedTabHeightIncrease : 0f));

        Color color = originalColor;
        if (selected)
            color.a = 1f;

        background.color = color;

        Sprite targetSprite = selected && selectedSprite != null ? selectedSprite : originalSprite;

        background.sprite = targetSprite;
        background.overrideSprite = targetSprite;
    }
}
