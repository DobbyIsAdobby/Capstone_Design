using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// LobbyScene에는 GameManager나 DataManager가 없으므로, 로비에서도 상품, 직급 호환성을 검사할 수 있는 설정용 Scriptable Asset
/// </summary>
[CreateAssetMenu(fileName = "SaveValidationConfig", menuName = "Scriptable Objects/SaveValidationConfig")]
public class SaveValidationConfig : ScriptableObject
{
    [SerializeField] private TextAsset shopItemsJson;
    [SerializeField] private JobRules jobRules;

    // GameManager의 AP 설정과 동일하게 지정
    [SerializeField, Min(1)] private int baseMaxAP = 100;

    public void Validate(GameSaveData data)
    {
        SaveDataValidator.Validate(data);

        if (shopItemsJson == null || jobRules == null)
            throw new InvalidOperationException("저장 검증 설정의 JSON과 JobRules를 연결하세요.");

        if (!jobRules.Validate(out string error))
            throw new InvalidOperationException(error);

        var items = new Dictionary<string, ShopItemData>();

        foreach (ShopItemData item in ShopJsonParser.Parse(shopItemsJson.text))
            items.Add(item.Id, item);

        var counts = new Dictionary<string, int>();

        foreach (PurchaseCountSaveData saved in data.shop.purchaseCounts)
        {
            if (!items.TryGetValue(saved.itemId, out ShopItemData item))
                throw new FormatException("저장된 상품 ID가 현재 데이터에 없습니다.");

            if (item.PurchaseLimit > 0 && saved.count > item.PurchaseLimit)
            {
                throw new FormatException("상품 구매 제한을 초과한 기록입니다.");
            }

            counts.Add(saved.itemId, saved.count);
        }

        int maxAP = baseMaxAP;
        var owned = new HashSet<string>();

        foreach (OwnedItemSaveData saved in data.shop.ownedItems)
        {
            if (!items.TryGetValue(saved.itemId, out ShopItemData item) || item.Category != ShopCategory.Prestige)
            {
                throw new FormatException("보유 상품을 복원할 수 없습니다.");
            }

            if (!counts.TryGetValue(saved.itemId, out int count) || count != 1)
                throw new FormatException("보유 상품과 구매 횟수가 일치하지 않습니다.");

            owned.Add(saved.itemId);
            maxAP = checked(maxAP + item.MaxAPBonus);
        }

        foreach (var pair in counts)
        {
            if (items[pair.Key].Category == ShopCategory.Prestige && !owned.Contains(pair.Key))
            {
                throw new FormatException("과시성 상품의 보유 기록이 없습니다.");
            }
        }

        if (data.player.currentAP > maxAP)
            throw new FormatException("저장 AP가 최대치를 초과했습니다.");

        int gradeIndex = data.job.gradeIndex;

        if (gradeIndex < 0 || gradeIndex >= jobRules.Count)
            throw new FormatException("직급을 복원할 수 없습니다.");

        int experience = data.job.currentExperience;

        if (gradeIndex == jobRules.Count - 1)
        {
            if (experience != 0)
                throw new FormatException("최고 직급의 경험치가 올바르지 않습니다.");
        }
        else if (experience >= jobRules.GetGrade(gradeIndex).RequiredExperience)
        {
            throw new FormatException("승급되지 않은 경험치 기록입니다.");
        }
    }
}
