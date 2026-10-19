using System;
using System.Collections.Generic;
using UnityEngine;

public class InflationManager : Singleton<InflationManager>
{
    [Header("Inflation Rule")]
    [SerializeField] private InflationRules rules;

    [Header("Newspaper & Notice")]
    [SerializeField] private TextAsset noticeJson;

    private readonly Dictionary<int, InflationNewsData> newsByClosingTurn = new Dictionary<int, InflationNewsData>();

    private readonly Dictionary<int, InflationNewsData> noticeByStartTurn = new Dictionary<int, InflationNewsData>();

    public bool IsConfigured { get; private set; }

    protected override void OnSingletonAwake()
    {
        IsConfigured = false;

        if (rules == null)
            throw new InvalidOperationException("InflationRules 에셋을 연결하세요.");

        if (!rules.Validate(out string error))
            throw new InvalidOperationException(error);

        if (noticeJson == null)
            throw new InvalidOperationException("inflation_notice.json을 연결하세요.");

        InflationCatalogData catalog = JsonUtility.FromJson<InflationCatalogData>(noticeJson.text);

        if (catalog == null || catalog.inflation_news == null)
            throw new FormatException("인플레이션 신문 데이터를 읽지 못했습니다.");

        var ids = new HashSet<string>(StringComparer.Ordinal);
        var noticeTurns = new HashSet<int>();

        foreach (InflationNewsData item in catalog.inflation_news)
        {
            if (item == null ||
                string.IsNullOrWhiteSpace(item.id) ||
                !ids.Add(item.id) ||
                item.turn < 1 ||
                item.turn >= MarketModelConfig.TurnCount ||
                string.IsNullOrWhiteSpace(item.title) ||
                string.IsNullOrWhiteSpace(item.text) ||
                item.notice == null ||
                string.IsNullOrWhiteSpace(item.notice.message) ||
                item.notice.targets == null ||
                item.notice.targets.Length == 0 ||
                Array.Exists(
                    item.notice.targets,
                    x => string.IsNullOrWhiteSpace(x)))
            {
                throw new FormatException("인플레이션 기사 또는 안내 항목을 확인하세요.");
            }

            // 예고는 11턴, 적용 안내는 12턴.
            int startTurn = item.turn + 1;

            if (!noticeTurns.Add(startTurn))
                throw new FormatException("인플레이션 안내 턴이 중복되었습니다.");

            newsByClosingTurn.Add(item.turn, item);
            noticeByStartTurn.Add(startTurn, item);
        }

        // 규칙을 바꾸고 JSON 안내 턴을 수정하지 않은 경우를 검출.
        var ruleTurns = new HashSet<int>(rules.GetChangeTurns());

        if (!ruleTurns.SetEquals(noticeTurns))
        {
            throw new FormatException("InflationRules 적용 턴과 JSON 예고 턴+1이 일치하지 않습니다.");
        }

        IsConfigured = true;
    }

    public long CalculateGeneralPrice(long basePrice, int turn)
    {
        RequireConfigured();
        return rules.CalculateGeneralPrice(basePrice, turn);
    }

    public long CalculateInformationPrice(long basePrice, int turn)
    {
        RequireConfigured();
        return rules.CalculateInformationPrice(basePrice, turn);
    }

    /// <summary>
    /// 11, 23, 35, 47턴 마감에 표시할 기사 조회.
    /// </summary>
    /// <param name="turn"></param>
    /// <param name="news"></param>
    /// <returns></returns>
    public bool TryGetNewsForClosingTurn(int turn, out InflationNewsData news)
    {
        RequireConfigured();
        return newsByClosingTurn.TryGetValue(turn, out news);
    }

    /// <summary>
    /// 11, 23, 35, 47턴 시작에 표시할 적용 안내 조회.
    /// </summary>
    /// <param name="turn"></param>
    /// <param name="news"></param>
    /// <returns></returns>
    public bool TryGetNoticeForStartTurn(int turn, out InflationNewsData news)
    {
        RequireConfigured();
        return noticeByStartTurn.TryGetValue(turn, out news);
    }

    private void RequireConfigured()
    {
        if (!IsConfigured)
            throw new InvalidOperationException("InflationManager 설정이 완료되지 않았습니다.");
    }
}
