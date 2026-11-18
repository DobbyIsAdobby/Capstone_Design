using System;
using System.Collections.Generic;

// !대사 스크립트 데이터가 들어오면 후보 목록을 가져오는 부분 교체가 필요함! -- 교체 완료
/// <summary>
/// 정보 대화 스크립트 선택만 담당함. 
/// 대상 상품/정확도/판정/결제에는 관여하지 않음
/// </summary>
public static class InformationTextProvider
{
    public static string GetTitle(MarketSector sector, InformationExposure exposure)
    {
        string typeName = exposure == InformationExposure.Normal ? "일반형" : "인버스형";

        return $"{MarketSectorUtility.GetName(sector)} · {typeName} 전망";
    }

    /// <summary>
    /// 제목, 상품명, 수익률 수치 없이 CSV 문장만 반환.
    /// 공개가 완료될 때 RumorManager가 사용 기록을 추가.
    /// </summary>
    /// <param name="sector"></param>
    /// <param name="exposure"></param>
    /// <param name="presentedDirection"></param>
    /// <param name="random"></param>
    /// <param name="usedTexts"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public static string Create(MarketSector sector, InformationExposure exposure, InformationDirection presentedDirection, Random random, HashSet<string> usedTexts)
    {
        IReadOnlyList<string> candidates;

        if (presentedDirection == InformationDirection.Flat)
        {
            candidates = new[]
            {
                "이번 달은 변동이 없을 것으로 전망합니다.",
                "이번 달은 보합 흐름을 예상합니다.",
                "이번 달은 현재 수준을 유지할 것으로 보입니다."
            };
        }
        else
        {
            // CSV의 POS/NEG는 기초 산업의 상황.
            // 인버스 상승 전망에는 기초 산업의 부정적 근거를 사용.
            InformationDirection marketDirection = exposure == InformationExposure.Inverse ? (InformationDirection)(-(int)presentedDirection) : presentedDirection;

            if (DataManager.Instance == null || !DataManager.Instance.TryGetMarketDialogues(sector, marketDirection, out candidates))
            {
                throw new InvalidOperationException("해당 종목의 정보 대사를 읽지 못했습니다.");
            }
        }

        var available = new List<string>();

        foreach (string text in candidates)
        {
            if (!usedTexts.Contains(text))
                available.Add(text);
        }

        if (available.Count == 0)
        {
            throw new InvalidOperationException("중복 없이 제공할 정보 문장이 부족합니다.");
        }

        return available[random.Next(available.Count)];
    }
}




/* -- 기존 Create() 내에 있던 코드. 정보 스크립트 추가로 인해 더 이상 사용하지 않음.
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
*/
