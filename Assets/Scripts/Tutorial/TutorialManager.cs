using System;
using System.Collections.Generic;
using UnityEngine;

public class TutorialManager : MonoBehaviour
{
    // =========================================================
    // Singleton / 자동 생성
    // =========================================================

    public static TutorialManager Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null)
            return;

        GameObject obj = new GameObject("TutorialManager");
        DontDestroyOnLoad(obj);

        Instance = obj.AddComponent<TutorialManager>();
    }


    // =========================================================
    // FSM
    // =========================================================

    public enum TutorialState
    {
        Idle,
        RunningFixed,
        WaitingForEvent,
        RunningContext
    }

    public TutorialState CurrentState { get; private set; }
        = TutorialState.Idle;


    // =========================================================
    // JSON DATA CLASS
    // =========================================================

    [Serializable]
    public class TutorialDatabase
    {
        public int version;

        public List<FixedTutorialData> fixed_tutorials;
        public List<ContextGuideData> context_guides;
    }

    [Serializable]
    public class FixedTutorialData
    {
        public string id;
        public int turn;
        public bool once;

        public List<string> unlock;
        public List<TutorialStepData> steps;
    }

    [Serializable]
    public class TutorialStepData
    {
        public string type;

        public List<string> dialogues;

        public string event_name;
        public int count;
    }

    [Serializable]
    public class ContextGuideData
    {
        public string id;

        public string trigger;

        public bool once;

        // 이 고정형 튜토리얼을 본 뒤에만 실행
        public string required_completed_id;

        public List<string> dialogues;
    }


    // =========================================================
    // 클라이언트 연결용 EVENT
    // =========================================================

    /// <summary>
    /// 기존 대사 UI가 구독.
    /// 두 번째 Action은 모든 대사 출력이 끝난 뒤 반드시 호출.
    /// </summary>
    public static event Action<List<string>, Action>
        OnDialogueRequested;

    /// <summary>
    /// 향후 기능 해금 시스템이 구독.
    /// 예: STOCK, BANK, LOAN 등.
    /// </summary>
    public static event Action<List<string>>
        OnUnlockRequested;


    // =========================================================
    // SAVE
    // =========================================================

    private const string SaveKey =
        "TutorialCompletedIdsV2";

    private HashSet<string> completedTutorials =
        new HashSet<string>();


    // =========================================================
    // CACHE
    // =========================================================

    private TutorialDatabase database;

    // turn → 고정형 튜토리얼
    private Dictionary<int, FixedTutorialData>
        fixedTutorials =
        new Dictionary<int, FixedTutorialData>();

    // Event → 상황형 가이드
    private Dictionary<GameEventType, List<ContextGuideData>>
        contextGuides =
        new Dictionary<GameEventType, List<ContextGuideData>>();


    // =========================================================
    // CURRENT FIXED
    // =========================================================

    private FixedTutorialData currentFixed;

    private int currentStepIndex;

    private TutorialStepData currentWaitStep;

    // 현재 고정형 튜토리얼 중 발생한 이벤트 횟수
    private Dictionary<GameEventType, int>
        currentEventCounts =
        new Dictionary<GameEventType, int>();


    // =========================================================
    // CURRENT CONTEXT
    // =========================================================

    private ContextGuideData currentContext;

    private Queue<ContextGuideData> contextQueue =
        new Queue<ContextGuideData>();

    private HashSet<string> queuedContextIds =
        new HashSet<string>();


    // 고정형이 실행 중인데 다음 턴이 들어온 경우
    private Queue<int> pendingFixedTurns =
        new Queue<int>();


    // =========================================================
    // UNITY
    // =========================================================

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        DontDestroyOnLoad(gameObject);

        LoadTutorialJson();
        BuildCache();
        LoadProgress();
    }

    private void OnEnable()
    {
        GameEventBus.OnEvent += HandleGameEvent;
    }

    private void OnDisable()
    {
        GameEventBus.OnEvent -= HandleGameEvent;
    }


    // =========================================================
    // JSON LOAD
    // =========================================================

    private void LoadTutorialJson()
    {
        TextAsset json =
            Resources.Load<TextAsset>(
                "Database/tutorial_triggers"
            );

        if (json == null)
        {
            Debug.LogError(
                "[Tutorial] tutorial_triggers.json을 찾을 수 없습니다."
            );

            return;
        }

        database =
            JsonUtility.FromJson<TutorialDatabase>(
                json.text
            );

        if (database == null)
        {
            Debug.LogError(
                "[Tutorial] JSON 파싱 실패"
            );
        }
    }


    // =========================================================
    // CACHE
    // =========================================================

    private void BuildCache()
    {
        if (database == null)
            return;

        fixedTutorials.Clear();
        contextGuides.Clear();

        if (database.fixed_tutorials != null)
        {
            foreach (
                FixedTutorialData tutorial
                in database.fixed_tutorials
            )
            {
                fixedTutorials[
                    tutorial.turn
                ] = tutorial;
            }
        }

        if (database.context_guides != null)
        {
            foreach (
                ContextGuideData guide
                in database.context_guides
            )
            {
                GameEventType eventType;

                if (!Enum.TryParse(
                        guide.trigger,
                        true,
                        out eventType))
                {
                    Debug.LogWarning(
                        "[Tutorial] 존재하지 않는 Event: "
                        + guide.trigger
                    );

                    continue;
                }

                if (!contextGuides.ContainsKey(
                        eventType))
                {
                    contextGuides.Add(
                        eventType,
                        new List<ContextGuideData>()
                    );
                }

                contextGuides[eventType]
                    .Add(guide);
            }
        }
    }


    // =========================================================
    // EVENT RECEIVE
    // =========================================================

    private void HandleGameEvent(
        GameEventType eventType,
        int value)
    {
        // 고정형 튜토리얼 도중 발생한 이벤트 횟수 기록
        if (currentFixed != null)
        {
            AddEventCount(eventType);
        }

        // 현재 특정 행동을 기다리는 중이라면 검사
        if (CurrentState ==
            TutorialState.WaitingForEvent)
        {
            CheckWaitingEvent(eventType);
        }

        // 턴 시작 이벤트는 고정형 우선
        if (eventType ==
            GameEventType.TurnStarted)
        {
            if (CurrentState ==
                TutorialState.Idle)
            {
                TryStartFixedTutorial(value);
            }
            else
            {
                pendingFixedTurns.Enqueue(
                    value
                );
            }

            return;
        }

        // 상황형 이벤트 확인
        TryHandleContextEvent(
            eventType
        );
    }


    // =========================================================
    // EVENT COUNT
    // =========================================================

    private void AddEventCount(
        GameEventType eventType)
    {
        if (!currentEventCounts.ContainsKey(
                eventType))
        {
            currentEventCounts[
                eventType
            ] = 0;
        }

        currentEventCounts[
            eventType
        ]++;
    }

    private int GetEventCount(
        GameEventType eventType)
    {
        int count;

        if (currentEventCounts.TryGetValue(
                eventType,
                out count))
        {
            return count;
        }

        return 0;
    }


    // =========================================================
    // FIXED TUTORIAL
    // =========================================================

    private bool TryStartFixedTutorial(
        int turn)
    {
        FixedTutorialData tutorial;

        if (!fixedTutorials.TryGetValue(
                turn,
                out tutorial))
        {
            return false;
        }

        if (tutorial.once &&
            completedTutorials.Contains(
                tutorial.id))
        {
            return false;
        }

        StartFixedTutorial(
            tutorial
        );

        return true;
    }

    private void StartFixedTutorial(
        FixedTutorialData tutorial)
    {
        currentFixed = tutorial;

        currentStepIndex = 0;

        currentWaitStep = null;

        currentEventCounts.Clear();

        CurrentState =
            TutorialState.RunningFixed;


        // 기능 해금 요청
        if (tutorial.unlock != null &&
            tutorial.unlock.Count > 0)
        {
            OnUnlockRequested?.Invoke(
                tutorial.unlock
            );
        }

        RunCurrentStep();
    }


    // =========================================================
    // FIXED STEP
    // =========================================================

    private void RunCurrentStep()
    {
        if (currentFixed == null)
            return;

        if (currentFixed.steps == null)
        {
            CompleteFixedTutorial();
            return;
        }

        while (
            currentStepIndex
            < currentFixed.steps.Count
        )
        {
            TutorialStepData step =
                currentFixed.steps[
                    currentStepIndex
                ];

            string stepType =
                string.IsNullOrEmpty(step.type)
                    ? ""
                    : step.type.ToUpper();


            // -------------------------
            // DIALOGUE
            // -------------------------

            if (stepType == "DIALOGUE")
            {
                CurrentState =
                    TutorialState.RunningFixed;

                RequestDialogues(
                    step.dialogues,
                    delegate
                    {
                        currentStepIndex++;
                        RunCurrentStep();
                    }
                );

                return;
            }


            // -------------------------
            // WAIT_EVENT
            // -------------------------

            if (stepType == "WAIT_EVENT")
            {
                GameEventType waitType;

                if (!Enum.TryParse(
                        step.event_name,
                        true,
                        out waitType))
                {
                    Debug.LogWarning(
                        "[Tutorial] 잘못된 wait event: "
                        + step.event_name
                    );

                    currentStepIndex++;

                    continue;
                }

                int requiredCount =
                    Mathf.Max(
                        1,
                        step.count
                    );

                // 이미 조건 충족된 경우
                if (GetEventCount(waitType)
                    >= requiredCount)
                {
                    currentStepIndex++;

                    continue;
                }

                currentWaitStep = step;

                CurrentState =
                    TutorialState.WaitingForEvent;

                return;
            }


            // -------------------------
            // COMPLETE
            // -------------------------

            if (stepType == "COMPLETE")
            {
                CompleteFixedTutorial();

                return;
            }


            Debug.LogWarning(
                "[Tutorial] 알 수 없는 Step Type: "
                + step.type
            );

            currentStepIndex++;
        }

        CompleteFixedTutorial();
    }


    // =========================================================
    // WAIT EVENT
    // =========================================================

    private void CheckWaitingEvent(
        GameEventType eventType)
    {
        if (currentWaitStep == null)
            return;

        GameEventType waitType;

        if (!Enum.TryParse(
                currentWaitStep.event_name,
                true,
                out waitType))
        {
            return;
        }

        if (waitType != eventType)
            return;

        int requiredCount =
            Mathf.Max(
                1,
                currentWaitStep.count
            );

        if (GetEventCount(eventType)
            < requiredCount)
        {
            return;
        }

        currentWaitStep = null;

        CurrentState =
            TutorialState.RunningFixed;

        currentStepIndex++;

        RunCurrentStep();
    }


    // =========================================================
    // FIXED COMPLETE
    // =========================================================

    private void CompleteFixedTutorial()
    {
        if (currentFixed == null)
            return;

        string id =
            currentFixed.id;

        bool once =
            currentFixed.once;

        currentFixed = null;

        currentWaitStep = null;

        currentEventCounts.Clear();

        CurrentState =
            TutorialState.Idle;

        if (once)
        {
            AddCompleted(id);
        }

        ContinuePendingTutorial();
    }


    // =========================================================
    // CONTEXT GUIDE
    // =========================================================

    private void TryHandleContextEvent(
        GameEventType eventType)
    {
        List<ContextGuideData> guides;

        if (!contextGuides.TryGetValue(
                eventType,
                out guides))
        {
            return;
        }

        foreach (
            ContextGuideData guide
            in guides
        )
        {
            if (guide.once &&
                completedTutorials.Contains(
                    guide.id))
            {
                continue;
            }

            if (currentContext != null &&
                currentContext.id ==
                guide.id)
            {
                continue;
            }

            if (queuedContextIds.Contains(
                    guide.id))
            {
                continue;
            }

            if (CurrentState ==
                    TutorialState.Idle &&
                IsRequirementMet(guide))
            {
                StartContextGuide(
                    guide
                );
            }
            else
            {
                contextQueue.Enqueue(
                    guide
                );

                queuedContextIds.Add(
                    guide.id
                );
            }
        }
    }

    private bool IsRequirementMet(
        ContextGuideData guide)
    {
        if (string.IsNullOrEmpty(
                guide.required_completed_id))
        {
            return true;
        }

        return completedTutorials.Contains(
            guide.required_completed_id
        );
    }

    private void StartContextGuide(
        ContextGuideData guide)
    {
        currentContext = guide;

        CurrentState =
            TutorialState.RunningContext;

        RequestDialogues(
            guide.dialogues,
            CompleteCurrentContext
        );
    }

    private void CompleteCurrentContext()
    {
        if (currentContext == null)
            return;

        ContextGuideData guide =
            currentContext;

        currentContext = null;

        CurrentState =
            TutorialState.Idle;

        if (guide.once)
        {
            AddCompleted(
                guide.id
            );
        }

        ContinuePendingTutorial();
    }


    // =========================================================
    // QUEUE
    // =========================================================

    private void ContinuePendingTutorial()
    {
        // 고정형이 항상 상황형보다 우선
        while (
            pendingFixedTurns.Count > 0
        )
        {
            int turn =
                pendingFixedTurns.Dequeue();

            if (TryStartFixedTutorial(
                    turn))
            {
                return;
            }
        }

        TryRunNextContextGuide();
    }

    private void TryRunNextContextGuide()
    {
        if (CurrentState !=
            TutorialState.Idle)
        {
            return;
        }

        int count =
            contextQueue.Count;

        for (int i = 0;
             i < count;
             i++)
        {
            ContextGuideData guide =
                contextQueue.Dequeue();

            queuedContextIds.Remove(
                guide.id
            );

            if (guide.once &&
                completedTutorials.Contains(
                    guide.id))
            {
                continue;
            }

            if (!IsRequirementMet(
                    guide))
            {
                contextQueue.Enqueue(
                    guide
                );

                queuedContextIds.Add(
                    guide.id
                );

                continue;
            }

            StartContextGuide(
                guide
            );

            return;
        }
    }


    // =========================================================
    // DIALOGUE REQUEST
    // =========================================================

    private void RequestDialogues(
        List<string> dialogues,
        Action onComplete)
    {
        if (dialogues == null ||
            dialogues.Count == 0)
        {
            onComplete?.Invoke();
            return;
        }

        Action<List<string>, Action>
            handler =
                OnDialogueRequested;

        if (handler == null)
        {
            Debug.LogWarning(
                "[Tutorial] Dialogue UI가 연결되지 않았습니다."
            );

            onComplete?.Invoke();

            return;
        }

        bool completed = false;

        Action completeOnce =
            delegate
            {
                if (completed)
                    return;

                completed = true;

                onComplete?.Invoke();
            };

        handler(
            dialogues,
            completeOnce
        );
    }


    // =========================================================
    // SAVE
    // =========================================================

    private void LoadProgress()
    {
        completedTutorials.Clear();

        string saved =
            PlayerPrefs.GetString(
                SaveKey,
                ""
            );

        if (string.IsNullOrEmpty(
                saved))
        {
            return;
        }

        string[] ids =
            saved.Split(
                new char[] { '|' },
                StringSplitOptions
                    .RemoveEmptyEntries
            );

        foreach (string id in ids)
        {
            completedTutorials.Add(
                id
            );
        }
    }

    private void AddCompleted(
        string tutorialId)
    {
        if (string.IsNullOrEmpty(
                tutorialId))
        {
            return;
        }

        completedTutorials.Add(
            tutorialId
        );

        SaveProgress();
    }

    private void SaveProgress()
    {
        string data =
            string.Join(
                "|",
                completedTutorials
            );

        PlayerPrefs.SetString(
            SaveKey,
            data
        );

        PlayerPrefs.Save();
    }


    // =========================================================
    // NEW GAME RESET
    // =========================================================

    public static void ResetSavedProgress()
    {
        PlayerPrefs.DeleteKey(
            SaveKey
        );

        PlayerPrefs.Save();

        if (Instance != null)
        {
            Instance.ResetRuntimeProgress();
        }
    }

    private void ResetRuntimeProgress()
    {
        completedTutorials.Clear();

        contextQueue.Clear();

        queuedContextIds.Clear();

        pendingFixedTurns.Clear();

        currentEventCounts.Clear();

        currentFixed = null;
        currentContext = null;
        currentWaitStep = null;

        currentStepIndex = 0;

        CurrentState =
            TutorialState.Idle;
    }
}