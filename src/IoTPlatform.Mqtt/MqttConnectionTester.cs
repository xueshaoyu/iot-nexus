using System.Diagnostics;
using NewLife;
using NewLife.MQTT;

namespace IoTPlatform.Mqtt;

public record MqttConnectionTestResult(Boolean Ok, String Server, String? ClientId, String? Error, Int32 ElapsedMs);

/// <summary>用短超时连一次外部 MQTT 服务器，用于界面“测试连接”</summary>
public class MqttConnectionTester
{
    public async Task<MqttConnectionTestResult> TestAsync(ExternalMqttOptions broker, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        var server = broker.Server?.Trim() ?? "";
        if (String.IsNullOrEmpty(server))
            return new(false, server, broker.ClientId, "Server is required", 0);

        timeout ??= TimeSpan.FromSeconds(5);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout.Value);
        var sw = Stopwatch.StartNew();

        var client = new MqttClient
        {
            Server = server,
            ClientId = broker.ClientId.IsNullOrEmpty() ? $"newlife-iot-test-{Environment.MachineName}" : broker.ClientId,
            UserName = broker.UserName,
            Password = broker.Password,
            SslProtocol = broker.UseTls ? System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13 : System.Security.Authentication.SslProtocols.None,
            KeepAlive = Math.Max(5, broker.KeepAlive),
            CleanSession = true,
            Reconnect = false,
            Log = NewLife.Log.Logger.Null,
        };

        try
        {
            await client.ConnectAsync(cts.Token);
            try { await client.DisconnectAsync(CancellationToken.None); } catch { }
            return new(true, server, client.ClientId, null, (Int32)sw.ElapsedMilliseconds);
        }
        catch (OperationCanceledException)
        {
            return new(false, server, client.ClientId, cts.IsCancellationRequested && !cancellationToken.IsCancellationRequested ? "Timed out" : "Cancelled", (Int32)sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            return new(false, server, client.ClientId, ex.Message, (Int32)sw.ElapsedMilliseconds);
        }
        finally
        {
            client.Dispose();
        }
    }
}
