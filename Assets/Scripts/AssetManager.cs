using UnityEditor.VersionControl;
using UnityEngine;

// enum으로 자산 분류
public enum AssetType
{
    Bank,    //은행(예적금)
    Stock,   //주식
    Leverage //레버리지
}

public class AssetManager : MonoBehaviour
{
    /*
    Inspector Zone
    */
    public static AssetManager Instance;

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
    private void Awake()
    {
        if(Instance == null) Instance = this;
        else Destroy(gameObject);
    }
    
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
        //1. 은행 (고정 수익률)
        if(bankBalance > 0)
        {
            bankBalance += (long)(bankBalance * bankMontlyRate);
        }

        //2. 주식 (난수(RNG) 수익률)
        if(stockBalance > 0)
        {
            float currentStockRate = Random.Range(stockMinRate, stockMaxRate);
            stockBalance += (long)(stockBalance * currentStockRate);
            Debug.Log($"이번 달 주식 수익률 : {currentStockRate * 100:F1}%");
        }

        //3. 레버리지 (난수 수익률 - 극단적 변동성)
        if(leverageBalance > 0)
        {
            float currentLeverageRate = Random.Range(leverageMinRate, leverageMaxRate);
            leverageBalance += (long)(leverageBalance * currentLeverageRate);
            Debug.Log($"이번 달 레버리지 수익률 : {currentLeverageRate * 100:F1}%");
        }
    }

    /// <summary>
    /// UI [매수하기] 버튼을 통해 자산을 매수할 때 호출할 함수
    /// </summary>
    public void BuyAsset(AssetType type, long amount)
    {
        if(GameManager.Instance.availableCash < amount)
        {
            Debug.Log("가용 현금이 부족합니다.");
            return;
        }

        //현금 차감
        GameManager.Instance.availableCash -= amount;

        //자산 증가
        switch (type)
        {
            case AssetType.Bank:
                bankBalance += amount;
                break;
            case AssetType.Stock:
                stockBalance += amount;
                break;
            case AssetType.Leverage:
                leverageBalance += amount;
                break;
        }

        Debug.Log($"{type} 자산 {amount}원 매수 주문 체결 완료.");
    }

    /// <summary> 
    /// UI [매도하기] 버튼을 통해 자산을 매도할 때 호출할 함수
    /// </summary>
    public void SellAsset(AssetType type, long amount)
    {
        long currentBalance = 0;

        switch (type)
        {
            case AssetType.Bank:
                currentBalance = bankBalance;
                break;
            case AssetType.Stock:
                currentBalance = stockBalance;
                break;
            case AssetType.Leverage:
                currentBalance = leverageBalance;
                break;
        }

        if(currentBalance < amount)
        {
            Debug.Log($"보유한 {type} 자산이 부족하여 매도 주문을 체결할 수 없습니다.");
            return;
        }

        //자산 차감
        switch (type)
        {
            case AssetType.Bank:
                bankBalance -= amount;
                break;
            case AssetType.Stock:
                stockBalance -= amount;
                break;
            case AssetType.Leverage:
                leverageBalance -= amount;
                break;
        }

        //현금 증가
        GameManager.Instance.availableCash += amount;
        Debug.Log($"{type} 자산 {amount}원 매도 주문 체결 완료.");
    }
}
