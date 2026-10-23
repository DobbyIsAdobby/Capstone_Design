using System;
using System.Collections.Generic;

[Serializable]
public sealed class RealEstateDefinition
{
    public string id;
    public string name;

    // 최초 가격과 최초 임대료.
    // 인플레이션이나 시세 변동으로 이 값을 직접 변경하지 않음.
    public long basePrice;
    public long baseRent;
}

[Serializable]
public sealed class RealEstateCatalog
{
    public RealEstateDefinition[] properties;

    public NewsArticleData[] positive_news;
    public NewsArticleData[] negative_news;
    public NewsArticleData[] neutral_news;
}

[Serializable]
public sealed class RealEstatePropertyState
{
    public string id;

    // [0]은 최초 가격.
    // [1]~[60]은 각 턴 마감 후 가격.
    public long[] prices;

    public bool owned;
    public int purchaseTurn;
    public long purchasePrice;

    // 부동산 대출은 이 상품에 연결.
    // principal이 0이면 현재 대출이 없는 상태.
    public long principal;
    public int duration;
    public string annualRate = "0";
    public int lastInterestPaidTurn;

    // 취소해도 remainingSaleTurns는 보관.
    // 재신청으로 대기 시간을 다시 추첨하지 않기 위함.
    public bool saleRequested;
    public int saleRequestedTurn;
    public int remainingSaleTurns;
}

[Serializable]
public sealed class RealEstateSaveData
{
    public string catalogHash;

    // 매각 대기 기간 추첨에 사용할 난수 상태.
    public long randomState;

    public int lastMarketSettlementTurn;
    public int lastLoanSettlementTurn;

    public List<RealEstatePropertyState> properties = new List<RealEstatePropertyState>();

    // 매달 신문에서 다룰 부동산과 기사 선택값.
    // 불러오기 이후에도 같은 기사를 보여주도록 저장.
    public int[] newsTargets;
    public int[] newsVariants;
}