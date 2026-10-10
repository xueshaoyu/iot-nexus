using IoTPlatform.Services;

namespace IoTPlatform.ThingModel;

/// <summary>阿里云物模型 TSL 数据类型与标识符规则</summary>
public static class TslTypes
{
    /// <summary>TSL 标准类型：int、float、double、text、date、bool、enum、struct、array</summary>
    public static readonly String[] All = ["int", "float", "double", "text", "date", "bool", "enum", "struct", "array"];

    /// <summary>历史数据类型。驱动点位仍可能使用，TSL 导出时映射为 int 或 text</summary>
    public static readonly String[] Legacy = ["string", "short", "long", "ushort", "uint", "byte", "number"];

    public static readonly String[] Events = ["info", "alert", "error"];

    public static readonly String[] AccessModes = ["r", "w", "rw"];

    public static readonly String[] CallTypes = ["async", "sync"];

    /// <summary>折叠别名，便于校验和比较。不改变仍需保留的历史类型</summary>
    public static String Normalize(String? type) => (type ?? "").Trim().ToLowerInvariant() switch
    {
        "string" or "str" => "text",
        "boolean" or "bit" => "bool",
        "integer" or "int32" or "int64" => "int",
        "single" => "float",
        "number" or "short" or "long" or "ushort" or "uint" or "byte" => "int",
        "" => "text",
        var t => t,
    };

    /// <summary>保存到物模型的规范类型。标准类型用 TSL 名称，历史数值类型原样保留</summary>
    public static String Canonical(String? type)
    {
        var raw = (type ?? "").Trim().ToLowerInvariant();
        if (raw.Length == 0 || raw is "string" or "str") return "text";
        if (raw is "boolean" or "bit") return "bool";
        if (raw is "integer" or "int32" or "int64") return "int";
        if (raw is "single") return "float";
        if (All.Contains(raw) || Legacy.Contains(raw)) return raw;

        throw new PlatformException("Unsupported data type '{0}'. Expected {1}", type, String.Join("/", All));
    }

    public static Boolean IsNumeric(String? type)
    {
        var n = Normalize(type);
        return n is "int" or "float" or "double" or "enum";
    }

    public static Boolean IsWritable(String? accessMode) =>
        accessMode != null && accessMode.Equals("rw", StringComparison.OrdinalIgnoreCase) ||
        accessMode != null && accessMode.Equals("w", StringComparison.OrdinalIgnoreCase);

    public static String CanonicalAccess(String? accessMode)
    {
        var mode = (accessMode ?? "").Trim().ToLowerInvariant();
        if (mode.Length == 0) return "r";
        return AccessModes.Contains(mode) ? mode : throw new PlatformException("Access mode must be r, w or rw");
    }

    public static String CanonicalEvent(String? type)
    {
        var t = (type ?? "info").Trim().ToLowerInvariant();
        return Events.Contains(t) ? t : "info";
    }

    public static String CanonicalCall(String? callType) =>
        (callType ?? "").Trim().Equals("sync", StringComparison.OrdinalIgnoreCase) ? "sync" : "async";
}
