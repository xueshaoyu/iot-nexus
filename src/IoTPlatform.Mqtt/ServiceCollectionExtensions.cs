using IoTPlatform.Rules;
using IoTPlatform.Services;
using Microsoft.Extensions.DependencyInjection;
using IoTPlatform.Protocols;
using NewLife.MQTT.Handlers;

namespace IoTPlatform.Mqtt;

public static class MqttServiceCollectionExtensions
{
    /// <summary>注册 MQTT 接入（内置服务端或外部服务器）、设备认证、下行通道与 mqtt-publish 规则动作</summary>
    public static IServiceCollection AddIoTMqtt(this IServiceCollection services)
    {
        services.AddSingleton<DeviceMqttAuthenticator>();
        services.AddSingleton<MqttUplinkRouter>();

        // 内置与外部都注册；按设备/产品引用同时连接多个服务器。IMqttTransport 为路由中枢
        services.AddSingleton<MqttServerHost>();
        services.AddSingleton<ExternalMqttTransport>();
        services.AddSingleton<MqttBrokerHub>();
        services.AddSingleton<IMqttRuntimeControl, MqttRuntimeControl>();
        services.AddSingleton<MqttConnectionTester>();
        services.AddHostedService(sp => sp.GetRequiredService<MqttServerHost>());
        services.AddHostedService(sp => sp.GetRequiredService<ExternalMqttTransport>());
        services.AddSingleton<IMqttTransport>(sp => sp.GetRequiredService<MqttBrokerHub>());

        // NewLife.MQTT 会话从 ServiceProvider 解析 IMqttHandler，瞬态注册保证每个连接一个处理器
        services.AddTransient<IMqttHandler, IoTMqttHandler>();

        services.AddSingleton<IDeviceCommandChannel, MqttCommandChannel>();
        services.AddSingleton<IDeviceSessionManager, MqttSessionManager>();
        services.AddSingleton<IRuleAction, MqttPublishAction>();

        // 全局主题根；各外部服务器可用自己的 TopicRoot 覆盖
        services.AddOptions<IoTOptions>().PostConfigure(o => Topics.Configure(o.Mqtt.TopicRoot));

        return services;
    }
}
