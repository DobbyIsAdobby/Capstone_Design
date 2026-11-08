using System;

public enum GameEventType
{
    TurnStarted,

    OvertimeUsed,
    ShopOpened,
    FatigueRecoveredByShop,

    InvestmentPurchased,
    InformationPurchased,

    LoanCreated,
    JobPromoted
}

public static class GameEventBus
{
    public static event Action<GameEventType, int> OnEvent;

    public static void Publish(GameEventType type, int value = 0)
    {
        OnEvent?.Invoke(type, value);
    }
}