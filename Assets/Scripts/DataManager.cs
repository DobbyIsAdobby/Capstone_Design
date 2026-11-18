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

    [Serializable]
    public sealed class SectorDialogueCsv
    {
        public MarketSector sector;
        public TextAsset csv;
    }

    [Header("Information Dialogue CSV")]
    [SerializeField] private SectorDialogueCsv[] sectorDialogueCsvs;

    private Dictionary<MarketSector, Dictionary<InformationDirection, IReadOnlyList<string>>>
        marketDialogues = new Dictionary<MarketSector, Dictionary<InformationDirection, IReadOnlyList<string>>>();

    [Header("Newspaper Data")]
    [SerializeField] private TextAsset newsJson;

    private NewsCatalogData newsCatalog;

    // 신문 선택용 난수
    private readonly System.Random newsRandom = new System.Random();

    private Dictionary<MarketSector, SectorNewsData> sectorNews = new Dictionary<MarketSector, SectorNewsData>();

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

    public int MarketTurnCount => IsMarketReady ? MarketModelConfig.TurnCount : 0;

    public bool IsInformationLoaded { get; private set; }

    public bool IsNewsLoaded { get; private set; }

    protected override void OnSingletonAwake()
    {
        //LoadAllData();
        // 시장(수익률) 생성은 시간이 걸리므로 Awake에서 실행하면 안됨.
        // 상점 데이터만 읽기.
        LoadShopData();
        LoadInformationData();

        // 신문 데이터 읽기.
        LoadNewsData();
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
            throw new InvalidOperationException("이미 시장이 등록되었습니다.");

        int expectedCount = MarketSectorUtility.SectorCount * MarketModelConfig.TurnCount;

        if (source == null || source.Count != expectedCount)
            throw new InvalidOperationException("4종목의 60턴 데이터가 필요합니다.");

        // 네 종목의 모든 턴 검사.
        for (int sectorIndex = 0; sectorIndex < MarketSectorUtility.SectorCount; sectorIndex++)
        {
            MarketSector sector = (MarketSector)sectorIndex;

            for (int turn = 1; turn <= MarketModelConfig.TurnCount; turn++)
            {
                int key = MarketSectorUtility.GetMarketKey(sector, turn);

                if (!source.TryGetValue(key, out MonthlyMarketRates rates) || !MarketReturnRules.IsValid(rates))
                {
                    throw new InvalidOperationException($"{sector} {turn}턴 시장 데이터가 올바르지 않습니다.");
                }
            }
        }

        // 외부 원본이 변경되어도 등록된 시장에는 영향이 없도록 복사
        generatedMarket = new ReadOnlyDictionary<int, MonthlyMarketRates>(new Dictionary<int, MonthlyMarketRates>(source));

        MarketSeed = seed;
        IsMarketReady = true;
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

        // 우선은 기술주만 조회
        return TryGetGeneratedMarketRates(MarketSector.Technology, turn, out rates);
    }

    /// <summary>
    /// 지정한 종목의 확정된 월 수익률 조회.
    /// </summary>
    public bool TryGetGeneratedMarketRates(MarketSector sector, int turn, out MonthlyMarketRates rates)
    {
        rates = default;

        if (!IsMarketReady || !MarketSectorUtility.IsValid(sector) || turn < 1 || turn > MarketModelConfig.TurnCount)
        {
            return false;
        }

        int key = MarketSectorUtility.GetMarketKey(sector, turn);

        return generatedMarket.TryGetValue(key, out rates);
    }

    /// <summary>
    /// 전체 시장 데이터가 필요한 시스템에 읽기 전용으로 제공함.
    /// 차트 UI 적용 시에는 미래 턴까지 표시하지 않도록 별도로 제한이 필요함.
    /// </summary>
    /// <param name="table"></param>
    /// <returns></returns>
    public bool TryGetGeneratedMarketTable(out IReadOnlyDictionary<int, MonthlyMarketRates> table)
    {
        table = null;

        if (!IsMarketReady)
            return false;

        var copy = new Dictionary<int, MonthlyMarketRates>();

        for (int turn = 1; turn <= MarketModelConfig.TurnCount; turn++)
        {
            int key = MarketSectorUtility.GetMarketKey(MarketSector.Technology, turn);

            copy.Add(turn, generatedMarket[key]);
        }

        table = new ReadOnlyDictionary<int, MonthlyMarketRates>(copy);
        return true;
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
    /// 정보 상품의 가격/정확도 설정과 대사 CSV가 모두 준비되어야 정보 구매가 가능함.
    /// </summary>
    private void LoadInformationData()
    {
        IsInformationLoaded = false;
        informationTable.Clear();
        marketDialogues.Clear();

        try
        {
            if (informationItemsJson == null)
                throw new FormatException("정보 상품 JSON을 연결하세요.");

            if (sectorDialogueCsvs == null || sectorDialogueCsvs.Length != MarketSectorUtility.SectorCount)
            {
                throw new FormatException("네 종목의 정보 CSV를 연결하세요.");
            }

            var items = InformationJsonParser.Parse(informationItemsJson.text);

            var dialogues = new Dictionary<MarketSector, Dictionary<InformationDirection, IReadOnlyList<string>>>();

            foreach (SectorDialogueCsv binding in sectorDialogueCsvs)
            {
                if (binding == null || !MarketSectorUtility.IsValid(binding.sector) || binding.csv == null || dialogues.ContainsKey(binding.sector))
                {
                    throw new FormatException("정보 CSV의 종목, 파일 또는 중복 연결을 확인하세요.");
                }

                // CSV Parser
                dialogues.Add(binding.sector, MarketDialogueCsvParser.Parse(binding.csv.text));
            }

            informationTable = items;
            marketDialogues = dialogues;
            IsInformationLoaded = true;
        }
        catch (Exception exception)
        {
            Debug.LogError($"정보 데이터 오류: {exception.Message}", this);
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

    /// <summary>
    /// 기초 주식시장의 상승/하락에 맞는 대사 후보 return
    /// </summary>
    /// <param name="sector"></param>
    /// <param name="marketDirection"></param>
    /// <param name="dialogues"></param>
    /// <returns></returns>
    public bool TryGetMarketDialogues(MarketSector sector, InformationDirection marketDirection, out IReadOnlyList<string> dialogues)
    {
        dialogues = null;

        return IsInformationLoaded && marketDialogues.TryGetValue(sector, out var byDirection) && byDirection.TryGetValue(marketDirection, out dialogues);
    }

    /// <summary>
    /// Inspector에 연결한 new.json을 읽어옴.
    /// 데이터 오류는 명시적으로 알림.
    /// </summary>
    private void LoadNewsData()
    {
        IsNewsLoaded = false;
        sectorNews.Clear();

        try
        {
            if (newsJson == null)
                throw new FormatException("News Json을 연결하세요.");

            NewsCatalogData loaded =
                JsonUtility.FromJson<NewsCatalogData>(newsJson.text);

            if (loaded == null || loaded.version != 2 || loaded.sectors == null || loaded.sectors.Length != MarketSectorUtility.SectorCount)
            {
                throw new FormatException("종목별 뉴스 JSON 구조를 확인하세요.");
            }

            var table = new Dictionary<MarketSector, SectorNewsData>();
            var ids = new HashSet<string>(StringComparer.Ordinal);

            foreach (SectorNewsData row in loaded.sectors)
            {
                if (row == null)
                    throw new FormatException("뉴스 종목 항목이 없습니다.");

                MarketSector sector = MarketSectorUtility.Parse(row.sector);

                if (table.ContainsKey(sector))
                    throw new FormatException("뉴스 종목이 중복되었습니다.");

                ValidateNewsPool(row.positive_news, ids);
                ValidateNewsPool(row.negative_news, ids);
                ValidateNewsPool(row.neutral_news, ids);

                table.Add(sector, row);
            }

            sectorNews = table;
            newsCatalog = loaded;
            IsNewsLoaded = true;
        }
        catch (Exception exception)
        {
            Debug.LogError($"신문 데이터 오류: {exception.Message}", this);
        }
    }

    /// <summary>
    /// 각 분류에 신문 데이터가 있고 필수 내용이 채워져 있는지 확인.
    /// </summary>
    /// <param name="pool"></param>
    /// <param name="ids"></param>
    /// <exception cref="FormatException"></exception>
    private static void ValidateNewsPool(NewsArticleData[] pool, HashSet<string> ids)
    {
        if (pool == null || pool.Length == 0)
            throw new FormatException("뉴스 후보 목록이 비어 있습니다.");

        foreach (NewsArticleData item in pool)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.id) || !ids.Add(item.id) || string.IsNullOrWhiteSpace(item.title) || string.IsNullOrWhiteSpace(item.text))
            {
                throw new FormatException("뉴스 ID, 제목, 본문을 확인하세요.");
            }
        }
    }

    /* -- 더 이상 사용하지 않음
    /// <summary>
    /// 각 분류에 신문 데이터가 있고 필수 내용이 채워져 있는지 확인. 
    /// 필수 내용 - id, title, text.
    /// </summary>
    /// <param name="articles"></param>
    /// <param name="categoryName"></param>
    /// <exception cref="InvalidOperationException"></exception>
    private static void ValidateNewsArticles(NewsArticleData[] articles, string categoryName)
    {
        if (articles == null || articles.Length == 0)
        {
            throw new InvalidOperationException($"{categoryName}에 기사가 없습니다.");
        }

        foreach (NewsArticleData article in articles)
        {
            if (article == null ||
                string.IsNullOrWhiteSpace(article.id) ||
                string.IsNullOrWhiteSpace(article.title) ||
                string.IsNullOrWhiteSpace(article.text))
            {
                throw new InvalidOperationException($"{categoryName}의 id, title, text를 확인하세요.");
            }
        }
    }
    */

    /// <summary>
    /// 지정한 턴의 시장 수익률로 기사 한 건을 선택.
    /// 기사는 반드시 수익률과 동일한 방향만 알려줌. 4개 중 1개
    /// </summary>
    /// <param name="turn"></param>
    /// <param name="article"></param>
    /// <returns></returns>
    public bool TryCreateMonthlyNews(int turn, out NewsArticleData article)
    {
        article = null;

        if (!IsNewsLoaded || !IsMarketReady || turn < 1 || turn > MarketModelConfig.TurnCount)
        {
            return false;
        }

        // 네 종목 중 하나를 같은 확률로 선택.
        MarketSector sector = (MarketSector)newsRandom.Next(MarketSectorUtility.SectorCount);

        if (!TryGetGeneratedMarketRates(sector, turn, out MonthlyMarketRates rates))
        {
            return false;
        }

        if (!sectorNews.TryGetValue(sector, out SectorNewsData news))
            return false;

        // 이벤트까지 반영된 최종 기본 수익률을 사용해서 진실 여부를 판단.
        NewsArticleData[] pool = rates.Stock > 0m ? news.positive_news : rates.Stock < 0m ? news.negative_news : news.neutral_news;

        NewsArticleData selected = pool[newsRandom.Next(pool.Length)];

        // 원본 기사 객체를 수정하지 않도록 복사.
        article = new NewsArticleData
        {
            id = selected.id,
            title = selected.title,
            text = selected.text
        };

        return true;
    }

    public MarketSaveData CaptureSave()
    {
        if (!IsMarketReady)
            throw new InvalidOperationException("시장이 준비되지 않았습니다.");

        var data = new MarketSaveData
        {
            seed = MarketSeed
        };

        for (int sectorIndex = 0; sectorIndex < MarketSectorUtility.SectorCount; sectorIndex++)
        {
            MarketSector sector = (MarketSector)sectorIndex;

            for (int turn = 1; turn <= MarketModelConfig.TurnCount; turn++)
            {
                int key = MarketSectorUtility.GetMarketKey(sector, turn);
                MonthlyMarketRates rates = generatedMarket[key];

                data.turns.Add(new MarketTurnSaveData
                {
                    sector = sector,
                    turn = turn,
                    stock = SaveNumber.Write(rates.Stock),
                    leverage = SaveNumber.Write(rates.Leverage),
                    stockInverse = SaveNumber.Write(rates.StockInverse),
                    leverageInverse = SaveNumber.Write(rates.LeverageInverse)
                });
            }
        }

        return data;
    }

    /// <summary>
    /// 저장된 시장을 검증하고 새로운 Dictionary로 변환.
    /// </summary>
    /// <param name="data"></param>
    /// <returns></returns>
    /// <exception cref="FormatException"></exception>
    public static Dictionary<int, MonthlyMarketRates> DecodeMarketSave(MarketSaveData data)
    {
        int expectedCount = MarketSectorUtility.SectorCount * MarketModelConfig.TurnCount;

        if (data == null || data.turns == null || data.turns.Count != expectedCount)
        {
            throw new FormatException("4종목의 60턴 시장 기록이 필요합니다.");
        }

        var restored = new Dictionary<int, MonthlyMarketRates>();

        foreach (MarketTurnSaveData row in data.turns)
        {
            if (row == null || !MarketSectorUtility.IsValid(row.sector) || row.turn < 1 ||
                row.turn > MarketModelConfig.TurnCount)
            {
                throw new FormatException("시장 종목 또는 턴이 잘못되었습니다.");
            }

            var rates = new MonthlyMarketRates(
                SaveNumber.Read(row.stock),
                SaveNumber.Read(row.leverage),
                SaveNumber.Read(row.stockInverse),
                SaveNumber.Read(row.leverageInverse));

            if (!MarketReturnRules.IsValid(rates))
                throw new FormatException("저장된 상품 수익률이 잘못되었습니다.");

            int key = MarketSectorUtility.GetMarketKey(row.sector, row.turn);

            if (restored.ContainsKey(key))
                throw new FormatException("같은 종목과 턴이 중복되었습니다.");

            restored.Add(key, rates);
        }

        // 유효한 키는 240개. 위에서 개수, 범위, 중복을 검사했음.
        // 따라서 여기까지 통과하면 모든 종목의 60턴이 존재하는거임
        return restored;
    }

    /// <summary>
    /// 저장된 시장 수익률을 복원.
    /// 
    /// ONNX를 다시 실행하지 않고 저장된 60턴 결과를 사용.
    /// 게임 중 불러오기와 복원 실패 시 되돌리기에도 사용.
    /// </summary>
    /// <param name="data"></param>
    /// <exception cref="FormatException"></exception>
    public void RestoreSave(MarketSaveData data)
    {
        // 복원 데이터를 먼저 검증.
        // 실패했을 때 현재 시장을 지우지 않음.
        Dictionary<int, MonthlyMarketRates> restored = DecodeMarketSave(data);

        generatedMarket = new ReadOnlyDictionary<int, MonthlyMarketRates>(restored);

        MarketSeed = data.seed;
        IsMarketReady = true;
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
