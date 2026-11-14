using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class SpotlightTutorialManager : Singleton<SpotlightTutorialManager>
{
    /// <summary>
    /// FSM State Enum Class
    /// </summary>
    public enum TutorialState
    {
        Idle,
        Preparing,
        Dialogue,
        WaitingForEvent,
        Completing,
        Fault
    }

    [Serializable]
    public sealed class PanelSignal
    {
        public GameObject panel;
        public string openedEvent;
        public string closedEvent;

        [NonSerialized] public bool previousActive;
    }

    [Header("Data / UI")]
    [SerializeField] private TextAsset tutorialJson;
    [SerializeField] private SpotlightTutorialUI tutorialUI;

    [Header("Panel Signals")]
    [SerializeField] private PanelSignal[] panelSignals;

    [Header("Priority Panels")]
    [SerializeField] private GameObject newspaperPanel;

    // 튜토리얼보다 먼저 확인해야 하는 패널. ex)병원비 청구서 패널
    [SerializeField] private GameObject[] interruptPanels;

    private SpotlightCatalog catalog;
    private SpotlightSaveData saveData;
    private string configHash;

    private readonly Dictionary<string, SpotlightGuide> guides = new Dictionary<string, SpotlightGuide>();

    private readonly Dictionary<string, int> tutorialCounts = new Dictionary<string, int>();

    private SpotlightGuide currentGuide;
    private SpotlightTutorialUI.TargetBinding currentTarget;

    private int stepIndex;
    private int dialogueIndex;
    private int stepCount;
    private int readyFrame;

    private bool targetPrepared;
    private bool suspended;
    private bool trackingPanels;

    public bool IsConfigured { get; private set; }
    public bool IsInitialized => saveData != null;
    public bool IsBusy => currentGuide != null;

    public TutorialState State { get; private set; } = TutorialState.Idle;

    private SpotlightStep CurrentStep => currentGuide.steps[stepIndex];

    private bool HasInterruptPanel =>
        interruptPanels != null &&
        interruptPanels.Any(panel =>
            panel != null && panel.activeInHierarchy);

    public bool CanSaveNow =>
        IsInitialized &&
        !IsBusy &&
        FindDueFixedGuide() == null;

    public bool CanLeaveTurn =>
        CanSaveNow &&
        !HasReadyContextGuide();

    public bool CanAdvanceNewspaper =>
        !IsBusy &&
        (!IsInitialized || !catalog.context_guides.Any(guide =>
            guide.trigger == "EconomicNewspaperOpened" &&
            saveData.pending.Contains(guide.id) &&
            saveData.completed.Contains(guide.required_completed_id)));

    protected override void OnSingletonAwake()
    {
        IsConfigured = false;

        try
        {
            if (tutorialJson == null || tutorialUI == null || newspaperPanel == null)
            {
                throw new InvalidOperationException("SpotlightTutorialManager의 연결을 확인하세요.");
            }

            tutorialUI.Initialize();

            catalog = JsonUtility.FromJson<SpotlightCatalog>(tutorialJson.text);

            if (catalog == null || catalog.version != 3 || catalog.fixed_tutorials == null || catalog.context_guides == null)
            {
                throw new InvalidOperationException("tutorial_triggers.json v3가 필요합니다.");
            }

            guides.Clear();

            foreach (SpotlightGuide guide in catalog.fixed_tutorials.Concat(catalog.context_guides))
            {
                ValidateGuide(guide);
                guides.Add(guide.id, guide);
            }

            // 1~10턴에 고정 안내가 하나씩 존재함
            int[] turns = catalog.fixed_tutorials.Select(guide => guide.turn).OrderBy(turn => turn).ToArray();

            if (!turns.SequenceEqual(Enumerable.Range(1, 10)))
            {
                throw new InvalidOperationException("고정 안내는 1~10턴에 하나씩 있어야 합니다.");
            }

            foreach (SpotlightGuide guide in catalog.context_guides)
            {
                if (string.IsNullOrWhiteSpace(guide.trigger) || string.IsNullOrWhiteSpace(guide.required_completed_id) || !guides.ContainsKey(guide.required_completed_id))
                {
                    throw new InvalidOperationException($"조건부 안내의 신호 또는 선행 ID 오류: {guide.id}");
                }
            }

            configHash = Hash128.Compute(tutorialJson.text).ToString();

            IsConfigured = true;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
        }
    }

    private void ValidateGuide(SpotlightGuide guide)
    {
        if (guide == null || string.IsNullOrWhiteSpace(guide.id) || guides.ContainsKey(guide.id) || !guide.once || guide.steps == null || guide.steps.Length == 0)
        {
            throw new InvalidOperationException("튜토리얼 ID, once 또는 steps를 확인하세요.");
        }

        for (int i = 0; i < guide.steps.Length; i++)
        {
            SpotlightStep step = guide.steps[i];

            if (step == null)
                throw new InvalidOperationException("빈 단계가 있습니다.");

            SpotlightTutorialUI.TargetBinding target = tutorialUI.GetTarget(step.target_id);

            switch (step.type)
            {
                case "DIALOGUE":
                    if (step.dialogues == null || step.dialogues.Length == 0 || step.dialogues.Any(string.IsNullOrWhiteSpace))
                    {
                        throw new InvalidOperationException($"대사가 없습니다: {guide.id}");
                    }
                    break;

                case "WAIT_EVENT":
                    if (target == null ||
                        target.button == null ||
                        string.IsNullOrWhiteSpace(step.event_name) ||
                        string.IsNullOrWhiteSpace(step.instruction) ||
                        step.count <= 0 ||
                        (step.count_scope != "STEP" &&
                         step.count_scope != "TUTORIAL"))
                    {
                        throw new InvalidOperationException($"행동 대기 설정을 확인하세요: {guide.id}");
                    }
                    break;

                case "COMPLETE":
                    if (i != guide.steps.Length - 1)
                    {
                        throw new InvalidOperationException("COMPLETE는 마지막 단계에만 사용할 수 있습니다.");
                    }
                    break;

                default:
                    throw new InvalidOperationException($"알 수 없는 단계: {step.type}");
            }
        }

        if (guide.steps[guide.steps.Length - 1].type != "COMPLETE")
        {
            throw new InvalidOperationException($"마지막 COMPLETE가 없습니다: {guide.id}");
        }
    }

    public void InitializeNewGame()
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("Spotlight 튜토리얼 설정이 준비되지 않았습니다.");
        }

        ResetPlayback();

        saveData = new SpotlightSaveData
        {
            configHash = configHash
        };

        trackingPanels = false;
    }

    /// <summary>
    /// 행동 성공 이후 호출. 
    /// 여기서 화면을 바로 바꾸지 않고 다음 Update에서 진행함.
    /// </summary>
    /// <param name="eventName"></param>
    public void Notify(string eventName)
    {
        if (!IsInitialized || string.IsNullOrWhiteSpace(eventName))
            return;

        if (IsBusy)
        {
            tutorialCounts.TryGetValue(eventName, out int previousCount);
            tutorialCounts[eventName] = previousCount + 1;

            if (HasInterruptPanel)
                tutorialUI.BlockTargetInput();

            if (State == TutorialState.WaitingForEvent && CurrentStep.event_name == eventName)
            {
                stepCount++;

                // 조건 달성 후 추가 클릭이 들어오지 않도록 먼저 막아둠
                if (GetCurrentCount() >= CurrentStep.count)
                    tutorialUI.BlockTargetInput();
            }
        }

        // 조건부 안내는 중복 없이 대기 목록에 넣음
        foreach (SpotlightGuide guide in catalog.context_guides)
        {
            if (guide.trigger == eventName && !saveData.completed.Contains(guide.id) && !saveData.pending.Contains(guide.id))
            {
                saveData.pending.Add(guide.id);
            }
        }
    }

    public bool AllowsAction(string targetId)
    {
        if (!IsInitialized)
            return false;

        if (!IsBusy)
            return true;

        return !suspended && !HasInterruptPanel && State == TutorialState.WaitingForEvent && CurrentStep.target_id == targetId && GetCurrentCount() < CurrentStep.count;
    }

    public bool AllowsFeature(TutorialFeature feature)
    {
        if (!IsBusy)
            return IsInitialized;

        switch (feature)
        {
            case TutorialFeature.Overtime:
                return AllowsAction("OVERTIME_BUTTON");

            case TutorialFeature.Shop:
                return AllowsAction("SHOP_BUTTON");

            case TutorialFeature.AssetNavigation:
                return AllowsAction("ASSET_BUTTON");

            case TutorialFeature.BankDeposit:
                return AllowsAction("BANK_BUTTON");

            case TutorialFeature.JobMinigame:
                return AllowsAction("JOB_BUTTON");

            case TutorialFeature.Information:
                return AllowsAction("INFORMATION_BUTTON");

            case TutorialFeature.BankLoan:
                return AllowsAction("BANK_LOAN_BUTTON");

            default:
                return false;
        }
    }

    private SpotlightGuide FindDueFixedGuide()
    {
        if (!IsInitialized || GameManager.Instance == null)
            return null;

        return catalog.fixed_tutorials.Where(guide => guide.turn <= GameManager.Instance.currentMonth && !saveData.completed.Contains(guide.id)).OrderBy(guide => guide.turn).FirstOrDefault();
    }

    private bool HasReadyContextGuide()
    {
        return IsInitialized && saveData.pending.Any(id => saveData.completed.Contains(guides[id].required_completed_id));
    }

    private void Update()
    {
        if (!IsInitialized || GameManager.Instance == null)
            return;

        if (GameManager.Instance.IsGameOver)
        {
            ResetPlayback();
            return;
        }

        try
        {
            PollPanelSignals();
            UpdateState();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);

            if (currentGuide != null)
            {
                State = TutorialState.Fault;

                tutorialUI.Show(null, "안내 대상을 준비하지 못했습니다.\n" + "연결 상태를 확인하거나 안내를 건너뛰세요.", false, "", false);
            }
        }
    }

    private void PollPanelSignals()
    {
        if (panelSignals == null)
            return;

        foreach (PanelSignal signal in panelSignals)
        {
            if (signal == null || signal.panel == null)
                continue;

            bool active = signal.panel.activeInHierarchy;

            // 최초 상태는 열기/닫기 행동으로 계산하면 안됨
            if (trackingPanels && active != signal.previousActive)
            {
                Notify(active ? signal.openedEvent : signal.closedEvent);
            }

            signal.previousActive = active;
        }

        trackingPanels = true;
    }

    private void UpdateState()
    {
        if (!IsBusy)
        {
            if (HasInterruptPanel || PauseController.IsPaused)
                return;

            // 경제 신문 안내만 월말 정산 중에도 읽기 전용으로 실행.
            SpotlightGuide newspaperGuide =
                catalog.context_guides.FirstOrDefault(guide =>
                    guide.trigger == "EconomicNewspaperOpened" &&
                    saveData.pending.Contains(guide.id) &&
                    saveData.completed.Contains(
                        guide.required_completed_id));

            if (newspaperPanel.activeInHierarchy && newspaperGuide != null)
            {
                BeginGuide(newspaperGuide);
                return;
            }

            // 일반 안내는 기존 월초 안내가 끝난 후에만 시작
            if (!GameManager.Instance.CanAct || newspaperPanel.activeInHierarchy)
            {
                return;
            }

            SpotlightGuide fixedGuide = FindDueFixedGuide();

            if (fixedGuide != null)
            {
                BeginGuide(fixedGuide);
                return;
            }

            SpotlightGuide contextGuide =
                catalog.context_guides.FirstOrDefault(guide =>
                    guide.trigger != "EconomicNewspaperOpened" &&
                    saveData.pending.Contains(guide.id) &&
                    saveData.completed.Contains(
                        guide.required_completed_id));

            if (contextGuide != null)
                BeginGuide(contextGuide);

            return;
        }

        // 병원 안내가 나타나면 튜토리얼만 잠시 숨김
        if (HasInterruptPanel || PauseController.IsPaused)
        {
            if (!suspended)
            {
                suspended = true;
                tutorialUI.Hide();
            }

            return;
        }

        if (suspended)
        {
            suspended = false;
            RenderCurrentState();
            readyFrame = Time.frameCount + 1;
            return;
        }

        if (Time.frameCount < readyFrame)
            return;

        if (tutorialUI.SkipRequested)
        {
            FinishGuide(true);
            return;
        }

        // FSM
        switch (State)
        {
            case TutorialState.Preparing:
                if (!targetPrepared)
                {
                    currentTarget = tutorialUI.GetTarget(CurrentStep.target_id);

                    tutorialUI.PrepareTarget(currentTarget);

                    targetPrepared = true;

                    // 메뉴, 스크롤 변경 후 한 프레임 대기
                    readyFrame = Time.frameCount + 1;
                    return;
                }

                if (CurrentStep.type == "COMPLETE")
                {
                    State = TutorialState.Completing;
                    return;
                }

                State = CurrentStep.type == "DIALOGUE" ? TutorialState.Dialogue : TutorialState.WaitingForEvent;

                stepCount = 0;
                RenderCurrentState();
                readyFrame = Time.frameCount + 1;
                break;

            case TutorialState.Dialogue:
                if (!tutorialUI.NextRequested)
                    return;

                dialogueIndex++;

                if (dialogueIndex >= CurrentStep.dialogues.Length)
                {
                    AdvanceStep();
                }
                else
                {
                    RenderCurrentState();
                    readyFrame = Time.frameCount + 1;
                }
                break;

            case TutorialState.WaitingForEvent:
                if (GetCurrentCount() >= CurrentStep.count)
                {
                    AdvanceStep();
                    return;
                }

                tutorialUI.SetProgress(
                    $"{GetCurrentCount()} / {CurrentStep.count}");
                break;

            case TutorialState.Completing:
                FinishGuide(false);
                break;

            // Fault 상태에서는 건너뛰기만 허용
            case TutorialState.Fault:
                break;
        }
    }

    private int GetCurrentCount()
    {
        if (CurrentStep.count_scope != "TUTORIAL")
            return stepCount;

        return tutorialCounts.TryGetValue(CurrentStep.event_name, out int count) ? count : 0;
    }

    private void BeginGuide(SpotlightGuide guide)
    {
        currentGuide = guide;
        stepIndex = 0;
        tutorialCounts.Clear();

        EnterPreparing();
    }

    private void AdvanceStep()
    {
        stepIndex++;
        EnterPreparing();
    }

    private void EnterPreparing()
    {
        dialogueIndex = 0;
        stepCount = 0;
        targetPrepared = false;

        State = TutorialState.Preparing;
        readyFrame = Time.frameCount + 1;

        // 안내 위치를 준비하는 동안 입력 차단만 유지
        tutorialUI.ShowPreparing();
    }

    private void RenderCurrentState()
    {
        if (State == TutorialState.Dialogue)
        {
            tutorialUI.Show(
                currentTarget?.target,
                CurrentStep.dialogues[dialogueIndex],
                true,
                "",
                false);
        }
        else if (State == TutorialState.WaitingForEvent)
        {
            tutorialUI.Show(
                currentTarget?.target,
                CurrentStep.instruction +
                "\n진행하기 어려우면 안내를 건너뛸 수 있습니다.",
                false,
                $"{GetCurrentCount()} / {CurrentStep.count}",
                true);
        }
        else if (State == TutorialState.Fault)
        {
            tutorialUI.Show(
                null,
                "안내 연결을 확인하세요.\n건너뛰기로 돌아갈 수 있습니다.",
                false,
                "",
                false);
        }
        else
        {
            // 준비 또는 완료 전환 중에는 임시 안내 문구를 표시하지 않음.
            tutorialUI.ShowPreparing();
        }
    }

    private void FinishGuide(bool skipped)
    {
        string id = currentGuide.id;

        if (!saveData.completed.Contains(id))
            saveData.completed.Add(id);

        if (skipped && !saveData.skipped.Contains(id))
            saveData.skipped.Add(id);

        saveData.pending.Remove(id);

        ResetPlayback();
    }

    private void ResetPlayback()
    {
        currentGuide = null;
        currentTarget = null;
        suspended = false;

        State = TutorialState.Idle;
        tutorialCounts.Clear();

        if (tutorialUI != null)
            tutorialUI.Hide();
    }

    public SpotlightSaveData CaptureSave()
    {
        if (!CanSaveNow)
        {
            throw new InvalidOperationException("튜토리얼 진행 중에는 저장할 수 없습니다.");
        }

        return Clone(saveData);
    }

    public void ValidateSave(SpotlightSaveData saved, int turn)
    {
        if (!IsConfigured || saved == null || saved.configHash != configHash || saved.completed == null || saved.skipped == null || saved.pending == null)
        {
            throw new InvalidOperationException("튜토리얼 저장 데이터 또는 설정이 일치하지 않습니다.");
        }

        foreach (List<string> list in new[]
                 {
                     saved.completed,
                     saved.skipped,
                     saved.pending
                 })
        {
            if (list.Any(id => string.IsNullOrWhiteSpace(id) || !guides.ContainsKey(id)) || list.Distinct().Count() != list.Count)
            {
                throw new InvalidOperationException("튜토리얼 저장 ID가 잘못되었습니다.");
            }
        }

        if (saved.skipped.Any(id => !saved.completed.Contains(id)))
        {
            throw new InvalidOperationException("건너뛰기 기록과 완료 기록이 일치하지 않습니다.");
        }

        foreach (string id in saved.pending)
        {
            if (saved.completed.Contains(id) || !catalog.context_guides.Any(guide => guide.id == id))
            {
                throw new InvalidOperationException("조건부 안내의 대기 기록이 잘못되었습니다.");
            }
        }

        // 저장 가능한 시점에는 현재 턴까지의 고정 안내가 끝나 있어야 함
        foreach (SpotlightGuide guide in catalog.fixed_tutorials)
        {
            bool shouldBeCompleted = guide.turn <= turn;
            bool completed = saved.completed.Contains(guide.id);

            if (shouldBeCompleted != completed)
            {
                throw new InvalidOperationException("고정 안내의 완료 기록과 저장 턴이 일치하지 않습니다.");
            }
        }

        foreach (SpotlightGuide guide in catalog.context_guides)
        {
            if (saved.completed.Contains(guide.id) && !saved.completed.Contains(guide.required_completed_id))
            {
                throw new InvalidOperationException("선행 안내의 완료 기록이 없습니다.");
            }
        }
    }

    public void RestoreSave(SpotlightSaveData saved, int turn)
    {
        ValidateSave(saved, turn);

        ResetPlayback();
        saveData = Clone(saved);
        trackingPanels = false;
    }

    private static SpotlightSaveData Clone(SpotlightSaveData source)
    {
        return JsonUtility.FromJson<SpotlightSaveData>(JsonUtility.ToJson(source));
    }

    protected override void OnDestroy()
    {
        ResetPlayback();
        base.OnDestroy();
    }
}
