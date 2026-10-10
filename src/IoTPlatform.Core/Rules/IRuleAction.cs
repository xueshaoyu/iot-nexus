using IoTPlatform.Data;

namespace IoTPlatform.Rules;

/// <summary>规则动作。实现该接口并注册到容器即可扩展新的动作类型</summary>
/// <remarks>
/// 注册方式：services.AddSingleton&lt;IRuleAction, MyAction&gt;()。
/// 参数值支持模板占位符，例如 {device.code}、{temperature}，由 <see cref="RuleTemplate"/> 渲染。
/// </remarks>
public interface IRuleAction
{
    /// <summary>动作类型名，保存在规则 Actions 的 type 字段</summary>
    String Name { get; }

    /// <summary>显示名</summary>
    String DisplayName { get; }

    /// <summary>说明</summary>
    String Description { get; }

    /// <summary>参数定义</summary>
    IReadOnlyList<RuleActionParameter> Parameters { get; }

    /// <summary>执行动作</summary>
    Task<RuleActionResult> ExecuteAsync(Rule rule, RuleContext context, IDictionary<String, String?> parameters, CancellationToken cancellationToken);
}
