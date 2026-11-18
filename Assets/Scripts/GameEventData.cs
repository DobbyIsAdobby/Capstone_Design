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

    // 이벤트 대상 종목만 정보 후보에서 제외하는 정책
    public string information_policy;
}

/// <summary>
/// 이벤트가 교체할 종목별 기본 수익률.
/// </summary>
[Serializable]
public sealed class EventReturnData
{
    public string sector;
    public double value;

    public MarketSector Sector => MarketSectorUtility.Parse(sector);
}

/// <summary>
/// 시장 이벤트 한 건.
/// </summary>
[Serializable]
public sealed class RandomMarketEventData
{
    public string id;
    public string title;
    public string event_text;
    public string effect_type;
    public EventReturnData[] returns;

    // JSON의 긍정/부정 목록을 읽을 때 설정
    [NonSerialized]
    public bool IsPositive;

    public bool Affects(MarketSector sector)
    {
        return returns != null && Array.Exists(returns, row => row.Sector == sector);
    }

    public bool TryGetReturn(MarketSector sector, out decimal rate)
    {
        rate = 0m;

        if (returns == null)
            return false;

        foreach (EventReturnData row in returns)
        {
            if (row.Sector != sector)
                continue;

            rate = checked((decimal)row.value);
            return true;
        }

        return false;
    }
}

/// <summary>
/// 기술주, 바이오주, 철강주, 엔터주, 전체 시장 그룹.
/// </summary>
[Serializable]
public sealed class RandomMarketEventGroup
{
    public string group_id;
    public string scope;
    public string sector;

    public RandomMarketEventData[] positive_events;
    public RandomMarketEventData[] negative_events;
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
    public RandomMarketEventGroup[] random_event_groups;

    // 검증 후 그룹을 합쳐 만든 실행용 목록.
    public RandomMarketEventData[] random_events { get; private set; }

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
            randomRules.adjustment_target == "EVENT_RETURN_LIST" &&
            randomRules.adjustment_type == "REPLACE" &&
            randomRules.apply_asset_multipliers &&
            randomRules.information_policy == "EXCLUDE_AFFECTED_SECTORS",
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

        // 그룹을 검증하고 실행용 목록으로 합침
        Require(
            data.random_event_groups != null &&
            data.random_event_groups.Length == 5,
            "시장 이벤트 그룹은 개별 종목 4개와 전체 시장 1개가 필요합니다.");

        var flattened = new List<RandomMarketEventData>();
        var groupIds = new HashSet<string>(StringComparer.Ordinal);
        var sectorGroups = new HashSet<MarketSector>();
        int globalCount = 0;

        foreach (RandomMarketEventGroup group in data.random_event_groups)
        {
            Require(
                group != null &&
                !string.IsNullOrWhiteSpace(group.group_id) &&
                groupIds.Add(group.group_id),
                "이벤트 그룹 ID가 누락되었거나 중복되었습니다.");

            bool isGlobal = group.scope == "ALL";
            MarketSector groupSector = default;

            if (isGlobal)
            {
                globalCount++;

                Require(
                    string.IsNullOrEmpty(group.sector),
                    "전체 시장 그룹의 sector는 비워두세요.");
            }
            else
            {
                Require(
                    group.scope == "SECTOR" &&
                    MarketSectorUtility.TryParse(group.sector, out groupSector) &&
                    sectorGroups.Add(groupSector),
                    "개별 이벤트 그룹의 종목이 잘못되었거나 중복되었습니다.");
            }

            ValidateEventPool(
                group.positive_events,
                true,
                isGlobal,
                groupSector,
                ids,
                flattened);

            ValidateEventPool(
                group.negative_events,
                false,
                isGlobal,
                groupSector,
                ids,
                flattened);
        }

        Require(
            globalCount == 1 &&
            sectorGroups.Count == MarketSectorUtility.SectorCount,
            "네 종목 그룹과 전체 시장 그룹이 각각 한 번씩 필요합니다.");

        data.random_events = flattened.ToArray();

        int rangeCount = data.random_event_ranges.Length;

        Require(
            data.random_events.Count(x => x.IsPositive) >= rangeCount &&
            data.random_events.Count(x => !x.IsPositive) >= rangeCount,
            "발생 구간에 배정할 긍정·부정 이벤트가 부족합니다.");

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

    /// <summary>
    /// 그룹별 긍정 또는 부정 이벤트 목록 검사.
    /// </summary>
    /// <param name="pool"></param>
    /// <param name="positive"></param>
    /// <param name="global"></param>
    /// <param name="groupSector"></param>
    /// <param name="ids"></param>
    /// <param name="output"></param>
    private static void ValidateEventPool(RandomMarketEventData[] pool, bool positive, bool global, MarketSector groupSector, HashSet<string> ids, List<RandomMarketEventData> output)
    {
        Require(pool != null && pool.Length == 2, "각 이벤트 그룹에는 긍정 2개, 부정 2개가 필요합니다.");

        foreach (RandomMarketEventData item in pool)
        {
            Require(
                item != null &&
                !string.IsNullOrWhiteSpace(item.id) &&
                ids.Add(item.id) &&
                !string.IsNullOrWhiteSpace(item.title) &&
                !string.IsNullOrWhiteSpace(item.event_text) &&
                item.effect_type == "REPLACE_BASE_RETURN" &&
                item.returns != null &&
                item.returns.Length ==
                    (global ? MarketSectorUtility.SectorCount : 1),
                "이벤트 ID, 문구 또는 수익률 목록을 확인하세요.");

            var targets = new HashSet<MarketSector>();

            foreach (EventReturnData row in item.returns)
            {
                Require(row != null, "이벤트 수익률 항목이 없습니다.");

                MarketSector sector = MarketSectorUtility.Parse(row.sector);

                Require(
                    targets.Add(sector) &&
                    (global || sector == groupSector),
                    "이벤트 대상 종목이 중복되었거나 그룹과 다릅니다.");

                Require(
                    !double.IsNaN(row.value) &&
                    !double.IsInfinity(row.value) &&
                    row.value > -1.0 &&
                    (positive ? row.value > 0.0 : row.value < 0.0),
                    "이벤트 수익률의 범위 또는 긍정·부정 방향이 잘못되었습니다.");

                // decimal로 변환 가능한지 검사
                decimal checkedValue = checked((decimal)row.value);
            }

            item.IsPositive = positive;
            output.Add(item);
        }
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