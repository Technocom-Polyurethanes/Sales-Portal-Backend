using System.Collections.Concurrent;

namespace sales_portal.api.Live;

/// <summary>Tracks the dedicated Postgres listeners so /health and /api/system/status can report them.</summary>
public interface ILiveListenerStatus
{
    string State { get; }

    bool Disabled { get; }

    bool IsConnected(string listener);

    DateTimeOffset? ChangesLastEventAt { get; }

    DateTimeOffset? NotificationsLastEventAt { get; }

    int ReconnectCount { get; }

    string? LastError { get; }

    void Disable();

    void Update(string listener, bool connected);

    void RecordEvent(string listener);

    void RecordReconnect();

    void RecordError(string error);
}

public sealed class LiveListenerStatus : ILiveListenerStatus
{
    private readonly ConcurrentDictionary<string, bool> _listeners = new(StringComparer.Ordinal);
    private volatile bool _disabled;
    private int _reconnectCount;
    private string? _lastError;
    private DateTimeOffset? _changesLastEventAt;
    private DateTimeOffset? _notificationsLastEventAt;

    public bool Disabled => _disabled;

    public string State => _disabled
        ? "disabled"
        : _listeners.IsEmpty || _listeners.Values.Any(connected => !connected)
            ? "disconnected"
            : "connected";

    public DateTimeOffset? ChangesLastEventAt => _changesLastEventAt;
    public DateTimeOffset? NotificationsLastEventAt => _notificationsLastEventAt;
    public int ReconnectCount => Volatile.Read(ref _reconnectCount);
    public string? LastError => _lastError;

    public bool IsConnected(string listener) =>
        _listeners.TryGetValue(listener, out var connected) && connected;

    public void Disable() => _disabled = true;

    public void Update(string listener, bool connected) => _listeners[listener] = connected;

    public void RecordEvent(string listener)
    {
        var at = DateTimeOffset.UtcNow;
        if (listener == "changes") _changesLastEventAt = at;
        if (listener == "notifications") _notificationsLastEventAt = at;
    }

    public void RecordReconnect() => Interlocked.Increment(ref _reconnectCount);

    public void RecordError(string error) => _lastError = error;
}
