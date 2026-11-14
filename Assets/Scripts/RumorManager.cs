using System;
using System.Collections.Generic;
using UnityEngine;

/*
// 정보의 진위 여부(enum) - 더 이상 사용하지 않음. 정보 JSON 적용 완료
public enum RumorType
{
    True, //진짜 정보 (다음 턴 방향과 일치)
    Fake  //거짓 정보 (다음 턴 방향과 반대 또는 잘못된 조언)
}
*/

/// <summary>
/// 턴별 정보 구매/무료 공개/결과 보관을 담당함. 
/// 패널을 닫더라도 RumorManager의 결과는 유지됨
/// 동일한 턴에 패널을 다시 열어도, 열람된 정보는 유지되어야하기 때문.
/// </summary>
public class RumorManager : Singleton<RumorManager>
{
    /*
    Inspector Zone
    */

    [Header("Information AP Cost")]
    [SerializeField, Min(0)] private int highAPCost = 20;
    [SerializeField, Min(0)] private int midAPCost = 10;
    [SerializeField, Min(0)] private int lowAPCost = 5;

    /*
    function Zone
    */

    /* -- 더 이상 각 상품을 직접 추첨하지 않기에 사용하지 않음.
    private static readonly AssetType[] TargetAssets =
    {
        AssetType.Stock,
        AssetType.Leverage,
        AssetType.StockInverse,
        AssetType.LeverageInverse
    };
    */

    /// <summary>
    /// 해당 턴 이벤트의 영향을 받지 않는 종목만 반환.
    /// 유료 정보와 무료 정보가 같은 후보를 사용.
    /// </summary>
    /// <param name="turn"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    private List<MarketSector> GetAvailableSectors(int turn)
    {
        EventManager events = EventManager.Instance;

        if (events == null)
            throw new InvalidOperationException("EventManager가 없습니다.");

        var result = new List<MarketSector>();

        for (int i = 0; i < MarketSectorUtility.SectorCount; i++)
        {
            MarketSector sector = (MarketSector)i;

            if (!events.IsSectorAffected(turn, sector))
                result.Add(sector);
        }

        return result;
    }

    private bool HasRates(List<MarketSector> sectors, int targetTurn)
    {
        DataManager data = DataManager.Instance;

        if (data == null)
            return false;

        foreach (MarketSector sector in sectors)
        {
            if (!data.TryGetGeneratedMarketRates(sector, targetTurn, out _))
            {
                return false;
            }
        }

        return true;
    }

    // 공개된 등급만 저장
    // 이 Dictionary에 있으면 해당 턴 재구매는 불가능함
    private readonly Dictionary<InformationGrade, InformationReveal> reveals = new Dictionary<InformationGrade, InformationReveal>();

    private readonly HashSet<string> usedTexts = new HashSet<string>(StringComparer.Ordinal);

    private SaveRandom random;
    // SaveRandom.random으로 대체함
    //private System.Random random;
    private int informationTurn = -1;

    /// <summary>
    /// GameManager가 새 턴을 시작할 때 호출. 같은 턴으로 다시 호출되어도 결과를 초기화하지 않음.
    /// </summary>
    public void BeginTurn()
    {
        GameManager game = GameManager.Instance;
        DataManager data = DataManager.Instance;

        if (game == null || data == null || !data.IsMarketReady || !data.IsInformationLoaded)
        {
            return;
        }

        if (informationTurn == game.currentMonth)
            return;

        informationTurn = game.currentMonth;
        reveals.Clear();
        usedTexts.Clear();

        int seed = unchecked(data.MarketSeed ^ (informationTurn * 397) ^ 0x5317);

        random = new SaveRandom(seed);

        if (!TutorialManager.IsUnlocked(TutorialFeature.Information))
            return;

        if (game.IsGameOver || informationTurn < 1 || informationTurn > game.maxMonth)
        {
            return;
        }

        List<MarketSector> candidates = GetAvailableSectors(informationTurn);

        // 전체 시장 이벤트라면 무료 정보도 생성하지 않아야함
        if (candidates.Count == 0)
            return;

        if (!data.TryGetInformation(InformationGrade.Low, out InformationItemData low))
        {
            return;
        }

        if (ShopManager.Instance == null || !ShopManager.Instance.HasFreeLowInformation(low.FreeWithItem, informationTurn))
        {
            return;
        }

        if (!HasRates(candidates, informationTurn + low.LookaheadTurns))
            return;

        // 현금과 AP를 사용하지 않는 무료 공개 경로
        InformationReveal reveal = CreateReveal(low, true);
        StoreReveal(InformationGrade.Low, reveal);
    }

    public bool TryGetReveal(InformationGrade grade, out InformationReveal reveal)
    {
        reveal = null;

        // 이전 턴의 결과가 잘못 표시되지 않도록 검사
        return GameManager.Instance != null && informationTurn == GameManager.Instance.currentMonth && reveals.TryGetValue(grade, out reveal);
    }

    /// <summary>
    /// 정보 가격에는 일반 물가가 아닌 정보 전용 규칙을 적용.
    /// 현재 설정에서는 36턴부터 기본 가격의 2배.
    /// </summary>
    /// <param name="item"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="InvalidOperationException"></exception>
    public long GetPurchasePrice(InformationItemData item)
    {
        if (item == null)
            throw new ArgumentNullException(nameof(item));

        GameManager game = GameManager.Instance;

        if (game == null)
            throw new InvalidOperationException("정보 가격을 계산할 GameManager가 없습니다.");

        return InflationManager.Instance.CalculateInformationPrice(item.Price, game.currentMonth);
    }

    /// <summary>
    /// UI 표시와 실제 결제 직전 검사에 함께 사용. 검사만 하며 현금이나 정보 상태를 변경하지 않음.
    /// </summary>
    /// <param name="grade"></param>
    /// <param name="reason"></param>
    /// <returns></returns>
    public bool CanPurchase(InformationGrade grade, out string reason)
    {
        reason = "";

        if (!TutorialManager.Check(TutorialFeature.Information, out reason))
        {
            return false;
        }

        GameManager game = GameManager.Instance;
        DataManager data = DataManager.Instance;

        if (game == null || !game.CanAct)
        {
            reason = "지금은 정보를 구매할 수 없습니다.";
            return false;
        }

        // 신문을 닫은 이후에도 해당 턴에는 구매 불가.
        // 등급과 무관하게 동일하게 적용.
        if (EventManager.Instance != null && EventManager.Instance.IsInformationBlocked(game.currentMonth))
        {
            reason = "이번 달에는 제공 가능한 정보가 없습니다.";
            return false;
        }

        if (data == null || !data.TryGetInformation(grade, out InformationItemData item))
        {
            reason = "정보 데이터를 불러오지 못했습니다.";
            return false;
        }

        if (game.currentMonth < 1 || game.currentMonth > game.maxMonth)
        {
            reason = "정보를 구매할 수 있는 턴이 아닙니다.";
            return false;
        }

        if (informationTurn != game.currentMonth)
        {
            reason = "이번 턴 정보가 준비되지 않았습니다.";
            return false;
        }

        if (reveals.ContainsKey(grade))
        {
            reason = "이번 턴에 이미 공개한 정보입니다.";
            return false;
        }

        List<MarketSector> candidates = GetAvailableSectors(game.currentMonth);

        if (candidates.Count == 0)
        {
            reason = "이번 달에는 제공 가능한 정보가 없습니다.";
            return false;
        }

        if (!HasRates(candidates, game.currentMonth + item.LookaheadTurns))
        {
            reason = "현재 턴 시장 데이터가 없습니다.";
            return false;
        }

        long price = GetPurchasePrice(item);

        if (game.availableCash < price)
        {
            reason = "보유 현금이 부족합니다.";
            return false;
        }

        if (!game.CanSpendAP(GetAPCost(grade), out reason))
        {
            reason = "보유 AP가 부족합니다.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// 확인창의 '예'에서 호출. 확인창을 연 이후 턴이나 현금이 바뀌었는지도 다시 검사.
    /// </summary>
    /// <param name="grade"></param>
    /// <param name="requestedTurn"></param>
    /// <param name="message"></param>
    /// <returns></returns>
    public bool TryPurchase(InformationGrade grade, int requestedTurn, out string message)
    {
        GameManager game = GameManager.Instance;

        // Spotlight 튜토리얼 시스템은 거래 방법 설명까지만 진행.
        if (SpotlightTutorialManager.Instance != null && SpotlightTutorialManager.Instance.IsBusy)
        {
            message = "안내를 마친 뒤 이용해주세요.";
            return false;
        }

        if (game == null || requestedTurn != game.currentMonth)
        {
            message = "턴이 변경되었습니다. 정보를 다시 선택하세요.";
            return false;
        }

        if (!CanPurchase(grade, out message))
            return false;

        DataManager.Instance.TryGetInformation(grade, out InformationItemData item);

        // 결제 직전에 현재 턴 가격을 다시 계산.
        long price = GetPurchasePrice(item);

        // 결과 생성 또는 AP 결제가 실패하면 추첨 상태를 되돌림
        long randomStateBefore = random.State;

        InformationReveal reveal;

        // 결과 생성에 실패하면 돈을 차감하지 않음
        try
        {
            reveal = CreateReveal(item, false);
        }
        catch (Exception exception)
        {
            random.Restore(randomStateBefore);

            Debug.LogError(exception, this);
            message = "정보 생성에 실패했습니다. 현금은 차감되지 않았습니다.";
            return false;
        }

        if (!game.TrySpendAP(GetAPCost(grade), out message))
        {
            random.Restore(randomStateBefore);
            return false;
        }

        // 먼저 공개 상태를 기록하여 중복 호출 시 재구매를 차단
        StoreReveal(grade, reveal);

        // 현금 차감과 청구서 기록을 기존 경로로 함께 처리
        game.ApplyCashChange(-price, "정보 구매");

        if (UIManager.Instance != null)
            UIManager.Instance.RefreshUI();

        message = $"{item.Name}가 공개되었습니다.";

        SpotlightTutorialManager.Instance?.Notify("InformationPurchased");
        
        return true;
    }

    private InformationReveal CreateReveal(InformationItemData item, bool isFree)
    {
        int targetTurn = informationTurn + item.LookaheadTurns;

        List<MarketSector> candidates = GetAvailableSectors(targetTurn);

        if (candidates.Count == 0)
        {
            throw new InvalidOperationException("이번 달에는 제공 가능한 정보가 없습니다.");
        }

        // 남은 종목 중 균등하게 선택
        MarketSector sector = candidates[random.Next(candidates.Count)];

        // 일반형과 인버스형을 각각 50%로 선택
        InformationExposure exposure = (InformationExposure)random.Next(2);

        if (!DataManager.Instance.TryGetGeneratedMarketRates(sector, targetTurn, out MonthlyMarketRates rates))
        {
            throw new InvalidOperationException("정보 대상 시장 데이터가 없습니다.");
        }

        // 같은 유형 안에서는 배율만 다르고 방향이 같으므로 일반형은 Stock, 인버스형은 StockInverse를 대표값으로 사용.
        decimal actualRate = exposure == InformationExposure.Normal ? rates.Stock : rates.StockInverse;

        int actualDirection = actualRate > 0m ? 1 : actualRate < 0m ? -1 : 0;

        int presentedDirection = actualDirection;

        // JSON 등급별 정확도
        if (actualDirection != 0 && random.NextDouble() >= item.Accuracy)
        {
            presentedDirection = -actualDirection;
        }

        InformationDirection direction = (InformationDirection)presentedDirection;

        string title = InformationTextProvider.GetTitle(sector, exposure);

        string body = InformationTextProvider.Create(sector, exposure, direction, random, usedTexts);

        return new InformationReveal(targetTurn, sector, exposure, direction, title, body, isFree);
    }

    private void StoreReveal(InformationGrade grade, InformationReveal reveal)
    {
        // 이제 이곳에는 이번 턴에 공개한 결과만 보관함.
        reveals.Add(grade, reveal);
        
        // 결제 또는 무료 공개가 성공한 뒤에만 사용 처리.
        usedTexts.Add(reveal.Body);
    }

    /// <summary>
    /// 유료 정보 구매 시 필요한 AP
    /// 휴대폰 구매 시, 하급 정보는 현재 구매 경로를 거치지 않고 다이렉트로 해금됨.
    /// </summary>
    /// <param name="grade"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public int GetAPCost(InformationGrade grade)
    {
        switch (grade)
        {
            case InformationGrade.High:
                return highAPCost;

            case InformationGrade.Mid:
                return midAPCost;

            case InformationGrade.Low:
                return lowAPCost;

            default:
                throw new ArgumentOutOfRangeException(nameof(grade));
        }
    }

    public InformationSaveData CaptureSave()
    {
        if (random == null)
            throw new InvalidOperationException("정보 난수가 초기화되지 않았습니다.");

        var data = new InformationSaveData
        {
            informationTurn = informationTurn,
            randomState = random.State
        };

        foreach (var pair in reveals)
        {
            InformationReveal reveal = pair.Value;

            data.reveals.Add(new InformationRevealSaveData
            {
                grade = pair.Key,
                targetTurn = reveal.TargetTurn,
                sector = reveal.Sector,
                exposure = reveal.Exposure,
                direction = reveal.Direction,
                title = reveal.Title,
                body = reveal.Body,
                isFree = reveal.IsFree
            });
        }

        data.usedTexts.AddRange(usedTexts);

        return data;
    }

    public void RestoreSave(InformationSaveData data)
    {
        var restored = new Dictionary<InformationGrade, InformationReveal>();

        foreach (InformationRevealSaveData saved in data.reveals)
        {
            restored.Add(
                saved.grade,
                new InformationReveal(
                    saved.targetTurn,
                    saved.sector,
                    saved.exposure,
                    saved.direction,
                    saved.title,
                    saved.body,
                    saved.isFree));
        }

        var restoredRandom = new SaveRandom(1);
        restoredRandom.Restore(data.randomState);

        informationTurn = data.informationTurn;
        random = restoredRandom;

        reveals.Clear();

        foreach (var pair in restored)
            reveals.Add(pair.Key, pair.Value);

        usedTexts.Clear();

        foreach (string text in data.usedTexts)
            usedTexts.Add(text);
    }
}

    /* -- 프로토타이핑 용으로 테스트에 사용했던 코드 -- 더 이상 사용하지 않음.
    
    Inspector Zone
    
    //public static RumorManager Instance;

    [Header("Rumor Settings")]
    public int rumorCost = 100000; // 정보 1회 열람 비용 (10만 원)

    //가챠 확률 (ex : True-40% / Fake-60%)
    [Range(0f,1f)]
    public float trueRumorProbability = 0.4f;

    
    function Zone
    

    //싱글톤 패턴
    private void Awake()
    {
        if(Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    /// <summary>
    /// UI [정보 대화방] 버튼을 통해 호출되는 함수
    /// </summary>
    public void OnClickBuyRumorGacha()
    {
        //1. 현금 확인
        if(GameManager.Instance.availableCash < rumorCost)
        {
            Debug.Log("가용 현금이 부족하여 정보를 구매할 수 없습니다.");
            return;
        }

        //2. 비용 지불
        GameManager.Instance.ApplyCashChange(-rumorCost, "정보 구매");

        //3. 확률에 따른 진위 여부 판별
        RumorType resultType = (Random.value <= trueRumorProbability) ? RumorType.True : RumorType.Fake;

        //4. 정보 텍스트 생성 및 출력
        string rumorMessage = GenerateRumorText(resultType);

        Debug.Log($"정보 도착. 비용 : -{rumorCost}원 / 진위 : {resultType}");
        Debug.Log($"메시지 내용 : {rumorMessage}");

        if(UIManager.Instance != null)
        {
            UIManager.Instance.RefreshUI();
        }

        //추후 UIManager를 통해 인게임 채팅방 UI에 텍스트 띄우기
        //UIManager.Instance.ShowRumorUI(rumorMessage);
    }

    /// <summary>
    /// 진위 여부에 따라 출력할 텍스트를 생성하는 함수 (프로토타입용)
    /// </summary>
    private string GenerateRumorText(RumorType type)
    {
        //프로토타입 단계이므로 임시 텍스트 배열 사용(현재 부분은 생성형AI를 통해 텍스트를 제작했습니다.)
        //추후 DB 담당 팀원 분께서 제작한 CSV 데이터를 파싱해서 불러오도록 변경 예정

        string[] trueRumors = {
            "여의도 기관들 지금 다 숏(하락) 치고 있다 조심해라.",
            "다음 달 금리 인상 확정이란 썰 돌더라. 현금 관망 추천.",
            "[단독] 기술주 섹터 대규모 실적 쇼크 예상... 폭락 주의"
        };

        string[] fakeRumors = {
            "나스닥 3배 레버리지 지금이 바닥이다. 내일 무조건 쏜다.",
            "아는 형이 펀드매니저인데 이번 하락장 페이크래. 풀매수 가라.",
            "다음 달 증시 V자 반등 무조건 나옴. 빚내서라도 타라."
        };

        if(type == RumorType.True)
        {
            return trueRumors[Random.Range(0, trueRumors.Length)];
        }
        else
        {
            return fakeRumors[Random.Range(0, fakeRumors.Length)];
        }
    }
    */
