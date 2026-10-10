using System.Text;
using IoTPlatform.Data;
using IoTPlatform.Rules;
using IoTPlatform.Services;
using NewLife;
using NewLife.MQTT.Messaging;

namespace IoTPlatform.Mqtt;

/// <summary>规则动作：向触发设备所在 MQTT 服务器的主题发布消息</summary>
public class MqttPublishAction(IMqttTransport transport) : IRuleAction
{
    public String Name => "mqtt-publish";
    public String DisplayName => "Publish MQTT message";
    public String Description => "Publish a message to an MQTT topic on the broker assigned to the triggering device. Empty payload sends the trigger data as JSON.";

    public IReadOnlyList<RuleActionParameter> Parameters { get; } =
    [
        new("topic", "Topic", "e.g. alerts/{product.code}/{device.code}", true, "alerts/{device.code}"),
        new("payload", "Payload", "Optional template"),
        new("qos", "QoS", "0 / 1", false, "0", ["0", "1"]),
    ];

    public async Task<RuleActionResult> ExecuteAsync(Rule rule, RuleContext context, IDictionary<String, String?> parameters, CancellationToken cancellationToken)
    {
        var topic = parameters.GetValueOrDefault("topic");
        if (topic.IsNullOrWhiteSpace() || topic.Contains('#') || topic.Contains('+')) return RuleActionResult.Fail("invalid topic");

        var payload = parameters.GetValueOrDefault("payload");
        var bytes = payload.IsNullOrWhiteSpace()
            ? Encoding.UTF8.GetBytes(JsonDefaults.Serialize(new { rule = rule.Name, device = context.Device.Code, trigger = context.TriggerType, values = context.Values, time = context.Time }))
            : Encoding.UTF8.GetBytes(payload);
        var qos = parameters.GetValueOrDefault("qos") == "1" ? QualityOfService.AtLeastOnce : QualityOfService.AtMostOnce;

        var brokerId = transport.ResolveBrokerId(context.Device);
        return await transport.PublishAsync(brokerId, topic, bytes, qos, false, cancellationToken)
            ? RuleActionResult.Ok(topic)
            : RuleActionResult.Fail("MQTT server is not running");
    }
}
