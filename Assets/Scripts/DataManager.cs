using System;
using System.Collections.Generic;
using System.Collections.ObjectModel; // 제너릭 읽기 전용 컬렉션(ReadOnlyCollections)을 사용하여 다른 Manager 스크립트에서 읽기만 가능하게끔 딕셔너리를 제공
using UnityEngine;

// csv 데이터를 담을 구조체 선언 => MarketCSVParser.cs 및 MarketData.cs로 parser 및 구조체를 이관함.
// 향후 다른 데이터(상점/정보)의 경우에도 위와 같이 CSVParser와 Data로 각각의 파서와 구조체를 분리, DataManager에서 메인으로 관리만 하도록 설계할 계획.
/*public struct MarketData
{
    public string date;      // 일자
    public float returnRate; // 수익률
    
    // 추후 bankRate, leverageRate 등을 추가할 예정
    
}*/

public class DataManager : Singleton<DataManager>
{
    /*
    Inspector Zone
    */
    //public static DataManager Instance;

    [Header("Shop Data")]
    [SerializeField] private TextAsset shopItemsJson;

    private readonly Dictionary<string, ShopItemData> shopTable = new Dictionary<string, ShopItemData>(StringComparer.Ordinal);
    public IReadOnlyList<ShopItemData> ShopItems { get; private set; } = Array.Empty<ShopItemData>();

    [Header("Information Data")]
    [SerializeField] private TextAsset informationItemsJson;

    private Dictionary<InformationGrade, InformationItemData> informationTable = new Dictionary<InformationGrade, InformationItemData>();

    // 수익률 자동 생성 ONNX 모델 적용으로 인해 더 이상 Market Data 인스펙터는 사용하지 않음
    /*
    [Header("Market Data")]
    [SerializeField] private TextAsset nasdaqCSV;
    [Tooltip("현재는 미연결 허용. 연결 시 주식과 동일한 턴/월 구간(횟수)여야만 함.")]
    [SerializeField] private TextAsset leverageCSV;

    // 각 테이블은 턴(int)을 키값으로 보관, 외부에는 읽기 전용 뷰만 제공(제너릭 컬렉션 사용)
    private Dictionary<MarketType, IReadOnlyDictionary<int, MarketData>> marketTables = new Dictionary<MarketType, IReadOnlyDictionary<int, MarketData>>();
    */


    /*
    function Zone
    */

    //싱글톤 패턴
    /*private void Awake()
    {
        if(Instance == null) Instance = this;
        else Destroy(gameObject);

        // 게임 시작 시 데이터를 메모리에 적재
        LoadCSVData();
    }*/

    // 연결한 선택 파일의 load 성공 여부
    // 모든 테이블의 존재를 의미하진 않음.
    public bool IsShopLoaded { get; private set; }

    //public bool IsLoaded { get; private set; }
    //public string LoadError { get; private set; } = "아직 초기화하지 않음.";

    // Key: 게임 턴 번호(1~60)
    // Value: 해당 턴의 네 투자상품 수익률
    private IReadOnlyDictionary<int, MonthlyMarketRates> generatedMarket;

    // 60턴 전체가 등록되기 전에는 false.
    public bool IsMarketReady { get; private set; }

    // 문제 재현 및 Load&Save 기능에서 참고할 시장 생성 시드.
    public int MarketSeed { get; private set; }

    public int MarketTurnCount => generatedMarket?.Count ?? 0;

    public bool IsInformationLoaded { get; private set; }

    protected override void OnSingletonAwake()
    {
        //LoadAllData();
        // 시장(수익률) 생성은 시간이 걸리므로 Awake에서 실행하면 안됨.
        // 상점 데이터만 읽기.
        LoadShopData();
        LoadInformationData();
    }

    /// <summary>
    /// 생성이 모두 끝난 시장을 검증한 뒤 한 번에 등록함.
    /// 불완전한 데이터는 공개하지 않음.
    /// </summary>
    /// <param name="source"></param>
    /// <param name="seed"></param>
    /// <exception cref="InvalidOperationException"></exception>
    public void RegisterGeneratedMarket(Dictionary<int, MonthlyMarketRates> source, int seed)
    {
        if (IsMarketReady)
        {
            throw new InvalidOperationException("이번 플레이의 시장은 이미 등록되었습니다.");
        }

        if (source == null || source.Count != MarketModelConfig.TurnCount)
        {
            throw new InvalidOperationException("60턴의 시장 데이터가 필요합니다.");
        }

        for (int turn = 1; turn <= MarketModelConfig.TurnCount; turn++)
        {
            if (!source.TryGetValue(turn, out MonthlyMarketRates rates))
            {
                throw new InvalidOperationException($"{turn}턴 데이터가 없습니다.");
            }

            // 현재 게임에서 사용하는 상품 간 수익률 관계를 검사.
            if (rates.Stock <= -1m || rates.Leverage != rates.Stock * 2m || rates.StockInverse != -rates.Stock || rates.LeverageInverse != -rates.Stock * 2m)
            {
                throw new InvalidOperationException($"{turn}턴 수익률 규칙이 올바르지 않습니다.");
            }
        }

        // 원본 Dictionary가 나중에 수정되어도 영향을 받지 않도록 복사
        var copy = new Dictionary<int, MonthlyMarketRates>(source);

        // 외부 Manager에는 수정할 수 없는 형태로 제공
        generatedMarket = new ReadOnlyDictionary<int, MonthlyMarketRates>(copy);

        MarketSeed = seed;

        // 데이터 등록이 완전히 끝난 시점에 준비 완료로 변경.
        IsMarketReady = true;

        Debug.Log($"수익률 자동 생성 ONNX 모델 적용 완료: {copy.Count}턴 / Seed={seed}");
    }

    /// <summary>
    /// AssetManager에서 이번 턴 수익률을 요청할 때 사용
    /// 조회할 때 새로 생성하지 않으므로 같은 턴은 항상 동일 값
    /// </summary>
    /// <param name="turn"></param>
    /// <param name="rates"></param>
    /// <returns></returns>
    public bool TryGetGeneratedMarketRates(int turn, out MonthlyMarketRates rates)
    {
        rates = default;

        return IsMarketReady && generatedMarket.TryGetValue(turn, out rates);
    }

    /// <summary>
    /// 전체 시장 데이터가 필요한 시스템에 읽기 전용으로 제공함.
    /// 차트 UI 적용 시에는 미래 턴까지 표시하지 않도록 별도로 제한이 필요함.
    /// </summary>
    /// <param name="table"></param>
    /// <returns></returns>
    public bool TryGetGeneratedMarketTable(out IReadOnlyDictionary<int, MonthlyMarketRates> table)
    {
        table = generatedMarket;
        return IsMarketReady;
    }

    /// <summary>
    /// 상점 JSON 로딩
    /// </summary>
    private void LoadShopData()
    {
        IsShopLoaded = false;
        shopTable.Clear();
        ShopItems = Array.Empty<ShopItemData>();

        try
        {
            if(shopItemsJson == null)
            {
                throw new FormatException("DataManager의 Shop Items Json을 연결하세요.");
            }
            
            List<ShopItemData> loaded = ShopJsonParser.Parse(shopItemsJson.text);

            foreach(ShopItemData item in loaded)
                shopTable.Add(item.Id, item);
            
            ShopItems = loaded.AsReadOnly();
            IsShopLoaded = true;

            Debug.Log($"상점 JSON : {loaded.Count}개 load Completed.", this);
        }
        catch(FormatException exception)
        {
            shopTable.Clear();
            Debug.LogError(exception.Message, this);
        }
    }

    /// <summary>
    /// 상점 아이템을 조회하는 함수
    /// </summary>
    /// <param name="id"></param>
    /// <param name="item"></param>
    /// <returns></returns>
    public bool TryGetShopItem(string id, out ShopItemData item)
    {
        item = null;

        return IsShopLoaded && !string.IsNullOrEmpty(id) && shopTable.TryGetValue(id, out item);
    }

    /// <summary>
    /// 정보 상품의 가격/정확도 설정을 읽어옴
    /// </summary>
    private void LoadInformationData()
    {
        IsInformationLoaded = false;
        informationTable.Clear();

        try
        {
            if (informationItemsJson == null)
                throw new FormatException("Information Items Json을 연결하세요.");

            // 검증이 끝난 테이블만 등록함
            informationTable = InformationJsonParser.Parse(informationItemsJson.text);

            IsInformationLoaded = true;
            Debug.Log("정보 JSON: 상/중/하급 로드 완료.", this);
        }
        catch (FormatException exception)
        {
            Debug.LogError(exception.Message, this);
        }
    }

    /// <summary>
    /// 정보를 조회하는 함수
    /// </summary>
    /// <param name="grade"></param>
    /// <param name="item"></param>
    /// <returns></returns>
    public bool TryGetInformation(InformationGrade grade, out InformationItemData item)
    {
        item = null;

        return IsInformationLoaded && informationTable.TryGetValue(grade, out item);
    }

    // 더 이상 사용하지 않음
    // 시작 시 한번 호출. 런타임 리로드는 현재 지원 X
    /*
    private void LoadAllData()
    {
        IsLoaded = false;
        marketTables.Clear();

        if(nasdaqCSV == null)
        {
            LoadError = "DataManager의 Nasdaq CSV에 CSV 파일이 연결되어있지 않음.";
            Debug.LogError(LoadError, this);
            return;
        }
        try
        {
            var loadedTables = new Dictionary<MarketType, IReadOnlyDictionary<int, MarketData>>();

            var nasdaq = LoadMarketTable(nasdaqCSV, MarketType.Nasdaq);
            loadedTables.Add(MarketType.Nasdaq, nasdaq);

            if(leverageCSV != null)
            {
                var leverage = LoadMarketTable(leverageCSV, MarketType.Leverage);
                ValidateSameTimeline(nasdaq, leverage);
                loadedTables.Add(MarketType.Leverage, leverage);
            }

            // 모든 파일 검증 완료 후 공개 처리
            marketTables = loadedTables;
            IsLoaded = true;
            LoadError = "";

            foreach(var entry in marketTables)
                Debug.Log($"{entry.Key}: {entry.Value.Count}개월(턴) load Completed.", this);
            
            if(leverageCSV == null)
                Debug.Log("Leverage CSV is not connected. only loaded NASDAQ Tables.", this);
        }
        catch(FormatException exception)
        {
            LoadError = exception.Message;
            Debug.LogError(LoadError, this);
        }
    }
    */

    /*
    private static IReadOnlyDictionary<int, MarketData> LoadMarketTable(TextAsset csv, MarketType type)
    {
        Dictionary<int, MarketData> parsed = MarketCSVParser.Parse(csv.text, $"{type} ({csv.name})");

        // 읽기 전용으로 제작
        return new ReadOnlyDictionary<int, MarketData>(parsed);
    }

    private static void ValidateSameTimeline(IReadOnlyDictionary<int, MarketData> nasdaq, IReadOnlyDictionary<int, MarketData> leverage)
    {
        if(nasdaq.Count != leverage.Count)
            throw new FormatException("NASDAQ & Leverage의 데이터 개월 수가 다릅니다.");

        foreach(var entry in nasdaq)
        {
            if(!leverage.TryGetValue(entry.Key, out MarketData other) || entry.Value.Month != other.Month)
                throw new FormatException($"{entry.Key}개월(턴)의 NASDAQ와 Leverage의 날짜가 다릅니다.");
        }
    }
    */

    // AssetManager, RumorManager 차트 등이 필요한 테이블을 요청하는 함수 -- ONNX 모델에 맞게 위에 같은 이름의 새로운 메서드로 제작함
    /*
    public bool TryGetMarketTable(MarketType type, out IReadOnlyDictionary<int, MarketData> table)
    {
        table = null;
        return IsLoaded && marketTables.TryGetValue(type, out table);
    }
    */

    // 한 행(row)만 필요할 때 사용하는 편의 조회 함수 -- 더 이상 사용하지 않음
    /*
    public bool TryGetMarketData(MarketType type, int turn, out MarketData data)
    {
        data = default;
        return TryGetMarketTable(type, out var table) && table.TryGetValue(turn, out data);
    }
    */
}
