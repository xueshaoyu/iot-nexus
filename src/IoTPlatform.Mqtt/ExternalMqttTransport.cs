using System.Collections.Concurrent;
using IoTPlatform.Data;
using IoTPlatform.Protocols;
using IoTPlatform.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Client;
using NewLife;
using NewLife.Log;
using NewLife.MQTT.Messaging;

namespace IoTPlatform.Mqtt;

/// <summary>外部 MQTT 会话快照</summary>
public record ExternalBrokerSessionStatus(
    String Id,
    Boolean Running,
    String Server,
    Int32 Port,
    String? ClientId,
    String? LastError,
    Int64 MessagesReceived,
    Int64 MessagesSent,
    Int64 Connections,
    DateTime ConnectedAt,
    IReadOnlyDictionary<String, Int32> Subscriptions);

/// <summary>外部 MQTT 服务器传输：为每个在用的 Brokers[] 配置维持一条客户端连接</summary>
/// <remarks>
/// 启动时按设备/产品引用与平台默认解析需要连接的服务器；之后每 15 秒对账，新分配的服务器会自动连上。
/// 每条连接订阅该服务器主题根下的设备上行主题，交给 <see cref="MqttUplinkRouter"/>；下行按设备发布到对应连接。
/// </remarks>
public class ExternalMqttTransport(IOptions<IoTOptions> options, MqttUplinkRouter router, MqttBrokerCatalog catalog) : IHostedService
{
    public const String SessionIdPrefix = "mqtt-external";

    private readonly ConcurrentDictionary<String, Session> _sessions = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? _cts;
    private Task? _reconcile;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public Boolean IsReady => _sessions.Values.Any(e => e.IsReady);

    public Boolean IsReadyFor(String? brokerId)
    {
        var id = catalog.ResolveId(brokerId);
        return _sessions.TryGetValue(id, out var s) && s.IsReady;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = new CancellationTokenSource();
        _ = ReconcileAsync(CancellationToken.None);
        _reconcile = Task.Run(() => ReconcileLoopAsync(_cts.Token), CancellationToken.None);

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_cts == null) return;

        _cts.Cancel();
        try { if (_reconcile != null) await _reconcile; } catch { }
        foreach (var s in _sessions.Values) await s.StopAsync();
        _sessions.Clear();
        _cts.Dispose();
        _cts = null;
    }

    private async Task ReconcileLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try { await Task.Delay(TimeSpan.FromSeconds(15), token); }
            catch (OperationCanceledException) { break; }
            await ReconcileAsync(token);
        }
    }

    /// <summary>立刻按当前配置对账：指纹变化则重连，未启用或不在用的会话关掉</summary>
    public Task ReconcileNowAsync(CancellationToken cancellationToken = default) => ReconcileAsync(cancellationToken);

    private async Task ReconcileAsync(CancellationToken token)
    {
        await _gate.WaitAsync(token);
        try
        {
            var mqtt = options.Value.Mqtt;
            var required = mqtt.Enable
                ? catalog.RequiredIds().Where(e => !MqttBrokerIds.IsEmbedded(e)).ToHashSet(StringComparer.OrdinalIgnoreCase)
                : new HashSet<String>(StringComparer.OrdinalIgnoreCase);

            foreach (var kv in _sessions.ToArray())
            {
                var profile = mqtt.FindBroker(kv.Key);
                var fingerprint = profile == null ? null : MqttSettingsMerger.Fingerprint(profile);
                var keep = required.Contains(kv.Key) && profile is { Enable: true } && kv.Value.Fingerprint == fingerprint;
                if (keep) continue;

                await kv.Value.StopAsync();
                _sessions.TryRemove(kv.Key, out _);
            }

            if (!mqtt.Enable) return;

            foreach (var id in required)
            {
                if (_sessions.ContainsKey(id)) continue;

                var profile = mqtt.FindBroker(id);
                if (profile == null || !profile.Enable)
                {
                    var missing = new Session(id, null, router, catalog);
                    missing.SetError($"Broker '{id}' is not defined in IoT:Mqtt:Brokers");
                    _sessions.TryAdd(id, missing);
                    XTrace.WriteLine("外部 MQTT：未找到服务器 {0}，可选：{1}", id, String.Join(", ", mqtt.GetBrokers().Select(e => e.Id)));
                    continue;
                }

                var session = new Session(profile.Id!, profile, router, catalog);
                if (_sessions.TryAdd(profile.Id!, session))
                {
                    XTrace.WriteLine("外部 MQTT：连接服务器 {0}（{1}）{2}", profile.Id, profile.Name ?? "-", profile.EffectiveServer);
                    session.Start();
                }
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public Task<Boolean> PublishAsync(String? brokerId, String topic, Byte[] payload, QualityOfService qos = QualityOfService.AtLeastOnce, Boolean retain = false, CancellationToken cancellationToken = default)
    {
        var id = catalog.ResolveId(brokerId);
        if (!_sessions.TryGetValue(id, out var session)) return Task.FromResult(false);

        return session.PublishAsync(topic, payload, qos, retain, cancellationToken);
    }

    public MqttStatus GetStatus(String? brokerId = null)
    {
        var id = catalog.ResolveId(brokerId);
        var snap = GetSnapshot(id);
        var profile = options.Value.Mqtt.FindBroker(id);
        var server = snap?.Server ?? profile?.EffectiveServer ?? "";
        var port = snap?.Port ?? (profile == null ? 1883 : MqttBrokerEndpoint.From(profile).Port);
        var error = snap?.LastError ?? (profile == null ? $"Broker '{id}' is not defined in IoT:Mqtt:Brokers" : null);

        return new MqttStatus(
            MqttModes.External,
            snap?.Running == true,
            server,
            port,
            "NewLife.MQTT client",
            snap is { Running: true, ConnectedAt: var at } && at != default ? (Int64)(DateTime.Now - at).TotalSeconds : 0,
            0,
            snap?.Connections ?? 0,
            snap?.MessagesReceived ?? 0,
            snap?.MessagesSent ?? 0,
            0,
            0,
            [],
            snap?.Subscriptions ?? new Dictionary<String, Int32>(),
            snap?.ClientId ?? profile?.ClientId,
            error,
            id,
            profile?.Name);
    }

    public ExternalBrokerSessionStatus? GetSnapshot(String id) =>
        _sessions.TryGetValue(id, out var s) ? s.Snapshot() : null;

    public IReadOnlyList<ExternalBrokerSessionStatus> GetSnapshots() =>
        _sessions.Values.Select(e => e.Snapshot()).ToList();

    private sealed class Session(String id, ExternalMqttOptions? settings, MqttUplinkRouter router, MqttBrokerCatalog catalog)
    {
        public String? Fingerprint { get; } = settings == null ? null : MqttSettingsMerger.Fingerprint(settings);

        private CancellationTokenSource? _cts;
        private Task? _loop;
        private IMqttClient? _client;
        private DateTime _connectedAt;
        private Int64 _received;
        private Int64 _sent;
        private Int64 _connections;
        private String? _lastError;
        private readonly ConcurrentDictionary<String, Int32> _subscriptions = new();

        public Boolean IsReady => _client?.IsConnected == true;

        public void SetError(String error) => _lastError = error;

        public void Start()
        {
            if (settings == null || _cts != null) return;
            _cts = new CancellationTokenSource();
            _loop = Task.Run(() => RunAsync(_cts.Token), CancellationToken.None);
        }

        public async Task StopAsync()
        {
            if (_cts == null) return;
            _cts.Cancel();
            try { if (_loop != null) await _loop; } catch { }
            _cts.Dispose();
            _cts = null;
        }

        private async Task RunAsync(CancellationToken token)
        {
            var s = settings!;
            while (!token.IsCancellationRequested)
            {
                var endpoint = MqttBrokerEndpoint.From(s);
                var clientId = s.ClientId.IsNullOrEmpty() ? $"newlife-iot-{id}-{Environment.MachineName}" : s.ClientId;
                var client = ExternalMqttConnector.CreateClient();
                client.ApplicationMessageReceivedAsync += OnReceivedAsync;

                try
                {
                    var options = ExternalMqttConnector.BuildOptions(s, clientId);
                    var ack = await client.ConnectAsync(options, token);
                    if (ack.ResultCode != MqttClientConnectResultCode.Success)
                        throw new InvalidOperationException(ack.ResultCode.ToString());

                    var qos = ExternalMqttConnector.ToQos(s.QoS);
                    var root = catalog.TopicRoot(id);
                    var filters = Topics.UplinkFilters(root).Concat(AlinkTopics.UplinkFilters()).ToArray();
                    var subscribe = new MqttClientSubscribeOptionsBuilder();
                    foreach (var filter in filters) subscribe.WithTopicFilter(filter, qos);
                    await client.SubscribeAsync(subscribe.Build(), token);

                    _subscriptions.Clear();
                    foreach (var f in filters) _subscriptions[f] = 1;
                    _client = client;
                    _connectedAt = DateTime.Now;
                    _lastError = null;
                    Interlocked.Increment(ref _connections);
                    XTrace.WriteLine("已连接外部 MQTT 服务器 {0}（{1} ClientId={2}），订阅 {3}", id, endpoint.Server, clientId, String.Join(" ", filters));

                    while (!token.IsCancellationRequested && client.IsConnected) await Task.Delay(1000, token);
                    if (!token.IsCancellationRequested) _lastError = "Connection lost";
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                }
                catch (Exception ex)
                {
                    _lastError = ex.Message;
                    XTrace.WriteLine("连接外部 MQTT 服务器 {0}（{1}）失败：{2}", id, endpoint.Server, ex.Message);
                }
                finally
                {
                    _client = null;
                    _subscriptions.Clear();
                    client.ApplicationMessageReceivedAsync -= OnReceivedAsync;
                    try { if (client.IsConnected) await client.DisconnectAsync(cancellationToken: CancellationToken.None); } catch { }
                    client.Dispose();
                }

                try { await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, s.ReconnectDelay)), token); }
                catch (OperationCanceledException) { break; }
            }
        }

        private Task OnReceivedAsync(MqttApplicationMessageReceivedEventArgs e)
        {
            var msg = e.ApplicationMessage;
            if (msg?.Topic == null) return Task.CompletedTask;

            Interlocked.Increment(ref _received);
            var root = catalog.TopicRoot(id);
            var payload = msg.PayloadSegment.Count == 0 ? [] : msg.PayloadSegment.ToArray();
            var result = router.Handle(msg.Topic, payload, new MqttUplinkContext(null, $"{SessionIdPrefix}:{id}", settings?.ClientId, null, id, root));
            if (result.ReplyTopic != null && result.ReplyPayload != null)
                _ = PublishAsync(result.ReplyTopic, result.ReplyPayload, QualityOfService.AtMostOnce, false, CancellationToken.None);
            return Task.CompletedTask;
        }

        public async Task<Boolean> PublishAsync(String topic, Byte[] payload, QualityOfService qos, Boolean retain, CancellationToken cancellationToken)
        {
            var client = _client;
            if (client == null || !client.IsConnected) return false;

            try
            {
                var message = new MqttApplicationMessageBuilder()
                    .WithTopic(topic)
                    .WithPayload(payload)
                    .WithQualityOfServiceLevel(ExternalMqttConnector.ToQos(qos))
                    .WithRetainFlag(retain)
                    .Build();
                await client.PublishAsync(message, cancellationToken);
                Interlocked.Increment(ref _sent);
                return true;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _lastError = ex.Message;
                return false;
            }
        }

        public ExternalBrokerSessionStatus Snapshot()
        {
            var endpoint = settings == null ? default : MqttBrokerEndpoint.From(settings);
            var server = settings == null ? "" : endpoint.Server;
            var port = settings == null ? 1883 : endpoint.Port;
            return new ExternalBrokerSessionStatus(
                id,
                IsReady,
                server,
                port,
                settings?.ClientId,
                _lastError,
                Interlocked.Read(ref _received),
                Interlocked.Read(ref _sent),
                Interlocked.Read(ref _connections),
                _connectedAt,
                new Dictionary<String, Int32>(_subscriptions));
        }
    }
}
