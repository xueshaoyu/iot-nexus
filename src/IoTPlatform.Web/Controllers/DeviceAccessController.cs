using IoTPlatform.Data;
using IoTPlatform.Protocols.Codecs;
using IoTPlatform.Services;
using Microsoft.AspNetCore.Mvc;

namespace IoTPlatform.Web.Controllers;

/// <summary>设备 HTTP 接入。适用于 HTTP 协议产品的设备，使用请求头 X-Device-Secret 认证</summary>
/// <remarks>载荷格式由产品的编解码器决定（默认 Json，与 MQTT 上报一致）。下行指令通过 GET commands 轮询拉取。</remarks>
[ApiController]
[Route("api/device-access/{code}")]
[Tags("Device HTTP access")]
public class DeviceAccessController(DataIngestService ingest, DeviceCommandService commands, PayloadCodecFactory codecs) : ControllerBase
{
    public const String SecretHeader = "X-Device-Secret";
    private const String Protocol = AccessProtocols.Http;

    /// <summary>上报属性，例如 {"temperature":25.1,"humidity":60}</summary>
    [HttpPost("properties")]
    public async Task<IActionResult> PostProperties(String code)
    {
        if (!TryAuth(code, out var device, out var error)) return error!;

        var payload = await ReadBody();
        var props = codecs.Get(device!.Product?.Codec).DecodeProperties(payload);
        if (props == null || props.Values.Count == 0) return BadRequest(new ProblemDetails { Status = 400, Title = "No properties in payload" });

        var accepted = ingest.PostProperties(device, props.Values, props.Timestamp, Protocol, "http", RemoteIp);
        return Ok(new { accepted });
    }

    /// <summary>上报事件，例如 {"type":"alert","name":"overheat","remark":"..."}</summary>
    [HttpPost("events")]
    public async Task<IActionResult> PostEvents(String code)
    {
        if (!TryAuth(code, out var device, out var error)) return error!;

        var events = codecs.Get(device!.Product?.Codec).DecodeEvents(await ReadBody());
        foreach (var e in events) ingest.PostEvent(device, e.Type, e.Name, e.Remark, e.Data, e.Timestamp, Protocol, "http", RemoteIp);

        return Ok(new { accepted = events.Count });
    }

    /// <summary>心跳</summary>
    [HttpPost("heartbeat")]
    public IActionResult Heartbeat(String code)
    {
        if (!TryAuth(code, out var device, out var error)) return error!;

        ingest.Heartbeat(device!, Protocol, "http", RemoteIp);
        return Ok(new { time = DateTime.Now });
    }

    /// <summary>拉取待执行的指令（服务调用 / 属性设置），拉取后标记为已下发</summary>
    [HttpGet("commands")]
    public IActionResult Commands(String code)
    {
        if (!TryAuth(code, out var device, out var error)) return error!;

        ingest.Heartbeat(device!, Protocol, "http", RemoteIp);
        var list = commands.FetchPending(device!).Select(e => new
        {
            id = e.Id,
            kind = e.Kind,
            name = e.Name,
            @params = JsonDefaults.ParseObject(e.InputData),
            time = e.CreateTime,
        });

        return Ok(list);
    }

    /// <summary>回复指令，例如 {"id":123,"status":2,"data":"ok"}</summary>
    [HttpPost("reply")]
    public async Task<IActionResult> Reply(String code)
    {
        if (!TryAuth(code, out var device, out var error)) return error!;

        var reply = codecs.Get(device!.Product?.Codec).DecodeReply(await ReadBody());
        if (reply == null) return BadRequest(new ProblemDetails { Status = 400, Title = "Invalid reply payload" });

        var ok = ingest.PostReply(device, reply.Id, reply.Success, reply.Data, Protocol, "http");
        return ok ? Ok(new { updated = true }) : NotFound(new ProblemDetails { Status = 404, Title = "Command not found" });
    }

    private String? RemoteIp => HttpContext.Connection.RemoteIpAddress?.MapToIPv4().ToString();

    private Boolean TryAuth(String code, out Device? device, out IActionResult? error)
    {
        var secret = Request.Headers[SecretHeader].FirstOrDefault() ?? Request.Query["secret"].FirstOrDefault();
        device = DeviceService.Authenticate(code, secret, out var reason);
        error = null;

        if (device == null)
            error = Unauthorized(new ProblemDetails { Status = 401, Title = $"Device authentication failed: {reason}" });
        else if (device.Product?.Protocol != AccessProtocols.Http)
            error = StatusCode(403, new ProblemDetails { Status = 403, Title = $"Product {device.Product?.Code} does not use the HTTP protocol" });

        return error == null;
    }

    private async Task<Byte[]> ReadBody()
    {
        using var ms = new MemoryStream();
        await Request.Body.CopyToAsync(ms);
        return ms.ToArray();
    }
}
