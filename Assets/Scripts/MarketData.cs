using System;

// 게임의 AssetType과 분리한 데이터 테이블 식별자
public enum MarketType
{
    Nasdaq,
    Leverage
}

// readonly 월별 데이터 구조
public readonly struct MarketData
{
    public DateTime Month { get; }
    public decimal ReturnRate { get; }

    public MarketData(DateTime month, decimal returnRate)
    {
        Month = month;
        ReturnRate = returnRate;
    }
}
