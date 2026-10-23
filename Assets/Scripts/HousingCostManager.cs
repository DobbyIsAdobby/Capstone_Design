using System;
using UnityEngine;

/// <summary>
/// 이번 달에 청구할 주거비 계산 결과
/// </summary>
public readonly struct HousingMonthlyCost
{
    public string HousingName { get; }
    public long Rent { get; }
    public long Maintenance { get; }

    public long Total => checked(Rent + Maintenance);

    public HousingMonthlyCost(string housingName, long rent, long maintenance)
    {
        HousingName = housingName;
        Rent = rent;
        Maintenance = maintenance;
    }
}

public class HousingCostManager : Singleton<HousingCostManager>
{
    [Serializable]
    private sealed class HousingEntry
    {
        [SerializeField] private string housingName;
        [SerializeField] private long baseRent;
        [SerializeField] private long baseMaintenance;

        public string HousingName => housingName;
        public long BaseRent => baseRent;
        public long BaseMaintenance => baseMaintenance;

        public HousingEntry(string name, long rent, long maintenance)
        {
            housingName = name;
            baseRent = rent;
            baseMaintenance = maintenance;
        }
    }

    [Header("Tier")]
    [SerializeField]
    private HousingEntry[] entries =
    {
        new HousingEntry("반지하", 800_000, 0),
        new HousingEntry("옥탑방", 1_200_000, 0),
        new HousingEntry("빌라", 1_500_000, 100_000),
        new HousingEntry("오피스텔", 1_800_000, 200_000),
        new HousingEntry("아파트", 2_000_000, 400_000)
    };

    public bool IsConfigured { get; private set; }

    protected override void OnSingletonAwake()
    {
        IsConfigured = false;

        if (entries == null || entries.Length != 5)
            throw new InvalidOperationException("티어별 주거비를 흙·동·은·금·다이아 순서로 5개 설정하세요.");

        foreach (HousingEntry entry in entries)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.HousingName) || entry.BaseRent < 0 || entry.BaseMaintenance < 0)
            {
                throw new InvalidOperationException("주거 이름과 기본 월세·관리비를 확인하세요.");
            }
        }

        IsConfigured = true;
    }

    /// <summary>
    /// 이번 턴 시작에 확정된 티어로 주거비 조회.
    /// 아직 월초 초기화 전이라면 false 반환.
    /// 조회 과정에서 돈을 차감하지 않음.
    /// </summary>
    /// <param name="cost"></param>
    /// <returns></returns>
    public bool TryGetCurrent(out HousingMonthlyCost cost)
    {
        cost = default;

        GameManager game = GameManager.Instance;
        WealthTierManager wealth = WealthTierManager.Instance;
        InflationManager inflation = InflationManager.Instance;

        if (!IsConfigured ||
            game == null ||
            wealth == null ||
            !wealth.IsConfigured ||
            inflation == null ||
            !inflation.IsConfigured ||
            wealth.AppliedTurn != game.currentMonth)
        {
            return false;
        }

        int tierIndex = wealth.CurrentTierIndex;

        if (tierIndex < 0 || tierIndex >= entries.Length)
            return false;

        HousingEntry entry = entries[tierIndex];

        // 월세 전용 규칙: 인상 시마다 최초 월세의 25% 추가.
        long rent = inflation.CalculateRent(entry.BaseRent, game.currentMonth);

        // 관리비는 동일하게 100%씩 증가. - 이거라도 해야 더 어렵지 않을까?
        long maintenance = inflation.CalculateGeneralPrice(entry.BaseMaintenance, game.currentMonth);

        cost = new HousingMonthlyCost(entry.HousingName, rent, maintenance);

        return true;
    }
}
