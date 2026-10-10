namespace IoTPlatform.Protocols;

/// <summary>设备主题规范</summary>
/// <remarks>
/// 上行（设备发布）：
///   iot/{productKey}/{deviceCode}/property/post   属性上报
///   iot/{productKey}/{deviceCode}/event/post      事件上报
///   iot/{productKey}/{deviceCode}/service/reply   服务调用/属性设置回复
///   iot/{productKey}/{deviceCode}/heartbeat       心跳（可选，内置服务端下 PINGREQ 同样有效）
///   iot/{productKey}/{deviceCode}/status          上下线状态 online/offline（建议作为保留消息 + 遗嘱，外部服务器模式据此判定上下线）
/// 下行（设备订阅 iot/{productKey}/{deviceCode}/#）：
///   iot/{productKey}/{deviceCode}/service/invoke  服务调用
///   iot/{productKey}/{deviceCode}/property/set    属性设置
/// </remarks>
public static class Topics
{
    /// <summary>主题根，默认 iot，可按 IoT:Mqtt:TopicRoot 配置（可含多级）</summary>
    public static String Root { get; private set; } = "iot";

    public const String Status = "status";

    public const String PropertyPost = "property/post";
    public const String EventPost = "event/post";
    public const String ServiceReply = "service/reply";
    public const String Heartbeat = "heartbeat";
    public const String ServiceInvoke = "service/invoke";
    public const String PropertySet = "property/set";

    public static readonly String[] Uplinks = [PropertyPost, EventPost, ServiceReply, Heartbeat, Status];
    public static readonly String[] Downlinks = [ServiceInvoke, PropertySet];

    /// <summary>设置主题根，启动时调用</summary>
    public static void Configure(String? root) => Root = Normalize(root);

    /// <summary>规范化主题根：去掉首尾斜杠，含通配符或为空时回退为 iot</summary>
    public static String Normalize(String? root)
    {
        root = root?.Trim().Trim('/');
        return String.IsNullOrEmpty(root) || root.Contains('+') || root.Contains('#') ? "iot" : root;
    }

    /// <summary>平台需要订阅的设备上行主题过滤器（外部服务器模式）</summary>
    public static String[] UplinkFilters(String? root = null)
    {
        var r = Normalize(root ?? Root);
        return Uplinks.Select(k => $"{r}/+/+/{k}").ToArray();
    }

    public static String Build(String productKey, String deviceCode, String kind, String? root = null) =>
        $"{Normalize(root ?? Root)}/{productKey}/{deviceCode}/{kind}";

    /// <summary>设备订阅的下行主题过滤器</summary>
    public static String DeviceFilter(String productKey, String deviceCode, String? root = null) =>
        $"{Normalize(root ?? Root)}/{productKey}/{deviceCode}/#";

    /// <summary>解析设备主题</summary>
    public static Boolean TryParse(String? topic, out String productKey, out String deviceCode, out String kind) =>
        TryParse(topic, Root, out productKey, out deviceCode, out kind);

    /// <summary>按指定主题根解析设备主题</summary>
    public static Boolean TryParse(String? topic, String root, out String productKey, out String deviceCode, out String kind)
    {
        productKey = deviceCode = kind = "";
        if (String.IsNullOrEmpty(topic)) return false;

        if (!topic.StartsWith(root + "/", StringComparison.Ordinal)) return false;

        var parts = topic[(root.Length + 1)..].Split('/');
        if (parts.Length < 3 || parts[0].Length == 0 || parts[1].Length == 0) return false;

        productKey = parts[0];
        deviceCode = parts[1];
        kind = String.Join('/', parts, 2, parts.Length - 2);

        return true;
    }
}
