using System.Collections.Generic;

public enum ReceiptLineType
{
    FixedExpense,   // 고정지출 : 검정
    Change,         // 수익|지출 : 부호에 따라 색상 변경 -빨|+초
}

public readonly struct ReceiptLine
{
    // 자동 구현 프로퍼티 - { get; } 형태
    public string Name { get; }     // 금액명
    public long Amount { get; }     // 금액
    public ReceiptLineType Type { get; }    // 각 타입에 맞게 분류.

    // 생성자
    public ReceiptLine(string name, long amount, ReceiptLineType type)
    {
        Name = name;
        Amount = amount;
        Type = type;
    }
}

public sealed class MonthlyReceiptData
{
    // 자동 구현 프로퍼티 - { get; } 형태
    public IReadOnlyList<ReceiptLine> Lines { get; }    //각 영수증 줄 마다 리스트로 분리하기 위함.(하나씩 띄우는 연출 목적)
    public long AssetCange { get; }     // 이번달 수익률(자산변동)
    public long TotalAsset { get; }     // 총 자산
    public bool IsBankrupt { get; }     // 파산 여부 확인
    public bool IsFinalMonth { get; }   // 종료 턴(달) 도달 여부 확인

    // 생성자
    public MonthlyReceiptData(List<ReceiptLine> lines, long assetChange, long totalAsset, bool isBankrupt, bool isFinalMonth)
    {
        Lines = new List<ReceiptLine>(lines).AsReadOnly();  // ReadOnly로 리스트 선언
        
        AssetCange = assetChange;
        TotalAsset = totalAsset;
        IsBankrupt = isBankrupt;
        IsFinalMonth = isFinalMonth;
    }
}