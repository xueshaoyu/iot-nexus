using System.Text.RegularExpressions;
using IoTPlatform.Data;

namespace IoTPlatform.Rules;

/// <summary>动作参数模板渲染</summary>
/// <remarks>
/// 支持的占位符：{device.code} {device.name} {device.id} {product.name} {product.code}
/// {rule.name} {trigger} {event.name} {event.type} {event.remark} {time}，
/// 以及任意属性/事件参数 {temperature}。未知占位符保持原样。
/// </remarks>
public static partial class RuleTemplate
{
    [GeneratedRegex(@"\{([A-Za-z_][A-Za-z0-9_\.]*)\}")]
    private static partial Regex Placeholder();

    public static String Render(String? template, Rule rule, RuleContext context)
    {
        if (String.IsNullOrEmpty(template)) return template ?? "";

        return Placeholder().Replace(template, m =>
        {
            var key = m.Groups[1].Value;
            var value = key.ToLowerInvariant() switch
            {
                "device.code" => context.Device.Code,
                "device.name" => context.Device.Name ?? context.Device.Code,
                "device.id" => context.Device.Id.ToString(),
                "product.name" => context.Product?.Name,
                "product.code" => context.Product?.Code,
                "rule.name" => rule.Name,
                "rule.id" => rule.Id.ToString(),
                "trigger" => context.TriggerType,
                "event.name" => context.EventName,
                "event.type" => context.EventType,
                "event.remark" => context.Remark,
                "time" => context.Time.ToString("yyyy-MM-dd HH:mm:ss"),
                _ => null,
            };
            if (value != null) return value;

            var name = key.StartsWith("value.", StringComparison.OrdinalIgnoreCase) ? key[6..] : key;
            if (context.Values.TryGetValue(name, out var v) || context.Shadow.TryGetValue(name, out v))
                return RuleEvaluator.ToText(v);

            return m.Value;
        });
    }
}
