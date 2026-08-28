using DH.Core.Models;

namespace DH.Hardware;

public sealed class SimulatedDevice : IDevice
{
    private readonly DeviceInfo _info;
    private bool _connected;
    private bool _acquiring;
    private double _sampleRate = 1000;
    private double _phase;
    private readonly Random _rng = new();
    private readonly System.Timers.Timer _timer;
    private readonly object _lock = new();

    public SimulatedDevice(DeviceInfo info)
    {
        _info = info;
        _timer = new System.Timers.Timer(50);
        _timer.Elapsed += OnTimerElapsed;
    }

    public DeviceInfo Info => _info;
    public bool IsConnected => _connected;
    public bool IsAcquiring => _acquiring;

    public event EventHandler<DataAvailableEventArgs>? DataAvailable;

    public bool Connect()
    {
        _connected = true;
        _info.Status = DeviceStatus.Connected;
        return true;
    }

    public bool Disconnect()
    {
        StopAcquisition();
        _connected = false;
        _info.Status = DeviceStatus.Offline;
        return true;
    }

    public int GetChannelCount() => _info.ChannelCount;
    public double GetMaxSampleRate() => _info.MaxSampleRate;
    public double GetCurrentSampleRate() => _sampleRate;

    public bool SetSampleRate(double frequency)
    {
        if (_acquiring)
            return false;
        if (frequency < 1 || frequency > _info.MaxSampleRate)
            return false;
        _sampleRate = frequency;
        var interval = 1000.0 / Math.Max(1, _sampleRate / 500);
        _timer.Interval = Math.Clamp(interval, 10, 500);
        return true;
    }

    public bool StartAcquisition()
    {
        if (!_connected || _acquiring)
            return false;
        _acquiring = true;
        _info.Status = DeviceStatus.Acquiring;
        _phase = 0;
        _timer.Start();
        return true;
    }

    public bool StopAcquisition()
    {
        if (!_acquiring)
            return false;
        _timer.Stop();
        _acquiring = false;
        _info.Status = DeviceStatus.Connected;
        return true;
    }

    public int ReadData(float[] buffer, int offset, int count)
    {
        lock (_lock)
        {
            var toRead = Math.Min(count, buffer.Length - offset);
            for (int i = 0; i < toRead; i++)
            {
                buffer[offset + i] = (float)(_rng.NextDouble() * 2 - 1);
            }
            return toRead;
        }
    }

    private void OnTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        if (!_acquiring)
            return;

        var samplesPerChannel = (int)(_sampleRate * _timer.Interval / 1000.0);
        if (samplesPerChannel < 1) samplesPerChannel = 1;

        var totalSamples = samplesPerChannel * _info.ChannelCount;
        var data = new float[totalSamples];
        var dt = 1.0 / _sampleRate;

        for (int s = 0; s < samplesPerChannel; s++)
        {
            for (int ch = 0; ch < _info.ChannelCount; ch++)
            {
                var freq = 10.0 * (ch + 1);
                var amp = 5000.0 / (ch + 1);
                var noise = (_rng.NextDouble() - 0.5) * 200;
                data[s * _info.ChannelCount + ch] = (float)(amp * Math.Sin(2 * Math.PI * freq * _phase) + noise);
                _phase += dt / _info.ChannelCount;
            }
        }

        DataAvailable?.Invoke(this, new DataAvailableEventArgs
        {
            Data = data,
            ChannelId = -1,
            SamplesRead = totalSamples,
            Timestamp = (DateTime.Now - DateTime.UnixEpoch).TotalSeconds
        });
    }

    public string GetLastError() => string.Empty;

    public void Dispose()
    {
        _timer.Dispose();
        GC.SuppressFinalize(this);
    }
}
