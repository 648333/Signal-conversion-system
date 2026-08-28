using Microsoft.Extensions.DependencyInjection;
using DH.Core.Configuration;
using DH.Core.Events;
using DH.Core.Logging;

namespace DH.Core.Services;

/// <summary>
/// 全局应用服务容器，封装所有核心服务的注册和解析
/// </summary>
public sealed class AppServices : IServiceProvider
{
    private readonly IServiceProvider _provider;

    private AppServices(IServiceProvider provider)
    {
        _provider = provider;
    }

    public static AppServices Build(string configRoot, string logDir)
    {
        var services = new ServiceCollection();

        var configService = new ConfigService(configRoot);
        var logService = new LogService(logDir);
        var eventBus = new EventBus();
        var appState = new AppState();

        services.AddSingleton(configService);
        services.AddSingleton<ILogService>(logService);
        services.AddSingleton(eventBus);
        services.AddSingleton(appState);
        services.AddSingleton<AppServices>();

        var provider = services.BuildServiceProvider();
        var instance = new AppServices(provider);
        services.AddSingleton(instance);
        return instance;
    }

    public object? GetService(Type serviceType) => _provider.GetService(serviceType);

    public T GetService<T>() where T : notnull => _provider.GetRequiredService<T>();
    public T? GetOptional<T>() where T : class => _provider.GetService<T>();
}
