using System.Collections.Concurrent;

namespace DH.Core.Events;

/// <summary>
/// 事件总线：模块间松耦合通信，基于发布/订阅模式
/// </summary>
public sealed class EventBus
{
    private readonly ConcurrentDictionary<Type, List<Action<object>>> _subscribers = new();
    private readonly object _lock = new();

    public void Subscribe<T>(Action<T> handler) where T : IEvent
    {
        var type = typeof(T);
        lock (_lock)
        {
            if (!_subscribers.ContainsKey(type))
                _subscribers[type] = new List<Action<object>>();
            _subscribers[type].Add(obj => handler((T)obj));
        }
    }

    public void Unsubscribe<T>(Action<T> handler) where T : IEvent
    {
        var type = typeof(T);
        lock (_lock)
        {
            if (_subscribers.TryGetValue(type, out var list))
            {
                list.RemoveAll(h =>
                {
                    try { h.DynamicInvoke(default(T)); return false; }
                    catch { return false; }
                });
            }
        }
    }

    public void Publish<T>(T eventData) where T : IEvent
    {
        var type = typeof(T);
        if (_subscribers.TryGetValue(type, out var list))
        {
            foreach (var handler in list.ToList())
            {
                try
                {
                    handler(eventData!);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"EventBus handler error: {ex.Message}");
                }
            }
        }
    }
}

public interface IEvent { }
