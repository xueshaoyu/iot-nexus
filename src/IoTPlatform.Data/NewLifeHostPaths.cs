using System.IO;
using NewLife;
using XCode;

namespace IoTPlatform.Data;

/// <summary>
/// NewLife.Core <c>Setting.OnLoaded</c> treats assemblies whose name ends with
/// Web/Api/Server/Service/Job as multi-app hosts and defaults
/// <c>DataPath</c> to <c>../Data</c> (and Log to <c>../{cwd}Log</c>).
/// With WORKDIR <c>/app</c> that resolves to <c>/Data</c> and <c>/appLog</c>,
/// which the non-root container user cannot create.
/// </summary>
public static class NewLifeHostPaths
{
    public const String DataPathEnv = "NEWLIFE_DATAPATH";
    public const String LogPathEnv = "NEWLIFE_LOGPATH";

    /// <summary>Setting.Current 是进程级单例，测试和启动初始化不能交错改写。</summary>
    public static readonly Object SyncRoot = new();

    /// <summary>Pin Core/XCode directories before the first cache or config save.</summary>
    public static void Apply()
    {
        lock (SyncRoot)
            ApplyCore();
    }

    private static void ApplyCore()
    {
        var app = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var data = Resolve(Environment.GetEnvironmentVariable(DataPathEnv), Path.Combine(app, "Data"));
        var log = Resolve(Environment.GetEnvironmentVariable(LogPathEnv), Path.Combine(app, "Log"));
        var backup = Path.Combine(data, "Backup");
        var plugins = Path.Combine(data, "Plugins");
        var config = Path.Combine(data, "Config");

        // Keep PathHelper.BasePath at the app directory (default /app). XTrace builds
        // the file log as GetFullPath(BasePath) + "Log" without a separator; pointing
        // BasePath at /app/Data produces /app/DataLog. Config files use /app/Config,
        // which the entrypoint symlinks to /app/Data/Config.
        foreach (var dir in new[] { data, log, backup, plugins, config })
            Directory.CreateDirectory(dir);

        // Seed Core.config before Setting.Current so OnLoaded does not fill
        // LogPath=../{cwd}Log (→ /appLog) and DataPath=../Data (→ /Data).
        SeedCoreConfig(Path.Combine(AppContext.BaseDirectory, "Config", "Core.config"), data, log, backup, plugins);
        SeedCoreConfig(Path.Combine(config, "Core.config"), data, log, backup, plugins);

        var core = NewLife.Setting.Current;
        core.DataPath = data;
        core.LogPath = log;
        core.BackupPath = backup;
        core.PluginPath = plugins;
        try { core.Save(); } catch { /* config dir may still be created by the entrypoint */ }

        var xcode = XCode.XCodeSetting.Current;
        xcode.SQLPath = Path.Combine(log, "sql");
        try { xcode.Save(); } catch { }
    }

    static String Resolve(String? configured, String fallback)
    {
        var path = String.IsNullOrWhiteSpace(configured) ? fallback : configured.Trim();
        return Path.GetFullPath(path);
    }

    static void SeedCoreConfig(String file, String data, String log, String backup, String plugins)
    {
        try
        {
            var dir = Path.GetDirectoryName(file);
            if (!String.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            if (File.Exists(file)) return;
            File.WriteAllText(file,
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>" + Environment.NewLine +
                "<Core>" + Environment.NewLine +
                $"  <LogPath>{log}</LogPath>" + Environment.NewLine +
                $"  <DataPath>{data}</DataPath>" + Environment.NewLine +
                $"  <BackupPath>{backup}</BackupPath>" + Environment.NewLine +
                $"  <PluginPath>{plugins}</PluginPath>" + Environment.NewLine +
                "</Core>" + Environment.NewLine);
        }
        catch
        {
            // Entrypoint creates /app/Config → /app/Data/Config; ignore if not writable yet.
        }
    }
}
