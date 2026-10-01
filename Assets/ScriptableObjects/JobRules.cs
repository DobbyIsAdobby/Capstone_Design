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

        public string Name => gradeName;
        public long Salary => salary;
        public int RequiredExperience => requiredExperience;

        public Grade(string name, long pay, int required)
        {
            gradeName = name;
            salary = pay;
            requiredExperience = required;
        }
    }

    [Header("JobGrade")]
    [SerializeField]
    private List<Grade> grades = new List<Grade>
    {
        new Grade("백수", 0, 50),
        new Grade("알바", 500_000, 80),
        new Grade("인턴", 1_000_000, 120),
        new Grade("사원", 1_800_000, 170),
        new Grade("주임", 2_600_000, 230),
        new Grade("대리", 3_600_000, 300),
        new Grade("과장", 4_900_000, 390),
        new Grade("차장", 6_400_000, 500),
        new Grade("부장", 8_100_000, 650),
        new Grade("임원", 10_000_000, 0)
    };

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

        return true;
    }
}
