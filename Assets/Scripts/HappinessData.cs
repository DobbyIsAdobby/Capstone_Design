using System;
using System.Collections.Generic;

/// <summary>
/// happiness_events.json의 최상위 구조
/// </summary>
[Serializable]
public sealed class HappinessCatalog
{
    public int version;
    public HappinessSettings settings;
    public HappinessEventData[] events;
}

[Serializable]
public sealed class HappinessSettings
{
    // 각 구간에서 실제로 발생시킬 이벤트 수
    public int events_per_range;

    // 이벤트를 추첨할 턴 구간
    public HappinessEventRange[] ranges;
}

[Serializable]
public sealed class HappinessEventRange
{
    public int start_turn;
    public int end_turn;
}

[Serializable]
public sealed class HappinessEventData
{
    public string id;
    public int start_turn;
    public int end_turn;

    public string title;
    public string[] dialogue;

    public int ap_cost;
    public int stress_increase;
    public int happiness_gain;

    public string accept_label;
    public string decline_label;

    public string[] accept_result;
    public string[] decline_result;
    public string description;
}

/// <summary>
/// 새 게임에서 미리 정한 이벤트 일정
/// </summary>
[Serializable]
public sealed class HappinessSchedule
{
    public int turn;
    public string eventId;
}

/// <summary>
/// 전성기를 적용할 턴 구간
/// </summary>
[Serializable]
public sealed class GoldenAgePeriod
{
    public int startTurn;
    public int endTurn;
}

[Serializable]
public sealed class HappinessSaveData
{
    // JSON과 전성기 규칙 변경 여부를 확인
    public string configHash;

    public int points;

    // 이미 달성한 전성기 목표의 개수.
    public int reachedThresholds;

    // 현재 턴까지 행복도 안내를 처리했는지 기록.
    public int lastProcessedTurn;

    public List<HappinessSchedule> schedule = new List<HappinessSchedule>();

    public List<GoldenAgePeriod> periods = new List<GoldenAgePeriod>();
}