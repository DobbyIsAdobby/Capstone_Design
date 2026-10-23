using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "JobRules", menuName = "Scriptable Objects/JobRules")]
public class JobRules : ScriptableObject
{
    [Serializable]
    public class Grade
    {
        [SerializeField] private string gradeName;
        [SerializeField] private long salary;
        [SerializeField] private int requiredExperience;

        [Header("Overtime")]
        [Tooltip("모든 야근에 지급하는 기본수당")]
        [SerializeField] private long overtimeBasePay;

        [Tooltip("월 추가수당 적용 횟수 안에서 기본수당에 더할 금액")]
        [SerializeField] private long overtimeBonusPay;

        public string Name => gradeName;
        public long Salary => salary;
        public int RequiredExperience => requiredExperience;

        public long OvertimeBasePay => overtimeBasePay;
        public long OvertimeBonusPay => overtimeBonusPay;

        public Grade(string name, long pay, int required, long basePay, long bonusPay)
        {
            gradeName = name;
            salary = pay;
            requiredExperience = required;

            overtimeBasePay = basePay;
            overtimeBonusPay = bonusPay;
        }
    }

    [Header("JobGrade")]
    [SerializeField]
    private List<Grade> grades = new List<Grade>
    {
        // 직급명, 월급, 승급 요구 경험치, 야근 기본수당, 첫 구간 추가수당
        new Grade("백수",          0,  50, 50_000,       0),
        new Grade("알바",    500_000,  80, 51_000,   9_000),
        new Grade("인턴",  1_000_000, 120, 52_000,  18_000),
        new Grade("사원",  1_800_000, 170, 53_000,  32_000),
        new Grade("주임",  2_600_000, 230, 54_000,  46_000),
        new Grade("대리",  3_600_000, 300, 55_000,  60_000),
        new Grade("과장",  4_900_000, 390, 56_000,  74_000),
        new Grade("차장",  6_400_000, 500, 57_000,  88_000),
        new Grade("부장",  8_100_000, 650, 58_000, 102_000),
        new Grade("임원", 10_000_000,   0, 60_000, 115_000)
    };

    [Header("Overtime Rule")]
    [Tooltip("매달 처음 몇 회의 야근에 직급 추가수당을 지급할지 설정")]
    [SerializeField, Range(0, 30)]
    private int overtimeBonusCount = 10;

    public int OvertimeBonusCount => overtimeBonusCount;

    [Header("Play Cost & Reward")]
    [SerializeField, Min(1)] private int apCost = 30;
    [SerializeField, Min(0)] private int successExperience = 20;
    [SerializeField, Min(0)] private int failureExperience = 10;

    [Header("Minigame")]
    [Tooltip("1이면 한쪽 끝에서 반대 끝까지 약 1초")]
    [SerializeField, Min(0.1f)] private float travelSpeed = 0.8f;

    [Tooltip("원이 이동하는 가로 범위 대비 성공 구간 비율")]
    [SerializeField, Range(0.05f, 0.8f)]
    private float successWidth = 0.22f;

    public int Count => grades.Count;
    public int APCost => apCost;
    public int SuccessExperience => successExperience;
    public int FailureExperience => failureExperience;
    public float TravelSpeed => travelSpeed;
    public float SuccessWidth => successWidth;

    public Grade GetGrade(int index)
    {
        return grades[index];
    }

    public bool Validate(out string error)
    {
        error = "";

        if (grades == null || grades.Count != 10)
        {
            error = "직급은 10개여야 합니다.";
            return false;
        }

        long previousSalary = -1;
        int previousRequirement = 0;

        for (int i = 0; i < grades.Count; i++)
        {
            Grade grade = grades[i];

            if (grade == null || string.IsNullOrWhiteSpace(grade.Name) || grade.Salary < 0 || grade.Salary < previousSalary)
            {
                error = "직급 이름과 급여 설정을 확인하세요.";
                return false;
            }

            // 기본수당은 양수, 추가수당은 0 이상이어야 함.
            // 새 필드의 Inspector 입력이 누락되어 0원이 되는 것도 방지해야함.
            if (grade.OvertimeBasePay <= 0 || grade.OvertimeBonusPay < 0)
            {
                error = $"{grade.Name}: 야근 기본수당과 추가수당을 확인하세요.";
                return false;
            }

            // 두 금액을 더했을 때 long 범위를 초과하지 않도록 검사.
            if (grade.OvertimeBonusPay > long.MaxValue - grade.OvertimeBasePay)
            {
                error = $"{grade.Name}: 야근 수당 합계가 너무 큽니다.";
                return false;
            }

            if (i < grades.Count - 1)
            {
                if (grade.RequiredExperience <= previousRequirement)
                {
                    error = "승급 요구 경험치는 직급마다 증가해야 합니다.";
                    return false;
                }

                previousRequirement = grade.RequiredExperience;
            }
            else if (grade.RequiredExperience != 0)
            {
                error = "최고 직급의 요구 경험치는 0이어야 합니다.";
                return false;
            }

            previousSalary = grade.Salary;
        }

        if (apCost <= 0 || failureExperience < 0 || successExperience < failureExperience || travelSpeed <= 0 || successWidth <= 0 || successWidth >= 1)
        {
            error = "미니게임 설정을 확인하세요.";
            return false;
        }

        // 게임의 월 최대 야근 횟수는 30회.
        if (overtimeBonusCount < 0 || overtimeBonusCount > 30)
        {
            error = "야근 추가수당 적용 횟수는 0~30회여야 합니다.";
            return false;
        }

        return true;
    }
}
