using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "InflationRules", menuName = "Scriptable Objects/InflationRules")]
public class InflationRules : ScriptableObject
{
    [Serializable]
    private sealed class InflationStep
    {
        [SerializeField, Min(2)]
        private int startTurn;

        [Tooltip("최초 기본 가격에 추가할 비율. 100이면 기본 가격만큼 추가.")]
        [SerializeField, Min(1)]
        private int increasePercent;

        public int StartTurn => startTurn;
        public int IncreasePercent => increasePercent;

        public InflationStep(int turn, int percent)
        {
            startTurn = turn;
            increasePercent = percent;
        }
    }

    [Header("Comsumable Prices / Life Assets expenses / Housing Maintenance")]
    [SerializeField]
    private List<InflationStep> generalSteps =
        new List<InflationStep>
        {
            new InflationStep(12, 100),
            new InflationStep(24, 100),
            new InflationStep(36, 100),
            new InflationStep(48, 100)
        };

    [Header("Monthly Rent")]
    [SerializeField]
    private List<InflationStep> rentSteps =
        new List<InflationStep>
        {
            new InflationStep(12, 25),
            new InflationStep(24, 25),
            new InflationStep(36, 25),
            new InflationStep(48, 25)
        };

    [Header("Infomation Price")]
    [SerializeField]
    private List<InflationStep> informationSteps =
        new List<InflationStep>
        {
            new InflationStep(36, 100)
        };

    /// <summary>
    /// Inspector에 입력한 인상 규칙을 검사.
    /// 각 목록은 적용 턴 오름차순으로 배치해야 함.
    /// </summary>
    /// <param name="error"></param>
    /// <returns></returns>
    public bool Validate(out string error)
    {
        if (!ValidateSteps(generalSteps, "일반 물가", out error))
            return false;

        if (!ValidateSteps(rentSteps, "월세", out error))
            return false;

        return ValidateSteps(informationSteps, "정보 가격", out error);
    }

    private static bool ValidateSteps(List<InflationStep> steps, string label, out string error)
    {
        error = "";

        if (steps == null)
        {
            error = $"{label} 인상 규칙 목록이 없습니다.";
            return false;
        }

        int previousTurn = 1;

        foreach (InflationStep step in steps)
        {
            if (step == null || step.StartTurn <= previousTurn || step.StartTurn > MarketModelConfig.TurnCount || step.IncreasePercent <= 0)
            {
                error = $"{label}: 적용 턴은 2~60의 오름차순이고, " + "추가 인상률은 양수여야 합니다.";

                return false;
            }

            previousTurn = step.StartTurn;
        }

        return true;
    }

    /// <summary>
    /// JSON의 예고 턴과 규칙의 적용 턴을 대조할 때 사용.
    /// 같은 턴이 두 목록에 있어도 호출 측에서 중복을 제거.
    /// </summary>
    /// <returns></returns>
    public IEnumerable<int> GetChangeTurns()
    {
        foreach (InflationStep step in generalSteps)
            yield return step.StartTurn;

        foreach (InflationStep step in rentSteps)
            yield return step.StartTurn;

        foreach (InflationStep step in informationSteps)
            yield return step.StartTurn;
    }

    public long CalculateGeneralPrice(long basePrice, int turn)
    {
        return CalculatePrice(basePrice, turn, generalSteps);
    }

    public long CalculateRent(long baseRent, int turn)
    {
        return CalculatePrice(baseRent, turn, rentSteps);
    }

    public long CalculateInformationPrice(long basePrice, int turn)
    {
        return CalculatePrice(basePrice, turn, informationSteps);
    }

    /// <summary>
    /// 기본 금액 × (1 + 누적 추가 인상률).
    /// 기본 금액이나 규칙 에셋은 변경하지 않음.
    /// </summary>
    /// <param name="basePrice"></param>
    /// <param name="turn"></param>
    /// <param name="steps"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    private static long CalculatePrice(long basePrice, int turn, List<InflationStep> steps)
    {
        if (basePrice < 0)
            throw new ArgumentOutOfRangeException(nameof(basePrice));

        if (turn < 1 || turn > MarketModelConfig.TurnCount)
            throw new ArgumentOutOfRangeException(nameof(turn));

        decimal multiplier = 1m;

        foreach (InflationStep step in steps)
        {
            if (turn >= step.StartTurn)
            {
                // 현재 배율을 곱하는 것이 아니라, 기본 가격에 대한 추가 비율을 더함.
                multiplier += step.IncreasePercent / 100m;
            }
        }

        decimal calculated = basePrice * multiplier;

        // 추후 100% 이외의 인상률을 사용해도 원 단위로 처리.
        return checked((long)decimal.Round(calculated, 0, MidpointRounding.AwayFromZero));
    }
}
