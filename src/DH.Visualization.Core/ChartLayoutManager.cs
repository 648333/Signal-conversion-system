using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DH.Visualization;

public sealed class ChartLayoutManager : INotifyPropertyChanged
{
    private LayoutMode _layoutMode = LayoutMode.Single;

    public ObservableCollection<IChartView> Charts { get; } = new();

    public LayoutMode LayoutMode
    {
        get => _layoutMode;
        set => SetField(ref _layoutMode, value);
    }

    public void AddChart(IChartView chart) => Charts.Add(chart);
    public void RemoveChart(IChartView chart) => Charts.Remove(chart);
    public void ClearAll() => Charts.Clear();
    public void ArrangeHorizontal() => LayoutMode = LayoutMode.Horizontal;
    public void ArrangeVertical() => LayoutMode = LayoutMode.Vertical;
    public void ArrangeGrid() => LayoutMode = LayoutMode.Grid;

    public event PropertyChangedEventHandler? PropertyChanged;
    private void SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

public enum LayoutMode { Single, Horizontal, Vertical, Grid, Custom }
