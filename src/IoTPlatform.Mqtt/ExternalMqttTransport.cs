using System.Collections.Concurrent;
using IoTPlatform.Data;
using IoTPlatform.Protocols;
using IoTPlatform.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NewLife;
using NewLife.Data;
using NewLife.Log;
using NewLife.MQTT;
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

    public Boolean IsReady => _sessions.Values.Any(e => e.IsReady);

    public Boolean IsReadyFor(String? brokerId)
    {
        var id = catalog.ResolveId(brokerId);
        return _sessions.TryGetValue(id, out var s) && s.IsReady;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!options.Value.Mqtt.Enable) return Task.CompletedTask;

        _cts = new CancellationTokenSource();
        Reconcile();
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
            Reconcile();
        }
    }

    private void Reconcile()
    {
        var mqtt = options.Value.Mqtt;
        if (!mqtt.Enable) return;

        foreach (var id in catalog.RequiredIds().Where(e => !MqttBrokerIds.IsEmbedded(e)))
        {
            if (_sessions.ContainsKey(id)) continue;

            var profile = mqtt.FindBroker(id);
            if (profile == null)
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
                XTrace.WriteLine("外部 MQTT：连接服务器 {0}（{1}）{2}", profile.Id, profile.Name ?? "-", profile.Server);
                session.Start();
            }
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
        var server = snap?.Server ?? profile?.Server ?? "";
        var port = snap?.Port ?? (Uri.TryCreate(server, UriKind.Absolute, out var uri) && uri.Port > 0 ? uri.Port : 1883);
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
        private CancellationTokenSource? _cts;
        private Task? _loop;
        private MqttClient? _client;
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
                var client = new MqttClient
                {
                    Server = s.Server,
                    ClientId = s.ClientId.IsNullOrEmpty() ? $"newlife-iot-{id}-{Environment.MachineName}" : s.ClientId,
                    UserName = s.UserName,
                    Password = s.Password,
                    SslProtocol = s.UseTls ? System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13 : System.Security.Authentication.SslProtocols.None,
                    KeepAlive = Math.Max(5, s.KeepAlive),
                    CleanSession = true,
                    Reconnect = false,
                    Log = Logger.Null,
                };
                client.Received += OnReceived;

                try
                {
                    await client.ConnectAsync(token);

                    var qos = s.QoS <= 0 ? QualityOfService.AtMostOnce : QualityOfService.AtLeastOnce;
                    var root = catalog.TopicRoot(id);
                    var filters = Topics.UplinkFilters(root).Concat(AlinkTopics.UplinkFilters()).ToArray();
                    await client.SubscribeAsync(filters, qos, token);

                    _subscriptions.Clear();
                    foreach (var f in filters) _subscriptions[f] = 1;
                    _client = client;
                    _connectedAt = DateTime.Now;
                    _lastError = null;
                    Interlocked.Increment(ref _connections);
                    XTrace.WriteLine("已连接外部 MQTT 服务器 {0}（{1} ClientId={2}），订阅 {3}", id, s.Server, client.ClientId, String.Join(" ", filters));

                    while (!token.IsCancellationRequested && client.IsConnected) await Task.Delay(1000, token);
                    if (!token.IsCancellationRequested) _lastError = "Connection lost";
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                }
                catch (Exception ex)
                {
                    _lastError = ex.Message;
                    XTrace.WriteLine("连接外部 MQTT 服务器 {0}（{1}）失败：{2}", id, s.Server, ex.Message);
                }
                finally
                {
                    _client = null;
                    _subscriptions.Clear();
                    client.Received -= OnReceived;
                    try { if (client.IsConnected) await client.DisconnectAsync(CancellationToken.None); } catch { }
                    client.Dispose();
                }

                try { await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, s.ReconnectDelay)), token); }
                catch (OperationCanceledException) { break; }
            }
        }

        private void OnReceived(Object? sender, NewLife.EventArgs<PublishMessage> e)
        {
            var msg = e.Arg;
            if (msg?.Topic == null) return;

            Interlocked.Increment(ref _received);
            var root = catalog.TopicRoot(id);
            var result = router.Handle(msg.Topic, msg.Payload?.ReadBytes() ?? [], new MqttUplinkContext(null, $"{SessionIdPrefix}:{id}", settings?.ClientId, null, id, root));
            if (result.ReplyTopic != null && result.ReplyPayload != null)
                _ = PublishAsync(result.ReplyTopic, result.ReplyPayload, QualityOfService.AtMostOnce, false, CancellationToken.None);
        }

        public async Task<Boolean> PublishAsync(String topic, Byte[] payload, QualityOfService qos, Boolean retain, CancellationToken cancellationToken)
        {
            var client = _client;
            if (client == null || !client.IsConnected) return false;

            try
            {
                await client.PublishAsync(new PublishMessage { Topic = topic, Payload = new ArrayPacket(payload), QoS = qos, Retain = retain }, cancellationToken);
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
            var server = settings?.Server ?? "";
            var port = Uri.TryCreate(server, UriKind.Absolute, out var uri) && uri.Port > 0 ? uri.Port : 1883;
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
