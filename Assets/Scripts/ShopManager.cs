using UnityEngine;

//소비 항목 분류 -> 추후 더 세부적(일회성/과시성 소비)으로 구분지을 계획
public enum ShopItemType
{
    DeliveryFood,
    DrinkingParty,
    Hocance,
    LuxuryWatch
}

public class ShopManager : MonoBehaviour
{
    /*
    Inspector Zone
    */
    public static ShopManager Instance;

    /*
    function Zone
    */

    //싱글톤 패턴
    private void Awake()
    {
        if(Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    /// <summary>
    /// UI [구매하기] 버튼을 통해 아이템을 소비할 때 호출할 함수
    /// </summary>
    /// <param name="itemType"></param>
    public void BuyItem(ShopItemType itemType)
    {
        long cost = 0;
        float stressRelief = 0f;
        string itemName = "";

        // 아이템별 가격 및 스트레스 감소량 세팅 (프로토타입 밸런싱용)
        switch (itemType)
        {
            case ShopItemType.DeliveryFood:
                itemName = "배달 음식";
                cost = 30000;      // 3만 원
                stressRelief = 15f; // 스트레스 15% 감소
                break;
            case ShopItemType.DrinkingParty:
                itemName = "친구들과 술자리";
                cost = 100000;     // 10만 원
                stressRelief = 40f; // 스트레스 40% 감소
                break;
            case ShopItemType.Hocance:
                itemName = "5성급 호캉스";
                cost = 500000;     // 50만 원
                stressRelief = 100f; // 스트레스 전액 탕감
                break;
            case ShopItemType.LuxuryWatch:
                itemName = "명품 시계";
                cost = 5000000;    // 500만 원
                stressRelief = 100f;
                // TODO: 추후 GameManager에 '초기 스트레스 증가량 감소' 같은 영구 버프 로직 추가 연동 -> 해당부분 구체적 기획 필요함.
                break;
        }

        // 1. 현금 확인
        if(GameManager.Instance.availableCash < cost)
        {
            Debug.LogWarning($"{itemName}을 결제하기엔 보유 현금이 부족합니다. (필요 금액 : {cost:N0}원)");
            return;
        }

        // 2. 결제 진행 (현금 차감)
        GameManager.Instance.availableCash -= cost;

        // 3. 스트레스 감소 적용(0 밑으로 떨어지지 않도록 Mathf.Max 사용)
        GameManager.Instance.stressLevel = Mathf.Max(0, GameManager.Instance.stressLevel - stressRelief);

        Debug.Log($"[구매 완료] {itemName} 결제 (-{cost:N0}원). 현재 스트레스 : {GameManager.Instance.stressLevel}%");

        if(UIManager.Instance != null)
        {
            UIManager.Instance.RefreshUI();
        }

        // 추후 과시성 소비의 경우 할부 시스템을 구현해야함.
    }
}
