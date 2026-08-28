namespace DH.SignalProcessing;

/// <summary>
/// FFT 处理器：实现 ISignalProcessor 接口，对输入信号执行 FFT 并返回幅值谱
/// </summary>
public sealed class FFTProcessor : ISignalProcessor
{
    private readonly SpectrumAnalyzer _analyzer = new();

    public string Name => "FFT 频谱分析";
    public ProcessingNodeType NodeType => ProcessingNodeType.FFT;

    public WindowType Window
    {
        get => _analyzer.WindowType;
        set => _analyzer.WindowType = value;
    }

    public float[] Process(float[] input)
    {
        var result = _analyzer.ComputeMagnitudeSpectrum(input, 1000);
        return result.Values.Select(v => (float)v).ToArray();
    }

    public SpectrumResult ComputeSpectrum(float[] input, double sampleRate)
    {
        return _analyzer.ComputeMagnitudeSpectrum(input, sampleRate);
    }

    public void Reset() { }
}

/// <summary>
/// 滤波处理器：包装 DigitalFilter 实现 ISignalProcessor
/// </summary>
public sealed class FilterProcessor : ISignalProcessor
{
    private readonly DigitalFilter _filter;

    public string Name => _filter.Name;
    public ProcessingNodeType NodeType => ProcessingNodeType.Filter;

    public FilterProcessor(FilterType filterType, double sampleRate, double cutoffFreq, int order = 4,
        double cutoffFreq2 = 0, FilterDesign design = FilterDesign.Butterworth)
    {
        _filter = new DigitalFilter(filterType, sampleRate, cutoffFreq, order, cutoffFreq2, design);
    }

    public float[] Process(float[] input) => _filter.Process(input);
    public void Reset() => _filter.Reset();
}

/// <summary>
/// 统计处理器：计算信号的统计特征
/// </summary>
public sealed class StatisticsProcessor : ISignalProcessor
{
    public string Name => "统计分析";
    public ProcessingNodeType NodeType => ProcessingNodeType.Statistics;

    public float[] Process(float[] input)
    {
        var stats = StatisticsCalculator.Compute(input);
        return new float[]
        {
            (float)stats.Mean,
            (float)stats.Rms,
            (float)stats.Peak,
            (float)stats.PeakToPeak,
            (float)stats.StdDev,
            (float)stats.CrestFactor,
            (float)stats.Skewness,
            (float)stats.Kurtosis
        };
    }

    public void Reset() { }
}

/// <summary>
/// 积分/微分处理器
/// </summary>
public sealed class IntegralProcessor : ISignalProcessor
{
    private readonly IntegralType _integralType;
    private readonly double _sampleRate;
    private double _prevValue;
    private double _accumulator;

    public string Name => _integralType switch
    {
        IntegralType.SingleIntegral => "单积分",
        IntegralType.DoubleIntegral => "双积分",
        IntegralType.SingleDifferential => "单微分",
        IntegralType.DoubleDifferential => "双微分",
        _ => "无"
    };

    public ProcessingNodeType NodeType => _integralType switch
    {
        IntegralType.SingleIntegral or IntegralType.DoubleIntegral => ProcessingNodeType.Integral,
        IntegralType.SingleDifferential or IntegralType.DoubleDifferential => ProcessingNodeType.Differential,
        _ => ProcessingNodeType.Custom
    };

    public IntegralProcessor(IntegralType type, double sampleRate)
    {
        _integralType = type;
        _sampleRate = sampleRate;
    }

    public float[] Process(float[] input)
    {
        var output = new float[input.Length];
        var dt = 1.0 / _sampleRate;

        switch (_integralType)
        {
            case IntegralType.SingleIntegral:
                for (int i = 0; i < input.Length; i++)
                {
                    _accumulator += (input[i] + _prevValue) * 0.5 * dt;
                    output[i] = (float)_accumulator;
                    _prevValue = input[i];
                }
                break;

            case IntegralType.DoubleIntegral:
                var intAcc = 0.0;
                for (int i = 0; i < input.Length; i++)
                {
                    intAcc += (input[i] + _prevValue) * 0.5 * dt;
                    _accumulator += intAcc * dt;
                    output[i] = (float)_accumulator;
                    _prevValue = input[i];
                }
                break;

            case IntegralType.SingleDifferential:
                for (int i = 0; i < input.Length; i++)
                {
                    output[i] = (float)((input[i] - _prevValue) / dt);
                    _prevValue = input[i];
                }
                break;

            case IntegralType.DoubleDifferential:
                var prevDiff = 0.0;
                for (int i = 0; i < input.Length; i++)
                {
                    var diff = (input[i] - _prevValue) / dt;
                    output[i] = (float)((diff - prevDiff) / dt);
                    prevDiff = diff;
                    _prevValue = input[i];
                }
                break;

            default:
                Array.Copy(input, output, input.Length);
                break;
        }

        return output;
    }

    public void Reset()
    {
        _prevValue = 0;
        _accumulator = 0;
    }
}

/// <summary>
/// 包络处理器：基于 Hilbert 变换的包络检波
/// </summary>
public sealed class EnvelopeProcessor : ISignalProcessor
{
    public string Name => "包络分析";
    public ProcessingNodeType NodeType => ProcessingNodeType.Envelope;

    public float[] Process(float[] input)
    {
        var n = FourierTransform.NextPowerOfTwo(input.Length);
        var complex = new System.Numerics.Complex[n];
        for (int i = 0; i < input.Length; i++)
            complex[i] = new System.Numerics.Complex(input[i], 0);

        FourierTransform.Forward(complex);

        var halfN = n / 2;
        for (int i = 1; i < halfN; i++)
            complex[i] *= 2;
        for (int i = halfN + 1; i < n; i++)
            complex[i] = 0;

        FourierTransform.Inverse(complex);

        var envelope = new float[input.Length];
        for (int i = 0; i < input.Length; i++)
            envelope[i] = (float)complex[i].Magnitude;

        return envelope;
    }

    public void Reset() { }
}
