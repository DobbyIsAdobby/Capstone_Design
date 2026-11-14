using System;

/// <summary>
/// 투자상품이 추종하는 산업 종목.
/// 저장 파일에서 사용하는 번호이니 순서 임의 변경 금지.
/// </summary>
public enum MarketSector
{
    Technology = 0,
    Biotechnology = 1,
    MetalsMining = 2,
    Entertainment = 3,
}

public static class MarketSectorUtility
{
    public const int SectorCount = 4;

    public static bool IsValid(MarketSector sector)
    {
        return Enum.IsDefined(typeof(MarketSector), sector);
    }

    public static string GetName(MarketSector sector)
    {
        switch (sector)
        {
            case MarketSector.Technology:
                return "기술주";

            case MarketSector.Biotechnology:
                return "바이오주";

            case MarketSector.MetalsMining:
                return "철강주";

            case MarketSector.Entertainment:
                return "엔터주";

            default:
                throw new ArgumentOutOfRangeException(nameof(sector));
        }
    }

    /// <summary>
    /// 종목과 턴을 하나의 시장 데이터 키로 변경
    /// 
    /// 기술주 : 1~60
    /// 바이오주 : 61~120
    /// 철강주 : 121~180
    /// 엔터주 : 181~240
    /// 
    /// 화면에 표시하는 턴 번호는 1~60 임
    /// </summary>
    /// <param name="sector"></param>
    /// <param name="turn"></param>
    /// <returns></returns>
    public static int GetMarketKey(MarketSector sector, int turn)
    {
        if (!IsValid(sector))
            throw new ArgumentOutOfRangeException(nameof(sector));

        if (turn < 1 || turn > MarketModelConfig.TurnCount)
            throw new ArgumentOutOfRangeException(nameof(turn));

        return (int)sector * MarketModelConfig.TurnCount + turn;
    }

    /// <summary>
    /// JSON에 기록된 종목 이름 변환.
    /// </summary>
    /// <param name="value"></param>
    /// <param name="sector"></param>
    /// <returns></returns>
    public static bool TryParse(string value, out MarketSector sector)
    {
        switch (value)
        {
            case "Technology":
                sector = MarketSector.Technology;
                return true;

            case "Biotechnology":
                sector = MarketSector.Biotechnology;
                return true;

            case "MetalsMining":
                sector = MarketSector.MetalsMining;
                return true;

            case "Entertainment":
                sector = MarketSector.Entertainment;
                return true;

            default:
                sector = default;
                return false;
        }
    }

    /// <summary>
    /// 반드시 정상 종목이어야 하는 데이터에서 사용.
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    /// <exception cref="FormatException"></exception>
    public static MarketSector Parse(string value)
    {
        if (!TryParse(value, out MarketSector sector))
        {
            throw new FormatException($"지원하지 않는 종목 이름입니다: {value}");
        }

        return sector;
    }
}