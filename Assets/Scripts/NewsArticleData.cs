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
/// news.json 필드 내용
/// </summary>
[Serializable]
public sealed class SectorNewsData
{
    public string sector;
    public NewsArticleData[] positive_news;
    public NewsArticleData[] negative_news;
    public NewsArticleData[] neutral_news;
}


/// <summary>
/// 각 섹터별(종목별) 뉴스 데이터
/// </summary>
[Serializable]
public sealed class NewsCatalogData
{
    public int version;
    public SectorNewsData[] sectors;
}
