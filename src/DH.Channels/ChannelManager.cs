using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Xml.Serialization;
using DH.Core.Models;

namespace DH.Channels;

public sealed class ChannelManager : INotifyPropertyChanged
{
    private double _sampleRate = 1000;
    private int _activeChannelCount;

    public ObservableCollection<ChannelConfig> Channels { get; } = new();

    public double SampleRate
    {
        get => _sampleRate;
        set => SetField(ref _sampleRate, value);
    }

    public int ActiveChannelCount
    {
        get => _activeChannelCount;
        private set => SetField(ref _activeChannelCount, value);
    }

    public void AddChannel(ChannelConfig channel)
    {
        channel.SampleRate = _sampleRate;
        Channels.Add(channel);
        UpdateActiveCount();
    }

    public void RemoveChannel(int index)
    {
        if (index >= 0 && index < Channels.Count)
        {
            Channels.RemoveAt(index);
            UpdateActiveCount();
        }
    }

    public void ClearAll()
    {
        Channels.Clear();
        UpdateActiveCount();
    }

    public void ApplySampleRateToAll(double sampleRate)
    {
        _sampleRate = sampleRate;
        foreach (var ch in Channels)
            ch.SampleRate = sampleRate;
    }

    /// <summary>
    /// 导出通道配置到XML模板文件
    /// </summary>
    public bool ExportTemplate(string filePath)
    {
        try
        {
            var template = new ChannelTemplate
            {
                SampleRate = _sampleRate,
                Channels = Channels.ToList()
            };

            var serializer = new XmlSerializer(typeof(ChannelTemplate));
            var settings = new System.Xml.XmlWriterSettings
            {
                Indent = true,
                Encoding = new System.Text.UTF8Encoding(false)
            };
            using var writer = System.Xml.XmlWriter.Create(filePath, settings);
            serializer.Serialize(writer, template);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 从XML模板文件导入通道配置
    /// </summary>
    public bool ImportTemplate(string filePath)
    {
        try
        {
            if (!File.Exists(filePath)) return false;

            var serializer = new XmlSerializer(typeof(ChannelTemplate));
            using var reader = new StreamReader(filePath);
            if (serializer.Deserialize(reader) is not ChannelTemplate template)
                return false;

            Channels.Clear();
            _sampleRate = template.SampleRate;
            foreach (var ch in template.Channels)
            {
                ch.SampleRate = _sampleRate;
                Channels.Add(ch);
            }
            UpdateActiveCount();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void UpdateActiveCount()
    {
        ActiveChannelCount = Channels.Count(c => c.Enabled);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        return true;
    }
}

[XmlRoot("ChannelTemplate")]
public sealed class ChannelTemplate
{
    [XmlAttribute] public double SampleRate { get; set; } = 1000;
    [XmlArray("Channels")]
    [XmlArrayItem("Channel")]
    public List<ChannelConfig> Channels { get; set; } = new();
}
