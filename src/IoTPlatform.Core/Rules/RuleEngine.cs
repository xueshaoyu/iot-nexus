using System.Collections.Concurrent;
using System.Threading.Channels;
using IoTPlatform.Data;
using IoTPlatform.Services;
using XCode;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NewLife;
using NewLife.Log;

namespace IoTPlatform.Rules;

/// <summary>规则引擎</summary>
/// <remarks>
/// 数据接入、上下线只负责 <see cref="Post"/> 投递上下文，引擎在后台队列中异步求值并执行动作，
/// 避免 Webhook 等慢动作阻塞设备接入链路。
/// 求值顺序：触发类型 → 作用域（产品/分组/设备）→ 条件 → 静默期 → 顺序执行动作 → 记录日志。
/// </remarks>
public class RuleEngine(IServiceProvider serviceProvider) : BackgroundService
{
    private readonly Channel<RuleContext> _queue = Channel.CreateBounded<RuleContext>(new BoundedChannelOptions(10_000)
    {
        FullMode = BoundedChannelFullMode.DropOldest,
        SingleReader = true,
    });
    private readonly ConcurrentDictionary<(Int32 RuleId, Int32 DeviceId), DateTime> _silence = new();
    private readonly ConcurrentDictionary<Int32, (DateTime Version, List<RuleCondition> Conditions, List<RuleActionDefinition> Actions)> _parsed = new();
    private IReadOnlyList<IRuleAction>? _actions;

    /// <summary>已注册的动作</summary>
    public IReadOnlyList<IRuleAction> Actions => _actions ??= serviceProvider.GetServices<IRuleAction>().ToList();

    /// <summary>投递触发上下文</summary>
    public void Post(RuleContext context) => _queue.Writer.TryWrite(context);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var context in _queue.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await ProcessAsync(context, stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    XTrace.WriteException(ex);
                }
            }
        }
        catch (OperationCanceledException) { }
    }

    /// <summary>对上下文求值并执行命中的规则</summary>
    public async Task<IList<RuleLog>> ProcessAsync(RuleContext context, CancellationToken cancellationToken = default)
    {
        var logs = new List<RuleLog>();
        var rules = Rule.FindAllWithCache().Where(e => e.Enable && context.TriggerType.EqualIgnoreCase(e.TriggerType)).ToList();
        if (rules.Count == 0) return logs;

        foreach (var rule in rules)
        {
            if (!RuleEvaluator.InScope(rule, context.Device, DeviceGroupService.IsDescendant)) continue;

            var (conditions, _) = Parse(rule);
            if (!RuleEvaluator.IsMatch(rule.TriggerType!, rule.EventName, rule.ConditionLogic, conditions, context)) continue;

            var key = (rule.Id, context.Device.Id);
            if (rule.SilenceSeconds > 0 && _silence.TryGetValue(key, out var last) && (context.Time - last).TotalSeconds < rule.SilenceSeconds) continue;
            _silence[key] = context.Time;

            logs.Add(await ExecuteAsync(rule, context, cancellationToken));
        }

        return logs;
    }

    /// <summary>执行规则的全部动作并记录日志</summary>
    public async Task<RuleLog> ExecuteAsync(Rule rule, RuleContext context, CancellationToken cancellationToken = default)
    {
        var (_, actions) = Parse(rule);
        var results = new List<String>();
        var success = true;

        foreach (var def in actions)
        {
            var action = Actions.FirstOrDefault(e => e.Name.EqualIgnoreCase(def.Type));
            if (action == null)
            {
                success = false;
                results.Add($"[{def.Type}] unknown action");
                continue;
            }

            var parameters = new Dictionary<String, String?>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in action.Parameters) parameters[p.Name] = p.DefaultValue;
            foreach (var (k, v) in def.Parameters) parameters[k] = RuleTemplate.Render(v, rule, context);

            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(30));
                var result = await action.ExecuteAsync(rule, context, parameters, cts.Token);
                success &= result.Success;
                results.Add($"[{action.Name}] {(result.Success ? "ok" : "failed")}{(result.Message.IsNullOrEmpty() ? "" : ": " + result.Message)}");
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                success = false;
                results.Add($"[{action.Name}] error: {ex.Message}");
                XTrace.WriteLine("规则[{0}]动作[{1}]执行异常 {2}", rule.Name, action.Name, ex.Message);
            }
        }

        var snapshot = new Dictionary<String, Object?>(context.Values, StringComparer.OrdinalIgnoreCase);
        if (!context.EventName.IsNullOrEmpty()) snapshot["$event"] = context.EventName;
        if (!context.Remark.IsNullOrEmpty()) snapshot["$remark"] = context.Remark;

        var log = new RuleLog
        {
            Id = RuleLog.Meta.Factory.Snow.NewId(),
            RuleId = rule.Id,
            DeviceId = context.Device.Id,
            TriggerType = context.TriggerType,
            Snapshot = JsonDefaults.Serialize(snapshot).Cut(2000),
            Success = success,
            Result = String.Join("; ", results).Cut(2000),
            CreateTime = DateTime.Now,
        };
        log.SaveAsync(SqliteSettings.SaveAsyncDelayMs);

        rule.Triggers++;
        rule.LastTrigger = DateTime.Now;
        rule.SaveAsync(SqliteSettings.DeviceCounterDelayMs);

        DeviceHistory.Add(context.Device.Id, DeviceActions.Rule, success, $"{rule.Name}: {log.Result}");
        XTrace.WriteLine("规则触发 [{0}] 设备 {1} {2}", rule.Name, context.Device.Code, log.Result);

        return log;
    }

    /// <summary>解析规则的条件与动作，按更新时间缓存</summary>
    public (List<RuleCondition> Conditions, List<RuleActionDefinition> Actions) Parse(Rule rule)
    {
        if (_parsed.TryGetValue(rule.Id, out var item) && item.Version == rule.UpdateTime) return (item.Conditions, item.Actions);

        var conditions = JsonDefaults.Deserialize<List<RuleCondition>>(rule.Conditions) ?? [];
        var actions = JsonDefaults.Deserialize<List<RuleActionDefinition>>(rule.Actions) ?? [];
        _parsed[rule.Id] = (rule.UpdateTime, conditions, actions);

        return (conditions, actions);
    }
}
