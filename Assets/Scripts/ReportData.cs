using System;
using System.Collections.Generic;

/// <summary>
/// 매수 기록 한 건.
/// </summary>
[Serializable]
public sealed class ReportBuy
{
    public int turn;

    // 같은 투자항목을 묶는 식별자
    public string key;

    // 화면에 표시할 이름.
    public string label;

    // 금융 종목은 0~3, 부동산은 -1.
    public int sector;

    // 금융자산 매수액 또는 부동산 전체 매입가격.
    public long amount;
}

/// <summary>
/// 한 턴의 청구서 내역을 합산한 기록.
/// </summary>
[Serializable]
public sealed class ReportChange
{
    public int turn;
    public string label;
    public ReceiptLineType type;
    public long amount;
}

/// <summary>
/// 세이브 파일에 포함할 플레이 이력
/// 아직 결산하지 않은 현재 턴 기록도 포함함.
/// </summary>
[Serializable]
public sealed class ReportHistory
{
    public int lastSettledTurn;
    public List<ReportBuy> buys = new List<ReportBuy>();
    public List<ReportChange> changes = new List<ReportChange>();
}

/// <summary>
/// 엔딩 차트 하나에 필요한 데이터.
/// </summary>
public sealed class ReportSeries
{
    public string title;
    public readonly List<float> percentages = new List<float>();
}

/// <summary>
/// GameScene이 사라지기 전에 확정해서 전달하는 결과.
/// EndingScene에서 GameManager를 다시 조회해서는 안됨.
/// </summary>
public sealed class EndingReportData
{
    public EndingType ending;
    public int endedTurn;
    public bool bankrupt;
    public string cause;
    public long finalNetWorth;

    public ReportHistory history;

    public readonly List<ReportSeries> charts =
        new List<ReportSeries>();

    // 혹시라도 패널을 여러 번 열어도 최고 기록 처리를 반복하지 않음.
    public bool recordChecked;
    public bool isNewRecord;
    public long bestNetWorth;
}