using System.Text.Json;
using IoTPlatform.Data;
using IoTPlatform.Services;

namespace IoTPlatform.Protocols.Codecs;

/// <summary>标准 Json 编解码，兼容 NewLife.IoT 的 DataModel/EventModel/ServiceReplyModel</summary>
/// <remarks>
/// 属性上报支持三种写法：
///   扁平对象   {"temperature":25.1,"humidity":60}
///   带时间戳   {"ts":1700000000000,"params":{"temperature":25.1}}
///   数据数组   {"items":[{"name":"temperature","value":"25.1","time":1700000000000}]}
/// 事件上报：{"type":"alert","name":"overheat","remark":"...","data":{...},"time":ms}，也可为数组。
/// 回复：{"id":123,"status":2,"data":"..."}，status 取 NewLife ServiceStatus（2=已完成，4=错误），或使用 "success":true。
/// </remarks>
public class JsonPayloadCodec : IPayloadCodec
{
    public const String CodecName = "Json";

    private static readonly HashSet<String> Reserved = new(StringComparer.OrdinalIgnoreCase) { "ts", "time", "timestamp", "id" };

    public String Name => CodecName;

    public String Description => "Plain JSON: {\"temperature\":25.1} / {\"ts\":..,\"params\":{..}} / NewLife DataModel items";

    public UplinkProperties? DecodeProperties(ReadOnlySpan<Byte> payload)
    {
        using var doc = Parse(payload);
        if (doc == null || doc.RootElement.ValueKind != JsonValueKind.Object) return null;

        var root = doc.RootElement;
        var ts = ReadTimestamp(root);
        var values = new Dictionary<String, Object?>(StringComparer.OrdinalIgnoreCase);

        if (root.TryGetProperty("params", out var ps) && ps.ValueKind == JsonValueKind.Object)
        {
            foreach (var p in ps.EnumerateObject()) values[p.Name] = JsonDefaults.Normalize(p.Value);
        }
        else if (root.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in items.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty("name", out var n) || n.GetString() is not { Length: > 0 } name) continue;

                values[name] = item.TryGetProperty("value", out var v) ? JsonDefaults.Normalize(v) : null;
                if (ts == 0) ts = ReadTimestamp(item);
            }
        }
        else
        {
            foreach (var p in root.EnumerateObject())
            {
                if (!Reserved.Contains(p.Name)) values[p.Name] = JsonDefaults.Normalize(p.Value);
            }
        }

        return new UplinkProperties(values, ts);
    }

    public IList<UplinkEvent> DecodeEvents(ReadOnlySpan<Byte> payload, String? topicHint = null)
    {
        using var doc = Parse(payload);
        var list = new List<UplinkEvent>();
        if (doc == null) return list;

        var root = doc.RootElement;
        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in root.EnumerateArray()) AddEvent(list, item);
        }
        else if (root.ValueKind == JsonValueKind.Object)
        {
            if (root.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
                foreach (var item in items.EnumerateArray()) AddEvent(list, item);
            else
                AddEvent(list, root);
        }

        return list;
    }

    public UplinkReply? DecodeReply(ReadOnlySpan<Byte> payload)
    {
        using var doc = Parse(payload);
        if (doc == null || doc.RootElement.ValueKind != JsonValueKind.Object) return null;

        var root = doc.RootElement;
        if (!root.TryGetProperty("id", out var idElement)) return null;

        var id = idElement.ValueKind == JsonValueKind.Number ? idElement.GetInt64() : Int64.TryParse(idElement.GetString(), out var v) ? v : 0;
        if (id == 0) return null;

        var success = true;
        if (root.TryGetProperty("success", out var s) && s.ValueKind is JsonValueKind.True or JsonValueKind.False)
            success = s.GetBoolean();
        else if (root.TryGetProperty("status", out var st))
            success = st.ValueKind == JsonValueKind.Number ? st.GetInt32() == 2 : st.GetString()?.ToLowerInvariant() is "success" or "ok" or "done";
        else if (root.TryGetProperty("code", out var code) && code.ValueKind == JsonValueKind.Number)
            success = code.GetInt32() is 0 or 200;

        String? data = null;
        if (root.TryGetProperty("data", out var d))
            data = d.ValueKind == JsonValueKind.String ? d.GetString() : d.GetRawText();

        return new UplinkReply(id, success, data);
    }

    public Byte[] EncodeServiceInvoke(ServiceRecord record, String service, IDictionary<String, Object?> args) =>
        JsonSerializer.SerializeToUtf8Bytes(new Dictionary<String, Object?>
        {
            ["id"] = record.Id,
            ["name"] = service,
            ["params"] = args,
            ["time"] = new DateTimeOffset(record.CreateTime).ToUnixTimeMilliseconds(),
        }, JsonDefaults.Options);

    public Byte[] EncodePropertySet(ServiceRecord record, IDictionary<String, Object?> values) =>
        JsonSerializer.SerializeToUtf8Bytes(new Dictionary<String, Object?>
        {
            ["id"] = record.Id,
            ["params"] = values,
            ["time"] = new DateTimeOffset(record.CreateTime).ToUnixTimeMilliseconds(),
        }, JsonDefaults.Options);

    private static void AddEvent(List<UplinkEvent> list, JsonElement e)
    {
        if (e.ValueKind != JsonValueKind.Object) return;

        var name = GetString(e, "name") ?? GetString(e, "identifier");
        if (String.IsNullOrWhiteSpace(name)) return;

        IDictionary<String, Object?>? data = null;
        if (e.TryGetProperty("data", out var d) && d.ValueKind == JsonValueKind.Object) data = JsonDefaults.ToDictionary(d);

        list.Add(new UplinkEvent(name, GetString(e, "type"), GetString(e, "remark"), data, ReadTimestamp(e)));
    }

    internal static JsonDocument? Parse(ReadOnlySpan<Byte> payload)
    {
        if (payload.IsEmpty) return null;

        try
        {
            var reader = new Utf8JsonReader(payload, new JsonReaderOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
            return JsonDocument.ParseValue(ref reader);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    internal static Int64 ReadTimestamp(JsonElement e)
    {
        foreach (var name in new[] { "ts", "time", "timestamp" })
        {
            if (e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var ts))
                return ts < 100_000_000_000 ? ts * 1000 : ts;
        }

        return 0;
    }

    internal static String? GetString(JsonElement e, String name) =>
        e.TryGetProperty(name, out var v) ? v.ValueKind == JsonValueKind.String ? v.GetString() : v.ValueKind == JsonValueKind.Null ? null : v.GetRawText() : null;
}
