using System.Collections.Generic;

public enum ShopCategory
{
    Consumable,     // 일회성 소비
    Prestige        // 과시성 소비
}

// shop_items.json 기준 분류
public enum ShopEffectType
{
    FATIGUE,
    AP,
    FREE_LOW_GRADE_INFO,
    CAREER_EXP,
    INVESTMENT_RETURN_BONUS,
    MAX_AP,
    MAX_FATIGUE,
    HAPPY_ENDING_CONDITION
}

public enum ShopEffectTrigger
{
    IMMEDIATE,
    PERMANENT,
    PER_TURN,
    ON_PURCHASE
}

public sealed class ShopEffectData
{
    // 자동 구현 프로퍼티 - { get; } 형태
    public ShopEffectType Type { get; }     // 버프 효과 타입
    public decimal Value { get; }           // 금액
    public ShopEffectTrigger Trigger { get; }   // 버프 동작

    /// <summary>
    /// 생성자
    /// </summary>
    /// <param name="type"></param>
    /// <param name="value"></param>
    /// <param name="trigger"></param>
    public ShopEffectData(ShopEffectType type, decimal value, ShopEffectTrigger trigger)
    {
        Type = type;
        Value = value;
        Trigger = trigger;
    }
}

public sealed class ShopItemData
{
    // 자동 구현 프로퍼티 - { get; } 형태
    public string Id { get; }       // 상품 ID
    public string DisplayName { get; }  // UI 상 이름
    public ShopCategory Category { get; }   // 카테고리
    public long Price { get; }      // 가격
    public int PurchaseLimit { get; }   // 구매 제한 - 과시성 소비는 각 상품별 1회만 구매가 가능
    public string Description { get; }  // 상품 설명
    public IReadOnlyList<ShopEffectData> Effects { get; }   // 효과 - 외부에서 값을 변경할 수 없게 IReadOnlyList 인터페이스 사용

    /// <summary>
    /// 생성자
    /// </summary>
    /// <param name="id"></param>
    /// <param name="displayName"></param>
    /// <param name="category"></param>
    /// <param name="price"></param>
    /// <param name="purchaseLimit"></param>
    /// <param name="description"></param>
    /// <param name="effects"></param>
    public ShopItemData(string id, string displayName, ShopCategory category, long price, int purchaseLimit, string description, List<ShopEffectData> effects)
    {
        Id = id;
        DisplayName = displayName;
        Category = category;
        Price = price;
        PurchaseLimit = purchaseLimit;
        Description = description;

        Effects = new List<ShopEffectData>(effects).AsReadOnly();
    }

    /// <summary>
    /// effect 값 추가 함수
    /// </summary>
    /// <param name="type"></param>
    /// <param name="trigger"></param>
    /// <returns></returns>
    public decimal GetEffectValue(ShopEffectType type, ShopEffectTrigger trigger)
    {
        decimal result = 0;

        foreach(ShopEffectData effect in Effects)
        {
            if(effect.Type == type && effect.Trigger == trigger)
                result += effect.Value;
        }

        return result;
    }

    // 각 버프 별 내용
    public float ImmediateFatigueChange =>
        (float)GetEffectValue(
            ShopEffectType.FATIGUE,
            ShopEffectTrigger.IMMEDIATE);

    public float MonthlyFatigueChange =>
        (float)GetEffectValue(
            ShopEffectType.FATIGUE,
            ShopEffectTrigger.PER_TURN);

    public decimal InvestmentBonusRate =>
        GetEffectValue(
            ShopEffectType.INVESTMENT_RETURN_BONUS,
            ShopEffectTrigger.PERMANENT);

    public float MaxStressBonus =>
        (float)GetEffectValue(
            ShopEffectType.MAX_FATIGUE,
            ShopEffectTrigger.ON_PURCHASE);

    public int MaxAPBonus =>
        (int)GetEffectValue(
            ShopEffectType.MAX_AP,
            ShopEffectTrigger.ON_PURCHASE);

    public int MonthlyFreeLowRumors =>
        (int)GetEffectValue(
            ShopEffectType.FREE_LOW_GRADE_INFO,
            ShopEffectTrigger.PERMANENT);

    public int MonthlyJobExperience =>
        (int)GetEffectValue(
            ShopEffectType.CAREER_EXP,
            ShopEffectTrigger.PER_TURN);

    public bool RequiredForHappyEnding =>
        GetEffectValue(
            ShopEffectType.HAPPY_ENDING_CONDITION,
            ShopEffectTrigger.PERMANENT) > 0;
}