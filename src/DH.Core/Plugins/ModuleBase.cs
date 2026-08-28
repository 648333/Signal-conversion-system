using System.ComponentModel.Composition;

namespace DH.Core.Plugins;

/// <summary>
/// 模块基类：实现IModule的公共逻辑，供具体模块继承
/// </summary>
[InheritedExport(typeof(IModule))]
public abstract class ModuleBase : IModule
{
    public abstract string ModuleName { get; }
    public abstract string DisplayName { get; }
    public abstract ModuleCategory Category { get; }
    public virtual string Version => "1.0.0";

    public ModuleState State { get; protected set; } = ModuleState.NotLoaded;

    protected IServiceProvider? Services { get; private set; }

    public virtual void Initialize(IServiceProvider services)
    {
        Services = services;
        State = ModuleState.Initialized;
    }

    public virtual void Activate()
    {
        State = ModuleState.Activated;
    }

    public virtual void Deactivate()
    {
        State = ModuleState.Deactivated;
    }

    public virtual void Unload()
    {
        State = ModuleState.Unloaded;
    }
}
