using IoTPlatform.Data;
using IoTPlatform.Protocols;
using IoTPlatform.Services;
using Microsoft.Extensions.Options;
using NewLife;

namespace IoTPlatform.Web.Services;

/// <summary>设备接入信息</summary>
public record DeviceConnectionInfo(
    String Protocol,
    String? MqttServer,
    String? MqttWebSocket,
    String ClientId,
    String Username,
    String? Password,
    IReadOnlyDictionary<String, String> Topics,
    String? HttpBase,
    String? Driver,
    String? DriverParameter,
    String? BrokerProfileId = null,
    String? BrokerName = null);

/// <summary>生成设备接入说明</summary>
public class ConnectionInfoService(IOptions<IoTOptions> options, MqttBrokerCatalog catalog)
{
    public DeviceConnectionInfo Get(Device device, HttpRequest? request = null, String? host = null)
    {
        var product = device.Product;
        var protocol = product?.Protocol ?? AccessProtocols.Mqtt;
        var mqtt = options.Value.Mqtt;
        host = mqtt.PublicHost.IsNullOrEmpty() ? host ?? request?.Host.Host ?? "127.0.0.1" : mqtt.PublicHost;

        String? mqttServer = null;
        String? mqttWs = null;
        String? brokerId = null;
        String? brokerName = null;
        var topicRoot = Topics.Root;

        if (protocol == AccessProtocols.Mqtt)
        {
            brokerId = catalog.Resolve(device, product);
            var choice = catalog.FindChoice(brokerId);
            brokerName = choice?.Name;
            topicRoot = catalog.TopicRoot(brokerId);

            if (catalog.IsEmbedded(brokerId))
            {
                mqttServer = $"tcp://{host}:{mqtt.Port}";
                mqttWs = $"ws://{host}:{mqtt.Port}/mqtt";
            }
            else
            {
                var profile = mqtt.FindBroker(brokerId);
                mqttServer = profile == null ? "—" : profile.PublicServer.IsNullOrEmpty() ? profile.EffectiveServer : profile.PublicServer;
            }
        }

        var pk = product?.Code ?? "";
        var topics = new Dictionary<String, String>();
        if (protocol == AccessProtocols.Mqtt)
        {
            foreach (var k in Protocols.Topics.Uplinks) topics[$"publish: {k}"] = Protocols.Topics.Build(pk, device.Code, k, topicRoot);
            topics["subscribe"] = Protocols.Topics.DeviceFilter(pk, device.Code, topicRoot);
        }

        var httpBase = request == null ? null : $"{request.Scheme}://{request.Host}/api/device-access/{device.Code}";

        return new DeviceConnectionInfo(
            protocol,
            mqttServer,
            mqttWs,
            device.Code,
            device.Code,
            device.Secret,
            topics,
            protocol == AccessProtocols.Http ? httpBase : null,
            protocol == AccessProtocols.Driver ? product?.DriverName : null,
            protocol == AccessProtocols.Driver ? Protocols.Drivers.DriverHostService.MergeParameters(product?.DriverParameter, device.DriverParameter) : null,
            brokerId,
            brokerName);
    }
}
