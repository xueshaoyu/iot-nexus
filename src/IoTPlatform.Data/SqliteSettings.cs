using System.Data;
using Microsoft.Data.Sqlite;
using NewLife;
using NewLife.Log;
using XCode;
using XCode.DataAccessLayer;

namespace IoTPlatform.Data;

/// <summary>
/// SQLite WAL / busy-timeout / delayed-write settings for NewLife.XCode.
/// </summary>
/// <remarks>
/// XCode only injects <c>Journal Mode=WAL</c> and <c>Synchronous=Off</c> when the
/// ADO.NET factory is System.Data.SQLite. This host uses Microsoft.Data.Sqlite,
/// which rejects those keywords, so a bare <c>Data Source=…</c> (the Docker default)
/// stays on DELETE journal + <c>busy_timeout=0</c>. Under ingest load that surfaces
/// as 1–30s 慢SQL and <c>SQLite Error 5: database is locked</c>.
/// </remarks>
public static class SqliteSettings
{
    public const Int32 BusyTimeoutSeconds = 30;
    public const Int32 BusyTimeoutMs = BusyTimeoutSeconds * 1000;
    public const Int32 SaveAsyncDelayMs = 3_000;
    public const Int32 DeviceCounterDelayMs = 10_000;

    public const String CacheShared = "Shared";
    public const String JournalWal = "WAL";
    public const String SynchronousNormal = "NORMAL";

    /// <summary>Microsoft.Data.Sqlite-safe extras: shared cache + busy timeout.</summary>
    public static String EnhanceConnectionString(String connectionString)
    {
        if (connectionString.IsNullOrWhiteSpace()) return connectionString;

        var builder = new SqliteConnectionStringBuilder(StripUnknown(connectionString));
        if (builder.Cache != SqliteCacheMode.Shared) builder.Cache = SqliteCacheMode.Shared;
        if (builder.DefaultTimeout < BusyTimeoutSeconds) builder.DefaultTimeout = BusyTimeoutSeconds;
        return builder.ToString();
    }

    public static Boolean IsSqlite(String? provider, String? connectionString = null)
    {
        if (!provider.IsNullOrWhiteSpace() && provider.Contains("sqlite", StringComparison.OrdinalIgnoreCase))
            return true;
        if (!connectionString.IsNullOrWhiteSpace() && connectionString.Contains("sqlite", StringComparison.OrdinalIgnoreCase))
            return true;
        return provider.IsNullOrWhiteSpace();
    }

    /// <summary>Apply WAL / NORMAL / busy_timeout through XCode and warm pooled connections.</summary>
    public static void Apply(String connName, String? provider = "SQLite")
    {
        if (!IsSqlite(provider)) return;

        try
        {
            var setting = XCodeSetting.Current;
            if (setting.EntityCacheExpire < 60) setting.EntityCacheExpire = 300;
            if (setting.SingleCacheExpire < 60) setting.SingleCacheExpire = 300;
        }
        catch { /* older XCode builds omit these properties */ }

        ConfigureQueues();

        try
        {
            var dal = DAL.Create(connName);
            ExecutePragmas(dal);
            WarmPooledConnections(dal.ConnStr);
            XTrace.WriteLine("SQLite pragmas applied on {0}: journal_mode=WAL synchronous=NORMAL busy_timeout={1}ms", connName, BusyTimeoutMs);
        }
        catch (Exception ex)
        {
            XTrace.WriteLine("SQLite pragma apply failed: {0}", ex.Message);
        }
    }

    public static void ExecutePragmas(DAL dal)
    {
        foreach (var sql in PragmaStatements)
        {
            try { dal.Execute(sql); }
            catch (Exception ex) { XTrace.WriteLine("PRAGMA failed [{0}]: {1}", sql, ex.Message); }
        }
    }

    public static readonly String[] PragmaStatements =
    [
        "PRAGMA journal_mode=WAL;",
        "PRAGMA synchronous=NORMAL;",
        $"PRAGMA busy_timeout={BusyTimeoutMs};",
        "PRAGMA temp_store=MEMORY;",
        "PRAGMA cache_size=-65536;",
        "PRAGMA wal_autocheckpoint=1000;",
    ];

    static void ConfigureQueues()
    {
        SetQueuePeriod(Device.Meta.Factory, DeviceCounterDelayMs);
        SetQueuePeriod(DeviceProperty.Meta.Factory, SaveAsyncDelayMs);
        SetQueuePeriod(DeviceOnline.Meta.Factory, DeviceCounterDelayMs);
        SetQueuePeriod(DeviceGroup.Meta.Factory, DeviceCounterDelayMs);
        SetQueuePeriod(DeviceHistory.Meta.Factory, SaveAsyncDelayMs);
        SetQueuePeriod(DeviceData.Meta.Factory, SaveAsyncDelayMs);
        SetQueuePeriod(DeviceEvent.Meta.Factory, SaveAsyncDelayMs);
        SetQueuePeriod(Alarm.Meta.Factory, SaveAsyncDelayMs);
        SetQueuePeriod(RuleLog.Meta.Factory, SaveAsyncDelayMs);
        SetQueuePeriod(AccessLog.Meta.Factory, SaveAsyncDelayMs);
        SetQueuePeriod(Rule.Meta.Factory, DeviceCounterDelayMs);
    }

    static void SetQueuePeriod(Object factory, Int32 period)
    {
        try
        {
            var queue = factory.GetType().GetProperty("Queue")?.GetValue(factory);
            if (queue == null) return;
            var prop = queue.GetType().GetProperty("Period");
            if (prop?.CanWrite == true) prop.SetValue(queue, period);
        }
        catch { }
    }

    /// <summary>
    /// Microsoft.Data.Sqlite keeps PRAGMA state on pooled physical connections.
    /// Opening a few connections after Init seeds the pool with WAL/NORMAL/busy_timeout.
    /// </summary>
    static void WarmPooledConnections(String? connectionString)
    {
        if (connectionString.IsNullOrWhiteSpace()) return;

        String mds;
        try { mds = EnhanceConnectionString(connectionString); }
        catch { return; }

        var opened = new List<SqliteConnection>();
        try
        {
            for (var i = 0; i < 4; i++)
            {
                var conn = new SqliteConnection(mds);
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = String.Join(" ", PragmaStatements);
                cmd.ExecuteNonQuery();
                opened.Add(conn);
            }
        }
        catch (Exception ex)
        {
            XTrace.WriteLine("SQLite pool warm failed: {0}", ex.Message);
        }
        finally
        {
            foreach (var conn in opened) conn.Dispose();
        }
    }

    /// <summary>Drop System.Data.SQLite-only keys so Microsoft.Data.Sqlite will parse the string.</summary>
    static String StripUnknown(String connectionString)
    {
        var keep = new HashSet<String>(StringComparer.OrdinalIgnoreCase)
        {
            "Data Source", "DataSource", "Filename",
            "Mode", "Cache", "Password", "Foreign Keys", "Recursive Triggers",
            "Default Timeout", "Pooling", "Command Timeout",
        };

        var parts = new List<String>();
        foreach (var raw in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var eq = raw.IndexOf('=');
            if (eq <= 0) continue;
            var key = raw[..eq].Trim();
            if (keep.Contains(key)) parts.Add(raw);
        }

        return String.Join(";", parts);
    }
}
