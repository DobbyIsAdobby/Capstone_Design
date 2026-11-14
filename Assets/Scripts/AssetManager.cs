using System;
using System.Collections.Generic;
using UnityEngine;

// enum으로 자산 분류
public enum AssetType
{
    Bank = 0,    //은행(예적금)
    Stock = 1,   //주식
    Leverage = 2, //레버리지
    StockInverse = 3, //주식 인버스
    LeverageInverse = 4, //레버리지 인버스
}

public class AssetManager : Singleton<AssetManager>
{
    /*
    Inspector Zone
    */
    //public static AssetManager Instance;

    [Header("Asset Balances")]
    public long bankBalance = 0; //은행 예치금
    // 첫 번째 인덱스: 종목 0~3
    // 두 번째 인덱스: 상품 0~3
    // 상품 인덱스는 AssetType에서 은행을 제외한 번호.
    private long[,] holdings = new long[4, 4];

    // 기존 HUD와 GameManager가 읽는 이름을 유지.
    // 각 값은 네 종목의 합계.
    public long stockBalance => GetBalance(AssetType.Stock); // 주식 평가액 합산
    public long leverageBalance => GetBalance(AssetType.Leverage); // 레버리지 평가액 합산
    public long stockInverseBalance => GetBalance(AssetType.StockInverse); // 주식 인버스 평가액 합산
    public long leverageInverseBalance => GetBalance(AssetType.LeverageInverse); // 레버리지 인버스 평가액 합산

    private static int GetProductIndex(AssetType type)
    {
        switch (type)
        {
            case AssetType.Stock:
                return 0;
            case AssetType.Leverage:
                return 1;
            case AssetType.StockInverse:
                return 2;
            case AssetType.LeverageInverse:
                return 3;
            default:
                throw new ArgumentOutOfRangeException(nameof(type));
        }
    }

    /* -- ONNX 모델 추가로 인해 더이상 사용하지 않는 인스펙터 값
    [Header("Temporary Return Rates (RNG)")]
    //추후 DB 데이터로 대체될 임시 수익률 범위(프로토타이핑 전용)
    //readonly로 불변성을 가진 변수로 선언
    //private readonly float bankMonthlyRate = 0.003f; //은행 : 월 0.3% (연 약 3.6% 고정)
    private readonly float stockMinRate = -0.05f; //주식 : 월 -5% ~
    private readonly float stockMaxRate = 0.07f; //주식 : 월 +7% (장기 우상향)
    private readonly float leverageMinRate = -0.37f; //레버리지 : 월 -37% ~
    private readonly float leverageMaxRate = 0.40f; //레버리지 : 월 +40%
    */

    /*
    function Zone
    */

    //싱글톤 패턴
    /*private void Awake()
    {
        if(Instance == null) Instance = this;
        else Destroy(gameObject);
    }*/
    
    /// <summary>
    /// GameManager의 TotalAsset 프로퍼티에서 호출하는 총 투자금액 반환 함수
    /// </summary>
    public long GetTotalInvestedValue()
    {
        return bankBalance + stockBalance + leverageBalance + stockInverseBalance + leverageInverseBalance;
    }

    /// <summary>
    /// 매 턴(월) 종료 시 GameManager에서 호출하여 각 자산의 수익률을 적용하는 함수
    /// </summary>
    public void CalculateMonthlyReturns()
    {
        int turn = GameManager.Instance.currentMonth;
        DataManager data = DataManager.Instance;

        // 일부 종목만 정산된 상태로 실패하지 않도록 새 배열에서 모든 계산을 완료한 뒤 반영.
        long[,] nextHoldings = (long[,])holdings.Clone();

        long nextBankBalance = ApplyReturn(bankBalance, 0.003m);

        for (int sectorIndex = 0; sectorIndex < MarketSectorUtility.SectorCount; sectorIndex++)
        {
            MarketSector sector = (MarketSector)sectorIndex;

            if (data == null || !data.TryGetGeneratedMarketRates(sector, turn, out MonthlyMarketRates rates))
            {
                throw new InvalidOperationException($"{sector} {turn}턴 수익률이 없습니다.");
            }

            nextHoldings[sectorIndex, 0] = ApplyReturn(holdings[sectorIndex, 0], rates.Stock);

            nextHoldings[sectorIndex, 1] = ApplyReturn(holdings[sectorIndex, 1], rates.Leverage);

            nextHoldings[sectorIndex, 2] = ApplyReturn(holdings[sectorIndex, 2], rates.StockInverse);

            nextHoldings[sectorIndex, 3] = ApplyReturn(holdings[sectorIndex, 3], rates.LeverageInverse);
        }

        holdings = nextHoldings;
        bankBalance = nextBankBalance;
    }

    /// <summary>
    /// 수익률 계산 함수
    /// </summary>
    /// <param name="balance"></param>
    /// <param name="rate"></param>
    /// <returns></returns>
    private long ApplyReturn(long balance, decimal rate)
    {
        if (balance <= 0)
            return balance;

        // 차입 없는 투자상품으로 취급하여 평가액의 최솟값은 0원
        if (rate < -1m)
        {
            Debug.LogWarning(
                $"수익률 {rate * 100m:F4}%가 -100% 미만이므로 " +
                "해당 투자 평가액을 0원으로 처리합니다.");

            return 0;
        }

        // 원 미만 손익은 0 방향으로 버림
        long profit = (long)(balance * rate);

        return balance + profit;
    }

    /// <summary>
    /// HUD용 조회. 상품 유형에 해당하는 네 종목을 합산.
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    public long GetBalance(AssetType type)
    {
        if (type == AssetType.Bank)
            return bankBalance;

        int productIndex = GetProductIndex(type);
        long total = 0;

        for (int sectorIndex = 0; sectorIndex < MarketSectorUtility.SectorCount; sectorIndex++)
        {
            total = checked(total + holdings[sectorIndex, productIndex]);
        }

        return total;
    }

    /// <summary>
    /// 거래 패널용 조회. 선택한 종목의 보유액만 반환.
    /// </summary>
    /// <param name="type"></param>
    /// <param name="sector"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public long GetBalance(AssetType type, MarketSector sector)
    {
        if (type == AssetType.Bank)
            return bankBalance;

        if (!MarketSectorUtility.IsValid(sector))
            throw new ArgumentOutOfRangeException(nameof(sector));

        return holdings[(int)sector, GetProductIndex(type)];
    }

    /// <summary>
    /// 거래를 진행할때 호출할 함수
    /// </summary>
    /// <param name="type"></param>
    /// <param name="sector"></param>
    /// <param name="isBuying"></param>
    /// <param name="amount"></param>
    /// <param name="message"></param>
    /// <returns></returns>
    public bool TryTrade(AssetType type, MarketSector sector, bool isBuying, long amount, out string message) //bool 을 반환하는 이유 : 패널에서 거래 성공/실패를 구분하기 위함.
    {
        GameManager game = GameManager.Instance;

        // Spotlight 튜토리얼 시스템은 거래 방법 설명까지만 진행.
        if (SpotlightTutorialManager.Instance != null && SpotlightTutorialManager.Instance.IsBusy)
        {
            message = "안내를 마친 뒤 이용해주세요.";
            return false;
        }

        if (!game.CanAct)
        {
            message = "지금은 거래가 불가능합니다.";
            return false;
        }

        if (type != AssetType.Bank && !MarketSectorUtility.IsValid(sector))
        {
            message = "지원하지 않는 종목입니다.";
            return false;
        }

        if(type != AssetType.Bank && type != AssetType.Stock && type != AssetType.Leverage && type != AssetType.StockInverse && type != AssetType.LeverageInverse)
        {
            message = "지원하지 않는 자산입니다.";
            return false;
        }

        // 자산 종류에 따라 해금 턴을 다르게 적용.
        TutorialFeature feature = TutorialManager.GetAssetFeature(type);

        if (!TutorialManager.Check(feature, out message))
        {
            return false;
        }

        if(amount <= 0)
        {
            message = "거래 금액은 1원 이상이어야 합니다.";
            return false;
        }

        // 참일 경우 availableCash 변수 반환, 거짓일 경우 GetBalance 내 각 타입에 맞는 변수 반환.
        // 매도 한도는 상품군 전체가 아니라 선택 종목의 보유액.
        long limit = isBuying ? game.availableCash : GetBalance(type, sector);

        if(amount > limit)
        {
            message = isBuying ? "가용 현금이 부족합니다." : "보유 자산이 부족합니다.";
            return false;
        }

        // 매수(참) / 매도(거짓) 으로 자산 변경
        long assetChange = isBuying ? amount : -amount;

        if (type == AssetType.Bank)
        {
            bankBalance = checked(bankBalance + assetChange);
        }
        else
        {
            int sectorIndex = (int)sector;
            int productIndex = GetProductIndex(type);

            holdings[sectorIndex, productIndex] = checked(holdings[sectorIndex, productIndex] + assetChange);
        }

        game.availableCash -= assetChange;

        message = $"{amount:N0}원 거래가 완료되었습니다.";

        if(UIManager.Instance != null)
        {
            UIManager.Instance.RefreshUI();
        }

        // 은행 예금과 매도는 투자 매수 안내 대상에서 제외.
        if (isBuying && type != AssetType.Bank)
        {
            SpotlightTutorialManager.Instance?.Notify("InvestmentPurchased");
        }

        return true;
    }

    /// <summary>
    /// 종목을 전달하지 않는 기존 호출은 기술주로 처리.
    /// 신규 거래 패널에서는 반드시 종목을 전달하는 메서드를 사용.
    /// </summary>
    /// <param name="type"></param>
    /// <param name="isBuying"></param>
    /// <param name="amount"></param>
    /// <param name="message"></param>
    /// <returns></returns>
    public bool TryTrade(AssetType type, bool isBuying, long amount, out string message)
    {
        return TryTrade(type, MarketSector.Technology, isBuying, amount, out message);
    }

    /// <summary>
    /// UI [매수하기] 버튼을 통해 자산을 매수할 때 호출할 함수 - TryTrade로 핵심 기능 대체 완료
    /// </summary>
    public void BuyAsset(AssetType type, long amount)
    {
        TryTrade(type, true, amount, out string message);
        Debug.Log(message);
    }

    /// <summary> 
    /// UI [매도하기] 버튼을 통해 자산을 매도할 때 호출할 함수 - TryTrade로 핵심 기능 대체 완료
    /// </summary>
    public void SellAsset(AssetType type, long amount)
    {
        TryTrade(type, false, amount, out string message);
        Debug.Log(message);
    }

    /// <summary>
    /// 새로 수익률을 생성하지 않고, 게임 시작 시 확정된 이번 턴의 수익률을 읽어옴
    /// </summary>
    /// <param name="turn"></param>
    /// <returns></returns>
    /// <exception cref="System.InvalidOperationException"></exception>
    private MonthlyMarketRates GetMonthlyMarketRates(int turn)
    {
        DataManager data = DataManager.Instance;

        if (data == null || !data.TryGetGeneratedMarketRates(turn, out MonthlyMarketRates rates))
        {
            // 데이터 누락을 다른 난수로 대체하면 이번 플레이에서 확정된 시장과 달라지므로 오류로 처리함
            throw new System.InvalidOperationException($"{turn}턴의 생성된 시장 데이터가 없습니다.");
        }

        Debug.Log($"[{turn}턴][ONNX 모델] " + $"주식 {rates.Stock * 100m:F4}% / " + $"레버리지 {rates.Leverage * 100m:F4}% / " + $"주식 인버스 {rates.StockInverse * 100m:F4}% / " + $"레버리지 인버스 {rates.LeverageInverse * 100m:F4}%");

        return rates;
    }

    // 직전 월 정산에서 발생한 손익
    // 새 게임에서는 기본값인 0으로 시작함
    public long LastBankProfit { get; private set; }
    public long LastStockGroupProfit { get; private set; }
    public long LastLeverageGroupProfit { get; private set; }

    /// <summary>
    /// 정산이 끝나고 손익을 갱신함.
    /// </summary>
    /// <param name="bankProfit"></param>
    /// <param name="stockGroupProfit"></param>
    /// <param name="leverageGroupProfit"></param>
    public void RecordLastMonthlyProfits(long bankProfit, long stockGroupProfit, long leverageGroupProfit)
    {
        LastBankProfit = bankProfit;
        LastStockGroupProfit = stockGroupProfit;
        LastLeverageGroupProfit = leverageGroupProfit;
    }


    // 현재 아래 함수는 저장 및 복원하는 메서드임..
    // 모든 Manager 스크립트에 들어가야함.
    // 현재 각 Manager마다 저장해야할 값들이 다 다르기에.. 어쩔 수 없이 다 따로 지정해서 입력해야함..
    // 나도 이러고 싶지 않았음..
    // ...
    // 아래 메서드에 대한 설명은 여기에다가만 달거임.. 궁금하면 이것을 보세요..

    /// <summary>
    /// 투자 종목 추가에 따른 CaptureSave 보완용 메서드
    /// </summary>
    /// <returns></returns>
    private List<SectorHoldingSaveData> CaptureHoldings()
    {
        var rows = new List<SectorHoldingSaveData>();

        for (int sectorIndex = 0; sectorIndex < MarketSectorUtility.SectorCount; sectorIndex++)
        {
            for (int productIndex = 0; productIndex < 4; productIndex++)
            {
                // 잔액이 0인 항목도 저장해 항상 16개를 유지.
                rows.Add(new SectorHoldingSaveData
                {
                    sector = (MarketSector)sectorIndex,
                    assetType = (AssetType)(productIndex + 1),
                    balance = holdings[sectorIndex, productIndex]
                });
            }
        }

        return rows;
    }

    /// <summary>
    /// 16개 보유 내역과 상품별 합계가 일치하는지 검사.
    /// </summary>
    /// <param name="data"></param>
    /// <returns></returns>
    /// <exception cref="FormatException"></exception>
    public static long[,] DecodeHoldings(AssetSaveData data)
    {
        if (data == null || data.bankBalance < 0 || data.holdings == null || data.holdings.Count != 16)
        {
            throw new FormatException("종목별 보유 내역이 올바르지 않습니다.");
        }

        var restored = new long[4, 4];
        var occupied = new bool[4, 4];
        var totals = new long[4];

        foreach (SectorHoldingSaveData row in data.holdings)
        {
            if (row == null || !MarketSectorUtility.IsValid(row.sector) || row.balance < 0 || (int)row.assetType < (int)AssetType.Stock || (int)row.assetType > (int)AssetType.LeverageInverse)
            {
                throw new FormatException("잘못된 종목별 보유 기록입니다.");
            }

            int sectorIndex = (int)row.sector;
            int productIndex = GetProductIndex(row.assetType);

            if (occupied[sectorIndex, productIndex])
                throw new FormatException("종목별 보유 기록이 중복되었습니다.");

            occupied[sectorIndex, productIndex] = true;
            restored[sectorIndex, productIndex] = row.balance;

            totals[productIndex] = checked(totals[productIndex] + row.balance);
        }

        if (totals[0] != data.stockBalance || totals[1] != data.leverageBalance || totals[2] != data.stockInverseBalance || totals[3] != data.leverageInverseBalance)
        {
            throw new FormatException("종목별 보유액과 상품 합계가 다릅니다.");
        }

        return restored;
    }

    /// <summary>
    /// 현재 상태를 파일용 데이터로 복사함.
    /// </summary>
    /// <returns></returns>
    public AssetSaveData CaptureSave()
    {
        return new AssetSaveData
        {
            bankBalance = bankBalance,
            stockBalance = stockBalance,
            leverageBalance = leverageBalance,
            stockInverseBalance = stockInverseBalance,
            leverageInverseBalance = leverageInverseBalance,
            
            // 현재 잔액과 직전 정산 손익을 함께 저장.
            lastBankProfit = LastBankProfit,
            lastStockGroupProfit = LastStockGroupProfit,
            lastLeverageGroupProfit = LastLeverageGroupProfit,

            holdings = CaptureHoldings(),  
        };
    }

    /// <summary>
    /// 검증을 통과한 저장 데이터를 현재 상태에 반영함
    /// </summary>
    /// <param name="data"></param>
    public void RestoreSave(AssetSaveData data)
    {
        // 전체를 검증한 뒤 상태를 변경
        long[,] restored = DecodeHoldings(data);

        holdings = restored;
        bankBalance = data.bankBalance;

        LastBankProfit = data.lastBankProfit;
        LastStockGroupProfit = data.lastStockGroupProfit;
        LastLeverageGroupProfit = data.lastLeverageGroupProfit;
    }

    /// <summary>
    /// CSV 우선, 없을 시 RNG 진행 -- ONNX 모델 적용으로 인해 더 이상 사용하지 않음.
    /// </summary>
    /// <param name="marketType"></param>
    /// <param name="turn"></param>
    /// <param name="minRate"></param>
    /// <param name="maxRate"></param>
    /// <param name="source"></param>
    /// <returns></returns>
    /*private decimal GetBaseReturnRate(MarketType marketType, int turn, float minRate, float maxRate, out string source)
    {
        DataManager data = DataManager.Instance;

        if (data != null && data.TryGetMarketData(marketType, turn, out MarketData marketData))
        {
            source = "CSV";
            return marketData.ReturnRate;
        }

        source = "RNG";
        return (decimal)Random.Range(minRate, maxRate);
    }*/

    /// <summary>
    /// 주식과 레버리지의 인버스 표기 (부호 변경) -- ONNX 모델 적용으로 인해 동일한 이름의 새로운 메서드로 위에 새로 제작함.
    /// </summary>
    /// <param name="turn"></param>
    /// <returns></returns>
    /*private MonthlyMarketRates GetMonthlyMarketRates(int turn)
    {
        decimal stockRate = GetBaseReturnRate(MarketType.Nasdaq, turn, stockMinRate, stockMaxRate, out string stockSource);

        decimal leverageRate = GetBaseReturnRate(MarketType.Leverage, turn, leverageMinRate, leverageMaxRate, out string leverageSource);

        MonthlyMarketRates rates = new MonthlyMarketRates(stockRate, leverageRate, -stockRate, -leverageRate);

        Debug.Log($"[{turn}턴][{stockSource}] " + $"주식: {rates.Stock * 100m:F4}% / " + $"주식 인버스: {rates.StockInverse * 100m:F4}%");

        Debug.Log($"[{turn}턴][{leverageSource}] " + $"레버리지: {rates.Leverage * 100m:F4}% / " + $"레버리지 인버스: {rates.LeverageInverse * 100m:F4}%");

        return rates;
    }*/
}
