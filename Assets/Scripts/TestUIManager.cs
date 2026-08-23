using UnityEngine;
using UnityEngine.UI;

public class TestUIManager : MonoBehaviour
{
    //테스트용 고정 금액(100만원) -> 불변성을 지닌 변수로 선언.
    private readonly long testAmount = 1000000;

    //주식 매수/매도 주문 버튼 연결용
    public void OnClickBuyStock()
    {
        AssetManager.Instance.BuyAsset(AssetType.Stock, testAmount);
    }
    public void OnClickSellStock()
    {
        AssetManager.Instance.SellAsset(AssetType.Stock, testAmount);
    }

    //레버리지 매수/매도 주문 버튼 연결용
    public void OnClickBuyLeverage()
    {
        AssetManager.Instance.BuyAsset(AssetType.Leverage, testAmount);
    }
    public void OnClickSellLeverage()
    {
        AssetManager.Instance.SellAsset(AssetType.Leverage, testAmount);
    }

    //소비 시스템 연결 용
    public void OnClickBuyDeliveryFood()
    {
        ShopManager.Instance.BuyItem(ShopItemType.DeliveryFood);
    }
    public void OnClickBuyHocance()
    {
        ShopManager.Instance.BuyItem(ShopItemType.Hocance);
    }
}
