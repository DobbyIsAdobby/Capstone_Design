using System;
using System.Collections.Generic;

/// <summary>
/// tutorial_triggers.json의 최상위 구조
/// </summary>
[Serializable]
public sealed class SpotlightCatalog
{
    public int version;
    public SpotlightGuide[] fixed_tutorials;
    public SpotlightGuide[] context_guides;
}

/// <summary>
/// 고정 안내와 조건부 안내가 공통으로 사용하는 구조
/// </summary>
[Serializable]
public sealed class SpotlightGuide
{
    public string id;

    // 고정 안내에서 사용하는 발생 턴
    public int turn;

    public bool once;

    // 조건부 안내에서 사용하는 성공 신호
    public string trigger;

    // 먼저 완료되어야 하는 안내의 ID
    public string required_completed_id;

    public SpotlightStep[] steps;
}

/// <summary>
/// 안내 하나를 구성하는 개별 단계
/// </summary>
[Serializable]
public sealed class SpotlightStep
{
    public string type;
    public string target_id;

    public string[] dialogues;

    public string event_name;
    public int count;
    public string count_scope;
    public string instruction;
}

/// <summary>
/// 진행 중 저장은 금지함.
/// 완료, 건너뛰기, 대기 기록만 저장.
/// </summary>
[Serializable]
public sealed class SpotlightSaveData
{
    public string configHash;

    // 건너뛴 안내도 다시 강제하지 않도록 같이 기록
    public List<string> completed = new List<string>();

    // 완료와 건너뛰기를 구분하는 기록
    public List<string> skipped = new List<string>();

    // 신호는 발생했지만 아직 표시하지 못한 조건부 안내
    public List<string> pending = new List<string>();
}