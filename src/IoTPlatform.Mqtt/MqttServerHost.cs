using IoTPlatform.Data;
using IoTPlatform.Protocols;
using IoTPlatform.Protocols.Codecs;
using IoTPlatform.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NewLife;
using NewLife.Data;
using NewLife.Log;
using NewLife.MQTT;
using NewLife.MQTT.Handlers;
using NewLife.MQTT.Messaging;

namespace IoTPlatform.Mqtt;

/// <summary>内置 MQTT 服务端（NewLife.MQTT）。Mode=Embedded，或有设备/产品选用 embedded 时启动</summary>
public class MqttServerHost(IServiceProvider serviceProvider, IOptions<IoTOptions> options, DeviceMqttAuthenticator authenticator, MqttBrokerCatalog catalog) : IHostedService
{
    public MqttServer? Server { get; private set; }

    public MqttExchange Exchange { get; } = new();

    public String Mode => MqttModes.Embedded;

    public Boolean IsReady => Server != null;

    public Boolean CanInspectSubscribers => true;

    private CancellationTokenSource? _cts;
    private Task? _reconcile;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!options.Value.Mqtt.Enable) return Task.CompletedTask;

        TryStart();
        _cts = new CancellationTokenSource();
        _reconcile = Task.Run(() => ReconcileLoopAsync(_cts.Token), CancellationToken.None);

        return Task.CompletedTask;
    }

    private async Task ReconcileLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try { await Task.Delay(TimeSpan.FromSeconds(15), token); }
            catch (OperationCanceledException) { break; }
            TryStart();
        }
    }

    private void TryStart()
    {
        if (Server != null || !options.Value.Mqtt.Enable) return;
        if (!catalog.RequiredIds().Contains(MqttBrokerIds.Embedded)) return;

        var opt = options.Value.Mqtt;
        var server = new MqttServer
        {
            Port = opt.Port,
            ServiceProvider = serviceProvider,
            Authenticator = authenticator,
            Exchange = Exchange,
            Log = XTrace.Log,
        };
        if (opt.ManagementPort > 0) server.ManagementPort = opt.ManagementPort;

        server.Start();
        Server = server;
        XTrace.WriteLine("MQTT 服务端已启动 tcp://0.0.0.0:{0} (WebSocket ws://0.0.0.0:{0}/mqtt)", opt.Port);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_cts != null)
        {
            _cts.Cancel();
            try { if (_reconcile != null) await _reconcile; } catch { }
            _cts.Dispose();
            _cts = null;
        }

        Server?.Stop("Platform stopping");
        Server = null;
    }

    public Task<Boolean> PublishAsync(String topic, Byte[] payload, QualityOfService qos = QualityOfService.AtLeastOnce, Boolean retain = false, CancellationToken cancellationToken = default)
    {
        if (Server == null) return Task.FromResult(false);

        Exchange.Publish(new PublishMessage
        {
            Topic = topic,
            Payload = new ArrayPacket(payload),
            QoS = qos,
            Retain = retain,
        });

        return Task.FromResult(true);
    }

    public Boolean HasSubscriber(String topic) =>
        Exchange.GetSubscriberCount(topic) > 0 || Exchange.GetTopicSubscriptions().Keys.Any(f => MqttTopicFilter.IsMatch(topic, f));

    public Boolean Disconnect(Device device, String reason)
    {
        if (Server == null) return false;

        var found = false;
        foreach (var clientId in Exchange.GetConnectedClients())
        {
            if (authenticator.GetIdentity(clientId)?.DeviceId == device.Id)
                found |= Exchange.DisconnectClient(clientId, reason);
        }

        return found;
    }

    public MqttStatus GetStatus()
    {
        var stats = Exchange.Stats;
        IReadOnlyList<String> clients = Server == null ? [] : Exchange.GetConnectedClients();
        IReadOnlyDictionary<String, Int32> subscriptions = Server == null ? new Dictionary<String, Int32>() : Exchange.GetTopicSubscriptions();
        var port = options.Value.Mqtt.Port;

        return new MqttStatus(
            Mode,
            Server != null,
            $"tcp://0.0.0.0:{port}",
            port,
            stats.Version,
            Server == null ? 0 : stats.Uptime,
            clients.Count,
            stats.TotalConnections,
            stats.MessagesReceived,
            stats.MessagesSent,
            stats.BytesReceived,
            stats.BytesSent,
            clients,
            subscriptions,
            null,
            Server == null ? "Embedded broker is not running" : null,
            MqttBrokerIds.Embedded,
            "Embedded");
    }
}

/// <summary>MQTT 下行通道：按产品编解码器编码后，发布到该设备所分配的 MQTT 服务器</summary>
public class MqttCommandChannel(IMqttTransport transport, PayloadCodecFactory codecs, MqttBrokerCatalog catalog) : IDeviceCommandChannel
{
    public String Name => "MQTT";

    public Boolean CanHandle(Product product) => product.Protocol == AccessProtocols.Mqtt;

    public Task<CommandResult> InvokeServiceAsync(Device device, Product product, ServiceRecord record, IDictionary<String, Object?> args, CancellationToken cancellationToken)
    {
        var alinkTopic = AlinkTopics.Service(product.Code, device.Code, record.Name!);
        var legacy = Topics.ServiceInvoke;
        var (topic, alink) = ResolveTopic(device, product, legacy, alinkTopic);
        var codec = alink ? codecs.Get("Alink") : codecs.Get(product.Codec);
        return SendAsync(device, product, topic, codec.EncodeServiceInvoke(record, record.Name!, args), cancellationToken);
    }

    public Task<CommandResult> SetPropertyAsync(Device device, Product product, ServiceRecord record, IDictionary<String, Object?> values, CancellationToken cancellationToken)
    {
        var alinkTopic = AlinkTopics.PropertySet(product.Code, device.Code);
        var (topic, alink) = ResolveTopic(device, product, Topics.PropertySet, alinkTopic);
        var codec = alink ? codecs.Get("Alink") : codecs.Get(product.Codec);
        return SendAsync(device, product, topic, codec.EncodePropertySet(record, values), cancellationToken);
    }

    /// <summary>
    /// 设备订的是哪套主题就发到哪套。内置服务器能看到订阅；外部服务器按产品 Codec 选择，
    /// Codec=Alink 走 /sys/...，其余仍走 iot/...，避免已接入设备被换到新主题。
    /// </summary>
    private (String Topic, Boolean Alink) ResolveTopic(Device device, Product product, String legacyKind, String alinkTopic)
    {
        var brokerId = catalog.Resolve(device, product);
        var legacy = Topics.Build(product.Code, device.Code, legacyKind, catalog.TopicRoot(brokerId));
        if (!catalog.IsEmbedded(brokerId) || !transport.CanInspectSubscribers)
            return product.Codec.EqualIgnoreCase("Alink") ? (alinkTopic, true) : (legacy, false);

        var hasAlink = transport.HasSubscriber(brokerId, alinkTopic);
        var hasLegacy = transport.HasSubscriber(brokerId, legacy);
        if (hasAlink && !hasLegacy) return (alinkTopic, true);
        if (hasLegacy && !hasAlink) return (legacy, false);

        return product.Codec.EqualIgnoreCase("Alink") ? (alinkTopic, true) : (legacy, false);
    }

    private async Task<CommandResult> SendAsync(Device device, Product product, String topic, Byte[] payload, CancellationToken cancellationToken)
    {
        var brokerId = catalog.Resolve(device, product);

        if (!transport.IsReadyFor(brokerId))
            return CommandResult.Failed(catalog.IsEmbedded(brokerId) ? "MQTT server is not running" : "External MQTT broker is not connected");
        if (catalog.IsEmbedded(brokerId) && !transport.HasSubscriber(brokerId, topic))
            return CommandResult.Failed($"Device is not subscribed to {topic}");

        var profile = catalog.Mqtt.FindBroker(brokerId);
        var qos = profile?.QoS <= 0 ? QualityOfService.AtMostOnce : QualityOfService.AtLeastOnce;
        return await transport.PublishAsync(brokerId, topic, payload, qos, false, cancellationToken)
            ? CommandResult.Sent()
            : CommandResult.Failed("MQTT publish failed");
    }
}

/// <summary>通过当前 MQTT 传输断开设备连接（外部服务器模式不支持踢下线）</summary>
public class MqttSessionManager(IMqttTransport transport) : IDeviceSessionManager
{
    public Boolean Disconnect(Device device, String reason) => transport.Disconnect(device, reason);
}
