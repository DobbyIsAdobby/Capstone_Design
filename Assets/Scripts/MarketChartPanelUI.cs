using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 선택한 투자상품과 종목의 과거 월별 수익률 표시.
/// 현재 턴과 미래 턴의 수익률은 공개하지 않음.
/// </summary>
public class MarketChartPanelUI : MonoBehaviour
{
    [Header("Header")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text rangeText;

    [Header("Chart")]
    [SerializeField] private GameObject chartArea;
    [SerializeField] private MarketLineGraphic lineGraphic;

    [SerializeField] private TMP_Text topValueText;
    [SerializeField] private TMP_Text zeroValueText;
    [SerializeField] private TMP_Text bottomValueText;
    [SerializeField] private TMP_Text firstTurnText;
    [SerializeField] private TMP_Text lastTurnText;

    [Header("Selection")]
    [SerializeField] private Slider turnSlider;
    [SerializeField] private TMP_Text selectedValueText;
    [SerializeField] private TMP_Text emptyText;

    // 상세 수치 표시에는 decimal을 사용.
    private readonly List<decimal> percentages = new List<decimal>();

    // 그래픽 좌표 계산에는 float을 사용.
    private readonly List<float> graphValues = new List<float>();

    private bool IsConfigured =>
        titleText != null &&
        rangeText != null &&
        chartArea != null &&
        lineGraphic != null &&
        topValueText != null &&
        zeroValueText != null &&
        bottomValueText != null &&
        firstTurnText != null &&
        lastTurnText != null &&
        turnSlider != null &&
        selectedValueText != null &&
        emptyText != null;

    /// <summary>
    /// 거래 패널에서 현재 선택한 상품과 종목을 전달.
    /// </summary>
    /// <param name="assetType"></param>
    /// <param name="sector"></param>
    public void Open(AssetType assetType, MarketSector sector)
    {
        GameManager game = GameManager.Instance;
        DataManager data = DataManager.Instance;

        if (game == null || !game.CanAct)
            return;

        if (SpotlightTutorialManager.Instance != null && SpotlightTutorialManager.Instance.IsBusy)
        {
            return;
        }

        if (!IsConfigured)
        {
            Debug.LogError("MarketChartPanelUI의 Inspector 연결을 확인하세요.", this);

            return;
        }

        if (data == null || !data.IsMarketReady)
            return;

        if (!MarketSectorUtility.IsValid(sector) || !IsInvestmentAsset(assetType))
        {
            Debug.LogError("지원하지 않는 차트 대상입니다.", this);
            return;
        }

        percentages.Clear();
        graphValues.Clear();

        // 핵심 공개 제한.
        // 10턴에서는 1~9턴만 조회.
        int lastCompletedTurn = Mathf.Clamp(game.currentMonth - 1, 0, MarketModelConfig.TurnCount);

        for (int turn = 1; turn <= lastCompletedTurn; turn++)
        {
            if (!data.TryGetGeneratedMarketRates(sector, turn, out MonthlyMarketRates rates))
            {
                percentages.Clear();
                graphValues.Clear();

                Debug.LogError($"{sector} {turn}턴의 과거 수익률이 없습니다.", this);

                // 일부 기록만 표시하지 않음.
                Close();
                return;
            }

            decimal rate = GetReturn(assetType, rates);

            // 정산처럼 차입 없는 상품의 손실 한도를 적용.
            rate = Math.Max(-1m, rate);

            decimal percent = rate * 100m;

            percentages.Add(percent);
            graphValues.Add((float)percent);
        }

        titleText.text = $"{MarketSectorUtility.GetName(sector)} · " + $"{GetAssetName(assetType)} 수익률";

        gameObject.SetActive(true);

        // PanelLayer의 마지막으로 이동해 기존 거래 패널 위에 표시.
        transform.SetAsLastSibling();

        bool hasHistory = percentages.Count > 0;

        chartArea.SetActive(hasHistory);
        turnSlider.gameObject.SetActive(hasHistory);
        selectedValueText.gameObject.SetActive(hasHistory);
        emptyText.gameObject.SetActive(!hasHistory);

        if (!hasHistory)
        {
            rangeText.text = "월별 수익률";
            emptyText.text = "아직 공개된 수익률 내역이 없습니다.\n" + "첫 턴을 마감하면 확인할 수 있습니다.";

            lineGraphic.SetData(graphValues, 5f);
            return;
        }

        rangeText.text = $"1~{lastCompletedTurn}턴 · 월별 수익률";

        // 축 계산에도 공개된 과거 값만 사용.
        // 최소 ±5%, 이후 5% 단위로 범위를 늘림.
        float maximumAbsolute = 0f;

        foreach (float value in graphValues)
        {
            maximumAbsolute = Mathf.Max(maximumAbsolute, Mathf.Abs(value));
        }

        float axisLimit = Mathf.Max(5f, Mathf.Ceil(maximumAbsolute / 5f) * 5f);

        topValueText.text = $"+{axisLimit:0.##}%";
        zeroValueText.text = "0%";
        bottomValueText.text = $"-{axisLimit:0.##}%";

        firstTurnText.text = "1턴";

        // 기록이 하나면 왼쪽의 1턴 표기만 사용.
        lastTurnText.text = percentages.Count > 1 ? $"{lastCompletedTurn}턴" : "";

        lineGraphic.SetData(graphValues, axisLimit);

        turnSlider.wholeNumbers = true;
        turnSlider.minValue = 1;

        // 값이 하나뿐인 경우 슬라이더는 비활성화.
        // 범위는 유효하게 유지.
        turnSlider.maxValue = Mathf.Max(2, lastCompletedTurn);
        turnSlider.interactable = percentages.Count > 1;

        turnSlider.SetValueWithoutNotify(lastCompletedTurn);
        ShowSelectedTurn(lastCompletedTurn);
    }

    /// <summary>
    /// TurnSlider의 On Value Changed에 연결.
    /// 이미 읽어둔 과거 기록에서만 선택.
    /// </summary>
    /// <param name="value"></param>
    public void OnTurnChanged(float value)
    {
        if (percentages.Count == 0)
            return;

        ShowSelectedTurn(Mathf.RoundToInt(value));
    }

    private void ShowSelectedTurn(int turn)
    {
        int index = Mathf.Clamp(turn - 1, 0, percentages.Count - 1);

        decimal percent = percentages[index];

        selectedValueText.text = $"{index + 1}턴 · " + $"{percent.ToString("+0.00;-0.00;0.00")}%";

        selectedValueText.color = percent > 0m ? new Color32(0, 200, 0, 255) : percent < 0m ? new Color32(200, 0, 0, 255) : new Color32(220, 220, 220, 255);

        lineGraphic.SetSelectedIndex(index);
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }

    private static bool IsInvestmentAsset(AssetType type)
    {
        return type == AssetType.Stock || type == AssetType.Leverage || type == AssetType.StockInverse || type == AssetType.LeverageInverse;
    }

    private static decimal GetReturn(AssetType type, MonthlyMarketRates rates)
    {
        switch (type)
        {
            case AssetType.Stock:
                return rates.Stock;

            case AssetType.Leverage:
                return rates.Leverage;

            case AssetType.StockInverse:
                return rates.StockInverse;

            case AssetType.LeverageInverse:
                return rates.LeverageInverse;

            default:
                throw new ArgumentOutOfRangeException(nameof(type));
        }
    }

    private static string GetAssetName(AssetType type)
    {
        switch (type)
        {
            case AssetType.Stock:
                return "주식";

            case AssetType.Leverage:
                return "레버리지";

            case AssetType.StockInverse:
                return "주식 인버스";

            case AssetType.LeverageInverse:
                return "레버리지 인버스";

            default:
                throw new ArgumentOutOfRangeException(nameof(type));
        }
    }
}
