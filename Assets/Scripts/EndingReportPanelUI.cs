using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 미리 전달받은 엔딩 결과 출력
/// </summary>
public class EndingReportPanelUI : MonoBehaviour
{
    [Header("Root Panel")]
    [SerializeField] private GameObject endingRoot;
    [SerializeField] private GameObject reportRoot;

    [Header("Bankruptcy Summary")]
    [SerializeField] private GameObject bankruptcyRoot;
    [SerializeField] private TMP_Text turnText;
    [SerializeField] private TMP_Text causeText;

    [Header("Normal Summary")]
    [SerializeField] private GameObject normalRoot;
    [SerializeField] private TMP_Text bestAssetText;
    [SerializeField] private TMP_Text finalAssetText;
    [SerializeField] private GameObject newRecordText;

    [Header("Charts")]
    [SerializeField] private TMP_Dropdown chartDropdown;
    [SerializeField] private GameObject chartArea;
    [SerializeField] private MarketLineGraphic lineGraphic;
    [SerializeField] private TMP_Text topValueText;
    [SerializeField] private TMP_Text zeroValueText;
    [SerializeField] private TMP_Text bottomValueText;
    [SerializeField] private TMP_Text emptyChartText;
    [SerializeField] private TMP_Text selectedValueText;
    [SerializeField] private Slider turnSlider;

    [Header("Investment Statistics")]
    [SerializeField] private TMP_Text frequentSectorText;
    [SerializeField] private TMP_Text largestPurchaseText;
    [SerializeField] private TMP_Text maximumIncomeText;
    [SerializeField] private TMP_Text maximumLossText;

    [Header("Move Scene")]
    [SerializeField] private SceneTransitionManager sceneTransition;

    private EndingReportData result;
    private ReportSeries selectedSeries;

    // int나 float로 저장하면 큰 금액의 범위, 정밀도 문제가 존재함.
    // 최고 기록은 long을 문자열로 변환해 저장.
    private const string BestRecordKey = "EndingBestNetWorth_v1";

    /// <summary>
    /// EndingSceneUI의 On Report Requested에 연결.
    /// </summary>
    public void Open()
    {
        result = EndingReportSession.Current;

        if (result == null)
        {
            // EndingScene만 직접 실행한 미리보기에서는 플레이 결과가 없음.
            // 이를 위한 예외처리
            Debug.LogWarning("결산 데이터가 없습니다. GameScene에서 엔딩으로 진입하세요.", this);

            return;
        }

        reportRoot.SetActive(true);
        reportRoot.transform.SetAsLastSibling();

        if (endingRoot != null)
            endingRoot.SetActive(false);

        bool earlyEnd = result.ending == EndingType.Bad;

        bankruptcyRoot.SetActive(earlyEnd);
        normalRoot.SetActive(!earlyEnd);

        if (earlyEnd)
        {
            turnText.text = result.bankrupt ? $"{result.endedTurn}턴" : $"{result.endedTurn}턴";

            string cause = string.IsNullOrWhiteSpace(result.cause) ? "보유 현금 부족" : result.cause;

            causeText.text = result.bankrupt ? $"파산 사유 : {cause}" : $"종료 사유 : {cause}";
        }
        else
        {
            UpdateBestRecord();

            bestAssetText.text = $"당신의 최고 기록 순자산 : {result.bestNetWorth:N0}원";

            finalAssetText.text = $"이번 인생의 당신의 순자산 : {result.finalNetWorth:N0}원";

            newRecordText.SetActive(result.isNewRecord);
        }

        // 계산한 결과를 각각의 본문에 전달합니다.
        // RectTransform이나 카드 크기는 변경하지 않습니다.
        string[] statistics = BuildStatistics(result.history);

        frequentSectorText.text = statistics[0];
        largestPurchaseText.text = statistics[1];
        maximumIncomeText.text = statistics[2];
        maximumLossText.text = statistics[3];

        // 목록은 결과에 포함된 금융자산, 부동산 차트로 구성.
        chartDropdown.ClearOptions();

        chartDropdown.AddOptions(result.charts.Select(chart => chart.title).ToList());

        chartDropdown.SetValueWithoutNotify(0);
        SelectChart(0);
    }

    /// <summary>
    /// 정상 완주 결과만 최고 기록과 비교.
    /// 같은 결과 패널을 다시 열어도 최초 판정은 유지.
    /// </summary>
    private void UpdateBestRecord()
    {
        if (result.recordChecked)
            return;

        bool hasPrevious = long.TryParse(
            PlayerPrefs.GetString(BestRecordKey, ""),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out long previous);

        result.isNewRecord = !hasPrevious || result.finalNetWorth > previous;

        result.bestNetWorth = result.isNewRecord ? result.finalNetWorth : previous;

        if (result.isNewRecord)
        {
            PlayerPrefs.SetString(BestRecordKey, result.bestNetWorth.ToString(CultureInfo.InvariantCulture));

            PlayerPrefs.Save();
        }

        result.recordChecked = true;
    }

    /// <summary>
    /// 각 자산별 차트 선택을 위한 인덱스 생성.
    /// Dropdown의 Dynamic int 이벤트에 연결.
    /// </summary>
    /// <param name="index"></param>
    public void SelectChart(int index)
    {
        if (result == null || index < 0 || index >= result.charts.Count)
        {
            return;
        }

        selectedSeries = result.charts[index];

        int count = selectedSeries.percentages.Count;
        bool hasData = count > 0;

        chartArea.SetActive(hasData);
        turnSlider.gameObject.SetActive(hasData);
        selectedValueText.gameObject.SetActive(hasData);
        emptyChartText.gameObject.SetActive(!hasData);

        if (!hasData)
        {
            emptyChartText.text = "마감이 완료된 턴이 없어\n표시할 수익률 기록이 없습니다.";

            lineGraphic.SetData(new List<float>(), 5f);
            return;
        }

        // 공개 가능한 결과만 사용해 세로축 범위 계산.
        float maximum = selectedSeries.percentages.Max(value => Mathf.Abs(value));

        float limit = Mathf.Max(5f, Mathf.Ceil(maximum / 5f) * 5f);

        topValueText.text = $"+{limit:0.##}%";
        zeroValueText.text = "0%";
        bottomValueText.text = $"-{limit:0.##}%";

        lineGraphic.SetData(selectedSeries.percentages, limit);

        turnSlider.wholeNumbers = true;
        turnSlider.minValue = 1;
        turnSlider.maxValue = Mathf.Max(2, count);
        turnSlider.interactable = count > 1;

        turnSlider.SetValueWithoutNotify(count);
        SelectTurn(count);
    }

    /// <summary>
    /// 슬라이더를 통해 각 달별 내역 확인.
    /// </summary>
    /// <param name="value"></param>
    public void SelectTurn(float value)
    {
        if (selectedSeries == null || selectedSeries.percentages.Count == 0)
        {
            return;
        }

        int index = Mathf.Clamp(Mathf.RoundToInt(value) - 1, 0, selectedSeries.percentages.Count - 1);

        float percent = selectedSeries.percentages[index];

        selectedValueText.text = $"{index + 1}달 : " + percent.ToString("+0.00;-0.00;0.00") + "%";

        selectedValueText.color = percent > 0
            ? new Color32(0, 200, 0, 255)
            : percent < 0
                ? new Color32(200, 0, 0, 255)
                : new Color32(90, 90, 90, 255);

        lineGraphic.SetSelectedIndex(index);
    }

    /// <summary>
    /// 저장된 자료를 집계해 투자 기록 문구 생성.
    /// 동률이면 공동 기록으로 표시.
    /// </summary>
    /// <param name="history"></param>
    /// <returns></returns>
    private static string[] BuildStatistics(ReportHistory history)
    {
        string mostFrequent = "투자 기록 없음";
        string largestPurchase = "투자 기록 없음";

        var sectorGroups = history.buys
            .Where(row => row.sector >= 0)
            .GroupBy(row => row.sector)
            .Select(group => new
            {
                Sector = group.Key,
                Count = group.Count()
            })
            .ToList();

        if (sectorGroups.Count > 0)
        {
            int maximum = sectorGroups.Max(row => row.Count);

            mostFrequent = string.Join(" / ",
                sectorGroups
                    .Where(row => row.Count == maximum)
                    .OrderBy(row => row.Sector)
                    .Select(row =>
                        MarketSectorUtility.GetName(
                            (MarketSector)row.Sector)));

            mostFrequent += $" : {maximum}회";
        }

        var purchases = history.buys
            .GroupBy(row => row.key)
            .Select(group => new
            {
                Label = group.First().label,
                // decimal로 합산해 long 합계 범위 초과를 피함.
                Amount = group.Sum(row => (decimal)row.amount)
            })
            .ToList();

        if (purchases.Count > 0)
        {
            decimal maximum = purchases.Max(row => row.Amount);

            largestPurchase = string.Join(" / ",
                purchases
                    .Where(row => row.Amount == maximum)
                    .OrderBy(row => row.Label)
                    .Select(row => row.Label));

            largestPurchase += $" : {maximum:N0}원";
        }

        return new[]
        {
            mostFrequent,
            largestPurchase,
            Extreme(history, true),
            Extreme(history, false)
        };
    }

    private static string Extreme(ReportHistory history, bool positive)
    {
        var candidates = history.changes.Where(row => positive ? row.amount > 0 : row.amount < 0).ToList();

        if (candidates.Count == 0)
            return "기록 없음";

        long amount = positive ? candidates.Max(row => row.amount) : candidates.Min(row => row.amount);

        var winners = candidates.Where(row => row.amount == amount).OrderBy(row => row.turn).ToList();

        ReportChange first = winners[0];

        string signedAmount = amount > 0 ? $"+{amount:N0}" : $"{amount:N0}";

        // 숫자 서식을 명확하게 구성.
        signedAmount = amount > 0 ? $"+{amount:N0}" : $"{amount:N0}";

        string suffix = winners.Count > 1 ? $" (동률 {winners.Count}건)" : "";

        return $"{first.turn}달 : {first.label}\n" + $"{signedAmount}원{suffix}";
    }

    public void MoveToLobby()
    {
        if (sceneTransition == null)
        {
            Debug.LogError("SceneTransitionManager를 연결하세요.", this);
            return;
        }

        if (!sceneTransition.TryMoveToLobby(out string error))
            Debug.LogError(error, this);
    }

    public void Restart()
    {
        if (sceneTransition == null)
        {
            Debug.LogError("SceneTransitionManager를 연결하세요.", this);
            return;
        }

        // 시장도 새로 생성.
        if (!sceneTransition.TryStartNewGame(out string error))
            Debug.LogError(error, this);
    }
}
