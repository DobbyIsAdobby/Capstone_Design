using System;
using System.Collections.Generic;
using System.Linq;      // linq 확장 메서드를 사용하기 위해 네임스페이스 선언.
using UnityEngine;

/// <summary>
/// 랜덤 이벤트 발생 시 각 자산에 곱해야할 수익률 랜덤 수치
/// </summary>
[Serializable]
public sealed class EventMultiplierData
{
    public double min_multiplier;
    public double max_multiplier;
}

/// <summary>
/// 곱연산할 자산군 종류
/// </summary>
[Serializable]
public sealed class EventAssetMultiplierData
{
    public EventMultiplierData TECH;
    public EventMultiplierData LEVERAGE;
    public EventMultiplierData INVERSE;
    public EventMultiplierData INVERSE_LEVERAGE;
}

/// <summary>
/// 고정 이벤트 세팅 데이터
/// </summary>
[Serializable]
public sealed class FixedEventSettingsData
{
    public bool random_from_pool;
    public bool allow_duplicate_in_same_game;
    public string hint_timing;
    public string settlement_timing;
    public string result_timing;
}

/// <summary>
/// 고정 이벤트 데이터
/// </summary>
[Serializable]
public sealed class FixedEventData
{
    public string id;
    public string title;
    public string hint_text;

    // 수입 이벤트에선 JSON에 없어도 됨.
    public string notice_text;

    public string event_text;
    public string effect_type;
    public long value;
}

/// <summary>
/// 랜덤 이벤트 각 구간
/// </summary>
[Serializable]
public sealed class RandomEventRangeData
{
    public string range_id;
    public int start_turn;
    public int end_turn;
    public int min_count;
    public int max_count;
}

/// <summary>
/// 랜덤 이벤트 세팅 데이터
/// </summary>
[Serializable]
public sealed class RandomEventSettingsData
{
    public bool random_event_from_pool;
    public bool allow_duplicate_in_same_game;

    public double two_events_probability;
    public double positive_probability_when_single;

    public bool pair_positive_and_negative;
    public bool allow_same_turn_in_range;
    public bool use_hint;

    public string announcement_timing;
    public string adjustment_target;
    public string adjustment_type;
    public bool apply_asset_multipliers;
    public bool block_information;
}

/// <summary>
/// 랜덤 시장 이벤트 데이터
/// </summary>
[Serializable]
public sealed class RandomMarketEventData
{
    public string id;
    public string title;
    public string event_text;
    public string effect_type;
    public double tech_return;
}

/// <summary>
/// 게임 이벤트 내역
/// </summary>
[Serializable]
public sealed class GameEventCatalog
{
    public EventAssetMultiplierData asset_multiplier_rules;
    public int[] fixed_event_turns;
    public FixedEventSettingsData fixed_event_settings;
    public FixedEventData[] fixed_event_pool;
    public RandomEventRangeData[] random_event_ranges;
    public RandomEventSettingsData random_event_settings;
    public RandomMarketEventData[] random_events;

    /// <summary>
    /// 처리할 수 있는 데이터인지 검사.
    /// 잘못된 데이터일 경우 요구사항 출력.
    /// </summary>
    public static GameEventCatalog Parse(TextAsset source)
    {
        Require(source != null, "Event Json을 연결하세요.");

        GameEventCatalog data = JsonUtility.FromJson<GameEventCatalog>(source.text);

        Require(data != null, "이벤트 JSON을 읽지 못했습니다.");

        Require(
            data.fixed_event_turns != null &&
            data.fixed_event_turns.SequenceEqual(new[] { 12, 24, 36, 48 }),
            "고정 이벤트 턴은 12, 24, 36, 48이어야 합니다.");

        FixedEventSettingsData fixedRules = data.fixed_event_settings;

        Require(
            fixedRules != null &&
            fixedRules.random_from_pool &&
            !fixedRules.allow_duplicate_in_same_game &&
            fixedRules.hint_timing == "TURN_START" &&
            fixedRules.settlement_timing == "TURN_END" &&
            fixedRules.result_timing == "NEXT_TURN_START",
            "고정 이벤트 실행 규칙을 확인하세요.");

        Require(
            data.fixed_event_pool != null &&
            data.fixed_event_pool.Length >= data.fixed_event_turns.Length,
            "고정 이벤트 후보가 부족합니다.");

        var ids = new HashSet<string>(StringComparer.Ordinal);

        foreach (FixedEventData item in data.fixed_event_pool)
        {
            Require(
                item != null &&
                !string.IsNullOrWhiteSpace(item.id) &&
                ids.Add(item.id) &&
                !string.IsNullOrWhiteSpace(item.title) &&
                !string.IsNullOrWhiteSpace(item.hint_text) &&
                !string.IsNullOrWhiteSpace(item.event_text) &&
                item.effect_type == "CASH" &&
                item.value != 0 &&
                item.value != long.MinValue,
                "고정 이벤트의 ID, 문구, 효과 또는 금액이 잘못되었습니다.");

            // 지출 이벤트만 사전 안내 문구가 필수.
            if (item.value < 0)
            {
                Require(
                    !string.IsNullOrWhiteSpace(item.notice_text),
                    $"{item.id}: 지출 이벤트에 notice_text가 없습니다.");
            }
        }

        RandomEventSettingsData randomRules = data.random_event_settings;

        Require(
            randomRules != null &&
            randomRules.random_event_from_pool &&
            !randomRules.allow_duplicate_in_same_game &&
            randomRules.pair_positive_and_negative &&
            !randomRules.allow_same_turn_in_range &&
            !randomRules.use_hint &&
            randomRules.announcement_timing == "TURN_START" &&
            randomRules.adjustment_target == "TECH" &&
            randomRules.adjustment_type == "REPLACE" &&
            randomRules.apply_asset_multipliers &&
            randomRules.block_information,
            "시장 이벤트 실행 규칙을 확인하세요.");

        Require(IsProbability(randomRules.two_events_probability) &&
                IsProbability(randomRules.positive_probability_when_single),
                "이벤트 확률은 0~1이어야 합니다.");

        Require(data.random_event_ranges != null &&
                data.random_event_ranges.Length == 4,
                "시장 이벤트 구간은 4개여야 합니다.");

        var occupiedTurns = new HashSet<int>(data.fixed_event_turns);
        var rangeIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (RandomEventRangeData range in data.random_event_ranges)
        {
            Require(
                range != null &&
                !string.IsNullOrWhiteSpace(range.range_id) &&
                rangeIds.Add(range.range_id) &&
                range.start_turn >= 1 &&
                range.end_turn < 60 &&
                range.end_turn > range.start_turn &&
                range.min_count == 1 &&
                range.max_count == 2,
                "시장 이벤트 구간 설정이 잘못되었습니다.");

            for (int turn = range.start_turn; turn <= range.end_turn; turn++)
            {
                Require(occupiedTurns.Add(turn),
                    "이벤트 구간끼리 또는 고정 이벤트 턴과 겹칩니다.");
            }
        }

        Require(data.random_events != null, "시장 이벤트 목록이 없습니다.");

        foreach (RandomMarketEventData item in data.random_events)
        {
            Require(
                item != null &&
                !string.IsNullOrWhiteSpace(item.id) &&
                ids.Add(item.id) &&
                !string.IsNullOrWhiteSpace(item.title) &&
                !string.IsNullOrWhiteSpace(item.event_text) &&
                item.effect_type == "MARKET_RETURN" &&
                !double.IsNaN(item.tech_return) &&
                !double.IsInfinity(item.tech_return) &&
                item.tech_return != 0 &&
                item.tech_return > -1,
                "시장 이벤트 데이터가 잘못되었습니다.");

            // decimal로 표현할 수 있는지도 검사.
            decimal rate = checked((decimal)item.tech_return);
        }

        int rangeCount = data.random_event_ranges.Length;

        Require(
            data.random_events.Count(x => x.tech_return > 0) >= rangeCount &&
            data.random_events.Count(x => x.tech_return < 0) >= rangeCount,
            "각 구간에 배정할 호재·악재 후보가 부족합니다.");

        // 시장 생성, 저장 검증에서 사용하는 배율과 반드시 정확하게 일치해야 함.
        // JSON만 바꿔서 생성 규칙과 저장 검증 규칙이 달라지는 것을 막음.
        EventAssetMultiplierData multipliers = data.asset_multiplier_rules;
        Require(multipliers != null, "자산 배율 설정이 없습니다.");

        CheckMultiplier(multipliers.TECH, 1m, 1m);
        CheckMultiplier(multipliers.LEVERAGE, MarketReturnRules.LeverageMin, MarketReturnRules.LeverageMax);
        CheckMultiplier(multipliers.INVERSE, MarketReturnRules.StockInverseMin, MarketReturnRules.StockInverseMax);
        CheckMultiplier(multipliers.INVERSE_LEVERAGE, MarketReturnRules.LeverageInverseMin, MarketReturnRules.LeverageInverseMax);

        return data;
    }

    private static bool IsProbability(double value)
    {
        return !double.IsNaN(value) && value >= 0 && value <= 1;
    }

    private static void CheckMultiplier(EventMultiplierData value, decimal min, decimal max)
    {
        Require(
            value != null &&
            value.min_multiplier == (double)min &&
            value.max_multiplier == (double)max,
            "JSON 배율과 MarketReturnRules 배율이 일치하지 않습니다.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new FormatException(message);
    }
}