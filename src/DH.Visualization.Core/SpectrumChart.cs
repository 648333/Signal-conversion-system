using System.Windows;
using System.Windows.Media;
using DH.SignalProcessing;

namespace DH.Visualization;

/// <summary>
/// FFT 频谱图控件：在 WPF Canvas 上绘制频域幅值谱
/// 支持对数/线性坐标、峰值标注、多通道叠加
/// </summary>
public sealed class SpectrumChart : FrameworkElement, IChartView
{
    private readonly DrawingVisual _visual = new();
    private SpectrumResult? _spectrum;
    private List<SpectrumResult> _multiSpectrum = new();
    private List<Color> _channelColors = new();
    private bool _frozen;
    private string _title = "FFT 频谱";
    private static readonly Color[] DefaultColors =
    {
        Color.FromRgb(0x4C, 0xAF, 0x50),
        Color.FromRgb(0x21, 0x96, 0xF3),
        Color.FromRgb(0xFF, 0x98, 0x00),
        Color.FromRgb(0xE9, 0x1E, 0x63),
        Color.FromRgb(0x9C, 0x27, 0xB0),
        Color.FromRgb(0x00, 0xBC, 0xD4),
        Color.FromRgb(0xFF, 0xEB, 0x3B),
        Color.FromRgb(0x79, 0x86, 0xCB),
    };

    public Guid Id { get; } = Guid.NewGuid();
    public string Title
    {
        get => _title;
        set => _title = value;
    }
    public ChartType ChartType => ChartType.FFT;

    public bool LogScaleX { get; set; } = false;
    public bool LogScaleY { get; set; } = true;
    public double YMin { get; set; } = -120;
    public double YMax { get; set; } = 10;
    public bool ShowPeaks { get; set; } = true;
    public int MaxPeakCount { get; set; } = 5;
    public double PeakThreshold { get; set; } = 0.1;
    public bool ShowGrid { get; set; } = true;
    public string XLabel { get; set; } = "频率 (Hz)";
    public string YLabel { get; set; } = "幅值 (dB)";

    private static readonly Pen GridPen = new(new SolidColorBrush(Color.FromArgb(40, 128, 128, 128))) { Thickness = 0.5 };
    private static readonly Pen AxisPen = new(new SolidColorBrush(Color.FromArgb(180, 200, 200, 200))) { Thickness = 1 };
    private static readonly SolidColorBrush BgBrush = new(Color.FromRgb(0x1E, 0x1E, 0x2E));
    private static readonly SolidColorBrush TextBrush = new(Color.FromRgb(0xCC, 0xCC, 0xCC));

    static SpectrumChart()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(SpectrumChart),
            new FrameworkPropertyMetadata(typeof(FrameworkElement)));
    }

    public SpectrumChart()
    {
        AddVisualChild(_visual);
        AddLogicalChild(_visual);
    }

    protected override int VisualChildrenCount => 1;
    protected override Visual GetVisualChild(int index) => _visual;

    public void SetSpectrum(SpectrumResult spectrum, int channelIndex = 0)
    {
        if (_frozen) return;

        while (_multiSpectrum.Count <= channelIndex)
            _multiSpectrum.Add(null!);

        _multiSpectrum[channelIndex] = spectrum;

        while (_channelColors.Count <= channelIndex)
            _channelColors.Add(DefaultColors[(_channelColors.Count) % DefaultColors.Length]);

        InvalidateVisual();
    }

    public void UpdateData(int channelId, float[] data)
    {
        if (_frozen || data.Length == 0) return;

        var analyzer = new SpectrumAnalyzer { WindowType = WindowType.Hanning };
        var spectrum = analyzer.ComputeMagnitudeSpectrum(data, 1000);
        SetSpectrum(spectrum, channelId);
    }

    public void Clear()
    {
        _multiSpectrum.Clear();
        _spectrum = null;
        InvalidateVisual();
    }

    public void Freeze() => _frozen = true;
    public void Unfreeze() => _frozen = false;

    protected override void OnRender(DrawingContext drawingContext)
    {
        var renderSize = RenderSize;
        if (renderSize.Width < 10 || renderSize.Height < 10)
            return;

        var dc = _visual.RenderOpen();
        var w = renderSize.Width;
        var h = renderSize.Height;

        var margin = new Thickness(60, 40, 20, 40);
        var plotArea = new Rect(margin.Left, margin.Top,
            w - margin.Left - margin.Right,
            h - margin.Top - margin.Bottom);

        dc.DrawRectangle(BgBrush, null, new Rect(0, 0, w, h));

        if (ShowGrid)
            DrawGrid(dc, plotArea);

        DrawAxes(dc, plotArea);

        for (int ch = 0; ch < _multiSpectrum.Count; ch++)
        {
            if (_multiSpectrum[ch] == null) continue;
            DrawSpectrumLine(dc, plotArea, _multiSpectrum[ch], _channelColors[ch]);
        }

        if (ShowPeaks && _multiSpectrum.Count > 0 && _multiSpectrum[0] != null)
            DrawPeaks(dc, plotArea, _multiSpectrum[0]);

        dc.DrawText(new FormattedText(Title,
            System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI"), 14, TextBrush,
            VisualTreeHelper.GetDpi(this).PixelsPerDip),
            new Point(plotArea.Left + (plotArea.Width - 100) / 2, 8));

        dc.Close();
    }

    private void DrawGrid(DrawingContext dc, Rect area)
    {
        var xDivs = 10;
        var yDivs = 8;

        for (int i = 0; i <= xDivs; i++)
        {
            var x = area.Left + area.Width * i / xDivs;
            dc.DrawLine(GridPen, new Point(x, area.Top), new Point(x, area.Bottom));
        }

        for (int i = 0; i <= yDivs; i++)
        {
            var y = area.Top + area.Height * i / yDivs;
            dc.DrawLine(GridPen, new Point(area.Left, y), new Point(area.Right, y));
        }
    }

    private void DrawAxes(DrawingContext dc, Rect area)
    {
        dc.DrawLine(AxisPen, new Point(area.Left, area.Top), new Point(area.Left, area.Bottom));
        dc.DrawLine(AxisPen, new Point(area.Left, area.Bottom), new Point(area.Right, area.Bottom));

        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;

        var xDivs = 5;
        for (int i = 0; i <= xDivs; i++)
        {
            var x = area.Left + area.Width * i / xDivs;
            var freq = GetMaxFreq();
            var label = (freq * i / xDivs).ToString("F0");
            if (LogScaleX && i > 0)
                label = Math.Pow(freq, (double)i / xDivs).ToString("F0");

            dc.DrawText(new FormattedText(label,
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, new Typeface("Consolas"), 10, TextBrush, dpi),
                new Point(x - 15, area.Bottom + 4));
        }

        var yDivs = 4;
        for (int i = 0; i <= yDivs; i++)
        {
            var y = area.Bottom - area.Height * i / yDivs;
            var value = YMin + (YMax - YMin) * i / yDivs;
            dc.DrawText(new FormattedText($"{value:F0}",
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, new Typeface("Consolas"), 10, TextBrush, dpi),
                new Point(area.Left - 35, y - 7));
        }

        var dpi2 = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        dc.DrawText(new FormattedText(XLabel,
            System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight, new Typeface("Segoe UI"), 11, TextBrush, dpi2),
            new Point(area.Left + area.Width / 2 - 30, area.Bottom + 18));

        dc.PushTransform(new RotateTransform(-90));
        dc.DrawText(new FormattedText(YLabel,
            System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight, new Typeface("Segoe UI"), 11, TextBrush, dpi2),
            new Point(-area.Top - area.Height / 2 - 30, 8));
        dc.Pop();
    }

    private void DrawSpectrumLine(DrawingContext dc, Rect area, SpectrumResult spectrum, Color color)
    {
        var freqs = spectrum.Frequencies;
        var mags = spectrum.Values;
        if (freqs.Length == 0) return;

        var maxFreq = GetMaxFreq();
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            bool started = false;
            Point? lastPoint = null;

            for (int i = 0; i < freqs.Length; i++)
            {
                if (freqs[i] > maxFreq) break;

                var xNorm = LogScaleX && freqs[i] > 0
                    ? Math.Log10(freqs[i]) / Math.Log10(maxFreq)
                    : freqs[i] / maxFreq;

                var mag = mags[i];
                if (LogScaleY && mag > 0)
                    mag = 20 * Math.Log10(mag);
                else if (LogScaleY)
                    mag = YMin;

                var yNorm = (mag - YMin) / (YMax - YMin);
                yNorm = Math.Clamp(yNorm, 0, 1);

                var x = area.Left + xNorm * area.Width;
                var y = area.Bottom - yNorm * area.Height;

                if (!started)
                {
                    ctx.BeginFigure(new Point(x, area.Bottom), false, false);
                    started = true;
                }

                ctx.LineTo(new Point(x, y), true, false);
                lastPoint = new Point(x, y);
            }

            if (started && lastPoint.HasValue)
            {
                ctx.LineTo(new Point(lastPoint.Value.X, area.Bottom), true, false);
            }
        }

        var brush = new SolidColorBrush(Color.FromArgb(180, color.R, color.G, color.B));
        var pen = new Pen(new SolidColorBrush(color), 1.2);
        dc.DrawGeometry(brush, pen, geometry);
    }

    private void DrawPeaks(DrawingContext dc, Rect area, SpectrumResult spectrum)
    {
        var peaks = SpectrumAnalyzer.FindPeaks(spectrum, MaxPeakCount, PeakThreshold);
        var maxFreq = GetMaxFreq();
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;

        foreach (var peak in peaks)
        {
            if (peak.Frequency > maxFreq) continue;

            var xNorm = LogScaleX && peak.Frequency > 0
                ? Math.Log10(peak.Frequency) / Math.Log10(maxFreq)
                : peak.Frequency / maxFreq;

            var mag = peak.Magnitude;
            if (LogScaleY && mag > 0)
                mag = 20 * Math.Log10(mag);
            else if (LogScaleY)
                mag = YMin;

            var yNorm = (mag - YMin) / (YMax - YMin);
            yNorm = Math.Clamp(yNorm, 0, 1);

            var x = area.Left + xNorm * area.Width;
            var y = area.Bottom - yNorm * area.Height;

            dc.DrawEllipse(Brushes.Yellow, new Pen(Brushes.Orange, 1), new Point(x, y), 4, 4);

            var label = $"{peak.Frequency:F1} Hz";
            dc.DrawText(new FormattedText(label,
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, new Typeface("Consolas"), 9, Brushes.Yellow, dpi),
                new Point(x + 6, y - 8));
        }
    }

    private double GetMaxFreq()
    {
        if (_multiSpectrum.Count > 0 && _multiSpectrum[0] != null)
        {
            var freqs = _multiSpectrum[0].Frequencies;
            if (freqs.Length > 0)
                return freqs[freqs.Length - 1];
        }
        return 1000;
    }
}
