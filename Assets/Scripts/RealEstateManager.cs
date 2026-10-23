using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography; // SHA256 입력데이터의 해시를 계산하기 위한 namespace
using System.Text;
using UnityEngine;

public class RealEstateManager : Singleton<RealEstateManager>
{
    [SerializeField] private TextAsset catalogJson;
    [SerializeField] private RealEstateRules rules;

    private RealEstateCatalog catalog;
    private RealEstateSaveData state;
    private string catalogHash;

    private readonly List<string> auctionMessages = new List<string>();

    public bool IsConfigured { get; private set; }
    public bool IsInitialized => state != null;

    public int Count => catalog.properties.Length;
    public LoanRules LoanRules => rules.LoanRules;

    public IReadOnlyList<string> AuctionMessages => auctionMessages;

    public RealEstateDefinition Definition(int index)
    {
        return catalog.properties[index];
    }

    public RealEstatePropertyState Property(int index)
    {
        return state.properties[index];
    }

    /// <summary>
    /// 턴 진행 중에는 직전 마감 가격으로 거래.
    /// 1턴에는 prices[0]을 사용.
    /// </summary>
    /// <param name="index"></param>
    /// <returns></returns>
    public long Price(int index)
    {
        return Property(index).prices[state.lastMarketSettlementTurn];
    }

    public long Rent(int index, int turn)
    {
        // 월세와 같은 +25% 누적 규칙을 사용
        return InflationManager.Instance.CalculateRent(Definition(index).baseRent, turn);
    }

    public long GrossValue => !IsInitialized ? 0 : Enumerable.Range(0, Count).Where(i => Property(i).owned).Sum(i => Price(i));

    public long OutstandingPrincipal => !IsInitialized ? 0 : state.properties.Where(p => p.owned).Sum(p => p.principal);

    public int MaxDuration => Math.Max(0, Math.Min(LoanRules.MaxDuration, 60 - GameManager.Instance.currentMonth));

    public long LoanLimit(int index)
    {
        // 매입가의 50% 안에서 기존 대출 입력 단위에 맞춤
        long step = LoanRules.AmountStep;
        return (Price(index) / 2 / step) * step;
    }

    protected override void OnSingletonAwake()
    {
        IsConfigured = false;

        if (rules == null)
            throw new InvalidOperationException("RealEstateRules를 연결하세요.");

        if (!rules.Validate(out string error))
            throw new InvalidOperationException(error);

        if (catalogJson == null)
            throw new InvalidOperationException("real_estate.json을 연결하세요.");

        catalog = JsonUtility.FromJson<RealEstateCatalog>(catalogJson.text);

        if (catalog == null || catalog.properties == null || catalog.properties.Length == 0)
        {
            throw new FormatException("부동산 상품 목록이 없습니다.");
        }

        var ids = new HashSet<string>();

        foreach (RealEstateDefinition item in catalog.properties)
        {
            if (item == null ||
                string.IsNullOrWhiteSpace(item.id) ||
                !ids.Add(item.id) ||
                string.IsNullOrWhiteSpace(item.name) ||
                item.basePrice <= 0 ||
                item.baseRent < 0)
            {
                throw new FormatException("부동산 상품 데이터를 확인하세요.");
            }
        }

        ValidateArticles(catalog.positive_news);
        ValidateArticles(catalog.negative_news);
        ValidateArticles(catalog.neutral_news);

        // SHA 256 데이터 해시 변환 계산.

        using (SHA256 hash = SHA256.Create())
        {
            catalogHash = Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(catalogJson.text)));
        }

        IsConfigured = true;
    }

    private static void ValidateArticles(NewsArticleData[] articles)
    {
        if (articles == null || articles.Length == 0)
            throw new FormatException("부동산 기사 목록이 없습니다.");

        foreach (NewsArticleData article in articles)
        {
            if (article == null ||
                string.IsNullOrWhiteSpace(article.id) ||
                string.IsNullOrWhiteSpace(article.title) ||
                string.IsNullOrWhiteSpace(article.text))
            {
                throw new FormatException("부동산 기사 내용을 확인하세요.");
            }
        }
    }

    /// <summary>
    /// 난수 상태까지 저장해 불러오기 후 재추첨 방지.
    /// uint = unsigned Integer
    /// </summary>
    /// <returns></returns>
    private uint NextRandom()
    {
        // 초기화된 value 값을 base로 Xorshift 방식의 난수 생성 알고리즘. - PRNG(의사난수발생) 알고리즘
        // 비트 이동(shift)과 XOR 연산만 사용하기 때문에 속도가 극도로 빠르다는 장점이 있음.
        // 보안은 중요하지 않고 매우 빠른 난수 생성이 반복적으로 필요하기 때문에 해당 알고리즘을 사용함.

        uint value = (uint)state.randomState;

        // lv1 : 원래 값과 왼쪽으로 13칸 밀어낸 값을 XOR - 하위 비트들의 정보를 상위비트로 확산
        value ^= value << 13;
        // lv2 : 1단계 결과물과 오른쪽으로 17칸 밀어낸 값을 XOR - 상위 비트들의 정보를 다시 하위 비트들로 확산
        value ^= value >> 17;
        // lv3 : 2단계 결과물과 왼쪽으로 5칸 밀어낸 값을 XOR - 마지막으로 비트들을 섞어 난수성을 극대화.
        value ^= value << 5;

        state.randomState = value;
        return value;
    }

    /// <summary>
    /// 새 게임 시작시 자동 설정
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    public void InitializeNewGame()
    {
        if (!IsConfigured)
            throw new InvalidOperationException("부동산 설정이 준비되지 않았습니다.");

        uint seed = unchecked((uint)Guid.NewGuid().GetHashCode());
        if (seed == 0)
            seed = 1;

        state = new RealEstateSaveData
        {
            catalogHash = catalogHash,
            randomState = seed,
            lastMarketSettlementTurn = 0,
            lastLoanSettlementTurn = 0,
            newsTargets = new int[60],
            newsVariants = new int[60]
        };

        auctionMessages.Clear();

        foreach (RealEstateDefinition item in catalog.properties)
        {
            var property = new RealEstatePropertyState
            {
                id = item.id,
                prices = new long[61]
            };

            property.prices[0] = item.basePrice;

            long minimum = item.basePrice / 2;
            long maximum = checked(item.basePrice + item.basePrice / 2);

            for (int turn = 1; turn <= 60; turn++)
            {
                decimal sample = NextRandom() / 4294967296m;

                decimal rate =
                    rules.MinimumReturn +
                    sample * (rules.MaximumReturn - rules.MinimumReturn);

                long price = checked((long)decimal.Round(
                    property.prices[turn - 1] * (1m + rate),
                    0,
                    MidpointRounding.AwayFromZero));

                property.prices[turn] =
                    Math.Max(minimum, Math.Min(maximum, price));
            }

            state.properties.Add(property);
        }

        for (int turn = 0; turn < 60; turn++)
        {
            state.newsTargets[turn] = (int)(NextRandom() % (uint)Count);
            state.newsVariants[turn] = (int)(NextRandom() & 0x7fffffff);
        }
    }

    /// <summary>
    /// LoanContract의 이자 계산 재사용.
    /// 은행 LoanManager의 계약 목록에는 추가하지 않음. - 별도의 부동산 전용 대출임.
    /// </summary>
    /// <param name="index"></param>
    /// <returns></returns>
    public LoanContract GetLoan(int index)
    {
        RealEstatePropertyState p = Property(index);

        if (!p.owned || p.principal <= 0)
            return null;

        var contract = new LoanContract(
            index + 1,
            p.principal,
            p.purchaseTurn,
            p.duration,
            SaveNumber.Read(p.annualRate));

        contract.MarkInterestPaid(p.lastInterestPaidTurn);
        return contract;
    }

    /// <summary>
    /// 행동할 수 없는 예외처리 반환
    /// </summary>
    /// <param name="index"></param>
    /// <param name="requestedTurn"></param>
    /// <returns></returns>
    private string ActionError(int index, int requestedTurn)
    {
        // 부동산 관련 사용자 행동을 공통으로 검사.
        if (!TutorialManager.Check(TutorialFeature.RealEstate, out string lockReason))
        {
            return lockReason;
        }
        
        GameManager game = GameManager.Instance;

        if (!IsInitialized || index < 0 || index >= Count)
            return "부동산 정보가 준비되지 않았습니다.";

        if (game == null || !game.CanAct || game.currentMonth != requestedTurn)
        {
            return "현재는 거래할 수 없습니다.";
        }

        return null;
    }

    /// <summary>
    /// 구매시 발생할 예외처리 반환
    /// </summary>
    /// <param name="index"></param>
    /// <param name="loanAmount"></param>
    /// <param name="duration"></param>
    /// <param name="requestedTurn"></param>
    /// <returns></returns>
    public string BuyError(int index, long loanAmount, int duration, int requestedTurn)
    {
        string error = ActionError(index, requestedTurn);
        if (error != null)
            return error;

        if (Property(index).owned)
            return "이미 보유한 부동산입니다.";

        if (loanAmount < 0)
            return "대출 금액이 올바르지 않습니다.";

        if (loanAmount > 0)
        {
            if (duration < 1 || duration > MaxDuration)
                return "선택할 수 없는 대출 기간입니다.";

            if (loanAmount < LoanRules.MinimumLoanAmount || loanAmount % LoanRules.AmountStep != 0 || loanAmount > LoanLimit(index))
            {
                return "대출 최소 금액, 입력 단위 또는 50% 한도를 확인하세요.";
            }
        }

        long requiredCash = Price(index) - loanAmount;

        if (GameManager.Instance.availableCash < requiredCash)
            return $"현금이 {requiredCash - GameManager.Instance.availableCash:N0}원 부족합니다.";

        return null;
    }

    /// <summary>
    /// 부동산 구매 처리 함수
    /// </summary>
    /// <param name="index"></param>
    /// <param name="loanAmount"></param>
    /// <param name="duration"></param>
    /// <param name="requestedTurn"></param>
    /// <returns></returns>
    public string Buy(int index, long loanAmount, int duration, int requestedTurn)
    {
        string error = BuyError(index, loanAmount, duration, requestedTurn);
        if (error != null)
            return error;

        GameManager game = GameManager.Instance;
        RealEstatePropertyState p = Property(index);
        long price = Price(index);

        // 모든 검사가 끝난 뒤 소유권과 대출을 함께 생성.
        p.owned = true;
        p.purchaseTurn = requestedTurn;
        p.purchasePrice = price;
        p.principal = loanAmount;
        p.duration = loanAmount > 0 ? duration : 0;
        p.annualRate = SaveNumber.Write(LoanRules.AnnualRate);
        p.lastInterestPaidTurn = requestedTurn - 1;

        // 매입은 현금이 자산으로 바뀌는 거래.
        // 순자산 손실로 집계하지 않도록 Financing으로 기록.
        game.ApplyCashChange(-(price - loanAmount), $"{Definition(index).name} 매입 자기자금", ReceiptLineType.Financing);

        RefreshHUD();
        return null;
    }

    /// <summary>
    /// 대출 이자 및 원금 상환 함수
    /// </summary>
    /// <param name="index"></param>
    /// <param name="requestedTurn"></param>
    /// <returns></returns>
    public string Repay(int index, int requestedTurn)
    {
        string error = ActionError(index, requestedTurn);
        if (error != null)
            return error;

        LoanContract loan = GetLoan(index);
        if (loan == null)
            return "상환할 부동산 대출이 없습니다.";

        long interest = loan.GetInterestDue(requestedTurn);
        long total = checked(loan.Principal + interest);

        GameManager game = GameManager.Instance;

        // 자발적인 상환 시도에서 현금이 부족하면 거래만 거부.
        // 이 경우 경매 처리하지 않음.
        if (game.availableCash < total)
            return $"전액 상환에 {total:N0}원이 필요합니다.";

        RealEstatePropertyState p = Property(index);

        game.ApplyCashChange(-loan.Principal, $"{Definition(index).name} 대출 원금", ReceiptLineType.Financing);

        if (interest > 0)
        {
            game.ApplyCashChange(-interest, $"{Definition(index).name} 대출 이자", ReceiptLineType.FixedExpense);
        }

        p.principal = 0;
        p.lastInterestPaidTurn = requestedTurn;

        RefreshHUD();
        return null;
    }

    /// <summary>
    /// 매각 진행 함수
    /// </summary>
    /// <param name="index"></param>
    /// <param name="requestedTurn"></param>
    /// <returns></returns>
    public string RequestSale(int index, int requestedTurn)
    {
        string error = ActionError(index, requestedTurn);
        if (error != null)
            return error;

        RealEstatePropertyState p = Property(index);

        if (!p.owned)
            return "보유한 부동산이 아닙니다.";

        if (p.principal > 0)
            return "해당 부동산 대출을 전액 상환한 뒤 매각할 수 있습니다.";

        if (p.saleRequested)
            return "이미 매각 대기 중입니다.";

        if (requestedTurn >= 60)
            return "마지막 턴에는 새 매각을 신청할 수 없습니다.";

        // 첫 신청 때만 추첨.
        // 취소 후 재신청에서는 남은 기간을 그대로 사용.
        if (p.remainingSaleTurns == 0)
            p.remainingSaleTurns = 1 + (int)(NextRandom() % 3);

        p.saleRequested = true;
        p.saleRequestedTurn = requestedTurn;

        RefreshHUD();
        return null;
    }

    /// <summary>
    /// 매각 취소 함수
    /// </summary>
    /// <param name="index"></param>
    /// <param name="requestedTurn"></param>
    /// <returns></returns>
    public string CancelSale(int index, int requestedTurn)
    {
        string error = ActionError(index, requestedTurn);
        if (error != null)
            return error;

        RealEstatePropertyState p = Property(index);

        if (!p.owned || !p.saleRequested)
            return "취소할 매각 신청이 없습니다.";

        // 대기 상태만 해제.
        // 남은 기간은 보존하여 재추첨을 막음.
        p.saleRequested = false;

        RefreshHUD();
        return null;
    }

    /// <summary>
    /// 월말 시세 및 임대료 지급 함수
    /// </summary>
    /// <param name="turn"></param>
    /// <exception cref="InvalidOperationException"></exception>
    public void SettleMarketAndIncome(int turn)
    {
        GameManager game = GameManager.Instance;

        if (!IsInitialized || !game.IsSetting || game.currentMonth != turn)
            throw new InvalidOperationException("부동산 정산 시점이 올바르지 않습니다.");

        // 중복 호출 방지.
        if (state.lastMarketSettlementTurn == turn)
            return;

        if (state.lastMarketSettlementTurn != turn - 1)
            throw new InvalidOperationException("부동산 시세 정산 순서가 맞지 않습니다.");

        auctionMessages.Clear();

        for (int i = 0; i < Count; i++)
        {
            RealEstatePropertyState p = Property(i);

            if (!p.owned)
                continue;

            // 현금은 변경하지 않고 평가손익만 청구서에 기록.
            long change = p.prices[turn] - p.prices[turn - 1];

            game.RecordMonthlyChange($"{Definition(i).name} 평가손익", change);
        }

        // 이후 전체 상품의 현재 가격이 이번 턴 마감 가격으로 변경됨.
        state.lastMarketSettlementTurn = turn;

        for (int i = 0; i < Count; i++)
        {
            RealEstatePropertyState p = Property(i);

            if (!p.owned)
                continue;

            // 구매한 달부터 지급.
            // 매각되는 달에도 먼저 임대료를 지급.
            game.ApplyCashChange(Rent(i, turn), $"{Definition(i).name} 임대료");

            // 신청한 당월에는 대기 기간을 줄이지 않음.
            // n턴 신청 → 가장 빨라도 n+1턴 마감에 매각됨.
            if (!p.saleRequested || turn <= p.saleRequestedTurn)
                continue;

            p.remainingSaleTurns--;

            if (p.remainingSaleTurns > 0)
                continue;

            if (p.principal > 0)
                throw new InvalidOperationException("대출이 남은 부동산은 매각할 수 없습니다.");

            long salePrice = Price(i);

            // 매각 역시 자산을 현금으로 바꾸는 거래.
            game.ApplyCashChange(salePrice, $"{Definition(i).name} 매각대금", ReceiptLineType.Financing);

            RemoveOwnership(i);
        }
    }

    /// <summary>
    /// 대출 및 경매 처리 함수
    /// </summary>
    /// <param name="turn"></param>
    /// <exception cref="InvalidOperationException"></exception>
    public void SettleLoans(int turn)
    {
        GameManager game = GameManager.Instance;

        if (!IsInitialized || !game.IsSetting || state.lastMarketSettlementTurn != turn)
        {
            throw new InvalidOperationException("부동산 대출 정산 순서가 맞지 않습니다.");
        }

        if (state.lastLoanSettlementTurn == turn)
            return;

        if (state.lastLoanSettlementTurn != turn - 1)
            throw new InvalidOperationException("부동산 대출 정산 턴이 맞지 않습니다.");

        // 여러 계약이 있을 때는 만기가 빠른 계약부터 처리.
        // 같은 만기라면 구매 턴, 상품 순서로 고정. - Linq.Enumerable 사용
        int[] indices = Enumerable.Range(0, Count)
            .Where(i => Property(i).owned && Property(i).principal > 0)
            .OrderBy(i => GetLoan(i).MaturityTurn)
            .ThenBy(i => Property(i).purchaseTurn)
            .ThenBy(i => i)
            .ToArray();

        foreach (int i in indices)
        {
            RealEstatePropertyState p = Property(i);
            LoanContract loan = GetLoan(i);

            long interest = loan.GetInterestDue(turn);
            long principalDue = turn >= loan.MaturityTurn ? loan.Principal : 0;
            long total = checked(interest + principalDue);

            if (total == 0)
                continue;

            if (game.availableCash >= total)
            {
                if (interest > 0)
                {
                    game.ApplyCashChange(-interest, $"{Definition(i).name} 대출 이자", ReceiptLineType.FixedExpense);
                }

                if (principalDue > 0)
                {
                    game.ApplyCashChange(-principalDue, $"{Definition(i).name} 대출 원금", ReceiptLineType.Financing);

                    p.principal = 0;
                }

                p.lastInterestPaidTurn = turn;
                continue;
            }

            // 납부 불가: 파산되는게 아닌 납부하지 못한 부동산이 경매로 넘어가는 것.
            // 부동산과 연결 채무를 함께 제거.
            long assetValue = Price(i);
            long removedDebt = p.principal;

            // 순자산 변화 = 사라진 부채 - 사라진 부동산 가치.
            // 매입 때 낸 현금을 다시 차감하지 않음.
            game.RecordMonthlyChange($"{Definition(i).name} 경매 처분", checked(removedDebt - assetValue));

            auctionMessages.Add($"{Definition(i).name}의 이자/대출금을 상환하지 못해 " + "부동산이 경매에 넘어갔습니다.\n" + "해당 부동산과 연결 대출 계약이 종료되었습니다.");

            RemoveOwnership(i);
        }

        state.lastLoanSettlementTurn = turn;
    }

    /// <summary>
    /// 대출 이자 및 원금
    /// </summary>
    /// <param name="interest"></param>
    /// <param name="principal"></param>
    public void GetCurrentMonthExpenses(out long interest, out long principal)
    {
        interest = 0;
        principal = 0;

        if (!IsInitialized)
            return;

        int turn = GameManager.Instance.currentMonth;

        for (int i = 0; i < Count; i++)
        {
            LoanContract loan = GetLoan(i);
            if (loan == null)
                continue;

            interest = checked(interest + loan.GetInterestDue(turn));

            if (turn >= loan.MaturityTurn)
                principal = checked(principal + loan.Principal);
        }
    }

    public void ClearAuctionMessages()
    {
        auctionMessages.Clear();
    }

    /// <summary>
    /// 부동산 신문 생성
    /// </summary>
    /// <param name="turn"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public NewsArticleData CreateMonthlyNews(int turn)
    {
        if (!IsInitialized || turn < 1 || turn > 60)
            throw new InvalidOperationException("부동산 기사 생성 턴이 올바르지 않습니다.");

        int index = state.newsTargets[turn - 1];

        RealEstateDefinition item = Definition(index);
        RealEstatePropertyState p = Property(index);

        long change = p.prices[turn] - p.prices[turn - 1];

        NewsArticleData[] pool = change > 0 ? catalog.positive_news : change < 0 ? catalog.negative_news : catalog.neutral_news;

        int articleIndex = state.newsVariants[turn - 1] % pool.Length;
        NewsArticleData source = pool[articleIndex];

        string ReplaceTokens(string text)
        {
            return text.Replace("{name}", item.name).Replace("{price}", p.prices[turn].ToString("N0"));
        }

        // JSON 기사 객체를 수정하지 않고 표시용 복사본을 만듦.
        return new NewsArticleData
        {
            id = source.id, title = ReplaceTokens(source.title), text = ReplaceTokens(source.text)
        };
    }

    private void RefreshHUD()
    {
        UIManager.Instance?.RefreshUI();
    }

    /// <summary>
    /// 소유권만 제거함.
    /// </summary>
    /// <param name="index"></param>
    private void RemoveOwnership(int index)
    {
        RealEstatePropertyState old = Property(index);

        state.properties[index] = new RealEstatePropertyState
        {
            id = old.id,
            prices = old.prices
        };
    }

    // 아래부터는 저장 및 복원
    // 이를 검증하는 메서드임.

    public RealEstateSaveData CaptureSave()
    {
        if (!IsInitialized)
            throw new InvalidOperationException("부동산 시장이 준비되지 않았습니다.");

        return JsonUtility.FromJson<RealEstateSaveData>(JsonUtility.ToJson(state));
    }

    public void ValidateSavedCatalog(RealEstateSaveData saved, int turn)
    {
        ValidateSave(saved, turn, out _, out _);

        if (saved.catalogHash != catalogHash || saved.properties.Count != Count)
        {
            throw new FormatException("현재 부동산 데이터와 저장 파일이 다릅니다.");
        }

        for (int i = 0; i < Count; i++)
        {
            if (saved.properties[i].id != Definition(i).id || saved.properties[i].prices[0] != Definition(i).basePrice)
            {
                throw new FormatException("부동산 상품 순서 또는 기본 가격이 다릅니다.");
            }
        }
    }

    public void RestoreSave(RealEstateSaveData saved, int turn)
    {
        ValidateSavedCatalog(saved, turn);

        state = JsonUtility.FromJson<RealEstateSaveData>(JsonUtility.ToJson(saved));

        auctionMessages.Clear();
    }

    /// <summary>
    /// 로비에서도 사용할 수 있도록 Singleton에 의존하지 않는 검사
    /// </summary>
    /// <param name="saved"></param>
    /// <param name="turn"></param>
    /// <param name="gross"></param>
    /// <param name="debt"></param>
    /// <exception cref="FormatException"></exception>
    public static void ValidateSave(RealEstateSaveData saved, int turn, out long gross, out long debt)
    {
        gross = 0;
        debt = 0;

        void Require(bool condition, string message)
        {
            if (!condition)
                throw new FormatException(message);
        }

        Require(saved != null, "부동산 저장 데이터가 없습니다.");
        Require(turn >= 1 && turn <= 60, "부동산 저장 턴이 올바르지 않습니다.");

        Require(
            !string.IsNullOrWhiteSpace(saved.catalogHash) &&
            saved.randomState > 0 &&
            saved.randomState <= uint.MaxValue &&
            saved.lastMarketSettlementTurn == turn - 1 &&
            saved.lastLoanSettlementTurn == turn - 1 &&
            saved.properties != null &&
            saved.properties.Count > 0,
            "부동산 정산 또는 난수 상태가 올바르지 않습니다.");

        Require(
            saved.newsTargets != null && saved.newsTargets.Length == 60 &&
            saved.newsVariants != null && saved.newsVariants.Length == 60,
            "부동산 신문 기록이 없습니다.");

        for (int i = 0; i < 60; i++)
        {
            Require(
                saved.newsTargets[i] >= 0 &&
                saved.newsTargets[i] < saved.properties.Count &&
                saved.newsVariants[i] >= 0,
                "부동산 기사 선택값이 올바르지 않습니다.");
        }

        var ids = new HashSet<string>();

        foreach (RealEstatePropertyState p in saved.properties)
        {
            Require(
                p != null &&
                !string.IsNullOrWhiteSpace(p.id) &&
                ids.Add(p.id) &&
                p.prices != null &&
                p.prices.Length == 61 &&
                p.prices[0] > 0,
                "부동산 시세 기록이 올바르지 않습니다.");

            long minimum = p.prices[0] / 2;
            long maximum = checked(p.prices[0] + p.prices[0] / 2);

            foreach (long price in p.prices)
            {
                Require(
                    price >= minimum && price <= maximum,
                    "부동산 시세가 가격 제한을 벗어났습니다.");
            }

            if (!p.owned)
            {
                Require(
                    p.principal == 0 &&
                    p.purchaseTurn == 0 &&
                    p.purchasePrice == 0 &&
                    !p.saleRequested &&
                    p.remainingSaleTurns == 0,
                    "미보유 부동산에 계약이 남아 있습니다.");

                continue;
            }

            Require(
                p.purchaseTurn >= 1 &&
                p.purchaseTurn <= turn &&
                p.purchasePrice == p.prices[p.purchaseTurn - 1] &&
                p.principal >= 0 &&
                p.principal <= p.purchasePrice / 2 &&
                p.remainingSaleTurns >= 0 &&
                p.remainingSaleTurns <= 3,
                "부동산 보유 또는 대출 금액이 올바르지 않습니다.");

            if (p.remainingSaleTurns > 0 || p.saleRequested)
            {
                Require(
                    p.principal == 0 &&
                    p.remainingSaleTurns >= 1 &&
                    p.saleRequestedTurn >= p.purchaseTurn &&
                    p.saleRequestedTurn <= turn,
                    "부동산 매각 상태가 올바르지 않습니다.");
            }

            if (p.principal > 0)
            {
                decimal rate = SaveNumber.Read(p.annualRate);

                Require(
                    p.duration >= 1 &&
                    p.duration <= 12 &&
                    p.purchaseTurn + p.duration <= 60 &&
                    p.purchaseTurn + p.duration >= turn &&
                    p.lastInterestPaidTurn == turn - 1 &&
                    rate >= 0 && rate <= 1,
                    "부동산 대출 일정이 올바르지 않습니다.");
            }

            gross = checked(gross + p.prices[turn - 1]);
            debt = checked(debt + p.principal);
        }
    }
}