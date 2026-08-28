using System.IO;
using System.Text;
using DH.Core.Models;

namespace DH.Acquisition;

public sealed class DataStorageService : IDisposable
{
    private FileStream? _dataStream;
    private BinaryWriter? _writer;
    private readonly object _lock = new();

    public const string FileHeader = "DH-RTDAS-DATA-V1";
    public const int HeaderSize = 512;

    public bool IsRecording { get; private set; }
    public string? CurrentFile { get; private set; }
    public string? LastDataFile { get; private set; }
    public long TotalBytesWritten { get; private set; }
    public long TotalSamplesWritten { get; private set; }
    private int _channelCount;

    public string StartRecording(string filePath, int channelCount, double sampleRate, SaveFormat format)
    {
        lock (_lock)
        {
            StopRecording();

            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            _dataStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read);
            _writer = new BinaryWriter(_dataStream, Encoding.UTF8);

            WriteHeader(channelCount, sampleRate, format);

            CurrentFile = filePath;
            _channelCount = channelCount;
            IsRecording = true;
            TotalBytesWritten = HeaderSize;
            TotalSamplesWritten = 0;

            return filePath;
        }
    }

    private void WriteHeader(int channelCount, double sampleRate, SaveFormat format)
    {
        _writer!.Write(FileHeader.ToCharArray());
        _writer.Write(channelCount);
        _writer.Write(sampleRate);
        _writer.Write((int)format);
        _writer.Write(DateTime.Now.ToBinary());

        var padding = HeaderSize - _dataStream!.Position;
        if (padding > 0)
        {
            var zeros = new byte[padding];
            _writer.Write(zeros);
        }
    }

    public void WriteData(float[] data, int count)
    {
        if (!IsRecording || _writer == null)
            return;

        lock (_lock)
        {
            _writer.Write(data, 0, count);
            TotalBytesWritten += count * sizeof(float);
            TotalSamplesWritten += count / Math.Max(_channelCount, 1);
        }
    }

    public void WriteData(short[] data, int count)
    {
        if (!IsRecording || _writer == null)
            return;

        lock (_lock)
        {
            var buffer = new byte[count * sizeof(short)];
            Buffer.BlockCopy(data, 0, buffer, 0, buffer.Length);
            _writer.Write(buffer);
            TotalBytesWritten += buffer.Length;
        }
    }

    public void StopRecording()
    {
        lock (_lock)
        {
            if (_writer != null)
            {
                _writer.Flush();
                _writer.Dispose();
                _writer = null;
            }
            if (_dataStream != null)
            {
                _dataStream.Dispose();
                _dataStream = null;
            }
            if (IsRecording && CurrentFile != null)
            {
                LastDataFile = CurrentFile;
            }
            IsRecording = false;
        }
    }

    public static RecordingInfo? ReadHeader(string filePath)
    {
        if (!File.Exists(filePath))
            return null;

        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var br = new BinaryReader(fs, Encoding.UTF8);

        var headerChars = br.ReadChars(FileHeader.Length);
        var header = new string(headerChars);
        if (header != FileHeader)
            return null;

        var channelCount = br.ReadInt32();
        var sampleRate = br.ReadDouble();
        var format = (SaveFormat)br.ReadInt32();
        var timestamp = DateTime.FromBinary(br.ReadInt64());

        return new RecordingInfo
        {
            FilePath = filePath,
            ChannelCount = channelCount,
            SampleRate = sampleRate,
            Format = format,
            StartTime = timestamp,
            DataOffset = HeaderSize,
            FileSize = new FileInfo(filePath).Length
        };
    }

    public static float[]? ReadChannel(string filePath, int channelIndex, long startSample, int sampleCount)
    {
        var info = ReadHeader(filePath);
        if (info == null)
            return null;

        var totalChannels = info.ChannelCount;
        if (channelIndex < 0 || channelIndex >= totalChannels)
            return null;

        var data = new float[sampleCount];
        var dataStart = info.DataOffset + startSample * totalChannels * sizeof(float);

        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        fs.Seek(dataStart, SeekOrigin.Begin);
        using var br = new BinaryReader(fs);

        for (int s = 0; s < sampleCount; s++)
        {
            for (int ch = 0; ch < totalChannels; ch++)
            {
                var value = br.ReadSingle();
                if (ch == channelIndex)
                    data[s] = value;
            }
        }

        return data;
    }

    public void Dispose()
    {
        StopRecording();
        GC.SuppressFinalize(this);
    }
}

public sealed class RecordingInfo
{
    public string FilePath { get; set; } = string.Empty;
    public int ChannelCount { get; set; }
    public double SampleRate { get; set; }
    public SaveFormat Format { get; set; }
    public DateTime StartTime { get; set; }
    public long DataOffset { get; set; }
    public long FileSize { get; set; }

    public long TotalSamples => FileSize > DataOffset ? (FileSize - DataOffset) / (ChannelCount * sizeof(float)) : 0;
    public double DurationSeconds => SampleRate > 0 ? TotalSamples / SampleRate : 0;
}
