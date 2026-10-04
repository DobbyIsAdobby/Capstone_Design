using System;

/// <summary>
/// 전체 상태 수집과 복원 순서를 정하는 class
/// </summary>
public static class GameSaveCoordinator
{
    public const int FormatVersion = 1;

    // 상품 효과나 직급 규칙 등 저장 호환성이 달라지면 올림.
    public const int ContentVersion = 1;

    public static GameSaveData Capture()
    {
        RequireManagers();

        GameManager game = GameManager.Instance;

        if (!game.CanSaveCurrentState)
            throw new InvalidOperationException("현재는 저장할 수 없습니다.");

        var data = new GameSaveData
        {
            formatVersion = FormatVersion,
            contentVersion = ContentVersion,
            savedAtUTC = DateTime.UtcNow.ToString("O"),

            summary = new SaveSummaryData
            {
                turn = game.currentMonth,
                netWorth = game.TotalAsset,
                jobGradeName = JobManager.Instance.GradeName,
                wealthTierName = WealthTierManager.Instance.CurrentTierName
            },

            player = game.CaptureSave(),
            assets = AssetManager.Instance.CaptureSave(),
            market = DataManager.Instance.CaptureSave(),
            shop = ShopManager.Instance.CaptureSave(),
            loans = LoanManager.Instance.CaptureSave(),
            job = JobManager.Instance.CaptureSave(),
            information = RumorManager.Instance.CaptureSave(),
            pendingEvent = EventManager.Instance.CaptureSave(),
            wealthTier = WealthTierManager.Instance.CaptureSave()
        };

        // 잘못된 상태를 파일로 기록하지 않도록 검사
        SaveDataValidator.Validate(data);

        return data;
    }

    public static void Restore(GameSaveData data)
    {
        RequireManagers();
        SaveDataValidator.Validate(data);

        // 여기서부터 상태를 변경
        // 콘텐츠 호환성 검사는 호출한 전환 관리자에서 먼저 수행
        GameManager.Instance.RestoreSave(data.player);

        DataManager.Instance.RestoreSave(data.market);
        ShopManager.Instance.RestoreSave(data.shop);

        AssetManager.Instance.RestoreSave(data.assets);
        LoanManager.Instance.RestoreSave(data.loans);
        JobManager.Instance.RestoreSave(data.job);

        RumorManager.Instance.RestoreSave(data.information);
        EventManager.Instance.RestoreSave(data.pendingEvent);
        WealthTierManager.Instance.RestoreSave(data.wealthTier);

        // AP 지급, 청구서 초기화 없이 플레이만 다시 허용
        GameManager.Instance.CompleteSaveRestore();
    }

    private static void RequireManagers()
    {
        if (GameManager.Instance == null ||
            DataManager.Instance == null ||
            AssetManager.Instance == null ||
            ShopManager.Instance == null ||
            LoanManager.Instance == null ||
            JobManager.Instance == null ||
            RumorManager.Instance == null ||
            EventManager.Instance == null ||
            WealthTierManager.Instance == null)
        {
            throw new InvalidOperationException("필수 게임 매니저가 누락되었습니다.");
        }

        if (!DataManager.Instance.IsShopLoaded ||
            !DataManager.Instance.IsInformationLoaded ||
            !LoanManager.Instance.IsConfigured ||
            !JobManager.Instance.IsConfigured ||
            !WealthTierManager.Instance.IsConfigured)
        {
            throw new InvalidOperationException("게임 데이터 또는 규칙 설정이 준비되지 않았습니다.");
        }
    }
}
