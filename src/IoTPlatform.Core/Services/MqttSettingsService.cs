using System.Text.RegularExpressions;
using IoTPlatform.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace IoTPlatform.Services;

/// <summary>界面 MQTT 配置：合并覆盖层、写入文件、应用到运行中的 <see cref="IOptions{IoTOptions}"/></summary>
public class MqttSettingsService
{
    static readonly Regex BrokerIdPattern = new(@"^[A-Za-z][A-Za-z0-9_-]{0,39}$", RegexOptions.Compiled);

    readonly IoTOptions _live;
    readonly MqttOptions _baseline;
    readonly MqttSettingsStore _store;
    readonly IMqttRuntimeControl? _runtime;
    readonly HashSet<String> _locks;
    readonly Object _gate = new();

    public MqttSettingsService(IOptions<IoTOptions> options, MqttSettingsStore store, IServiceProvider services)
    {
        _live = options.Value;
        _baseline = MqttSettingsMerger.Clone(_live.Mqtt);
        _store = store;
        _runtime = services.GetService<IMqttRuntimeControl>();
        _locks = MqttEnvLocks.Detect();
    }

    /// <summary>测试用：注入已检测的锁定集合与可选运行时</summary>
    public MqttSettingsService(IOptions<IoTOptions> options, MqttSettingsStore store, ISet<String> envLocks, IMqttRuntimeControl? runtime = null)
    {
        _live = options.Value;
        _baseline = MqttSettingsMerger.Clone(_live.Mqtt);
        _store = store;
        _runtime = runtime;
        _locks = new HashSet<String>(envLocks, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyCollection<String> LockedFields => _locks;
    public Boolean BrokersLocked => MqttEnvLocks.BrokersLocked(_locks);
    public Boolean IsLocked(String field) => _locks.Contains(field);
    public String StorePath => _store.FilePath;

    public void ApplyAtStartup()
    {
        lock (_gate) ApplyToLive();
    }

    public MqttOptions Current()
    {
        lock (_gate) return MqttSettingsMerger.Clone(_live.Mqtt);
    }

    public void SaveBuiltIn(MqttBuiltInInput input)
    {
        Task.Run(() => SaveBuiltInAsync(input)).GetAwaiter().GetResult();
    }

    public async Task SaveBuiltInAsync(MqttBuiltInInput input, CancellationToken cancellationToken = default)
    {
        ValidateBuiltIn(input);
        MqttOptions before, after;
        lock (_gate)
        {
            RejectLocked(MqttEnvLocks.Enable, input.Enable != _live.Mqtt.Enable);
            RejectLocked(MqttEnvLocks.Mode, !String.Equals(input.Mode, _live.Mqtt.Mode, StringComparison.OrdinalIgnoreCase));
            RejectLocked(MqttEnvLocks.TopicRoot, !String.Equals(input.TopicRoot ?? "", _live.Mqtt.TopicRoot ?? "", StringComparison.Ordinal));
            RejectLocked(MqttEnvLocks.ActiveBroker, !String.Equals(input.ActiveBroker ?? "", _live.Mqtt.ActiveBroker ?? "", StringComparison.Ordinal));
            RejectLocked(MqttEnvLocks.Port, input.Port != _live.Mqtt.Port);
            RejectLocked(MqttEnvLocks.PublicHost, !String.Equals(input.PublicHost ?? "", _live.Mqtt.PublicHost ?? "", StringComparison.Ordinal));
            RejectLocked(MqttEnvLocks.AdminUser, !String.Equals(input.AdminUser ?? "", _live.Mqtt.AdminUser ?? "", StringComparison.Ordinal));
            RejectLocked(MqttEnvLocks.AdminPassword, !String.IsNullOrEmpty(input.AdminPassword));
            RejectLocked(MqttEnvLocks.AllowAnonymous, input.AllowAnonymous != _live.Mqtt.AllowAnonymous);
            RejectLocked(MqttEnvLocks.ManagementPort, input.ManagementPort != _live.Mqtt.ManagementPort);

            var previous = _store.Load();
            var ui = CloneUi(previous);
            ui.Enable = input.Enable;
            ui.Mode = input.Mode;
            ui.TopicRoot = input.TopicRoot;
            ui.ActiveBroker = input.ActiveBroker;
            ui.Port = input.Port;
            ui.PublicHost = input.PublicHost ?? "";
            ui.AdminUser = input.AdminUser;
            ui.AdminPassword = input.AdminPassword;
            ui.AllowAnonymous = input.AllowAnonymous;
            ui.ManagementPort = input.ManagementPort;
            MqttSettingsMerger.KeepPasswords(ui, previous, _baseline);
            _store.Save(ui);

            before = MqttSettingsMerger.Clone(_live.Mqtt);
            ApplyToLive();
            after = MqttSettingsMerger.Clone(_live.Mqtt);
        }

        if (_runtime != null) await _runtime.ApplyAsync(before, after, cancellationToken);
    }

    public async Task UpsertBrokerAsync(MqttBrokerInput input, CancellationToken cancellationToken = default)
    {
        if (BrokersLocked) throw new PlatformException("MQTT broker list is locked by environment variables");
        var broker = ToBroker(input);
        ValidateBroker(broker);

        MqttOptions before, after;
        lock (_gate)
        {
            var previous = _store.Load();
            var ui = CloneUi(previous);
            ui.Brokers ??= CurrentBrokers();
            var existing = ui.Brokers.FirstOrDefault(e => String.Equals(e.Id, broker.Id, StringComparison.OrdinalIgnoreCase));
            if (existing == null)
            {
                if (ui.Brokers.Any(e => String.Equals(e.Id, broker.Id, StringComparison.OrdinalIgnoreCase)))
                    throw new PlatformException("MQTT broker '{0}' already exists", broker.Id);
                ui.Brokers.Add(broker);
            }
            else
            {
                if (String.IsNullOrEmpty(broker.Password)) broker.Password = existing.Password;
                var index = ui.Brokers.IndexOf(existing);
                ui.Brokers[index] = broker;
            }

            MqttSettingsMerger.KeepPasswords(ui, previous, _baseline);
            _store.Save(ui);
            before = MqttSettingsMerger.Clone(_live.Mqtt);
            ApplyToLive();
            after = MqttSettingsMerger.Clone(_live.Mqtt);
        }

        if (_runtime != null) await _runtime.ApplyAsync(before, after, cancellationToken);
    }

    public async Task DeleteBrokerAsync(String id, CancellationToken cancellationToken = default)
    {
        if (BrokersLocked) throw new PlatformException("MQTT broker list is locked by environment variables");
        id = RequireId(id);

        MqttOptions before, after;
        lock (_gate)
        {
            var previous = _store.Load();
            var ui = CloneUi(previous);
            ui.Brokers ??= CurrentBrokers();
            var removed = ui.Brokers.RemoveAll(e => String.Equals(e.Id, id, StringComparison.OrdinalIgnoreCase));
            if (removed == 0) throw new PlatformException(404, "MQTT broker '{0}' not found", id);
            if (String.Equals(ui.ActiveBroker, id, StringComparison.OrdinalIgnoreCase)) ui.ActiveBroker = null;
            _store.Save(ui);
            before = MqttSettingsMerger.Clone(_live.Mqtt);
            ApplyToLive();
            after = MqttSettingsMerger.Clone(_live.Mqtt);
        }

        if (_runtime != null) await _runtime.ApplyAsync(before, after, cancellationToken);
    }

    public async Task SetBrokerEnabledAsync(String id, Boolean enable, CancellationToken cancellationToken = default)
    {
        if (BrokersLocked) throw new PlatformException("MQTT broker list is locked by environment variables");
        id = RequireId(id);

        MqttOptions before, after;
        lock (_gate)
        {
            var previous = _store.Load();
            var ui = CloneUi(previous);
            ui.Brokers ??= CurrentBrokers();
            var existing = ui.Brokers.FirstOrDefault(e => String.Equals(e.Id, id, StringComparison.OrdinalIgnoreCase))
                ?? throw new PlatformException(404, "MQTT broker '{0}' not found", id);
            existing.Enable = enable;
            _store.Save(ui);
            before = MqttSettingsMerger.Clone(_live.Mqtt);
            ApplyToLive();
            after = MqttSettingsMerger.Clone(_live.Mqtt);
        }

        if (_runtime != null) await _runtime.ApplyAsync(before, after, cancellationToken);
    }

    /// <summary>测试连接用：按 Id 取已保存配置，表单密码为空时沿用已存密码</summary>
    public ExternalMqttOptions ResolveForTest(MqttBrokerInput input)
    {
        var broker = ToBroker(input);
        if (String.IsNullOrEmpty(broker.Password))
        {
            var stored = _live.Mqtt.FindBroker(broker.Id);
            if (stored != null) broker.Password = stored.Password;
        }
        if (String.IsNullOrWhiteSpace(broker.Server))
            throw new PlatformException("MQTT broker server is required");
        return broker;
    }

    public Boolean HasAdminPassword()
    {
        lock (_gate) return !String.IsNullOrEmpty(_live.Mqtt.AdminPassword);
    }

    public Boolean BrokerHasPassword(String? id) =>
        !String.IsNullOrEmpty(_live.Mqtt.FindBroker(id)?.Password);

    void ApplyToLive()
    {
        var merged = MqttSettingsMerger.Merge(_baseline, _store.Load(), _locks);
        ValidateEffective(merged);
        MqttSettingsMerger.CopyInto(_live.Mqtt, merged);
    }

    static MqttUiSettings CloneUi(MqttUiSettings? src) => src == null
        ? new MqttUiSettings()
        : new MqttUiSettings
        {
            Enable = src.Enable,
            Mode = src.Mode,
            TopicRoot = src.TopicRoot,
            ActiveBroker = src.ActiveBroker,
            Port = src.Port,
            PublicHost = src.PublicHost,
            AdminUser = src.AdminUser,
            AdminPassword = src.AdminPassword,
            AllowAnonymous = src.AllowAnonymous,
            ManagementPort = src.ManagementPort,
            Brokers = src.Brokers?.Select(MqttSettingsMerger.CloneBroker).ToList(),
        };

    List<ExternalMqttOptions> CurrentBrokers() =>
        _live.Mqtt.GetBrokers().Select(MqttSettingsMerger.CloneBroker).ToList();

    void RejectLocked(String field, Boolean changing)
    {
        if (changing && MqttEnvLocks.IsLocked(_locks, field))
            throw new PlatformException("Cannot change MQTT setting '{0}' because it is set by an environment variable", field);
    }

    static void ValidateBuiltIn(MqttBuiltInInput input)
    {
        if (input.Port is < 1 or > 65535) throw new PlatformException("MQTT port must be between 1 and 65535");
        if (input.ManagementPort is < 0 or > 65535) throw new PlatformException("MQTT management port must be between 0 and 65535");
        if (!IsMode(input.Mode)) throw new PlatformException("MQTT mode must be Embedded or External");
    }

    static void ValidateEffective(MqttOptions mqtt)
    {
        if (mqtt.Port is < 1 or > 65535) throw new PlatformException("MQTT port must be between 1 and 65535");
        if (!IsMode(mqtt.Mode)) throw new PlatformException("MQTT mode must be Embedded or External");
        var seen = new HashSet<String>(StringComparer.OrdinalIgnoreCase);
        foreach (var broker in mqtt.GetBrokers())
        {
            if (String.IsNullOrWhiteSpace(broker.Id)) continue;
            if (!seen.Add(broker.Id)) throw new PlatformException("MQTT broker '{0}' already exists", broker.Id);
            if (MqttBrokerIds.IsEmbedded(broker.Id)) throw new PlatformException("MQTT broker id '{0}' is reserved", broker.Id);
        }
    }

    static void ValidateBroker(ExternalMqttOptions broker)
    {
        if (String.IsNullOrWhiteSpace(broker.Id) || !BrokerIdPattern.IsMatch(broker.Id))
            throw new PlatformException("MQTT broker id '{0}' is invalid", broker.Id ?? "");
        if (MqttBrokerIds.IsEmbedded(broker.Id)) throw new PlatformException("MQTT broker id '{0}' is reserved", broker.Id);
        if (String.IsNullOrWhiteSpace(broker.Server)) throw new PlatformException("MQTT broker server is required");
        if (broker.QoS is < 0 or > 1) throw new PlatformException("MQTT QoS must be 0 or 1");
        if (broker.KeepAlive < 5) throw new PlatformException("MQTT keep-alive must be at least 5 seconds");
        if (broker.ReconnectDelay < 1) throw new PlatformException("MQTT reconnect delay must be at least 1 second");
    }

    static ExternalMqttOptions ToBroker(MqttBrokerInput input)
    {
        var id = input.Id?.Trim();
        return new ExternalMqttOptions
        {
            Id = id,
            Name = String.IsNullOrWhiteSpace(input.Name) ? id : input.Name.Trim(),
            TopicRoot = String.IsNullOrWhiteSpace(input.TopicRoot) ? null : input.TopicRoot.Trim(),
            Server = input.Server?.Trim() ?? "",
            ClientId = String.IsNullOrWhiteSpace(input.ClientId) ? "newlife-iot-platform" : input.ClientId.Trim(),
            UserName = String.IsNullOrWhiteSpace(input.UserName) ? null : input.UserName.Trim(),
            Password = input.Password,
            UseTls = input.UseTls,
            QoS = input.QoS,
            KeepAlive = input.KeepAlive,
            ReconnectDelay = input.ReconnectDelay,
            PublicServer = String.IsNullOrWhiteSpace(input.PublicServer) ? null : input.PublicServer.Trim(),
            Enable = input.Enable,
        };
    }

    static String RequireId(String? id)
    {
        id = id?.Trim();
        if (String.IsNullOrEmpty(id)) throw new PlatformException("MQTT broker id '{0}' is invalid", id ?? "");
        return id;
    }

    static Boolean IsMode(String? mode) =>
        String.Equals(mode, MqttModes.Embedded, StringComparison.OrdinalIgnoreCase)
        || String.Equals(mode, MqttModes.External, StringComparison.OrdinalIgnoreCase);
}
