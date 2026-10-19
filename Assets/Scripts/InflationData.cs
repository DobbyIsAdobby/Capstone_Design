using System;

[Serializable]
public sealed class InflationNoticeData
{
    public string message;
    public string[] targets;
}

[Serializable]
public sealed class InflationNewsData
{
    public string id;

    // 인상 적용 턴이 아니라 예고 신문을 보여줄 마감 턴.
    public int turn;

    public string title;
    public string text;

    public InflationNoticeData notice;
}

[Serializable]
public sealed class InflationCatalogData
{
    public InflationNewsData[] inflation_news;
}