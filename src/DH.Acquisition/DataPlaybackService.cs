using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using DH.Core.Models;

namespace DH.Acquisition;

public enum PlaybackState
{
    Stopped,
    Playing,
    Paused,
    Ended
}

/// <summary>
/// 数据回放服务：从已存储的数据文件中读取数据进行回放分析
/// 支持播放/暂停/停止/定位，支持变速回放和逐块读取
/// </summary>
public sealed class DataPlaybackService : INotifyPropertyChanged, IDisposable
{
    private FileStream? _fileStream;
    private BinaryReader? _reader;
    private RecordingInfo? _info;
    private PlaybackState _state = PlaybackState.Stopped;
    private long _currentSample;
    private double _playbackSpeed = 1.0;
    private Timer? _playbackTimer;
    private readonly object _lock = new();

    public RecordingInfo? Info => _info;
    public PlaybackState State
    {
        get => _state;
        private set => SetField(ref _state, value);
    }
    public double PlaybackSpeed
    {
        get => _playbackSpeed;
        set => SetField(ref _playbackSpeed, Math.Clamp(value, 0.1, 10.0));
    }
    public long CurrentSample => _currentSample;
    public double CurrentTime => _info != null && _info.SampleRate > 0
        ? _currentSample / _info.SampleRate : 0;
    public double TotalDuration => _info?.DurationSeconds ?? 0;
    public long TotalSamples => _info?.TotalSamples ?? 0;
    public bool IsOpen => _info != null;

    public int BlockSize { get; set; } = 4096;

    public event Action<float[]?, int>? DataBlockRead;
    public event Action? PlaybackEnded;

    /// <summary>
    /// 打开数据文件
    /// </summary>
    public bool Open(string filePath)
    {
        Close();

        var info = DataStorageService.ReadHeader(filePath);
        if (info == null)
            return false;

        _fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        _reader = new BinaryReader(_fileStream);
        _info = info;
        _currentSample = 0;
        State = PlaybackState.Stopped;

        SeekTo(0);
        return true;
    }

    /// <summary>
    /// 开始播放
    /// </summary>
    public void Play()
    {
        if (_info == null || _reader == null)
            return;

        if (State == PlaybackState.Ended)
            SeekTo(0);

        State = PlaybackState.Playing;

        var intervalMs = _info.SampleRate > 0
            ? (int)(BlockSize / _info.SampleRate * 1000 / _playbackSpeed)
            : 50;
        intervalMs = Math.Max(1, intervalMs);

        _playbackTimer?.Dispose();
        _playbackTimer = new Timer(OnPlaybackTick, null, intervalMs, intervalMs);
    }

    /// <summary>
    /// 暂停播放
    /// </summary>
    public void Pause()
    {
        if (State != PlaybackState.Playing)
            return;
        State = PlaybackState.Paused;
        _playbackTimer?.Dispose();
        _playbackTimer = null;
    }

    /// <summary>
    /// 停止播放
    /// </summary>
    public void Stop()
    {
        State = PlaybackState.Stopped;
        _playbackTimer?.Dispose();
        _playbackTimer = null;
        SeekTo(0);
    }

    /// <summary>
    /// 定位到指定时间（秒）
    /// </summary>
    public void SeekToTime(double timeSeconds)
    {
        if (_info == null) return;
        var sample = (long)(timeSeconds * _info.SampleRate);
        SeekTo(sample);
    }

    /// <summary>
    /// 定位到指定采样点
    /// </summary>
    public void SeekTo(long sampleIndex)
    {
        if (_info == null || _reader == null) return;

        lock (_lock)
        {
            _currentSample = Math.Clamp(sampleIndex, 0, _info.TotalSamples);
            var dataPos = _info.DataOffset + _currentSample * _info.ChannelCount * sizeof(float);
            _fileStream?.Seek(dataPos, SeekOrigin.Begin);
            OnPropertyChanged(nameof(CurrentSample));
            OnPropertyChanged(nameof(CurrentTime));
        }
    }

    /// <summary>
    /// 读取指定通道的指定范围数据（不改变播放位置）
    /// </summary>
    public float[]? ReadChannel(int channelIndex, long startSample, int sampleCount)
    {
        if (_info == null) return null;
        return DataStorageService.ReadChannel(_info.FilePath, channelIndex, startSample, sampleCount);
    }

    /// <summary>
    /// 读取所有通道的一个数据块（交织格式）
    /// </summary>
    public float[]? ReadInterleavedBlock(int sampleCount)
    {
        if (_info == null || _reader == null)
            return null;

        lock (_lock)
        {
            var totalFloats = sampleCount * _info.ChannelCount;
            var remaining = _info.TotalSamples - _currentSample;
            if (remaining <= 0)
                return null;

            var actualSamples = (int)Math.Min(sampleCount, remaining);
            var actualFloats = actualSamples * _info.ChannelCount;
            var data = new float[actualFloats];

            for (int i = 0; i < actualFloats; i++)
            {
                try
                {
                    data[i] = _reader.ReadSingle();
                }
                catch { break; }
            }

            _currentSample += actualSamples;
            OnPropertyChanged(nameof(CurrentSample));
            OnPropertyChanged(nameof(CurrentTime));

            return data;
        }
    }

    /// <summary>
    /// 从交织数据中提取指定通道
    /// </summary>
    public static float[] ExtractChannel(float[] interleaved, int channelCount, int channelIndex)
    {
        var sampleCount = interleaved.Length / channelCount;
        var result = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++)
            result[i] = interleaved[i * channelCount + channelIndex];
        return result;
    }

    private void OnPlaybackTick(object? state)
    {
        if (State != PlaybackState.Playing || _info == null)
            return;

        var block = ReadInterleavedBlock(BlockSize);
        if (block == null || block.Length == 0)
        {
            State = PlaybackState.Ended;
            _playbackTimer?.Dispose();
            _playbackTimer = null;
            PlaybackEnded?.Invoke();
            return;
        }

        DataBlockRead?.Invoke(block, _info.ChannelCount);

        if (_currentSample >= _info.TotalSamples)
        {
            State = PlaybackState.Ended;
            _playbackTimer?.Dispose();
            _playbackTimer = null;
            PlaybackEnded?.Invoke();
        }
    }

    public void Close()
    {
        Stop();
        _reader?.Dispose();
        _fileStream?.Dispose();
        _reader = null;
        _fileStream = null;
        _info = null;
        _currentSample = 0;
    }

    public void Dispose()
    {
        Close();
        GC.SuppressFinalize(this);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
    private void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
