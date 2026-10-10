using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using NewLife.Data;
using NewLife.Log;
using NewLife.MQTT;
using NewLife.MQTT.Messaging;

namespace IoTPlatform.Simulator;

/// <summary>MQTT 设备模拟器。每个设备一个 NewLife.MQTT 客户端，按平台主题规范上报并响应下行指令</summary>
/// <remarks>
/// 连接时设置遗嘱 {root}/{pk}/{dc}/status = offline（保留），连接成功后发布 online（保留），
/// 平台连接外部 MQTT 服务器时据此判定上下线；内置服务端同样兼容。
/// </remarks>
public sealed class MqttDeviceSimulator(String server, IEnumerable<DeviceProfile> profiles, Int32 interval = 3000, ILog? log = null, String topicRoot = "iot", Boolean useTls = false) : IAsyncDisposable
{
    private readonly List<DeviceProfile> _profiles = profiles.ToList();
    private readonly CancellationTokenSource _cts = new();
    private readonly List<Task> _tasks = [];

    public Int32 Count => _profiles.Count;

    public void Start()
    {
        foreach (var profile in _profiles) _tasks.Add(Task.Run(() => RunDeviceAsync(profile, _cts.Token)));
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        try { await Task.WhenAll(_tasks); } catch { }
        _cts.Dispose();
    }

    private async Task RunDeviceAsync(DeviceProfile profile, CancellationToken token)
    {
        var device = SimulatedDevice.Create(profile);
        var prefix = $"{topicRoot.Trim('/')}/{profile.ProductKey}/{profile.DeviceCode}/";

        // 错开启动，避免所有设备同一时刻上报
        try { await Task.Delay(Random.Shared.Next(200, 1500), token); } catch (OperationCanceledException) { return; }

        while (!token.IsCancellationRequested)
        {
            var reboot = false;
            var refused = false;
            var client = new MqttClient
            {
                Server = server,
                ClientId = profile.DeviceCode,
                UserName = profile.DeviceCode,
                Password = profile.Secret,
                KeepAlive = 30,
                Reconnect = false,
                SslProtocol = useTls ? System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13 : System.Security.Authentication.SslProtocols.None,
                WillTopic = prefix + "status",
                WillMessage = Encoding.UTF8.GetBytes("offline"),
                WillQoS = QualityOfService.AtLeastOnce,
                WillRetain = true,
                Log = Logger.Null,
            };

            try
            {
                var ack = await client.ConnectAsync(token);
                if (ack?.ReturnCode != ConnectReturnCode.Accepted)
                {
                    log?.Info("[Simulator] {0} connect refused: {1}", profile.DeviceCode, ack?.ReturnCode);
                    await Task.Delay(10_000, token);
                    continue;
                }

                async void OnDownlink(PublishMessage msg)
                {
                    try
                    {
                        var kind = msg.Topic[prefix.Length..];
                        var json = JsonNode.Parse(msg.Payload?.ToStr() ?? "{}") as JsonObject;
                        var id = json?["id"]?.GetValue<Int64>() ?? 0;
                        var args = json?["params"] as JsonObject;

                        Object reply;
                        if (kind == "property/set")
                        {
                            var ok = args != null && args.All(kv => device.SetProperty(kv.Key, kv.Value));
                            reply = new { id, status = ok ? 2 : 4, data = ok ? "ok" : "unsupported property" };
                            await Publish(client, prefix + "service/reply", reply);
                            if (ok) await Publish(client, prefix + "property/post", device.NextTelemetry());
                        }
                        else if (kind == "service/invoke")
                        {
                            var name = json?["name"]?.GetValue<String>() ?? "";
                            var (success, output, restart) = device.Invoke(name, args);
                            reply = new { id, status = success ? 2 : 4, data = JsonSerializer.Serialize(output) };
                            await Publish(client, prefix + "service/reply", reply);
                            if (restart) reboot = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        log?.Info("[Simulator] {0} downlink error {1}", profile.DeviceCode, ex.Message);
                    }
                }

                await client.SubscribeAsync(prefix + "service/invoke", OnDownlink, token);
                await client.SubscribeAsync(prefix + "property/set", OnDownlink, token);
                await PublishRaw(client, prefix + "status", "online", retain: true);

                while (!token.IsCancellationRequested && client.IsConnected && !reboot)
                {
                    await Publish(client, prefix + "property/post", device.NextTelemetry());
                    foreach (var e in device.NextEvents()) await Publish(client, prefix + "event/post", e);

                    // 每 100ms 检查一次重启标记，保证 reboot 服务能及时断开
                    for (var waited = 0; waited < interval && !reboot && !token.IsCancellationRequested; waited += 100) await Task.Delay(100, token);
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                refused = ex.Message.Contains("Refused");
                log?.Info("[Simulator] {0} {1}", profile.DeviceCode, refused ? "connection refused by broker (disabled or wrong secret), retrying in 30s" : "error " + ex.Message);
            }
            finally
            {
                // 正常断开不会触发遗嘱，主动发布离线状态
                try { if (client.IsConnected) await PublishRaw(client, prefix + "status", "offline", retain: true); } catch { }
                try { await client.DisconnectAsync(CancellationToken.None); } catch { }
                client.Dispose();
            }

            if (token.IsCancellationRequested) break;

            // 模拟重启或断线后重连
            try { await Task.Delay(refused ? 30_000 : reboot ? 8_000 : 5_000, token); } catch (OperationCanceledException) { break; }
        }
    }

    private static Task PublishRaw(MqttClient client, String topic, String text, Boolean retain) =>
        client.PublishAsync(new PublishMessage
        {
            Topic = topic,
            Payload = new ArrayPacket(Encoding.UTF8.GetBytes(text)),
            QoS = QualityOfService.AtLeastOnce,
            Retain = retain,
        });

    private static Task Publish(MqttClient client, String topic, Object data) =>
        client.PublishAsync(new PublishMessage
        {
            Topic = topic,
            Payload = new ArrayPacket(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(data))),
            QoS = QualityOfService.AtMostOnce,
        });
}
