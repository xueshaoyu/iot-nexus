using IoTPlatform.Data;
using IoTPlatform.Protocols;
using IoTPlatform.Services;
using NewLife;
using NewLife.Data;
using NewLife.MQTT;
using NewLife.MQTT.Handlers;
using NewLife.MQTT.Messaging;

namespace IoTPlatform.Mqtt;

/// <summary>内置服务端的 MQTT 会话处理器。每个连接一个实例，在 NewLife.MQTT 标准处理之上接入设备在线与数据上报</summary>
/// <remarks>
/// 连接成功 → 设备上线；PINGREQ → 心跳；发布到设备上行主题 → 交给 <see cref="MqttUplinkRouter"/>；
/// 连接关闭 → 设备下线（仅当该会话仍是设备当前会话）。
/// 消息仍按 MQTT 语义转发给订阅者，因此管理账号可以订阅 iot/# 实时观察设备数据。
/// </remarks>
public class IoTMqttHandler(DeviceMqttAuthenticator authenticator, DeviceOnlineService onlineService, MqttUplinkRouter router, MqttServerHost server) : MqttHandler
{
    private const String Protocol = AccessProtocols.Mqtt;

    private MqttIdentity? _identity;
    private String? _clientId;
    private String? _sessionId;
    private String? _ip;

    protected override MqttMessage? OnConnect(ConnectMessage message)
    {
        var result = base.OnConnect(message);
        if (result is ConnAck { ReturnCode: ConnectReturnCode.Accepted })
        {
            _clientId = message.ClientId;
            _identity = authenticator.GetIdentity(message.ClientId);
            _sessionId = Session?.ID.ToString();
            _ip = Session?.Remote?.Host;

            var device = GetDevice();
            if (device != null) onlineService.SetOnline(device, Protocol, _sessionId, _clientId, _ip);
        }

        return result;
    }

    protected override PingResponse? OnPing(PingRequest message)
    {
        var device = GetDevice();
        if (device != null) onlineService.Touch(device, Protocol, _sessionId, _clientId, _ip);

        return base.OnPing(message);
    }

    protected override MqttIdMessage? OnPublish(PublishMessage message)
    {
        var result = base.OnPublish(message);

        var device = GetDevice();
        if (device != null && BelongsToDevice(message.Topic))
        {
            var uplink = router.Handle(message.Topic, message.Payload?.ReadBytes() ?? [], new MqttUplinkContext(device, _sessionId, _clientId, _ip, MqttBrokerIds.Embedded, Topics.Root));
            if (uplink.ReplyTopic != null && uplink.ReplyPayload != null)
                _ = server.PublishAsync(uplink.ReplyTopic, uplink.ReplyPayload, QualityOfService.AtMostOnce);
        }

        return result;
    }

    public override void Close(String reason)
    {
        base.Close(reason);

        var device = GetDevice();
        if (device != null) onlineService.SetOffline(device, $"MQTT disconnected: {reason}", _sessionId);
    }

    private Device? GetDevice() => _identity is { IsDevice: true } ? Device.FindById(_identity.DeviceId) : null;

    /// <summary>只处理本设备产品下的平台主题和 Alink 主题</summary>
    private Boolean BelongsToDevice(String? topic)
    {
        if (_identity == null || topic.IsNullOrEmpty()) return false;

        if (Topics.TryParse(topic, out var pk, out _, out _) && pk == _identity.ProductKey) return true;
        return AlinkTopics.TryParse(topic, out pk, out _, out _, out _) && pk == _identity.ProductKey;
    }
}
