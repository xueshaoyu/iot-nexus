using IoTPlatform.Data;
using IoTPlatform.Services;
using Microsoft.Extensions.Options;
using NewLife;
using NewLife.MQTT.Messaging;

namespace IoTPlatform.Mqtt;

/// <summary>按设备解析 MQTT 服务器，并把发布/踢线路由到内置服务端或对应的外部连接</summary>
public class MqttBrokerHub(
    IOptions<IoTOptions> options,
    MqttServerHost embedded,
    ExternalMqttTransport external,
    MqttBrokerCatalog catalog) : IMqttTransport
{
    public String Mode => options.Value.Mqtt.IsExternal ? MqttModes.External : MqttModes.Embedded;

    public Boolean IsReady => IsReadyFor(catalog.PlatformDefaultId);

    public Boolean CanInspectSubscribers => catalog.IsEmbedded(catalog.PlatformDefaultId);

    public Task<Boolean> PublishAsync(String topic, Byte[] payload, QualityOfService qos = QualityOfService.AtLeastOnce, Boolean retain = false, CancellationToken cancellationToken = default) =>
        PublishAsync(catalog.PlatformDefaultId, topic, payload, qos, retain, cancellationToken);

    public Task<Boolean> PublishAsync(String? brokerProfileId, String topic, Byte[] payload, QualityOfService qos = QualityOfService.AtLeastOnce, Boolean retain = false, CancellationToken cancellationToken = default)
    {
        var id = catalog.ResolveId(brokerProfileId);
        return catalog.IsEmbedded(id)
            ? embedded.PublishAsync(topic, payload, qos, retain, cancellationToken)
            : external.PublishAsync(id, topic, payload, qos, retain, cancellationToken);
    }

    public Boolean HasSubscriber(String topic) => HasSubscriber(catalog.PlatformDefaultId, topic);

    public Boolean HasSubscriber(String? brokerProfileId, String topic)
    {
        var id = catalog.ResolveId(brokerProfileId);
        return catalog.IsEmbedded(id) ? embedded.HasSubscriber(topic) : true;
    }

    public Boolean IsReadyFor(String? brokerProfileId)
    {
        var id = catalog.ResolveId(brokerProfileId);
        return catalog.IsEmbedded(id) ? embedded.IsReady : external.IsReadyFor(id);
    }

    public Boolean Disconnect(Device device, String reason) =>
        catalog.IsEmbedded(catalog.Resolve(device)) && embedded.Disconnect(device, reason);

    public String ResolveBrokerId(Device device, Product? product = null) => catalog.Resolve(device, product);

    public String TopicRootFor(String? brokerProfileId) => catalog.TopicRoot(brokerProfileId);

    public MqttStatus GetStatus()
    {
        var id = catalog.PlatformDefaultId;
        if (catalog.IsEmbedded(id))
        {
            var s = embedded.GetStatus();
            return s with { BrokerId = MqttBrokerIds.Embedded, BrokerName = "Embedded" };
        }

        return external.GetStatus(id);
    }

    public IReadOnlyList<MqttBrokerSnapshot> GetBrokers()
    {
        var counts = catalog.CountEnabledDevices();
        var required = catalog.RequiredIds();
        var list = new List<MqttBrokerSnapshot>();

        var emb = embedded.GetStatus();
        list.Add(new MqttBrokerSnapshot(
            MqttBrokerIds.Embedded,
            "Embedded (built-in)",
            MqttModes.Embedded,
            emb.Running,
            required.Contains(MqttBrokerIds.Embedded) || counts.GetValueOrDefault(MqttBrokerIds.Embedded) > 0,
            emb.Server,
            emb.Port,
            catalog.TopicRoot(MqttBrokerIds.Embedded),
            counts.GetValueOrDefault(MqttBrokerIds.Embedded),
            null,
            emb.LastError,
            emb.MessagesReceived,
            emb.MessagesSent,
            emb.TotalConnections));

        var seen = new HashSet<String>(StringComparer.OrdinalIgnoreCase) { MqttBrokerIds.Embedded };
        foreach (var choice in catalog.Choices().Where(e => e.Kind == MqttModes.External))
        {
            seen.Add(choice.Id);
            var snap = external.GetSnapshot(choice.Id);
            list.Add(new MqttBrokerSnapshot(
                choice.Id,
                choice.Name,
                MqttModes.External,
                snap?.Running == true,
                required.Contains(choice.Id) || counts.GetValueOrDefault(choice.Id) > 0,
                choice.Server,
                snap?.Port ?? PortOf(choice.Server),
                choice.TopicRoot,
                counts.GetValueOrDefault(choice.Id),
                snap?.ClientId,
                snap?.LastError,
                snap?.MessagesReceived ?? 0,
                snap?.MessagesSent ?? 0,
                snap?.Connections ?? 0));
        }

        foreach (var id in required.Where(e => !seen.Contains(e)))
        {
            var snap = external.GetSnapshot(id);
            list.Add(new MqttBrokerSnapshot(
                id,
                id,
                MqttModes.External,
                snap?.Running == true,
                true,
                snap?.Server ?? "",
                snap?.Port ?? 0,
                catalog.TopicRoot(id),
                counts.GetValueOrDefault(id),
                snap?.ClientId,
                snap?.LastError ?? $"Broker '{id}' is not defined in IoT:Mqtt:Brokers",
                snap?.MessagesReceived ?? 0,
                snap?.MessagesSent ?? 0,
                snap?.Connections ?? 0));
        }

        return list;
    }

    private static Int32 PortOf(String server) =>
        Uri.TryCreate(server, UriKind.Absolute, out var uri) && uri.Port > 0 ? uri.Port : 1883;
}
