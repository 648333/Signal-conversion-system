namespace DH.Core.Models;

public enum ChannelType
{
    Analog = 0x0100,
    Strain = 0x0200,
    Voltage = 0x0300,
    Temperature = 0x0400,
    Power = 0x1400,
    Tacho = 0x0500,
    Counter = 0x0600,
    DigitalIO = 0x0700,
    GPS = 0x0800,
    CAN = 0x0900,
    Video = 0x0A00,
    Audio = 0x0B00,
    SignalSource = 0x0C00
}

public enum MeasureType
{
    InnerInput = 0,
    StrainStress = 1,
    PiezoIEPE = 2,
    Bridge = 3,
    Charge = 4,
    Thermocouple = 5,
    RTD = 6,
    Voltage = 7,
    Current = 8,
    Displacement = 9,
    Acceleration = 10,
    Velocity = 11,
    Force = 12,
    Pressure = 13,
    SoundPressure = 14,
    Temperature_External = 15
}

public enum CouplingType
{
    AC = 0,
    DC = 1,
    IEPE = 2,
    ICP = 3
}

public enum IntegralType
{
    None = 0,
    SingleIntegral = 1,
    DoubleIntegral = 2,
    SingleDifferential = 3,
    DoubleDifferential = 4
}

public sealed class ChannelConfig
{
    public int Index { get; set; }
    public string Name { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    public ChannelType ChannelType { get; set; } = ChannelType.Analog;
    public MeasureType MeasureType { get; set; } = MeasureType.InnerInput;
    public string SensorModel { get; set; } = string.Empty;
    public string SensorSerial { get; set; } = string.Empty;
    public double Sensitivity { get; set; } = 1.0;
    public string Unit { get; set; } = "mV";
    public CouplingType Coupling { get; set; } = CouplingType.DC;
    public double Range { get; set; } = 10000;
    public double FilterCutoff { get; set; } = 10000;
    public IntegralType Integration { get; set; } = IntegralType.None;
    public string IntegralUnit { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public double Offset { get; set; }
    public double Gain { get; set; } = 1.0;
    public string CalibrationDate { get; set; } = string.Empty;
    public string Formula { get; set; } = string.Empty;
    public int BridgeType { get; set; }
    public double BridgeVoltage { get; set; }

    public string ChannelTypeDisplay => ChannelType switch
    {
        ChannelType.Analog => "模拟量",
        ChannelType.Strain => "应变",
        ChannelType.Voltage => "电压",
        ChannelType.Temperature => "温度",
        ChannelType.Power => "功率",
        ChannelType.Tacho => "转速",
        ChannelType.Counter => "计数器",
        ChannelType.DigitalIO => "数字IO",
        ChannelType.GPS => "GPS",
        ChannelType.CAN => "CAN",
        ChannelType.Video => "视频",
        ChannelType.Audio => "音频",
        ChannelType.SignalSource => "信号源",
        _ => ChannelType.ToString()
    };

    public double SampleRate { get; set; } = 1000;
    public string SampleRateDisplay => SampleRate >= 1000 ? $"{SampleRate / 1000:F1} kHz" : $"{SampleRate:F0} Hz";

    public string StatusDisplay => Enabled ? "正常" : "禁用";
}

public sealed class SampleRateConfig
{
    public string DisplayText { get; set; } = string.Empty;
    public double Frequency { get; set; }
    public double HardwareFrequency { get; set; }
    public int ParaCode { get; set; }
    public int CtrlCode { get; set; } = 4;
    public int DataRatio { get; set; } = 1;
}

public static class DefaultSampleRates
{
    public static readonly SampleRateConfig[] Rates = new[]
    {
        new SampleRateConfig { DisplayText = "10Hz", Frequency = 10, HardwareFrequency = 12.8 },
        new SampleRateConfig { DisplayText = "20Hz", Frequency = 20, HardwareFrequency = 25.6 },
        new SampleRateConfig { DisplayText = "50Hz", Frequency = 50, HardwareFrequency = 64 },
        new SampleRateConfig { DisplayText = "100Hz", Frequency = 100, HardwareFrequency = 128 },
        new SampleRateConfig { DisplayText = "200Hz", Frequency = 200, HardwareFrequency = 256 },
        new SampleRateConfig { DisplayText = "500Hz", Frequency = 500, HardwareFrequency = 640 },
        new SampleRateConfig { DisplayText = "1kHz", Frequency = 1000, HardwareFrequency = 1280 },
        new SampleRateConfig { DisplayText = "2kHz", Frequency = 2000, HardwareFrequency = 2560 },
        new SampleRateConfig { DisplayText = "5kHz", Frequency = 5000, HardwareFrequency = 6400 },
        new SampleRateConfig { DisplayText = "10kHz", Frequency = 10000, HardwareFrequency = 12800 },
        new SampleRateConfig { DisplayText = "20kHz", Frequency = 20000, HardwareFrequency = 25600 },
        new SampleRateConfig { DisplayText = "50kHz", Frequency = 50000, HardwareFrequency = 64000 },
        new SampleRateConfig { DisplayText = "100kHz", Frequency = 100000, HardwareFrequency = 128000 },
        new SampleRateConfig { DisplayText = "200kHz", Frequency = 200000, HardwareFrequency = 256000 },
        new SampleRateConfig { DisplayText = "500kHz", Frequency = 500000, HardwareFrequency = 640000 },
        new SampleRateConfig { DisplayText = "1MHz", Frequency = 1000000, HardwareFrequency = 1024000 },
    };
}
