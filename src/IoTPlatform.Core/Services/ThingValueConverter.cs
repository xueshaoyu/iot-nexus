using System.Collections;
using System.Globalization;
using IoTPlatform.Data;
using IoTPlatform.ThingModel;

namespace IoTPlatform.Services;

/// <summary>按物模型数据类型转换上报值</summary>
public static class ThingValueConverter
{
    private static readonly HashSet<String> IntTypes = new(StringComparer.OrdinalIgnoreCase) { "int", "int16", "int32", "int64", "long", "short", "ushort", "uint", "uint16", "uint32", "uint64", "ulong", "byte", "sbyte", "number" };
    private static readonly HashSet<String> FloatTypes = new(StringComparer.OrdinalIgnoreCase) { "float", "single", "double", "decimal" };
    private static readonly HashSet<String> BoolTypes = new(StringComparer.OrdinalIgnoreCase) { "bool", "boolean", "bit" };

    public static Boolean IsNumeric(String? dataType) => dataType != null && (IntTypes.Contains(dataType) || FloatTypes.Contains(dataType) || dataType.Equals("enum", StringComparison.OrdinalIgnoreCase));

    /// <summary>转换为物模型声明的类型，无法转换时返回原值</summary>
    public static Object? Convert(ThingProperty? definition, Object? value)
    {
        value = JsonDefaults.Normalize(value);
        if (definition == null || value == null) return value;

        var type = TslTypes.Normalize(definition.DataType);
        if (type == "int")
        {
            if (TryDouble(value, out var d)) return (Int64)Math.Round(d);
            return value;
        }
        if (type is "float" or "double")
        {
            if (TryDouble(value, out var d)) return definition.Step > 0 ? Math.Round(d, Decimals(definition.Step)) : d;
            return value;
        }
        if (type == "bool") return TryBool(value, out var b) ? b : value;
        if (type == "enum")
        {
            if (TryDouble(value, out var d)) return (Int64)Math.Round(d);
            return JsonDefaults.ToText(value);
        }
        if (type == "date") return TryDateMillis(value, out var ms) ? ms.ToString(CultureInfo.InvariantCulture) : value;
        if (type == "struct" && value is IDictionary<String, Object?>) return value;
        if (type == "array" && value is IList) return value;
        if (type == "text") return JsonDefaults.ToText(value);

        if (IntTypes.Contains(definition.DataType ?? ""))
        {
            if (TryDouble(value, out var d)) return (Int64)Math.Round(d);
            return value;
        }

        return JsonDefaults.ToText(value);
    }

    /// <summary>阿里云 date：UTC 毫秒。接受毫秒、秒和可解析的时间字符串</summary>
    public static Boolean TryDateMillis(Object? value, out Int64 millis)
    {
        millis = 0;
        switch (value)
        {
            case DateTime dt:
                millis = new DateTimeOffset(DateTime.SpecifyKind(dt, dt.Kind == DateTimeKind.Unspecified ? DateTimeKind.Local : dt.Kind)).ToUnixTimeMilliseconds();
                return true;
            case DateTimeOffset dto:
                millis = dto.ToUnixTimeMilliseconds();
                return true;
            case String s when Int64.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n):
                millis = n < 10_000_000_000 ? n * 1000 : n;
                return n > 0;
            case String s when DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed):
                millis = new DateTimeOffset(parsed).ToUnixTimeMilliseconds();
                return true;
            default:
                if (!TryDouble(value, out var d) || d <= 0) return false;
                var whole = (Int64)d;
                millis = whole < 10_000_000_000 ? whole * 1000 : whole;
                return true;
        }
    }

    /// <summary>对驱动原始值应用缩放因子</summary>
    public static Object? Scale(ThingProperty definition, Object? raw)
    {
        if (raw == null || definition.Scaling == 0 || definition.Scaling == 1) return raw;
        if (!TryDouble(raw, out var d)) return raw;

        return d * definition.Scaling;
    }

    public static Boolean TryDouble(Object? value, out Double result)
    {
        switch (value)
        {
            case Double d: result = d; return true;
            case Single f: result = f; return true;
            case Int64 l: result = l; return true;
            case Int32 i: result = i; return true;
            case Boolean b: result = b ? 1 : 0; return true;
            case IConvertible c when value is not String:
                try { result = c.ToDouble(CultureInfo.InvariantCulture); return true; }
                catch (FormatException) { result = 0; return false; }
                catch (InvalidCastException) { result = 0; return false; }
            case String s: return Double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
            default: result = 0; return false;
        }
    }

    public static Boolean TryBool(Object? value, out Boolean result)
    {
        switch (value)
        {
            case Boolean b: result = b; return true;
            case String s when s.Equals("true", StringComparison.OrdinalIgnoreCase) || s == "1" || s.Equals("on", StringComparison.OrdinalIgnoreCase): result = true; return true;
            case String s when s.Equals("false", StringComparison.OrdinalIgnoreCase) || s == "0" || s.Equals("off", StringComparison.OrdinalIgnoreCase): result = false; return true;
            default:
                if (TryDouble(value, out var d)) { result = d != 0; return true; }
                result = false;
                return false;
        }
    }

    private static Int32 Decimals(Double step)
    {
        var text = step.ToString(CultureInfo.InvariantCulture);
        var idx = text.IndexOf('.');
        return idx < 0 ? 0 : Math.Min(10, text.Length - idx - 1);
    }
}
