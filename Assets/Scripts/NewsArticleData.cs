using System;

/// <summary>
/// JSON 신문 한 건.
/// 같은 객체의 title과 text 표시.
/// </summary>
[Serializable]
public sealed class NewsArticleData
{
    public string id;
    public string title;
    public string text;
}

/// <summary>
/// new.json의 최상위 구조.
/// field 이름은 json의 키 이름과 동일해야함.
/// </summary>
[Serializable]
public sealed class NewsCatalogData
{
    public NewsArticleData[] positive_news;
    public NewsArticleData[] negative_news;
}
