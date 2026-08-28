using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;

namespace DH.Visualization;

public sealed class RecorderChart : FrameworkElement, IChartView
{
    private readonly List<ChannelWaveform> _channels = new();
    private bool _frozen;
    private int _maxPoints = 2000;
    private double _yMin = -10000;
    private double _yMax = 10000;

    public Guid Id { get; } = Guid.NewGuid();
    public string Title { get; set; } = "记录仪";
    public ChartType ChartType => ChartType.Recorder;
    public bool IsFrozen => _frozen;

    private string _statusText = string.Empty;
    public string StatusText
    {
        get => _statusText;
        set
        {
            _statusText = value;
            InvalidateVisual();
        }
    }

    public void AddChannel(int channelId, string name, Color color)
    {
        _channels.Add(new ChannelWaveform
        {
            ChannelId = channelId,
            Name = name,
            Color = color,
            Data = new Queue<float>(_maxPoints)
        });
    }

    public void RemoveChannel(int channelId)
    {
        _channels.RemoveAll(c => c.ChannelId == channelId);
    }

    public void SetMaxPoints(int maxPoints)
    {
        _maxPoints = maxPoints;
        foreach (var ch in _channels)
        {
            while (ch.Data.Count > maxPoints)
                ch.Data.Dequeue();
        }
    }

    public void UpdateData(int channelId, float[] data)
    {
        if (_frozen)
            return;

        var ch = _channels.FirstOrDefault(c => c.ChannelId == channelId);
        if (ch == null)
            return;

        foreach (var v in data)
        {
            ch.Data.Enqueue(v);
            while (ch.Data.Count > _maxPoints)
                ch.Data.Dequeue();

            if (v < _yMin) _yMin = v;
            if (v > _yMax) _yMax = v;
        }

        InvalidateVisual();
    }

    public void UpdateData(float[] interleavedData, int channelCount)
    {
        if (_frozen || channelCount <= 0)
            return;

        var samples = interleavedData.Length / channelCount;
        for (int ch = 0; ch < channelCount; ch++)
        {
            if (ch >= _channels.Count)
                continue;

            for (int s = 0; s < samples; s++)
            {
                var val = interleavedData[s * channelCount + ch];
                _channels[ch].Data.Enqueue(val);
                while (_channels[ch].Data.Count > _maxPoints)
                    _channels[ch].Data.Dequeue();

                if (val < _yMin) _yMin = val;
                if (val > _yMax) _yMax = val;
            }
        }

        InvalidateVisual();
    }

    public void Clear()
    {
        foreach (var ch in _channels)
            ch.Data.Clear();
        _yMin = -10000;
        _yMax = 10000;
        InvalidateVisual();
    }

    public void Freeze()
    {
        _frozen = true;
    }

    public void Unfreeze()
    {
        _frozen = false;
    }

    public void SetFrozen(bool frozen)
    {
        _frozen = frozen;
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        var renderSize = RenderSize;
        var bg = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x2A));
        dc.DrawRectangle(bg, null, new Rect(renderSize));

        var gridPen = new Pen(new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x3A)), 1);
        var centerX = renderSize.Width / 2;
        var centerY = renderSize.Height / 2;

        for (int i = 0; i <= 10; i++)
        {
            var y = i * renderSize.Height / 10;
            dc.DrawLine(gridPen, new Point(0, y), new Point(renderSize.Width, y));
        }
        for (int i = 0; i <= 20; i++)
        {
            var x = i * renderSize.Width / 20;
            dc.DrawLine(gridPen, new Point(x, 0), new Point(x, renderSize.Height));
        }

        dc.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x5A)), 1),
            new Point(0, centerY), new Point(renderSize.Width, centerY));

        if (_channels.Count == 0)
        {
            var ft = new FormattedText("等待数据...", System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, new Typeface("Segoe UI"), 14,
                new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66)), 1.0);
            dc.DrawText(ft, new Point(centerX - ft.Width / 2, centerY - ft.Height / 2));
            return;
        }

        var yRange = _yMax - _yMin;
        if (yRange < 1) yRange = 1;

        var channelHeight = renderSize.Height / _channels.Count;

        for (int i = 0; i < _channels.Count; i++)
        {
            var ch = _channels[i];
            var yOffset = i * channelHeight;
            var midY = yOffset + channelHeight / 2;

            if (ch.Data.Count < 2)
                continue;

            var stepX = renderSize.Width / Math.Max(1, ch.Data.Count - 1);
            var points = new List<Point>();
            var idx = 0;
            foreach (var val in ch.Data)
            {
                var x = idx * stepX;
                var normalized = (val - _yMin) / yRange;
                var y = midY - (normalized - 0.5) * channelHeight * 0.8;
                points.Add(new Point(x, y));
                idx++;
            }

            if (points.Count >= 2)
            {
                var geometry = new StreamGeometry();
                using (var ctx = geometry.Open())
                {
                    ctx.BeginFigure(points[0], false, false);
                    for (int p = 1; p < points.Count; p++)
                        ctx.LineTo(points[p], true, false);
                }
                geometry.Freeze();

                var pen = new Pen(new SolidColorBrush(ch.Color), 1.2);
                dc.DrawGeometry(null, pen, geometry);
            }

            var labelFT = new FormattedText($"{ch.Name}",
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, new Typeface("Segoe UI"), 11,
                new SolidColorBrush(ch.Color), 1.0);
            dc.DrawText(labelFT, new Point(8, yOffset + 4));
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        return new Size(availableSize.Width, availableSize.Height);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

internal sealed class ChannelWaveform
{
    public int ChannelId { get; set; }
    public string Name { get; set; } = string.Empty;
    public Color Color { get; set; }
    public Queue<float> Data { get; set; } = new();
}
