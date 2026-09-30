using System;

/// <summary>
/// 주식 수익률 기준 다른 상품의 수익률을 매 턴마다 생성하고 검증함. 
/// 동일한 배율 범위를 지정 및 사용하여 한곳에서 관리할 수 있게 제작
/// </summary>
public class MarketReturnRules
{
    public const decimal LeverageMin = 1.8m;
    public const decimal LeverageMax = 2.2m;

    public const decimal StockInverseMin = -1.3m;
    public const decimal StockInverseMax = -1.1m;

    public const decimal LeverageInverseMin = -2.2m;
    public const decimal LeverageInverseMax = -1.8m;

    /// <summary>
    /// 한 턴에 사용할 세 배율을 각각 추출하여 수익률을 확정
    /// </summary>
    /// <param name="stockReturn"></param>
    /// <param name="multiplierRandom"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public static MonthlyMarketRates Create(decimal stockReturn, Random multiplierRandom)
    {
        if (multiplierRandom == null)
            throw new ArgumentNullException(nameof(multiplierRandom));

        // 상품마다 별도로 추출하므로 같은 턴에도 배율이 서로 다름
        decimal leverageMultiplier = Sample(multiplierRandom, LeverageMin, LeverageMax);

        decimal stockInverseMultiplier = Sample(multiplierRandom, StockInverseMin, StockInverseMax);

        decimal leverageInverseMultiplier = Sample(multiplierRandom, LeverageInverseMin, LeverageInverseMax);

        return new MonthlyMarketRates(stockReturn, stockReturn * leverageMultiplier, stockReturn * stockInverseMultiplier, stockReturn * leverageInverseMultiplier);
    }

    /// <summary>
    /// 지정한 범위에서 균등하게 배율 추출. 
    /// 수익률은 여기서 반올림하지 않고, 화면 표시 단계에서만 반올림
    /// </summary>
    /// <param name="random"></param>
    /// <param name="min"></param>
    /// <param name="max"></param>
    /// <returns></returns>
    private static decimal Sample(Random random, decimal min, decimal max)
    {
        decimal ratio = (decimal)random.NextDouble();
        return min + (max - min) * ratio;
    }

    /// <summary>
    /// 네 상품의 수익률이 현재 배율 규칙을 만족하는지 검사.
    /// </summary>
    /// <param name="rates"></param>
    /// <returns></returns>
    public static bool IsValid(MonthlyMarketRates rates)
    {
        if (rates.Stock <= -1m)
            return false;

        return IsWithinRange(
                   rates.Stock, rates.Leverage,
                   LeverageMin, LeverageMax)
            && IsWithinRange(
                   rates.Stock, rates.StockInverse,
                   StockInverseMin, StockInverseMax)
            && IsWithinRange(
                   rates.Stock, rates.LeverageInverse,
                   LeverageInverseMin, LeverageInverseMax);
    }

    private static bool IsWithinRange(decimal stockReturn, decimal productReturn, decimal minMultiplier, decimal maxMultiplier)
    {
        // 나눗셈으로 배율을 역산하지 않아 주식 수익률이 0이어도 안전함.
        decimal endpointA = stockReturn * minMultiplier;
        decimal endpointB = stockReturn * maxMultiplier;

        // 주식 수익률이 음수면 결과의 대소 관계가 뒤집힐 수 있음
        decimal minimum = Math.Min(endpointA, endpointB);
        decimal maximum = Math.Max(endpointA, endpointB);

        return productReturn >= minimum && productReturn <= maximum;
    }
}
