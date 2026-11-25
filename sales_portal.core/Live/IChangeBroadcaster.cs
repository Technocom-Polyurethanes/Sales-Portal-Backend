namespace sales_portal.core.Live;

/// <summary>
/// Tells every connected browser that some data changed, so it can load it again instead of
/// waiting for a manual refresh. Announcing is fire-and-forget and never fails a write.
/// </summary>
public interface IChangeBroadcaster
{
    /// <summary>Announces the topics to every current subscriber. Safe to call from any thread.</summary>
    void Publish(IReadOnlyCollection<string> topics);

    /// <summary>Starts listening. Dispose the subscription when the connection ends.</summary>
    IChangeSubscription Subscribe();
}

/// <summary>One connected browser.</summary>
public interface IChangeSubscription : IDisposable
{
    /// <summary>
    /// Waits for the next announcement. Announcements that arrive while the caller is busy are merged,
    /// so a slow connection receives one batch rather than a backlog. Returns an empty collection when
    /// nothing happened within <paramref name="timeout"/> (the caller's cue to send a keep-alive).
    /// Throws <see cref="OperationCanceledException"/> when <paramref name="cancellationToken"/> is cancelled.
    /// </summary>
    Task<IReadOnlyCollection<string>> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken);
}

/// <summary>Announces nothing; used when no broadcaster is registered (for example in unit tests).</summary>
public sealed class NoChangeBroadcaster : IChangeBroadcaster
{
    public static readonly NoChangeBroadcaster Instance = new();

    public void Publish(IReadOnlyCollection<string> topics)
    {
    }

    public IChangeSubscription Subscribe() => new IdleSubscription();

    private sealed class IdleSubscription : IChangeSubscription
    {
        public async Task<IReadOnlyCollection<string>> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            await Task.Delay(timeout, cancellationToken).ConfigureAwait(false);
            return Array.Empty<string>();
        }

        public void Dispose()
        {
        }
    }
}
