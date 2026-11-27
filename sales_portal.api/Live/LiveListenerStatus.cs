using System.Collections.Concurrent;

namespace sales_portal.api.Live;

/// <summary>Tracks the dedicated Postgres listeners so /health can report whether live updates work.</summary>
public interface ILiveListenerStatus
{
    string State { get; }

    bool IsConnected(string listener);

    void Disable();

    void Update(string listener, bool connected);
}

public sealed class LiveListenerStatus : ILiveListenerStatus
{
    private readonly ConcurrentDictionary<string, bool> _listeners = new(StringComparer.Ordinal);
    private volatile bool _disabled;

    public string State => _disabled
        ? "disabled"
        : _listeners.IsEmpty || _listeners.Values.Any(connected => !connected)
            ? "disconnected"
            : "connected";

    public bool IsConnected(string listener) =>
        _listeners.TryGetValue(listener, out var connected) && connected;

    public void Disable() => _disabled = true;

    public void Update(string listener, bool connected) => _listeners[listener] = connected;
}
