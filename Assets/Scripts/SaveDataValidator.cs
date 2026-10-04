using System;
using System.Collections.Generic;

public class SaveDataValidator
{
    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new FormatException(message);
    }

    private static bool ValidRandom(long value)
    {
        return value > 0 && value <= uint.MaxValue;
    }

    private static bool PastTurn(int value, int current)
    {
        // 아직 정산하지 않은 초기 상태는 -1
        return value == -1 || (value >= 1 && value < current);
    }

    public static void Validate(GameSaveData data)
    {
        Require(data != null, "저장 데이터가 없습니다.");

        Require(data.formatVersion == GameSaveCoordinator.FormatVersion, "지원하지 않는 저장 형식입니다.");

        Require(data.contentVersion == GameSaveCoordinator.ContentVersion, "현재 게임 규칙과 호환되지 않는 저장 파일입니다.");

        Require(DateTimeOffset.TryParse(data.savedAtUTC, out _), "저장 날짜가 올바르지 않습니다.");

        Require(
            data.summary != null &&
            data.player != null &&
            data.assets != null &&
            data.market != null &&
            data.shop != null &&
            data.loans != null &&
            data.job != null &&
            data.information != null &&
            data.pendingEvent != null &&
            data.wealthTier != null,
            "필수 저장 항목이 누락되었습니다.");

        PlayerSaveData player = data.player;
        int turn = player.currentMonth;

        Require(turn >= 1 && turn <= 60, "턴 범위가 올바르지 않습니다.");
        Require(player.availableCash >= 0, "저장된 현금이 올바르지 않습니다.");
        Require(player.currentAP >= 0, "저장된 AP가 올바르지 않습니다.");

        Require(
            !float.IsNaN(player.stressLevel) &&
            !float.IsInfinity(player.stressLevel) &&
            player.stressLevel >= 0,
            "저장된 피로도가 올바르지 않습니다.");

        Require(player.overtimeCount >= 0 && player.overtimeCount <= 30, "야근 횟수가 올바르지 않습니다.");

        Require(player.lastAPResetTurn == turn, "AP 지급 턴이 일치하지 않습니다.");

        Require(player.monthlyLines != null, "청구서 기록이 없습니다.");

        foreach (ReceiptLineSaveData line in player.monthlyLines)
        {
            Require(
                line != null &&
                !string.IsNullOrWhiteSpace(line.name) &&
                Enum.IsDefined(typeof(ReceiptLineType), line.type),
                "청구서 내역이 올바르지 않습니다.");
        }

        AssetSaveData assets = data.assets;

        Require(
            assets.bankBalance >= 0 &&
            assets.stockBalance >= 0 &&
            assets.leverageBalance >= 0 &&
            assets.stockInverseBalance >= 0 &&
            assets.leverageInverseBalance >= 0,
            "자산 잔액이 올바르지 않습니다.");

        ValidateMarket(data.market);
        ValidateShop(data.shop, turn);
        long debt = ValidateLoans(data.loans, turn);

        Require(
            data.job.gradeIndex >= 0 &&
            data.job.gradeIndex < 10 &&
            data.job.currentExperience >= 0 &&
            PastTurn(data.job.lastMonthlyExperienceTurn, turn),
            "직급 기록이 올바르지 않습니다.");

        ValidateInformation(data.information, turn);

        Require(
            data.pendingEvent.pendingPenaltyAmount >= 0 &&
            ValidRandom(data.pendingEvent.randomState),
            "이벤트 기록이 올바르지 않습니다.");

        if (data.pendingEvent.pendingPenaltyAmount > 0)
        {
            Require(
                !string.IsNullOrWhiteSpace(
                    data.pendingEvent.pendingEventName),
                "납부 예정 이벤트 이름이 없습니다.");
        }

        Require(
            data.wealthTier.currentTierIndex >= 0 &&
            data.wealthTier.currentTierIndex < 5 &&
            data.wealthTier.appliedTurn == turn,
            "재산 등급 기록이 올바르지 않습니다.");

        // 요약과 실제 수치가 서로 다른 파일을 거부
        long netWorth = checked(
            player.availableCash +
            assets.bankBalance +
            assets.stockBalance +
            assets.leverageBalance +
            assets.stockInverseBalance +
            assets.leverageInverseBalance -
            debt);

        Require(
            data.summary.turn == turn &&
            data.summary.netWorth == netWorth,
            "슬롯 요약과 실제 저장 데이터가 일치하지 않습니다.");
    }

    private static void ValidateMarket(MarketSaveData market)
    {
        Require(market.turns != null && market.turns.Count == 60, "60턴 시장 데이터가 필요합니다.");

        var turns = new HashSet<int>();

        foreach (MarketTurnSaveData row in market.turns)
        {
            Require(
                row != null &&
                row.turn >= 1 &&
                row.turn <= 60 &&
                turns.Add(row.turn),
                "시장 턴이 중복되거나 범위를 벗어났습니다.");

            var rates = new MonthlyMarketRates(
                SaveNumber.Read(row.stock),
                SaveNumber.Read(row.leverage),
                SaveNumber.Read(row.stockInverse),
                SaveNumber.Read(row.leverageInverse));

            Require(MarketReturnRules.IsValid(rates), "저장된 시장 수익률이 규칙과 일치하지 않습니다.");
        }
    }

    private static void ValidateShop(ShopSaveData shop, int turn)
    {
        Require(shop.purchaseCounts != null && shop.ownedItems != null, "상점 기록이 누락되었습니다.");

        Require(PastTurn(shop.lastSettlementTurn, turn), "상점 정산 턴이 올바르지 않습니다.");

        Require(
            shop.lastOverseasTravelTurn == -1 ||
            (shop.lastOverseasTravelTurn >= 1 &&
             shop.lastOverseasTravelTurn <= turn),
            "해외여행 이용 턴이 올바르지 않습니다.");

        var counts = new Dictionary<string, int>();

        foreach (PurchaseCountSaveData item in shop.purchaseCounts)
        {
            Require(
                item != null &&
                !string.IsNullOrWhiteSpace(item.itemId) &&
                item.count > 0 &&
                !counts.ContainsKey(item.itemId),
                "상품 구매 횟수가 올바르지 않습니다.");

            counts.Add(item.itemId, item.count);
        }

        var ownedIds = new HashSet<string>();

        foreach (OwnedItemSaveData item in shop.ownedItems)
        {
            Require(
                item != null &&
                !string.IsNullOrWhiteSpace(item.itemId) &&
                ownedIds.Add(item.itemId) &&
                counts.ContainsKey(item.itemId),
                "보유 상품 기록이 올바르지 않습니다.");

            Require(
                item.purchaseTurn >= 1 &&
                item.purchaseTurn <= turn &&
                item.maintenanceStartTurn == item.purchaseTurn + 1 &&
                item.monthlyMaintenance >= 0 &&
                item.remainingDebt >= 0 &&
                item.monthlyPayment >= 0 &&
                item.remainingPayments >= 0 &&
                item.remainingPayments <= 12,
                "상품 납부 기록이 올바르지 않습니다.");

            if (item.remainingPayments == 0)
            {
                Require(item.remainingDebt == 0, "완납 상품에 남은 할부금이 있습니다.");
            }
            else
            {
                Require(
                    item.monthlyPayment > 0 &&
                    item.nextPaymentTurn == turn &&
                    item.remainingDebt >= checked(
                        item.monthlyPayment * item.remainingPayments),
                    "남은 할부 일정이 올바르지 않습니다.");
            }
        }
    }

    private static long ValidateLoans(LoanSaveData loans, int turn)
    {
        Require(loans.contracts != null, "대출 목록이 없습니다.");

        Require(PastTurn(loans.lastSettlementTurn, turn), "대출 정산 턴이 올바르지 않습니다.");

        var ids = new HashSet<int>();
        int maximumId = 0;
        long debt = 0;

        foreach (LoanContractSaveData loan in loans.contracts)
        {
            Require(
                loan != null &&
                loan.id > 0 &&
                ids.Add(loan.id) &&
                loan.principal > 0 &&
                loan.startTurn >= 1 &&
                loan.startTurn <= turn &&
                loan.duration >= 1 &&
                loan.duration <= 12 &&
                loan.startTurn + loan.duration <= 60,
                "대출 계약이 올바르지 않습니다.");

            decimal rate = SaveNumber.Read(loan.annualRate);
            Require(rate >= 0 && rate <= 1, "대출 금리가 올바르지 않습니다.");

            Require(
                loan.lastInterestPaidTurn >= loan.startTurn - 1 &&
                loan.lastInterestPaidTurn <= turn,
                "대출 이자 납부 턴이 올바르지 않습니다.");

            if (loan.isClosed)
            {
                Require(
                    loan.closedTurn >= loan.startTurn &&
                    loan.closedTurn <= turn &&
                    loan.lastInterestPaidTurn == loan.closedTurn,
                    "종료된 대출 계약이 올바르지 않습니다.");
            }
            else
            {
                Require(
                    loan.closedTurn == -1 &&
                    loan.startTurn + loan.duration >= turn &&
                    loan.lastInterestPaidTurn < turn,
                    "진행 중 대출 계약이 올바르지 않습니다.");

                debt = checked(debt + loan.principal);
            }

            maximumId = Math.Max(maximumId, loan.id);
        }

        Require(
            loans.nextContractId > maximumId,
            "다음 대출 계약 번호가 올바르지 않습니다.");

        return debt;
    }

    private static void ValidateInformation(
        InformationSaveData information, int turn)
    {
        Require(
            information.informationTurn == turn &&
            ValidRandom(information.randomState) &&
            information.reveals != null &&
            information.usedTexts != null,
            "정보 시스템 기록이 올바르지 않습니다.");

        var grades = new HashSet<InformationGrade>();

        foreach (InformationRevealSaveData reveal in information.reveals)
        {
            Require(
                reveal != null &&
                Enum.IsDefined(typeof(InformationGrade), reveal.grade) &&
                grades.Add(reveal.grade) &&
                reveal.targetTurn == turn + 1 &&
                reveal.targetTurn <= 60 &&
                Enum.IsDefined(typeof(InformationDirection), reveal.direction) &&
                !string.IsNullOrWhiteSpace(reveal.body),
                "공개 정보 기록이 올바르지 않습니다.");

            Require(
                reveal.asset == AssetType.Stock ||
                reveal.asset == AssetType.Leverage ||
                reveal.asset == AssetType.StockInverse ||
                reveal.asset == AssetType.LeverageInverse,
                "정보 대상 자산이 올바르지 않습니다.");

            Require(!reveal.isFree || reveal.grade == InformationGrade.Low, "무료 정보 등급이 올바르지 않습니다.");
        }

        var texts = new HashSet<string>(StringComparer.Ordinal);

        foreach (string text in information.usedTexts)
        {
            Require(!string.IsNullOrWhiteSpace(text) && texts.Add(text), "사용한 대사 기록이 올바르지 않습니다.");
        }
    }
}
