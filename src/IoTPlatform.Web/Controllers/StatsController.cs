using IoTPlatform.Data;
using IoTPlatform.Mqtt;
using IoTPlatform.Protocols;
using IoTPlatform.Protocols.Codecs;
using IoTPlatform.Protocols.Drivers;
using IoTPlatform.Services;
using Microsoft.AspNetCore.Mvc;

namespace IoTPlatform.Web.Controllers;

/// <summary>设备统计</summary>
[ApiController]
[Route("api/stats")]
[Tags("Statistics")]
public class StatsController(StatService service) : ControllerBase
{
    /// <summary>平台概览：设备数、在线率、今日消息量、告警等</summary>
    [HttpGet("overview")]
    public StatOverview Overview() => service.GetOverview();

    /// <summary>按产品统计</summary>
    [HttpGet("products")]
    public IList<ProductStat> Products() => service.GetProductStats();

    /// <summary>趋势。level=Hour 最近 count 小时，level=Day 最近 count 天；productId=0 为全平台</summary>
    [HttpGet("trend")]
    public IList<TrendPoint> Trend(StatLevels level = StatLevels.Hour, Int32 count = 24, Int32 productId = 0) => service.GetTrend(level, count, productId);
}

/// <summary>协议与接入</summary>
[ApiController]
[Route("api/protocols")]
[Tags("Protocols & MQTT")]
public class ProtocolsController(IMqttTransport mqtt, PayloadCodecFactory codecs, DriverHostService driverHost, Microsoft.Extensions.Options.IOptions<IoTOptions> options, MqttBrokerCatalog catalog) : ControllerBase
{
    /// <summary>支持的接入协议</summary>
    [HttpGet]
    public Object Protocols() => new
    {
        protocols = AccessProtocols.All,
        topics = new
        {
            uplink = Topics.Uplinks.Select(k => $"{Topics.Root}/{{productKey}}/{{deviceCode}}/{k}"),
            downlink = Topics.Downlinks.Select(k => $"{Topics.Root}/{{productKey}}/{{deviceCode}}/{k}"),
        },
    };

    /// <summary>已注册的 NewLife.IoT 驱动（含默认参数模板）</summary>
    [HttpGet("drivers")]
    public IEnumerable<Object> Drivers() => DriverRegistry.Drivers.Select(d => new { d.Name, d.DisplayName, d.Description, d.ClassName, d.Version, d.DefaultParameter });

    /// <summary>驱动采集任务运行状态</summary>
    [HttpGet("drivers/workers")]
    public IReadOnlyList<DriverWorkerStatus> Workers() => driverHost.GetStatus();

    /// <summary>载荷编解码器</summary>
    [HttpGet("codecs")]
    public IEnumerable<Object> Codecs() => codecs.Codecs.Select(c => new { c.Name, c.Description });

    /// <summary>MQTT 状态（平台默认服务器）</summary>
    [HttpGet("mqtt")]
    public MqttStatus Mqtt() => mqtt.GetStatus();

    /// <summary>MQTT 配置：目录、每台服务器连接状态与占用设备数（不含密码）</summary>
    [HttpGet("mqtt/config")]
    public Object MqttConfig()
    {
        var o = options.Value.Mqtt;
        var status = mqtt.GetStatus();
        var snapshots = mqtt.GetBrokers();
        return new
        {
            o.Enable,
            mode = o.IsExternal ? MqttModes.External : MqttModes.Embedded,
            topicRoot = Topics.Root,
            defaultBroker = catalog.PlatformDefaultId,
            requestedBroker = o.ActiveBroker,
            status = new { status.Running, status.Server, status.LastError, status.BrokerId },
            embedded = new { o.Port, o.AdminUser, o.AllowAnonymous },
            brokers = snapshots.Select(s =>
            {
                var profile = o.FindBroker(s.Id);
                return new
                {
                    s.Id,
                    s.Name,
                    s.Kind,
                    s.Running,
                    s.InUse,
                    s.Server,
                    s.Port,
                    s.TopicRoot,
                    s.DeviceCount,
                    s.ClientId,
                    s.LastError,
                    s.MessagesReceived,
                    s.MessagesSent,
                    userName = profile?.UserName,
                    hasPassword = !String.IsNullOrEmpty(profile?.Password),
                    useTls = profile?.UseTls ?? false,
                    qos = profile?.QoS,
                    keepAlive = profile?.KeepAlive,
                    reconnectDelay = profile?.ReconnectDelay,
                    publicServer = profile?.PublicServer,
                    isDefault = catalog.Same(s.Id, catalog.PlatformDefaultId),
                };
            }),
        };
    }
}
