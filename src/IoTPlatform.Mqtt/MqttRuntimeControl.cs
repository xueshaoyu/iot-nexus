using IoTPlatform.Protocols;
using IoTPlatform.Services;

namespace IoTPlatform.Mqtt;

/// <summary>把界面保存的 MQTT 配置应用到运行中的内置服务端与外部连接</summary>
public class MqttRuntimeControl(MqttServerHost embedded, ExternalMqttTransport external) : IMqttRuntimeControl
{
    public async Task ApplyAsync(MqttOptions before, MqttOptions after, CancellationToken cancellationToken = default)
    {
        Topics.Configure(after.TopicRoot);
        embedded.Restart();
        await external.ReconcileNowAsync(cancellationToken);
    }
}
