using IoTPlatform.Mqtt;
using IoTPlatform.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace IoTPlatform.Web.Controllers;

/// <summary>MQTT 配置（管理端）。密码只写不读；环境变量锁定的字段不可改</summary>
[ApiController]
[Route("api/settings/mqtt")]
[Tags("MQTT settings")]
public class MqttSettingsController(
    MqttSettingsService settings,
    IMqttTransport mqtt,
    MqttServerHost embedded,
    MqttConnectionTester tester,
    IOptions<IoTOptions> options) : ControllerBase
{
    /// <summary>当前生效配置与连接状态（不含密码）</summary>
    [HttpGet]
    public Object Get()
    {
        var o = options.Value.Mqtt;
        var status = mqtt.GetStatus();
        var embeddedStatus = embedded.GetStatus();
        var snapshots = mqtt.GetBrokers();
        return new
        {
            o.Enable,
            mode = o.Mode,
            o.TopicRoot,
            o.ActiveBroker,
            defaultBroker = o.GetPlatformDefaultId(),
            o.Port,
            o.PublicHost,
            o.AdminUser,
            hasAdminPassword = settings.HasAdminPassword(),
            o.AllowAnonymous,
            o.ManagementPort,
            lockedFields = settings.LockedFields,
            brokersLocked = settings.BrokersLocked,
            storePath = settings.StorePath,
            precedence = "Environment variables (IoT__Mqtt__*) override UI settings, which override appsettings.json.",
            restartHint = "Changing Enable, Port, Mode or ManagementPort restarts the built-in broker (connected devices drop briefly). Account, password and external brokers apply immediately.",
            status = new { status.Running, status.Server, status.ConnectedClients, status.LastError, status.BrokerId },
            embedded = new { embeddedStatus.Running, embeddedStatus.ConnectedClients, embeddedStatus.Port, embeddedStatus.LastError, clients = embeddedStatus.Clients },
            brokers = snapshots.Select(s =>
            {
                var profile = o.FindBroker(s.Id);
                return new
                {
                    s.Id,
                    s.Name,
                    s.Kind,
                    enable = profile?.Enable ?? s.Kind == MqttModes.Embedded,
                    s.Running,
                    s.InUse,
                    s.Server,
                    s.Port,
                    s.TopicRoot,
                    s.DeviceCount,
                    s.ClientId,
                    s.LastError,
                    userName = profile?.UserName,
                    hasPassword = settings.BrokerHasPassword(s.Id),
                    useTls = profile?.UseTls ?? false,
                    qos = profile?.QoS,
                    keepAlive = profile?.KeepAlive,
                    reconnectDelay = profile?.ReconnectDelay,
                    publicServer = profile?.PublicServer,
                    locked = settings.BrokersLocked,
                };
            }),
        };
    }

    /// <summary>保存内置 Broker 与平台默认项</summary>
    [HttpPut]
    public async Task<IActionResult> Save(MqttBuiltInInput input)
    {
        await settings.SaveBuiltInAsync(input);
        return Ok(Get());
    }

    /// <summary>新增或更新外部服务器</summary>
    [HttpPut("brokers")]
    public async Task<IActionResult> UpsertBroker(MqttBrokerInput input)
    {
        await settings.UpsertBrokerAsync(input);
        return Ok(Get());
    }

    [HttpDelete("brokers/{id}")]
    public async Task<IActionResult> DeleteBroker(String id)
    {
        await settings.DeleteBrokerAsync(id);
        return NoContent();
    }

    [HttpPost("brokers/{id}/enable")]
    public async Task<IActionResult> EnableBroker(String id, [FromQuery] Boolean enable = true)
    {
        await settings.SetBrokerEnabledAsync(id, enable);
        return Ok(Get());
    }

    /// <summary>测试连接。密码为空时使用已保存的密码</summary>
    [HttpPost("test")]
    public async Task<MqttConnectionTestResult> Test(MqttBrokerInput input, CancellationToken cancellationToken)
    {
        var broker = settings.ResolveForTest(input);
        return await tester.TestAsync(broker, cancellationToken: cancellationToken);
    }
}
