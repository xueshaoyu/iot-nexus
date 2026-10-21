using IoTPlatform.Data;

namespace IoTPlatform;

/// <summary>平台配置。绑定 appsettings.json 的 IoT 节</summary>
public class IoTOptions
{
    public const String SectionName = "IoT";

    /// <summary>离线超时。设备超过该时间无任何心跳或数据判定离线，单位秒。产品可单独覆盖</summary>
    public Int32 OfflineTimeout { get; set; } = 90;

    /// <summary>历史数据保留天数。设备数据、事件、规则日志超过该天数自动清理，0表示不清理</summary>
    public Int32 DataRetentionDays { get; set; } = 7;

    /// <summary>默认采集间隔。驱动轮询间隔，单位毫秒</summary>
    public Int32 DefaultPollInterval { get; set; } = 5000;

    /// <summary>管理接口密钥。非空时 /api 下的管理接口需要携带 X-Api-Key 请求头</summary>
    public String? ApiKey { get; set; }

    public MqttOptions Mqtt { get; set; } = new();

    public DemoOptions Demo { get; set; } = new();

    public AccessLogOptions AccessLog { get; set; } = new();
}

/// <summary>MQTT 模式</summary>
public static class MqttModes
{
    /// <summary>进程内运行 NewLife.MQTT 服务端（默认）</summary>
    public const String Embedded = "Embedded";

    /// <summary>作为客户端连接外部 MQTT 服务器（Mosquitto / EMQX / HiveMQ 等）</summary>
    public const String External = "External";
}

/// <summary>MQTT 配置。Mode 选择内置服务端或外部服务器，同一时间只启用一个</summary>
public class MqttOptions
{
    /// <summary>是否启用 MQTT 接入</summary>
    public Boolean Enable { get; set; } = true;

    /// <summary>模式：Embedded 内置服务端，External 连接外部服务器</summary>
    public String Mode { get; set; } = MqttModes.Embedded;

    /// <summary>主题根，设备主题为 {TopicRoot}/{productKey}/{deviceCode}/...，可含多级如 tenant-a/iot</summary>
    public String TopicRoot { get; set; } = "iot";

    /// <summary>外部服务器列表（Mode=External 时从中选用一个），为空时使用 <see cref="External"/> 作为唯一配置</summary>
    public List<ExternalMqttOptions> Brokers { get; set; } = [];

    /// <summary>平台默认外部服务器 Id（或名称）。设备与产品均未指定时，Mode=External 使用此项（空表示列表第一个）</summary>
    public String? ActiveBroker { get; set; }

    /// <summary>单个外部服务器配置（兼容旧版）。Brokers 为空时作为 Id=default 的唯一配置</summary>
    public ExternalMqttOptions External { get; set; } = new();

    /// <summary>全部外部服务器配置。Brokers 优先；为空时回退为旧版 External；缺省 Id 自动编号</summary>
    public IReadOnlyList<ExternalMqttOptions> GetBrokers()
    {
        if (Brokers.Count == 0)
        {
            if (String.IsNullOrWhiteSpace(External.Id)) External.Id = "default";
            return [External];
        }

        for (var i = 0; i < Brokers.Count; i++)
        {
            if (String.IsNullOrWhiteSpace(Brokers[i].Id)) Brokers[i].Id = $"broker-{i + 1}";
        }

        return Brokers;
    }

    /// <summary>当前选用的外部服务器：按 ActiveBroker 匹配 Id 或名称，未设置时取第一个；找不到返回 null（不会回退到其它服务器）</summary>
    public ExternalMqttOptions? GetActiveBroker()
    {
        var brokers = GetBrokers().Where(e => e.Enable).ToList();
        if (String.IsNullOrWhiteSpace(ActiveBroker)) return brokers.FirstOrDefault();

        var key = ActiveBroker.Trim();
        return brokers.FirstOrDefault(e => String.Equals(e.Id, key, StringComparison.OrdinalIgnoreCase))
            ?? brokers.FirstOrDefault(e => String.Equals(e.Name, key, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>平台默认 MQTT 服务器标识：External 为 ActiveBroker（或第一项），否则为内置 embedded</summary>
    public String GetPlatformDefaultId()
    {
        if (IsExternal)
        {
            var active = GetActiveBroker();
            if (active != null) return active.Id!;
            return String.IsNullOrWhiteSpace(ActiveBroker)
                ? GetBrokers().FirstOrDefault(e => e.Enable)?.Id ?? MqttBrokerIds.Embedded
                : ActiveBroker.Trim();
        }

        return MqttBrokerIds.Embedded;
    }

    /// <summary>按 Id 或名称查找外部服务器配置</summary>
    public ExternalMqttOptions? FindBroker(String? idOrName)
    {
        var key = idOrName?.Trim();
        if (String.IsNullOrEmpty(key) || MqttBrokerIds.IsEmbedded(key)) return null;

        return GetBrokers().FirstOrDefault(e => String.Equals(e.Id, key, StringComparison.OrdinalIgnoreCase))
            ?? GetBrokers().FirstOrDefault(e => String.Equals(e.Name, key, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>指定服务器的主题根：外部配置可覆盖全局 TopicRoot</summary>
    public String TopicRootFor(String? brokerProfileId)
    {
        var broker = FindBroker(brokerProfileId);
        return broker?.TopicRoot is { Length: > 0 } root ? root : TopicRoot;
    }

    /// <summary>生效的主题根：平台默认服务器的主题根</summary>
    public String EffectiveTopicRoot => TopicRootFor(GetPlatformDefaultId());

    public Boolean IsExternal => Enable && String.Equals(Mode, MqttModes.External, StringComparison.OrdinalIgnoreCase);

    public Boolean IsEmbedded => Enable && !IsExternal;

    /// <summary>内置服务端监听端口。同一端口同时支持 TCP 与 WebSocket(/mqtt)</summary>
    public Int32 Port { get; set; } = 1883;

    /// <summary>对外公布的连接地址，仅用于界面展示接入信息</summary>
    public String? PublicHost { get; set; }

    /// <summary>管理账号。可订阅/发布任意主题，用于平台集成、调试工具</summary>
    public String? AdminUser { get; set; } = "admin";

    /// <summary>管理密码</summary>
    public String? AdminPassword { get; set; } = "admin123";

    /// <summary>允许匿名连接。匿名客户端只能订阅 $SYS 与公共主题，不能冒充设备上报</summary>
    public Boolean AllowAnonymous { get; set; }

    /// <summary>MQTT 服务端管理端口（NewLife.MQTT 内置管理 API/看板），0 表示关闭</summary>
    public Int32 ManagementPort { get; set; }
}

/// <summary>外部 MQTT 服务器连接参数。平台以客户端身份订阅设备上行主题并发布下行指令</summary>
public class ExternalMqttOptions
{
    /// <summary>标识，供 ActiveBroker 选用，如 mosquitto、emqx-prod</summary>
    public String? Id { get; set; }

    /// <summary>显示名称</summary>
    public String? Name { get; set; }

    /// <summary>主题根覆盖（可选），空表示使用全局 IoT:Mqtt:TopicRoot</summary>
    public String? TopicRoot { get; set; }

    /// <summary>服务器地址，如 tcp://127.0.0.1:1883、tcp://emqx.example.com:8883</summary>
    public String Server { get; set; } = "tcp://127.0.0.1:1883";

    /// <summary>平台客户端 ClientId，同一服务器上需唯一</summary>
    public String ClientId { get; set; } = "newlife-iot-platform";

    public String? UserName { get; set; }

    public String? Password { get; set; }

    /// <summary>使用 TLS</summary>
    public Boolean UseTls { get; set; }

    /// <summary>订阅与下行发布使用的 QoS（0/1）</summary>
    public Int32 QoS { get; set; } = 1;

    /// <summary>心跳间隔，秒</summary>
    public Int32 KeepAlive { get; set; } = 30;

    /// <summary>断线重连间隔，秒</summary>
    public Int32 ReconnectDelay { get; set; } = 5;

    /// <summary>展示给设备的接入地址（界面接入信息），空表示与 Server 相同</summary>
    public String? PublicServer { get; set; }

    /// <summary>是否启用。关闭后不作为平台默认，也不再为该配置建立平台客户端连接</summary>
    public Boolean Enable { get; set; } = true;
}

/// <summary>演示配置</summary>
public class DemoOptions
{
    /// <summary>首次启动时写入演示产品、设备、分组与规则</summary>
    public Boolean SeedData { get; set; } = true;

    /// <summary>在平台进程内运行设备模拟器（MQTT 设备 + Modbus 从站），使演示数据实时流动</summary>
    public Boolean EnableSimulator { get; set; } = true;

    /// <summary>内置 Modbus TCP 从站模拟器端口</summary>
    public Int32 ModbusPort { get; set; } = 1502;

    /// <summary>演示数据语言。zh-CN 写入中文名称，其它值写入英文</summary>
    public String Language { get; set; } = "zh-CN";

    /// <summary>模拟器上报间隔，单位毫秒</summary>
    public Int32 ReportInterval { get; set; } = 3000;
}

/// <summary>管理端访问记录</summary>
public class AccessLogOptions
{
    /// <summary>是否记录管理端页面和 API 访问</summary>
    public Boolean Enable { get; set; } = true;

    /// <summary>保留天数。0 表示不清理</summary>
    public Int32 RetentionDays { get; set; } = 30;

    /// <summary>信任这些来源上的转发头。支持 IP 和 CIDR。本机回环始终信任</summary>
    public List<String> TrustedProxies { get; set; } = ["127.0.0.1", "::1"];

    /// <summary>路径前缀命中则不记录。不含查询串</summary>
    public List<String> ExcludePrefixes { get; set; } =
    [
        "/_framework",
        "/_blazor",
        "/_content",
        "/css",
        "/js",
        "/lib",
        "/favicon.ico",
    ];

    /// <summary>这些扩展名不记录</summary>
    public List<String> ExcludeExtensions { get; set; } =
    [
        ".css", ".js", ".map", ".png", ".jpg", ".jpeg", ".gif", ".svg", ".ico", ".woff", ".woff2", ".ttf", ".wasm", ".webmanifest",
    ];
}
