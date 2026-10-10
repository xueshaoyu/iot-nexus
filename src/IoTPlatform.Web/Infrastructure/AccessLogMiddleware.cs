using System.Diagnostics;
using IoTPlatform.Services;
using Microsoft.Extensions.Options;

namespace IoTPlatform.Web.Infrastructure;

/// <summary>记录管理端页面和 API。静态资源与 Blazor 心跳在服务里丢掉，写库走异步队列</summary>
public class AccessLogMiddleware(RequestDelegate next, IOptions<IoTOptions> options, AccessLogService logs)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var start = Stopwatch.GetTimestamp();
        try
        {
            await next(context);
        }
        finally
        {
            if (options.Value.AccessLog.Enable)
            {
                try
                {
                    var request = context.Request;
                    var headers = new Dictionary<String, String?>(StringComparer.OrdinalIgnoreCase);
                    foreach (var header in request.Headers)
                        headers[header.Key] = header.Value.ToString();

                    logs.Record(new AccessHit
                    {
                        RemoteAddress = context.Connection.RemoteIpAddress?.ToString(),
                        Headers = headers,
                        Method = request.Method,
                        Path = request.Path.Value ?? "/",
                        Query = request.QueryString.Value,
                        StatusCode = context.Response.StatusCode,
                        DurationMs = (Int32)Stopwatch.GetElapsedTime(start).TotalMilliseconds,
                        Host = request.Host.Value,
                    });
                }
                catch
                {
                    // 访问记录不能影响正常请求
                }
            }
        }
    }
}
