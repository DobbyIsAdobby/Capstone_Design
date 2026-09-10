using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

public static class MarketDataLoader
{
    private static Dictionary<int, float> returnRates;
    private static Dictionary<int, string> dates;
    private static bool isLoaded = false;

    // 1. CSV 데이터 불러오기
    private static void LoadData()
    {
        if (isLoaded)
        {
            return;
        }

        returnRates = new Dictionary<int, float>();
        dates = new Dictionary<int, string>();

        TextAsset csvFile = Resources.Load<TextAsset>("Data/market_data_test");

        if (csvFile == null)
        {
            Debug.LogError("[MarketDataLoader] market_data_test.csv 파일을 찾을 수 없습니다.");
            isLoaded = true;
            return;
        }

        string[] lines = csvFile.text.Replace("\r", "").Split('\n');

        // 2. CSV 각 행 읽기
        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
            {
                continue;
            }

            string[] values = lines[i].Split(',');

            if (values.Length < 3)
            {
                continue;
            }

            if (
                int.TryParse(values[0], out int turn)
                &&
                float.TryParse(
                    values[2],
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out float returnRate
                )
            )
            {
                returnRates[turn] = returnRate;
                dates[turn] = values[1];
            }
        }

        isLoaded = true;

        Debug.Log(
            $"[MarketDataLoader] NASDAQ 테스트 데이터 {returnRates.Count}개월 로드 완료."
        );
    }

    // 3. 해당 턴 수익률 반환
    public static float GetReturnRate(int turn)
    {
        LoadData();

        if (returnRates.TryGetValue(turn, out float rate))
        {
            return rate;
        }

        Debug.LogWarning(
            $"[MarketDataLoader] Turn {turn} 데이터가 없습니다. 수익률 0% 적용."
        );

        return 0f;
    }

    // 4. 해당 턴 날짜 반환
    public static string GetDate(int turn)
    {
        LoadData();

        if (dates.TryGetValue(turn, out string date))
        {
            return date;
        }

        return "Unknown";
    }
}