using System;
using System.Collections.Generic;
using UnityEngine;

public static class ShopJsonParser
{
    [Serializable]
    private sealed class RootDto
    {
        public ItemDto[] shop_items;
    }

    [Serializable]
    private sealed class ItemDto
    {
        public string item_id;
        public string name;
        public string item_type;
        public long price;
        public int purchase_limit;
        public string description;
        public EffectDto[] effects;
    }

    [Serializable]
    private sealed class EffectDto
    {
        public string type;
        public double value;
        public string trigger;
    }

    public static List<ShopItemData> Parse(string json)
    {
        if(string.IsNullOrWhiteSpace(json))
            throw new FormatException("상점 JSON이 비어 있습니다.");

        RootDto root;

        try
        {
            root = JsonUtility.FromJson<RootDto>(json.TrimStart('\uFeFF'));
        }
        catch(ArgumentException exception)
        {
            throw new FormatException("상점 JSON 문법을 확인하세요.", exception);
        }
        if (root == null ||
            root.shop_items == null ||
            root.shop_items.Length == 0)
        {
            throw new FormatException(
                "shop_items 배열이 없거나 비어 있습니다.");
        }

        var result = new List<ShopItemData>();
        var ids = new HashSet<string>(StringComparer.Ordinal);

        foreach (ItemDto dto in root.shop_items)
        {
            if (dto == null ||
                string.IsNullOrWhiteSpace(dto.item_id) ||
                string.IsNullOrWhiteSpace(dto.name))
            {
                throw new FormatException("상품 ID와 이름을 확인하세요.");
            }

            if (!ids.Add(dto.item_id))
                throw new FormatException(
                    $"중복 상품 ID: {dto.item_id}");

            if (dto.price <= 0 || dto.purchase_limit < 0)
                throw new FormatException(
                    $"{dto.item_id}: 가격 또는 구매 제한 오류");

            ShopCategory category;

            switch (dto.item_type)
            {
                case "CONSUMABLE":
                    category = ShopCategory.Consumable;
                    break;

                case "PERMANENT":
                    category = ShopCategory.Prestige;
                    break;

                default:
                    throw new FormatException(
                        $"{dto.item_id}: 알 수 없는 item_type");
            }

            if (dto.effects == null || dto.effects.Length == 0)
                throw new FormatException(
                    $"{dto.item_id}: effects가 비어 있습니다.");

            var effects = new List<ShopEffectData>();
            var effectKeys = new HashSet<string>();

            foreach (EffectDto raw in dto.effects)
            {
                if (raw == null)
                    throw new FormatException(
                        $"{dto.item_id}: 비어 있는 효과입니다.");

                if (!Enum.TryParse(raw.type, out ShopEffectType type) ||
                    !Enum.IsDefined(typeof(ShopEffectType), type) ||
                    !Enum.TryParse(raw.trigger, out ShopEffectTrigger trigger) ||
                    !Enum.IsDefined(typeof(ShopEffectTrigger), trigger))
                {
                    throw new FormatException(
                        $"{dto.item_id}: 알 수 없는 효과 또는 실행 시점");
                }

                if (double.IsNaN(raw.value) ||
                    double.IsInfinity(raw.value))
                {
                    throw new FormatException(
                        $"{dto.item_id}: 효과 수치가 잘못되었습니다.");
                }

                decimal value;

                try
                {
                    value = (decimal)raw.value;
                }
                catch (OverflowException exception)
                {
                    throw new FormatException(
                        $"{dto.item_id}: 효과 수치 범위 초과", exception);
                }

                ValidateEffect(dto.item_id, category, type, trigger, value);

                string key = $"{type}:{trigger}";

                if (!effectKeys.Add(key))
                    throw new FormatException(
                        $"{dto.item_id}: 중복 효과 {key}");

                effects.Add(new ShopEffectData(type, value, trigger));
            }

            result.Add(new ShopItemData(
                dto.item_id,
                dto.name,
                category,
                dto.price,
                dto.purchase_limit,
                dto.description ?? "",
                effects));
        }

        return result;
    }

    private static void ValidateEffect(
        string id,
        ShopCategory category,
        ShopEffectType type,
        ShopEffectTrigger trigger,
        decimal value)
    {
        bool supported = false;

        switch (type)
        {
            case ShopEffectType.FATIGUE:
                supported =
                    trigger == ShopEffectTrigger.IMMEDIATE ||
                    trigger == ShopEffectTrigger.PER_TURN;

                // 현재 상점 상품은 피로도 회복 효과만 지원.
                supported &= value <= 0;
                break;

            case ShopEffectType.AP:
                supported =
                    trigger == ShopEffectTrigger.IMMEDIATE &&
                    IsInteger(value);
                break;

            case ShopEffectType.FREE_LOW_GRADE_INFO:
                supported =
                    trigger == ShopEffectTrigger.PERMANENT &&
                    value >= 0 && IsInteger(value);
                break;

            case ShopEffectType.CAREER_EXP:
                supported =
                    trigger == ShopEffectTrigger.PER_TURN &&
                    value >= 0 && IsInteger(value);
                break;

            case ShopEffectType.INVESTMENT_RETURN_BONUS:
                supported =
                    trigger == ShopEffectTrigger.PERMANENT &&
                    value >= 0;
                break;

            case ShopEffectType.MAX_AP:
                supported =
                    trigger == ShopEffectTrigger.ON_PURCHASE &&
                    value >= 0 && IsInteger(value);
                break;

            case ShopEffectType.MAX_FATIGUE:
                supported =
                    trigger == ShopEffectTrigger.ON_PURCHASE &&
                    value >= 0;
                break;

            case ShopEffectType.HAPPY_ENDING_CONDITION:
                supported =
                    trigger == ShopEffectTrigger.PERMANENT &&
                    (value == 0 || value == 1);
                break;
        }

        if (category == ShopCategory.Consumable &&
            trigger != ShopEffectTrigger.IMMEDIATE)
        {
            supported = false;
        }

        if (!supported)
            throw new FormatException(
                $"{id}: 지원하지 않는 효과 조합 {type}/{trigger}/{value}");
    }

    private static bool IsInteger(decimal value)
    {
        return value >= int.MinValue &&
               value <= int.MaxValue &&
               value == decimal.Truncate(value);
    }
}
