namespace IoTPlatform.Data;

/// <summary>接入协议</summary>
public static class AccessProtocols
{
    /// <summary>设备通过内置MQTT服务端接入，主动上报</summary>
    public const String Mqtt = "MQTT";

    /// <summary>设备通过HTTP接口上报</summary>
    public const String Http = "HTTP";

    /// <summary>平台通过NewLife.IoT驱动主动采集（Modbus等）</summary>
    public const String Driver = "Driver";

    public static readonly String[] All = [Mqtt, Http, Driver];
}

/// <summary>MQTT 服务器配置标识。设备/产品 BrokerProfileId 为空时回退平台默认</summary>
public static class MqttBrokerIds
{
    /// <summary>内置 NewLife.MQTT 服务端，设备下拉中始终可选</summary>
    public const String Embedded = "embedded";

    public static Boolean IsEmbedded(String? id) =>
        !String.IsNullOrWhiteSpace(id) &&
        (id.Equals(Embedded, StringComparison.OrdinalIgnoreCase) || id.Equals("Embedded", StringComparison.OrdinalIgnoreCase));

    public static String? Normalize(String? id)
    {
        id = id?.Trim();
        if (String.IsNullOrEmpty(id)) return null;
        return IsEmbedded(id) ? Embedded : id;
    }
}

/// <summary>规则触发类型</summary>
public static class RuleTriggers
{
    public const String Property = "Property";
    public const String Event = "Event";
    public const String Online = "Online";
    public const String Offline = "Offline";

    public static readonly String[] All = [Property, Event, Online, Offline];
}

/// <summary>设备历史操作</summary>
public static class DeviceActions
{
    public const String Register = "Register";
    public const String Online = "Online";
    public const String Offline = "Offline";
    public const String Enable = "Enable";
    public const String Disable = "Disable";
    public const String Service = "Service";
    public const String SetProperty = "SetProperty";
    public const String Rule = "Rule";
}
