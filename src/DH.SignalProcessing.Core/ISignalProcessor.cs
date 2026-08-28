namespace DH.SignalProcessing;

public enum ProcessingNodeType
{
    Source,
    VirtualChannel,
    Filter,
    Integral,
    Differential,
    FFT,
    Octave,
    Envelope,
    Cepstrum,
    Correlation,
    Statistics,
    RainFlow,
    Resample,
    Custom
}

public interface ISignalProcessor
{
    string Name { get; }
    ProcessingNodeType NodeType { get; }
    float[] Process(float[] input);
    void Reset();
}

public interface IPipelineNode
{
    Guid Id { get; }
    string Name { get; }
    ProcessingNodeType NodeType { get; }
    IPipelineNode[] Inputs { get; }
    float[] Execute();
}
