using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 행복도, 이벤트 일정, 전성기와 저장 상태 관리
/// </summary>
public class HappinessManager : Singleton<HappinessManager>
{
    [SerializeField] private TextAsset eventJson;
    [SerializeField] private HappinessRules rules;

    private HappinessCatalog catalog;
    private string configHash;
    private HappinessSaveData state;

    private readonly Dictionary<string, HappinessEventData> byId = new Dictionary<string, HappinessEventData>();

    public bool IsConfigured { get; private set; }
    public bool IsInitialized => state != null;

    public int Points => state != null ? state.points : 0;

    public int? NextTarget =>
        state != null &&
        state.reachedThresholds < rules.ThresholdCount ? rules.GetThreshold(state.reachedThresholds) : (int?)null;

    protected override void OnSingletonAwake()
    {
        IsConfigured = false;

        try
        {
            if (eventJson == null || rules == null)
                throw new InvalidOperationException("행복도 JSON과 Rules를 연결하세요.");

            rules.Validate();

            catalog = JsonUtility.FromJson<HappinessCatalog>(eventJson.text);

            if (catalog == null || catalog.settings == null || catalog.events == null)
            {
                throw new InvalidOperationException("행복도 JSON 구조가 올바르지 않습니다.");
            }

            HappinessSettings setting = catalog.settings;

            // 구간마다 2회, 총 6구간
            if (setting.events_per_range != 2 || setting.ranges == null || setting.ranges.Length != 6)
            {
                throw new InvalidOperationException("행복도는 6개 구간에서 각각 2회 발생하도록 설정하세요.");
            }

            // 구간이 1~10, 11~20, ..., 51~59인지 검사
            for (int i = 0; i < setting.ranges.Length; i++)
            {
                HappinessEventRange range = setting.ranges[i];

                int expectedStart = i * 10 + 1;
                int expectedEnd = Math.Min((i + 1) * 10, 59);

                if (range == null || range.start_turn != expectedStart || range.end_turn != expectedEnd)
                {
                    throw new InvalidOperationException("행복도 이벤트의 턴 구간을 확인하세요.");
                }
            }

            // JSON에서 읽은 이벤트를 ID로 찾을 수 있도록 Dictionary(byId)를 구성
            // 초기화가 다시 실행되더라도 이전 항목이 남지 않도록 초기화
            byId.Clear();

            foreach (HappinessEventData item in catalog.events)
            {
                // 이벤트가 없거나 ID가 비어 있으면 등록할 수 없음
                if (item == null || string.IsNullOrWhiteSpace(item.id))
                {
                    throw new InvalidOperationException("행복도 이벤트 또는 ID가 비어 있습니다.");
                }

                // ID 중복은 잘못된 이벤트 조회로 이어지므로 차단해야함
                if (byId.ContainsKey(item.id))
                {
                    throw new InvalidOperationException($"행복도 이벤트 ID가 중복되었습니다: {item.id}");
                }

                // 이벤트의 기본 수치와 표시 문구를 검사
                if (item.start_turn < 1 ||
                    item.end_turn > 59 ||
                    item.start_turn > item.end_turn ||
                    item.ap_cost < 0 ||
                    item.stress_increase < 0 ||
                    item.happiness_gain <= 0 ||
                    string.IsNullOrWhiteSpace(item.title) ||
                    string.IsNullOrWhiteSpace(item.description) ||
                    string.IsNullOrWhiteSpace(item.accept_label) ||
                    string.IsNullOrWhiteSpace(item.decline_label))
                {
                    throw new InvalidOperationException($"{item.id}의 기간, 비용 또는 표시 문구를 확인하세요.");
                }

                // 선택 전후에 출력할 대사가 있는지 검사
                ValidateLines(item.dialogue);
                ValidateLines(item.accept_result);
                ValidateLines(item.decline_result);

                // ID와 이벤트 데이터를 Dictionary에 등록
                byId.Add(item.id, item);
            }

            // 각 이벤트는 정의된 구간 중 하나에 정확히 속해야 함
            foreach (HappinessEventData item in catalog.events)
            {
                bool belongsToRange = setting.ranges.Any(range => item.start_turn == range.start_turn && item.end_turn == range.end_turn);

                if (!belongsToRange)
                {
                    throw new InvalidOperationException($"{item.id}의 발생 구간이 설정과 일치하지 않습니다.");
                }
            }

            // 각 구간에는 추첨 후보가 정확히 10개 있어야 함
            foreach (HappinessEventRange range in setting.ranges)
            {
                int count = catalog.events.Count(item => item.start_turn == range.start_turn && item.end_turn == range.end_turn);

                if (count != 10)
                {
                    throw new InvalidOperationException($"{range.start_turn}~{range.end_turn}턴의 " + $"행복도 상황은 10개여야 합니다. 현재 {count}개입니다.");
                }
            }

            configHash = Hash128.Compute(eventJson.text + rules.GetSignature()).ToString();

            IsConfigured = true;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
        }
    }

    private static void ValidateLines(string[] lines)
    {
        if (lines == null || lines.Length == 0 || lines.Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidOperationException("행복도 이벤트의 대사 배열을 확인하세요.");
        }
    }

    /// <summary>
    /// 각 구간의 상황 10개 중 2개를 중복 없이 선택하고, 서로 다른 두 턴에 배치.
    /// 새 게임에서만 호출하며 불러오기에서는 호출하지 않음.
    /// </summary>
    /// <param name="marketSeed"></param>
    /// <exception cref="InvalidOperationException"></exception>
    public void InitializeNewGame(int marketSeed)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("행복도 설정이 준비되지 않았습니다.");
        }

        // 시장 난수와 구분된 행복도 일정용 난수
        var random = new SaveRandom(unchecked(marketSeed ^ 0x416937));

        var nextState = new HappinessSaveData
        {
            configHash = configHash
        };

        int count = catalog.settings.events_per_range;

        foreach (HappinessEventRange range in catalog.settings.ranges)
        {
            // 이 구간에 속하는 상황 10개를 가져옴
            List<HappinessEventData> candidates =
                catalog.events.Where(item =>
                    item.start_turn == range.start_turn &&
                    item.end_turn == range.end_turn).ToList();

            // 발생 가능한 턴을 모두 준비
            var availableTurns = new List<int>();

            for (int turn = range.start_turn; turn <= range.end_turn; turn++)
            {
                availableTurns.Add(turn);
            }

            for (int i = 0; i < count; i++)
            {
                // 상황 하나를 선택한 뒤 후보에서 제거
                // 같은 구간에서 동일 상황이 다시 선택되지 않음
                int eventIndex = random.Next(candidates.Count);
                HappinessEventData selectedEvent = candidates[eventIndex];

                candidates.RemoveAt(eventIndex);

                // 턴도 선택 후 제거해 같은 턴의 중복 발생 방지.
                int turnIndex = random.Next(availableTurns.Count);
                int selectedTurn = availableTurns[turnIndex];

                availableTurns.RemoveAt(turnIndex);

                nextState.schedule.Add(new HappinessSchedule
                {
                    turn = selectedTurn,
                    eventId = selectedEvent.id
                });
            }
        }

        // 저장 및 검증에서 사용하기 쉽도록 턴 순서로 정렬
        nextState.schedule.Sort((left, right) => left.turn.CompareTo(right.turn));

        // 전체 일정 생성이 완료된 뒤 상태에 반영
        state = nextState;
    }

    public HappinessEventData GetEvent(int turn)
    {
        if (state == null)
            throw new InvalidOperationException("행복도가 초기화되지 않았습니다.");

        HappinessSchedule entry = state.schedule.Find(item => item.turn == turn);

        return entry == null ? null : byId[entry.eventId];
    }

    /// <summary>
    /// 안내를 진행할 턴의 순서 확인.
    /// 이미 완료했다면 다시 실행하지 않음.
    /// </summary>
    /// <param name="turn"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public bool NeedsPresentation(int turn)
    {
        if (state == null)
            throw new InvalidOperationException("행복도가 초기화되지 않았습니다.");

        if (state.lastProcessedTurn >= turn)
            return false;

        if (state.lastProcessedTurn != turn - 1)
            throw new InvalidOperationException("행복도 진행 턴이 일치하지 않습니다.");

        return true;
    }

    public bool IsGoldenAge(int turn)
    {
        return state != null &&
               state.periods.Any(period =>
                   period.startTurn <= turn &&
                   turn <= period.endTurn);
    }

    public int GetAPCost(int baseCost)
    {
        if (baseCost < 0)
            throw new ArgumentOutOfRangeException(nameof(baseCost));

        GameManager game = GameManager.Instance;

        if (game == null || !IsGoldenAge(game.currentMonth))
            return baseCost;

        // 기본 비용이 0이면 계속 무료
        // 1 AP 행동은 올림 처리되어 1 AP를 사용
        return Mathf.CeilToInt(baseCost * rules.APCostMultiplier);
    }

    public float GetStressIncrease(float baseIncrease)
    {
        if (baseIncrease < 0f)
            throw new ArgumentOutOfRangeException(nameof(baseIncrease));

        GameManager game = GameManager.Instance;

        return game != null && IsGoldenAge(game.currentMonth) ? baseIncrease * rules.StressMultiplier : baseIncrease;
    }

    public bool CanAccept(int turn, out string reason)
    {
        reason = "";
        GameManager game = GameManager.Instance;

        if (state == null ||
            game == null ||
            game.IsGameOver ||
            game.currentMonth != turn ||
            state.lastProcessedTurn != turn - 1 ||
            EventManager.Instance == null ||
            !EventManager.Instance.IsPresenting)
        {
            reason = "현재는 행복도 이벤트에 참여할 수 없습니다.";
            return false;
        }

        HappinessEventData item = GetEvent(turn);

        if (item == null)
        {
            reason = "현재 턴의 행복도 이벤트가 없습니다.";
            return false;
        }

        if (game.CurrentAP < item.ap_cost)
        {
            reason = "AP가 부족합니다.";
            return false;
        }

        // 행복도 활동으로 병원에 가는 상황은 참여 전에 막음.
        if (game.stressLevel + item.stress_increase >= game.MaxStress)
        {
            reason = "참여하면 피로도 한도에 도달합니다.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// 참여 또는 거절을 한 번만 처리.
    /// 반환 문자열은 새 전성기 예약 안내.
    /// </summary>
    /// <param name="turn"></param>
    /// <param name="accept"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public string Resolve(int turn, bool accept)
    {
        GameManager game = GameManager.Instance;

        if (!NeedsPresentation(turn) ||
            game == null ||
            game.currentMonth != turn ||
            EventManager.Instance == null ||
            !EventManager.Instance.IsPresenting)
        {
            throw new InvalidOperationException("행복도 이벤트 처리 상태가 올바르지 않습니다.");
        }

        HappinessEventData item = GetEvent(turn);
        string notice = "";

        if (accept)
        {
            if (item == null)
                throw new InvalidOperationException("참여할 이벤트가 없습니다.");

            // 행복도 합계의 오류는 비용 적용 전에 확인
            int nextPoints = checked(state.points + item.happiness_gain);

            if (!game.TryPayHappinessEventCost(turn, out string reason))
                throw new InvalidOperationException(reason);

            state.points = nextPoints;
            var messages = new List<string>();

            // 한 번에 여러 목표를 넘은 경우도 처리
            while (state.reachedThresholds < rules.ThresholdCount && state.points >= rules.GetThreshold(state.reachedThresholds))
            {
                int start = turn + 1;

                if (state.periods.Count > 0)
                {
                    start = Math.Max(start, state.periods[state.periods.Count - 1].endTurn + 1);
                }

                int end = checked(start + rules.DurationTurns - 1);

                state.periods.Add(new GoldenAgePeriod
                {
                    startTurn = start,
                    endTurn = end
                });

                state.reachedThresholds++;

                if (start <= game.maxMonth)
                {
                    int visibleEnd = Math.Min(end, game.maxMonth);

                    messages.Add($"너무 행복해서 무엇을 해도 지치지 않을 것같습니다.\n" + $"{start}~{visibleEnd}턴 동안 일반 활동의 AP 비용과 " + $"피로도 증가량이 50% 감소합니다.");
                }
                else
                {
                    messages.Add("행복도 목표를 달성했습니다.\n" + "게임 종료 이후의 기간이므로 추가 보너스는 적용되지 않습니다.");
                }
            }

            notice = string.Join("\n\n", messages);
        }

        // 이벤트가 없는 턴이나 거절한 턴도 처리 완료로 기록.
        state.lastProcessedTurn = turn;
        return notice;
    }

    public HappinessSaveData CaptureSave()
    {
        if (state == null)
            throw new InvalidOperationException("행복도 저장 상태가 없습니다.");

        return Clone(state);
    }

    public void ValidateSave(HappinessSaveData saved, int turn)
    {
        if (saved == null ||
            saved.configHash != configHash ||
            saved.points < 0 ||
            saved.lastProcessedTurn != turn ||
            saved.schedule == null ||
            saved.periods == null ||
            saved.reachedThresholds < 0 ||
            saved.reachedThresholds > rules.ThresholdCount)
        {
            throw new InvalidOperationException("행복도 저장 데이터 또는 설정이 일치하지 않습니다.");
        }

        int expectedCount = 0;

        while (expectedCount < rules.ThresholdCount && saved.points >= rules.GetThreshold(expectedCount))
        {
            expectedCount++;
        }

        if (expectedCount != saved.reachedThresholds || saved.periods.Count != expectedCount)
        {
            throw new InvalidOperationException("전성기 목표 기록이 올바르지 않습니다.");
        }

        int eventsPerRange = catalog.settings.events_per_range;
        int expectedScheduleCount = catalog.settings.ranges.Length * eventsPerRange;

        // 6개 구간 × 2회이니 총 12개여야함
        if (saved.schedule.Count != expectedScheduleCount)
        {
            throw new InvalidOperationException("행복도 이벤트 일정 개수가 올바르지 않습니다.");
        }

        int previousTurn = 0;
        var usedEventIds = new HashSet<string>();

        foreach (HappinessSchedule entry in saved.schedule)
        {
            // 일정은 턴 순서여야 하며 같은 턴에 중복될 수 없음
            if (entry == null || string.IsNullOrWhiteSpace(entry.eventId) || entry.turn <= previousTurn)
            {
                throw new InvalidOperationException("행복도 이벤트의 턴 순서가 올바르지 않습니다.");
            }

            if (!byId.TryGetValue(entry.eventId, out HappinessEventData item))
            {
                throw new InvalidOperationException("저장된 행복도 이벤트를 찾을 수 없습니다.");
            }

            // 선택된 상황의 구간 안에 배치되었는지 확인
            if (entry.turn < item.start_turn || entry.turn > item.end_turn)
            {
                throw new InvalidOperationException("행복도 이벤트가 잘못된 턴에 배치되어 있습니다.");
            }

            // 같은 상황을 두 번 배치하지 않음.
            if (!usedEventIds.Add(entry.eventId))
            {
                throw new InvalidOperationException("행복도 이벤트가 중복되어 있습니다.");
            }

            previousTurn = entry.turn;
        }

        // 총 개수뿐 아니라 각 구간에 정확히 2개씩 있는지도 확인
        foreach (HappinessEventRange range in catalog.settings.ranges)
        {
            int count = saved.schedule.Count(entry => entry.turn >= range.start_turn && entry.turn <= range.end_turn);

            if (count != eventsPerRange)
            {
                throw new InvalidOperationException($"{range.start_turn}~{range.end_turn}턴의 " + "행복도 발생 횟수가 올바르지 않습니다.");
            }
        }

        int previousEnd = 0;

        foreach (GoldenAgePeriod period in saved.periods)
        {
            if (period == null ||
                period.startTurn < 2 ||
                period.startTurn <= previousEnd ||
                period.endTurn - period.startTurn + 1 != rules.DurationTurns)
            {
                throw new InvalidOperationException("전성기 기간이 올바르지 않습니다.");
            }

            previousEnd = period.endTurn;
        }
    }

    public void RestoreSave(HappinessSaveData saved, int turn)
    {
        ValidateSave(saved, turn);
        state = Clone(saved);
    }

    private static HappinessSaveData Clone(HappinessSaveData source)
    {
        return JsonUtility.FromJson<HappinessSaveData>(
            JsonUtility.ToJson(source));
    }
}
