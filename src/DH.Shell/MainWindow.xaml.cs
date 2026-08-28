using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Ribbon;
using System.Windows.Threading;
using DH.Acquisition;
using DH.Channels;
using DH.Core.Logging;
using DH.Core.Models;
using DH.Core.Services;
using DH.Core.Events;
using DH.Hardware;
using DH.Shell.ViewModels;
using DH.Shell.Views;
using DH.SignalProcessing;
using DH.Visualization;

namespace DH.Shell;

public partial class MainWindow : Window
{
    private readonly AppServices _services;
    private readonly ILogService _log;
    private readonly EventBus _eventBus;
    private readonly DispatcherTimer _clockTimer;
    private readonly MainViewModel _viewModel;

    private readonly HardwareManager _hardwareManager;
    private readonly ChannelManager _channelManager;
    private readonly AcquisitionEngine _acqEngine;
    private readonly DataStorageService _dataStorage;
    private readonly ProjectService _projectService;

    private RecorderChart? _recorderChart;
    private SpectrumChart? _spectrumChart;
    private DataPlaybackService? _playbackService;
    private readonly SpectrumAnalyzer _spectrumAnalyzer = new();
    private SimulatedDevice? _connectedDevice;
    private readonly DispatcherTimer _fftTimer;
    private readonly float[] _fftBuffer = new float[2048];
    private int _fftBufferPos;

    public MainWindow(AppServices services)
    {
        InitializeComponent();
        _services = services;
        _log = services.GetService<ILogService>();
        _eventBus = services.GetService<EventBus>();
        _viewModel = new MainViewModel(services);
        DataContext = _viewModel;

        _hardwareManager = new HardwareManager();
        _hardwareManager.RegisterDriver(new SimulatedDriver());
        _channelManager = new ChannelManager();
        _acqEngine = new AcquisitionEngine();
        _dataStorage = new DataStorageService();
        _projectService = new ProjectService();

        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clockTimer.Tick += (_, _) => ClockText.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        _eventBus.Subscribe<DeviceConnectedEvent>(OnDeviceConnected);
        _eventBus.Subscribe<DeviceDisconnectedEvent>(OnDeviceDisconnected);
        _eventBus.Subscribe<AcquisitionStartedEvent>(OnAcquisitionStarted);
        _eventBus.Subscribe<AcquisitionStoppedEvent>(OnAcquisitionStopped);

        _acqEngine.PropertyChanged += OnAcqEngineStateChanged;

        _fftTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _fftTimer.Tick += OnFftTimerTick;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        _clockTimer.Start();
        _log.Info("主窗口加载完成");
        StatusText.Text = "就绪";
        UpdateAcqStatus(AcquisitionState.Idle);

        InitDefaultChannels();
        SetupRecorderChart();
    }

    private void InitDefaultChannels()
    {
        for (int i = 1; i <= 8; i++)
        {
            _channelManager.AddChannel(new ChannelConfig
            {
                Index = i,
                Name = $"通道{i}",
                ChannelType = ChannelType.Analog,
                MeasureType = MeasureType.InnerInput,
                Sensitivity = 100,
                Unit = "mV",
                Range = 10000,
                Enabled = true
            });
        }
        _viewModel.Channels.Clear();
        foreach (var ch in _channelManager.Channels)
            _viewModel.Channels.Add(ch);

        ChannelGrid.ItemsSource = _viewModel.Channels;
    }

    private void SetupRecorderChart()
    {
        var colors = new[]
        {
            System.Windows.Media.Color.FromRgb(0x4C, 0xAF, 0x50),
            System.Windows.Media.Color.FromRgb(0x21, 0x96, 0xF3),
            System.Windows.Media.Color.FromRgb(0xFF, 0x98, 0x00),
            System.Windows.Media.Color.FromRgb(0xE9, 0x1E, 0x63),
            System.Windows.Media.Color.FromRgb(0x9C, 0x27, 0xB0),
            System.Windows.Media.Color.FromRgb(0x00, 0xBC, 0xD4),
            System.Windows.Media.Color.FromRgb(0xFF, 0xEB, 0x3B),
            System.Windows.Media.Color.FromRgb(0x7C, 0xC4, 0xFF),
        };

        _recorderChart = new RecorderChart { Title = "记录仪" };
        for (int i = 0; i < _channelManager.Channels.Count; i++)
        {
            _recorderChart.AddChannel(i, _channelManager.Channels[i].Name, colors[i % colors.Length]);
        }

        var tab = ChartTabControl.Items[0] as TabItem;
        if (tab != null)
        {
            var grid = new Grid();
            grid.Children.Add(_recorderChart);
            tab.Content = grid;
        }
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        _clockTimer.Stop();
        _fftTimer.Stop();
        _dataStorage.Dispose();
        _playbackService?.Dispose();
        _log.Info("主窗口关闭");
    }

    private void NewProject_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "DH工程文件|*.dhproj",
            Title = "新建工程",
            FileName = $"工程_{DateTime.Now:yyyyMMdd}"
        };
        if (dlg.ShowDialog() == true)
        {
            var name = Path.GetFileNameWithoutExtension(dlg.FileName);
            var dir = Path.GetDirectoryName(dlg.FileName) ?? "";
            _projectService.NewProject(name, dir);
            _services.GetService<AppState>().CurrentProject = _projectService.CurrentProject;
            TreeRoot.Header = name;
            _log.Info($"新建工程: {name}");
            StatusText.Text = $"工程: {name}";
        }
    }

    private void OpenProject_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "DH工程文件|*.dhproj|所有文件|*.*",
            Title = "打开工程"
        };
        if (dlg.ShowDialog() == true)
        {
            _projectService.LoadProject(dlg.FileName);
            if (_projectService.CurrentProject != null)
            {
                _services.GetService<AppState>().CurrentProject = _projectService.CurrentProject;
                TreeRoot.Header = _projectService.CurrentProject.Name;
                StatusText.Text = $"已加载: {_projectService.CurrentProject.Name}";
                _log.Info($"打开工程: {dlg.FileName}");
            }
        }
    }

    private void SaveProject_Click(object sender, RoutedEventArgs e)
    {
        if (_projectService.CurrentProject == null)
        {
            NewProject_Click(sender, e);
            return;
        }
        _projectService.SaveProject();
        _log.Info("保存工程");
        StatusText.Text = "工程已保存";
    }

    private void ScanDevices_Click(object sender, RoutedEventArgs e)
    {
        _log.Info("扫描设备...");
        StatusText.Text = "正在扫描设备...";
        _hardwareManager.ScanDevices();

        TreeDevices.Items.Clear();
        foreach (var dev in _hardwareManager.AvailableDevices)
        {
            TreeDevices.Items.Add(new TreeViewItem
            {
                Header = $"{dev.ModelName} [{dev.SerialNumber}]",
                Tag = dev
            });
        }

        var count = _hardwareManager.AvailableDevices.Count;
        StatusText.Text = count > 0 ? $"扫描完成: 发现 {count} 台设备" : "扫描完成: 未发现设备";
    }

    private void ConnectDevice_Click(object sender, RoutedEventArgs e)
    {
        if (_hardwareManager.AvailableDevices.Count == 0)
        {
            MessageBox.Show("请先扫描设备", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var firstDev = _hardwareManager.AvailableDevices[0];
        _connectedDevice = _hardwareManager.ConnectDevice(firstDev) as SimulatedDevice;

        if (_connectedDevice != null)
        {
            _connectedDevice.SetSampleRate(_channelManager.SampleRate);
            _connectedDevice.DataAvailable += OnDeviceDataAvailable;
            _eventBus.Publish(new DeviceConnectedEvent
            {
                DeviceId = firstDev.Id,
                DeviceName = firstDev.ModelName
            });
            _log.Info($"设备已连接: {firstDev.ModelName}");
        }
    }

    private void DisconnectDevice_Click(object sender, RoutedEventArgs e)
    {
        if (_connectedDevice != null)
        {
            _connectedDevice.DataAvailable -= OnDeviceDataAvailable;
            _hardwareManager.DisconnectDevice(_connectedDevice.Info.Id);
            _eventBus.Publish(new DeviceDisconnectedEvent { DeviceId = _connectedDevice.Info.Id });
            _connectedDevice = null;
            _log.Info("设备已断开");
        }
    }

    private void ChannelSetup_Click(object sender, RoutedEventArgs e)
    {
        var win = new ChannelConfigWindow(_channelManager) { Owner = this };
        if (win.ShowDialog() == true)
        {
            _viewModel.Channels.Clear();
            foreach (var ch in _channelManager.Channels)
                _viewModel.Channels.Add(ch);

            if (_recorderChart != null)
            {
                _recorderChart.Clear();
                var colors = new[]
                {
                    System.Windows.Media.Color.FromRgb(0x4C, 0xAF, 0x50),
                    System.Windows.Media.Color.FromRgb(0x21, 0x96, 0xF3),
                    System.Windows.Media.Color.FromRgb(0xFF, 0x98, 0x00),
                    System.Windows.Media.Color.FromRgb(0xE9, 0x1E, 0x63),
                    System.Windows.Media.Color.FromRgb(0x9C, 0x27, 0xB0),
                    System.Windows.Media.Color.FromRgb(0x00, 0xBC, 0xD4),
                    System.Windows.Media.Color.FromRgb(0xFF, 0xEB, 0x3B),
                    System.Windows.Media.Color.FromRgb(0x7C, 0xC4, 0xFF),
                };
                for (int i = 0; i < _channelManager.Channels.Count; i++)
                {
                    _recorderChart.AddChannel(i, _channelManager.Channels[i].Name, colors[i % colors.Length]);
                }
            }

            if (_connectedDevice != null)
                _connectedDevice.SetSampleRate(_channelManager.SampleRate);

            _log.Info($"通道配置已更新: {_channelManager.ActiveChannelCount} 个有效通道");
        }
    }

    private void StartAcquisition_Click(object sender, RoutedEventArgs e)
    {
        if (_connectedDevice == null)
        {
            MessageBox.Show("请先连接设备", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _connectedDevice.SetSampleRate(_channelManager.SampleRate);

        var eventName = $"事件_{DateTime.Now:HHmmss}";
        if (_projectService.IsProjectOpen)
        {
            var dataFile = Path.Combine(_projectService.GetDataDirectory(), eventName + ".dat");
            _dataStorage.StartRecording(dataFile, _channelManager.ActiveChannelCount,
                _channelManager.SampleRate, SaveFormat.Float);
        }

        _connectedDevice.StartAcquisition();
        _acqEngine.Start();
        _fftBufferPos = 0;
        if (_spectrumChart != null)
            _fftTimer.Start();
        _eventBus.Publish(new AcquisitionStartedEvent { EventName = eventName });
    }

    private void PauseAcquisition_Click(object sender, RoutedEventArgs e)
    {
        _acqEngine.Pause();
        if (_connectedDevice != null)
            _connectedDevice.StopAcquisition();
        _log.Info("暂停采集");
    }

    private void StopAcquisition_Click(object sender, RoutedEventArgs e)
    {
        if (_connectedDevice != null)
            _connectedDevice.StopAcquisition();
        _acqEngine.Stop();
        _fftTimer.Stop();
        _dataStorage.StopRecording();
        _eventBus.Publish(new AcquisitionStoppedEvent());
    }

    private void OnDeviceDataAvailable(object? sender, DataAvailableEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            if (_recorderChart != null && e.SamplesRead > 0)
            {
                _recorderChart.UpdateData(e.Data, _channelManager.Channels.Count);
            }

            if (_dataStorage.IsRecording)
            {
                _dataStorage.WriteData(e.Data, e.SamplesRead);
            }

            _acqEngine.PushData(e.Data, e.SamplesRead);

            var samplesToCopy = Math.Min(e.SamplesRead, _fftBuffer.Length - _fftBufferPos);
            if (samplesToCopy > 0)
            {
                Array.Copy(e.Data, 0, _fftBuffer, _fftBufferPos, samplesToCopy);
                _fftBufferPos += samplesToCopy;
            }
        });
    }

    private void OnAcqEngineStateChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AcquisitionEngine.State))
        {
            Dispatcher.Invoke(() => UpdateAcqStatus(_acqEngine.State));
        }
    }

    private void AddRecorder_Click(object sender, RoutedEventArgs e)
    {
        var tab = new TabItem { Header = $"记录仪 {ChartTabControl.Items.Count + 1}" };
        var recorder = new RecorderChart { Title = "记录仪" };

        var colors = new[]
        {
            System.Windows.Media.Color.FromRgb(0x4C, 0xAF, 0x50),
            System.Windows.Media.Color.FromRgb(0x21, 0x96, 0xF3),
            System.Windows.Media.Color.FromRgb(0xFF, 0x98, 0x00),
            System.Windows.Media.Color.FromRgb(0xE9, 0x1E, 0x63),
        };

        for (int i = 0; i < _channelManager.Channels.Count && i < 4; i++)
        {
            recorder.AddChannel(i, _channelManager.Channels[i].Name, colors[i % colors.Length]);
        }

        var grid = new Grid();
        grid.Children.Add(recorder);
        tab.Content = grid;
        ChartTabControl.Items.Add(tab);
        ChartTabControl.SelectedItem = tab;
    }

    private void StyleItem_Selected(object sender, RoutedEventArgs e)
    {
        if (sender is RibbonGalleryItem item)
            _log.Info($"切换样式: {item.Content}");
    }

    private void PackageItem_Selected(object sender, RoutedEventArgs e)
    {
        if (sender is RibbonGalleryItem item)
        {
            var tag = item.Tag?.ToString() ?? "CommonSoft";
            _services.GetService<AppState>().CurrentSoftwarePackage = tag;
            _log.Info($"切换软件包: {item.Content} ({tag})");
            StatusText.Text = $"软件包: {item.Content}";
        }
    }

    private void SetChinese_Click(object sender, RoutedEventArgs e)
    {
        _services.GetService<AppState>().CurrentLanguage = "zh-CN";
        _eventBus.Publish(new LanguageChangedEvent { Language = "zh-CN" });
        _log.Info("切换语言: 中文");
    }

    private void SetEnglish_Click(object sender, RoutedEventArgs e)
    {
        _services.GetService<AppState>().CurrentLanguage = "en-US";
        _eventBus.Publish(new LanguageChangedEvent { Language = "en-US" });
        _log.Info("Switch language: English");
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            "DH-RTDAS 实时数据测量与分析系统\n" +
            "版本: 1.0.0\n" +
            "功能等同于东华DHDAS\n\n" +
            "支持: 振动/应变/力/压力/声音/转速等信号的\n" +
            "实时采集、显示、存储、回放和工程分析",
            "关于 DH-RTDAS",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void OnDeviceConnected(DeviceConnectedEvent e)
    {
        Dispatcher.Invoke(() => DeviceStatus.Text = $"设备: {e.DeviceName} [已连接]");
    }

    private void OnDeviceDisconnected(DeviceDisconnectedEvent e)
    {
        Dispatcher.Invoke(() => DeviceStatus.Text = "设备: 未连接");
    }

    private void OnAcquisitionStarted(AcquisitionStartedEvent e)
    {
        Dispatcher.Invoke(() =>
        {
            UpdateAcqStatus(AcquisitionState.Acquiring);
            StatusText.Text = $"采集中 - 事件: {e.EventName}";
        });
    }

    private void OnAcquisitionStopped(AcquisitionStoppedEvent e)
    {
        Dispatcher.Invoke(() => UpdateAcqStatus(AcquisitionState.Stopped));
    }

    private void UpdateAcqStatus(AcquisitionState state)
    {
        AcqStatus.Text = $"采集: {state switch
        {
            AcquisitionState.Idle => "停止",
            AcquisitionState.Acquiring => "采集中",
            AcquisitionState.Paused => "暂停",
            AcquisitionState.Stopped => "已停止",
            AcquisitionState.Frozen => "冻结",
            _ => state.ToString()
        }}";
    }

    private void AddSpectrumChart_Click(object sender, RoutedEventArgs e)
    {
        var tab = new TabItem { Header = "FFT 频谱" };
        _spectrumChart = new SpectrumChart
        {
            Title = "FFT 频谱分析",
            LogScaleY = true,
            ShowPeaks = true,
            YMin = -120,
            YMax = 10
        };

        var grid = new Grid();
        grid.Children.Add(_spectrumChart);
        tab.Content = grid;
        ChartTabControl.Items.Add(tab);
        ChartTabControl.SelectedItem = tab;

        if (_acqEngine.State == AcquisitionState.Acquiring)
            _fftTimer.Start();

        _log.Info("FFT 频谱图已添加");
    }

    private void OnFftTimerTick(object? sender, EventArgs e)
    {
        if (_spectrumChart == null || _fftBufferPos < _fftBuffer.Length)
            return;

        var channelCount = _channelManager.Channels.Count;
        if (channelCount <= 0) return;

        var channelData = new float[_fftBuffer.Length / channelCount];
        for (int i = 0; i < channelData.Length; i++)
        {
            channelData[i] = _fftBuffer[i * channelCount];
        }

        var spectrum = _spectrumAnalyzer.ComputeMagnitudeSpectrum(channelData, _channelManager.SampleRate);
        _spectrumChart.SetSpectrum(spectrum, 0);

        _fftBufferPos = 0;
    }

    private void OpenDataFile_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "DH数据文件|*.dat|所有文件|*.*",
            Title = "打开数据文件"
        };

        if (dlg.ShowDialog() != true) return;

        _playbackService?.Dispose();
        _playbackService = new DataPlaybackService();

        if (!_playbackService.Open(dlg.FileName))
        {
            MessageBox.Show("无法打开数据文件", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var info = _playbackService.Info!;
        StatusText.Text = $"数据已加载: {info.ChannelCount} 通道, {info.SampleRate:F0} Hz, {info.DurationSeconds:F1}s";
        _log.Info($"打开数据文件: {dlg.FileName} ({info.ChannelCount}ch, {info.SampleRate}Hz, {info.DurationSeconds:F1}s)");

        var tab = new TabItem { Header = "数据回放" };
        var panel = new StackPanel { Margin = new Thickness(10) };

        var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
        var btnPlay = new Button { Content = "播放", Width = 60, Margin = new Thickness(0, 0, 5, 0) };
        var btnPause = new Button { Content = "暂停", Width = 60, Margin = new Thickness(0, 0, 5, 0) };
        var btnStop = new Button { Content = "停止", Width = 60, Margin = new Thickness(0, 0, 5, 0) };
        btnPlay.Click += (_, _) => _playbackService.Play();
        btnPause.Click += (_, _) => _playbackService.Pause();
        btnStop.Click += (_, _) => _playbackService.Stop();
        btnPanel.Children.Add(btnPlay);
        btnPanel.Children.Add(btnPause);
        btnPanel.Children.Add(btnStop);

        var infoText = new TextBlock
        {
            Text = $"采样率: {info.SampleRate:F0} Hz | 通道数: {info.ChannelCount} | 时长: {info.DurationSeconds:F2}s",
            Margin = new Thickness(0, 0, 0, 10)
        };

        var recorder = new RecorderChart { Title = "回放波形", Height = 400 };
        var colors = new[]
        {
            System.Windows.Media.Color.FromRgb(0x4C, 0xAF, 0x50),
            System.Windows.Media.Color.FromRgb(0x21, 0x96, 0xF3),
            System.Windows.Media.Color.FromRgb(0xFF, 0x98, 0x00),
            System.Windows.Media.Color.FromRgb(0xE9, 0x1E, 0x63),
        };
        for (int i = 0; i < info.ChannelCount && i < 4; i++)
            recorder.AddChannel(i, $"通道{i + 1}", colors[i % colors.Length]);

        _playbackService.DataBlockRead += (data, chCount) =>
        {
            Dispatcher.Invoke(() => recorder.UpdateData(data, chCount));
        };

        panel.Children.Add(infoText);
        panel.Children.Add(btnPanel);
        panel.Children.Add(recorder);

        tab.Content = panel;
        ChartTabControl.Items.Add(tab);
        ChartTabControl.SelectedItem = tab;
    }

    private void ShowStatistics_Click(object sender, RoutedEventArgs e)
    {
        if (_fftBufferPos == 0)
        {
            MessageBox.Show("当前无数据可用于统计", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var channelCount = _channelManager.Channels.Count;
        if (channelCount <= 0) return;

        var channelData = new float[_fftBufferPos];
        for (int i = 0; i < channelData.Length; i++)
            channelData[i] = _fftBuffer[i];

        var stats = StatisticsCalculator.Compute(channelData);

        var report = $"通道 1 统计信息:\n" +
                     $"  均值:     {stats.Mean:F6}\n" +
                     $"  有效值:   {stats.Rms:F6}\n" +
                     $"  峰值:     {stats.Peak:F6}\n" +
                     $"  峰峰值:   {stats.PeakToPeak:F6}\n" +
                     $"  标准差:   {stats.StdDev:F6}\n" +
                     $"  方差:     {stats.Variance:F6}\n" +
                     $"  波峰因数: {stats.CrestFactor:F4}\n" +
                     $"  偏度:     {stats.Skewness:F4}\n" +
                     $"  峭度:     {stats.Kurtosis:F4}";

        MessageBox.Show(report, "统计分析", MessageBoxButton.OK, MessageBoxImage.Information);
        _log.Info($"统计计算完成: RMS={stats.Rms:F4}, Peak={stats.Peak:F4}");
    }
}
