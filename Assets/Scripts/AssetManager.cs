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
    public long stockBalance = 0; //주식 평가 금액
    public long leverageBalance = 0; //레버리지 평가 금액
    public long stockInverseBalance = 0; //주식 인버스 평가 금액
    public long leverageInverseBalance = 0; //레버리지 인버스 평가 금액

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

        // 보유 여부와 관계없이 해당 턴의 확정된 시장 수익률을 DataManager에서 가져와 조회
        MonthlyMarketRates rates = GetMonthlyMarketRates(turn);

        // 은행은 모델과 관계없이 기존 월 이율 유지함. - 추후 밸런싱 작업 이후 변경할 수 있음.
        bankBalance = ApplyReturn(bankBalance, 0.003m);

        // 각 상품의 보유 평가액에 해당 턴 수익률을 적용함
        stockBalance = ApplyReturn(stockBalance, rates.Stock);
        leverageBalance = ApplyReturn(leverageBalance, rates.Leverage);

        stockInverseBalance = ApplyReturn(stockInverseBalance, rates.StockInverse);

        leverageInverseBalance = ApplyReturn(leverageInverseBalance, rates.LeverageInverse);
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
    /// 자산에 따른 금액 반환을 위한 함수
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    public long GetBalance(AssetType type)
    {
        switch (type)
        {
            case AssetType.Bank:
                return bankBalance;
            case AssetType.Stock:
                return stockBalance;
            case AssetType.Leverage:
                return leverageBalance;
            case AssetType.StockInverse:
                return stockInverseBalance;
            case AssetType.LeverageInverse:
                return leverageInverseBalance;

            default:
                return 0;
        }
    }

    /// <summary>
    /// 거래를 진행할때 호출할 함수
    /// </summary>
    /// <param name="type"></param>
    /// <param name="isBuying"></param>
    /// <param name="amount"></param>
    /// <param name="message"></param>
    /// <returns></returns>
    public bool TryTrade(AssetType type, bool isBuying, long amount, out string message) //bool 을 반환하는 이유 : 패널에서 거래 성공/실패를 구분하기 위함.
    {
        GameManager game = GameManager.Instance;

        if (!game.CanAct)
        {
            message = "지금은 거래가 불가능합니다.";
            return false;
        }

        if(type != AssetType.Bank && type != AssetType.Stock && type != AssetType.Leverage && type != AssetType.StockInverse && type != AssetType.LeverageInverse)
        {
            message = "지원하지 않는 자산입니다.";
            return false;
        }

        if(amount <= 0)
        {
            message = "거래 금액은 1원 이상이어야 합니다.";
            return false;
        }

        // 참일 경우 availableCash 변수 반환, 거짓일 경우 GetBalance 내 각 타입에 맞는 변수 반환.
        long limit = isBuying ? game.availableCash : GetBalance(type);

        if(amount > limit)
        {
            message = isBuying ? "가용 현금이 부족합니다." : "보유 자산이 부족합니다.";
            return false;
        }

        // 매수(참) / 매도(거짓) 으로 자산 변경
        long assetChange = isBuying ? amount : -amount;

        switch (type)
        {
            case AssetType.Bank:
                bankBalance += assetChange;
                break;
            case AssetType.Stock:
                stockBalance += assetChange;
                break;
            case AssetType.Leverage:
                leverageBalance += assetChange;
                break;
            case AssetType.StockInverse:
                stockInverseBalance += assetChange;
                break;
            case AssetType.LeverageInverse:
                leverageInverseBalance += assetChange;
                break;
        }

        game.availableCash -= assetChange;

        message = $"{amount:N0}원 거래가 완료되었습니다.";

        if(UIManager.Instance != null)
        {
            UIManager.Instance.RefreshUI();
        }

        return true;
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

    // 현재 아래 함수는 저장 및 복원하는 메서드임..
    // 모든 Manager 스크립트에 들어가야함.
    // 현재 각 Manager마다 저장해야할 값들이 다 다르기에.. 어쩔 수 없이 다 따로 지정해서 입력해야함..
    // 나도 이러고 싶지 않았음..
    // ...
    // 아래 메서드에 대한 설명은 여기에다가만 달거임.. 궁금하면 이것을 보세요..

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
            leverageInverseBalance = leverageInverseBalance
        };
    }

    /// <summary>
    /// 검증을 통과한 저장 데이터를 현재 상태에 반영함
    /// </summary>
    /// <param name="data"></param>
    public void RestoreSave(AssetSaveData data)
    {
        // 매수, 매도 함수를 호출하지 않고 잔액만 복원
        bankBalance = data.bankBalance;
        stockBalance = data.stockBalance;
        leverageBalance = data.leverageBalance;
        stockInverseBalance = data.stockInverseBalance;
        leverageInverseBalance = data.leverageInverseBalance;
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
