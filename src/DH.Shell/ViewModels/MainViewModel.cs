using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using DH.Core.Logging;
using DH.Core.Models;
using DH.Core.Services;
using DH.Core.Events;

namespace DH.Shell.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly AppServices _services;
    private readonly ILogService _log;
    private readonly AppState _appState;
    private readonly EventBus _eventBus;

    private string _statusMessage = "就绪";
    private int _connectedDeviceCount;
    private int _totalChannelCount;

    public MainViewModel(AppServices services)
    {
        _services = services;
        _log = services.GetService<ILogService>();
        _appState = services.GetService<AppState>();
        _eventBus = services.GetService<EventBus>();

        _appState.PropertyChanged += (_, e) => OnPropertyChanged(e.PropertyName);
        _eventBus.Subscribe<DeviceConnectedEvent>(OnDeviceConnected);
        _eventBus.Subscribe<AcquisitionStartedEvent>(OnAcqStarted);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetField(ref _statusMessage, value);
    }

    public int ConnectedDeviceCount
    {
        get => _connectedDeviceCount;
        set => SetField(ref _connectedDeviceCount, value);
    }

    public int TotalChannelCount
    {
        get => _totalChannelCount;
        set => SetField(ref _totalChannelCount, value);
    }

    public ObservableCollection<ChannelConfig> Channels { get; } = new();

    private void OnDeviceConnected(DeviceConnectedEvent e)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            ConnectedDeviceCount++;
            StatusMessage = $"设备已连接: {e.DeviceName}";
        });
    }

    private void OnAcqStarted(AcquisitionStartedEvent e)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            StatusMessage = $"采集中: {e.EventName}";
        });
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
