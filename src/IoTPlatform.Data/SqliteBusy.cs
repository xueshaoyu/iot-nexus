using NewLife.Log;

namespace IoTPlatform.Data;

/// <summary>Retry helper for transient SQLite lock / busy errors.</summary>
public static class SqliteBusy
{
    public static Boolean IsLocked(Exception? exception)
    {
        for (var ex = exception; ex != null; ex = ex.InnerException)
        {
            var message = ex.Message;
            if (message.Contains("database is locked", StringComparison.OrdinalIgnoreCase)) return true;
            if (message.Contains("SQLite Error 5", StringComparison.OrdinalIgnoreCase)) return true;
            if (message.Contains("SQLITE_BUSY", StringComparison.OrdinalIgnoreCase)) return true;
        }

        return false;
    }

    public static T Retry<T>(Func<T> action, Int32 attempts = 6, Int32 baseDelayMs = 25)
    {
        Exception? last = null;
        for (var i = 0; i < Math.Max(1, attempts); i++)
        {
            try
            {
                return action();
            }
            catch (Exception ex) when (IsLocked(ex) && i < attempts - 1)
            {
                last = ex;
                var delay = Math.Min(1_000, baseDelayMs * (1 << i));
                Thread.Sleep(delay);
            }
        }

        throw last ?? new InvalidOperationException("SQLite retry exhausted");
    }

    public static void Retry(Action action, Int32 attempts = 6, Int32 baseDelayMs = 25) =>
        Retry(() => { action(); return true; }, attempts, baseDelayMs);

    /// <summary>Retry, then run <paramref name="fallback"/> instead of failing the caller.</summary>
    public static T RetryOrFallback<T>(Func<T> action, Func<T> fallback, Int32 attempts = 6)
    {
        try
        {
            return Retry(action, attempts);
        }
        catch (Exception ex) when (IsLocked(ex))
        {
            XTrace.WriteLine("SQLite still locked after {0} attempts, using fallback: {1}", attempts, ex.Message);
            return fallback();
        }
    }
}
