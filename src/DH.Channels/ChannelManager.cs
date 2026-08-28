using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using DH.Core.Models;

namespace DH.Channels;

public sealed class ChannelManager : INotifyPropertyChanged
{
    private double _sampleRate = 1000;
    private int _activeChannelCount;

    public ObservableCollection<ChannelConfig> Channels { get; } = new();

    public double SampleRate
    {
        get => _sampleRate;
        set => SetField(ref _sampleRate, value);
    }

    public int ActiveChannelCount
    {
        get => _activeChannelCount;
        private set => SetField(ref _activeChannelCount, value);
    }

    public void AddChannel(ChannelConfig channel)
    {
        channel.SampleRate = _sampleRate;
        Channels.Add(channel);
        UpdateActiveCount();
    }

    public void RemoveChannel(int index)
    {
        if (index >= 0 && index < Channels.Count)
        {
            Channels.RemoveAt(index);
            UpdateActiveCount();
        }
    }

    public void ClearAll()
    {
        Channels.Clear();
        UpdateActiveCount();
    }

    public void ApplySampleRateToAll(double sampleRate)
    {
        _sampleRate = sampleRate;
        foreach (var ch in Channels)
            ch.SampleRate = sampleRate;
    }

    private void UpdateActiveCount()
    {
        ActiveChannelCount = Channels.Count(c => c.Enabled);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
