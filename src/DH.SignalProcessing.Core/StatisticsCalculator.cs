namespace DH.SignalProcessing;

/// <summary>
/// 信号统计计算器：RMS、峰值、峰峰值、均值、标准差、波峰因数、偏度、峭度
/// </summary>
public static class StatisticsCalculator
{
    public static SignalStatistics Compute(float[] data)
    {
        if (data.Length == 0)
            return new SignalStatistics(0, 0, 0, 0, 0, 0, 0, 0, 0);

        var n = data.Length;
        var sum = 0.0;
        var min = double.MaxValue;
        var max = double.MinValue;
        var sumSquares = 0.0;

        for (int i = 0; i < n; i++)
        {
            var v = (double)data[i];
            sum += v;
            sumSquares += v * v;
            if (v < min) min = v;
            if (v > max) max = v;
        }

        var mean = sum / n;
        var rms = Math.Sqrt(sumSquares / n);
        var peak = Math.Max(Math.Abs(max), Math.Abs(min));
        var peakToPeak = max - min;

        var variance = sumSquares / n - mean * mean;
        if (variance < 0) variance = 0;
        var stdDev = Math.Sqrt(variance);

        var crestFactor = rms > 1e-12 ? peak / rms : 0;

        var sumCubedDev = 0.0;
        var sumFourthDev = 0.0;
        for (int i = 0; i < n; i++)
        {
            var dev = (double)data[i] - mean;
            var dev2 = dev * dev;
            sumCubedDev += dev * dev2;
            sumFourthDev += dev2 * dev2;
        }

        var skewness = n > 0 && variance > 1e-12
            ? (sumCubedDev / n) / (variance * Math.Sqrt(variance))
            : 0;
        var kurtosis = n > 0 && variance > 1e-12
            ? (sumFourthDev / n) / (variance * variance) - 3.0
            : 0;

        return new SignalStatistics(
            Mean: mean,
            Rms: rms,
            Peak: peak,
            PeakToPeak: peakToPeak,
            StdDev: stdDev,
            Variance: variance,
            CrestFactor: crestFactor,
            Skewness: skewness,
            Kurtosis: kurtosis
        );
    }

    public static double ComputeRMS(float[] data)
    {
        if (data.Length == 0) return 0;
        var sumSquares = 0.0;
        for (int i = 0; i < data.Length; i++)
            sumSquares += (double)data[i] * data[i];
        return Math.Sqrt(sumSquares / data.Length);
    }

    public static double ComputePeak(float[] data)
    {
        if (data.Length == 0) return 0;
        var max = 0.0;
        for (int i = 0; i < data.Length; i++)
        {
            var abs = Math.Abs(data[i]);
            if (abs > max) max = abs;
        }
        return max;
    }
}

public sealed record SignalStatistics(
    double Mean,
    double Rms,
    double Peak,
    double PeakToPeak,
    double StdDev,
    double Variance,
    double CrestFactor,
    double Skewness,
    double Kurtosis
);
