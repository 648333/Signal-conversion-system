using DH.Core.Models;
using System.ComponentModel;

namespace DH.Hardware;

/// <summary>
/// 硬件管理器：管理所有驱动和设备的发现、连接、状态
/// </summary>
public interface IHardwareManager : INotifyPropertyChanged
{
    IReadOnlyList<IDriver> Drivers { get; }
    IReadOnlyList<IDevice> Devices { get; }
    IReadOnlyList<IDevice> ConnectedDevices { get; }

    void RegisterDriver(IDriver driver);
    void UnregisterDriver(IDriver driver);

    void ScanDevices();
    IDevice? ConnectDevice(DeviceInfo info);
    bool DisconnectDevice(string deviceId);
    void DisconnectAll();

    IDevice? GetDevice(string deviceId);
    IEnumerable<DeviceInfo> GetAvailableDevices();
}
