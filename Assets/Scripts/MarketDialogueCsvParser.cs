using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

/// <summary>
/// POS, NEG 두 열의 정보 스크립트 CSV를 읽어옴. 
/// 각 문장을 따음표 없이 보관하고, 중복 문장은 제거함.
/// </summary>
public class MarketDialogueCsvParser
{
    public static Dictionary<InformationDirection, IReadOnlyList<string>> Parse(string csv)
    {
        if (string.IsNullOrWhiteSpace(csv))
            throw new FormatException("정보 스크립트 CSV가 비어 있습니다.");

        var positive = new List<string>();
        var negative = new List<string>();

        var positiveSet = new HashSet<string>(StringComparer.Ordinal);
        var negativeSet = new HashSet<string>(StringComparer.Ordinal);

        bool headerRead = false;
        int lineNumber = 0;

        using var reader = new StringReader(csv);

        string line;

        while ((line = reader.ReadLine()) != null)
        {
            lineNumber++;

            if (string.IsNullOrWhiteSpace(line))
                continue;

            // UTF-8 BOM이 있는 파일도 읽을 수 있도록 제거
            string[] columns = ReadColumns(line.TrimStart('\uFEFF'), lineNumber);

            if (columns.Length != 2)
                throw new FormatException($"{lineNumber}행: 두 열이 필요합니다.");

            if (!headerRead)
            {
                if (!string.Equals(columns[0], "POS", StringComparison.OrdinalIgnoreCase) || !string.Equals(columns[1], "NEG", StringComparison.OrdinalIgnoreCase))
                {
                    throw new FormatException("CSV 헤더는 POS,NEG여야 합니다.");
                }

                headerRead = true;
                continue;
            }

            if (string.IsNullOrWhiteSpace(columns[0]) ||
                string.IsNullOrWhiteSpace(columns[1]))
            {
                throw new FormatException($"{lineNumber}행: 비어 있는 대사가 있습니다.");
            }

            // 같은 문장이 여러 행에 있어도 후보에는 한 번만 넣음
            if (positiveSet.Add(columns[0]))
                positive.Add(columns[0]);

            if (negativeSet.Add(columns[1]))
                negative.Add(columns[1]);
        }

        // 세 등급 모두 같은 방향을 선택해도 중복 없이 제공해야 함.
        if (positive.Count < 3 || negative.Count < 3)
        {
            throw new FormatException("POS와 NEG에 서로 다른 대사가 최소 3개씩 필요합니다.");
        }

        return new Dictionary<InformationDirection, IReadOnlyList<string>>
        {
            { InformationDirection.Up, positive.AsReadOnly() },
            { InformationDirection.Down, negative.AsReadOnly() }
        };
    }

    /// <summary>
    /// 따옴표 안의 쉼표와 "" 형태의 이스케이프 따옴표를 처리
    /// </summary>
    /// <param name="line"></param>
    /// <param name="lineNumber"></param>
    /// <returns></returns>
    /// <exception cref="FormatException"></exception>
    private static string[] ReadColumns(string line, int lineNumber)
    {
        var columns = new List<string>();
        var cell = new StringBuilder();

        bool quoted = false;
        bool quoteClosed = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (quoted)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        cell.Append('"');
                        i++;
                    }
                    else
                    {
                        quoted = false;
                        quoteClosed = true;
                    }
                }
                else
                {
                    cell.Append(c);
                }

                continue;
            }

            if (c == ',')
            {
                columns.Add(cell.ToString().Trim());
                cell.Clear();
                quoteClosed = false;
            }
            else if (quoteClosed)
            {
                // 닫는 따옴표 이후에는 공백 또는 구분자만 허용
                if (!char.IsWhiteSpace(c))
                    throw new FormatException($"{lineNumber}행: 잘못된 CSV 형식.");
            }
            else if (c == '"')
            {
                if (!string.IsNullOrWhiteSpace(cell.ToString()))
                    throw new FormatException($"{lineNumber}행: 따옴표 위치 오류.");

                cell.Clear();
                quoted = true;
            }
            else
            {
                cell.Append(c);
            }
        }

        if (quoted)
        {
            throw new FormatException($"{lineNumber}행: 닫히지 않은 따옴표 또는 셀 내부 줄바꿈.");
        }

        columns.Add(cell.ToString().Trim());
        return columns.ToArray();
    }
}
