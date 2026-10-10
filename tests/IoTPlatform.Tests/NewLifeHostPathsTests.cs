using IoTPlatform.Data;
using NewLife;

namespace IoTPlatform.Tests;

public class NewLifeHostPathsTests
{
    [Fact]
    public void Apply_pins_paths_under_configured_directories()
    {
        var root = Path.Combine(Path.GetTempPath(), $"nl-paths-{Guid.NewGuid():N}");
        var data = Path.Combine(root, "Data");
        var log = Path.Combine(root, "Log");
        lock (NewLifeHostPaths.SyncRoot)
        {
            Environment.SetEnvironmentVariable(NewLifeHostPaths.DataPathEnv, data);
            Environment.SetEnvironmentVariable(NewLifeHostPaths.LogPathEnv, log);
            try
            {
                NewLifeHostPaths.Apply();

                Assert.Equal(Path.GetFullPath(data), Setting.Current.DataPath);
                Assert.Equal(Path.GetFullPath(log), Setting.Current.LogPath);
                Assert.Equal(Path.GetFullPath(Path.Combine(data, "Backup")), Setting.Current.BackupPath);
                Assert.Equal(Path.GetFullPath(Path.Combine(data, "Plugins")), Setting.Current.PluginPath);
                Assert.Equal(Path.GetFullPath(Path.Combine(log, "sql")), XCode.XCodeSetting.Current.SQLPath);

                Assert.False(Setting.Current.DataPath.Replace('\\', '/').Equals("/Data", StringComparison.OrdinalIgnoreCase));
                Assert.True(Directory.Exists(Path.Combine(data, "Config")));
            }
            finally
            {
                Environment.SetEnvironmentVariable(NewLifeHostPaths.DataPathEnv, null);
                Environment.SetEnvironmentVariable(NewLifeHostPaths.LogPathEnv, null);
            }
        }
    }
}
