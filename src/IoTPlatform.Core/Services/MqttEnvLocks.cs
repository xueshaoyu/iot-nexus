namespace IoTPlatform.Services;

/// <summary>
/// 环境变量对 MQTT 配置的锁定。进程环境中的 <c>IoT__Mqtt__*</c> / <c>IoT:Mqtt:*</c>
/// 优先于界面保存的值，也优先于 appsettings.json。
/// </summary>
public static class MqttEnvLocks
{
    public const String Prefix = "IoT:Mqtt:";

    public const String Enable = nameof(MqttOptions.Enable);
    public const String Mode = nameof(MqttOptions.Mode);
    public const String TopicRoot = nameof(MqttOptions.TopicRoot);
    public const String ActiveBroker = nameof(MqttOptions.ActiveBroker);
    public const String Port = nameof(MqttOptions.Port);
    public const String PublicHost = nameof(MqttOptions.PublicHost);
    public const String AdminUser = nameof(MqttOptions.AdminUser);
    public const String AdminPassword = nameof(MqttOptions.AdminPassword);
    public const String AllowAnonymous = nameof(MqttOptions.AllowAnonymous);
    public const String ManagementPort = nameof(MqttOptions.ManagementPort);
    public const String Brokers = nameof(MqttOptions.Brokers);
    public const String External = nameof(MqttOptions.External);

    /// <summary>从进程环境变量检测被锁定的顶层字段（Enable、Port、Brokers…）</summary>
    public static HashSet<String> Detect() => Detect(EnumerateProcessKeys());

    /// <summary>从给定键名检测锁定（便于测试）。键可为 <c>IoT__Mqtt__Port</c> 或 <c>IoT:Mqtt:Port</c></summary>
    public static HashSet<String> Detect(IEnumerable<String> keys)
    {
        var locks = new HashSet<String>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in keys)
        {
            var field = Normalize(raw);
            if (field == null) continue;

            var top = field.Split(':')[0];
            locks.Add(top);
            if (top.Equals(Brokers, StringComparison.OrdinalIgnoreCase) || top.Equals(External, StringComparison.OrdinalIgnoreCase))
                locks.Add(Brokers);
        }

        return locks;
    }

    public static Boolean IsLocked(ISet<String> locks, String field) => locks.Contains(field);

    public static Boolean BrokersLocked(ISet<String> locks) =>
        locks.Contains(Brokers) || locks.Contains(External);

    /// <summary>去掉 IoT:Mqtt: / IoT__Mqtt__ 前缀，统一为冒号分隔</summary>
    public static String? Normalize(String? key)
    {
        if (String.IsNullOrWhiteSpace(key)) return null;
        var n = key.Trim().Replace("__", ":");
        if (!n.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)) return null;
        var rest = n[Prefix.Length..];
        return rest.Length == 0 ? null : rest;
    }

    static IEnumerable<String> EnumerateProcessKeys()
    {
        foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            if (entry.Key is String key) yield return key;
        }
    }
}
