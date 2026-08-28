namespace DH.Core.Plugins;

public enum ModuleCategory
{
    Shell,
    Hardware,
    Channel,
    Acquisition,
    Visualization,
    SignalProcessing,
    Analysis,
    Modal,
    Acoustics,
    Reporting,
    ProjectManagement
}

public enum ModuleState
{
    NotLoaded,
    Loading,
    Loaded,
    Initialized,
    Activated,
    Deactivated,
    Unloaded
}

/// <summary>
/// 插件模块接口：所有功能模块通过此接口被主框架加载和管理
/// </summary>
public interface IModule
{
    string ModuleName { get; }
    string DisplayName { get; }
    ModuleCategory Category { get; }
    string Version { get; }
    ModuleState State { get; }

    void Initialize(IServiceProvider services);
    void Activate();
    void Deactivate();
    void Unload();
}
