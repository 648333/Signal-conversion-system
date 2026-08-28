using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DH.Channels;
using DH.SignalProcessing;

namespace DH.Shell.Views;

public partial class FilterConfigWindow : Window
{
    private readonly ChannelManager _channelManager;

    public bool FilterEnabled { get; private set; }
    public int SelectedChannelIndex { get; private set; }
    public FilterType FilterType { get; private set; } = FilterType.LowPass;
    public FilterDesign FilterDesign { get; private set; } = FilterDesign.Butterworth;
    public double CutoffFreq1 { get; private set; } = 100;
    public double CutoffFreq2 { get; private set; } = 500;
    public int Order { get; private set; } = 4;
    public double SampleRate { get; private set; }

    public FilterConfigWindow(ChannelManager channelManager, double sampleRate)
    {
        InitializeComponent();
        _channelManager = channelManager;
        SampleRate = sampleRate;
        SampleRateText.Text = sampleRate.ToString("F0");

        // 填充通道列表
        foreach (var ch in _channelManager.Channels)
        {
            ChannelCombo.Items.Add(new ComboBoxItem
            {
                Content = $"通道{ch.Index}: {ch.Name}",
                Tag = ch.Index - 1
            });
        }
        if (ChannelCombo.Items.Count > 0)
            ChannelCombo.SelectedIndex = 0;

        FilterTypeCombo.SelectedIndex = 0;
        UpdateFilterDescription();
        UpdateCutoff2Visibility();
    }

    private void ChannelCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ChannelCombo.SelectedItem is ComboBoxItem item && item.Tag is int idx)
        {
            SelectedChannelIndex = idx;
        }
    }

    private void FilterTypeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateCutoff2Visibility();
        UpdateFilterDescription();
    }

    private void EnableFilter_Checked(object sender, RoutedEventArgs e)
    {
        FilterEnabled = EnableFilterCheck.IsChecked == true;
    }

    private void UpdateCutoff2Visibility()
    {
        if (FilterTypeCombo.SelectedItem is ComboBoxItem item && item.Tag is string tag)
        {
            var isBand = tag == "BandPass" || tag == "BandStop";
            Cutoff2Label.Opacity = isBand ? 1.0 : 0.3;
            Cutoff2Text.IsEnabled = isBand;
        }
    }

    private void UpdateFilterDescription()
    {
        if (FilterTypeCombo.SelectedItem is ComboBoxItem typeItem && typeItem.Tag is string typeTag
            && DesignCombo.SelectedItem is ComboBoxItem designItem && designItem.Tag is string designTag
            && OrderCombo.SelectedItem is ComboBoxItem orderItem && orderItem.Tag is string orderTagStr
            && int.TryParse(orderTagStr, out var order))
        {
            var typeName = typeTag switch
            {
                "LowPass" => "低通滤波器",
                "HighPass" => "高通滤波器",
                "BandPass" => "带通滤波器",
                "BandStop" => "带阻滤波器",
                _ => "滤波器"
            };

            var designName = designTag switch
            {
                "Butterworth" => "Butterworth（巴特沃斯）",
                "Bessel" => "Bessel（贝塞尔）",
                "Chebyshev1" => "Chebyshev I 型",
                "Chebyshev2" => "Chebyshev II 型",
                _ => designTag
            };

            FilterDescriptionText.Text =
                $"{designName} {typeName}\n" +
                $"阶数: {order} 阶\n" +
                $"采样率: {SampleRate:F0} Hz\n" +
                $"截止频率 1: {CutoffFreq1:F1} Hz\n" +
                $"截止频率 2: {CutoffFreq2:F1} Hz (仅带通/带阻有效)\n\n" +
                $"注意: 截止频率必须小于采样率的一半 (奈奎斯特频率 = {SampleRate / 2:F0} Hz)";
        }
    }

    private void NumberOnly_Preview(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !double.TryParse(e.Text, out _) && e.Text != ".";
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        FilterEnabled = EnableFilterCheck.IsChecked == true;

        if (!double.TryParse(Cutoff1Text.Text, out var cf1) || cf1 <= 0)
        {
            MessageBox.Show("请输入有效的截止频率 1", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        if (!double.TryParse(Cutoff2Text.Text, out var cf2) || cf2 <= 0)
        {
            MessageBox.Show("请输入有效的截止频率 2", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        if (FilterTypeCombo.SelectedItem is ComboBoxItem typeItem && typeItem.Tag is string typeTag)
        {
            FilterType = typeTag switch
            {
                "LowPass" => FilterType.LowPass,
                "HighPass" => FilterType.HighPass,
                "BandPass" => FilterType.BandPass,
                "BandStop" => FilterType.BandStop,
                _ => FilterType.LowPass
            };
        }

        if (DesignCombo.SelectedItem is ComboBoxItem designItem && designItem.Tag is string designTag)
        {
            FilterDesign = designTag switch
            {
                "Butterworth" => FilterDesign.Butterworth,
                "Bessel" => FilterDesign.Bessel,
                "Chebyshev1" => FilterDesign.Chebyshev1,
                "Chebyshev2" => FilterDesign.Chebyshev2,
                _ => FilterDesign.Butterworth
            };
        }

        if (OrderCombo.SelectedItem is ComboBoxItem orderItem && orderItem.Tag is string orderTagStr
            && int.TryParse(orderTagStr, out var order))
        {
            Order = order;
        }

        CutoffFreq1 = cf1;
        CutoffFreq2 = cf2;

        // 验证
        var nyquist = SampleRate / 2;
        if (cf1 >= nyquist)
        {
            MessageBox.Show($"截止频率 1 必须小于奈奎斯特频率 ({nyquist:F0} Hz)", "错误",
                MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        if ((FilterType == FilterType.BandPass || FilterType == FilterType.BandStop) && cf2 >= nyquist)
        {
            MessageBox.Show($"截止频率 2 必须小于奈奎斯特频率 ({nyquist:F0} Hz)", "错误",
                MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        if ((FilterType == FilterType.BandPass || FilterType == FilterType.BandStop) && cf2 <= cf1)
        {
            MessageBox.Show("截止频率 2 必须大于截止频率 1", "错误",
                MessageBoxButton.OK, MessageBoxImage.Error);
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
