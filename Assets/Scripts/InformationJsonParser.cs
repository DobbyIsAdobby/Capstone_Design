using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 정보 JSON을 읽고 게임에서 사용할 데이터로 변환. 
/// GameObj에 붙이지 않는 일반 c# class
/// </summary>
public static class InformationJsonParser
{
    [Serializable] 
    private sealed class Root
    {
        public Row[] information_items;
    }

    /// <summary>
    /// JSON 내 target_asset은 사용하지 않음
    /// </summary>
    [Serializable]
    private sealed class Row
    {
        public string info_id;
        public string name;
        public string grade;
        public long price;
        public double accuracy;
        public string prediction_type;
        public int lookahead_turns;
        public string free_with_item;

        // target_asset과 descripttion은 사용하지 않음.
        // JSON 내 TECH 설명을 UI에 출력하지 않기 위함.
    }

    public static Dictionary<InformationGrade, InformationItemData> Parse(string json)
    {
        Root root;

        try
        {
            root = JsonUtility.FromJson<Root>(json);
        }
        catch (ArgumentException exception)
        {
            throw new FormatException("정보 JSON 형식이 잘못되었습니다.", exception);
        }

        if (root?.information_items == null)
            throw new FormatException("information_items가 없습니다.");

        var result = new Dictionary<InformationGrade, InformationItemData>();

        var ids = new HashSet<string>(StringComparer.Ordinal);

        foreach (Row row in root.information_items)
        {
            if (row == null || string.IsNullOrWhiteSpace(row.info_id) || string.IsNullOrWhiteSpace(row.name))
            {
                throw new FormatException("정보 ID 또는 이름이 없습니다.");
            }

            // LOW/MID/HIGH 문자열을 enum으로 변환
            if (!Enum.TryParse(row.grade, true, out InformationGrade grade) || !Enum.IsDefined(typeof(InformationGrade), grade))
            {
                throw new FormatException($"잘못된 정보 등급: {row.grade}");
            }

            if (row.price < 0 || double.IsNaN(row.accuracy) || double.IsInfinity(row.accuracy) || row.accuracy < 0 || row.accuracy > 1)
            {
                throw new FormatException($"{row.info_id}: 가격 또는 정확도가 잘못되었습니다.");
            }

            if (row.prediction_type != "DIRECTION" || row.lookahead_turns != 1)
            {
                throw new FormatException("현재는 다음 턴의 방향 정보만 지원합니다.");
            }

            if (!ids.Add(row.info_id) || result.ContainsKey(grade))
                throw new FormatException("정보 ID 또는 등급이 중복되었습니다.");

            // 무료 공개는 하급 정보에서만 적용하는지 검사
            if (grade != InformationGrade.Low &&
                !string.IsNullOrEmpty(row.free_with_item))
            {
                throw new FormatException("무료 아이템 조건은 하급 정보에만 지정하세요.");
            }

            result.Add(
                grade,
                new InformationItemData(
                    row.info_id,
                    row.name,
                    grade,
                    row.price,
                    row.accuracy,
                    row.lookahead_turns,
                    row.free_with_item));
        }

        if (result.Count != 3)
            throw new FormatException("상/중/하급 정보가 모두 필요합니다.");

        return result;
    }
}
