using System.ComponentModel;

namespace DH.Visualization;

public enum ChartType
{
    Recorder,
    Oscilloscope,
    TwoD,
    ThreeD,
    FFT,
    Octave,
    Polar,
    XYRecorder,
    XYPackage,
    Digital,
    Meter,
    Bar,
    MultiBar,
    Table,
    StatTable,
    Media,
    Scatter,
    Wavelet,
    Order,
    Balancer,
    NormalModes,
    Model,
    Cloud3D,
    PathTrack3D,
    GpsRoute,
    MachineStatus
}

public interface IChartView : INotifyPropertyChanged
{
    Guid Id { get; }
    string Title { get; set; }
    ChartType ChartType { get; }
    void UpdateData(int channelId, float[] data);
    void Clear();
    void Freeze();
    void Unfreeze();
}

public interface IChartFactory
{
    ChartType SupportedType { get; }
    IChartView Create();
}
