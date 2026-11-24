using System.Collections.Concurrent;

namespace sales_portal.core.Live;

/// <summary>
/// In-process fan-out. Each subscriber keeps only the set of topics that changed since it last
/// looked, so memory per connection is bounded by the number of topics no matter how slow the
/// connection is, and a publisher never waits for a subscriber.
///
/// This reaches the browsers connected to this server process. The application runs as one
/// process; running several would need a shared channel (for example Postgres LISTEN/NOTIFY)
/// behind <see cref="IChangeBroadcaster"/>.
/// </summary>
public sealed class ChangeBroadcaster : IChangeBroadcaster
{
    private readonly ConcurrentDictionary<Subscription, byte> _subscriptions = new();

    public int SubscriberCount => _subscriptions.Count;

    public void Publish(IReadOnlyCollection<string> topics)
    {
        if (topics.Count == 0)
        {
            return;
        }

        foreach (var subscription in _subscriptions.Keys)
        {
            subscription.Add(topics);
        }
    }

    public IChangeSubscription Subscribe()
    {
        var subscription = new Subscription(this);
        _subscriptions[subscription] = 0;
        return subscription;
    }

    private sealed class Subscription : IChangeSubscription
    {
        private readonly ChangeBroadcaster _owner;
        private readonly object _gate = new();
        private readonly HashSet<string> _pending = new(StringComparer.Ordinal);
        private TaskCompletionSource? _signal;

        public Subscription(ChangeBroadcaster owner)
        {
            _owner = owner;
        }

        public void Add(IReadOnlyCollection<string> topics)
        {
            lock (_gate)
            {
                foreach (var topic in topics)
                {
                    _pending.Add(topic);
                }

                _signal?.TrySetResult();
            }
        }

        public async Task<IReadOnlyCollection<string>> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            Task wait;
            lock (_gate)
            {
                if (_pending.Count > 0)
                {
                    return Drain();
                }

                _signal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                wait = _signal.Task;
            }

            try
            {
                await wait.WaitAsync(timeout, cancellationToken).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                // Nothing within the timeout: fall through and report an empty batch.
            }
            finally
            {
                lock (_gate)
                {
                    _signal = null;
                }
            }

            lock (_gate)
            {
                // An announcement can land between the timeout and here; do not lose it.
                return Drain();
            }
        }

        // Callers hold _gate.
        private IReadOnlyCollection<string> Drain()
        {
            if (_pending.Count == 0)
            {
                return Array.Empty<string>();
            }

            var topics = _pending.ToArray();
            _pending.Clear();
            return topics;
        }

        public void Dispose() => _owner._subscriptions.TryRemove(this, out _);
    }
}
