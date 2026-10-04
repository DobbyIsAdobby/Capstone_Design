using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Unity.InferenceEngine;

/// <summary>
/// 새 게임에 사용할 60턴 시장 생성
/// GameManager가 Begin()을 호출한 뒤 완료 여부를 대기함.
/// </summary>
public class MarketGenerator : MonoBehaviour
{
    [Header("Model Package")]
    [SerializeField] private ModelAsset modelAsset;
    [SerializeField] private TextAsset configJson;

    [Header("Generation")]
    [Tooltip("한 프레임에서 생성할 거래일 수")]
    [SerializeField, Min(1)] private int daysPerFrame = 4;

    [Header("Seed")]
    [Tooltip("시드를 키면 동일한 환경에서 반복 테스트하기 좋음")]
    [SerializeField] private bool useFixedSeed = true;

    [SerializeField] private int fixedSeed = 12345;

    [Header("Loading UI")]
    [SerializeField] private GameObject loadingRoot;
    [SerializeField] private TMP_Text progressText;

    // 성공|실패 모두 작업이 끝나면 IsFinished가 true가 됨
    // 성공 여부는 Succeeded로 구분
    public bool IsFinished { get; private set; }
    public bool Succeeded { get; private set; }
    public string Error { get; private set; } = "";
    public int Seed { get; private set; }

    private bool started;
    private bool generating;

    private MarketModelConfig config;
    private MarketModelRunner runner;
    private System.Random random;
    // 상품 배율 전용 난수 생성기(RNG)
    // 모델의 일별 수익률 생성에 사용하는 난수와 분리하여 적용함. 
    // 배율 추출 때문에 모델이 사용하는 난수 순서가 바뀌지 않도록 하기 위해서임.
    // (기존 random을 같이 사용하면 같은 시드라도 다음 달 부터 주식 수익률 자체가 달라질 수 있음.)
    private System.Random multiplierRandom;
    private DataManager dataManager;

    // history에는 정규화 전의 실제 일별 수익률을 보관함
    private double[] history;

    // 모델에 전달하는 순간에만 정규화된 float 배열을 사용
    private float[] normalizedHistory;

    // 완성된 월별 수익률을 임시 보관함
    // 60턴 전체 생성 후 DataManager에 등록
    private readonly Dictionary<int, MonthlyMarketRates> results = new Dictionary<int, MonthlyMarketRates>();

    // Warm-up을 포함하여 지금까지 생성한 거래일 수
    private int generatedDays;

    // 현재 월에 포함한 거래일 수
    private int daysInMonth;

    // 월 복리 누적값
    // ex) 1.05일 경우 해당 월 수익률은 +5%
    private double monthlyGrowth = 1.0;

    /// <summary>
    /// 생성에 필요한 설정,난수,입력 내역,모델을 준비. 
    /// 실제 일별 생성은 이후 Update에서 나누어 실행
    /// </summary>
    public void Begin()
    {
        // 중복 호출로 같은 플레이의 시장이 바뀌는 것을 방지
        if (started)
            return;

        started = true;

        if (loadingRoot != null)
            loadingRoot.SetActive(true);

        SetProgress("시장을 준비하고 있습니다…");

        try
        {
            dataManager = DataManager.Instance;

            if (dataManager == null)
            {
                throw new InvalidOperationException("DataManager가 없습니다.");
            }

            if (dataManager.IsMarketReady)
            {
                throw new InvalidOperationException("이번 플레이의 시장이 이미 등록되어 있습니다.");
            }

            config = MarketModelConfig.Parse(configJson);

            // 테스트 중에는 고정 시드, 일반 플레이에서는 새 시드를 사용.
            Seed = useFixedSeed ? fixedSeed : Guid.NewGuid().GetHashCode();

            // 시장 생성 전용 난수 생성기(RNG).
            // UnityEngine.Random을 사용하는 다른 시스템과 분리됨.
            random = new System.Random(Seed);

            // 같은 시장 시드에서 배율도 재현할 수 있도록 파생 시드를 사용함.
            // 기존 모델용 random과는 별개임.
            // 0x51A7B39D는 해시 알고리즘에서 주로 사용하는 고정 상수.
            int multiplierSeed = unchecked(Seed ^ 0x51A7B39D);
            multiplierRandom = new System.Random(multiplierSeed);

            int window = config.generation.window_size;

            history = new double[window];
            normalizedHistory = new float[window];

            double mean = config.normalization.return_mean;
            double std = config.normalization.return_std;

            // Python 모델 학습 코드 기반 로직
            // rng.normal(RETURN_MEAN, RETURN_STD, 60).astype(np.float32)
            for (int i = 0; i < window; i++)
            {
                history[i] = (float)(mean + std * NextStandardNormal());
            }

            runner = new MarketModelRunner(modelAsset, config);
            generating = true;

            Debug.Log($"수익률 자동 생성 ONNX 모델 시장 생성 시작 / Seed={Seed}");
        }
        catch (Exception exception)
        {
            Fail(exception);
        }
    }

    private void Update()
    {
        if (!generating)
            return;

        try
        {
            // Inspector 값이 잘못 변경되어도 최소 1일은 처리
            int count = Math.Max(1, daysPerFrame);

            // 모든 추론을 프레임 별로 나눠서 하도록 함.
            for (int i = 0; i < count && generating; i++)
                GenerateOneDay();

            if (generating)
            {
                int warmup = config.generation.warmup_days;

                if (generatedDays < warmup)
                {
                    SetProgress(
                        $"시장 준비 중… {generatedDays}/{warmup}일");
                }
                else
                {
                    SetProgress(
                        $"시장 생성 중… " +
                        $"{results.Count}/{MarketModelConfig.TurnCount}개월");
                }
            }
        }
        catch (Exception exception)
        {
            // 모델 오류나 비정상 수치 발생 시 부분 생성 데이터는 공개하지 않음
            Fail(exception);
        }
    }

    /// <summary>
    /// 다음 하루를 생성하고, 필요한 경우 한 달의 수익률까지 확정함
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    private void GenerateOneDay()
    {
        double mean = config.normalization.return_mean;
        double std = config.normalization.return_std;

        // 1. 실제 일별 수익률을 모델 입력용으로 정규화
        // normalized = (history - mean) / std
        for (int i = 0; i < history.Length; i++)
        {
            normalizedHistory[i] = (float)((history[i] - mean) / std);
        }

        // 2. 모델로 다음 날의 정규화된 분포를 구함
        runner.Evaluate(normalizedHistory, out double mu, out double sigma);

        // 3. 평균 mu, 표준편차 sigma인 정규분포에서 하나를 추출
        double normalizedReturn = mu + sigma * NextStandardNormal();

        // 4. 실제 일별 수익률로 역정규화
        double dailyReturn = normalizedReturn * std + mean;

        // 게임 입력 검증
        // 잘못된 값을 임의로 보정하거나 다시 추출하지 않고 실패했음을 알림
        if (!MarketModelConfig.IsFinite(dailyReturn) || dailyReturn <= -1.0)
        {
            throw new InvalidOperationException($"유효하지 않은 일별 수익률: {dailyReturn}");
        }

        // 5. 가장 오래된 하루를 제거하고 새 수익률을 마지막에 추가
        // 배열 크기는 항상 60으로 유지.
        Array.Copy(history, 1, history, 0, history.Length - 1);

        history[history.Length - 1] = dailyReturn;

        generatedDays++;

        // 6. Warm-up 데이터는 입력 이력만 갱신하고 월별 계산에서는 제외
        // Warm-up이 120이면 121번째 생성일부터 1턴 계산에 들어감
        if (generatedDays <= config.generation.warmup_days)
            return;

        // 7. 일별 수익률을 복리로 누적
        // 월 수익률 = (1+r1)(1+r2)...(1+r21) - 1
        monthlyGrowth *= 1.0 + dailyReturn;
        daysInMonth++;

        if (!MarketModelConfig.IsFinite(monthlyGrowth) || monthlyGrowth <= 0)
        {
            throw new InvalidOperationException("월별 복리 계산 결과가 유효하지 않습니다.");
        }

        // 아직 21거래일이 되지 않았다면 다음 날 생성을 대기
        if (daysInMonth < config.generation.trading_days_per_turn)
            return;

        // 8. 완성된 월 수익률을 금액 계산에 사용할 decimal로 변환
        decimal r = (decimal)(monthlyGrowth - 1.0);

        if (r <= -1m)
        {
            throw new InvalidOperationException("월별 주식 수익률이 -100% 이하입니다.");
        }

        int turn = results.Count + 1;

        // 9. 하나의 월 수익률에서 네 상품의 수익률로 변경. - 너무 단순하여 상품별 랜덤 배율을 적용하도록 재구성함.
        // 주식 r / 레버리지 2r / 주식 인버스 -r / 레버리지 인버스 -2r
        //results.Add(turn, new MonthlyMarketRates(r, 2m * r, -r, -2m * r));

        // 월 주식 수익률 r에 이번 턴에 사용할 상품별 랜덤 배율을 적용
        MonthlyMarketRates rates = MarketReturnRules.Create(r, multiplierRandom);

        // 확정한 수익률 보관
        // 정보 열람이나 결산 시점에는 배율을 다시 추출하지 않음.
        results.Add(turn, rates);

        // 다음 달 복리 계산을 위해 월 단위 누적값만 초기화
        // 최근 60일 history는 그대로 이어서 사용
        daysInMonth = 0;
        monthlyGrowth = 1.0;

        if (results.Count == MarketModelConfig.TurnCount)
            CompleteGeneration();
    }

    /// <summary>
    /// 평균 0, 표준편차 1인 표준정규난수를 제작 
    /// System.Random의 균등분포 난수를 Box-Muller 방식으로 변환.
    /// </summary>
    /// <returns></returns>
    private double NextStandardNormal()
    {
        // NextDouble()로 0을 피하여 Log(0)이 발생하지 않도록
        double u1 = 1.0 - random.NextDouble();
        double u2 = 1.0 - random.NextDouble();

        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
    }

    /// <summary>
    /// 완성된 시장 등록과 모델에 관한 자원 정리만 진행
    /// 로딩 패널은 유저가 시작 버튼을 누른 뒤에 닫힘.
    /// </summary>
    private void CompleteGeneration()
    {
        dataManager.RegisterGeneratedMarket(results, Seed);

        generating = false;
        Succeeded = true;
        IsFinished = true;

        // 이후에는 저장된 시장만 조회하므로 모델 실행 자원이 필요 없음
        ReleaseModel();

        SetProgress("시장 생성 완료");

        // 스토리가 끝나기 전에 패널이 닫히면 안됨.
        /*
        if (loadingRoot != null)
            loadingRoot.SetActive(false);
        */
    }

    /// <summary>
    /// 실패 내용을 남기고 게임 시작을 막음
    /// CSV나 최초에 제작했던 RNG 생성으로 대체하지 않게함 - 테스트할때만 유지하려고 둔 것이지, 실제 게임 내에선 대체해선 안됨.
    /// </summary>
    /// <param name="exception"></param>
    private void Fail(Exception exception)
    {
        generating = false;
        Succeeded = false;
        IsFinished = true;
        Error = exception.Message;

        ReleaseModel();

        SetProgress($"시장 생성 실패\n{Error}");
        Debug.LogError($"ONNX {exception}", this);
    }

    private void SetProgress(string message)
    {
        if (progressText != null)
            progressText.text = message;
    }

    private void ReleaseModel()
    {
        runner?.Dispose();
        runner = null;
    }

    private void OnDisable()
    {
        // 비활성화되면 Update가 멈춤
        // GameManager가 완료를 무한히 기다리지 않도록 예외 로그를 남김.
        if (generating)
        {
            Fail(new InvalidOperationException("시장 생성 중 MarketGenerator가 비활성화되었습니다."));
        }
    }

    private void OnDestroy()
    {
        ReleaseModel();
    }
}
