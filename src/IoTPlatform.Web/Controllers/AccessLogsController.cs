using IoTPlatform.Models;
using IoTPlatform.Services;
using Microsoft.AspNetCore.Mvc;

namespace IoTPlatform.Web.Controllers;

/// <summary>管理端访问记录</summary>
[ApiController]
[Route("api/access-logs")]
[Tags("Access logs")]
public class AccessLogsController(AccessLogService service) : ControllerBase
{
    /// <summary>分页查询。时间、IP、路径、来源均可筛选</summary>
    [HttpGet]
    public PagedResult<global::IoTPlatform.Data.AccessLog> Query(DateTime? from, DateTime? to, String? ip, String? path, String? referer, Int32 pageIndex = 1, Int32 pageSize = 20) =>
        service.Query(from, to, ip, path, referer, pageIndex, pageSize);

    /// <summary>PV、独立 IP、来源 / 地区 / 浏览器 Top。默认统计今天</summary>
    [HttpGet("summary")]
    public AccessLogSummary Summary(DateTime? from, DateTime? to)
    {
        var start = from ?? DateTime.Today;
        var end = to ?? DateTime.Today.AddDays(1);
        if (end <= start) end = start.AddDays(1);
        return service.Summarize(start, end);
    }
}
