using IoTPlatform.Data;
using IoTPlatform.Services;
using Microsoft.Extensions.Localization;
using NewLife;

namespace IoTPlatform.Web.Services;

/// <summary>界面格式化</summary>
public static class Fmt
{
    /// <summary>界面本地化器，启动时注入。按调用线程的 CurrentUICulture 取翻译</summary>
    public static IStringLocalizer? Localizer { get; set; }

    private static String T(String key, params Object[] args) =>
        args.Length == 0 ? Localizer?[key].Value ?? key : Localizer?[key, args].Value ?? String.Format(key, args);

    /// <summary>按当前语言格式化文本（供非组件代码使用）</summary>
    public static String Tr(String key, params Object[] args) => T(key, args);

    /// <summary>事件类型 info/alert/error 的本地化显示</summary>
    public static String EventType(String? type) => Text(type switch { "alert" => "Alert", "error" => "Error", "info" => "Info", _ => type });

    /// <summary>枚举、协议等短词的本地化显示</summary>
    public static String Text(Object? value) => value == null ? "" : T(value.ToString()!);

    public static Boolean IsSet(DateTime t) => t.Year > 2000;

    /// <summary>时间文本中的空格替换为不换行空格，避免 "2 分钟前"、"2026-09-24 12:00" 在窄列中折行</summary>
    private static String Keep(String text) => text.Replace(' ', '\u00A0');

    public static String Time(DateTime t) => IsSet(t) ? Keep(t.ToString(t.Date == DateTime.Today ? "HH:mm:ss" : "yyyy-MM-dd HH:mm")) : "—";

    public static String Full(DateTime t) => IsSet(t) ? Keep(t.ToString("yyyy-MM-dd HH:mm:ss")) : "—";

    public static String Ago(DateTime t) => Keep(AgoText(t));

    private static String AgoText(DateTime t)
    {
        if (!IsSet(t)) return T("never");

        var span = DateTime.Now - t;
        if (span.TotalSeconds < 5) return T("just now");
        if (span.TotalSeconds < 60) return T("{0}s ago", (Int32)span.TotalSeconds);
        if (span.TotalMinutes < 60) return T("{0}m ago", (Int32)span.TotalMinutes);
        if (span.TotalHours < 24) return T("{0}h ago", (Int32)span.TotalHours);
        return T("{0}d ago", (Int32)span.TotalDays);
    }

    public static String Duration(Int64 seconds) => Keep(DurationText(seconds));

    private static String DurationText(Int64 seconds)
    {
        if (seconds <= 0) return T("{0}s", 0);

        var ts = TimeSpan.FromSeconds(seconds);
        if (ts.TotalDays >= 1) return T("{0}d {1}h", (Int32)ts.TotalDays, ts.Hours);
        if (ts.TotalHours >= 1) return T("{0}h {1}m", (Int32)ts.TotalHours, ts.Minutes);
        if (ts.TotalMinutes >= 1) return T("{0}m {1}s", ts.Minutes, ts.Seconds);
        return T("{0}s", ts.Seconds);
    }

    public static String Number(Int64 n) => n switch
    {
        >= 10_000_000 => $"{n / 1_000_000.0:0.#}M",
        >= 100_000 => $"{n / 1000.0:0.#}k",
        _ => n.ToString("N0"),
    };

    /// <summary>按枚举映射显示值，如 1 → On</summary>
    public static String Value(String? value, String? mapping, String? dataType)
    {
        if (value == null) return "—";
        if (!mapping.IsNullOrEmpty())
        {
            var map = ProductService.ParseMapping(mapping);
            var key = dataType is "bool" or "boolean" ? value is "true" or "1" ? "1" : "0" : value;
            if (map.TryGetValue(key, out var text) && text != null) return T(text);
        }

        return value;
    }

    public static String ProtocolTone(String? protocol) => protocol switch
    {
        AccessProtocols.Mqtt => "primary",
        AccessProtocols.Http => "info",
        AccessProtocols.Driver => "warning",
        _ => "",
    };

    public static String LevelTone(AlarmLevels level) => level switch
    {
        AlarmLevels.Critical => "danger",
        AlarmLevels.Warning => "warning",
        _ => "info",
    };

    public static String StatusTone(ServiceStatusKinds status) => status switch
    {
        ServiceStatusKinds.Success => "success",
        ServiceStatusKinds.Failed or ServiceStatusKinds.Timeout => "danger",
        ServiceStatusKinds.Sent => "info",
        _ => "",
    };
}
