using System;
using System.Collections.Generic;

[Serializable]
public sealed class GameSaveData
{
    // 저장 형식을 변경하면 버전을 올리고 이전 버전 처리 방침을 정함.
    public int formatVersion;
    // 데이터와 게임 규칙의 호환성을 구분하기 위한 버전
    public int contentVersion;
    // 저장한 UTC 날짜.
    public string savedAtUTC;

    // 슬롯 목록에 표시할 요약
    public SaveSummaryData summary;

    public PlayerSaveData player;
    public AssetSaveData assets;
    public MarketSaveData market;
    public ShopSaveData shop;
    public LoanSaveData loans;
    public JobSaveData job;
    public InformationSaveData information;
    public EventSaveData pendingEvent;
    public WealthTierSaveData wealthTier;
}

/// <summary>
/// 슬롯 목록에 표시할 요약(턴, 순자산, 직급, 재산 등급)
/// </summary>
[Serializable]
public sealed class SaveSummaryData
{
    public int turn;
    public long netWorth;
    public string jobGradeName;
    public string wealthTierName;
}

/// <summary>
/// 유저 데이터(현재 턴, 보유 현금, 피로도, 현재 AP, 야근 횟수, AP 지급 확인, 자산 증감 여부, 수입 지출)
/// </summary>
[Serializable]
public sealed class PlayerSaveData
{
    public int currentMonth;
    public long availableCash;
    public float stressLevel;
    public int currentAP;
    public int overtimeCount;

    // 월 중에 저장해도 AP를 다시 지급하지 않도록 보존함.
    public int lastAPResetTurn;

    // 이번 달 자산 증감 계산 기준
    public long monthStartTotalAsset;

    // 이번 달 이미 발생한 수입과 지출
    public List<ReceiptLineSaveData> monthlyLines = new List<ReceiptLineSaveData>();
}

/// <summary>
/// 월 말 청구서 내역(내역, 가격, 타입)
/// </summary>
[Serializable]
public sealed class ReceiptLineSaveData
{
    public string name;
    public long amount;
    public ReceiptLineType type;
}

/// <summary>
/// 각 자산별 내역(은행, 주식, 레버, 주식 인버스, 레버 인버스)
/// </summary>
[Serializable]
public sealed class AssetSaveData
{
    public long bankBalance;
    public long stockBalance;
    public long leverageBalance;
    public long stockInverseBalance;
    public long leverageInverseBalance;
}

/// <summary>
/// 수익률 자동 생성 ONNX 모델 내역(씨드, 생성된 60턴 결과)
/// </summary>
[Serializable]
public sealed class MarketSaveData
{
    public int seed;

    // 시드만 보관하지 않고 생성된 60턴 결과를 함께 저장해야함
    public List<MarketTurnSaveData> turns = new List<MarketTurnSaveData>();
}

/// <summary>
/// 매턴 수익률 내역(턴, 주식, 레버, 주식 인버스, 레버 인버스)
/// </summary>
[Serializable]
public sealed class MarketTurnSaveData
{
    public int turn;

    // decimal 수익률을 손실 없이 보존하기 위해 문자열을 사용
    public string stock;
    public string leverage;
    public string stockInverse;
    public string leverageInverse;
}

/// <summary>
/// 상점 내역(마지막 상환 턴, 마지막으로 실행한 해외여행 턴, 구매 횟수, 소유한 상품 목록)
/// </summary>
[Serializable]
public sealed class ShopSaveData
{
    public int lastSettlementTurn;
    public int lastOverseasTravelTurn;

    public List<PurchaseCountSaveData> purchaseCounts = new List<PurchaseCountSaveData>();

    public List<OwnedItemSaveData> ownedItems = new List<OwnedItemSaveData>();
}

/// <summary>
/// 구매횟수 내역(상품 ID, 횟수)
/// </summary>
[Serializable]
public sealed class PurchaseCountSaveData
{
    public string itemId;
    public int count;
}

/// <summary>
/// 소유한 상품 내역(상품 ID, 구매했던 턴, 월 유지비, 월 유지비 시작 턴, 남은 할부, 월 지불료, 남은 지불, 다음 지불 턴)
/// </summary>
[Serializable]
public sealed class OwnedItemSaveData
{
    // 상품 설명, 효과 데이터는 JSON에서 다시 조회
    public string itemId;
    public int purchaseTurn;

    // 계약 당시 결정된 납부 정보를 보존
    public long monthlyMaintenance;
    public int maintenanceStartTurn;

    public long remainingDebt;
    public long monthlyPayment;
    public int remainingPayments;
    public int nextPaymentTurn;
}

/// <summary>
/// 대출 내역(계약 ID, 마지막 상환 턴, 대출 계약 목록)
/// </summary>
[Serializable]
public sealed class LoanSaveData
{
    public int nextContractId;
    public int lastSettlementTurn;

    public List<LoanContractSaveData> contracts = new List<LoanContractSaveData>();
}

/// <summary>
/// 대출 계약 내역(계약 ID, 원금, 시작 턴, 기간, 연이율, 마지막 이자 납부 턴, 상환했는지 여부, 상환 턴)
/// </summary>
[Serializable]
public sealed class LoanContractSaveData
{
    public int id;
    public long principal;
    public int startTurn;
    public int duration;

    // 현재 LoanRules의 금리가 아니라 계약 당시 금리를 저장
    public string annualRate;

    public int lastInterestPaidTurn;
    public bool isClosed;
    public int closedTurn;
}

/// <summary>
/// 직급 내역(직급 Index, 현재 경험치, 마지막 월 경험치 지급 턴)
/// </summary>
[Serializable]
public sealed class JobSaveData
{
    public int gradeIndex;
    public int currentExperience;
    public int lastMonthlyExperienceTurn;
}

/// <summary>
/// 정보 내역(정보 턴, 정보 생성용 랜덤 난수 상태, 드러난 정보 목록, 사용한 대사 스크립트)
/// </summary>
[Serializable]
public sealed class InformationSaveData
{
    public int informationTurn;

    // 정보 생성용 난수의 진행 상태
    public long randomState;

    public List<InformationRevealSaveData> reveals = new List<InformationRevealSaveData>();

    // 이미 선택된 원문을 보존해 대사 중복 방지
    public List<string> usedTexts = new List<string>();
}

/// <summary>
/// 드러난 정보 내역(정보 등급, 해당 턴, 지정된 자산(주식/레버/주식|레버 인버스 중 하나), 지정된 방향성(참/거짓), 대사 스크립트, 무료인지 여부)
/// </summary>
[Serializable]
public sealed class InformationRevealSaveData
{
    public InformationGrade grade;
    public int targetTurn;
    public AssetType asset;
    public InformationDirection direction;
    public string body;
    public bool isFree;
}

/// <summary>
/// 이벤트 내역(내야하는 금액, 이벤트 명, 랜덤 상태)
/// </summary>
[Serializable]
public sealed class EventSaveData
{
    public long pendingPenaltyAmount;
    public string pendingEventName;

    // 추후 이벤트 추첨도 저장 전후 동일하게 이어지도록 보존
    public long randomState;
}

/// <summary>
/// 자산 등급 내역(현재 티어 Index, 적용 턴, 시작 시 순자산)
/// </summary>
[Serializable]
public sealed class WealthTierSaveData
{
    public int currentTierIndex;
    public int appliedTurn;
    public long netWorthAtTurnStart;
}
