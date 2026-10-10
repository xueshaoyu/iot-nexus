using IoTPlatform.Data;
using IoTPlatform.Models;
using IoTPlatform.Services;
using NewLife;
using NewLife.Data;
using XCode;

namespace IoTPlatform.Rules;

/// <summary>规则测试结果</summary>
public record RuleTestResult(Boolean Matched, String Message, RuleLog? Log);

/// <summary>规则管理</summary>
public class RuleService(RuleEngine engine)
{
    public PagedResult<Rule> Query(String? keyword, Boolean? enable, Int32 pageIndex = 1, Int32 pageSize = 20)
    {
        var exp = new WhereExpression();
        if (enable != null) exp &= Rule._.Enable == enable.Value;
        if (!keyword.IsNullOrWhiteSpace()) exp &= Rule._.Name.Contains(keyword.Trim()) | Rule._.Remark.Contains(keyword.Trim());

        var page = new PageParameter { PageIndex = Math.Max(1, pageIndex), PageSize = Math.Clamp(pageSize, 1, 500), Sort = nameof(Rule.Id), Desc = false };
        var list = Rule.FindAll(exp, page);

        return new PagedResult<Rule>(list, page.TotalCount, page.PageIndex, page.PageSize);
    }

    public Rule Get(Int32 id) => Rule.FindById(id) ?? throw PlatformException.NotFound("Rule");

    public RuleInput ToInput(Rule rule)
    {
        var (conditions, actions) = engine.Parse(rule);
        return new RuleInput
        {
            Name = rule.Name,
            Enable = rule.Enable,
            TriggerType = rule.TriggerType ?? RuleTriggers.Property,
            ProductId = rule.ProductId,
            GroupId = rule.GroupId,
            DeviceId = rule.DeviceId,
            EventName = rule.EventName,
            ConditionLogic = rule.ConditionLogic ?? "All",
            Conditions = conditions.Select(e => new RuleCondition { Property = e.Property, Operator = e.Operator, Value = e.Value }).ToList(),
            Actions = actions.Select(e => new RuleActionDefinition { Type = e.Type, Parameters = new(e.Parameters) }).ToList(),
            SilenceSeconds = rule.SilenceSeconds,
            Remark = rule.Remark,
        };
    }

    public Rule Create(RuleInput input)
    {
        var rule = new Rule();
        Apply(rule, input);
        rule.Insert();

        return rule;
    }

    public Rule Update(Int32 id, RuleInput input)
    {
        var rule = Get(id);
        Apply(rule, input);
        rule.UpdateTime = DateTime.Now;
        rule.Update();

        return rule;
    }

    public void Delete(Int32 id) => Get(id).Delete();

    public Rule SetEnable(Int32 id, Boolean enable)
    {
        var rule = Get(id);
        rule.Enable = enable;
        rule.Update();

        return rule;
    }

    public PagedResult<RuleLog> GetLogs(Int32 ruleId, Int32 deviceId, Int32 pageIndex = 1, Int32 pageSize = 20)
    {
        var exp = new WhereExpression();
        if (ruleId > 0) exp &= RuleLog._.RuleId == ruleId;
        if (deviceId > 0) exp &= RuleLog._.DeviceId == deviceId;

        var page = new PageParameter { PageIndex = Math.Max(1, pageIndex), PageSize = Math.Clamp(pageSize, 1, 500), Sort = nameof(RuleLog.Id), Desc = true };
        var list = RuleLog.FindAll(exp, page);

        return new PagedResult<RuleLog>(list, page.TotalCount, page.PageIndex, page.PageSize);
    }

    /// <summary>用给定数据测试规则。execute=true 时命中后真正执行动作</summary>
    public async Task<RuleTestResult> TestAsync(Int32 ruleId, Int32 deviceId, IDictionary<String, Object?> values, String? eventName, Boolean execute, CancellationToken cancellationToken = default)
    {
        var rule = Get(ruleId);
        var device = Device.FindById(deviceId) ?? throw PlatformException.NotFound("Device");
        if (!RuleEvaluator.InScope(rule, device, DeviceGroupService.IsDescendant)) return new(false, "Device is outside the rule scope", null);

        var shadow = DeviceProperty.FindAllByDeviceId(device.Id).ToDictionary(e => e.Name, e => (Object?)e.Value, StringComparer.OrdinalIgnoreCase);
        var normalized = values.ToDictionary(e => e.Key, e => JsonDefaults.Normalize(e.Value), StringComparer.OrdinalIgnoreCase);
        var context = new RuleContext
        {
            Device = device,
            Product = device.Product,
            TriggerType = rule.TriggerType ?? RuleTriggers.Property,
            Values = normalized,
            Previous = shadow,
            Shadow = shadow,
            EventName = eventName,
        };

        var (conditions, _) = engine.Parse(rule);
        if (!RuleEvaluator.IsMatch(context.TriggerType, rule.EventName, rule.ConditionLogic, conditions, context))
            return new(false, "Conditions not met", null);

        if (!execute) return new(true, "Conditions met (dry run, actions not executed)", null);

        var log = await engine.ExecuteAsync(rule, context, cancellationToken);
        return new(true, log.Result ?? "", log);
    }

    private void Apply(Rule rule, RuleInput input)
    {
        if (!RuleTriggers.All.Contains(input.TriggerType, StringComparer.OrdinalIgnoreCase))
            throw new PlatformException("Unsupported trigger type '{0}'", input.TriggerType);

        var conditions = input.Conditions.Where(e => !e.Property.IsNullOrWhiteSpace()).ToList();
        foreach (var c in conditions)
        {
            if (!RuleOperators.All.Contains(c.Operator)) throw new PlatformException("Unsupported operator '{0}'", c.Operator);
            c.Property = c.Property.Trim();
        }
        if (input.TriggerType.EqualIgnoreCase(RuleTriggers.Property) && conditions.Count == 0)
            throw new PlatformException("Property rules need at least one condition");

        var actions = input.Actions.Where(e => !e.Type.IsNullOrWhiteSpace()).ToList();
        if (actions.Count == 0) throw new PlatformException("At least one action is required");
        foreach (var a in actions)
        {
            var action = engine.Actions.FirstOrDefault(e => e.Name.EqualIgnoreCase(a.Type)) ?? throw new PlatformException("Unknown action type '{0}'", a.Type);
            foreach (var p in action.Parameters.Where(e => e.Required))
            {
                if (!a.Parameters.TryGetValue(p.Name, out var v) || v.IsNullOrWhiteSpace())
                    throw new PlatformException("Action '{0}' requires parameter '{1}'", action.DisplayName, p.DisplayName);
            }
        }

        rule.Name = input.Name.Trim();
        rule.Enable = input.Enable;
        rule.TriggerType = RuleTriggers.All.First(e => e.EqualIgnoreCase(input.TriggerType));
        rule.ProductId = input.ProductId;
        rule.GroupId = input.GroupId;
        rule.DeviceId = input.DeviceId;
        rule.EventName = input.EventName.IsNullOrWhiteSpace() ? null : input.EventName.Trim();
        rule.ConditionLogic = input.ConditionLogic.EqualIgnoreCase("Any") ? "Any" : "All";
        rule.Conditions = JsonDefaults.Serialize(conditions);
        rule.Actions = JsonDefaults.Serialize(actions);
        rule.SilenceSeconds = input.SilenceSeconds;
        rule.Remark = input.Remark;
    }
}
