namespace sales_portal.api.Middleware;

/// <summary>Fixed-size ring of recent database latency samples for the status screen.</summary>
public sealed class LatencyHistory
{
    private const int Capacity = 20;

    private readonly long[] _samples = new long[Capacity];
    private readonly object _sync = new();
    private int _next;
    private int _count;

    public void Add(long milliseconds)
    {
        lock (_sync)
        {
            _samples[_next] = milliseconds;
            _next = (_next + 1) % Capacity;
            if (_count < Capacity) _count++;
        }
    }

    public long[] Snapshot()
    {
        lock (_sync)
        {
            var snapshot = new long[_count];
            var oldest = (_next - _count + Capacity) % Capacity;
            for (var index = 0; index < _count; index++)
            {
                snapshot[index] = _samples[(oldest + index) % Capacity];
            }

            return snapshot;
        }
    }
}
