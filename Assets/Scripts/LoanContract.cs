using System;

/// <summary>
/// 대출 1 건의 조건과 처리 상태를 보관하는 class
/// </summary>
public sealed class LoanContract
{
    // 자동 생성 프로퍼티
    public int Id { get; }
    public long Principal { get; }
    public int StartTurn { get; }
    public int Duration { get; }
    public int MaturityTurn { get; }

    // 계약 당시 금리와 월 이자를 고정
    public decimal MonthlyRate { get; }
    public long MonthlyInterest { get; }

    public int LastInterestPaidTurn { get; private set; }
    public bool IsClosed { get; private set; }
    public int ClosedTurn { get; private set; }

    /// <summary>
    /// 생성자
    /// </summary>
    /// <param name="id"></param>
    /// <param name="principal"></param>
    /// <param name="startTurn"></param>
    /// <param name="duration"></param>
    /// <param name="monthlyRate"></param>
    public LoanContract(int id, long principal, int startTurn, int duration, decimal monthlyRate)
    {
        Id = id;
        Principal = principal;
        StartTurn = startTurn;
        Duration = duration;
        MaturityTurn = startTurn + duration;    // 만기일
        MonthlyRate = monthlyRate;

        MonthlyInterest = (long)decimal.Floor(principal * monthlyRate);

        // 실행한 턴에도 첫 이자를 내야함
        LastInterestPaidTurn = startTurn - 1;
        ClosedTurn = -1;
    }

    /// <summary>
    /// 월 이자 납부 기간
    /// </summary>
    /// <param name="turn"></param>
    /// <returns></returns>
    public long GetInterestDue(int turn)
    {
        if (IsClosed || turn < StartTurn || LastInterestPaidTurn >= turn)
        {
            return 0;
        }

        return MonthlyInterest;
    }

    // 접근 제어자 internal = 동일한 어셈블리 내에선 public, 다른 어셈블리에선 private 취급
    // 즉 동일 프로젝트에서는 public, 다른 프로젝트에서는 private임
    /// <summary>
    /// 계약 상태 변경은 LoanManager에서 처리
    /// </summary>
    /// <param name="turn"></param>
    internal void MarkInterestPaid(int turn)
    {
        LastInterestPaidTurn = turn;
    }

    internal void Close(int turn)
    {
        IsClosed = true;
        ClosedTurn = turn;
    }
}
