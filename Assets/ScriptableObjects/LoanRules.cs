using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

// 대출 규칙이므로 MonoBehaviour로 Scene이 로드될때 마다 새롭게 생성될 필요가 없음.
// 따라서 ScriptableObject로 생성. - 데이터만 저장하는 객체이기 때문
// MonoBehaviour와 다르게 씬이 바뀌어도 유지되며, 씬에 없어도 되고, 같은 데이터를 계속 사용 가능하다.
// 이렇게 하면 나중에 밸런스를 조정할 때 계산 코드에 손대지 않아도 됨.

[CreateAssetMenu(fileName = "LoanRules", menuName = "Scriptable Objects/LoanRules")]
public class LoanRules : ScriptableObject
{
    [Serializable]
    private class Tier
    {
        [SerializeField] private long minimumDeposit;   // 티어별 최소 예금액
        [SerializeField] private long loanLimit;        // 티어별 대출 한도

        public long MinimumDeposit => minimumDeposit;
        public long LoanLimit => loanLimit;

        public Tier(long deposit, long limit)
        {
            minimumDeposit = deposit;
            loanLimit = limit;
        }
    }

    [Header("Loan Tiers")]
    [SerializeField]
    private List<Tier> tiers = new List<Tier>
    {
        new Tier(1_000_000, 6_000_000),     // 1구간
        new Tier(5_000_000, 25_000_000),    // 2구간
        new Tier(20_000_000, 80_000_000),   // 3구간
        new Tier(50_000_000, 150_000_000),  // 4구간
        new Tier(100_000_000, 200_000_000)  // 5구간
    };

    [Header("Interest Rate")]
    [Tooltip("연이율 설정: 100 = 연 1%, 700 = 연 7%")]
    [FormerlySerializedAs("monthlyRateBasisPoints")]
    [SerializeField, Range(0, 10000)]
    private int annualRateBasisPoints = 700;

    [Header("Prerequisites")]
    [SerializeField, Range(1, 12)]
    private int maxDuration = 12;

    [SerializeField]
    private long minimumLoanAmount = 10_000;

    [SerializeField]
    private long amountStep = 10_000;

    // float 오차를 피하기 위해 decimal type 사용.
    // 연 7%는 0.07
    public decimal AnnualRate => annualRateBasisPoints / 10000m;

    // 월 환산 금리
    public decimal MonthlyRate => AnnualRate / 12m;
    public int MaxDuration => maxDuration;
    public long MinimumLoanAmount => minimumLoanAmount;
    public long AmountStep => amountStep;

    /// <summary>
    /// 게임 시작 시 설정 오류를 검사
    /// </summary>
    /// <param name="error"></param>
    /// <returns></returns>
    public bool Validate(out string error)
    {
        error = "";

        if (tiers == null || tiers.Count != 5)
        {
            error = "대출 구간은 5개여야 합니다.";
            return false;
        }

        long previousDeposit = 0;
        long previousLimit = 0;

        foreach (Tier tier in tiers)
        {
            if (tier == null || tier.MinimumDeposit <= previousDeposit || tier.LoanLimit <= previousLimit)
            {
                error = "예금 기준과 대출 한도는 양수이며 오름차순이어야 합니다.";
                return false;
            }

            previousDeposit = tier.MinimumDeposit;
            previousLimit = tier.LoanLimit;
        }

        if (minimumLoanAmount <= 0 || amountStep <= 0 || minimumLoanAmount % amountStep != 0)
        {
            error = "최소 금액과 금액 단위를 확인하세요.";
            return false;
        }

        if (maxDuration < 1 || maxDuration > 12 || annualRateBasisPoints < 0 || annualRateBasisPoints > 10000)
        {
            error = "대출 기간 또는 금리가 올바르지 않습니다.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// 심사용 예금액에 맞는 구간의 고정 한도를 반환
    /// </summary>
    /// <param name="eligibleDeposit"></param>
    /// <returns></returns>
    public long GetLimit(long eligibleDeposit)
    {
        long result = 0;

        foreach (Tier tier in tiers)
        {
            if (eligibleDeposit < tier.MinimumDeposit)
                break;

            result = tier.LoanLimit;
        }

        return result;
    }

    public long CalculateInterest(long principal)
    {
        // 연 이자를 계산한 뒤 12개월로 나누고, 마지막에 원 미만을 버림
        // 월 환산 금리를 먼저 반올림하지 않음
        return (long)decimal.Floor(principal * AnnualRate / 12m);
    }

    public string GetHelpText()
    {
        string text = "심사용 예금액 =\n 실제 예금액 - 미상환 대출 원금\n" + "추가 가능액 =\n 구간 한도 - 미상환 대출 원금\n\n";

        for (int i = 0; i < tiers.Count; i++)
        {
            Tier tier = tiers[i];

            text += $"{i + 1}구간: {tier.MinimumDeposit:N0}원 이상\n" + $"한도 {tier.LoanLimit:N0}원\n";
        }

        return text;
    }
}
