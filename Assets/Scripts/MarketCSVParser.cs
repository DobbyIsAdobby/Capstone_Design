using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

// 형식이 같은 NASDAQ/레버리지 CSV 사용 필수.
public static class MarketCSVParser
{
    public static Dictionary<int, MarketData> Parse(
        string csv, string sourceName = "Market CSV")
    {
        if (string.IsNullOrWhiteSpace(csv))
            throw new FormatException($"{sourceName}: CSV 내용이 비어 있습니다.");

        var result = new Dictionary<int, MarketData>();
        bool headerRead = false;
        int lineNumber = 0;
        DateTime previousMonth = default;

        using (var reader = new StringReader(csv))
        {
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                lineNumber++;
                if (string.IsNullOrWhiteSpace(line)) continue;

                string[] columns = line.Split(',');
                if (columns.Length != 3)
                    throw Error(sourceName, lineNumber, "Column은 3개여야 합니다.");

                for (int i = 0; i < columns.Length; i++)
                    columns[i] = columns[i].Trim();

                if (!headerRead)
                {
                    columns[0] = columns[0].TrimStart('\uFEFF').Trim();
                    if (columns[0] != "turn" || columns[1] != "date" ||
                        columns[2] != "return_rate")
                        throw Error(sourceName, lineNumber,
                            "Header는 turn,date,return_rate여야 합니다.");

                    headerRead = true;
                    continue;
                }

                if (!int.TryParse(columns[0], NumberStyles.None,
                    CultureInfo.InvariantCulture, out int turn) ||
                    turn != result.Count + 1)
                    throw Error(sourceName, lineNumber,
                        "turn은 1부터 시작해 1씩 증가해야 합니다.");

                if (!DateTime.TryParseExact(columns[1], "yyyy-MM",
                    CultureInfo.InvariantCulture, DateTimeStyles.None,
                    out DateTime month))
                    throw Error(sourceName, lineNumber,
                        "date는 yyyy-MM 형식이어야 합니다.");

                if (result.Count > 0 &&
                    month.Year * 12 + month.Month !=
                    previousMonth.Year * 12 + previousMonth.Month + 1)
                    throw Error(sourceName, lineNumber,
                        "date는 한 달 뒤여야 합니다.");

                const NumberStyles rateStyle =
                    NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint;

                if (!decimal.TryParse(columns[2], rateStyle,
                    CultureInfo.InvariantCulture, out decimal rate) || rate < -1m)
                    throw Error(sourceName, lineNumber,
                        "return_rate는 -1 이상의 소수 비율이어야 합니다. ex: 0.05 = 5%");

                result.Add(turn, new MarketData(month, rate));
                previousMonth = month;
            }
        }

        if (result.Count == 0)
            throw new FormatException($"{sourceName}: 데이터 행이 없습니다.");

        return result;
    }

    private static FormatException Error(string source, int line, string message)
    {
        return new FormatException($"{source} / {line}행: {message}");
    }
}
