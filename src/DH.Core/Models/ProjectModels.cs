using System.Xml.Serialization;

namespace DH.Core.Models;

public enum AcquisitionState
{
    Idle,
    Configuring,
    Ready,
    Acquiring,
    Paused,
    Stopped,
    Frozen
}

public enum SaveFormat
{
    Short = 0,
    Float = 1,
    Int = 2
}

[XmlRoot("DHProject")]
public sealed class ProjectInfo
{
    [XmlAttribute] public string Version { get; set; } = "1.0";
    [XmlElement] public string Name { get; set; } = "新建工程";
    [XmlElement] public string CreatedDate { get; set; } = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    [XmlElement] public string Description { get; set; } = string.Empty;
    [XmlElement] public string FilePath { get; set; } = string.Empty;
    [XmlElement] public string SoftwarePackage { get; set; } = "CommonSoft";
    [XmlElement] public int DeviceCount { get; set; }
    [XmlElement] public int ChannelCount { get; set; }
    [XmlElement] public double SampleRate { get; set; } = 1000;
    [XmlElement] public SaveFormat SaveFormat { get; set; } = SaveFormat.Float;
    [XmlElement] public string Language { get; set; } = "zh-CN";
    [XmlArray("Events")]
    [XmlArrayItem("Event")]
    public List<ExperimentEvent> Events { get; set; } = new();
}

public sealed class ExperimentEvent
{
    [XmlAttribute] public string Id { get; set; } = Guid.NewGuid().ToString("N");
    [XmlAttribute] public string Name { get; set; } = string.Empty;
    [XmlAttribute] public string StartTime { get; set; } = string.Empty;
    [XmlAttribute] public string EndTime { get; set; } = string.Empty;
    [XmlAttribute] public int ChannelCount { get; set; }
    [XmlAttribute] public double SampleRate { get; set; }
    [XmlAttribute] public long DataPoints { get; set; }
    [XmlElement] public string Comment { get; set; } = string.Empty;
    [XmlElement] public string DataFile { get; set; } = string.Empty;
}
