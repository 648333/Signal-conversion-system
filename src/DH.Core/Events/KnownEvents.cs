namespace DH.Core.Events;

public sealed class DeviceConnectedEvent : IEvent
{
    public string DeviceId { get; init; } = string.Empty;
    public string DeviceName { get; init; } = string.Empty;
}

public sealed class DeviceDisconnectedEvent : IEvent
{
    public string DeviceId { get; init; } = string.Empty;
}

public sealed class AcquisitionStartedEvent : IEvent
{
    public string EventName { get; init; } = string.Empty;
    public DateTime StartTime { get; init; } = DateTime.Now;
}

public sealed class AcquisitionStoppedEvent : IEvent
{
    public DateTime StopTime { get; init; } = DateTime.Now;
}

public sealed class DataReceivedEvent : IEvent
{
    public int ChannelId { get; init; }
    public float[] Data { get; init; } = Array.Empty<float>();
    public double SampleRate { get; init; }
}

public sealed class ProjectLoadedEvent : IEvent
{
    public string ProjectName { get; init; } = string.Empty;
    public string ProjectPath { get; init; } = string.Empty;
}

public sealed class LanguageChangedEvent : IEvent
{
    public string Language { get; init; } = "zh-CN";
}

public sealed class ModuleLoadedEvent : IEvent
{
    public string ModuleName { get; init; } = string.Empty;
}
