using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DH.Channels;
using DH.SignalProcessing;

namespace DH.Shell.Views;

public partial class SpectrumConfigWindow : Window
{
    private readonly ChannelManager _channelManager;
    private readonly double _sampleRate;

    public int SelectedChannelIndex { get; private set; }
    public SpectrumType SpectrumType { get; private set; } = SpectrumType.Magnitude;
    public WindowType WindowType { get; private set; } = WindowType.Hanning;
    public int FftSize { get; private set; } = 2048;
    public int AverageCount { get; private set; }
    public string AverageType { get; private set; } = "Linear";
    public bool LogScaleY { get; private set; } = true;
    public bool LogScaleX { get; private set; }
    public bool ShowPeaks { get; private set; } = true;
    public bool ShowGrid { get; private set; } = true;
    public double YMin { get; private set; } = -120;
    public double YMax { get; private set; } = 10;
    public double FreqMax { get; private set; } = 500;
    public bool CursorEnabled { get; private set; }
    public double Cursor1Freq { get; private set; }
    public double Cursor2Freq { get; private set; }

    public SpectrumConfigWindow(ChannelManager channelManager, double sampleRate)
    {
        InitializeComponent();
        _channelManager = channelManager;
        _sampleRate = sampleRate;

        InitChannelCombo();
        SetDefaults();
    }

    private void InitChannelCombo()
    {
        ChannelCombo.Items.Clear();
        for (int i = 0; i < _channelManager.Channels.Count; i++)
        {
            var ch = _channelManager.Channels[i];
            ChannelCombo.Items.Add(new ComboBoxItem
            {
                Content = $"通道{ch.Index}: {ch.Name}",
                Tag = i
            });
        }
        if (ChannelCombo.Items.Count > 0)
            ChannelCombo.SelectedIndex = 0;
    }

    private void SetDefaults()
    {
        SpectrumTypeCombo.SelectedIndex = 0;
        WindowCombo.SelectedIndex = 0;
        FftSizeCombo.SelectedIndex = 3;
        AverageCombo.SelectedIndex = 0;
        AverageTypeCombo.SelectedIndex = 0;

        FreqMaxText.Text = (_sampleRate / 2).ToString("F0");
        FreqMax = _sampleRate / 2;

        Cursor1Slider.Maximum = _sampleRate / 2;
        Cursor2Slider.Maximum = _sampleRate / 2;
    }

    private void CursorCheck_Checked(object sender, RoutedEventArgs e)
    {
        CursorPanel.Visibility = Visibility.Visible;
        CursorEnabled = true;
    }

    private void CursorCheck_Unchecked(object sender, RoutedEventArgs e)
    {
        CursorPanel.Visibility = Visibility.Collapsed;
        CursorEnabled = false;
    }

    private void Cursor1Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        Cursor1Freq = e.NewValue;
        UpdateCursorDisplay();
    }

    private void Cursor2Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        Cursor2Freq = e.NewValue;
        UpdateCursorDisplay();
    }

    private void UpdateCursorDisplay()
    {
        if (Cursor1FreqText != null)
            Cursor1FreqText.Text = $"频率: {Cursor1Freq:F1} Hz";
        if (Cursor2FreqText != null)
            Cursor2FreqText.Text = $"频率: {Cursor2Freq:F1} Hz";
        if (CursorDeltaFreqText != null)
            CursorDeltaFreqText.Text = $"Δ频率: {Math.Abs(Cursor2Freq - Cursor1Freq):F1} Hz";
    }

    private void ResetDefault_Click(object sender, RoutedEventArgs e)
    {
        SpectrumTypeCombo.SelectedIndex = 0;
        WindowCombo.SelectedIndex = 0;
        FftSizeCombo.SelectedIndex = 3;
        AverageCombo.SelectedIndex = 0;
        AverageTypeCombo.SelectedIndex = 0;
        LogYCheck.IsChecked = true;
        LogXCheck.IsChecked = false;
        ShowPeaksCheck.IsChecked = true;
        ShowGridCheck.IsChecked = true;
        YMinText.Text = "-120";
        YMaxText.Text = "10";
        FreqMaxText.Text = (_sampleRate / 2).ToString("F0");
        CursorCheck.IsChecked = false;
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (ChannelCombo.SelectedItem is ComboBoxItem chItem && chItem.Tag is int chIdx)
            SelectedChannelIndex = chIdx;

        if (SpectrumTypeCombo.SelectedItem is ComboBoxItem stItem && stItem.Tag is string st)
        {
            SpectrumType = st switch
            {
                "Magnitude" => SpectrumType.Magnitude,
                "Power" => SpectrumType.Power,
                "PSD" => SpectrumType.PowerSpectralDensity,
                _ => SpectrumType.Magnitude
            };
        }

        if (WindowCombo.SelectedItem is ComboBoxItem wItem && wItem.Tag is string wt)
        {
            WindowType = wt switch
            {
                "Hanning" => WindowType.Hanning,
                "Hamming" => WindowType.Hamming,
                "Blackman" => WindowType.Blackman,
                "BlackmanHarris" => WindowType.BlackmanHarris,
                "FlatTop" => WindowType.FlatTop,
                "Rectangle" => WindowType.Rectangle,
                "Triangle" => WindowType.Triangle,
                "Kaiser" => WindowType.Kaiser,
                _ => WindowType.Hanning
            };
        }

        if (FftSizeCombo.SelectedItem is ComboBoxItem fItem && fItem.Tag is string fs)
            int.TryParse(fs, out var fftSize);

        if (AverageCombo.SelectedItem is ComboBoxItem aItem && aItem.Tag is string avg)
            int.TryParse(avg, out var avgCount);

        if (AverageTypeCombo.SelectedItem is ComboBoxItem atItem && atItem.Tag is string at)
            AverageType = at;

        LogScaleY = LogYCheck.IsChecked == true;
        LogScaleX = LogXCheck.IsChecked == true;
        ShowPeaks = ShowPeaksCheck.IsChecked == true;
        ShowGrid = ShowGridCheck.IsChecked == true;

        double.TryParse(YMinText.Text, out var yMin);
        double.TryParse(YMaxText.Text, out var yMax);
        YMin = yMin;
        YMax = yMax;

        double.TryParse(FreqMaxText.Text, out var freqMax);
        FreqMax = Math.Clamp(freqMax, 1, _sampleRate / 2);

        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
