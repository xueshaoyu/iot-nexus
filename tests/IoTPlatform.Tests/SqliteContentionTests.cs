using IoTPlatform.Data;
using Microsoft.Data.Sqlite;

namespace IoTPlatform.Tests;

public class SqliteSettingsTests
{
    [Fact]
    public void Enhance_adds_shared_cache_and_busy_timeout()
    {
        var result = SqliteSettings.EnhanceConnectionString("Data Source=Data/iot.db");
        var builder = new SqliteConnectionStringBuilder(result);
        Assert.Equal(SqliteCacheMode.Shared, builder.Cache);
        Assert.Equal(SqliteSettings.BusyTimeoutSeconds, builder.DefaultTimeout);
        Assert.Contains("iot.db", builder.DataSource, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Enhance_is_idempotent_and_strips_system_data_sqlite_keys()
    {
        var first = SqliteSettings.EnhanceConnectionString("Data Source=x.db;Journal Mode=WAL;Synchronous=Normal;Busy Timeout=5000");
        var second = SqliteSettings.EnhanceConnectionString(first);
        var builder = new SqliteConnectionStringBuilder(second);
        Assert.Equal(SqliteCacheMode.Shared, builder.Cache);
        Assert.Equal(SqliteSettings.BusyTimeoutSeconds, builder.DefaultTimeout);
        Assert.DoesNotContain("Journal Mode", second, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Synchronous", second, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void IsSqlite_detects_provider_and_connection_string()
    {
        Assert.True(SqliteSettings.IsSqlite("SQLite"));
        Assert.True(SqliteSettings.IsSqlite("Microsoft.Data.Sqlite"));
        Assert.True(SqliteSettings.IsSqlite(null, "Data Source=a.db;Provider=SQLite"));
        Assert.False(SqliteSettings.IsSqlite("MySql", "Server=localhost"));
    }

    [Fact]
    public void Pragmas_enable_wal_normal_and_busy_timeout()
    {
        var db = Path.Combine(Path.GetTempPath(), $"iot-wal-{Guid.NewGuid():N}.db");
        var cs = SqliteSettings.EnhanceConnectionString($"Data Source={db}");
        using var conn = new SqliteConnection(cs);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = String.Join(" ", SqliteSettings.PragmaStatements);
        cmd.ExecuteNonQuery();

        cmd.CommandText = "PRAGMA journal_mode;";
        Assert.Equal("wal", cmd.ExecuteScalar()?.ToString(), ignoreCase: true);

        cmd.CommandText = "PRAGMA synchronous;";
        Assert.Equal("1", cmd.ExecuteScalar()?.ToString()); // NORMAL

        cmd.CommandText = "PRAGMA busy_timeout;";
        Assert.Equal(SqliteSettings.BusyTimeoutMs.ToString(), cmd.ExecuteScalar()?.ToString());
    }
}

public class SqliteBusyTests
{
    [Fact]
    public void IsLocked_recognizes_error_5_and_inner_exceptions()
    {
        Assert.True(SqliteBusy.IsLocked(new Exception("SQLite Error 5: 'database is locked'")));
        Assert.True(SqliteBusy.IsLocked(new InvalidOperationException("write failed", new Exception("SQLITE_BUSY"))));
        Assert.False(SqliteBusy.IsLocked(new Exception("constraint failed")));
        Assert.False(SqliteBusy.IsLocked(null));
    }

    [Fact]
    public void Retry_recovers_from_transient_lock()
    {
        var n = 0;
        var result = SqliteBusy.Retry(() =>
        {
            if (++n < 3) throw new Exception("SQLite Error 5: 'database is locked'");
            return 42;
        });
        Assert.Equal(42, result);
        Assert.Equal(3, n);
    }

    [Fact]
    public void RetryOrFallback_used_when_still_locked()
    {
        var result = SqliteBusy.RetryOrFallback(
            () => throw new Exception("database is locked"),
            () => "queued",
            attempts: 2);
        Assert.Equal("queued", result);
    }
}

