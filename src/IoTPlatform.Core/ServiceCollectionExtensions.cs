using IoTPlatform.Rules;
using IoTPlatform.Rules.Actions;
using IoTPlatform.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace IoTPlatform;

public static class ServiceCollectionExtensions
{
    /// <summary>注册平台核心服务：产品、设备、分组、在线、接入、下行、统计、告警、规则引擎与内置动作</summary>
    public static IServiceCollection AddIoTCore(this IServiceCollection services)
    {
        services.AddHttpClient(nameof(WebhookAction), c => c.Timeout = TimeSpan.FromSeconds(10));

        services.TryAddSingleton<MessageCounter>();
        services.TryAddSingleton<MqttBrokerCatalog>();
        services.TryAddSingleton<MqttSettingsStore>();
        services.TryAddSingleton<MqttSettingsService>();
        services.TryAddSingleton<ProductService>();
        services.TryAddSingleton<DeviceGroupService>();
        services.TryAddSingleton<DeviceService>();
        services.TryAddSingleton<DeviceOnlineService>();
        services.TryAddSingleton<DataIngestService>();
        services.TryAddSingleton<CommandTracker>();
        services.TryAddSingleton<DeviceCommandService>();
        services.TryAddSingleton<BatchControlService>();
        services.TryAddSingleton<StatService>();
        services.TryAddSingleton<AccessLogService>();
        services.TryAddSingleton<AlarmService>();
        services.TryAddSingleton<RuleService>();

        services.TryAddSingleton<RuleEngine>();
        services.AddHostedService(sp => sp.GetRequiredService<RuleEngine>());

        services.AddSingleton<IDeviceCommandChannel, HttpPollingChannel>();

        services.AddSingleton<IRuleAction, AlarmAction>();
        services.AddSingleton<IRuleAction, WebhookAction>();
        services.AddSingleton<IRuleAction, SetPropertyAction>();
        services.AddSingleton<IRuleAction, InvokeServiceAction>();
        services.AddSingleton<IRuleAction, DeviceLogAction>();

        return services;
    }

    /// <summary>注册自定义规则动作</summary>
    public static IServiceCollection AddRuleAction<TAction>(this IServiceCollection services) where TAction : class, IRuleAction
    {
        services.AddSingleton<IRuleAction, TAction>();
        return services;
    }
}
