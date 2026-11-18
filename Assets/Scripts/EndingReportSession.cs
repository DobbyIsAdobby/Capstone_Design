using System;
using UnityEngine;

/// <summary>
/// GameScene에서 결과를 생성하고, EndingScene으로 전달.
/// GameManager, RealEstateManager의 Reference를 보관하진 않음.
/// </summary>
public static class EndingReportSession
{
    public static EndingReportData Current { get; private set; }

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        Current = null;
    }

    public static void Clear()
    {
        Current = null;
    }

    /// <summary>
    /// 엔딩 씬으로 이동하기 직전에 호출.
    /// 모든 차트는 이미 확정된 데이터만 복사.
    /// </summary>
    /// <param name="ending"></param>
    /// <exception cref="InvalidOperationException"></exception>
    public static void Prepare(EndingType ending)
    {
        GameManager game = GameManager.Instance;
        DataManager market = DataManager.Instance;
        RealEstateManager estate = RealEstateManager.Instance;

        if (game == null || market == null || estate == null || !estate.IsInitialized || ReportHistoryManager.Instance == null)
        {
            throw new InvalidOperationException("리포트 생성에 필요한 매니저가 없습니다.");
        }

        ReportHistory history = ReportHistoryManager.Instance.CaptureSave();

        var result = new EndingReportData
        {
            ending = ending,
            endedTurn = game.currentMonth,
            bankrupt = game.IsBankrupt,
            cause = game.IsBankrupt
                ? game.BankruptcyCause
                : "게임 중도 종료",
            finalNetWorth = game.TotalAsset,
            history = history
        };

        int lastTurn = history.lastSettledTurn;

        // 예금은 계산식의 월 0.3%를 표시.
        var bank = new ReportSeries { title = "예금 : 월 이율" };

        for (int turn = 1; turn <= lastTurn; turn++)
            bank.percentages.Add(0.3f);

        result.charts.Add(bank);

        // 네 종목 * 네 상품의 월 수익률을 복사.
        for (int sectorIndex = 0; sectorIndex < MarketSectorUtility.SectorCount; sectorIndex++)
        {
            MarketSector sector = (MarketSector)sectorIndex;

            for (int product = 1; product <= 4; product++)
            {
                AssetType type = (AssetType)product;

                var series = new ReportSeries
                {
                    title = $"{MarketSectorUtility.GetName(sector)} : " + ReportHistoryManager.ProductName(type)
                };

                for (int turn = 1; turn <= lastTurn; turn++)
                {
                    if (!market.TryGetGeneratedMarketRates(sector, turn, out MonthlyMarketRates rates))
                    {
                        throw new InvalidOperationException($"{sector} {turn}턴 수익률이 없습니다.");
                    }

                    decimal rate;

                    switch (type)
                    {
                        case AssetType.Stock:
                            rate = rates.Stock;
                            break;
                        case AssetType.Leverage:
                            rate = rates.Leverage;
                            break;
                        case AssetType.StockInverse:
                            rate = rates.StockInverse;
                            break;
                        default:
                            rate = rates.LeverageInverse;
                            break;
                    }

                    // 게임의 투자 평가액 하한 0원 기준과 맞춤.
                    rate = Math.Max(-1m, rate);

                    series.percentages.Add((float)(rate * 100m));
                }

                result.charts.Add(series);
            }
        }

        // 부동산은 가격 배열에서 월 시세 변동률을 계산.
        // 임대료는 시세 수익률에 섞지 않음.
        for (int i = 0; i < estate.Count; i++)
        {
            RealEstatePropertyState property = estate.Property(i);

            var series = new ReportSeries
            {
                title = $"부동산 : {estate.Definition(i).name}"
            };

            for (int turn = 1; turn <= lastTurn; turn++)
            {
                long previous = property.prices[turn - 1];
                long current = property.prices[turn];

                decimal rate = ((decimal)current - previous) / previous;

                series.percentages.Add((float)(rate * 100m));
            }

            result.charts.Add(series);
        }

        // 모든 생성이 끝난 뒤 결과를 교체.
        Current = result;
    }
}
