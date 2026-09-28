using System;
using System.Collections.Generic;

// !대사 스크립트 데이터가 들어오면 후보 목록을 가져오는 부분 교체가 필요함!
/// <summary>
/// 정보 대화 스크립트 선택만 담당함. 
/// 대상 상품/정확도/판정/결제에는 관여하지 않음
/// </summary>
public static class InformationTextProvider
{
    public static string Create(AssetType asset, InformationDirection direction, int targetTurn, Random random, HashSet<string> usedTexts)
    {
        string assetName = GetAssetName(asset);

        string directionText = direction == InformationDirection.Up ? "상승" : "하락";

        // 추후 DB 대사 연결 위치:
        // 상품 + 방향에 맞는 후보 목록을 이 부분에서 받아오면됨 - 임시 정보 스크립트는 생성형 AI로 제작함
        string[] candidates;

        if (direction == InformationDirection.Flat)
        {
            candidates = new[]
            {
                $"[임시 정보] {targetTurn}턴의 {assetName}은 변동하지 않을 것으로 전망합니다.",
                $"[임시 정보] {assetName}은 {targetTurn}턴에 보합을 유지할 전망입니다.",
                $"[임시 정보] {targetTurn}턴에는 {assetName}의 수익률 변화가 없을 것으로 예상합니다."
            };
        }
        else
        {
            candidates = new[]
            {
                $"[임시 정보] {targetTurn}턴의 {assetName}은 {directionText}할 것으로 전망합니다.",
                $"[임시 정보] {assetName}의 {targetTurn}턴 방향은 {directionText}으로 예상됩니다.",
                $"[임시 정보] {targetTurn}턴에는 {assetName}의 {directionText} 가능성이 제시되고 있습니다."
            };
        }

        // 같은 턴에 이미 공개한 문장은 후보에서 제외함 - 중복 방지
        var available = new List<string>();

        foreach (string text in candidates)
        {
            if (!usedTexts.Contains(text))
                available.Add(text);
        }

        if (available.Count == 0)
        {
            throw new InvalidOperationException("중복 없이 표시할 정보 대사가 부족합니다.");
        }

        return available[random.Next(available.Count)];
    }

    public static string GetAssetName(AssetType asset)
    {
        switch (asset)
        {
            case AssetType.Stock:
                return "주식";
            case AssetType.Leverage:
                return "레버리지";
            case AssetType.StockInverse:
                return "주식 인버스";
            case AssetType.LeverageInverse:
                return "레버리지 인버스";
            default:
                throw new ArgumentOutOfRangeException(nameof(asset));
        }
    }
}
