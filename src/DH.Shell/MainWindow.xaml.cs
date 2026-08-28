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
    private readonly DispatcherTimer _statsTimer;
    private readonly float[] _fftBuffer = new float[2048];
    private int _fftBufferPos;
    private bool _frozen;
    private DigitalFilter?[]? _channelFilters;
    private TriggerService? _triggerService;
    private bool _triggerEnabled;
    private string _currentEventName = string.Empty;
    private DateTime _acquisitionStartTime;
    private int _spectrumChannelIndex;
    private SpectrumType _spectrumType = SpectrumType.Magnitude;
    private WindowType _spectrumWindow = WindowType.Hanning;

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

        _statsTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _statsTimer.Tick += OnStatsTimerTick;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        _clockTimer.Start();
        _log.Info("主窗口加载完成");
        StatusText.Text = "就绪";
        UpdateAcqStatus(AcquisitionState.Idle);

        InitDefaultChannels();
        SetupRecorderChart();
        PopulateProjectTree();
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
        _statsTimer.Stop();
        _dataStorage.Dispose();
        _playbackService?.Dispose();
        _triggerService?.Dispose();
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

            // 保存初始工程（包含当前通道配置）
            SaveCurrentProject();

            _log.Info($"新建工程: {name}");
            StatusText.Text = $"工程: {name}";
            PopulateProjectTree();
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
            var project = _projectService.LoadProject(dlg.FileName);
            if (project != null)
            {
                _services.GetService<AppState>().CurrentProject = project;
                TreeRoot.Header = project.Name;
                StatusText.Text = $"已加载: {project.Name}";
                _log.Info($"打开工程: {dlg.FileName}");

                // 恢复通道配置
                if (project.Channels.Count > 0)
                {
                    _channelManager.ClearAll();
                    _channelManager.SampleRate = project.SampleRate;
                    foreach (var ch in project.Channels)
                    {
                        _channelManager.AddChannel(new ChannelConfig
                        {
                            Index = ch.Index,
                            Name = ch.Name,
                            SerialNumber = ch.SerialNumber,
                            ChannelType = ch.ChannelType,
                            MeasureType = ch.MeasureType,
                            SensorModel = ch.SensorModel,
                            SensorSerial = ch.SensorSerial,
                            Sensitivity = ch.Sensitivity,
                            Unit = ch.Unit,
                            Coupling = ch.Coupling,
                            Range = ch.Range,
                            FilterCutoff = ch.FilterCutoff,
                            Integration = ch.Integration,
                            IntegralUnit = ch.IntegralUnit,
                            Enabled = ch.Enabled,
                            Offset = ch.Offset,
                            Gain = ch.Gain,
                            CalibrationDate = ch.CalibrationDate,
                            Formula = ch.Formula,
                            BridgeType = ch.BridgeType,
                            BridgeVoltage = ch.BridgeVoltage,
                            SampleRate = ch.SampleRate
                        });
                    }

                    _viewModel.Channels.Clear();
                    foreach (var ch in _channelManager.Channels)
                        _viewModel.Channels.Add(ch);

                    // 更新记录仪图表
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

                    _log.Info($"已加载 {project.Channels.Count} 个通道配置");
                }

                // 恢复软件包和语言设置
                if (!string.IsNullOrEmpty(project.SoftwarePackage))
                    _services.GetService<AppState>().CurrentSoftwarePackage = project.SoftwarePackage;
                if (!string.IsNullOrEmpty(project.Language))
                    _services.GetService<AppState>().CurrentLanguage = project.Language;

                PopulateProjectTree();
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
        SaveCurrentProject();
        _log.Info("保存工程");
        StatusText.Text = "工程已保存";
    }

    private void SaveProjectAs_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "DH工程文件|*.dhproj",
            Title = "工程另存为",
            FileName = _projectService.CurrentProject?.Name ?? $"工程_{DateTime.Now:yyyyMMdd}"
        };
        if (dlg.ShowDialog() == true)
        {
            if (_projectService.CurrentProject == null)
            {
                var name = Path.GetFileNameWithoutExtension(dlg.FileName);
                var dir = Path.GetDirectoryName(dlg.FileName) ?? "";
                _projectService.NewProject(name, dir);
                _services.GetService<AppState>().CurrentProject = _projectService.CurrentProject;
            }

            SaveCurrentProject();
            _projectService.SaveProjectAs(dlg.FileName);
            TreeRoot.Header = Path.GetFileNameWithoutExtension(dlg.FileName);
            _log.Info($"工程另存为: {dlg.FileName}");
            StatusText.Text = $"工程已另存为: {Path.GetFileName(dlg.FileName)}";
        }
    }

    private void SaveCurrentProject()
    {
        if (_projectService.CurrentProject == null) return;

        // 保存通道配置
        _projectService.CurrentProject.Channels.Clear();
        foreach (var ch in _channelManager.Channels)
        {
            _projectService.CurrentProject.Channels.Add(new ChannelConfig
            {
                Index = ch.Index,
                Name = ch.Name,
                SerialNumber = ch.SerialNumber,
                ChannelType = ch.ChannelType,
                MeasureType = ch.MeasureType,
                SensorModel = ch.SensorModel,
                SensorSerial = ch.SensorSerial,
                Sensitivity = ch.Sensitivity,
                Unit = ch.Unit,
                Coupling = ch.Coupling,
                Range = ch.Range,
                FilterCutoff = ch.FilterCutoff,
                Integration = ch.Integration,
                IntegralUnit = ch.IntegralUnit,
                Enabled = ch.Enabled,
                Offset = ch.Offset,
                Gain = ch.Gain,
                CalibrationDate = ch.CalibrationDate,
                Formula = ch.Formula,
                BridgeType = ch.BridgeType,
                BridgeVoltage = ch.BridgeVoltage,
                SampleRate = ch.SampleRate
            });
        }

        _projectService.CurrentProject.ChannelCount = _channelManager.Channels.Count;
        _projectService.CurrentProject.SampleRate = _channelManager.SampleRate;
        _projectService.CurrentProject.SoftwarePackage = _services.GetService<AppState>().CurrentSoftwarePackage;
        _projectService.CurrentProject.Language = _services.GetService<AppState>().CurrentLanguage;

        _projectService.SaveProject();
    }

    private void ScanDevices_Click(object sender, RoutedEventArgs e)
    {
        _log.Info("扫描设备...");
        StatusText.Text = "正在扫描设备...";
        _hardwareManager.ScanDevices();

        PopulateProjectTree();

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

    private void NewParamTemplate_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show("新建参数模板将清空当前所有通道配置，是否继续？",
            "新建参数", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes) return;

        _channelManager.ClearAll();
        InitDefaultChannels();

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

        PopulateProjectTree();
        _log.Info("已新建参数模板（恢复默认8通道）");
        StatusText.Text = "已新建参数模板";
    }

    private void ImportParamTemplate_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "通道参数模板|*.xml|所有文件|*.*",
            Title = "导入参数模板"
        };
        if (dlg.ShowDialog() != true) return;

        if (_channelManager.ImportTemplate(dlg.FileName))
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

            PopulateProjectTree();
            _log.Info($"参数模板导入成功: {dlg.FileName} ({_channelManager.Channels.Count} 通道)");
            StatusText.Text = $"参数模板已导入: {_channelManager.Channels.Count} 通道";
        }
        else
        {
            MessageBox.Show("参数模板导入失败，请检查文件格式。", "错误",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ExportParamTemplate_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "通道参数模板|*.xml|所有文件|*.*",
            Title = "导出参数模板",
            FileName = $"通道模板_{DateTime.Now:yyyyMMdd}"
        };
        if (dlg.ShowDialog() != true) return;

        if (_channelManager.ExportTemplate(dlg.FileName))
        {
            _log.Info($"参数模板导出成功: {dlg.FileName}");
            StatusText.Text = $"参数模板已导出: {dlg.FileName}";
            MessageBox.Show($"参数模板已成功导出到:\n{dlg.FileName}", "导出成功",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            MessageBox.Show("参数模板导出失败。", "错误",
                MessageBoxButton.OK, MessageBoxImage.Error);
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
        _currentEventName = eventName;
        _acquisitionStartTime = DateTime.Now;
        if (_projectService.IsProjectOpen)
        {
            var dataFile = Path.Combine(_projectService.GetDataDirectory(), eventName + ".dat");
            _dataStorage.StartRecording(dataFile, _channelManager.ActiveChannelCount,
                _channelManager.SampleRate, SaveFormat.Float);
        }

        _connectedDevice.StartAcquisition();
        _acqEngine.Start();
        _fftBufferPos = 0;
        _frozen = false;
        if (_spectrumChart != null)
            _fftTimer.Start();
        _statsTimer.Start();

        if (_triggerService != null && _triggerEnabled)
            _triggerService.Start();

        _eventBus.Publish(new AcquisitionStartedEvent { EventName = eventName });
        PopulateProjectTree();
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
        _statsTimer.Stop();
        _dataStorage.StopRecording();
        _triggerService?.Stop();

        // 记录事件到工程
        if (_projectService.IsProjectOpen && _dataStorage.LastDataFile != null)
        {
            var evt = new ExperimentEvent
            {
                Name = _currentEventName,
                StartTime = _acquisitionStartTime.ToString("yyyy-MM-dd HH:mm:ss"),
                EndTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                ChannelCount = _channelManager.ActiveChannelCount,
                SampleRate = _channelManager.SampleRate,
                DataPoints = _dataStorage.TotalSamplesWritten,
                DataFile = _dataStorage.LastDataFile,
                Comment = "自动采集事件"
            };
            _projectService.CurrentProject!.Events.Add(evt);
            SaveCurrentProject();
            _log.Info($"事件已记录到工程: {evt.Name}");
        }

        _eventBus.Publish(new AcquisitionStoppedEvent());
        PopulateProjectTree();
    }

    private void OnDeviceDataAvailable(object? sender, DataAvailableEventArgs e)
    {
        if (_frozen) return;

        Dispatcher.Invoke(() =>
        {
            if (_recorderChart != null && e.SamplesRead > 0)
            {
                var displayData = e.Data;

                // 应用数字滤波器
                if (_channelFilters != null)
                {
                    var channelCount = _channelManager.Channels.Count;
                    var samplesPerCh = e.SamplesRead / channelCount;
                    var filteredData = new float[e.SamplesRead];
                    Array.Copy(e.Data, filteredData, e.SamplesRead);

                    for (int ch = 0; ch < channelCount; ch++)
                    {
                        if (_channelFilters[ch] != null)
                        {
                            var chData = new float[samplesPerCh];
                            for (int s = 0; s < samplesPerCh; s++)
                                chData[s] = filteredData[s * channelCount + ch];

                            var filtered = _channelFilters[ch]!.Process(chData);

                            for (int s = 0; s < samplesPerCh; s++)
                                filteredData[s * channelCount + ch] = filtered[s];
                        }
                    }

                    displayData = filteredData;
                }

                _recorderChart.UpdateData(displayData, _channelManager.Channels.Count);
            }

            if (_dataStorage.IsRecording)
            {
                _dataStorage.WriteData(e.Data, e.SamplesRead);
            }

            _acqEngine.PushData(e.Data, e.SamplesRead);

            // 触发检测
            if (_triggerService != null && _triggerEnabled)
            {
                _triggerService.ProcessData(e.Data, e.SamplesRead);
            }

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

    private void SpectrumConfig_Click(object sender, RoutedEventArgs e)
    {
        if (_spectrumChart == null)
        {
            AddSpectrumChart_Click(sender, e);
            if (_spectrumChart == null) return;
        }

        var win = new SpectrumConfigWindow(_channelManager, _channelManager.SampleRate)
        {
            Owner = this
        };

        if (win.ShowDialog() == true)
        {
            _spectrumChannelIndex = win.SelectedChannelIndex;
            _spectrumType = win.SpectrumType;
            _spectrumWindow = win.WindowType;
            _spectrumAnalyzer.WindowType = win.WindowType;

            _spectrumChart.LogScaleY = win.LogScaleY;
            _spectrumChart.LogScaleX = win.LogScaleX;
            _spectrumChart.ShowPeaks = win.ShowPeaks;
            _spectrumChart.ShowGrid = win.ShowGrid;
            _spectrumChart.YMin = win.YMin;
            _spectrumChart.YMax = win.YMax;
            _spectrumChart.XMax = win.FreqMax;
            _spectrumChart.ShowCursors = win.CursorEnabled;
            _spectrumChart.Cursor1Frequency = win.Cursor1Freq;
            _spectrumChart.Cursor2Frequency = win.Cursor2Freq;

            var chName = _channelManager.Channels[win.SelectedChannelIndex].Name;
            StatusText.Text = $"频谱设置已更新: {chName}, {win.WindowType}, {win.SpectrumType}";
            _log.Info($"频谱配置 - 通道:{chName} 类型:{win.SpectrumType} 窗函数:{win.WindowType} " +
                     $"Y范围:[{win.YMin},{win.YMax}]dB 频率上限:{win.FreqMax}Hz");
        }
    }

    private void OnFftTimerTick(object? sender, EventArgs e)
    {
        if (_spectrumChart == null || _fftBufferPos < _fftBuffer.Length)
            return;

        var channelCount = _channelManager.Channels.Count;
        if (channelCount <= 0 || _spectrumChannelIndex >= channelCount) return;

        var samplesPerCh = _fftBuffer.Length / channelCount;
        var channelData = new float[samplesPerCh];
        for (int i = 0; i < samplesPerCh; i++)
        {
            channelData[i] = _fftBuffer[i * channelCount + _spectrumChannelIndex];
        }

        SpectrumResult spectrum;
        switch (_spectrumType)
        {
            case SpectrumType.Power:
            case SpectrumType.PowerSpectralDensity:
                spectrum = _spectrumAnalyzer.ComputePowerSpectrum(channelData, _channelManager.SampleRate);
                break;
            default:
                spectrum = _spectrumAnalyzer.ComputeMagnitudeSpectrum(channelData, _channelManager.SampleRate);
                break;
        }

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
        var recorder = new RecorderChart { Title = "回放波形", Height = 400 };
        var controlPanel = BuildPlaybackPanel(_playbackService, recorder);

        var panel = new DockPanel { Margin = new Thickness(10) };
        panel.Children.Add(controlPanel);
        DockPanel.SetDock(controlPanel, Dock.Top);
        panel.Children.Add(recorder);

        tab.Content = panel;
        ChartTabControl.Items.Add(tab);
        ChartTabControl.SelectedItem = tab;
    }

    private FrameworkElement BuildPlaybackPanel(DataPlaybackService playback, RecorderChart recorder)
    {
        var info = playback.Info!;
        var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };

        // 信息行
        var infoText = new TextBlock
        {
            Text = $"采样率: {info.SampleRate:F0} Hz | 通道数: {info.ChannelCount} | 总采样: {info.TotalSamples:N0}",
            Foreground = System.Windows.Media.Brushes.LightGray,
            Margin = new Thickness(0, 0, 0, 6)
        };
        panel.Children.Add(infoText);

        // 进度条和时间
        var progressPanel = new Grid { Margin = new Thickness(0, 0, 0, 6) };
        progressPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        progressPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        progressPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var timeCurrent = new TextBlock
        {
            Text = "00:00.000",
            Foreground = System.Windows.Media.Brushes.LightGray,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
            FontFamily = new System.Windows.Media.FontFamily("Consolas")
        };
        Grid.SetColumn(timeCurrent, 0);
        progressPanel.Children.Add(timeCurrent);

        var slider = new Slider
        {
            Minimum = 0,
            Maximum = info.TotalSamples,
            SmallChange = 1,
            LargeChange = (int)(info.SampleRate),
            IsSnapToTickEnabled = false,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(slider, 1);
        progressPanel.Children.Add(slider);

        var timeTotal = new TextBlock
        {
            Text = TimeSpan.FromSeconds(info.DurationSeconds).ToString(@"mm\:ss\.fff"),
            Foreground = System.Windows.Media.Brushes.LightGray,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0),
            FontFamily = new System.Windows.Media.FontFamily("Consolas")
        };
        Grid.SetColumn(timeTotal, 2);
        progressPanel.Children.Add(timeTotal);

        panel.Children.Add(progressPanel);

        // 控制按钮行
        var btnPanel = new StackPanel { Orientation = Orientation.Horizontal };

        var btnPlay = new Button
        {
            Content = "▶ 播放",
            Width = 70,
            Margin = new Thickness(0, 0, 5, 0),
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x4C, 0xAF, 0x50)),
            Foreground = System.Windows.Media.Brushes.White,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(8, 4)
        };
        var btnPause = new Button
        {
            Content = "⏸ 暂停",
            Width = 70,
            Margin = new Thickness(0, 0, 5, 0),
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0x98, 0x00)),
            Foreground = System.Windows.Media.Brushes.White,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(8, 4)
        };
        var btnStop = new Button
        {
            Content = "⏹ 停止",
            Width = 70,
            Margin = new Thickness(0, 0, 5, 0),
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE9, 0x1E, 0x63)),
            Foreground = System.Windows.Media.Brushes.White,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(8, 4)
        };

        btnPlay.Click += (_, _) => playback.Play();
        btnPause.Click += (_, _) => playback.Pause();
        btnStop.Click += (_, _) =>
        {
            playback.Stop();
            slider.Value = 0;
            timeCurrent.Text = "00:00.000";
        };

        btnPanel.Children.Add(btnPlay);
        btnPanel.Children.Add(btnPause);
        btnPanel.Children.Add(btnStop);

        // 速度控制
        var speedLabel = new TextBlock
        {
            Text = "速度:",
            Foreground = System.Windows.Media.Brushes.LightGray,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(15, 0, 4, 0)
        };
        btnPanel.Children.Add(speedLabel);

        var speedCombo = new ComboBox
        {
            Width = 70,
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x2A, 0x2A, 0x3E)),
            Foreground = System.Windows.Media.Brushes.White,
            VerticalAlignment = VerticalAlignment.Center
        };
        foreach (var s in new[] { "0.25x", "0.5x", "1x", "2x", "4x", "8x" })
            speedCombo.Items.Add(s);
        speedCombo.SelectedIndex = 2;
        speedCombo.SelectionChanged += (_, e) =>
        {
            if (speedCombo.SelectedItem is string s)
            {
                var speedStr = s.Replace("x", "");
                if (double.TryParse(speedStr, out var speed))
                    playback.PlaybackSpeed = speed;
            }
        };
        btnPanel.Children.Add(speedCombo);

        // 导出按钮
        var btnExport = new Button
        {
            Content = "导出CSV",
            Width = 75,
            Margin = new Thickness(15, 0, 0, 0),
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x2B, 0x5C, 0x8A)),
            Foreground = System.Windows.Media.Brushes.White,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(8, 4),
            HorizontalAlignment = HorizontalAlignment.Right
        };
        btnExport.Click += (_, _) =>
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "CSV 文件|*.csv|所有文件|*.*",
                Title = "导出 CSV 数据",
                FileName = $"回放数据_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };
            if (dlg.ShowDialog() == true)
            {
                var success = CsvExportService.ExportToCsv(info.FilePath, dlg.FileName);
                if (success)
                    StatusText.Text = $"已导出: {dlg.FileName}";
            }
        };
        btnPanel.Children.Add(btnExport);

        panel.Children.Add(btnPanel);

        // 设置图表数据绑定
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
        for (int i = 0; i < info.ChannelCount && i < 8; i++)
            recorder.AddChannel(i, $"通道{i + 1}", colors[i % colors.Length]);

        // 进度条拖动定位
        bool isDragging = false;
        slider.PreviewMouseLeftButtonDown += (_, _) => isDragging = true;
        slider.PreviewMouseLeftButtonUp += (_, _) =>
        {
            if (isDragging)
            {
                playback.SeekTo((long)slider.Value);
                isDragging = false;
            }
        };

        // 数据块更新时更新进度
        playback.DataBlockRead += (data, chCount) =>
        {
            Dispatcher.Invoke(() =>
            {
                recorder.UpdateData(data, chCount);
                if (!isDragging)
                {
                    slider.Value = playback.CurrentSample;
                    var ts = TimeSpan.FromSeconds(playback.CurrentTime);
                    timeCurrent.Text = ts.ToString(@"mm\:ss\.fff");
                }
            });
        };

        return panel;
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

        var samplesPerChannel = _fftBufferPos / channelCount;
        if (samplesPerChannel == 0) return;

        var report = new StringBuilder();
        report.AppendLine("=== 多通道统计分析 ===\n");

        for (int ch = 0; ch < channelCount; ch++)
        {
            var channelData = new float[samplesPerChannel];
            for (int i = 0; i < samplesPerChannel; i++)
                channelData[i] = _fftBuffer[i * channelCount + ch];

            var stats = StatisticsCalculator.Compute(channelData);
            var chName = _channelManager.Channels[ch].Name;

            report.AppendLine($"[{chName}] 均值:{stats.Mean:F4}  RMS:{stats.Rms:F4}  峰值:{stats.Peak:F4}  峰峰值:{stats.PeakToPeak:F4}  波峰因数:{stats.CrestFactor:F2}  偏度:{stats.Skewness:F3}  峭度:{stats.Kurtosis:F3}");
        }

        var tab = new TabItem { Header = "统计分析" };
        var scrollViewer = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var textBlock = new TextBlock
        {
            Text = report.ToString(),
            FontFamily = new System.Windows.Media.FontFamily("Consolas"),
            FontSize = 13,
            Foreground = System.Windows.Media.Brushes.LightGray,
            Margin = new Thickness(10)
        };
        scrollViewer.Content = textBlock;
        tab.Content = scrollViewer;
        ChartTabControl.Items.Add(tab);
        ChartTabControl.SelectedItem = tab;

        _log.Info($"多通道统计计算完成: {channelCount} 通道, 每通道 {samplesPerChannel} 点");
    }

    private void FreezeAcquisition_Click(object sender, RoutedEventArgs e)
    {
        _frozen = !_frozen;
        StatusText.Text = _frozen ? "数据已冻结" : "数据已解冻";
        _log.Info(_frozen ? "采集冻结" : "采集解冻");

        if (_recorderChart != null)
            _recorderChart.SetFrozen(_frozen);
    }

    private void ExportCsv_Click(object sender, RoutedEventArgs e)
    {
        if (!_dataStorage.IsRecording && _playbackService == null && !_projectService.IsProjectOpen)
        {
            MessageBox.Show("没有可导出的数据。请先采集数据或打开数据文件。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "CSV 文件|*.csv|所有文件|*.*",
            Title = "导出 CSV 数据",
            FileName = $"数据_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
        };

        if (dlg.ShowDialog() != true) return;

        try
        {
            string dataFile;

            if (_playbackService != null && _playbackService.IsOpen)
            {
                dataFile = _playbackService.Info!.FilePath;
            }
            else if (_projectService.IsProjectOpen && _projectService.CurrentProject!.Events.Count > 0)
            {
                dataFile = _projectService.CurrentProject.Events[^1].DataFile;
            }
            else
            {
                MessageBox.Show("找不到数据文件。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var success = CsvExportService.ExportToCsv(dataFile, dlg.FileName);
            if (success)
            {
                StatusText.Text = $"已导出: {dlg.FileName}";
                _log.Info($"CSV 导出成功: {dlg.FileName}");
                MessageBox.Show($"数据已成功导出到:\n{dlg.FileName}", "导出成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("导出失败，请检查数据文件。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            _log.Error($"CSV 导出异常: {ex.Message}");
            MessageBox.Show($"导出异常: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnStatsTimerTick(object? sender, EventArgs e)
    {
        if (_fftBufferPos == 0 || _frozen) return;

        var channelCount = _channelManager.Channels.Count;
        if (channelCount <= 0) return;

        var samplesPerChannel = _fftBufferPos / channelCount;
        if (samplesPerChannel == 0) return;

        var ch0Data = new float[samplesPerChannel];
        for (int i = 0; i < samplesPerChannel; i++)
            ch0Data[i] = _fftBuffer[i * channelCount];

        var stats = StatisticsCalculator.Compute(ch0Data);
        var chName = _channelManager.Channels[0].Name;

        StatusText.Text = $"[{chName}] RMS:{stats.Rms:F4}  峰值:{stats.Peak:F4}  峭度:{stats.Kurtosis:F3}  采样:{samplesPerChannel}点";
    }

    private void FilterConfig_Click(object sender, RoutedEventArgs e)
    {
        var win = new FilterConfigWindow(_channelManager, _channelManager.SampleRate)
        {
            Owner = this
        };

        if (win.ShowDialog() == true && win.FilterEnabled)
        {
            _channelFilters ??= new DigitalFilter[_channelManager.Channels.Count];

            try
            {
                var filter = new DigitalFilter(
                    win.FilterType,
                    _channelManager.SampleRate,
                    win.CutoffFreq1,
                    win.Order,
                    win.CutoffFreq2,
                    win.FilterDesign);

                _channelFilters[win.SelectedChannelIndex] = filter;

                var chName = _channelManager.Channels[win.SelectedChannelIndex].Name;
                StatusText.Text = $"已应用滤波器到 {chName}: {win.FilterDesign} {win.FilterType} {win.Order}阶";
                _log.Info($"滤波器配置 - 通道:{chName} 类型:{win.FilterType} 设计:{win.FilterDesign} 阶数:{win.Order} 截止1:{win.CutoffFreq1}Hz 截止2:{win.CutoffFreq2}Hz");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"滤波器配置失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                _log.Error($"滤波器配置失败: {ex.Message}");
            }
        }
    }

    private void TriggerConfig_Click(object sender, RoutedEventArgs e)
    {
        var win = new TriggerConfigWindow(_channelManager, _channelManager.SampleRate)
        {
            Owner = this
        };

        if (win.ShowDialog() == true)
        {
            _triggerEnabled = win.TriggerEnabled;

            if (_triggerEnabled)
            {
                _triggerService?.Dispose();
                _triggerService = new TriggerService(
                    _channelManager.Channels.Count,
                    _channelManager.SampleRate);
                _triggerService.TriggerChannelIndex = win.TriggerChannelIndex;
                _triggerService.TriggerLevel = win.TriggerLevel;
                _triggerService.Slope = win.Slope;
                _triggerService.Mode = win.Mode;
                _triggerService.PreTriggerSeconds = win.PreTriggerSeconds;
                _triggerService.PostTriggerSeconds = win.PostTriggerSeconds;
                _triggerService.Triggered += OnTriggered;

                var chName = _channelManager.Channels[win.TriggerChannelIndex].Name;
                StatusText.Text = $"触发已启用: {chName} @ {win.TriggerLevel} ({win.Slope})";
                _log.Info($"触发配置 - 通道:{chName} 电平:{win.TriggerLevel} 斜率:{win.Slope} 模式:{win.Mode} 预触发:{win.PreTriggerSeconds}s 后触发:{win.PostTriggerSeconds}s");

                if (_acqEngine.State == AcquisitionState.Acquiring)
                    _triggerService.Start();
            }
            else
            {
                _triggerService?.Stop();
                _triggerService?.Dispose();
                _triggerService = null;
                StatusText.Text = "触发已禁用";
                _log.Info("触发已禁用");
            }
        }
    }

    private void OnTriggered(object? sender, TriggeredEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            var triggerTime = e.TriggerTime.ToString("HH:mm:ss.fff");
            StatusText.Text = $"触发! {triggerTime} - 共 {e.Data.Length / e.ChannelCount:N0} 样本";
            _log.Info($"触发事件 - 时间:{triggerTime} 通道:{e.TriggerChannelIndex} 电平:{e.TriggerLevel} 样本数:{e.Data.Length / e.ChannelCount:N0}");

            // 创建触发波形显示页
            var tab = new TabItem { Header = $"触发 {triggerTime}" };
            var panel = new StackPanel { Margin = new Thickness(10) };

            var infoText = new TextBlock
            {
                Text = $"触发时间: {triggerTime} | 触发通道: 通道{e.TriggerChannelIndex + 1} | " +
                       $"触发电平: {e.TriggerLevel} | 斜率: {e.Slope} | " +
                       $"总时长: {e.PreTriggerSamples + e.PostTriggerSamples} 样本 ({(e.PreTriggerSamples + e.PostTriggerSamples) / e.SampleRate:F2}s)",
                Foreground = System.Windows.Media.Brushes.LightGray,
                Margin = new Thickness(0, 0, 0, 8)
            };

            var recorder = new RecorderChart { Title = "触发波形", Height = 350 };
            var colors = new[]
            {
                System.Windows.Media.Color.FromRgb(0x4C, 0xAF, 0x50),
                System.Windows.Media.Color.FromRgb(0x21, 0x96, 0xF3),
                System.Windows.Media.Color.FromRgb(0xFF, 0x98, 0x00),
                System.Windows.Media.Color.FromRgb(0xE9, 0x1E, 0x63),
            };
            for (int i = 0; i < e.ChannelCount && i < 4; i++)
                recorder.AddChannel(i, $"通道{i + 1}", colors[i % colors.Length]);

            recorder.UpdateData(e.Data, e.ChannelCount);

            panel.Children.Add(infoText);
            panel.Children.Add(recorder);
            tab.Content = panel;
            ChartTabControl.Items.Add(tab);
            ChartTabControl.SelectedItem = tab;
        });
    }

    private void ProjectTree_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (ProjectTree.SelectedItem is TreeViewItem item && item.Tag is DH.Core.Models.ExperimentEvent evt)
        {
            LoadEventData(evt);
        }
    }

    private void LoadEventData(DH.Core.Models.ExperimentEvent evt)
    {
        if (string.IsNullOrEmpty(evt.DataFile) || !File.Exists(evt.DataFile))
        {
            MessageBox.Show($"数据文件不存在:\n{evt.DataFile}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        _playbackService?.Dispose();
        _playbackService = new DataPlaybackService();

        if (!_playbackService.Open(evt.DataFile))
        {
            MessageBox.Show("无法打开数据文件", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var info = _playbackService.Info!;
        StatusText.Text = $"已加载事件: {evt.Name} ({info.ChannelCount}ch, {info.SampleRate:F0}Hz, {info.DurationSeconds:F1}s)";
        _log.Info($"加载事件数据: {evt.Name} 文件:{evt.DataFile}");

        var tab = new TabItem { Header = evt.Name };
        var recorder = new RecorderChart { Title = evt.Name, Height = 400 };
        var controlPanel = BuildPlaybackPanel(_playbackService, recorder);

        var panel = new DockPanel { Margin = new Thickness(10) };
        panel.Children.Add(controlPanel);
        DockPanel.SetDock(controlPanel, Dock.Top);
        panel.Children.Add(recorder);

        tab.Content = panel;
        ChartTabControl.Items.Add(tab);
        ChartTabControl.SelectedItem = tab;
    }

    private void PopulateProjectTree()
    {
        TreeDevices.Items.Clear();
        foreach (var dev in _hardwareManager.AvailableDevices)
        {
            var devItem = new TreeViewItem
            {
                Header = $"{dev.ModelName} [{dev.SerialNumber}]",
                Tag = dev
            };
            TreeDevices.Items.Add(devItem);
        }

        TreeChannels.Items.Clear();
        foreach (var ch in _channelManager.Channels)
        {
            TreeChannels.Items.Add(new TreeViewItem
            {
                Header = ch.Enabled ? $"通道{ch.Index}: {ch.Name} ({ch.Unit})" : $"通道{ch.Index}: {ch.Name} [禁用]",
                Tag = ch
            });
        }

        TreeEvents.Items.Clear();
        if (_projectService.IsProjectOpen && _projectService.CurrentProject!.Events.Count > 0)
        {
            foreach (var evt in _projectService.CurrentProject.Events)
            {
                TreeEvents.Items.Add(new TreeViewItem
                {
                    Header = $"{evt.Name} ({evt.StartTime})",
                    Tag = evt
                });
            }
        }
        else
        {
            TreeEvents.Items.Add(new TreeViewItem { Header = "无事件数据" });
        }
    }
}
