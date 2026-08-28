using System.Numerics;

namespace DH.SignalProcessing;

public static class FourierTransform
{
    public static void Forward(Complex[] data)
    {
        var n = data.Length;
        if (!IsPowerOfTwo(n))
            throw new ArgumentException($"FFT length must be power of 2, got {n}");
        BitReverse(data);
        Butterfly(data, forward: true);
    }

    public static void Inverse(Complex[] data)
    {
        var n = data.Length;
        if (!IsPowerOfTwo(n))
            throw new ArgumentException($"IFFT length must be power of 2, got {n}");
        for (int i = 0; i < n; i++)
            data[i] = Complex.Conjugate(data[i]);
        BitReverse(data);
        Butterfly(data, forward: true);
        for (int i = 0; i < n; i++)
            data[i] = Complex.Conjugate(data[i]) / n;
    }

    public static (double[] frequencies, double[] magnitudes) MagnitudeSpectrum(float[] input, double sampleRate)
    {
        var n = NextPowerOfTwo(input.Length);
        var complex = new Complex[n];
        for (int i = 0; i < input.Length; i++)
            complex[i] = new Complex(input[i], 0);
        Forward(complex);
        var halfN = n / 2;
        var freqs = new double[halfN];
        var mags = new double[halfN];
        var binWidth = sampleRate / n;
        for (int i = 0; i < halfN; i++)
        {
            freqs[i] = i * binWidth;
            mags[i] = complex[i].Magnitude * 2.0 / input.Length;
        }
        mags[0] /= 2.0;
        return (freqs, mags);
    }

    public static (double[] frequencies, double[] psd) PowerSpectralDensity(float[] input, double sampleRate)
    {
        var n = NextPowerOfTwo(input.Length);
        var complex = new Complex[n];
        for (int i = 0; i < input.Length; i++)
            complex[i] = new Complex(input[i], 0);
        Forward(complex);
        var halfN = n / 2;
        var freqs = new double[halfN];
        var psd = new double[halfN];
        var binWidth = sampleRate / n;
        for (int i = 0; i < halfN; i++)
        {
            freqs[i] = i * binWidth;
            psd[i] = complex[i].Magnitude * complex[i].Magnitude / (sampleRate * n);
        }
        return (freqs, psd);
    }

    private static void BitReverse(Complex[] data)
    {
        var n = data.Length;
        for (int i = 1, j = 0; i < n; i++)
        {
            var bit = n >> 1;
            for (; (j & bit) != 0; bit >>= 1)
                j ^= bit;
            j ^= bit;
            if (i < j)
                (data[i], data[j]) = (data[j], data[i]);
        }
    }

    private static void Butterfly(Complex[] data, bool forward)
    {
        var n = data.Length;
        var sign = forward ? -1.0 : 1.0;
        for (var len = 2; len <= n; len <<= 1)
        {
            var halfLen = len >> 1;
            var angle = sign * Math.PI * 2 / len;
            var wLen = new Complex(Math.Cos(angle), Math.Sin(angle));
            for (var i = 0; i < n; i += len)
            {
                var w = Complex.One;
                for (var j = 0; j < halfLen; j++)
                {
                    var u = data[i + j];
                    var v = data[i + j + halfLen] * w;
                    data[i + j] = u + v;
                    data[i + j + halfLen] = u - v;
                    w *= wLen;
                }
            }
        }
    }

    public static bool IsPowerOfTwo(int n) => n > 0 && (n & (n - 1)) == 0;

    public static int NextPowerOfTwo(int n)
    {
        if (n <= 1) return 1;
        var p = 1;
        while (p < n) p <<= 1;
        return p;
    }
}
