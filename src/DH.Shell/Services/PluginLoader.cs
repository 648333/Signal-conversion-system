using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.IO;
using DH.Core.Logging;
using DH.Core.Plugins;
using DH.Core.Services;

namespace DH.Shell.Services;

public sealed class PluginLoader
{
    private readonly AppServices _services;
    private readonly ILogService _log;
    private CompositionContainer? _container;
    private readonly List<IModule> _modules = new();

    public IReadOnlyList<IModule> Modules => _modules;

    [ImportMany(typeof(IModule))]
    public IEnumerable<IModule> ImportedModules { get; set; } = Enumerable.Empty<IModule>();

    public PluginLoader(AppServices services)
    {
        _services = services;
        _log = services.GetService<ILogService>();
    }

    public void LoadPlugins(string pluginDirectory)
    {
        try
        {
            if (!Directory.Exists(pluginDirectory))
            {
                Directory.CreateDirectory(pluginDirectory);
                _log.Info($"插件目录已创建: {pluginDirectory}");
            }

            var catalog = new AggregateCatalog();
            catalog.Catalogs.Add(new DirectoryCatalog(pluginDirectory, "*.dll"));
            catalog.Catalogs.Add(new AssemblyCatalog(typeof(PluginLoader).Assembly));

            _container = new CompositionContainer(catalog);
            _container.ComposeParts(this);

            foreach (var module in ImportedModules)
            {
                try
                {
                    module.Initialize(_services);
                    _modules.Add(module);
                    _log.Info($"模块已加载: {module.ModuleName} v{module.Version} [{module.Category}]");
                }
                catch (Exception ex)
                {
                    _log.Error($"模块加载失败: {module.ModuleName}", ex);
                }
            }

            _log.Info($"插件加载完成: {_modules.Count} 个模块");
        }
        catch (Exception ex)
        {
            _log.Error("插件加载过程出错", ex);
        }
    }

    public void ActivateAll()
    {
        foreach (var module in _modules)
        {
            try
            {
                module.Activate();
            }
            catch (Exception ex)
            {
                _log.Error($"模块激活失败: {module.ModuleName}", ex);
            }
        }
    }

    public void DeactivateAll()
    {
        foreach (var module in _modules)
        {
            try
            {
                module.Deactivate();
            }
            catch (Exception ex)
            {
                _log.Error($"模块停用失败: {module.ModuleName}", ex);
            }
        }
    }

    public T? GetModule<T>() where T : class, IModule
    {
        return _modules.OfType<T>().FirstOrDefault();
    }

    public IEnumerable<IModule> GetModulesByCategory(ModuleCategory category)
    {
        return _modules.Where(m => m.Category == category);
    }

    public void Dispose()
    {
        foreach (var module in _modules)
        {
            try { module.Unload(); }
            catch (Exception ex) { _log.Error($"模块卸载失败: {module.ModuleName}", ex); }
        }
        _modules.Clear();
        _container?.Dispose();
    }
}
