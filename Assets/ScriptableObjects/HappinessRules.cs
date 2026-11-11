using System;
using UnityEngine;

[CreateAssetMenu(fileName = "HappinessRules", menuName = "Scriptable Objects/HappinessRules")]
public class HappinessRules : ScriptableObject
{
    [Header("Cumulative Happiness Targets")]
    [SerializeField]
    private int[] thresholds =
    {
        10, 40, 90, 160, 250, 360, 490
    };

    [Header("Golden Age")]
    [SerializeField, Min(1)]
    private int durationTurns = 2;

    [SerializeField, Range(0f, 1f)]
    private float apCostMultiplier = 0.5f;

    [SerializeField, Range(0f, 1f)]
    private float stressMultiplier = 0.5f;

    public int ThresholdCount => thresholds.Length;
    public int DurationTurns => durationTurns;
    public float APCostMultiplier => apCostMultiplier;
    public float StressMultiplier => stressMultiplier;

    public int GetThreshold(int index)
    {
        return thresholds[index];
    }

    public void Validate()
    {
        if (thresholds == null || thresholds.Length == 0)
            throw new InvalidOperationException("행복도 목표가 없습니다.");

        int previous = 0;

        foreach (int value in thresholds)
        {
            if (value <= previous)
            {
                throw new InvalidOperationException("행복도 목표는 양수이며 순서대로 증가해야 합니다.");
            }

            previous = value;
        }

        if (durationTurns < 1 || apCostMultiplier < 0f || apCostMultiplier > 1f || stressMultiplier < 0f || stressMultiplier > 1f)
        {
            throw new InvalidOperationException("전성기 기간과 효과 설정을 확인하세요.");
        }
    }

    /// <summary>
    /// 저장 당시와 현재의 규칙이 같은지 비교할 때 사용
    /// </summary>
    /// <returns></returns>
    public string GetSignature()
    {
        return JsonUtility.ToJson(this);
    }
}
