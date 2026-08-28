namespace DH.SignalProcessing;

public enum FilterType { LowPass, HighPass, BandPass, BandStop }
public enum FilterDesign { Butterworth, Bessel, Chebyshev1, Chebyshev2 }

public sealed class DigitalFilter : ISignalProcessor
{
    private readonly List<BiquadSection> _sections = new();
    private readonly FilterType _filterType;
    private readonly FilterDesign _design;
    private readonly double _sampleRate;
    private readonly double _cutoffFreq;
    private readonly double _cutoffFreq2;
    private readonly int _order;

    public string Name => $"{_design} {_filterType} Order{_order}";
    public ProcessingNodeType NodeType => ProcessingNodeType.Filter;

    public DigitalFilter(FilterType filterType, double sampleRate, double cutoffFreq, int order = 4, double cutoffFreq2 = 0, FilterDesign design = FilterDesign.Butterworth)
    {
        _filterType = filterType; _design = design; _sampleRate = sampleRate;
        _cutoffFreq = cutoffFreq; _cutoffFreq2 = cutoffFreq2; _order = order;
        Design();
    }

    private void Design()
    {
        _sections.Clear();
        var pairs = _order / 2;
        for (int p = 0; p < pairs; p++)
        {
            var (b0, b1, b2, a0, a1, a2) = DesignButterworthSection(p, pairs);
            _sections.Add(new BiquadSection(b0 / a0, b1 / a0, b2 / a0, a1 / a0, a2 / a0));
        }
        if (_order % 2 == 1)
        {
            var (b0, b1, b2, a0, a1, a2) = DesignFirstOrderSection();
            _sections.Add(new BiquadSection(b0 / a0, b1 / a0, b2 / a0, a1 / a0, a2 / a0));
        }
    }

    private (double, double, double, double, double, double) DesignButterworthSection(int p, int pairs)
    {
        var wc = 2 * Math.PI * _cutoffFreq / _sampleRate;
        var wc2 = _cutoffFreq2 > 0 ? 2 * Math.PI * _cutoffFreq2 / _sampleRate : 0;
        var angle = Math.PI * (2 * p + 1) / (2 * pairs);
        var real = -Math.Cos(angle);
        return _filterType switch
        {
            FilterType.LowPass => DesignLowPass(wc, real),
            FilterType.HighPass => DesignHighPass(wc, real),
            FilterType.BandPass => DesignBandPass(wc, wc2, real),
            FilterType.BandStop => DesignBandStop(wc, wc2, real),
            _ => DesignLowPass(wc, real)
        };
    }

    private (double, double, double, double, double, double) DesignLowPass(double wc, double real)
    {
        var k = Math.Tan(wc / 2); var k2 = k * k;
        var norm = 1 + 2 * k * real + k2;
        return (k2 / norm, 2 * k2 / norm, k2 / norm, 1, 2 * (k2 - 1) / norm, (1 - 2 * k * real + k2) / norm);
    }

    private (double, double, double, double, double, double) DesignHighPass(double wc, double real)
    {
        var k = Math.Tan(wc / 2); var k2 = k * k;
        var norm = 1 + 2 * k * real + k2;
        return (1 / norm, -2 / norm, 1 / norm, 1, 2 * (k2 - 1) / norm, (1 - 2 * k * real + k2) / norm);
    }

    private (double, double, double, double, double, double) DesignBandPass(double wc, double wc2, double real)
    {
        var w0 = 2 * Math.PI * Math.Sqrt(_cutoffFreq * _cutoffFreq2) / _sampleRate;
        var k = Math.Tan((wc2 - wc) / 2); var k2 = k * k; var w0cos = Math.Cos(w0);
        var norm = 1 + 2 * k * real + k2;
        return (k / norm, 0, -k / norm, 1, (-2 * w0cos * (1 + k2)) / norm, (1 - 2 * k * real + k2) / norm);
    }

    private (double, double, double, double, double, double) DesignBandStop(double wc, double wc2, double real)
    {
        var w0 = 2 * Math.PI * Math.Sqrt(_cutoffFreq * _cutoffFreq2) / _sampleRate;
        var k = Math.Tan((wc2 - wc) / 2); var k2 = k * k; var w0cos = Math.Cos(w0); var w0cos2 = w0cos * w0cos;
        var norm = 1 + 2 * k * real + k2;
        return ((1 + k2) / norm, -2 * w0cos * (1 + k2) / norm, (1 + k2) / norm, 1, -2 * w0cos * (1 + k2) / norm, (1 - 2 * k * real + k2 - 4 * w0cos2 * k) / norm);
    }

    private (double, double, double, double, double, double) DesignFirstOrderSection()
    {
        var k = Math.Tan(Math.PI * _cutoffFreq / _sampleRate);
        return _filterType switch
        {
            FilterType.LowPass => (k, 0, 0, 1, k - 1, 0),
            FilterType.HighPass => (1, -1, 0, 1, k - 1, 0),
            _ => (k, 0, 0, 1, k - 1, 0)
        };
    }

    public float[] Process(float[] input)
    {
        var output = new float[input.Length];
        for (int i = 0; i < input.Length; i++)
        {
            var sample = input[i];
            foreach (var section in _sections) sample = section.Process(sample);
            output[i] = sample;
        }
        return output;
    }

    public void Reset() { foreach (var s in _sections) s.Reset(); }
}

internal sealed class BiquadSection
{
    private double _z1, _z2;
    private readonly double _b0, _b1, _b2, _a1, _a2;
    public BiquadSection(double b0, double b1, double b2, double a1, double a2)
    { _b0 = b0; _b1 = b1; _b2 = b2; _a1 = a1; _a2 = a2; }
    public float Process(float input)
    {
        var x = (double)input;
        var y = _b0 * x + _z1;
        _z1 = _b1 * x - _a1 * y + _z2;
        _z2 = _b2 * x - _a2 * y;
        return (float)y;
    }
    public void Reset() { _z1 = _z2 = 0; }
}
