using IoTPlatform.Data;
using IoTPlatform.Models;
using IoTPlatform.Services;
using Microsoft.AspNetCore.Mvc;

namespace IoTPlatform.Web.Controllers;

/// <summary>设备分组</summary>
[ApiController]
[Route("api/groups")]
[Tags("Device groups")]
public class GroupsController(DeviceGroupService service, DeviceService devices) : ControllerBase
{
    /// <summary>分组树（按层级展开，含直属与汇总设备数、在线数）</summary>
    [HttpGet]
    public IEnumerable<Object> Tree() => service.GetTree().Select(n => new
    {
        n.Group.Id,
        n.Group.Name,
        n.Group.ParentId,
        n.Group.Sort,
        n.Group.Enable,
        n.Group.Remark,
        n.Level,
        Path = DeviceGroupService.GetPath(n.Group.Id),
        n.Devices,
        n.Onlines,
        n.TotalDevices,
        n.TotalOnlines,
    });

    [HttpGet("{id:int}")]
    public DeviceGroup Get(Int32 id) => service.Get(id);

    [HttpPost]
    public DeviceGroup Create(GroupInput input) => service.Create(input);

    [HttpPut("{id:int}")]
    public DeviceGroup Update(Int32 id, GroupInput input) => service.Update(id, input);

    /// <summary>删除分组，子分组与设备移到上级</summary>
    [HttpDelete("{id:int}")]
    public IActionResult Delete(Int32 id)
    {
        service.Delete(id);
        return NoContent();
    }

    /// <summary>分组内设备（含子分组）</summary>
    [HttpGet("{id:int}/devices")]
    public PagedResult<Device> Devices(Int32 id, Boolean includeSubGroups = true, Int32 pageIndex = 1, Int32 pageSize = 50) =>
        devices.Query(new DeviceQuery { GroupId = id, IncludeSubGroups = includeSubGroups, PageIndex = pageIndex, PageSize = pageSize });

    /// <summary>批量移动设备到分组（groupId=0 移出分组）</summary>
    [HttpPost("assign")]
    public Object Assign(GroupAssignInput input) => new { moved = service.Assign(input) };
}
