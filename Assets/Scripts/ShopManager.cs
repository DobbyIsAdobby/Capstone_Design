using System;
using System.Collections.Generic;
using UnityEngine;

//소비 항목 분류 -> 추후 더 세부적(일회성/과시성 소비)으로 구분지을 계획 - 더이상 사용하지 않음.
/*public enum ShopItemType
{
    DeliveryFood,
    DrinkingParty,
    Hocance,
    LuxuryWatch
}*/

public class ShopManager : Singleton<ShopManager>
{
    [Serializable]
    private sealed class PaymentRule
    {
        [SerializeField] private string itemId;             // 아이템 ID
        [SerializeField] private bool allowInstallment;     // 제한 수량
        [SerializeField] private long monthlyMaintenance;   // 월 유지비

        public string ItemId => itemId;
        public bool AllowInstallment => allowInstallment;
        public long MonthlyMaintenance => monthlyMaintenance;

        /// <summary>
        /// 생성자
        /// </summary>
        /// <param name="id"></param>
        /// <param name="installment"></param>
        /// <param name="maintenance"></param>
        public PaymentRule(string id, bool installment, long maintenance)
        {
            itemId = id;
            allowInstallment = installment;
            monthlyMaintenance = maintenance;
        }
    }

    /// <summary>
    /// 소유 중인 아이템 함수
    /// </summary>
    private sealed class OwnedItem
    {
        public ShopItemData Data;
        public int PurchaseTurn;

        public long MonthlyMaintenance;
        public int MaintenanceStartTurn;

        public long RemainingDebt;
        public long MonthlyPayment;
        public int RemainingPayments;
        public int NextPaymentTurn;
    }

    [Header("Installment")]
    [SerializeField, Range(1,12)]
    private int maxInstallmentMonths = 12;

    [Header("paymentRules")]
    [SerializeField]
    private List<PaymentRule> paymentRules = new List<PaymentRule>
    {
        new PaymentRule("SHOP_006", true, 20000),
        new PaymentRule("SHOP_007", true, 50000),
        new PaymentRule("SHOP_008", true, 10000),
        new PaymentRule("SHOP_009", true, 1000000),
        new PaymentRule("SHOP_010", false, 2000000)
    };

    private readonly List<OwnedItem> ownedItems =
        new List<OwnedItem>();

    private readonly Dictionary<string, int> purchaseCounts =
        new Dictionary<string, int>(StringComparer.Ordinal);

    private int lastSettlementTurn = -1;
    private int rumorUsageTurn = -1;
    private int usedFreeRumors;

    public int GetPurchaseCount(string itemId)
    {
        return purchaseCounts.TryGetValue(itemId, out int count) ? count : 0;
    }

    public bool IsOwned(string itemId)
    {
        return GetPurchaseCount(itemId) > 0;
    }

    public bool HasReachedPurchaseLimit(ShopItemData item)
    {
        return item.PurchaseLimit > 0 &&
               GetPurchaseCount(item.Id) >= item.PurchaseLimit;
    }

    // 일회성 소비는 별도 결제 규칙 없이 일시불 / 유지비 0.
    public bool TryGetPaymentSettings(
        ShopItemData item,
        out bool allowInstallment,
        out long monthlyMaintenance)
    {
        allowInstallment = false;
        monthlyMaintenance = 0;

        if (item.Category == ShopCategory.Consumable)
            return true;

        PaymentRule found = null;

        foreach (PaymentRule rule in paymentRules)
        {
            if (rule == null || rule.ItemId != item.Id)
                continue;

            // 같은 상품 규칙을 중복 등록한 경우 거부.
            if (found != null)
                return false;

            found = rule;
        }

        if (found == null || found.MonthlyMaintenance < 0)
            return false;

        // 집은 일시불로만 구매가능.
        allowInstallment =
            found.AllowInstallment && !item.RequiredForHappyEnding;

        monthlyMaintenance = found.MonthlyMaintenance;
        return true;
    }

    public int GetMaxInstallmentMonths(ShopItemData item)
    {
        if (!TryGetPaymentSettings(item, out bool allow, out _) || !allow)
            return 0;

        return (int)Math.Min(
            Mathf.Clamp(maxInstallmentMonths, 1, 12), item.Price);
    }

    public long GetMonthlyPayment(ShopItemData item, int months)
    {
        return months > 0 ? item.Price / months : item.Price;
    }

    public long GetLastPayment(ShopItemData item, int months)
    {
        return months > 0
            ? item.Price / months + item.Price % months
            : item.Price;
    }

    public bool TryBuy(string itemId, int months, out string message)
    {
        GameManager game = GameManager.Instance;
        DataManager data = DataManager.Instance;

        if (game == null || !game.CanAct)
        {
            message = "지금은 구매할 수 없습니다.";
            return false;
        }

        if (data == null ||
            !data.TryGetShopItem(itemId, out ShopItemData item))
        {
            message = "상품 데이터를 찾을 수 없습니다.";
            return false;
        }

        if (HasReachedPurchaseLimit(item))
        {
            message = "구매 가능한 횟수를 모두 사용했습니다.";
            return false;
        }

        if (!TryGetPaymentSettings(
            item, out _, out long monthlyMaintenance))
        {
            message = "상품의 할부·유지비 설정을 확인하세요.";
            return false;
        }

        if (months < 0 || months > GetMaxInstallmentMonths(item))
        {
            message = "선택할 수 없는 할부 기간입니다.";
            return false;
        }

        long immediateCost = months == 0 ? item.Price : 0;

        if (game.availableCash < immediateCost)
        {
            message =
                $"현금이 {immediateCost - game.availableCash:N0}원 부족합니다.";
            return false;
        }

        // 모든 구매 조건을 확인한 다음 실제 상태 변경.
        if (immediateCost > 0)
        {
            game.ApplyCashChange(
                -immediateCost, $"상점: {item.DisplayName}");
        }

        purchaseCounts[item.Id] = GetPurchaseCount(item.Id) + 1;

        if (item.Category == ShopCategory.Prestige)
        {
            ownedItems.Add(new OwnedItem
            {
                Data = item,
                PurchaseTurn = game.currentMonth,

                MonthlyMaintenance = monthlyMaintenance,

                // 유지비는 다음 달부터.
                MaintenanceStartTurn = game.currentMonth + 1,

                RemainingDebt = months > 0 ? item.Price : 0,
                MonthlyPayment = months > 0 ? item.Price / months : 0,
                RemainingPayments = months,

                // 첫 할부금은 구매한 달 마감에 납부
                NextPaymentTurn = game.currentMonth
            });
        }

        // JSON의 FATIGUE는 음수이므로 그대로 더함.
        game.stressLevel =
            Mathf.Max(0, game.stressLevel + item.ImmediateFatigueChange);

        // AP는 추후 AP 시스템에서 적용 계획 - 현재 미구현 상태
        message = months == 0
            ? $"{item.DisplayName} 구매 완료."
            : $"{item.DisplayName} 구매 완료.\n이번 달부터 {months}회 납부합니다.";

        if (UIManager.Instance != null)
            UIManager.Instance.RefreshUI();

        return true;
    }

    public void ProcessMonthlySettlement(int turn, long marketProfit)
    {
        GameManager game = GameManager.Instance;

        if (game == null || game.IsGameOver ||
            !game.IsSetting || game.currentMonth != turn ||
            turn <= lastSettlementTurn)
            return;

        lastSettlementTurn = turn;

        decimal bonusRate = 0;
        float fatigueChange = 0;

        foreach (OwnedItem owned in ownedItems)
        {
            // 투자 보너스: 구매한 턴부터 적용
            if (turn >= owned.PurchaseTurn)
            {
                bonusRate += owned.Data.InvestmentBonusRate;
            }

            // 매월 피로도 회복: 기존 규칙인 구매 다음 턴부터 유지
            if (turn > owned.PurchaseTurn)
            {
                fatigueChange += owned.Data.MonthlyFatigueChange;
            }
        }

        // 기존 규칙: 주식+레버리지 합산 평가이익이 양수일 때 현금 보너스. - 이를 자산 각각으로 나눌지, 통합으로 할지는 아직 결정안됨.
        if (marketProfit > 0 && bonusRate > 0)
        {
            long bonus = (long)(marketProfit * bonusRate);

            if (bonus > 0)
                game.ApplyCashChange(bonus, "보유 상품 투자 보너스");
        }

        game.stressLevel = Mathf.Max(0, game.stressLevel + fatigueChange);

        foreach (OwnedItem owned in ownedItems)
        {
            if (owned.RemainingPayments > 0 &&
                turn >= owned.NextPaymentTurn)
            {
                long payment = owned.RemainingPayments == 1
                    ? owned.RemainingDebt
                    : owned.MonthlyPayment;

                game.ApplyMandatoryExpense(
                    payment,
                    $"{owned.Data.DisplayName} 할부금",
                    ReceiptLineType.FixedExpense);

                owned.RemainingDebt -= payment;
                owned.RemainingPayments--;
                owned.NextPaymentTurn++;
            }

            if (turn >= owned.MaintenanceStartTurn &&
                owned.MonthlyMaintenance > 0)
            {
                game.ApplyMandatoryExpense(
                    owned.MonthlyMaintenance,
                    $"{owned.Data.DisplayName} 유지비",
                    ReceiptLineType.FixedExpense);
            }
        }
    }

    /* - 더 이상 사용하지 않음.
    private bool TryPay(GameManager game, long amount, string label)
    {
        if (game.availableCash < amount)
        {
            game.TriggerBankruptcy(
                $"{label} 납부 불가\n필요: {amount:N0}원\n" +
                $"보유 현금: {game.availableCash:N0}원");
            return false;
        }

        game.ApplyCashChange(
            -amount, label, ReceiptLineType.FixedExpense);

        return true;
    }
    */

    // 구매할 때 수치를 누적해서 변경하지 않고 보유 상품에서 계산.
    // 패널을 다시 열어도 최대치가 중복 증가하지 않음.
    public float MaxStressBonus
    {
        get
        {
            float result = 0;
            foreach (OwnedItem owned in ownedItems)
                result += owned.Data.MaxStressBonus;
            return result;
        }
    }

    public int MaxAPBonus
    {
        get
        {
            int result = 0;
            foreach (OwnedItem owned in ownedItems)
                result += owned.Data.MaxAPBonus;
            return result;
        }
    }

    // 직급 시스템에서 월 경험치를 지급할 때 사용할 함수.
    public int GetMonthlyJobExperience(int turn)
    {
        int result = 0;

        foreach (OwnedItem owned in ownedItems)
        {
            if (turn > owned.PurchaseTurn)
                result += owned.Data.MonthlyJobExperience;
        }

        return result;
    }

    // 휴대폰 보유 시 매월 하급 정보 이용권.
    // 정보 시스템 구현 시 실제 무료 정보 제공 흐름에서 호출.
    public bool TryUseFreeLowRumor()
    {
        GameManager game = GameManager.Instance;

        if (game == null || !game.CanAct)
            return false;

        if (rumorUsageTurn != game.currentMonth)
        {
            rumorUsageTurn = game.currentMonth;
            usedFreeRumors = 0;
        }

        int limit = 0;

        foreach (OwnedItem owned in ownedItems)
        {
            if (game.currentMonth > owned.PurchaseTurn)
                limit += owned.Data.MonthlyFreeLowRumors;
        }

        if (usedFreeRumors >= limit)
            return false;

        usedFreeRumors++;
        return true;
    }

    public bool HasHappyEndingItem
    {
        get
        {
            foreach (OwnedItem owned in ownedItems)
            {
                if (owned.Data.RequiredForHappyEnding)
                    return true;
            }

            return false;
        }
    }
    // 더이상 사용하지 않음.
    /*
    Inspector Zone
    */
    //public static ShopManager Instance;

    /*
    function Zone
    */

    //싱글톤 패턴
    /*private void Awake()
    {
        if(Instance == null) Instance = this;
        else Destroy(gameObject);
    }*/

    /// <summary>
    /// UI [구매하기] 버튼을 통해 아이템을 소비할 때 호출할 함수
    /// </summary>
    /// <param name="itemType"></param>
    /*public void BuyItem(ShopItemType itemType)
    {
        if(!GameManager.Instance.CanAct) return;

        long cost = 0;
        float stressRelief = 0f;
        string itemName = "";

        // 아이템별 가격 및 스트레스 감소량 세팅 (프로토타입 밸런싱용)
        switch (itemType)
        {
            case ShopItemType.DeliveryFood:
                itemName = "배달 음식";
                cost = 30000;      // 3만 원
                stressRelief = 15f; // 스트레스 15% 감소
                break;
            case ShopItemType.DrinkingParty:
                itemName = "친구들과 술자리";
                cost = 100000;     // 10만 원
                stressRelief = 40f; // 스트레스 40% 감소
                break;
            case ShopItemType.Hocance:
                itemName = "5성급 호캉스";
                cost = 500000;     // 50만 원
                stressRelief = 100f; // 스트레스 전액 탕감
                break;
            case ShopItemType.LuxuryWatch:
                itemName = "명품 시계";
                cost = 5000000;    // 500만 원
                stressRelief = 100f;
                // TODO: 추후 GameManager에 '초기 스트레스 증가량 감소' 같은 영구 버프 로직 추가 연동 -> 해당부분 구체적 기획 필요함.
                break;
        }

        // 1. 현금 확인
        if(GameManager.Instance.availableCash < cost)
        {
            Debug.LogWarning($"{itemName}을 결제하기엔 보유 현금이 부족합니다. (필요 금액 : {cost:N0}원)");
            return;
        }

        // 2. 결제 진행 (현금 차감)
        GameManager.Instance.ApplyCashChange(-cost, $"상점");

        // 3. 스트레스 감소 적용(0 밑으로 떨어지지 않도록 Mathf.Max 사용)
        GameManager.Instance.stressLevel = Mathf.Max(0, GameManager.Instance.stressLevel - stressRelief);

        Debug.Log($"[구매 완료] {itemName} 결제 (-{cost:N0}원). 현재 스트레스 : {GameManager.Instance.stressLevel}%");

        if(UIManager.Instance != null)
        {
            UIManager.Instance.RefreshUI();
        }

        // 추후 과시성 소비의 경우 할부 시스템을 구현해야함.
    }*/
}
