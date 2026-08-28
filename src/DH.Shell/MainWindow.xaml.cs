using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Ribbon;
using System.Windows.Threading;
using DH.Core.Logging;
using DH.Core.Models;
using DH.Core.Services;
using DH.Core.Events;
using DH.Shell.ViewModels;

namespace DH.Shell;

public partial class MainWindow : Window
{
    private readonly AppServices _services;
    private readonly ILogService _log;
    private readonly EventBus _eventBus;
    private readonly DispatcherTimer _clockTimer;
    private readonly MainViewModel _viewModel;

    public MainWindow(AppServices services)
    {
        InitializeComponent();
        _services = services;
        _log = services.GetService<ILogService>();
        _eventBus = services.GetService<EventBus>();
        _viewModel = new MainViewModel(services);
        DataContext = _viewModel;

        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clockTimer.Tick += (_, _) => ClockText.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        _eventBus.Subscribe<DeviceConnectedEvent>(OnDeviceConnected);
        _eventBus.Subscribe<DeviceDisconnectedEvent>(OnDeviceDisconnected);
        _eventBus.Subscribe<AcquisitionStartedEvent>(OnAcquisitionStarted);
        _eventBus.Subscribe<AcquisitionStoppedEvent>(OnAcquisitionStopped);
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        _clockTimer.Start();
        _log.Info("主窗口加载完成");
        StatusText.Text = "就绪";
        UpdateAcqStatus(AcquisitionState.Idle);
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        _clockTimer.Stop();
        _log.Info("主窗口关闭");
    }

    private void NewProject_Click(object sender, RoutedEventArgs e)
    {
        var project = new ProjectInfo { Name = $"工程_{DateTime.Now:yyyyMMdd_HHmmss}" };
        _services.GetService<AppState>().CurrentProject = project;
        TreeRoot.Header = project.Name;
        _log.Info($"新建工程: {project.Name}");
        StatusText.Text = $"工程: {project.Name}";
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
            StatusText.Text = $"已加载: {dlg.FileName}";
            _log.Info($"打开工程: {dlg.FileName}");
        }
    }

    private void SaveProject_Click(object sender, RoutedEventArgs e)
    {
        _log.Info("保存工程");
        StatusText.Text = "工程已保存";
    }

    private void ScanDevices_Click(object sender, RoutedEventArgs e)
    {
        _log.Info("扫描设备...");
        StatusText.Text = "正在扫描设备...";
        TreeDevices.Items.Clear();
        TreeDevices.Items.Add(new TreeViewItem { Header = "(无设备)" });
        StatusText.Text = "扫描完成: 未发现设备";
    }

    private void ConnectDevice_Click(object sender, RoutedEventArgs e)
    {
        _log.Info("连接设备");
        StatusText.Text = "正在连接设备...";
        DeviceStatus.Text = "设备: DH5922 [已连接]";
    }

    private void DisconnectDevice_Click(object sender, RoutedEventArgs e)
    {
        _log.Info("断开设备");
        DeviceStatus.Text = "设备: 未连接";
    }

    private void ChannelSetup_Click(object sender, RoutedEventArgs e)
    {
        _log.Info("打开通道配置");
        StatusText.Text = "通道配置";
    }

    private void StartAcquisition_Click(object sender, RoutedEventArgs e)
    {
        _log.Info("开始采集");
        _eventBus.Publish(new AcquisitionStartedEvent { EventName = $"事件_{DateTime.Now:HHmmss}" });
    }

    private void PauseAcquisition_Click(object sender, RoutedEventArgs e)
    {
        _log.Info("暂停采集");
        UpdateAcqStatus(AcquisitionState.Paused);
    }

    private void StopAcquisition_Click(object sender, RoutedEventArgs e)
    {
        _log.Info("停止采集");
        _eventBus.Publish(new AcquisitionStoppedEvent());
    }

    private void AddRecorder_Click(object sender, RoutedEventArgs e)
    {
        var tab = new TabItem { Header = $"图表 {ChartTabControl.Items.Count + 1}" };
        var grid = new Grid { Background = (System.Windows.Media.Brush)FindResource("ChartBackground") };
        var text = new TextBlock
        {
            Text = "记录仪波形显示区",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 18,
            Foreground = System.Windows.Media.Brushes.Gray
        };
        grid.Children.Add(text);
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
}
