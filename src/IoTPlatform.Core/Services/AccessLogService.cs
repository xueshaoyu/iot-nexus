using IoTPlatform.AccessLog;
using IoTPlatform.Data;
using IoTPlatform.Models;
using Microsoft.Extensions.Options;
using NewLife;
using NewLife.Data;
using XCode;

namespace IoTPlatform.Services;

public record AccessLogSummary(Int64 Pv, Int32 UniqueIps, IList<NameCount> Referers, IList<NameCount> Countries, IList<NameCount> Browsers);

public record NameCount(String Name, Int32 Count);

/// <summary>一次已完成的管理端请求</summary>
public sealed class AccessHit
{
    public String? RemoteAddress { get; init; }

    public IReadOnlyDictionary<String, String?> Headers { get; init; } = new Dictionary<String, String?>();

    public String Method { get; init; } = "GET";

    public String Path { get; init; } = "/";

    public String? Query { get; init; }

    public Int32 StatusCode { get; init; }

    public Int32 DurationMs { get; init; }

    public String? Host { get; init; }
}

/// <summary>访问记录：请求线程只入队，XCode 异步队列批量落库</summary>
public class AccessLogService(IOptions<IoTOptions> options)
{
    public void Record(AccessHit hit)
    {
        var cfg = options.Value.AccessLog;
        if (!cfg.Enable) return;
        if (AccessPathFilter.ShouldSkip(hit.Path, cfg.ExcludePrefixes, cfg.ExcludeExtensions)) return;

        var headers = hit.Headers;
        var ua = Header(headers, "User-Agent");
        var agent = UserAgentParser.Parse(ua);
        var referer = Header(headers, "Referer");
        var now = DateTime.Now;
        var row = new Data.AccessLog
        {
            Id = Data.AccessLog.Meta.Factory.Snow.NewId(),
            ClientIp = ClientIpResolver.Resolve(hit.RemoteAddress, headers, cfg.TrustedProxies).Cut(50) ?? "",
            Country = ClientIpResolver.Country(headers),
            Referer = Cut(referer, 500),
            RefererHost = Cut(RefererHost(referer), 50),
            UserAgent = Cut(ua, 500),
            Browser = agent.Browser,
            Os = agent.Os,
            Bot = agent.Bot,
            Method = (hit.Method.IsNullOrEmpty() ? "GET" : hit.Method).Cut(16) ?? "GET",
            Path = (hit.Path.IsNullOrEmpty() ? "/" : hit.Path).Cut(300) ?? "/",
            QueryString = Cut(hit.Query?.TrimStart('?'), 500),
            StatusCode = hit.StatusCode,
            DurationMs = hit.DurationMs,
            Host = Cut(hit.Host, 50),
            CreateTime = now,
        };
        row.SaveAsync(SqliteSettings.SaveAsyncDelayMs);
    }

    public PagedResult<Data.AccessLog> Query(DateTime? from, DateTime? to, String? ip, String? path, String? referer, Int32 pageIndex = 1, Int32 pageSize = 20)
    {
        var exp = Build(from, to, ip, path, referer);
        var page = new PageParameter
        {
            PageIndex = Math.Max(1, pageIndex),
            PageSize = Math.Clamp(pageSize, 1, 200),
            Sort = nameof(Data.AccessLog.Id),
            Desc = true,
            RetrieveTotalCount = true,
        };
        var list = Data.AccessLog.FindAll(exp, page);
        return new PagedResult<Data.AccessLog>(list, page.TotalCount, page.PageIndex, page.PageSize);
    }

    /// <summary>统计一段时间内的 PV、独立 IP 和来源/地区/浏览器 Top</summary>
    public AccessLogSummary Summarize(DateTime from, DateTime to)
    {
        var rows = Data.AccessLog.FindAll(Data.AccessLog._.CreateTime >= from & Data.AccessLog._.CreateTime < to);
        return new AccessLogSummary(
            rows.Count,
            rows.Select(e => e.ClientIp).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            Top(rows, e => e.RefererHost.IsNullOrEmpty() ? "" : e.RefererHost!),
            Top(rows, e => e.Country.IsNullOrEmpty() ? "" : e.Country!),
            Top(rows, e => e.Browser.IsNullOrEmpty() ? "Unknown" : e.Browser!));
    }

    public Int32 Cleanup(Int32 retentionDays)
    {
        if (retentionDays <= 0) return 0;

        var end = DateTime.Today.AddDays(-retentionDays);
        var id = Data.AccessLog.Meta.Factory.Snow.GetId(end);
        return Data.AccessLog.Delete(Data.AccessLog._.Id < id);
    }

    private static WhereExpression Build(DateTime? from, DateTime? to, String? ip, String? path, String? referer)
    {
        var exp = new WhereExpression();
        if (from != null) exp &= Data.AccessLog._.CreateTime >= from.Value;
        if (to != null) exp &= Data.AccessLog._.CreateTime <= to.Value;
        if (!ip.IsNullOrWhiteSpace()) exp &= Data.AccessLog._.ClientIp.Contains(ip.Trim());
        if (!path.IsNullOrWhiteSpace()) exp &= Data.AccessLog._.Path.Contains(path.Trim());
        if (!referer.IsNullOrWhiteSpace()) exp &= Data.AccessLog._.Referer.Contains(referer.Trim()) | Data.AccessLog._.RefererHost.Contains(referer.Trim());
        return exp;
    }

    private static List<NameCount> Top(IList<Data.AccessLog> rows, Func<Data.AccessLog, String> key) =>
        rows.GroupBy(key, StringComparer.OrdinalIgnoreCase)
            .Select(g => new NameCount(g.Key, g.Count()))
            .OrderByDescending(e => e.Count)
            .ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToList();

    private static String? Header(IReadOnlyDictionary<String, String?> headers, String name)
    {
        if (headers.TryGetValue(name, out var value)) return value;
        foreach (var pair in headers)
        {
            if (pair.Key.Equals(name, StringComparison.OrdinalIgnoreCase)) return pair.Value;
        }

        return null;
    }

    private static String? Cut(String? value, Int32 max)
    {
        if (value.IsNullOrEmpty()) return value;
        return value!.Length <= max ? value : value[..max];
    }

    public static String? RefererHost(String? referer)
    {
        if (referer.IsNullOrWhiteSpace()) return null;
        return Uri.TryCreate(referer.Trim(), UriKind.Absolute, out var uri) ? uri.Host : null;
    }
}
