using System;
using UnityEngine;

// market_config.json의 내용을 보관하는 Data Class
[Serializable]
public sealed class MarketModelConfig
{
    /// <summary>
    /// 게임 전체 진행 턴 수 - 상수로 선언.
    /// 추후 목표 턴 수 조정 시스템을 추가할 경우 상수 선언을 철회해야함.
    /// </summary>
    public const int TurnCount = 60;

    // JSON 최상단 항목 이름과 일치하게
    public ModelSettings model;
    public NormalizationSettings normalization;
    public GenerationSettings generation;
    public AssetSettings assets;

    [Serializable]
    public sealed class ModelSettings
    {
        // JSON에 기록된 모델 파일명.
        // 모델 연결은 Inspector의 ModelAsset으로 진행
        public string file;

        // ONNX가 입력을 받는 이름: market_history
        public string input_name;

        // [배치 개수, 시계열 길이, 특성 개수] = [1, 60, 1]
        public int[] input_shape;

        // ONNX 출력 이름: mu, sigma
        public string[] output_names;
    }

    [Serializable]
    public sealed class NormalizationSettings
    {
        // 학습 데이터의 일별 수익률 평균과 표준편차
        // 입력 정규화와 출력 역정규화에 동일한 값 사용.
        public double return_mean;
        public double return_std;
    }

    [Serializable]
    public sealed class GenerationSettings
    {
        // 모델 입력에 필요한 과거 거래일 수.
        public int window_size;

        // 게임에 사용하기 전에 미리 생성하고 버릴 거래일 수.
        public int warmup_days;

        // 게임의 한 턴을 구성하는 거래일 수.
        public int trading_days_per_turn;

        // 생성할 전체 게임 턴 수.
        public int total_turns;
    }

    [Serializable]
    public sealed class AssetSettings
    {
        // 현재 지원하는 규칙은 r, 2 * r, -r. - 주식,레버,주식 인버스, 레버 인버스
        public string TECH;
        public string LEVERAGE;
        public string INVERSE;
    }

    /// <summary>
    /// Inspector에 연결한 JSON을 읽고, 사용할 수 있는 설정인지 검사.
    /// </summary>
    /// <param name="json"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    /// <exception cref="FormatException"></exception>
    public static MarketModelConfig Parse(TextAsset json)
    {
        if (json == null)
        {
            throw new InvalidOperationException("Market Config JSON을 연결하세요.");
        }

        MarketModelConfig config = JsonUtility.FromJson<MarketModelConfig>(json.text);

        if (config == null)
        {
            throw new FormatException("시장 설정 JSON을 읽을 수 없습니다.");
        }

        config.Validate();
        return config;
    }

    /// <summary>
    /// 잘못된 설정으로 모델을 실행하지 않도록 시작 전에 검사.
    /// </summary>
    /// <exception cref="FormatException"></exception>
    private void Validate()
    {
        if (model == null || normalization == null || generation == null || assets == null)
        {
            throw new FormatException("시장 설정에 필수 항목이 없습니다.");
        }

        // ONNX 모델의 입출력 내역을 검사.
        if (model.input_name != "market_history" ||
            model.input_shape == null ||
            model.input_shape.Length != 3 ||
            model.input_shape[0] != 1 ||
            model.input_shape[1] != 60 ||
            model.input_shape[2] != 1 ||
            model.output_names == null ||
            Array.IndexOf(model.output_names, "mu") < 0 ||
            Array.IndexOf(model.output_names, "sigma") < 0)
        {
            throw new FormatException("모델 입출력 설정이 예상과 다릅니다.");
        }

        if (generation.window_size != 60 || generation.warmup_days < 0 || generation.trading_days_per_turn <= 0 || generation.total_turns != TurnCount)
        {
            throw new FormatException("입력 이력은 60일, 전체 게임은 60턴이어야 합니다.");
        }

        // 표준편차가 0이면 정규화 과정에서 나눗셈이 불가능하므로 예외처리.
        if (!IsFinite(normalization.return_mean) || !IsFinite(normalization.return_std) || normalization.return_std <= 0)
        {
            throw new FormatException("정규화 평균 또는 표준편차가 잘못되었습니다.");
        }

        // 현재 코드가 지원하는 상품 규칙과 일치하는지만 확인.
        if (assets.TECH?.Replace(" ", "") != "r" || assets.LEVERAGE?.Replace(" ", "") != "2*r" || assets.INVERSE?.Replace(" ", "") != "-r")
        {
            throw new FormatException("지원하지 않는 상품 수익률 규칙입니다.");
        }
    }

    /// 계산 결과가 NaN 또는 무한대인지 확인하기 위한 공통 검사.
    public static bool IsFinite(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
