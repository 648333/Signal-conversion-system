namespace DH.Core.Models;

public enum InterfaceType
{
    Unknown = 0,
    EPP = 1,
    IEEE1394 = 2,
    USB = 3,
    RS232 = 4,
    Ethernet = 5,
    KiloMegaNet = 6,
    Mobile4G = 7,
    PCI = 8,
    Zigbee = 9,
    WiFi = 10,
    CAN = 11
}

public enum SyncClockType
{
    Normal = 0,
    GPS = 1,
    IEEE1588 = 2,
    DH5611 = 3,
    Cascade = 4
}

public enum DeviceStatus
{
    Offline = 0,
    Online = 1,
    Connected = 2,
    Acquiring = 3,
    Error = 4
}

public sealed class DeviceInfo
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string ModelName { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    public InterfaceType Interface { get; set; }
    public int InstrumentType { get; set; }
    public int ChannelCount { get; set; }
    public int AdBits { get; set; } = 24;
    public double MaxSampleRate { get; set; } = 1000000;
    public SyncClockType SyncClock { get; set; } = SyncClockType.Normal;
    public DeviceStatus Status { get; set; } = DeviceStatus.Offline;
    public string IpAddress { get; set; } = string.Empty;
    public int Port { get; set; }
    public string FirmwareVersion { get; set; } = string.Empty;
    public DateTime LastConnected { get; set; }
}
