using System;
using Unity.InferenceEngine; // ONNX 모델과 같은 신경망 추론 라이브러리를 사용하기 위해 선언

/// <summary>
/// ONNX 모델 실행만 담당하는 일반 C# Class.
/// MarketGenerator가 생성하고 사용 완료 후 IDisposable 인터페이스로 Dispose함.
/// </summary>
public sealed class MarketModelRunner : IDisposable
{
    // Worker는 유니티 InferenceEngine에서 모델의 연산을 실제로 실행하는 객체.
    private Worker worker;

    private readonly string inputName;
    private readonly int windowSize;

    public MarketModelRunner(
        ModelAsset modelAsset,
        MarketModelConfig config)
    {
        if (modelAsset == null)
            throw new ArgumentNullException(nameof(modelAsset));

        inputName = config.model.input_name;
        windowSize = config.generation.window_size;

        // Unity가 임포트한 모델 에셋으로 런타임 모델을 구성
        Model model = ModelLoader.Load(modelAsset);

        // CPU에서 기능 우선 검증.
        // GPU 전환 여부는 실제 생성 시간을 측정한 뒤 결정.
        worker = new Worker(model, BackendType.CPU);
    }

    /// <summary>
    /// 정규화된 최근 60일 이력으로 다음 날의 mu, sigma를 구함.
    /// 이 메서드에서는 난수 샘플링이나 역정규화를 하지 않음.
    /// </summary>
    /// <param name="normalizedHistory"></param>
    /// <param name="mu"></param>
    /// <param name="sigma"></param>
    /// <exception cref="ObjectDisposedException"></exception>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="InvalidOperationException"></exception>
    public void Evaluate(float[] normalizedHistory, out double mu, out double sigma)
    {
        if (worker == null)
        {
            throw new ObjectDisposedException(nameof(MarketModelRunner));
        }

        if (normalizedHistory == null || normalizedHistory.Length != windowSize)
        {
            throw new ArgumentException("모델 입력 개수가 맞지 않습니다.");
        }

        foreach (float value in normalizedHistory)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new ArgumentException("모델 입력에 비정상 값이 있습니다.");
            }
        }

        // float 배열을 ONNX가 요구하는 [1, 60, 1] 텐서로 구조화.
        // using으로 메서드가 끝날 때 입력 텐서를 해제.
        using var input = new Tensor<float>(new TensorShape(1, windowSize, 1), normalizedHistory);

        worker.SetInput(inputName, input);
        worker.Schedule();

        // 출력 이름으로 각각의 텐서를 가져옴.
        Tensor<float> muOutput = worker.PeekOutput("mu") as Tensor<float>;

        Tensor<float> sigmaOutput = worker.PeekOutput("sigma") as Tensor<float>;

        if (muOutput == null || sigmaOutput == null)
        {
            throw new InvalidOperationException("mu 또는 sigma 출력이 없습니다.");
        }

        // 연산 완료를 기다리고 CPU에서 읽을 수 있는 복사본 제작.
        // 원본 출력은 Worker 소유이므로 직접 Dispose하지 않음.
        using var muCopy = muOutput.ReadbackAndClone();
        using var sigmaCopy = sigmaOutput.ReadbackAndClone();

        if (muCopy.shape.length != 1 || sigmaCopy.shape.length != 1)
        {
            throw new InvalidOperationException("모델 출력 크기가 맞지 않습니다.");
        }

        mu = muCopy[0];
        sigma = sigmaCopy[0];

        // sigma는 모델 내부에서 이미 양수 변환된 값임.
        // 따라서 Softplus 등을 다시 적용하면 안됨.
        if (!MarketModelConfig.IsFinite(mu) || !MarketModelConfig.IsFinite(sigma) || sigma <= 0)
        {
            throw new InvalidOperationException($"비정상 모델 출력: mu={mu}, sigma={sigma}");
        }
    }

    /// <summary>
    /// 모델 실행에 사용한 자원을 해제
    /// </summary>
    public void Dispose()
    {
        worker?.Dispose();
        worker = null;
    }
}
