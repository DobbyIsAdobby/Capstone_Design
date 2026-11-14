using System;

/// <summary>
/// 전체 상태 수집과 복원 순서를 정하는 class
/// </summary>
public static class GameSaveCoordinator
{
    // v3 : 부동산이 추가됨에 따라 버전 상승
    // v4 : 행복도 저장 데이터 추가에 따라 버전 상승
    // v5 : 튜토리얼 시스템 데이터 추가에 따라 버전 상승
    // v6 : 투자 종목 데이터 추가에 따라 버전 상승
    // v7 : 투자 종목 정보, 신문, 랜덤 이벤트 데이터 추가에 따라 버전 상승
    public const int FormatVersion = 7;

    // 상품 효과나 직급 규칙 등 저장 호환성이 달라지면 올림.
    // v4 : 인플레이션 및 주거비 계산 규칙 추가.
    // v5 : 부동산이 추가됨에 따라 버전 상승.
    // v6 : 초반 스토리와 턴별 기능 해금 규칙 추가.
    // v7 : 행복도 및 전성기 규칙 추가.
    // v8 : spotlight 방식 튜토리얼 시스템 규칙 추가.
    // v9 : 투자 종목 추가에 따른 시스템 규칙 추가.
    // v10 : 투자 종목 정보, 신문, 랜덤 이벤트 데이터 추가에 따른 시스템 규칙 추가.
    public const int ContentVersion = 10;

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
            wealthTier = WealthTierManager.Instance.CaptureSave(),
            realEstate = RealEstateManager.Instance.CaptureSave(),
            happiness = HappinessManager.Instance.CaptureSave(),
            spotlightTutorial = SpotlightTutorialManager.Instance.CaptureSave(),
        };

        // 잘못된 상태를 파일로 기록하지 않도록 검사
        // 이벤트 JSON, 시장 일치 검사를 함께 수행함.
        EventManager.Instance.ValidateSavedEvents(data);

        RealEstateManager.Instance.ValidateSavedCatalog(data.realEstate, data.player.currentMonth);

        HappinessManager.Instance.ValidateSave(data.happiness, data.player.currentMonth);

        SpotlightTutorialManager.Instance.ValidateSave(data.spotlightTutorial, data.player.currentMonth);

        return data;
    }

    public static void Restore(GameSaveData data)
    {
        RequireManagers();

        SpotlightTutorialManager.Instance.ValidateSave(data.spotlightTutorial, data.player.currentMonth);

        RealEstateManager.Instance.ValidateSavedCatalog(data.realEstate, data.player.currentMonth);
        
        HappinessManager.Instance.ValidateSave(data.happiness, data.player.currentMonth);

        // 게임 상태를 바꾸기 전에 모든 이벤트 검사를 완료해야함.
        EventManager.Instance.ValidateSavedEvents(data);

        // 여기서부터 상태를 변경
        // 콘텐츠 호환성 검사는 호출한 전환 관리자에서 먼저 수행
        GameManager.Instance.RestoreSave(data.player);

        DataManager.Instance.RestoreSave(data.market);
        ShopManager.Instance.RestoreSave(data.shop);

        AssetManager.Instance.RestoreSave(data.assets);
        LoanManager.Instance.RestoreSave(data.loans);
        JobManager.Instance.RestoreSave(data.job);

        // 확정된 이벤트 일정부터 복원.
        EventManager.Instance.RestoreSave(data.pendingEvent);

        RumorManager.Instance.RestoreSave(data.information);

        // 재산 등급과 HUD 갱신 전에 부동산 자산, 부채를 복원.
        RealEstateManager.Instance.RestoreSave(data.realEstate, data.player.currentMonth);

        WealthTierManager.Instance.RestoreSave(data.wealthTier);

        HappinessManager.Instance.RestoreSave(data.happiness, data.player.currentMonth);

        SpotlightTutorialManager.Instance.RestoreSave(data.spotlightTutorial, data.player.currentMonth);

        // AP 지급, 청구서 초기화 없이 플레이만 다시 허용
        GameManager.Instance.CompleteSaveRestore();
    }

    private static void RequireManagers()
    {
        // 저장과 복원에서도 해금 규칙이 준비되어 있어야 함.
        if (TutorialManager.Instance == null || !TutorialManager.Instance.IsConfigured)
        {
            throw new InvalidOperationException("초반 스토리와 기능 해금 설정이 준비되지 않았습니다.");
        }

        if (GameManager.Instance == null ||
            DataManager.Instance == null ||
            AssetManager.Instance == null ||
            ShopManager.Instance == null ||
            LoanManager.Instance == null ||
            JobManager.Instance == null ||
            RumorManager.Instance == null ||
            EventManager.Instance == null ||
            WealthTierManager.Instance == null ||
            InflationManager.Instance == null ||
            HousingCostManager.Instance == null ||
            RealEstateManager.Instance == null ||
            HappinessManager.Instance == null ||
            SpotlightTutorialManager.Instance == null)
        {
            throw new InvalidOperationException("필수 게임 매니저가 누락되었습니다.");
        }

        if (!DataManager.Instance.IsShopLoaded ||
            !DataManager.Instance.IsInformationLoaded ||
            !LoanManager.Instance.IsConfigured ||
            !JobManager.Instance.IsConfigured ||
            !WealthTierManager.Instance.IsConfigured ||
            !EventManager.Instance.IsConfigured ||
            !InflationManager.Instance.IsConfigured ||
            !HousingCostManager.Instance.IsConfigured ||
            !RealEstateManager.Instance.IsConfigured ||
            !HappinessManager.Instance.IsConfigured ||
            !SpotlightTutorialManager.Instance.IsConfigured)
        {
            throw new InvalidOperationException("게임 데이터 또는 규칙 설정이 준비되지 않았습니다.");
        }
    }
}
