using System.Numerics;

namespace DH.SignalProcessing;

/// <summary>
/// 频谱分析器：组合 FFT + 加窗 + 幅值/功率谱计算
/// </summary>
public sealed class SpectrumAnalyzer
{
    public WindowType WindowType { get; set; } = WindowType.Hanning;
    public bool UseZeroPadding { get; set; } = true;
    public int ZeroPaddingFactor { get; set; } = 2;

    /// <summary>
    /// 计算幅值谱（单边谱）
    /// </summary>
    public SpectrumResult ComputeMagnitudeSpectrum(float[] signal, double sampleRate)
    {
        var windowed = WindowFunctions.ApplyToSignal(signal, WindowType);
        var fftSize = UseZeroPadding
            ? FourierTransform.NextPowerOfTwo(windowed.Length * ZeroPaddingFactor)
            : FourierTransform.NextPowerOfTwo(windowed.Length);

        var complex = new Complex[fftSize];
        for (int i = 0; i < windowed.Length; i++)
            complex[i] = new Complex(windowed[i], 0);

        FourierTransform.Forward(complex);

        var halfN = fftSize / 2;
        var freqs = new double[halfN];
        var mags = new double[halfN];
        var binWidth = sampleRate / fftSize;
        var corrFactor = WindowFunctions.GetAmplitudeCorrectionFactor(WindowType);

        for (int i = 0; i < halfN; i++)
        {
            freqs[i] = i * binWidth;
            mags[i] = complex[i].Magnitude * 2.0 * corrFactor / signal.Length;
        }
        mags[0] /= 2.0;

        return new SpectrumResult(freqs, mags, SpectrumType.Magnitude, sampleRate, fftSize);
    }

    /// <summary>
    /// 计算功率谱密度（PSD）
    /// </summary>
    public SpectrumResult ComputePowerSpectrum(float[] signal, double sampleRate)
    {
        var window = WindowFunctions.Generate(WindowType, signal.Length);
        var windowed = new float[signal.Length];
        for (int i = 0; i < signal.Length; i++)
            windowed[i] = (float)(signal[i] * window[i]);

        var fftSize = UseZeroPadding
            ? FourierTransform.NextPowerOfTwo(windowed.Length * ZeroPaddingFactor)
            : FourierTransform.NextPowerOfTwo(windowed.Length);

        var complex = new Complex[fftSize];
        for (int i = 0; i < windowed.Length; i++)
            complex[i] = new Complex(windowed[i], 0);

        FourierTransform.Forward(complex);

        var halfN = fftSize / 2;
        var freqs = new double[halfN];
        var psd = new double[halfN];
        var binWidth = sampleRate / fftSize;

        var windowSumSquares = 0.0;
        for (int i = 0; i < window.Length; i++)
            windowSumSquares += window[i] * window[i];

        var psdScale = 1.0 / (sampleRate * windowSumSquares);

        for (int i = 0; i < halfN; i++)
        {
            freqs[i] = i * binWidth;
            psd[i] = complex[i].Magnitude * complex[i].Magnitude * psdScale * 2.0;
        }
        psd[0] /= 2.0;

        return new SpectrumResult(freqs, psd, SpectrumType.Power, sampleRate, fftSize);
    }

    /// <summary>
    /// 从频谱结果中查找主频峰
    /// </summary>
    public static List<PeakInfo> FindPeaks(SpectrumResult spectrum, int maxPeaks = 5, double threshold = 0.1)
    {
        var peaks = new List<PeakInfo>();
        var mags = spectrum.Values;
        var freqs = spectrum.Frequencies;

        var maxMag = mags.Max();
        var thresholdValue = maxMag * threshold;

        for (int i = 1; i < mags.Length - 1; i++)
        {
            if (mags[i] > mags[i - 1] && mags[i] > mags[i + 1] && mags[i] > thresholdValue)
            {
                peaks.Add(new PeakInfo(freqs[i], mags[i], i));
            }
        }

        return peaks.OrderByDescending(p => p.Magnitude).Take(maxPeaks).ToList();
    }
}

public enum SpectrumType
{
    Magnitude,
    Power,
    PowerSpectralDensity
}

public sealed record SpectrumResult(
    double[] Frequencies,
    double[] Values,
    SpectrumType Type,
    double SampleRate,
    int FftSize
);

public sealed record PeakInfo(
    double Frequency,
    double Magnitude,
    int BinIndex
);
