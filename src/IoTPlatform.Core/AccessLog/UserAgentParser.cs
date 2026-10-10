using System.Text.RegularExpressions;

namespace IoTPlatform.AccessLog;

/// <summary>从 User-Agent 抽出浏览器、系统和是否爬虫。不依赖外部库</summary>
public static partial class UserAgentParser
{
    public static UserAgentInfo Parse(String? userAgent)
    {
        var ua = userAgent?.Trim() ?? "";
        if (ua.Length == 0) return new UserAgentInfo("Unknown", "Unknown", false);

        return new UserAgentInfo(Browser(ua), Os(ua), IsBot(ua));
    }

    public static Boolean IsBot(String ua)
    {
        if (ua.Contains("Cubot", StringComparison.OrdinalIgnoreCase)) return false;
        if (KnownBot().IsMatch(ua)) return true;
        return BotToken().IsMatch(ua);
    }

    private static String Browser(String ua)
    {
        if (ua.Contains("Edg/", StringComparison.Ordinal) || ua.Contains("EdgA/", StringComparison.Ordinal) || ua.Contains("Edge/", StringComparison.Ordinal))
            return "Edge";
        if (ua.Contains("OPR/", StringComparison.Ordinal) || ua.Contains("Opera", StringComparison.Ordinal))
            return "Opera";
        if (ua.Contains("Chrome/", StringComparison.Ordinal) || ua.Contains("CriOS/", StringComparison.Ordinal))
            return "Chrome";
        if (ua.Contains("Firefox/", StringComparison.Ordinal) || ua.Contains("FxiOS/", StringComparison.Ordinal))
            return "Firefox";
        if (ua.Contains("Safari/", StringComparison.Ordinal))
            return "Safari";
        if (ua.StartsWith("curl/", StringComparison.OrdinalIgnoreCase))
            return "curl";
        if (ua.StartsWith("Wget/", StringComparison.OrdinalIgnoreCase))
            return "Wget";
        if (ua.Contains("python-requests", StringComparison.OrdinalIgnoreCase) || ua.StartsWith("Python/", StringComparison.OrdinalIgnoreCase))
            return "Python";
        if (ua.StartsWith("Go-http-client", StringComparison.OrdinalIgnoreCase))
            return "Go";
        return "Other";
    }

    private static String Os(String ua)
    {
        if (ua.Contains("Android", StringComparison.Ordinal)) return "Android";
        if (ua.Contains("iPhone", StringComparison.Ordinal) || ua.Contains("iPad", StringComparison.Ordinal) || ua.Contains("iOS", StringComparison.Ordinal))
            return "iOS";
        if (ua.Contains("Windows", StringComparison.Ordinal)) return "Windows";
        if (ua.Contains("Mac OS", StringComparison.Ordinal) || ua.Contains("Macintosh", StringComparison.Ordinal)) return "macOS";
        if (ua.Contains("Linux", StringComparison.Ordinal)) return "Linux";
        return "Unknown";
    }

    [GeneratedRegex("googlebot|bingbot|baiduspider|yandex|duckduckbot|slurp|facebookexternalhit|twitterbot|linkedinbot|semrush|ahrefs|petalbot|bytespider|gptbot|claudebot|amazonbot|headlesschrome", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex KnownBot();

    [GeneratedRegex(@"(?:^|[^A-Za-z])(?:bot|spider|crawler)(?:[^A-Za-z]|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BotToken();
}

public readonly record struct UserAgentInfo(String Browser, String Os, Boolean Bot);
