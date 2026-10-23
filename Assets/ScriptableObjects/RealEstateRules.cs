using UnityEngine;

[CreateAssetMenu(fileName = "RealEstateRules", menuName = "Scriptable Objects/RealEstateRules")]
public class RealEstateRules : ScriptableObject
{
    [Header("Existing Loan Rules")]
    [SerializeField] private LoanRules loanRules;

    [Header("Monthly Price Change")]
    [Tooltip("100 = 1%. -100이면 -1%입니다.")]
    [SerializeField] private int minimumReturnBasisPoints = -100;

    [Tooltip("250이면 +2.5%입니다.")]
    [SerializeField] private int maximumReturnBasisPoints = 250;

    public LoanRules LoanRules => loanRules;

    public decimal MinimumReturn =>
        minimumReturnBasisPoints / 10000m;

    public decimal MaximumReturn =>
        maximumReturnBasisPoints / 10000m;

    public bool Validate(out string error)
    {
        error = "";

        if (loanRules == null)
        {
            error = "기존 LoanRules 에셋을 연결하세요.";
            return false;
        }

        if (!loanRules.Validate(out error))
            return false;

        if (minimumReturnBasisPoints <= -10000 || minimumReturnBasisPoints > maximumReturnBasisPoints)
        {
            error = "부동산 월 변동률 범위를 확인하세요.";
            return false;
        }

        return true;
    }
}
