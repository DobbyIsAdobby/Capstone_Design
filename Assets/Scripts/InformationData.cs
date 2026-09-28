/// <summary>
/// Inspector에서 지정할 정보 등급. 
/// 가격과 정확도는 JSON에서 가져옴.
/// </summary>
public enum InformationGrade
{
    Low,
    Mid,
    High,
}

/// <summary>
/// 유저에게 전달하는 방향. 
/// 정답|오답 여부는 UI에서 공개하지 않음.
/// </summary>
public enum InformationDirection
{
    Down = -1,  // 수익률 하락
    Flat = 0,   // 수익률 유지
    Up = 1,     // 수익률 상승
}

/// <summary>
/// JSON에서 읽은 정보 상품의 고정 설정
/// </summary>
public sealed class InformationItemData
{
    // 자동 생성 프로퍼티 - { get; } 사용
    public string Id { get; }
    public string Name { get; }
    public InformationGrade Grade { get; }
    public long Price { get; }
    public double Accuracy { get; }
    public int LookaheadTurns { get; }  //턴 수 확인 - 마지막 턴은 정보 생성이 안되기 때문
    public string FreeWithItem { get; } //휴대폰 보유 여부 확인 - 휴대폰 보유시 하급 정보는 무료로 공개되기 때문

    /// <summary>
    /// 생성자
    /// </summary>
    /// <param name="id"></param>
    /// <param name="name"></param>
    /// <param name="grade"></param>
    /// <param name="price"></param>
    /// <param name="accuracy"></param>
    /// <param name="lookaheadTurns"></param>
    /// <param name="freeWithItem"></param>
    public InformationItemData(string id, string name, InformationGrade grade, long price, double accuracy, int lookaheadTurns, string freeWithItem)
    {
        Id = id;
        Name = name;
        Grade = grade;
        Price = price;
        Accuracy = accuracy;
        LookaheadTurns = lookaheadTurns;
        FreeWithItem = freeWithItem;
    }

    /// <summary>
    /// 이번 턴에 공개된 정보. 
    /// 패널을 다시 열 때 이 결과를 그대로 표시함.
    /// </summary>
    public sealed class InformationReveal
    {
        // 자동 생성 프로퍼티 - { get; } 사용
        public int TargetTurn { get; }
        public AssetType Asset { get; }
        public InformationDirection Direction { get; }
        public string Body { get; }     // 각 정보에 들어갈 대화 스크립트
        public bool IsFree { get; }     // 휴대폰 보유 여부 - 보유 시 무료

        /// <summary>
        /// 생성자
        /// </summary>
        /// <param name="targetTurn"></param>
        /// <param name="asset"></param>
        /// <param name="direction"></param>
        /// <param name="body"></param>
        /// <param name="isFree"></param>
        public InformationReveal(int targetTurn, AssetType asset, InformationDirection direction, string body, bool isFree)
        {
            TargetTurn = targetTurn;
            Asset = asset;
            Direction = direction;
            Body = body;
            IsFree = isFree;
        }
    }
}
