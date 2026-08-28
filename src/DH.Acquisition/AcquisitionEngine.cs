using System.ComponentModel;
using System.Runtime.CompilerServices;
using DH.Core.Models;

namespace DH.Acquisition;

public sealed class AcquisitionEngine : INotifyPropertyChanged
{
    private AcquisitionState _state = AcquisitionState.Idle;
    private DateTime _startTime;
    private long _totalSamples;
    private readonly RingBuffer<float> _ringBuffer;

    public const int BufferCapacity = 1024 * 1024 * 64;

    public AcquisitionState State
    {
        get => _state;
        private set => SetField(ref _state, value);
    }

    public double SampleRate { get; set; } = 1000;
    public int ChannelCount { get; set; } = 1;
    public DateTime StartTime => _startTime;
    public long TotalSamples => _totalSamples;
    public double ElapsedSeconds => _state == AcquisitionState.Acquiring
        ? (DateTime.Now - _startTime).TotalSeconds : 0;

    public AcquisitionEngine()
    {
        _ringBuffer = new RingBuffer<float>(BufferCapacity);
    }

    public bool Start()
    {
        if (_state is AcquisitionState.Acquiring or AcquisitionState.Paused)
            return false;

        _startTime = DateTime.Now;
        _totalSamples = 0;
        State = AcquisitionState.Acquiring;
        return true;
    }

    public bool Pause()
    {
        if (_state != AcquisitionState.Acquiring)
            return false;
        State = AcquisitionState.Paused;
        return true;
    }

    public bool Resume()
    {
        if (_state != AcquisitionState.Paused)
            return false;
        State = AcquisitionState.Acquiring;
        return true;
    }

    public bool Stop()
    {
        if (_state is AcquisitionState.Idle or AcquisitionState.Stopped)
            return false;
        State = AcquisitionState.Stopped;
        return true;
    }

    public bool Freeze()
    {
        if (_state != AcquisitionState.Acquiring)
            return false;
        State = AcquisitionState.Frozen;
        return true;
    }

    public bool Unfreeze()
    {
        if (_state != AcquisitionState.Frozen)
            return false;
        State = AcquisitionState.Acquiring;
        return true;
    }

    public void PushData(float[] data, int count)
    {
        if (_state == AcquisitionState.Acquiring)
        {
            _ringBuffer.Write(data, 0, count);
            _totalSamples += count;
        }
    }

    public int ReadData(float[] buffer, int count)
    {
        return _ringBuffer.Read(buffer, 0, Math.Min(count, buffer.Length));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
