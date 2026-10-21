using System.Diagnostics;
using MQTTnet.Client;

namespace IoTPlatform.Mqtt;

public record MqttConnectionTestResult(Boolean Ok, String Server, String? ClientId, String? Error, Int32 ElapsedMs);

/// <summary>用短超时连一次外部 MQTT 服务器，用于界面“测试连接”</summary>
public class MqttConnectionTester
{
    public async Task<MqttConnectionTestResult> TestAsync(ExternalMqttOptions broker, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        var endpoint = MqttBrokerEndpoint.From(broker);
        var server = endpoint.Server;
        if (String.IsNullOrEmpty(server))
            return new(false, server, broker.ClientId, "Server is required", 0);

        timeout ??= TimeSpan.FromSeconds(endpoint.IsWebSocket ? 10 : 5);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout.Value);
        var sw = Stopwatch.StartNew();
        var clientId = String.IsNullOrWhiteSpace(broker.ClientId) ? $"newlife-iot-test-{Environment.MachineName}" : broker.ClientId;

        using var client = ExternalMqttConnector.CreateClient();
        try
        {
            var options = ExternalMqttConnector.BuildOptions(broker, clientId, timeout);
            var ack = await client.ConnectAsync(options, cts.Token);
            try { await client.DisconnectAsync(cancellationToken: CancellationToken.None); } catch { }
            if (ack.ResultCode != MqttClientConnectResultCode.Success)
                return new(false, server, clientId, ack.ResultCode.ToString(), (Int32)sw.ElapsedMilliseconds);
            return new(true, server, clientId, null, (Int32)sw.ElapsedMilliseconds);
        }
        catch (OperationCanceledException)
        {
            return new(false, server, clientId, cts.IsCancellationRequested && !cancellationToken.IsCancellationRequested ? "Timed out" : "Cancelled", (Int32)sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            return new(false, server, clientId, ex.Message, (Int32)sw.ElapsedMilliseconds);
        }
    }
}
