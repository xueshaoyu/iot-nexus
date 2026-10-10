using System.Globalization;
using IoTPlatform.Data;

namespace IoTPlatform.Rules;

/// <summary>规则条件求值器。纯函数，不依赖数据库</summary>
public static class RuleEvaluator
{
    /// <summary>判断规则作用域是否覆盖该设备</summary>
    public static Boolean InScope(Rule rule, Device device, Func<Int32, Int32, Boolean>? isGroupDescendant = null)
    {
        if (rule.ProductId > 0 && rule.ProductId != device.ProductId) return false;
        if (rule.DeviceId > 0 && rule.DeviceId != device.Id) return false;
        if (rule.GroupId > 0)
        {
            if (device.GroupId <= 0) return false;
            if (device.GroupId != rule.GroupId && !(isGroupDescendant?.Invoke(device.GroupId, rule.GroupId) ?? false)) return false;
        }

        return true;
    }

    /// <summary>判断规则是否被本次上下文触发</summary>
    public static Boolean IsMatch(String triggerType, String? eventName, String? logic, IList<RuleCondition> conditions, RuleContext context)
    {
        if (!triggerType.Equals(context.TriggerType, StringComparison.OrdinalIgnoreCase)) return false;

        if (triggerType.Equals(RuleTriggers.Event, StringComparison.OrdinalIgnoreCase) &&
            !String.IsNullOrEmpty(eventName) &&
            !eventName.Equals(context.EventName, StringComparison.OrdinalIgnoreCase))
            return false;

        var valid = conditions.Where(e => !String.IsNullOrWhiteSpace(e.Property)).ToList();
        if (valid.Count == 0) return true;

        // 属性触发：至少一个条件引用了本次上报的属性，避免无关属性上报导致重复触发
        if (triggerType.Equals(RuleTriggers.Property, StringComparison.OrdinalIgnoreCase) &&
            !valid.Any(e => context.Values.ContainsKey(e.Property)))
            return false;

        var any = String.Equals(logic, "Any", StringComparison.OrdinalIgnoreCase);
        return any ? valid.Any(e => Evaluate(e, context)) : valid.All(e => Evaluate(e, context));
    }

    /// <summary>对单个条件求值</summary>
    public static Boolean Evaluate(RuleCondition condition, RuleContext context)
    {
        var name = condition.Property;
        if (!context.Values.TryGetValue(name, out var current) && !context.Shadow.TryGetValue(name, out current)) return false;

        if (condition.Operator == RuleOperators.Changed)
        {
            if (!context.Values.ContainsKey(name)) return false;
            if (!context.Previous.TryGetValue(name, out var prev)) return true;

            return !String.Equals(ToText(prev), ToText(current), StringComparison.Ordinal);
        }

        return Compare(current, condition.Operator, condition.Value);
    }

    /// <summary>比较两个值。两侧都能转为数字时按数值比较，否则按字符串比较</summary>
    public static Boolean Compare(Object? left, String op, String? right)
    {
        var l = ToText(left);
        var r = right?.Trim() ?? "";

        if (op == RuleOperators.Contains) return l.Contains(r, StringComparison.OrdinalIgnoreCase);

        if (TryNumber(l, out var a) && TryNumber(r, out var b))
        {
            return op switch
            {
                ">" => a > b,
                ">=" => a >= b,
                "<" => a < b,
                "<=" => a <= b,
                "==" or "=" => Math.Abs(a - b) < 1e-9,
                "!=" or "<>" => Math.Abs(a - b) >= 1e-9,
                _ => false,
            };
        }

        var cmp = String.Compare(l, r, StringComparison.OrdinalIgnoreCase);
        return op switch
        {
            ">" => cmp > 0,
            ">=" => cmp >= 0,
            "<" => cmp < 0,
            "<=" => cmp <= 0,
            "==" or "=" => cmp == 0,
            "!=" or "<>" => cmp != 0,
            _ => false,
        };
    }

    internal static String ToText(Object? value) => value switch
    {
        null => "",
        Boolean b => b ? "true" : "false",
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? "",
    };

    private static Boolean TryNumber(String text, out Double value)
    {
        if (text.Equals("true", StringComparison.OrdinalIgnoreCase)) { value = 1; return true; }
        if (text.Equals("false", StringComparison.OrdinalIgnoreCase)) { value = 0; return true; }

        return Double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }
}
