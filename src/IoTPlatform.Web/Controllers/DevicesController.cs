using IoTPlatform.Data;
using IoTPlatform.Models;
using IoTPlatform.Services;
using IoTPlatform.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace IoTPlatform.Web.Controllers;

/// <summary>设备管理</summary>
[ApiController]
[Route("api/devices")]
[Tags("Devices")]
public class DevicesController(DeviceService service, DeviceCommandService commands, ConnectionInfoService connectionInfo) : ControllerBase
{
    /// <summary>设备列表。groupId=-1 表示未分组</summary>
    [HttpGet]
    public PagedResult<Device> Query([FromQuery] DeviceQuery query) => service.Query(query);

    [HttpGet("{id:int}")]
    public Device Get(Int32 id) => service.Get(id);

    [HttpGet("by-code/{code}")]
    public Device GetByCode(String code) => service.GetByCode(code);

    /// <summary>注册设备。编码、密钥留空自动生成</summary>
    [HttpPost]
    public ActionResult<Device> Create(DeviceInput input)
    {
        var device = service.Create(input);
        return CreatedAtAction(nameof(Get), new { id = device.Id }, device);
    }

    /// <summary>批量注册</summary>
    [HttpPost("batch")]
    public IList<Device> CreateBatch(DeviceBatchInput input) => service.CreateBatch(input);

    [HttpPut("{id:int}")]
    public Device Update(Int32 id, DeviceInput input) => service.Update(id, input);

    [HttpDelete("{id:int}")]
    public IActionResult Delete(Int32 id)
    {
        service.Delete(id);
        return NoContent();
    }

    [HttpPost("{id:int}/enable")]
    public Device Enable(Int32 id) => service.SetEnable(id, true);

    /// <summary>禁用设备，在线连接会被断开</summary>
    [HttpPost("{id:int}/disable")]
    public Device Disable(Int32 id) => service.SetEnable(id, false);

    [HttpPost("{id:int}/reset-secret")]
    public Device ResetSecret(Int32 id) => service.ResetSecret(id);

    /// <summary>接入信息：MQTT 连接参数与主题，HTTP 接入地址</summary>
    [HttpGet("{id:int}/connection")]
    public DeviceConnectionInfo Connection(Int32 id) => connectionInfo.Get(service.Get(id), Request);

    /// <summary>设备影子（物模型属性 + 最新值）</summary>
    [HttpGet("{id:int}/properties")]
    public IList<DeviceShadowItem> Properties(Int32 id) => service.GetShadow(id);

    /// <summary>历史数据</summary>
    [HttpGet("{id:int}/data")]
    public PagedResult<DeviceData> Data(Int32 id, String? name, DateTime? start, DateTime? end, Int32 pageIndex = 1, Int32 pageSize = 50) =>
        service.GetData(id, name, start, end, pageIndex, pageSize);

    [HttpGet("{id:int}/events")]
    public PagedResult<DeviceEvent> Events(Int32 id, Int32 pageIndex = 1, Int32 pageSize = 20) => service.GetEvents(id, pageIndex, pageSize);

    /// <summary>生命周期历史：上下线、启停、指令、规则</summary>
    [HttpGet("{id:int}/history")]
    public PagedResult<DeviceHistory> History(Int32 id, Int32 pageIndex = 1, Int32 pageSize = 20) => service.GetHistory(id, pageIndex, pageSize);

    /// <summary>下行指令记录</summary>
    [HttpGet("{id:int}/commands")]
    public PagedResult<ServiceRecord> Commands(Int32 id, Int32 pageIndex = 1, Int32 pageSize = 20) => service.GetServiceRecords(id, pageIndex, pageSize);

    /// <summary>调用设备服务。waitSeconds&gt;0 时等待设备回复</summary>
    [HttpPost("{id:int}/services/invoke")]
    public Task<ServiceRecord> Invoke(Int32 id, ServiceInvokeInput input, CancellationToken cancellationToken) =>
        commands.InvokeServiceAsync(service.Get(id), input.Service, input.Args, "Api", input.WaitSeconds, cancellationToken);

    /// <summary>设置设备属性（读写属性）</summary>
    [HttpPost("{id:int}/properties/set")]
    public Task<ServiceRecord> SetProperties(Int32 id, PropertySetInput input, CancellationToken cancellationToken) =>
        commands.SetPropertiesAsync(service.Get(id), input.Values, "Api", input.WaitSeconds, cancellationToken);
}
