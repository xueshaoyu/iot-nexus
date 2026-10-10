using IoTPlatform.Data;
using IoTPlatform.Models;
using IoTPlatform.Rules;
using IoTPlatform.Services;
using Microsoft.AspNetCore.Mvc;

namespace IoTPlatform.Web.Controllers;

/// <summary>规则测试请求</summary>
public class RuleTestInput
{
    public Int32 DeviceId { get; set; }
    public Dictionary<String, Object?> Values { get; set; } = [];
    public String? EventName { get; set; }

    /// <summary>命中后是否真正执行动作</summary>
    public Boolean Execute { get; set; }
}

/// <summary>规则引擎</summary>
[ApiController]
[Route("api/rules")]
[Tags("Rules & alarms")]
public class RulesController(RuleService service, RuleEngine engine) : ControllerBase
{
    [HttpGet]
    public PagedResult<Rule> Query(String? keyword, Boolean? enable, Int32 pageIndex = 1, Int32 pageSize = 20) => service.Query(keyword, enable, pageIndex, pageSize);

    /// <summary>规则详情，definition 为可编辑的条件与动作</summary>
    [HttpGet("{id:int}")]
    public Object Get(Int32 id)
    {
        var rule = service.Get(id);
        return new { rule, definition = service.ToInput(rule) };
    }

    [HttpPost]
    public Rule Create(RuleInput input) => service.Create(input);

    [HttpPut("{id:int}")]
    public Rule Update(Int32 id, RuleInput input) => service.Update(id, input);

    [HttpDelete("{id:int}")]
    public IActionResult Delete(Int32 id)
    {
        service.Delete(id);
        return NoContent();
    }

    [HttpPost("{id:int}/enable")]
    public Rule Enable(Int32 id) => service.SetEnable(id, true);

    [HttpPost("{id:int}/disable")]
    public Rule Disable(Int32 id) => service.SetEnable(id, false);

    /// <summary>用给定数据测试规则，execute=true 时执行动作</summary>
    [HttpPost("{id:int}/test")]
    public Task<RuleTestResult> Test(Int32 id, RuleTestInput input, CancellationToken cancellationToken) =>
        service.TestAsync(id, input.DeviceId, input.Values, input.EventName, input.Execute, cancellationToken);

    /// <summary>规则日志，ruleId/deviceId 为 0 表示全部</summary>
    [HttpGet("logs")]
    public PagedResult<RuleLog> Logs(Int32 ruleId = 0, Int32 deviceId = 0, Int32 pageIndex = 1, Int32 pageSize = 20) => service.GetLogs(ruleId, deviceId, pageIndex, pageSize);

    /// <summary>可用的触发类型、运算符与动作（含参数定义）</summary>
    [HttpGet("metadata")]
    public Object Metadata() => new
    {
        triggers = RuleTriggers.All,
        operators = RuleOperators.All,
        actions = engine.Actions.Select(a => new { a.Name, a.DisplayName, a.Description, a.Parameters }),
    };
}

/// <summary>告警</summary>
[ApiController]
[Route("api/alarms")]
[Tags("Rules & alarms")]
public class AlarmsController(AlarmService service) : ControllerBase
{
    [HttpGet]
    public PagedResult<Alarm> Query(AlarmStatus? status, AlarmLevels? level, Int32 deviceId = 0, Int32 ruleId = 0, Int32 pageIndex = 1, Int32 pageSize = 20) =>
        service.Query(status, level, deviceId, ruleId, pageIndex, pageSize);

    /// <summary>确认或关闭告警</summary>
    [HttpPost("{id:long}/handle")]
    public Alarm Handle(Int64 id, AlarmHandleInput input) => service.Handle(id, input);
}
