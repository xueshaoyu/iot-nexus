using IoTPlatform.Data;
using IoTPlatform.Models;
using IoTPlatform.Services;
using Microsoft.AspNetCore.Mvc;

namespace IoTPlatform.Web.Controllers;

/// <summary>批量控制：按产品、分组（可含子分组）或设备列表批量设置属性/调用服务</summary>
/// <remarks>
/// 离线处理（scope.onlineMode）：Auto（默认）跳过离线的 MQTT/驱动设备，HTTP 设备照常排队等待拉取；
/// OnlineOnly 仅在线设备；All 全部尝试。禁用设备、缺少该属性/服务或属性只读的设备总是跳过。
/// </remarks>
[ApiController]
[Route("api/devices/batch-control")]
[Tags("Batch control")]
public class BatchControlController(BatchControlService service) : ControllerBase
{
    /// <summary>预览：展开范围并给出每台设备是否会被跳过及原因，不下发</summary>
    /// <remarks>示例：{"scope":{"type":"Group","groupId":2,"includeSubGroups":true},"kind":"property","property":"switch","value":false}</remarks>
    [HttpPost("preview")]
    public Object Preview(BatchControlInput input)
    {
        var targets = service.Resolve(input.Scope, input);
        return new
        {
            total = targets.Count,
            willRun = targets.Count(e => e.SkipReason == null),
            skipped = targets.Count(e => e.SkipReason != null),
            targets,
        };
    }

    /// <summary>执行批量控制，返回汇总与每台设备结果</summary>
    /// <remarks>
    /// 设置属性：{"scope":{"type":"Product","productId":2},"kind":"property","property":"brightness","value":40,"waitSeconds":5}
    /// 调用服务：{"scope":{"type":"Devices","deviceIds":[6,7]},"kind":"service","service":"toggle","args":{}}
    /// </remarks>
    [HttpPost]
    public Task<BatchControlResult> Execute(BatchControlInput input, CancellationToken cancellationToken) =>
        service.ExecuteAsync(input, "Api", null, cancellationToken);

    /// <summary>最近的批量任务</summary>
    [HttpGet]
    public IList<BatchJob> Recent(Int32 count = 20) => service.GetRecent(count);

    /// <summary>批量任务详情（待回复的设备按最新指令状态刷新）</summary>
    [HttpGet("{id:long}")]
    public BatchControlResult Get(Int64 id) => service.Get(id);
}
