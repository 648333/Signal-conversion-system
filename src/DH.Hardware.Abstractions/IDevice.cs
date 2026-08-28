using DH.Core.Models;

namespace DH.Hardware;

/// <summary>
/// 数据采集设备接口：所有硬件设备实现此接口
/// </summary>
public interface IDevice : IDisposable
{
    DeviceInfo Info { get; }

    bool Connect();
    bool Disconnect();
    bool IsConnected { get; }

    int GetChannelCount();
    double GetMaxSampleRate();
    double GetCurrentSampleRate();
    bool SetSampleRate(double frequency);

    bool StartAcquisition();
    bool StopAcquisition();
    bool IsAcquiring { get; }

    int ReadData(float[] buffer, int offset, int count);
    event EventHandler<DataAvailableEventArgs>? DataAvailable;

    string GetLastError();
}

public sealed class DataAvailableEventArgs : EventArgs
{
    public float[] Data { get; init; } = Array.Empty<float>();
    public int ChannelId { get; init; }
    public int SamplesRead { get; init; }
    public double Timestamp { get; init; }
}
