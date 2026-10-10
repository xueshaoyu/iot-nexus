using System.ComponentModel.DataAnnotations;
using IoTPlatform.Data;

namespace IoTPlatform.Rules;

/// <summary>规则条件。对属性值进行比较</summary>
public class RuleCondition
{
    /// <summary>属性标识符。事件触发时可使用事件输出参数名</summary>
    public String Property { get; set; } = "";

    /// <summary>运算符。&gt; &gt;= &lt; &lt;= == != contains changed</summary>
    public String Operator { get; set; } = ">";

    /// <summary>比较值</summary>
    public String? Value { get; set; }

    public override String ToString() => Operator == RuleOperators.Changed ? $"{Property} changed" : $"{Property} {Operator} {Value}";
}

/// <summary>规则动作定义</summary>
public class RuleActionDefinition
{
    /// <summary>动作类型，对应 <see cref="IRuleAction.Name"/></summary>
    public String Type { get; set; } = "alarm";

    /// <summary>动作参数</summary>
    public Dictionary<String, String?> Parameters { get; set; } = [];
}

/// <summary>规则运算符</summary>
public static class RuleOperators
{
    public const String Changed = "changed";
    public const String Contains = "contains";

    public static readonly String[] All = [">", ">=", "<", "<=", "==", "!=", Contains, Changed];
}

/// <summary>规则输入</summary>
public class RuleInput
{
    [Required, StringLength(50)]
    public String Name { get; set; } = "";

    public Boolean Enable { get; set; } = true;

    /// <summary>Property/Event/Online/Offline</summary>
    [Required]
    public String TriggerType { get; set; } = RuleTriggers.Property;

    public Int32 ProductId { get; set; }
    public Int32 GroupId { get; set; }
    public Int32 DeviceId { get; set; }

    /// <summary>事件触发时匹配的事件标识，空表示任意事件</summary>
    public String? EventName { get; set; }

    /// <summary>All / Any</summary>
    public String ConditionLogic { get; set; } = "All";

    public List<RuleCondition> Conditions { get; set; } = [];

    public List<RuleActionDefinition> Actions { get; set; } = [];

    [Range(0, 86400)]
    public Int32 SilenceSeconds { get; set; } = 60;

    [StringLength(500)]
    public String? Remark { get; set; }
}

/// <summary>规则触发上下文</summary>
public class RuleContext
{
    public required Device Device { get; init; }

    public Product? Product { get; init; }

    public required String TriggerType { get; init; }

    /// <summary>本次上报的数据（属性值或事件输出参数）</summary>
    public IDictionary<String, Object?> Values { get; init; } = new Dictionary<String, Object?>(StringComparer.OrdinalIgnoreCase);

    /// <summary>上报前的属性值，用于 changed 判断</summary>
    public IDictionary<String, Object?> Previous { get; init; } = new Dictionary<String, Object?>(StringComparer.OrdinalIgnoreCase);

    /// <summary>设备影子（全部属性最新值），条件中引用了本次未上报的属性时从这里取值</summary>
    public IDictionary<String, Object?> Shadow { get; init; } = new Dictionary<String, Object?>(StringComparer.OrdinalIgnoreCase);

    public String? EventName { get; init; }
    public String? EventType { get; init; }
    public String? Remark { get; init; }
    public DateTime Time { get; init; } = DateTime.Now;
}

/// <summary>动作执行结果</summary>
public record RuleActionResult(Boolean Success, String? Message)
{
    public static RuleActionResult Ok(String? message = null) => new(true, message);
    public static RuleActionResult Fail(String message) => new(false, message);
}

/// <summary>动作参数描述，供管理界面动态生成表单</summary>
public record RuleActionParameter(String Name, String DisplayName, String? Description = null, Boolean Required = false, String? DefaultValue = null, String[]? Options = null);
