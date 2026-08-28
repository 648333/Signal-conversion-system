using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DH.Channels;
using DH.Core.Models;

namespace DH.Shell.Views;

public partial class ChannelConfigWindow : Window
{
    private readonly ChannelManager _channelManager;

    public ChannelConfigWindow(ChannelManager channelManager)
    {
        InitializeComponent();
        _channelManager = channelManager;
        ChannelGrid.ItemsSource = _channelManager.Channels;
        UpdateActiveCount();
    }

    private void SampleRate_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (SampleRateCombo.SelectedItem is ComboBoxItem item && item.Tag is string tagStr)
        {
            if (double.TryParse(tagStr, out var rate))
            {
                _channelManager.ApplySampleRateToAll(rate);
                UpdateActiveCount();
            }
        }
    }

    private void ApplyChannelCount_Click(object sender, RoutedEventArgs e)
    {
        if (int.TryParse(ChannelCountText.Text, out var count) && count > 0 && count <= 256)
        {
            while (_channelManager.Channels.Count < count)
            {
                var idx = _channelManager.Channels.Count + 1;
                _channelManager.AddChannel(new ChannelConfig
                {
                    Index = idx,
                    Name = $"通道{idx}",
                    ChannelType = ChannelType.Analog,
                    MeasureType = MeasureType.InnerInput,
                    Sensitivity = 100,
                    Unit = "mV",
                    Range = 10000,
                    Enabled = true
                });
            }
            while (_channelManager.Channels.Count > count)
            {
                _channelManager.RemoveChannel(_channelManager.Channels.Count - 1);
            }
            UpdateActiveCount();
        }
    }

    private void SelectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var ch in _channelManager.Channels)
            ch.Enabled = true;
        ChannelGrid.Items.Refresh();
        UpdateActiveCount();
    }

    private void SelectNone_Click(object sender, RoutedEventArgs e)
    {
        foreach (var ch in _channelManager.Channels)
            ch.Enabled = false;
        ChannelGrid.Items.Refresh();
        UpdateActiveCount();
    }

    private void ChannelGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ChannelGrid.SelectedItem is ChannelConfig ch)
        {
            ch.Enabled = !ch.Enabled;
            ChannelGrid.Items.Refresh();
            UpdateActiveCount();
        }
    }

    private void NumberOnly_Preview(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !int.TryParse(e.Text, out _);
    }

    private void UpdateActiveCount()
    {
        ActiveCountText.Text = _channelManager.ActiveChannelCount.ToString();
    }

    private void OK_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}