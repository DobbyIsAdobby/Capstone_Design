using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class WealthTierManager : Singleton<WealthTierManager>
{
    [Serializable]
    private class TierEntry
    {
        [SerializeField] private string tierName;
        [SerializeField] private long minimumNetWorth;

        [Header("Assets")]
        [SerializeField] private Sprite backgroundSprite;
        [SerializeField] private RuntimeAnimatorController characterController;

        public string Name => tierName;
        public long MinimumNetWorth => minimumNetWorth;
        public Sprite BackgroundSprite => backgroundSprite;

        public RuntimeAnimatorController CharacterController =>
            characterController;

        public TierEntry(string name, long minimum)
        {
            tierName = name;
            minimumNetWorth = minimum;
        }
    }

    [Header("Scene")]
    [SerializeField] private SpriteRenderer backgroundRenderer;
    [SerializeField] private Animator characterAnimator;
    [SerializeField] private TMP_Text tierText;

    [Header("WealthTier")]
    [SerializeField]
    private List<TierEntry> tiers = new List<TierEntry>
    {
        new TierEntry("흙수저", 0),
        new TierEntry("동수저", 25_000_000),
        new TierEntry("은수저", 100_000_000),
        new TierEntry("금수저", 300_000_000),
        new TierEntry("다이아수저", 1_000_000_000)
    };

    public bool IsConfigured { get; private set; }

    // 아직 첫 턴을 적용하지 않은 상태는 -1
    public int CurrentTierIndex { get; private set; } = -1;

    public int AppliedTurn { get; private set; } = -1;

    // 해당 턴 티어 판정에 사용한 순자산
    public long NetWorthAtTurnStart { get; private set; }

    public string CurrentTierName => CurrentTierIndex >= 0 ? tiers[CurrentTierIndex].Name : "준비 중";

    protected override void OnSingletonAwake()
    {
        IsConfigured = ValidateConfiguration(out string error);

        if (!IsConfigured)
            Debug.LogError(error, this);
    }

    private bool ValidateConfiguration(out string error)
    {
        error = "";

        if (backgroundRenderer == null || characterAnimator == null || tierText == null)
        {
            error = "배경 Renderer, 캐릭터 Animator, TierText를 연결하세요.";
            return false;
        }

        // 캐릭터 애니메이션은 SpriteRenderer 사용
        if (characterAnimator.GetComponent<SpriteRenderer>() == null)
        {
            error = "캐릭터 Animator와 SpriteRenderer를 같은 오브젝트에 두세요.";
            return false;
        }

        if (tiers == null || tiers.Count != 5)
        {
            error = "재산 티어는 5개여야 합니다.";
            return false;
        }

        for (int i = 0; i < tiers.Count; i++)
        {
            TierEntry tier = tiers[i];

            if (tier == null || string.IsNullOrWhiteSpace(tier.Name) || tier.BackgroundSprite == null || tier.CharacterController == null)
            {
                error = $"{i + 1}티어 이름과 외형 에셋을 확인하세요.";
                return false;
            }

            if (i == 0)
            {
                if (tier.MinimumNetWorth != 0)
                {
                    error = "첫 티어의 기준 금액은 0으로 설정하세요.";
                    return false;
                }
            }
            else if (tier.MinimumNetWorth <= tiers[i - 1].MinimumNetWorth)
            {
                error = "티어 기준 금액은 낮은 순서대로 증가해야 합니다.";
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 순자산에 해당하는 티어를 계산
    /// 이 함수는 화면을 바꾸지 않음
    /// </summary>
    /// <param name="netWorth"></param>
    /// <returns></returns>
    private int FindTierIndex(long netWorth)
    {
        // 순자산이 음수여도 최소 등급은 흙수저
        int result = 0;

        for (int i = 1; i < tiers.Count; i++)
        {
            if (netWorth < tiers[i].MinimumNetWorth)
                break;

            result = i;
        }

        return result;
    }

    /// <summary>
    /// 첫 게임 시작 및 새로운 턴 진입 시에만 호출.
    /// 턴 진행 중 자산 변화에는 반응하지 않음.
    /// </summary>
    /// <param name="turn"></param>
    /// <param name="netWorth"></param>
    public void ApplyAtTurnStart(int turn, long netWorth)
    {
        if (!IsConfigured)
            return;

        // 같은 턴의 중복 호출로 외형이 다시 판정되는 것을 막음
        if (turn <= AppliedTurn)
            return;

        GameManager game = GameManager.Instance;

        if (game == null || game.IsGameOver || game.currentMonth != turn)
        {
            return;
        }

        int nextTierIndex = FindTierIndex(netWorth);
        bool changed = nextTierIndex != CurrentTierIndex;

        AppliedTurn = turn;
        NetWorthAtTurnStart = netWorth;

        // 같은 티어라면 애니메이션을 처음부터 다시 재생하지 않음
        if (!changed)
        {
            tierText.text = CurrentTierName;
            return;
        }

        CurrentTierIndex = nextTierIndex;
        TierEntry current = tiers[CurrentTierIndex];

        // 기존 오브젝트를 유지하고 외형 에셋만 교체
        backgroundRenderer.sprite = current.BackgroundSprite;

        characterAnimator.enabled = true;
        characterAnimator.runtimeAnimatorController =
            current.CharacterController;

        // 새 Controller의 기본 상태와 첫 프레임을 반영
        characterAnimator.Rebind();
        characterAnimator.Update(0f);

        tierText.text = current.Name;

        Debug.Log(
            $"[{turn}턴] 재산 등급: {current.Name} / " + $"판정 순자산: {netWorth:N0}원");
    }

    /// <summary>
    /// 전달한 순자산이 다이아수저 조건을 충족하는지 확인.
    /// 화면에 표시 중인 등급이나 배경은 변경하지 않음.
    /// </summary>
    /// <param name="netWorth"></param>
    /// <returns></returns>
    public bool MeetsDiamondRequirement(long netWorth)
    {
        if (!IsConfigured)
            return false;

        // 현재 5개 티어 중 마지막 티어가 다이아수저임
        // Inspector의 기준 금액 그대로 사용
        return FindTierIndex(netWorth) == tiers.Count - 1;
    }

    public WealthTierSaveData CaptureSave()
    {
        return new WealthTierSaveData
        {
            currentTierIndex = CurrentTierIndex,
            appliedTurn = AppliedTurn,
            netWorthAtTurnStart = NetWorthAtTurnStart
        };
    }

    public void RestoreSave(WealthTierSaveData data)
    {
        if (!IsConfigured || data.currentTierIndex < 0 || data.currentTierIndex >= tiers.Count)
        {
            throw new InvalidOperationException("저장된 재산 등급을 복원할 수 없습니다.");
        }

        CurrentTierIndex = data.currentTierIndex;
        AppliedTurn = data.appliedTurn;
        NetWorthAtTurnStart = data.netWorthAtTurnStart;

        TierEntry tier = tiers[CurrentTierIndex];

        // 현재 순자산으로 다시 판정하지 않고 저장된 외형을 복원
        backgroundRenderer.sprite = tier.BackgroundSprite;
        characterAnimator.runtimeAnimatorController = tier.CharacterController;
        characterAnimator.enabled = true;

        if (characterAnimator.gameObject.activeInHierarchy)
        {
            characterAnimator.Rebind();
            characterAnimator.Update(0f);
        }

        tierText.text = tier.Name;
    }
}
