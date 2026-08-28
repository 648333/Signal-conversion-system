using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using DH.Core.Models;

namespace DH.Hardware;

public sealed class HardwareManager : IHardwareManager
{
    private readonly List<IDriver> _drivers = new();
    private readonly List<IDevice> _devices = new();
    private readonly List<IDevice> _connectedDevices = new();
    private readonly object _lock = new();

    public IReadOnlyList<IDriver> Drivers => _drivers;
    public IReadOnlyList<IDevice> Devices => _devices;
    public IReadOnlyList<IDevice> ConnectedDevices => _connectedDevices;

    public ObservableCollection<DeviceInfo> AvailableDevices { get; } = new();

    public void RegisterDriver(IDriver driver)
    {
        lock (_lock)
        {
            if (_drivers.Any(d => d.DriverName == driver.DriverName))
                return;
            _drivers.Add(driver);
        }
        OnPropertyChanged(nameof(Drivers));
    }

    public void UnregisterDriver(IDriver driver)
    {
        lock (_lock)
        {
            _drivers.Remove(driver);
        }
        OnPropertyChanged(nameof(Drivers));
    }

    public void ScanDevices()
    {
        lock (_lock)
        {
            _devices.Clear();
            AvailableDevices.Clear();

            foreach (var driver in _drivers)
            {
                foreach (var info in driver.Scan())
                {
                    var device = driver.CreateDevice(info);
                    _devices.Add(device);
                    AvailableDevices.Add(info);
                }
            }
        }
        OnPropertyChanged(nameof(Devices));
        OnPropertyChanged(nameof(AvailableDevices));
    }

    public IDevice? ConnectDevice(DeviceInfo info)
    {
        IDevice? device = null;
        lock (_lock)
        {
            device = _devices.FirstOrDefault(d => d.Info.Id == info.Id);
            if (device == null)
            {
                var driver = _drivers.FirstOrDefault(d => d.CanHandle(info));
                if (driver != null)
                {
                    device = driver.CreateDevice(info);
                    _devices.Add(device);
                }
            }

            if (device != null && device.Connect())
            {
                _connectedDevices.Add(device);
                info.Status = DeviceStatus.Connected;
            }
        }

        if (device != null)
            OnPropertyChanged(nameof(ConnectedDevices));

        return device;
    }

    public bool DisconnectDevice(string deviceId)
    {
        IDevice? device;
        lock (_lock)
        {
            device = _connectedDevices.FirstOrDefault(d => d.Info.Id == deviceId);
            if (device == null)
                return false;

            if (device.Disconnect())
            {
                _connectedDevices.Remove(device);
                device.Info.Status = DeviceStatus.Online;
            }
        }
        OnPropertyChanged(nameof(ConnectedDevices));
        return true;
    }

    public void DisconnectAll()
    {
        lock (_lock)
        {
            foreach (var device in _connectedDevices)
            {
                try { device.Disconnect(); device.Info.Status = DeviceStatus.Online; }
                catch { }
            }
            _connectedDevices.Clear();
        }
        OnPropertyChanged(nameof(ConnectedDevices));
    }

    public IDevice? GetDevice(string deviceId)
    {
        lock (_lock)
        {
            return _devices.FirstOrDefault(d => d.Info.Id == deviceId);
        }
    }

    public IEnumerable<DeviceInfo> GetAvailableDevices()
    {
        lock (_lock)
        {
            return _devices.Select(d => d.Info).ToList();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
