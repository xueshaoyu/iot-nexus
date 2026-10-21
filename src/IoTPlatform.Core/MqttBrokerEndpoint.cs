using System.Text.RegularExpressions;
using IoTPlatform.Services;

namespace IoTPlatform;

/// <summary>外部 MQTT 接入地址：scheme / host / port / path，并合成 NewLife.MQTT 的 Server URL</summary>
public readonly record struct MqttBrokerEndpoint(String Scheme, String Host, Int32 Port, String Path, Boolean UseTls)
{
    public const String Tcp = "tcp";
    public const String Ssl = "ssl";
    public const String Ws = "ws";
    public const String Wss = "wss";

    static readonly Regex KnownScheme = new(@"^(?<s>tcp|ssl|tls|mqtt|mqtts|ws|wss|http|https):(?<rest>.*)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public String Server => Build(Scheme, Host, Port, Path);

    public Boolean IsWebSocket => IsWebSocketScheme(Scheme);

    public static IReadOnlyList<String> Schemes { get; } = [Tcp, Ssl, Ws, Wss];

    public static Boolean IsKnown(String? scheme)
    {
        var s = Canonicalize(scheme);
        return s is Tcp or Ssl or Ws or Wss;
    }

    public static Boolean IsWebSocketScheme(String? scheme)
    {
        var s = Canonicalize(scheme);
        return s is Ws or Wss;
    }

    public static Boolean IsTlsScheme(String? scheme)
    {
        var s = Canonicalize(scheme);
        return s is Ssl or Wss;
    }

    public static Int32 DefaultPort(String? scheme) => Canonicalize(scheme) switch
    {
        Ssl => 8883,
        Ws => 8083,
        Wss => 443,
        _ => 1883,
    };

    public static String DefaultPath(String? scheme) => IsWebSocketScheme(scheme) ? "/mqtt" : "";

    public static String? Canonicalize(String? scheme)
    {
        if (String.IsNullOrWhiteSpace(scheme)) return null;
        return scheme.Trim().ToLowerInvariant() switch
        {
            "tcp" or "mqtt" => Tcp,
            "ssl" or "tls" or "mqtts" => Ssl,
            "ws" or "http" => Ws,
            "wss" or "https" => Wss,
            _ => null,
        };
    }

    /// <summary>把 <c>wss:/host</c> 这种少写一杠的地址修成合法 URL，再解析</summary>
    public static String Repair(String? raw)
    {
        raw = raw?.Trim() ?? "";
        if (raw.Length == 0) return "";
        var m = KnownScheme.Match(raw);
        if (!m.Success) return raw;
        var rest = m.Groups["rest"].Value;
        if (rest.StartsWith("//", StringComparison.Ordinal)) return raw;
        if (rest.StartsWith('/')) rest = rest[1..];
        return $"{m.Groups["s"].Value.ToLowerInvariant()}://{rest}";
    }

    public static MqttBrokerEndpoint Parse(String? server, Boolean useTls = false)
    {
        var raw = Repair(server);
        if (String.IsNullOrWhiteSpace(raw))
            return new(useTls ? Ssl : Tcp, "", DefaultPort(useTls ? Ssl : Tcp), "", useTls);

        if (!raw.Contains("://", StringComparison.Ordinal))
        {
            var scheme = useTls ? Ssl : Tcp;
            var host = raw;
            var port = DefaultPort(scheme);
            var slash = raw.IndexOf('/');
            var path = "";
            if (slash >= 0)
            {
                path = raw[slash..];
                host = raw[..slash];
            }
            var colon = host.LastIndexOf(':');
            if (colon > 0 && Int32.TryParse(host[(colon + 1)..], out var hostPort) && hostPort is > 0 and <= 65535)
            {
                port = hostPort;
                host = host[..colon];
            }
            return Normalize(scheme, host, port, path, useTls);
        }

        if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri) || String.IsNullOrEmpty(uri.Host))
            throw new PlatformException("MQTT broker server is required");

        var parsedScheme = Canonicalize(uri.Scheme) ?? (useTls ? Ssl : Tcp);
        var parsedPath = uri.AbsolutePath is "/" or "" ? (IsWebSocketScheme(parsedScheme) ? uri.AbsolutePath : "") : uri.AbsolutePath;
        var uriPort = uri.IsDefaultPort || uri.Port <= 0 ? DefaultPort(parsedScheme) : uri.Port;
        return Normalize(parsedScheme, uri.Host, uriPort, parsedPath, useTls);
    }

    public static MqttBrokerEndpoint From(ExternalMqttOptions broker)
    {
        var parsed = Parse(broker.Server, broker.UseTls);
        var scheme = Canonicalize(broker.Scheme) ?? parsed.Scheme;
        var host = String.IsNullOrWhiteSpace(broker.Host) ? parsed.Host : broker.Host.Trim();
        var port = broker.Port is > 0 and <= 65535 ? broker.Port : parsed.Port;
        var path = broker.Path != null ? broker.Path : parsed.Path;
        return Normalize(scheme, host, port, path, broker.UseTls);
    }

    public static void Apply(ExternalMqttOptions broker)
    {
        var endpoint = From(broker);
        broker.Scheme = endpoint.Scheme;
        broker.Host = endpoint.Host;
        broker.Port = endpoint.Port;
        broker.Path = endpoint.Path;
        broker.Server = endpoint.Server;
        broker.UseTls = endpoint.UseTls;
    }

    public static String Build(String? scheme, String? host, Int32 port, String? path)
    {
        scheme = Canonicalize(scheme) ?? Tcp;
        host = host?.Trim().TrimEnd('/') ?? "";
        if (String.IsNullOrEmpty(host)) return "";
        if (port is < 1 or > 65535) port = DefaultPort(scheme);
        path = NormalizePath(scheme, path);
        var authority = $"{scheme}://{host}:{port}";
        return path.Length == 0 ? authority : authority + path;
    }

    static MqttBrokerEndpoint Normalize(String? scheme, String? host, Int32 port, String? path, Boolean useTls)
    {
        scheme = Canonicalize(scheme) ?? (useTls ? Ssl : Tcp);
        if (useTls && scheme == Tcp) scheme = Ssl;
        host = host?.Trim().TrimEnd('/') ?? "";
        if (port is < 1 or > 65535) port = DefaultPort(scheme);
        path = NormalizePath(scheme, path);
        return new(scheme, host, port, path, IsTlsScheme(scheme));
    }

    static String NormalizePath(String? scheme, String? path)
    {
        if (!IsWebSocketScheme(scheme)) return "";
        path = path?.Trim() ?? "";
        if (path.Length == 0) return DefaultPath(scheme);
        if (!path.StartsWith('/')) path = "/" + path;
        if (path.Length > 1) path = path.TrimEnd('/');
        return path;
    }
}
