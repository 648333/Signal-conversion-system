namespace DH.SignalProcessing;

public enum WindowType
{
    None, Hanning, Hamming, Blackman, BlackmanHarris, FlatTop, Rectangle, Triangle, Kaiser
}

public static class WindowFunctions
{
    public static double[] Generate(WindowType type, int length)
    {
        var window = new double[length];
        Apply(type, window);
        return window;
    }

    public static void Apply(WindowType type, double[] window)
    {
        var n = window.Length;
        if (n <= 1) return;
        var nMinus1 = n - 1;
        for (int i = 0; i < n; i++)
        {
            window[i] = type switch
            {
                WindowType.None or WindowType.Rectangle => 1.0,
                WindowType.Hanning => 0.5 * (1 - Math.Cos(2 * Math.PI * i / nMinus1)),
                WindowType.Hamming => 0.54 - 0.46 * Math.Cos(2 * Math.PI * i / nMinus1),
                WindowType.Blackman => 0.42 - 0.5 * Math.Cos(2 * Math.PI * i / nMinus1) + 0.08 * Math.Cos(4 * Math.PI * i / nMinus1),
                WindowType.BlackmanHarris => 0.35875 - 0.48829 * Math.Cos(2 * Math.PI * i / nMinus1) + 0.14128 * Math.Cos(4 * Math.PI * i / nMinus1) - 0.01168 * Math.Cos(6 * Math.PI * i / nMinus1),
                WindowType.FlatTop => 0.21557895 - 0.41663158 * Math.Cos(2 * Math.PI * i / nMinus1) + 0.27726316 * Math.Cos(4 * Math.PI * i / nMinus1) - 0.08357895 * Math.Cos(6 * Math.PI * i / nMinus1) + 0.00694737 * Math.Cos(8 * Math.PI * i / nMinus1),
                WindowType.Triangle => 1.0 - Math.Abs(2.0 * i / nMinus1 - 1.0),
                _ => 1.0
            };
        }
    }

    public static float[] ApplyToSignal(float[] signal, WindowType windowType)
    {
        var window = Generate(windowType, signal.Length);
        var result = new float[signal.Length];
        for (int i = 0; i < signal.Length; i++)
            result[i] = (float)(signal[i] * window[i]);
        return result;
    }

    public static double GetAmplitudeCorrectionFactor(WindowType type) => type switch
    {
        WindowType.None or WindowType.Rectangle => 1.0,
        WindowType.Hanning => 2.0,
        WindowType.Hamming => 1.0 / 0.54,
        WindowType.Blackman => 1.0 / 0.42,
        WindowType.BlackmanHarris => 1.0 / 0.35875,
        WindowType.FlatTop => 1.0 / 0.21557895,
        WindowType.Triangle => 2.0,
        _ => 1.0
    };

    public static double GetEnergyCorrectionFactor(WindowType type, int length)
    {
        var window = Generate(type, length);
        var sumSquares = 0.0;
        var sum = 0.0;
        for (int i = 0; i < length; i++)
        {
            sumSquares += window[i] * window[i];
            sum += window[i];
        }
        return sum != 0 ? length * sum / (sumSquares * length) : 1.0;
    }
}
