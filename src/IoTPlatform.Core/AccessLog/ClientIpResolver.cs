using System.Net;
using System.Net.Sockets;

namespace IoTPlatform.AccessLog;

/// <summary>从连接地址和转发头解析真实客户端 IP。只有直连方是本机或配置的代理时才采信转发头</summary>
public static class ClientIpResolver
{
    public const String CfConnectingIp = "CF-Connecting-IP";
    public const String XRealIp = "X-Real-IP";
    public const String XForwardedFor = "X-Forwarded-For";
    public const String CfIpCountry = "CF-IPCountry";

    public static String Resolve(String? remoteAddress, IReadOnlyDictionary<String, String?> headers, IEnumerable<String>? trustedProxies)
    {
        var remote = Normalize(remoteAddress);
        if (remote.Length == 0) remote = "0.0.0.0";
        if (!IsTrusted(remote, trustedProxies)) return remote;

        if (Normalize(Header(headers, CfConnectingIp)) is { Length: > 0 } cfIp)
            return cfIp;
        if (Normalize(Header(headers, XRealIp)) is { Length: > 0 } realIp)
            return realIp;
        var forwarded = Header(headers, XForwardedFor);
        if (!String.IsNullOrWhiteSpace(forwarded))
        {
            var hops = forwarded.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            for (var i = hops.Length - 1; i >= 0; i--)
            {
                var hop = Normalize(hops[i]);
                if (hop.Length == 0) continue;
                if (!IsTrusted(hop, trustedProxies)) return hop;
            }
        }

        return remote;
    }

    /// <summary>CF-IPCountry。XX 和空值表示未知</summary>
    public static String? Country(IReadOnlyDictionary<String, String?> headers)
    {
        var code = Header(headers, CfIpCountry);
        if (String.IsNullOrWhiteSpace(code)) return null;
        code = code.Trim().ToUpperInvariant();
        if (code.Length != 2 || code == "XX" || !code.All(Char.IsLetter)) return null;
        return code;
    }

    private static String? Header(IReadOnlyDictionary<String, String?> headers, String name)
    {
        if (headers.TryGetValue(name, out var value)) return value;
        foreach (var pair in headers)
        {
            if (pair.Key.Equals(name, StringComparison.OrdinalIgnoreCase)) return pair.Value;
        }

        return null;
    }

    public static Boolean IsTrusted(String? address, IEnumerable<String>? trustedProxies)
    {
        var ip = Normalize(address);
        if (ip.Length == 0 || !IPAddress.TryParse(ip, out var parsed)) return false;
        if (IPAddress.IsLoopback(parsed)) return true;

        if (trustedProxies == null) return false;
        foreach (var rule in trustedProxies)
        {
            if (Matches(parsed, rule)) return true;
        }

        return false;
    }

    /// <summary>去掉空白、方括号和端口，IPv4 映射地址还原成 IPv4。无法解析时返回空字符串</summary>
    public static String Normalize(String? value)
    {
        if (String.IsNullOrWhiteSpace(value)) return "";
        var text = value.Trim().Trim('"');
        if (text.Length == 0 || text.Equals("unknown", StringComparison.OrdinalIgnoreCase)) return "";

        if (text[0] == '[')
        {
            var end = text.IndexOf(']');
            if (end > 1) text = text[1..end];
        }
        else if (text.Count(c => c == ':') == 1 && text.Contains('.'))
        {
            text = text[..text.IndexOf(':')];
        }

        if (!IPAddress.TryParse(text, out var ip)) return "";
        if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
        if (ip.AddressFamily == AddressFamily.InterNetworkV6) ip = ip.MapToIPv6();
        return ip.ToString();
    }

    private static Boolean Matches(IPAddress address, String? rule)
    {
        if (String.IsNullOrWhiteSpace(rule)) return false;
        var text = rule.Trim();
        var slash = text.IndexOf('/');
        var networkText = slash < 0 ? text : text[..slash];
        if (!IPAddress.TryParse(networkText, out var network)) return false;
        if (network.IsIPv4MappedToIPv6) network = network.MapToIPv4();
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();

        var prefix = slash < 0
            ? network.AddressFamily == AddressFamily.InterNetwork ? 32 : 128
            : Int32.TryParse(text[(slash + 1)..], out var bits) ? bits : -1;
        var max = network.AddressFamily == AddressFamily.InterNetwork ? 32 : 128;
        if (prefix < 0 || prefix > max || address.AddressFamily != network.AddressFamily) return false;

        var left = address.GetAddressBytes();
        var right = network.GetAddressBytes();
        var full = prefix / 8;
        var remain = prefix % 8;
        for (var i = 0; i < full; i++)
        {
            if (left[i] != right[i]) return false;
        }

        if (remain == 0) return true;
        var mask = (Byte)(0xFF << (8 - remain));
        return (left[full] & mask) == (right[full] & mask);
    }
}
