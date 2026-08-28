using System.IO;
using System.Windows;
using DH.Core.Events;
using DH.Core.Logging;
using DH.Core.Services;
using DH.Shell.Services;

namespace DH.Shell;

public partial class App : Application
{
    private AppServices? _appServices;
    private PluginLoader? _pluginLoader;

    public static new App Current => (App)Application.Current;

    public AppServices Services => _appServices ?? throw new InvalidOperationException("Services not initialized");
    public PluginLoader Plugins => _pluginLoader ?? throw new InvalidOperationException("Plugin loader not initialized");

    private void Application_Startup(object sender, StartupEventArgs e)
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var configRoot = Path.Combine(baseDir, "config");
        var logDir = Path.Combine(baseDir, "Logs");

        _appServices = AppServices.Build(configRoot, logDir);
        var log = _appServices.GetService<ILogService>();
        log.Info("=== DH-RTDAS 启动 ===");

        _pluginLoader = new PluginLoader(_appServices);
        _pluginLoader.LoadPlugins(Path.Combine(baseDir, "Plugins"));

        var mainWindow = new MainWindow(_appServices);
        mainWindow.Show();

        _appServices.GetService<EventBus>().Publish(new ModuleLoadedEvent { ModuleName = "Shell" });
    }

    private void Application_Exit(object sender, ExitEventArgs e)
    {
        var log = _appServices?.GetService<ILogService>();
        log?.Info("=== DH-RTDAS 关闭 ===");
        (_appServices?.GetService<LogService>() as LogService)?.Shutdown();
    }
}
