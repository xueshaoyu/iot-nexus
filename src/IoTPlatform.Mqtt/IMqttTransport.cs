using IoTPlatform.Data;
using NewLife.MQTT.Messaging;

namespace IoTPlatform.Mqtt;

/// <summary>MQTT 状态（平台默认服务器，或聚合）</summary>
public record MqttStatus(
    String Mode,
    Boolean Running,
    String Server,
    Int32 Port,
    String? Version,
    Int64 Uptime,
    Int32 ConnectedClients,
    Int64 TotalConnections,
    Int64 MessagesReceived,
    Int64 MessagesSent,
    Int64 BytesReceived,
    Int64 BytesSent,
    IReadOnlyList<String> Clients,
    IReadOnlyDictionary<String, Int32> Subscriptions,
    String? ClientId = null,
    String? LastError = null,
    String? BrokerId = null,
    String? BrokerName = null);

/// <summary>单个 MQTT 服务器运行快照（内置或外部），供协议页与配置接口展示</summary>
public record MqttBrokerSnapshot(
    String Id,
    String Name,
    String Kind,
    Boolean Running,
    Boolean InUse,
    String Server,
    Int32 Port,
    String TopicRoot,
    Int32 DeviceCount,
    String? ClientId,
    String? LastError,
    Int64 MessagesReceived,
    Int64 MessagesSent,
    Int64 Connections);

/// <summary>MQTT 传输。内置服务端与多个外部服务器可同时在线；下行按设备解析到对应服务器</summary>
public interface IMqttTransport
{
    /// <summary>平台默认模式 Embedded / External</summary>
    String Mode { get; }

    /// <summary>平台默认服务器是否可用</summary>
    Boolean IsReady { get; }

    /// <summary>默认服务器能否判断主题是否有订阅者</summary>
    Boolean CanInspectSubscribers { get; }

    Task<Boolean> PublishAsync(String topic, Byte[] payload, QualityOfService qos = QualityOfService.AtLeastOnce, Boolean retain = false, CancellationToken cancellationToken = default);

    /// <summary>发布到指定服务器。brokerProfileId 为空则使用平台默认</summary>
    Task<Boolean> PublishAsync(String? brokerProfileId, String topic, Byte[] payload, QualityOfService qos = QualityOfService.AtLeastOnce, Boolean retain = false, CancellationToken cancellationToken = default);

    Boolean HasSubscriber(String topic);

    Boolean HasSubscriber(String? brokerProfileId, String topic);

    Boolean IsReadyFor(String? brokerProfileId);

    /// <summary>断开设备连接。仅内置服务端可踢下线</summary>
    Boolean Disconnect(Device device, String reason);

    MqttStatus GetStatus();

    IReadOnlyList<MqttBrokerSnapshot> GetBrokers();

    String ResolveBrokerId(Device device, Product? product = null);

    String TopicRootFor(String? brokerProfileId);
}
