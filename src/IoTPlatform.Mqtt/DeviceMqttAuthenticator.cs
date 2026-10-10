using System.Collections.Concurrent;
using IoTPlatform.Data;
using IoTPlatform.Protocols;
using IoTPlatform.Services;
using Microsoft.Extensions.Options;
using NewLife;
using NewLife.Log;
using NewLife.MQTT.Handlers;
using NewLife.MQTT.Messaging;

namespace IoTPlatform.Mqtt;

/// <summary>MQTT 客户端身份</summary>
public record MqttIdentity(String Kind, Int32 DeviceId = 0, String? ProductKey = null, String? DeviceCode = null)
{
    public const String Admin = "admin";
    public const String Device = "device";
    public const String Anonymous = "anonymous";

    public Boolean IsAdmin => Kind == Admin;
    public Boolean IsDevice => Kind == Device;
}

/// <summary>设备认证与主题授权</summary>
/// <remarks>
/// 认证：用户名 = 设备编码（DeviceName），密码 = 设备密钥；管理账号可访问全部主题。
/// 授权：设备只能发布自己的上行主题 iot/{pk}/{dc}/(property/post|event/post|service/reply|heartbeat)，
/// 只能订阅 iot/{pk}/{dc}/ 下的主题，防止冒充其它设备。
/// </remarks>
public class DeviceMqttAuthenticator(IOptions<IoTOptions> options, MqttBrokerCatalog catalog) : IMqttAuthenticator
{
    private readonly ConcurrentDictionary<String, MqttIdentity> _identities = new();

    public MqttIdentity? GetIdentity(String? clientId) => clientId != null && _identities.TryGetValue(clientId, out var id) ? id : null;

    public ConnectReturnCode Authenticate(String? clientId, String? username, String? password)
    {
        if (clientId.IsNullOrEmpty()) return ConnectReturnCode.RefusedIdentifierRejected;

        var mqtt = options.Value.Mqtt;
        if (!mqtt.AdminUser.IsNullOrEmpty() && username == mqtt.AdminUser)
        {
            if (password != mqtt.AdminPassword) return ConnectReturnCode.RefusedBadUsernameOrPassword;

            _identities[clientId] = new MqttIdentity(MqttIdentity.Admin);
            return ConnectReturnCode.Accepted;
        }

        if (username.IsNullOrEmpty())
        {
            if (!mqtt.AllowAnonymous) return ConnectReturnCode.RefusedNotAuthorized;

            _identities[clientId] = new MqttIdentity(MqttIdentity.Anonymous);
            return ConnectReturnCode.Accepted;
        }

        // 兼容 "deviceCode&productKey" 形式的用户名
        var code = username.Split('&')[0];
        var device = DeviceService.Authenticate(code, password, out var error);
        if (device == null)
        {
            XTrace.WriteLine("MQTT认证失败 clientId={0} user={1} {2}", clientId, username, error);
            return error is "bad secret" or "unknown device" ? ConnectReturnCode.RefusedBadUsernameOrPassword : ConnectReturnCode.RefusedNotAuthorized;
        }

        var product = device.Product!;
        if (product.Protocol != AccessProtocols.Mqtt)
        {
            XTrace.WriteLine("MQTT认证失败 {0}: product {1} uses protocol {2}", code, product.Code, product.Protocol);
            return ConnectReturnCode.RefusedNotAuthorized;
        }

        var assigned = catalog.Resolve(device, product);
        if (!catalog.IsEmbedded(assigned))
        {
            XTrace.WriteLine("MQTT认证失败 {0}: device is assigned to broker {1}, not the embedded server", code, assigned);
            return ConnectReturnCode.RefusedNotAuthorized;
        }

        _identities[clientId] = new MqttIdentity(MqttIdentity.Device, device.Id, product.Code, device.Code);
        return ConnectReturnCode.Accepted;
    }

    public Boolean AuthorizePublish(String? clientId, String topic)
    {
        var id = GetIdentity(clientId);
        if (id == null) return false;
        if (id.IsAdmin) return true;
        if (!id.IsDevice) return false;

        if (Topics.TryParse(topic, out var pk, out var dc, out var kind)
            && pk == id.ProductKey && dc == id.DeviceCode && Topics.Uplinks.Contains(kind))
            return true;

        return AlinkTopics.TryParse(topic, out pk, out dc, out var alink, out _)
            && pk == id.ProductKey && dc == id.DeviceCode && AlinkTopics.IsUplink(alink);
    }

    public Boolean AuthorizeSubscribe(String? clientId, String topicFilter)
    {
        var id = GetIdentity(clientId);
        if (id == null) return false;
        if (id.IsAdmin) return true;
        if (id.IsDevice)
        {
            var own = $"{Topics.Root}/{id.ProductKey}/{id.DeviceCode}/";
            var alink = $"sys/{id.ProductKey}/{id.DeviceCode}/";
            return topicFilter.StartsWith(own, StringComparison.Ordinal)
                || topicFilter.StartsWith(alink, StringComparison.Ordinal)
                || topicFilter.StartsWith("/" + alink, StringComparison.Ordinal);
        }

        return topicFilter.StartsWith("$SYS", StringComparison.Ordinal) || topicFilter.StartsWith("public/", StringComparison.Ordinal);
    }
}
