namespace DH.Acquisition;

/// <summary>
/// 触发斜率类型
/// </summary>
public enum TriggerSlope
{
    Rising,
    Falling
}

/// <summary>
/// 触发模式
/// </summary>
public enum TriggerMode
{
    Normal,
    Auto,
    Single
}

/// <summary>
/// 触发状态
/// </summary>
public enum TriggerState
{
    Idle,
    Waiting,
    Triggered,
    Complete
}

/// <summary>
/// 触发事件参数
/// </summary>
public sealed class TriggeredEventArgs : EventArgs
{
    public float[] Data { get; init; } = Array.Empty<float>();
    public int ChannelCount { get; init; }
    public double SampleRate { get; init; }
    public int PreTriggerSamples { get; init; }
    public int PostTriggerSamples { get; init; }
    public double TriggerLevel { get; init; }
    public TriggerSlope Slope { get; init; }
    public int TriggerChannelIndex { get; init; }
    public DateTime TriggerTime { get; init; }
}

/// <summary>
/// 触发采集服务：支持预触发、触发电平、触发斜率等
/// </summary>
public sealed class TriggerService : IDisposable
{
    private readonly RingBuffer _preTriggerBuffer;
    private float[]? _captureBuffer;
    private int _capturePos;
    private bool _disposed;

    public int ChannelCount { get; private set; }
    public double SampleRate { get; private set; }
    public int TriggerChannelIndex { get; set; }
    public double TriggerLevel { get; set; }
    public TriggerSlope Slope { get; set; } = TriggerSlope.Rising;
    public TriggerMode Mode { get; set; } = TriggerMode.Normal;

    /// <summary>
    /// 预触发时长（秒）
    /// </summary>
    public double PreTriggerSeconds { get; set; } = 0.5;

    /// <summary>
    /// 触发后采集时长（秒）
    /// </summary>
    public double PostTriggerSeconds { get; set; } = 1.0;

    public TriggerState State { get; private set; } = TriggerState.Idle;

    /// <summary>
    /// 触发事件
    /// </summary>
    public event EventHandler<TriggeredEventArgs>? Triggered;

    public TriggerService(int channelCount, double sampleRate)
    {
        ChannelCount = channelCount;
        SampleRate = sampleRate;
        var preTriggerSamples = (int)(PreTriggerSeconds * sampleRate) * channelCount;
        _preTriggerBuffer = new RingBuffer(Math.Max(preTriggerSamples, channelCount * 10));
    }

    public void Configure(int channelCount, double sampleRate)
    {
        ChannelCount = channelCount;
        SampleRate = sampleRate;
        var preTriggerSamples = (int)(PreTriggerSeconds * sampleRate) * channelCount;
        _preTriggerBuffer.Resize(Math.Max(preTriggerSamples, channelCount * 10));
        Reset();
    }

    public void Start()
    {
        Reset();
        State = TriggerState.Waiting;
    }

    public void Stop()
    {
        State = TriggerState.Idle;
        _captureBuffer = null;
        _capturePos = 0;
    }

    public void Reset()
    {
        _preTriggerBuffer.Clear();
        _captureBuffer = null;
        _capturePos = 0;
        State = TriggerState.Idle;
    }

    /// <summary>
    /// 输入数据进行触发检测
    /// </summary>
    public void ProcessData(float[] interleavedData, int sampleCount)
    {
        if (State == TriggerState.Idle || State == TriggerState.Complete)
            return;

        var channelCount = ChannelCount;
        if (channelCount <= 0) return;

        var samplesPerCh = sampleCount / channelCount;
        if (samplesPerCh == 0) return;

        // 等待触发
        if (State == TriggerState.Waiting)
        {
            // 写入预触发缓冲区
            _preTriggerBuffer.Write(interleavedData, sampleCount);

            // 检测触发
            if (DetectTrigger(interleavedData, samplesPerCh, channelCount, out var triggerSampleIdx))
            {
                OnTriggered(interleavedData, samplesPerCh, triggerSampleIdx);
            }
        }
        // 已触发，采集后触发数据
        else if (State == TriggerState.Triggered && _captureBuffer != null)
        {
            var remaining = _captureBuffer.Length - _capturePos;
            var toCopy = Math.Min(sampleCount, remaining);
            Array.Copy(interleavedData, 0, _captureBuffer, _capturePos, toCopy);
            _capturePos += toCopy;

            if (_capturePos >= _captureBuffer.Length)
            {
                CompleteTrigger();
            }
        }
    }

    private bool DetectTrigger(float[] data, int samplesPerCh, int channelCount, out int triggerSampleIdx)
    {
        triggerSampleIdx = -1;
        var chIdx = TriggerChannelIndex;
        if (chIdx < 0 || chIdx >= channelCount)
            return false;

        var level = (float)TriggerLevel;

        // 从上一次的最后一个值开始
        // 我们需要预触发缓冲区中的最后一个样本作为参考
        float prevSample = 0;
        if (_preTriggerBuffer.Count >= channelCount)
        {
            var lastSamples = new float[channelCount];
            _preTriggerBuffer.PeekLast(lastSamples);
            prevSample = lastSamples[chIdx];
        }

        for (int s = 0; s < samplesPerCh; s++)
        {
            var sample = data[s * channelCount + chIdx];

            bool triggered = Slope switch
            {
                TriggerSlope.Rising => prevSample < level && sample >= level,
                TriggerSlope.Falling => prevSample > level && sample <= level,
                _ => false
            };

            if (triggered)
            {
                triggerSampleIdx = s;
                return true;
            }

            prevSample = sample;
        }

        return false;
    }

    private void OnTriggered(float[] currentBlock, int samplesPerCh, int triggerSampleIdx)
    {
        var channelCount = ChannelCount;
        var preTriggerSamples = (int)(PreTriggerSeconds * SampleRate);
        var postTriggerSamples = (int)(PostTriggerSeconds * SampleRate);
        var totalSamples = preTriggerSamples + postTriggerSamples;
        var totalFloats = totalSamples * channelCount;

        _captureBuffer = new float[totalFloats];

        // 复制预触发数据
        var preTriggerFloats = preTriggerSamples * channelCount;
        if (_preTriggerBuffer.Count >= preTriggerFloats)
        {
            var preData = new float[preTriggerFloats];
            _preTriggerBuffer.Read(preData, preTriggerFloats);
            // 丢弃多余的，只保留最后 preTriggerFloats 个
            var preStart = _preTriggerBuffer.Count - preTriggerFloats;
            if (preStart > 0)
            {
                var discard = new float[preStart];
                _preTriggerBuffer.Read(discard, preStart);
            }
            _preTriggerBuffer.Read(preData, preTriggerFloats);
            Array.Copy(preData, _captureBuffer, preTriggerFloats);
        }
        else
        {
            // 预触发数据不足，用已有数据填充
            var available = _preTriggerBuffer.Count;
            var preData = new float[available];
            _preTriggerBuffer.Read(preData, available);
            var offset = preTriggerFloats - available;
            Array.Copy(preData, 0, _captureBuffer, offset, available);
        }

        // 复制当前块中触发点之后的数据
        var postStartIdx = triggerSampleIdx * channelCount;
        var postInThisBlock = (samplesPerCh - triggerSampleIdx) * channelCount;
        var remainingPost = totalFloats - preTriggerFloats;
        var toCopy = Math.Min(postInThisBlock, remainingPost);

        Array.Copy(currentBlock, postStartIdx, _captureBuffer, preTriggerFloats, toCopy);
        _capturePos = preTriggerFloats + toCopy;

        if (_capturePos >= totalFloats)
        {
            CompleteTrigger();
        }
        else
        {
            State = TriggerState.Triggered;
        }
    }

    private void CompleteTrigger()
    {
        State = TriggerState.Complete;

        var args = new TriggeredEventArgs
        {
            Data = _captureBuffer!,
            ChannelCount = ChannelCount,
            SampleRate = SampleRate,
            PreTriggerSamples = (int)(PreTriggerSeconds * SampleRate),
            PostTriggerSamples = (int)(PostTriggerSeconds * SampleRate),
            TriggerLevel = TriggerLevel,
            Slope = Slope,
            TriggerChannelIndex = TriggerChannelIndex,
            TriggerTime = DateTime.Now
        };

        Triggered?.Invoke(this, args);

        // Auto 模式自动重新武装
        if (Mode == TriggerMode.Auto)
        {
            Start();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }
}
