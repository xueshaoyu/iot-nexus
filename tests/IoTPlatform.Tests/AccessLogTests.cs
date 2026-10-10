using IoTPlatform;
using IoTPlatform.AccessLog;

namespace IoTPlatform.Tests;

public class AccessLogTests
{
    private static IReadOnlyDictionary<String, String?> Headers(params (String Key, String? Value)[] pairs)
    {
        var map = new Dictionary<String, String?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in pairs) map[key] = value;
        return map;
    }

    [Fact]
    public void ClientIp_IgnoresForwardedHeadersFromUntrustedPeers()
    {
        var headers = Headers(("CF-Connecting-IP", "203.0.113.8"), ("X-Forwarded-For", "198.51.100.9"));
        Assert.Equal("198.51.100.20", ClientIpResolver.Resolve("198.51.100.20", headers, ["127.0.0.1"]));
    }

    [Fact]
    public void ClientIp_PrefersCloudflareHeaderFromLoopback()
    {
        var headers = Headers(
            ("CF-Connecting-IP", "203.0.113.8"),
            ("X-Real-IP", "198.51.100.7"),
            ("X-Forwarded-For", "1.2.3.4, 203.0.113.8"),
            ("CF-IPCountry", "jp"));

        Assert.Equal("203.0.113.8", ClientIpResolver.Resolve("127.0.0.1", headers, ["127.0.0.1", "::1"]));
        Assert.Equal("JP", ClientIpResolver.Country(headers));
        Assert.Null(ClientIpResolver.Country(Headers(("CF-IPCountry", "XX"))));
        Assert.Equal("203.0.113.8", ClientIpResolver.Resolve("::ffff:127.0.0.1", headers, []));
    }

    [Fact]
    public void ClientIp_WalksForwardedForFromTheRightAndHonorsCidr()
    {
        var headers = Headers(("X-Forwarded-For", "203.0.113.5, 10.1.2.3"));
        Assert.Equal("203.0.113.5", ClientIpResolver.Resolve("10.8.0.4", headers, ["10.0.0.0/8"]));

        var real = Headers(("X-Real-IP", "192.0.2.15:443"));
        Assert.Equal("192.0.2.15", ClientIpResolver.Resolve("[::1]", real, []));
    }

    [Theory]
    [InlineData("/_framework/blazor.web.js", true)]
    [InlineData("/_blazor", true)]
    [InlineData("/_blazor/negotiate", true)]
    [InlineData("/css/app.css", true)]
    [InlineData("/favicon.ico", true)]
    [InlineData("/app.js", true)]
    [InlineData("/api/products", false)]
    [InlineData("/access-logs", false)]
    [InlineData("/products/1", false)]
    [InlineData("/cssomething", false)]
    public void PathFilter_DropsNoise(String path, Boolean skip)
    {
        var options = new AccessLogOptions();
        Assert.Equal(skip, AccessPathFilter.ShouldSkip(path, options.ExcludePrefixes, options.ExcludeExtensions));
    }

    [Fact]
    public void UserAgent_ParsesBrowserOsAndBots()
    {
        var chrome = UserAgentParser.Parse("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        Assert.Equal(("Chrome", "Windows", false), (chrome.Browser, chrome.Os, chrome.Bot));

        var edge = UserAgentParser.Parse("Mozilla/5.0 (Windows NT 10.0) AppleWebKit/537.36 Chrome/120.0.0.0 Safari/537.36 Edg/120.0.0.0");
        Assert.Equal("Edge", edge.Browser);

        var iphone = UserAgentParser.Parse("Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1");
        Assert.Equal(("Safari", "iOS", false), (iphone.Browser, iphone.Os, iphone.Bot));

        var firefox = UserAgentParser.Parse("Mozilla/5.0 (X11; Linux x86_64; rv:121.0) Gecko/20100101 Firefox/121.0");
        Assert.Equal(("Firefox", "Linux", false), (firefox.Browser, firefox.Os, firefox.Bot));

        var bot = UserAgentParser.Parse("Mozilla/5.0 (compatible; Googlebot/2.1; +http://www.google.com/bot.html)");
        Assert.True(bot.Bot);

        var curl = UserAgentParser.Parse("curl/8.5.0");
        Assert.Equal(("curl", "Unknown", false), (curl.Browser, curl.Os, curl.Bot));
    }
}

[Collection("platform-db")]
public class AccessLogQueryTests(PlatformFixture fixture)
{
    [Fact]
    public void Query_reports_total_for_matching_ip()
    {
        var ip = $"203.0.113.{Random.Shared.Next(1, 250)}";
        var row = new IoTPlatform.Data.AccessLog
        {
            Id = IoTPlatform.Data.AccessLog.Meta.Factory.Snow.NewId(),
            ClientIp = ip,
            Method = "GET",
            Path = "/api/products",
            StatusCode = 200,
            Referer = "https://demo.example.com/docs",
            RefererHost = "demo.example.com",
            Country = "JP",
            CreateTime = DateTime.Now,
        };
        row.Insert();

        var page = fixture.Get<IoTPlatform.Services.AccessLogService>().Query(null, null, ip, null, null);
        Assert.Equal(1, page.Total);
        Assert.Single(page.Items);
        Assert.Equal(ip, page.Items[0].ClientIp);
        Assert.Equal("demo.example.com", page.Items[0].RefererHost);
    }
}
