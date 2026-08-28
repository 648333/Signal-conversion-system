using DH.Core.Models;

namespace DH.Hardware;

/// <summary>
/// 硬件驱动接口：负责特定接口类型的设备发现和创建
/// </summary>
public interface IDriver
{
    string DriverName { get; }
    InterfaceType SupportedInterface { get; }

    IEnumerable<DeviceInfo> Scan();
    IDevice CreateDevice(DeviceInfo info);
    bool CanHandle(DeviceInfo info);
}
