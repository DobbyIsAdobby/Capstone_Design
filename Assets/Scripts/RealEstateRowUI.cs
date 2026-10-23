using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RealEstateRowUI : MonoBehaviour
{
    [Header("Property")]
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text priceText;
    [SerializeField] private TMP_Text rentText;
    [SerializeField] private TMP_Text stateText;
    [SerializeField] private Button detailButton;

    private int propertyIndex;
    private Action<int> onSelected;

    /// <summary>
    /// 이 행에 표시할 부동산과 상세 화면을 여는 함수를 전달받음.
    /// </summary>
    /// <param name="index"></param>
    /// <param name="sprite"></param>
    /// <param name="callback"></param>
    public void Bind(int index, Sprite sprite, Action<int> callback)
    {
        propertyIndex = index;
        onSelected = callback;

        RealEstateManager manager = RealEstateManager.Instance;
        RealEstateDefinition definition = manager.Definition(index);
        RealEstatePropertyState state = manager.Property(index);

        icon.sprite = sprite;
        icon.enabled = sprite != null;

        nameText.text = definition.name;
        priceText.text = $"현재 시세 : {manager.Price(index):N0}원";

        rentText.text = $"월 임대료 : " + $"{manager.Rent(index, GameManager.Instance.currentMonth):N0}원";

        if (!state.owned)
            stateText.text = "매입 가능";
        else if (state.saleRequested)
            stateText.text = "매각 대기 중";
        else if (state.principal > 0)
            stateText.text = "보유 중 · 대출 있음";
        else
            stateText.text = "보유 중";

        // 재연결할 때 클릭 이벤트가 중복 등록되지 않도록 함.
        detailButton.onClick.RemoveListener(OnClickDetail);
        detailButton.onClick.AddListener(OnClickDetail);
    }

    private void OnClickDetail()
    {
        onSelected?.Invoke(propertyIndex);
    }

    private void OnDestroy()
    {
        if (detailButton != null)
            detailButton.onClick.RemoveListener(OnClickDetail);
    }
}
