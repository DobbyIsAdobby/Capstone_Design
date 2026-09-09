using System;
using System.Collections.Generic;
using System.Collections.ObjectModel; // 제너릭 읽기 전용 컬렉션(ReadOnlyCollections)을 사용하여 다른 Manager 스크립트에서 읽기만 가능하게끔 딕셔너리를 제공
using UnityEngine;

// csv 데이터를 담을 구조체 선언 => MarketCSVParser.cs로 구조체 및 parser를 이관함.
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
    [Header("Market Data")]
    [SerializeField] private TextAsset nasdaqCSV;
    [Tooltip("현재는 미연결 허용. 연결 시 주식과 동일한 턴/월 구간(횟수)여야만 함.")]
    [SerializeField] private TextAsset leverageCSV;

    // 각 테이블은 턴(int)을 키값으로 보관, 외부에는 읽기 전용 뷰만 제공(제너릭 컬렉션 사용)
    private Dictionary<MarketType, IReadOnlyDictionary<int, MarketData>> marketTables = new Dictionary<MarketType, IReadOnlyDictionary<int, MarketData>>();

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
    public bool IsLoaded { get; private set; }
    public string LoadError { get; private set; } = "아직 초기화하지 않음.";

    protected override void OnSingletonAwake()
    {
        LoadAllData();
    }

    // 시작 시 한번 호출. 런타임 리로드는 현재 지원 X
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

    // AssetManager, RumorManager 차트 등이 필요한 테이블을 요청하는 함수
    public bool TryGetMarketTable(MarketType type, out IReadOnlyDictionary<int, MarketData> table)
    {
        table = null;
        return IsLoaded && marketTables.TryGetValue(type, out table);
    }

    // 한 행(row)만 필요할 때 사용하는 편의 조회 함수
    public bool TryGetMarketData(MarketType type, int turn, out MarketData data)
    {
        data = default;
        return TryGetMarketTable(type, out var table) && table.TryGetValue(turn, out data);
    }
}
