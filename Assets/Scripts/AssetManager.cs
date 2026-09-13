using UnityEngine;

// enum으로 자산 분류
public enum AssetType
{
    Bank,    //은행(예적금)
    Stock,   //주식
    Leverage //레버리지
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

    [Header("Temporary Return Rates (RNG)")]
    //추후 DB 데이터로 대체될 임시 수익률 범위(프로토타이핑 전용)
    //readonly로 불변성을 가진 변수로 선언
    private readonly float bankMontlyRate = 0.003f; //은행 : 월 0.3% (연 약 3.6% 고정)
    private readonly float stockMinRate = -0.05f; //주식 : 월 -5% ~
    private readonly float stockMaxRate = 0.07f; //주식 : 월 +7% (장기 우상향)
    private readonly float leverageMinRate = -0.37f; //레버리지 : 월 -37% ~
    private readonly float leverageMaxRate = 0.40f; //레버리지 : 월 +40%

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
        return bankBalance + stockBalance + leverageBalance;
    }

    /// <summary>
    /// 매 턴(월) 종료 시 GameManager에서 호출하여 각 자산의 수익률을 적용하는 함수
    /// </summary>
    public void CalculateMonthlyReturns()
    {
        int currentTurn = GameManager.Instance.currentMonth; // CSV의 turn과 동일한 체계(Start at 1)

        //1. 은행 (고정 수익률)
        if(bankBalance > 0)
        {
            bankBalance += (long)(bankBalance * bankMontlyRate);
        }

        //2. 주식 (난수(RNG) 수익률) => CSV 데이터 우선 사용
        if(stockBalance > 0)
        {
            if(DataManager.Instance != null && 
                DataManager.Instance.TryGetMarketData(MarketType.Nasdaq, currentTurn, out MarketData nasdaqData))
            {
                stockBalance += (long)(stockBalance * nasdaqData.ReturnRate);
                Debug.Log($"이번 달 주식 수익률(CSV) : {nasdaqData.ReturnRate * 100:F1}%");
            }
            else
            {
                // 데이터가 없는 턴이거나, CSV 미연결 시 기존 RNG로 진행.
                float currentStockRate = Random.Range(stockMinRate, stockMaxRate);
                stockBalance += (long)(stockBalance * currentStockRate);
                Debug.Log($"이번 달 주식 수익률(RNG) : {currentStockRate * 100:F1}%");   
            }
        }

        //3. 레버리지 (난수 수익률 - 극단적 변동성) => CSV 데이터 우선 사용(현재 데이터가 없으나 선택적 연결을 허용했기 때문에 로직 구현 진행함.)
        if(leverageBalance > 0)
        {
            if(DataManager.Instance != null && 
                DataManager.Instance.TryGetMarketData(MarketType.Leverage, currentTurn, out MarketData leverageData))
            {
                leverageBalance += (long)(leverageBalance * leverageData.ReturnRate);
                Debug.Log($"이번 달 레버리지 수익률(CSV) : {leverageData.ReturnRate * 100:F1}%");
            }
            else
            {
                // 데이터가 없는 턴이거나, CSV 미연결 시 기존 RNG로 진행.
                float currentLeverageRate = Random.Range(leverageMinRate, leverageMaxRate);
                leverageBalance += (long)(leverageBalance * currentLeverageRate);
                Debug.Log($"이번 달 레버리지 수익률(RNG) : {currentLeverageRate * 100:F1}%");
            }
        }
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

        if(type != AssetType.Bank && type != AssetType.Stock && type != AssetType.Leverage)
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
}
