using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DH.Acquisition;
using DH.Channels;

namespace DH.Shell.Views;

public partial class TriggerConfigWindow : Window
{
    private readonly ChannelManager _channelManager;

    public bool TriggerEnabled { get; private set; }
    public int TriggerChannelIndex { get; private set; }
    public double TriggerLevel { get; private set; } = 1000;
    public TriggerSlope Slope { get; private set; } = TriggerSlope.Rising;
    public TriggerMode Mode { get; private set; } = TriggerMode.Auto;
    public double PreTriggerSeconds { get; private set; } = 0.5;
    public double PostTriggerSeconds { get; private set; } = 1.0;
    public double SampleRate { get; private set; }

    public TriggerConfigWindow(ChannelManager channelManager, double sampleRate)
    {
        InitializeComponent();
        _channelManager = channelManager;
        SampleRate = sampleRate;

        // 填充通道列表
        foreach (var ch in _channelManager.Channels)
        {
            SourceChannelCombo.Items.Add(new ComboBoxItem
            {
                Content = $"通道{ch.Index}: {ch.Name}",
                Tag = ch.Index - 1
            });
        }
        if (SourceChannelCombo.Items.Count > 0)
            SourceChannelCombo.SelectedIndex = 0;

        UpdateTriggerInfo();
    }

    private void NumberOnly_Preview(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !double.TryParse(e.Text, out _) && e.Text != "." && e.Text != "-";
    }

    private void UpdateTriggerInfo()
    {
        var preSamples = (int)(PreTriggerSeconds * SampleRate);
        var postSamples = (int)(PostTriggerSeconds * SampleRate);
        var totalSamples = preSamples + postSamples;
        var totalDuration = PreTriggerSeconds + PostTriggerSeconds;

        TriggerInfoText.Text =
            $"采样率: {SampleRate:F0} Hz\n" +
            $"预触发样本数: {preSamples:N0} ({PreTriggerSeconds:F2}秒)\n" +
            $"后触发样本数: {postSamples:N0} ({PostTriggerSeconds:F2}秒)\n" +
            $"总样本数: {totalSamples:N0} ({totalDuration:F2}秒)\n" +
            $"触发模式: {Mode} - {(Mode == TriggerMode.Auto ? "每次触发后自动重新武装" : Mode == TriggerMode.Single ? "触发一次后停止" : "需手动重新武装")}";
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        TriggerEnabled = EnableTriggerCheck.IsChecked == true;

        if (SourceChannelCombo.SelectedItem is ComboBoxItem chItem && chItem.Tag is int chIdx)
        {
            TriggerChannelIndex = chIdx;
        }

        if (!double.TryParse(LevelText.Text, out var level))
        {
            MessageBox.Show("请输入有效的触发电平", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        TriggerLevel = level;

        if (SlopeCombo.SelectedItem is ComboBoxItem slopeItem && slopeItem.Tag is string slopeTag)
        {
            Slope = slopeTag == "Falling" ? TriggerSlope.Falling : TriggerSlope.Rising;
        }

        if (ModeCombo.SelectedItem is ComboBoxItem modeItem && modeItem.Tag is string modeTag)
        {
            Mode = modeTag switch
            {
                "Normal" => TriggerMode.Normal,
                "Auto" => TriggerMode.Auto,
                "Single" => TriggerMode.Single,
                _ => TriggerMode.Normal
            };
        }

        if (!double.TryParse(PreTriggerText.Text, out var preSec) || preSec < 0)
        {
            MessageBox.Show("请输入有效的预触发时长", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        PreTriggerSeconds = preSec;

        if (!double.TryParse(PostTriggerText.Text, out var postSec) || postSec <= 0)
        {
            MessageBox.Show("请输入有效的后触发时长", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        PostTriggerSeconds = postSec;

        if (PreTriggerSeconds + PostTriggerSeconds > 300)
        {
            MessageBox.Show("总触发时长不能超过 300 秒", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
