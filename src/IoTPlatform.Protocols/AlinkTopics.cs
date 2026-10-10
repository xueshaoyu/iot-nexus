namespace IoTPlatform.Protocols;

/// <summary>阿里云 Alink 物模型 Topic。与平台原有 <c>iot/{pk}/{dn}/...</c> 主题并存</summary>
/// <remarks>
/// 上行：
///   /sys/{pk}/{dn}/thing/event/property/post
///   /sys/{pk}/{dn}/thing/event/{identifier}/post
///   /sys/{pk}/{dn}/thing/service/property/set_reply
///   /sys/{pk}/{dn}/thing/service/{identifier}_reply
/// 下行：
///   /sys/{pk}/{dn}/thing/event/property/post_reply
///   /sys/{pk}/{dn}/thing/event/{identifier}/post_reply
///   /sys/{pk}/{dn}/thing/service/property/set
///   /sys/{pk}/{dn}/thing/service/{identifier}
/// 自定义模块的 identifier 写成 <c>functionBlockId:identifier</c>。
/// 同时接受没有前导斜杠的 <c>sys/...</c>，下发使用阿里云文档中的前导斜杠形式。
/// </remarks>
public static class AlinkTopics
{
    public enum Kind
    {
        None,
        PropertyPost,
        EventPost,
        PropertySetReply,
        ServiceReply,
    }

    public static Boolean IsUplink(Kind kind) => kind is Kind.PropertyPost or Kind.EventPost or Kind.PropertySetReply or Kind.ServiceReply;

    public static String PropertyPost(String productKey, String deviceName) => $"/sys/{productKey}/{deviceName}/thing/event/property/post";

    public static String PropertyPostReply(String productKey, String deviceName) => $"/sys/{productKey}/{deviceName}/thing/event/property/post_reply";

    public static String EventPost(String productKey, String deviceName, String identifier) => $"/sys/{productKey}/{deviceName}/thing/event/{identifier}/post";

    public static String EventPostReply(String productKey, String deviceName, String identifier) => $"/sys/{productKey}/{deviceName}/thing/event/{identifier}/post_reply";

    public static String PropertySet(String productKey, String deviceName) => $"/sys/{productKey}/{deviceName}/thing/service/property/set";

    public static String PropertySetReply(String productKey, String deviceName) => $"/sys/{productKey}/{deviceName}/thing/service/property/set_reply";

    public static String Service(String productKey, String deviceName, String identifier) => $"/sys/{productKey}/{deviceName}/thing/service/{identifier}";

    public static String ServiceReply(String productKey, String deviceName, String identifier) => $"/sys/{productKey}/{deviceName}/thing/service/{identifier}_reply";

    /// <summary>外部 MQTT 服务器上需要订阅的 Alink 上行过滤器</summary>
    public static String[] UplinkFilters() =>
    [
        "/sys/+/+/thing/event/property/post",
        "/sys/+/+/thing/event/+/post",
        "/sys/+/+/thing/service/property/set_reply",
        "/sys/+/+/thing/service/+",
        "sys/+/+/thing/event/property/post",
        "sys/+/+/thing/event/+/post",
        "sys/+/+/thing/service/property/set_reply",
        "sys/+/+/thing/service/+",
    ];

    public static Boolean TryParse(String? topic, out String productKey, out String deviceName, out Kind kind, out String identifier)
    {
        productKey = deviceName = identifier = "";
        kind = Kind.None;
        if (String.IsNullOrWhiteSpace(topic)) return false;

        var parts = topic.Trim().Trim('/').Split('/');
        if (parts.Length < 6 || !parts[0].Equals("sys", StringComparison.Ordinal) || !parts[3].Equals("thing", StringComparison.Ordinal))
            return false;
        if (parts[1].Length == 0 || parts[2].Length == 0) return false;

        productKey = parts[1];
        deviceName = parts[2];
        if (parts[4].Equals("event", StringComparison.Ordinal))
        {
            if (parts.Length == 7 && parts[6].Equals("post", StringComparison.Ordinal))
            {
                if (parts[5].Equals("property", StringComparison.Ordinal)) kind = Kind.PropertyPost;
                else
                {
                    kind = Kind.EventPost;
                    identifier = parts[5];
                }
                return true;
            }
        }
        else if (parts[4].Equals("service", StringComparison.Ordinal))
        {
            if (parts.Length == 7 && parts[5].Equals("property", StringComparison.Ordinal) && parts[6].Equals("set_reply", StringComparison.Ordinal))
            {
                kind = Kind.PropertySetReply;
                return true;
            }

            if (parts.Length == 6 && parts[5].EndsWith("_reply", StringComparison.Ordinal) && parts[5].Length > "_reply".Length)
            {
                kind = Kind.ServiceReply;
                identifier = parts[5][..^"_reply".Length];
                return true;
            }
        }

        return false;
    }
}
