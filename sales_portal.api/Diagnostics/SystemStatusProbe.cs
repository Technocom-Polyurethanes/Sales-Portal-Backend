using sales_portal.core.DTOs;

namespace sales_portal.api.Diagnostics;

/// <summary>Runs one status probe at a time; a failed probe only marks itself as failed.</summary>
public static class SystemStatusProbe
{
    public static async Task<SystemProbeDto> RunAsync(
        string name,
        Func<Task<int>> probe,
        Action<Exception> onError,
        CancellationToken cancellationToken)
    {
        try
        {
            return new SystemProbeDto(name, true, await probe(), null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            if (cancellationToken.IsCancellationRequested) throw;
            onError(exception);
            // Only the type name reaches the response; exception text can contain connection details.
            return new SystemProbeDto(name, false, 0, exception.GetType().Name);
        }
    }

    public static async Task<T?> ValueAsync<T>(
        Func<Task<T?>> probe,
        Action<Exception> onError,
        CancellationToken cancellationToken)
    {
        try
        {
            return await probe();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            if (cancellationToken.IsCancellationRequested) throw;
            onError(exception);
            return default;
        }
    }
}
