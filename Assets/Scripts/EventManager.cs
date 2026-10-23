using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class EventManager : Singleton<EventManager>
{
    [Header("Event Data")]
    [SerializeField] private TextAsset eventJson;

    [Header("Event UI")]
    [SerializeField] private EventFlowUI eventUI;

    // 시장 생성 난수와 이벤트 추첨 난수를 분리.
    private const int EventSeedSalt = 0x27439A51;

    private GameEventCatalog catalog;
    private string catalogHash;

    private readonly Dictionary<string, FixedEventData> fixedById = new Dictionary<string, FixedEventData>();

    private readonly Dictionary<string, RandomMarketEventData> marketById = new Dictionary<string, RandomMarketEventData>();

    // 이번 플레이에서 확정한 일정과 처리 완료 상태.
    private EventSaveData state;

    private Coroutine presentationRoutine;

    public bool IsPresenting { get; private set; }

    public bool IsConfigured =>
        catalog != null &&
        eventUI != null &&
        eventUI.isActiveAndEnabled &&
        eventUI.IsConfigured;

    /// <summary>
    /// 지출 안내 UI가 사용하던 이름을 유지.
    /// 별도로 저장하는 값이 아닌 이번 턴 일정에서 계산.
    /// </summary>
    public long pendingPenaltyAmount
    {
        get
        {
            GameManager game = GameManager.Instance;

            if (game == null || state == null || state.lastSettledTurn >= game.currentMonth)
            {
                return 0;
            }

            FixedEventData item = GetFixedEvent(game.currentMonth);

            return item != null && item.value < 0 ? -item.value : 0;
        }
    }

    public string pendingEventName
    {
        get
        {
            if (pendingPenaltyAmount <= 0)
                return "";

            return GetFixedEvent(GameManager.Instance.currentMonth).title;
        }
    }

    protected override void OnSingletonAwake()
    {
        // 파싱 및 검증.
        catalog = GameEventCatalog.Parse(eventJson);

        // 저장 당시 JSON과 현재 JSON이 같은지 확인할 식별값.
        catalogHash = Hash128.Compute(eventJson.text).ToString();

        foreach (FixedEventData item in catalog.fixed_event_pool)
            fixedById.Add(item.id, item);

        foreach (RandomMarketEventData item in catalog.random_events)
            marketById.Add(item.id, item);
    }

    /// <summary>
    /// 새 게임에서 한 번만 호출.
    /// 시장을 생성하기 전에 전체 이벤트 일정을 확정.
    /// </summary>
    /// <param name="marketSeed"></param>
    /// <exception cref="InvalidOperationException"></exception>
    public void InitializeNewGame(int marketSeed)
    {
        RequireConfigured();

        if (state != null)
            throw new InvalidOperationException("이벤트 일정이 이미 준비되어 있습니다.");

        int eventSeed = unchecked(marketSeed ^ EventSeedSalt);
        var random = new SaveRandom(eventSeed);

        var created = new EventSaveData
        {
            catalogHash = catalogHash,
            lastSettledTurn = 0,
            lastPresentedTurn = 0
        };

        // 고정 이벤트 8개를 섞고 앞에서 4개를 사용.
        // 같은 ID가 중복 선택되지 않음.
        var fixedPool = new List<FixedEventData>(catalog.fixed_event_pool);

        Shuffle(fixedPool, random);

        for (int i = 0; i < catalog.fixed_event_turns.Length; i++)
        {
            created.fixedEvents.Add(new EventScheduleEntry
            {
                turn = catalog.fixed_event_turns[i],
                id = fixedPool[i].id
            });
        }

        // 호재|악재 후보를 각각 섞음.
        var positivePool = catalog.random_events
            .Where(x => x.tech_return > 0)
            .ToList();

        var negativePool = catalog.random_events
            .Where(x => x.tech_return < 0)
            .ToList();

        Shuffle(positivePool, random);
        Shuffle(negativePool, random);

        RandomEventSettingsData rules = catalog.random_event_settings;

        for (int i = 0; i < catalog.random_event_ranges.Length; i++)
        {
            RandomEventRangeData range = catalog.random_event_ranges[i];

            // 구간 내 턴을 섞어 서로 다른 발생 턴을 선택.
            var turns = Enumerable.Range(range.start_turn, range.end_turn - range.start_turn + 1).ToList();

            Shuffle(turns, random);

            bool useTwo = random.NextDouble() < rules.two_events_probability;

            if (useTwo)
            {
                // 두 번이면 호재 한 번 + 악재 한 번.
                AddMarketEvent(created, turns[0], positivePool[i].id);

                AddMarketEvent(created, turns[1], negativePool[i].id);
            }
            else
            {
                // 한 번이면 호재|악재 중 하나를 선택.
                bool usePositive = random.NextDouble() < rules.positive_probability_when_single;

                string id = usePositive ? positivePool[i].id : negativePool[i].id;

                AddMarketEvent(created, turns[0], id);
            }
        }

        created.fixedEvents.Sort((a, b) => a.turn.CompareTo(b.turn));

        created.marketEvents.Sort((a, b) => a.turn.CompareTo(b.turn));

        // 일정 생성이 모두 끝난 후 한 번에 등록.
        state = created;
    }

    private static void AddMarketEvent(EventSaveData target, int turn, string id)
    {
        target.marketEvents.Add(new EventScheduleEntry
        {
            turn = turn,
            id = id
        });
    }

    /// <summary>
    /// 중복 없이 선택할 수 있도록 목록 순서를 섞음.
    /// - Fisher-Yates Shuffle 알고리즘을 사용함.
    /// 편향되지 않은 배열을 생성할 수 있으며, 시간복잡도 또한 O(n)으로 빠르기 때문.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="items"></param>
    /// <param name="random"></param>
    private static void Shuffle<T>(IList<T> items, SaveRandom random)
    {
        // Fisher-Yates Shuffle Algorithm.
        for (int i = items.Count - 1; i > 0; i--)
        {
            int index = random.Next(i + 1);

            T temporary = items[i];
            items[i] = items[index];
            items[index] = temporary;
        }
    }

    private FixedEventData GetFixedEvent(int turn)
    {
        if (state == null)
            return null;

        EventScheduleEntry entry = state.fixedEvents.Find(x => x.turn == turn);

        return entry == null ? null : fixedById[entry.id];
    }

    private RandomMarketEventData GetMarketEvent(int turn)
    {
        if (state == null)
            return null;

        EventScheduleEntry entry = state.marketEvents.Find(x => x.turn == turn);

        return entry == null ? null : marketById[entry.id];
    }

    /// <summary>
    /// 이벤트 턴이면 JSON 수익률을 사용.
    /// 일반 턴이면 ONNX 모델의 월 수익률을 사용.
    /// </summary>
    /// <param name="turn"></param>
    /// <param name="generatedReturn"></param>
    /// <returns></returns>
    public decimal GetTechReturn(int turn, decimal generatedReturn)
    {
        RequireState();

        RandomMarketEventData item = GetMarketEvent(turn);

        return item == null ? generatedReturn : (decimal)item.tech_return;
    }

    /// <summary>
    /// 속보 화면을 닫아도 해당 턴에는 정보 구매를 차단.
    /// 화면 활성 상태가 아니라 확정된 일정으로 판단.
    /// </summary>
    /// <param name="turn"></param>
    /// <returns></returns>
    public bool IsInformationBlocked(int turn)
    {
        return state != null && GetMarketEvent(turn) != null;
    }

    /// <summary>
    /// 해당 턴의 시작 안내가 끝나야 행동과 저장을 허용.
    /// </summary>
    /// <param name="turn"></param>
    /// <returns></returns>
    public bool IsTurnReady(int turn)
    {
        return state != null && !IsPresenting && state.lastPresentedTurn == turn && state.lastSettledTurn == turn - 1;
    }

    /// <summary>
    /// 월 마감 시 고정 이벤트를 정산.
    /// 이벤트가 없는 달도 정산 완료 턴을 기록.
    /// </summary>
    /// <param name="turn"></param>
    /// <exception cref="InvalidOperationException"></exception>
    public void SettleFixedEvent(int turn)
    {
        RequireState();

        // 동일한 달이 다시 전달되어도 금액을 중복 처리하지 않음.
        if (turn <= state.lastSettledTurn)
            return;

        if (turn != state.lastSettledTurn + 1)
            throw new InvalidOperationException("이벤트 정산 턴이 연속되지 않습니다.");

        GameManager game = GameManager.Instance;

        if (game == null || game.currentMonth != turn)
            throw new InvalidOperationException("현재 게임 턴과 이벤트 정산 턴이 다릅니다.");

        FixedEventData item = GetFixedEvent(turn);

        if (item != null)
        {
            if (item.value > 0)
            {
                game.ApplyCashChange(item.value, "이벤트", ReceiptLineType.Change);
            }
            else
            {
                // 잔액이 부족하더라도 비용 전액을 기록.
                // 최종 파산 판정은 GameManager의 정산 흐름 사용.
                game.ApplyMandatoryExpense(-item.value, $"이벤트: {item.title}", ReceiptLineType.Change);
            }
        }

        state.lastSettledTurn = turn;
    }

    /// <summary>
    /// 월초 준비가 끝난 뒤 안내 순서를 시작.
    /// </summary>
    /// <param name="turn"></param>
    /// <exception cref="InvalidOperationException"></exception>
    public void PresentTurnStart(int turn)
    {
        RequireConfigured();
        RequireState();

        if (IsPresenting || state.lastPresentedTurn >= turn)
            return;

        if (state.lastSettledTurn != turn - 1 || state.lastPresentedTurn != turn - 1)
        {
            throw new InvalidOperationException("이벤트 안내 턴과 정산 기록이 일치하지 않습니다.");
        }

        // 진행 상태를 바꾸기 전에 스토리 설정부터 검사.
        if (TutorialManager.Instance == null || !TutorialManager.Instance.IsConfigured)
        {
            throw new InvalidOperationException("TutorialManager와 story.json 설정을 확인하세요.");
        }

        // 코루틴 시작 전에 잠금.
        // 같은 프레임에 다른 행동이 실행되지 않도록 함.
        IsPresenting = true;

        presentationRoutine = StartCoroutine(PresentTurnRoutine(turn));
    }

    private IEnumerator PresentTurnRoutine(int turn)
    {
        // 월초 데이터 초기화가 끝난 다음 안내를 시작.
        yield return null;

        // 1. 해당 턴의 초반 스토리를 표시.
        yield return TutorialManager.Instance.ShowTurnStory(turn, eventUI);

        // 2. 지난달에 정산한 고정 이벤트 결과를 표시.
        FixedEventData previous = GetFixedEvent(turn - 1);

        if (previous != null)
        {
            yield return eventUI.ShowSettlementResult(previous);
        }

        // 3. 해당하는 턴에는 물가 상승 안내를 표시.
        if (InflationManager.Instance.TryGetNoticeForStartTurn(turn, out InflationNewsData inflationNews))
        {
            yield return eventUI.ShowInflationNotice(inflationNews);
        }

        // 4. 이번 달 고정 이벤트를 안내.
        FixedEventData current = GetFixedEvent(turn);

        if (current != null)
        {
            yield return eventUI.ShowDialogue(current.hint_text);

            // 지출 이벤트는 납부 예정 금액도 안내.
            if (current.value < 0)
            {
                yield return eventUI.ShowExpenseNotice(current);
            }
        }

        // 5. 시장 이벤트가 있다면 긴급속보를 표시.
        // 긴급속보는 일반 신문의 9턴 해금과 관계없이 유지.
        RandomMarketEventData market = GetMarketEvent(turn);

        if (market != null)
        {
            yield return eventUI.ShowBreaking(market, GameManager.Instance.CurrentGameDate);
        }

        eventUI.CloseAll();

        // 스토리와 이벤트 안내가 모두 끝난 뒤 완료 처리.
        state.lastPresentedTurn = turn;
        IsPresenting = false;
        presentationRoutine = null;

        UIManager.Instance?.RefreshUI();
    }

    /// <summary>
    /// 확정 일정과 처리 완료 상태를 복사해서 저장.
    /// </summary>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public EventSaveData CaptureSave()
    {
        RequireState();

        GameManager game = GameManager.Instance;

        if (game == null || !IsTurnReady(game.currentMonth))
            throw new InvalidOperationException("이벤트 안내 또는 정산 중에는 저장할 수 없습니다.");

        return CloneState(state);
    }

    /// <summary>
    /// 저장된 일정 복원.
    /// 새 일정 추첨이나 안내 재실행은 하지 않음.
    /// </summary>
    /// <param name="saved"></param>
    /// <exception cref="ArgumentNullException"></exception>
    public void RestoreSave(EventSaveData saved)
    {
        RequireConfigured();

        if (saved == null)
            throw new ArgumentNullException(nameof(saved));

        if (presentationRoutine != null)
        {
            StopCoroutine(presentationRoutine);
            presentationRoutine = null;
        }

        eventUI.CloseAll();
        IsPresenting = false;

        state = CloneState(saved);
    }

    private static EventSaveData CloneState(EventSaveData source)
    {
        // 원본 저장 객체와 실행 중 상태가 같은 목록을 공유하지 않도록 복사.
        return JsonUtility.FromJson<EventSaveData>(JsonUtility.ToJson(source));
    }

    /// <summary>
    /// 기본 저장 검사 + 현재 이벤트 JSON과의 일치 검사.
    /// 게임 상태를 변경하기 전에 호출.
    /// </summary>
    /// <param name="save"></param>
    public void ValidateSavedEvents(GameSaveData save)
    {
        RequireConfigured();

        // 목록 누락, 중복 ID, 처리 턴 등 기본 구조부터 검사.
        SaveDataValidator.Validate(save);

        EventSaveData saved = save.pendingEvent;

        RequireValid(saved.catalogHash == catalogHash, "저장 당시 이벤트 JSON과 현재 JSON이 다릅니다.");

        // 고정 이벤트가 지정된 네 턴에 각각 하나씩 존재하는지 검사.
        foreach (int turn in catalog.fixed_event_turns)
        {
            EventScheduleEntry entry = saved.fixedEvents.Find(x => x.turn == turn);

            RequireValid(entry != null && fixedById.ContainsKey(entry.id), "저장된 고정 이벤트 일정이 올바르지 않습니다.");
        }

        foreach (EventScheduleEntry entry in saved.marketEvents)
        {
            RequireValid(marketById.ContainsKey(entry.id), "저장된 시장 이벤트 ID가 현재 JSON에 없습니다.");

            bool inRange = catalog.random_event_ranges.Any(x => entry.turn >= x.start_turn && entry.turn <= x.end_turn);

            RequireValid(inRange, "시장 이벤트가 허용 구간 밖에 있습니다.");

            // 저장된 실제 시장도 이벤트 TECH 값과 일치해야 함.
            MarketTurnSaveData marketRow = save.market.turns.Find(x => x.turn == entry.turn);

            decimal expected = (decimal)marketById[entry.id].tech_return;

            RequireValid(marketRow != null && SaveNumber.Read(marketRow.stock) == expected, "이벤트 일정과 저장된 시장 수익률이 다릅니다.");
        }

        // 구간마다 1~2회, 두 번이면 호재|악재 각각 한 번.
        foreach (RandomEventRangeData range in catalog.random_event_ranges)
        {
            List<EventScheduleEntry> entries =
                saved.marketEvents.Where(
                    x => x.turn >= range.start_turn &&
                         x.turn <= range.end_turn).ToList();

            RequireValid(entries.Count >= range.min_count && entries.Count <= range.max_count, "구간별 시장 이벤트 횟수가 올바르지 않습니다.");

            if (entries.Count == 2)
            {
                int positives = entries.Count(x => marketById[x.id].tech_return > 0);

                RequireValid(positives == 1, "두 번 발생하는 구간은 호재·악재가 각각 하나여야 합니다.");
            }
        }

        // 시장 이벤트 턴에는 유료,무료 정보가 모두 없어야 함.
        bool informationBlocked = saved.marketEvents.Any(x => x.turn == save.player.currentMonth);

        if (informationBlocked)
        {
            RequireValid(save.information.reveals.Count == 0 && save.information.usedTexts.Count == 0, "시장 이벤트 턴에 정보 공개 기록이 있습니다.");
        }
    }

    private void RequireConfigured()
    {
        if (!IsConfigured)
            throw new InvalidOperationException("EventManager의 JSON 및 EventFlowUI 연결을 확인하세요.");
    }

    private void RequireState()
    {
        if (state == null)
            throw new InvalidOperationException("이벤트 일정이 아직 준비되지 않았습니다.");
    }

    private static void RequireValid(bool condition, string message)
    {
        if (!condition)
            throw new FormatException(message);
    }
    // EventManager 전면 리뉴얼로 인해 더이상 하단 코드들은 사용하지 않음.
    /*
    
    Inspector Zone
    
    //public static EventManager Instance;

    [Header("Pending Penalty")]
    public long pendingPenaltyAmount = 0; //유예된 청구서 금액
    public string pendingEventName = "";  //유예된 청구서 사유

    private SaveRandom eventRandom; // 이벤트도 별도의 난수(seed)를 갖도록 함.
    private const int EventSeedSalt = 0x27439A51; // 같은 시장 시드에서도 이벤트용 난수를 별도로 만들기 위한 값

    
    function Zone
    

    //싱글톤 패턴
    private void Awake()
    {
        if(Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    /// <summary>
    /// GameManager에서 턴이 넘어갈 때마다 호출하는 함수
    /// </summary>
    public void CheckMonthlyEvent(int currentMonth)
    {
        // 새 게임에서 저장을 한 번도 하지 않았더라도 이벤트 추첨 전에 난수가 반드시 준비되도록 함. - 안할 시 nullReferenceException 발생함..
        EnsureRandom();

        // 1. 확정적 생애 주기 이벤트 (프로토타입용 => 추후 밸런싱 필요)
        if (currentMonth == 24) // 2년 차
        {
            SetPendingEvent("집주인의 전세 보증금 인상 통보", 15000000); // 1,500만 원
        }
        else if (currentMonth == 60) // 5년 차
        {
            SetPendingEvent("결혼 및 주거 독립 자금 지출", 30000000); // 3,000만 원
        }
        else if (currentMonth == 96) // 8년 차
        {
            SetPendingEvent("자녀 양육비 및 교육비 목돈 지출", 20000000); // 2,000만 원
        }
        // 2. 무작위 돌발 지출 (예: 5% 확률로 150만 원 지출)
        else if (eventRandom.NextDouble() <= 0.05) 
        {
            SetPendingEvent("경조사 및 갑작스러운 질병 발생", 1500000); // 150만 원
        }
    }
    
    /// <summary>
    /// 이벤트를 즉시 차감하지 않고, 다음 턴까지 유예(대기)시키는 함수
    /// </summary>
    private void SetPendingEvent(string name, long cost)
    {
        pendingEventName = name;
        pendingPenaltyAmount = cost;
        Debug.LogWarning($"[이벤트 발생] 다음 달 턴 종료 전까지 {cost:N0}원을 마련해야 합니다.");

        //추후 UIManager로 경고 UI 호출
        //UIManager.Instance.ShowWarningPopup(name, cost);
    }

    /// <summary>
    /// 다음 턴 결산 시 호출되어, 실제로 돈을 차감하고 파산 여부를 묻는 함수 - 해당 파트도 청구서 추가로 인한 수정
    /// </summary>
    public void ResolvePendingPenalty()
    {
        // 처리할 이벤트 비용이 없으면 종료.
        if (pendingPenaltyAmount <= 0)
            return;

        // 초기화하기 전에 이번에 처리할 내용을 복사.
        string eventName = pendingEventName;
        long amount = pendingPenaltyAmount;

        // 대기 중인 이벤트 청구를 초기화.
        pendingPenaltyAmount = 0;
        pendingEventName = "";

        // 복사한 값으로 실제 차감 및 청구서 기록.
        GameManager.Instance.ApplyMandatoryExpense(amount, $"이벤트: {eventName}", ReceiptLineType.Change);
    }

    /// <summary>
    /// 이벤트 난수가 없을 때만 생성.
    /// 이미 생성했거나 저장 상태를 복원했다면 그대로 유지함
    /// </summary>
    /// <exception cref="System.InvalidOperationException"></exception>
    private void EnsureRandom()
    {
        // 매 턴 다시 생성하면 난수 진행 상태가 초기화되므로, 이미 존재할 때는 아무것도 하지 않음.
        if (eventRandom != null)
            return;

        DataManager data = DataManager.Instance;

        // 시장 시드가 확정되기 전에 잘못된 시드로 생성하지 않음.
        if (data == null || !data.IsMarketReady)
        {
            throw new System.InvalidOperationException("시장 데이터가 준비되기 전에 이벤트 난수 초기화가 요청되었습니다.");
        }

        int seed = unchecked(data.MarketSeed ^ EventSeedSalt);

        eventRandom = new SaveRandom(seed);
    }

    public EventSaveData CaptureSave()
    {
        EnsureRandom();

        return new EventSaveData
        {
            //pendingPenaltyAmount = pendingPenaltyAmount,
            //pendingEventName = pendingEventName,
            //randomState = eventRandom.State
        };
    }

    public void RestoreSave(EventSaveData data)
    {
        //pendingPenaltyAmount = data.pendingPenaltyAmount;
        //pendingEventName = data.pendingEventName;

        eventRandom = new SaveRandom(1);
        //eventRandom.Restore(data.randomState);
    }
    */
}
