// 4개의 수익률을 각각 받도록 만든 구조체
// ONNX 모델이 4개의 출력을 제공할 때도 같은 구조체로 전달할 수 있게하기 위함.
// 현재는 인버스 값에 음수를 붙여 생성함.
public readonly struct MonthlyMarketRates
{
    // 자동 구현 프로퍼티
    public decimal Stock { get; }
    public decimal Leverage { get; }
    public decimal StockInverse { get; }
    public decimal LeverageInverse { get; }

    /// <summary>
    /// 생성자
    /// </summary>
    /// <param name="stock"></param>
    /// <param name="leverage"></param>
    /// <param name="stockInverse"></param>
    /// <param name="leverageInverse"></param>
    public MonthlyMarketRates(decimal stock, decimal leverage, decimal stockInverse, decimal leverageInverse)
    {
        Stock = stock;
        Leverage = leverage;
        StockInverse = stockInverse;
        LeverageInverse = leverageInverse;
    }
}