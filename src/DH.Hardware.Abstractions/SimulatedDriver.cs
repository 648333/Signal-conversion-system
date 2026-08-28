using System.ComponentModel.Composition;
using DH.Core.Models;

namespace DH.Hardware;

[Export(typeof(IDriver))]
public sealed class SimulatedDriver : IDriver
{
    public string DriverName => "模拟设备驱动";
    public InterfaceType SupportedInterface => InterfaceType.Unknown;

    public IEnumerable<DeviceInfo> Scan()
    {
        yield return new DeviceInfo
        {
            ModelName = "DH5922 (模拟)",
            SerialNumber = "SIM-5922-001",
            Interface = InterfaceType.USB,
            ChannelCount = 8,
            AdBits = 24,
            MaxSampleRate = 128000,
            SyncClock = SyncClockType.Normal,
            Status = DeviceStatus.Online
        };

        yield return new DeviceInfo
        {
            ModelName = "DH5902 (模拟)",
            SerialNumber = "SIM-5902-002",
            Interface = InterfaceType.Ethernet,
            IpAddress = "192.168.1.100",
            Port = 5000,
            ChannelCount = 4,
            AdBits = 24,
            MaxSampleRate = 51200,
            SyncClock = SyncClockType.Normal,
            Status = DeviceStatus.Online
        };
    }

    public IDevice CreateDevice(DeviceInfo info) => new SimulatedDevice(info);

    public bool CanHandle(DeviceInfo info) => info.ModelName.Contains("模拟");
}