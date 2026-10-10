namespace IoTPlatform.AccessLog;

/// <summary>丢掉静态资源、Blazor 框架和 SignalR 心跳，避免访问记录被噪声淹没</summary>
public static class AccessPathFilter
{
    public static Boolean ShouldSkip(String? path, IEnumerable<String>? prefixes, IEnumerable<String>? extensions)
    {
        var text = path?.Trim() ?? "";
        var query = text.IndexOf('?');
        if (query >= 0) text = text[..query];
        if (text.Length == 0) return true;
        if (!text.StartsWith('/')) text = "/" + text;

        if (prefixes != null)
        {
            foreach (var raw in prefixes)
            {
                if (String.IsNullOrWhiteSpace(raw)) continue;
                var rule = raw.Trim();
                if (!rule.StartsWith('/')) rule = "/" + rule;
                if (text.Equals(rule, StringComparison.OrdinalIgnoreCase)) return true;
                var dir = rule.EndsWith('/') ? rule : rule + "/";
                if (text.StartsWith(dir, StringComparison.OrdinalIgnoreCase)) return true;
            }
        }

        var dot = text.LastIndexOf('.');
        var slash = text.LastIndexOf('/');
        if (dot > slash && extensions != null)
        {
            var ext = text[dot..];
            foreach (var item in extensions)
            {
                if (String.IsNullOrWhiteSpace(item)) continue;
                var expected = item.StartsWith('.') ? item : "." + item;
                if (ext.Equals(expected, StringComparison.OrdinalIgnoreCase)) return true;
            }
        }

        return false;
    }
}
