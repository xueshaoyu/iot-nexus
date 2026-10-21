namespace IoTPlatform.Services;

/// <summary>
/// 合并 MQTT 配置：基线（appsettings + 已由 .NET 叠上的环境变量）+ 界面覆盖，跳过环境变量锁定的字段。
/// 优先级：环境变量 <c>IoT__Mqtt__*</c> &gt; 界面 <c>mqtt-ui.json</c> &gt; appsettings.json。
/// </summary>
public static class MqttSettingsMerger
{
    public static MqttOptions Merge(MqttOptions baseline, MqttUiSettings? ui, ISet<String>? envLocks = null)
    {
        var result = Clone(baseline);
        if (ui == null) return result;

        var locks = envLocks ?? new HashSet<String>(StringComparer.OrdinalIgnoreCase);

        Apply(locks, MqttEnvLocks.Enable, ui.Enable, v => result.Enable = v);
        Apply(locks, MqttEnvLocks.Mode, ui.Mode, v => { if (!String.IsNullOrWhiteSpace(v)) result.Mode = v.Trim(); });
        Apply(locks, MqttEnvLocks.TopicRoot, ui.TopicRoot, v => result.TopicRoot = v ?? result.TopicRoot);
        Apply(locks, MqttEnvLocks.ActiveBroker, ui.ActiveBroker, v => result.ActiveBroker = v);
        Apply(locks, MqttEnvLocks.Port, ui.Port, v => result.Port = v);
        Apply(locks, MqttEnvLocks.PublicHost, ui.PublicHost, v => result.PublicHost = v);
        Apply(locks, MqttEnvLocks.AdminUser, ui.AdminUser, v => result.AdminUser = v);
        Apply(locks, MqttEnvLocks.AdminPassword, ui.AdminPassword, v => result.AdminPassword = v);
        Apply(locks, MqttEnvLocks.AllowAnonymous, ui.AllowAnonymous, v => result.AllowAnonymous = v);
        Apply(locks, MqttEnvLocks.ManagementPort, ui.ManagementPort, v => result.ManagementPort = v);

        if (ui.Brokers != null && !MqttEnvLocks.BrokersLocked(locks))
            result.Brokers = ui.Brokers.Select(CloneBroker).ToList();

        return result;
    }

    /// <summary>空密码表示保持上一份界面值，再回退基线</summary>
    public static MqttUiSettings KeepPasswords(MqttUiSettings incoming, MqttUiSettings? previous, MqttOptions baseline)
    {
        if (String.IsNullOrEmpty(incoming.AdminPassword))
            incoming.AdminPassword = NonEmpty(previous?.AdminPassword) ?? baseline.AdminPassword;

        if (incoming.Brokers == null) return incoming;

        foreach (var broker in incoming.Brokers)
        {
            if (!String.IsNullOrEmpty(broker.Password)) continue;
            var prev = Find(previous?.Brokers, broker.Id)
                ?? baseline.FindBroker(broker.Id)
                ?? (String.Equals(baseline.External.Id, broker.Id, StringComparison.OrdinalIgnoreCase) ? baseline.External : null);
            if (prev != null && !String.IsNullOrEmpty(prev.Password)) broker.Password = prev.Password;
        }

        return incoming;
    }

    public static Boolean NeedsEmbeddedRestart(MqttOptions before, MqttOptions after) =>
        before.Enable != after.Enable
        || before.Port != after.Port
        || before.ManagementPort != after.ManagementPort
        || !String.Equals(before.Mode, after.Mode, StringComparison.OrdinalIgnoreCase);

    public static Boolean IsHotReloadOnly(MqttOptions before, MqttOptions after) => !NeedsEmbeddedRestart(before, after);

    public static MqttOptions Clone(MqttOptions src)
    {
        var brokers = src.Brokers.Count > 0 ? src.Brokers : src.GetBrokers().ToList();
        return new MqttOptions
        {
            Enable = src.Enable,
            Mode = src.Mode,
            TopicRoot = src.TopicRoot,
            ActiveBroker = src.ActiveBroker,
            Brokers = brokers.Select(CloneBroker).ToList(),
            External = CloneBroker(src.External),
            Port = src.Port,
            PublicHost = src.PublicHost,
            AdminUser = src.AdminUser,
            AdminPassword = src.AdminPassword,
            AllowAnonymous = src.AllowAnonymous,
            ManagementPort = src.ManagementPort,
        };
    }

    public static ExternalMqttOptions CloneBroker(ExternalMqttOptions src) => new()
    {
        Id = src.Id,
        Name = src.Name,
        TopicRoot = src.TopicRoot,
        Scheme = src.Scheme,
        Host = src.Host,
        Port = src.Port,
        Path = src.Path,
        Server = src.Server,
        ClientId = src.ClientId,
        UserName = src.UserName,
        Password = src.Password,
        UseTls = src.UseTls,
        QoS = src.QoS,
        KeepAlive = src.KeepAlive,
        ReconnectDelay = src.ReconnectDelay,
        PublicServer = src.PublicServer,
        Enable = src.Enable,
    };

    public static void CopyInto(MqttOptions dest, MqttOptions src)
    {
        dest.Enable = src.Enable;
        dest.Mode = src.Mode;
        dest.TopicRoot = src.TopicRoot;
        dest.ActiveBroker = src.ActiveBroker;
        dest.Brokers = src.Brokers.Select(CloneBroker).ToList();
        dest.External = CloneBroker(src.External);
        dest.Port = src.Port;
        dest.PublicHost = src.PublicHost;
        dest.AdminUser = src.AdminUser;
        dest.AdminPassword = src.AdminPassword;
        dest.AllowAnonymous = src.AllowAnonymous;
        dest.ManagementPort = src.ManagementPort;
    }

    public static String Fingerprint(ExternalMqttOptions src)
    {
        var endpoint = MqttBrokerEndpoint.From(src);
        return String.Join('|', src.Id, endpoint.Server, src.ClientId, src.UserName, src.Password, endpoint.UseTls, src.QoS, src.KeepAlive, src.ReconnectDelay, src.TopicRoot, src.Enable);
    }

    static void Apply<T>(ISet<String> locks, String field, T? value, Action<T> assign) where T : struct
    {
        if (value.HasValue && !MqttEnvLocks.IsLocked(locks, field)) assign(value.Value);
    }

    static void Apply(ISet<String> locks, String field, String? value, Action<String?> assign)
    {
        if (value != null && !MqttEnvLocks.IsLocked(locks, field)) assign(value);
    }

    static String? NonEmpty(String? value) => String.IsNullOrEmpty(value) ? null : value;

    static ExternalMqttOptions? Find(IEnumerable<ExternalMqttOptions>? list, String? id)
    {
        var key = id?.Trim();
        if (String.IsNullOrEmpty(key) || list == null) return null;
        return list.FirstOrDefault(e => String.Equals(e.Id, key, StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>通知 MQTT 运行时按新配置热更新或重启内置服务端</summary>
public interface IMqttRuntimeControl
{
    Task ApplyAsync(MqttOptions before, MqttOptions after, CancellationToken cancellationToken = default);
}
