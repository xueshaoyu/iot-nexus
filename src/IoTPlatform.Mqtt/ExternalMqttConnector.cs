using System.Security.Authentication;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;

namespace IoTPlatform.Mqtt;

/// <summary>
/// 外部 MQTT 客户端。NewLife.MQTT 的 WSS 在部分 CDN（如 Cloudflare）上会握手挂起；
/// 这里用 MQTTnet（BCL ClientWebSocket）连接 tcp/ssl/ws/wss。
/// </summary>
public static class ExternalMqttConnector
{
    static readonly MqttFactory Factory = new();

    public static IMqttClient CreateClient() => Factory.CreateMqttClient();

    public static MqttClientOptions BuildOptions(ExternalMqttOptions broker, String? clientId = null, TimeSpan? timeout = null)
    {
        var endpoint = MqttBrokerEndpoint.From(broker);
        if (String.IsNullOrEmpty(endpoint.Server) || String.IsNullOrEmpty(endpoint.Host))
            throw new InvalidOperationException("Server is required");

        var id = String.IsNullOrWhiteSpace(clientId)
            ? (String.IsNullOrWhiteSpace(broker.ClientId) ? $"newlife-iot-{Environment.MachineName}" : broker.ClientId)
            : clientId;

        var builder = new MqttClientOptionsBuilder()
            .WithClientId(id)
            .WithCleanSession()
            .WithKeepAlivePeriod(TimeSpan.FromSeconds(Math.Max(5, broker.KeepAlive)))
            .WithTimeout(timeout ?? TimeSpan.FromSeconds(endpoint.IsWebSocket ? 10 : 5));

        if (!String.IsNullOrEmpty(broker.UserName))
            builder.WithCredentials(broker.UserName, broker.Password ?? "");

        if (endpoint.IsWebSocket)
            builder.WithWebSocketServer(o => o.WithUri(endpoint.Server));
        else
            builder.WithTcpServer(endpoint.Host, endpoint.Port);

        if (endpoint.UseTls)
        {
            builder.WithTlsOptions(o =>
            {
                o.UseTls();
                o.WithSslProtocols(SslProtocols.Tls12 | SslProtocols.Tls13);
            });
        }

        return builder.Build();
    }

    public static MqttQualityOfServiceLevel ToQos(Int32 qos) =>
        qos <= 0 ? MqttQualityOfServiceLevel.AtMostOnce : MqttQualityOfServiceLevel.AtLeastOnce;

    public static MqttQualityOfServiceLevel ToQos(NewLife.MQTT.Messaging.QualityOfService qos) =>
        qos == NewLife.MQTT.Messaging.QualityOfService.AtMostOnce
            ? MqttQualityOfServiceLevel.AtMostOnce
            : MqttQualityOfServiceLevel.AtLeastOnce;
}
