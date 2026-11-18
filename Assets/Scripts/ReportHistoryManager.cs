using System;
using System.Linq;
using UnityEngine;

/// <summary>
/// 거래와 금액 변동을 기록.
/// </summary>
public class ReportHistoryManager : Singleton<ReportHistoryManager>
{
    private ReportHistory history = new ReportHistory();

    /// <summary>
    /// 매수 성공 이후에만 호출.
    /// 실패한 거래와 매도는 호출하지 않음.
    /// </summary>
    /// <param name="turn"></param>
    /// <param name="key"></param>
    /// <param name="label"></param>
    /// <param name="sector"></param>
    /// <param name="amount"></param>
    public void RecordBuy(int turn, string key, string label, int sector, long amount)
    {
        if (amount <= 0)
            return;

        history.buys.Add(new ReportBuy
        {
            turn = turn,
            key = key,
            label = label,
            sector = sector,
            amount = amount
        });
    }

    /// <summary>
    /// 청구서에 기록되는 금액 변동 보관.
    /// 같은 턴, 이름, 타입은 기존 청구서처럼 합산.
    /// </summary>
    /// <param name="turn"></param>
    /// <param name="label"></param>
    /// <param name="amount"></param>
    /// <param name="type"></param>
    public void RecordChange(int turn, string label, long amount, ReceiptLineType type)
    {
        // 대출 원금이나 매각대금은 소득이 아님.
        if (type == ReceiptLineType.Financing || amount == 0)
            return;

        ReportChange existing = history.changes.Find(row => row.turn == turn && row.label == label && row.type == type);

        if (existing != null)
        {
            existing.amount = checked(existing.amount + amount);
            return;
        }

        history.changes.Add(new ReportChange
        {
            turn = turn,
            label = label,
            type = type,
            amount = amount
        });
    }

    /// <summary>
    /// 해당 턴의 모든 결산 처리가 끝난 뒤 호출.
    /// 차트가 어디까지 공개 가능한지 판단하는 기준.
    /// </summary>
    /// <param name="turn"></param>
    /// <exception cref="InvalidOperationException"></exception>
    public void MarkSettled(int turn)
    {
        if (turn == history.lastSettledTurn)
            return;

        if (turn != history.lastSettledTurn + 1)
        {
            throw new InvalidOperationException("리포트 정산 기록의 턴 순서가 맞지 않습니다.");
        }

        history.lastSettledTurn = turn;
    }

    /// <summary>
    /// 원본의 복사본을 반환.
    /// 저장 및 엔딩 전달에 사용.
    /// </summary>
    /// <returns></returns>
    public ReportHistory CaptureSave()
    {
        return JsonUtility.FromJson<ReportHistory>(JsonUtility.ToJson(history));
    }

    public void RestoreSave(ReportHistory saved)
    {
        history = JsonUtility.FromJson<ReportHistory>(JsonUtility.ToJson(saved));
    }

    /// <summary>
    /// 저장 가능한 월중 상태에서는 직전 턴까지 정산되어 있어야 함.
    /// 로비에서도 호출할 수 있도록 Instance 의존 X.
    /// </summary>
    /// <param name="saved"></param>
    /// <param name="currentTurn"></param>
    /// <exception cref="FormatException"></exception>
    public static void Validate(ReportHistory saved, int currentTurn)
    {
        if (saved == null || saved.buys == null || saved.changes == null || saved.lastSettledTurn != currentTurn - 1)
        {
            throw new FormatException("리포트 이력이 올바르지 않습니다.");
        }

        foreach (ReportBuy row in saved.buys)
        {
            if (row == null || row.turn < 1 ||
                row.turn > currentTurn ||
                string.IsNullOrWhiteSpace(row.key) ||
                string.IsNullOrWhiteSpace(row.label) ||
                row.sector < -1 ||
                row.sector >= MarketSectorUtility.SectorCount ||
                row.amount <= 0)
            {
                throw new FormatException("매수 기록이 올바르지 않습니다.");
            }
        }

        foreach (ReportChange row in saved.changes)
        {
            if (row == null ||
                row.turn < 1 ||
                row.turn > currentTurn ||
                string.IsNullOrWhiteSpace(row.label) ||
                (row.type != ReceiptLineType.Change &&
                 row.type != ReceiptLineType.FixedExpense))
            {
                throw new FormatException("금액 변동 기록이 올바르지 않습니다.");
            }
        }

        // 같은 내역은 이미 합산되어 있어야 함.
        bool duplicated = saved.changes
            .GroupBy(row => new { row.turn, row.label, row.type })
            .Any(group => group.Count() > 1);

        if (duplicated)
        {
            throw new FormatException("리포트 금액 내역이 중복되었습니다.");
        }
    }

    /// <summary>
    /// 거래 기록과 차트에서 함께 사용할 상품 이름.
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public static string ProductName(AssetType type)
    {
        switch (type)
        {
            case AssetType.Bank: return "예금";
            case AssetType.Stock: return "주식";
            case AssetType.Leverage: return "레버리지";
            case AssetType.StockInverse: return "주식 인버스";
            case AssetType.LeverageInverse: return "레버리지 인버스";
            default: throw new ArgumentOutOfRangeException(nameof(type));
        }
    }
}
