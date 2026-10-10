using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IoTPlatform.Services;

/// <summary>平台统一的Json选项与值转换</summary>
public static class JsonDefaults
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static String Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);

    public static T? Deserialize<T>(String? json)
    {
        if (String.IsNullOrWhiteSpace(json)) return default;

        try
        {
            return JsonSerializer.Deserialize<T>(json, Options);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    /// <summary>解析Json对象为字典，值归一化为 String/Int64/Double/Boolean/null 或嵌套字典/列表</summary>
    public static Dictionary<String, Object?>? ParseObject(String? json)
    {
        if (String.IsNullOrWhiteSpace(json)) return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.ValueKind == JsonValueKind.Object ? ToDictionary(doc.RootElement) : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static Dictionary<String, Object?> ToDictionary(JsonElement element)
    {
        var dic = new Dictionary<String, Object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in element.EnumerateObject()) dic[p.Name] = Normalize(p.Value);

        return dic;
    }

    /// <summary>把 JsonElement 等框架类型转为基础类型，便于比较和存储</summary>
    public static Object? Normalize(Object? value) => value switch
    {
        JsonElement je => Normalize(je),
        _ => value,
    };

    public static Object? Normalize(JsonElement je) => je.ValueKind switch
    {
        JsonValueKind.String => je.GetString(),
        JsonValueKind.Number => je.TryGetInt64(out var l) ? (Object)l : je.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null or JsonValueKind.Undefined => null,
        JsonValueKind.Object => ToDictionary(je),
        JsonValueKind.Array => je.EnumerateArray().Select(Normalize).ToList(),
        _ => je.GetRawText(),
    };

    /// <summary>值转为存储文本</summary>
    public static String? ToText(Object? value) => value switch
    {
        null => null,
        String s => s,
        Boolean b => b ? "true" : "false",
        Double d => d.ToString("0.######", CultureInfo.InvariantCulture),
        Single f => f.ToString("0.######", CultureInfo.InvariantCulture),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        IDictionary<String, Object?> or System.Collections.IList => JsonSerializer.Serialize(value, Options),
        _ => value.ToString(),
    };
}
