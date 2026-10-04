using UnityEngine;

public class JobManager : Singleton<JobManager>
{
    [SerializeField] private JobRules rules;

    public bool IsConfigured { get; private set; }
    public bool IsPlaying { get; private set; }

    public int GradeIndex { get; private set; }
    public int CurrentExperience { get; private set; }

    // 진행 중인 미니게임이 어느 턴에서 시작됐는지 기록
    private int attemptTurn = -1;

    // 월 고정 경험치가 같은 턴에 중복 지급되지 않도록 기록
    private int lastMonthlyExperienceTurn = -1;

    public JobRules Rules => rules;

    public bool IsMaxGrade => IsConfigured && GradeIndex == rules.Count - 1;

    public string GradeName => IsConfigured ? rules.GetGrade(GradeIndex).Name : "준비 중";

    public long MonthlySalary => IsConfigured ? rules.GetGrade(GradeIndex).Salary : 0L;

    public int RequiredExperience => IsConfigured ? rules.GetGrade(GradeIndex).RequiredExperience : 0;

    protected override void OnSingletonAwake()
    {
        if (rules == null)
        {
            Debug.LogError("JobManager에 JobRules를 연결하세요.", this);
            return;
        }

        IsConfigured = rules.Validate(out string error);

        if (!IsConfigured)
        {
            Debug.LogError(error, this);
            return;
        }

        // 새로운 게임은 백수, 경험치 0으로 시작
        GradeIndex = 0;
        CurrentExperience = 0;
        IsPlaying = false;
    }

    /// <summary>
    /// 상태를 변경하지 않고 미니게임 시작 가능 여부만 검사
    /// </summary>
    /// <param name="reason"></param>
    /// <returns></returns>
    public bool CanStart(out string reason)
    {
        reason = "";

        if (!IsConfigured)
        {
            reason = "직급 시스템 설정을 확인하세요.";
            return false;
        }

        if (IsPlaying)
        {
            reason = "이미 미니게임을 진행 중입니다.";
            return false;
        }

        if (IsMaxGrade)
        {
            reason = "최고 직급에 도달했습니다.";
            return false;
        }

        GameManager game = GameManager.Instance;

        if (game == null)
        {
            reason = "GameManager가 없습니다.";
            return false;
        }

        return game.CanSpendAP(rules.APCost, out reason);
    }

    /// <summary>
    /// 시작하면 AP를 1회 차감.
    /// </summary>
    /// <param name="reason"></param>
    /// <returns></returns>
    public bool TryStart(out string reason)
    {
        if (!CanStart(out reason))
            return false;

        GameManager game = GameManager.Instance;

        // IsPlaying을 켜기 전에 AP를 차감
        // 이후에는 GameManager.CanAct를 통해 다른 행동을 막음
        if (!game.TrySpendAP(rules.APCost, out reason))
            return false;

        attemptTurn = game.currentMonth;
        IsPlaying = true;
        return true;
    }

    /// <summary>
    /// 미니게임 결과를 확정하고 기본 경험치만 지급
    /// 노트북 경험치는 이 함수에서 지급하지 않음
    /// </summary>
    /// <param name="success"></param>
    /// <param name="message"></param>
    /// <returns></returns>
    public bool Finish(bool success, out string message)
    {
        message = "";

        // 일시정지 중에는 결과를 확정하거나 진행 상태를 해제하지 않음.
        if (PauseController.IsPaused)
        {
            message = "일시정지 중에는 결과를 확정할 수 없습니다.";
            return false;
        }

        if (!IsPlaying)
            return false;

        // 중복 결과 요청이 들어와도 보상이 한 번만 지급되게
        IsPlaying = false;

        GameManager game = GameManager.Instance;

        if (game == null || game.IsGameOver || game.IsSetting || game.currentMonth != attemptTurn)
        {
            message = "게임 상태가 변경되어 훈련이 중단되었습니다.";
            return false;
        }

        int gainedExperience = success ? rules.SuccessExperience : rules.FailureExperience;

        int previousGrade = GradeIndex;

        AddExperience(gainedExperience);

        message = $"{(success ? "성공!" : "실패")}\n" + $"경험치 +{gainedExperience}";

        if (GradeIndex > previousGrade)
        {
            message += $"\n{GradeName} 승급!\n" + $"월급 {MonthlySalary:N0}원";
        }

        return true;
    }

    /// <summary>
    /// 정상적으로 마감된 턴의 고정 경험치를 한 번 지급. 
    /// ShopManager의 구매 다음 턴 적용 규칙을 그대로 사용함
    /// </summary>
    /// <param name="turn"></param>
    public void ProcessMonthlyExperience(int turn)
    {
        GameManager game = GameManager.Instance;

        if (!IsConfigured || game == null || !game.IsSetting || game.IsGameOver || IsPlaying || game.currentMonth != turn)
        {
            return;
        }

        if (turn <= lastMonthlyExperienceTurn)
            return;

        lastMonthlyExperienceTurn = turn;

        if (IsMaxGrade)
            return;

        int gainedExperience = ShopManager.Instance != null ? ShopManager.Instance.GetMonthlyJobExperience(turn) : 0;

        if (gainedExperience <= 0)
            return;

        int previousGrade = GradeIndex;

        AddExperience(gainedExperience);

        Debug.Log($"[{turn}턴] 보유 상품 경험치 +{gainedExperience}");

        if (GradeIndex > previousGrade)
        {
            Debug.Log($"{GradeName} 승급! " + $"다음 턴 급여 {MonthlySalary:N0}원");
        }
    }

    /// <summary>
    /// 요구 경험치를 차감하고 남은 경험치는 다음 직급으로 이월
    /// </summary>
    /// <param name="amount"></param>
    private void AddExperience(int amount)
    {
        if (amount <= 0 || IsMaxGrade)
            return;

        CurrentExperience += amount;

        while (!IsMaxGrade &&
               CurrentExperience >= RequiredExperience)
        {
            CurrentExperience -= RequiredExperience;
            GradeIndex++;
        }

        // 최고 직급에는 다음 승급이 없으므로 진행 경험치를 비움.
        if (IsMaxGrade)
            CurrentExperience = 0;
    }

    public void AbortAttempt()
    {
        // 씬 전환 등으로 강제 종료될 때 진행 잠금을 해제
        // 정상 UI에서는 플레이 중 닫기를 허용하지 않음
        // 이미 사용한 AP는 반환하지 않고 경험치도 지급하지 않음
        IsPlaying = false;
    }

    public JobSaveData CaptureSave()
    {
        return new JobSaveData
        {
            gradeIndex = GradeIndex,
            currentExperience = CurrentExperience,
            lastMonthlyExperienceTurn = lastMonthlyExperienceTurn
        };
    }

    public void RestoreSave(JobSaveData data)
    {
        GradeIndex = data.gradeIndex;
        CurrentExperience = data.currentExperience;
        lastMonthlyExperienceTurn = data.lastMonthlyExperienceTurn;

        // 진행 중 미니게임은 저장 대상이 아님
        IsPlaying = false;
        attemptTurn = -1;
    }
}
