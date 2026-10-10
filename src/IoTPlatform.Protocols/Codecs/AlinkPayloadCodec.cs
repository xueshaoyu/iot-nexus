using System.Text.Json;
using IoTPlatform.Data;
using IoTPlatform.Services;

namespace IoTPlatform.Protocols.Codecs;

/// <summary>阿里云 Alink Json 格式，方便已有 Alink 设备固件迁移</summary>
/// <remarks>
/// 属性：{"id":"1","version":"1.0","params":{"temp":{"value":23.6,"time":1524448722000},"hum":60},"method":"thing.event.property.post"}
/// 事件：{"id":"2","params":{"value":{"code":1},"time":1524448722000},"method":"thing.event.overheat.post"}
/// 回复：{"id":"123","code":200,"data":{}}
/// </remarks>
public class AlinkPayloadCodec : IPayloadCodec
{
    public String Name => "Alink";

    public String Description => "Aliyun Alink JSON (thing.event.property.post / thing.service.*)";

    public UplinkProperties? DecodeProperties(ReadOnlySpan<Byte> payload)
    {
        using var doc = JsonPayloadCodec.Parse(payload);
        if (doc == null || !doc.RootElement.TryGetProperty("params", out var ps) || ps.ValueKind != JsonValueKind.Object) return null;

        Int64 ts = 0;
        var values = new Dictionary<String, Object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in ps.EnumerateObject())
        {
            if (p.Value.ValueKind == JsonValueKind.Object && p.Value.TryGetProperty("value", out var v))
            {
                values[p.Name] = JsonDefaults.Normalize(v);
                if (ts == 0) ts = JsonPayloadCodec.ReadTimestamp(p.Value);
            }
            else
            {
                values[p.Name] = JsonDefaults.Normalize(p.Value);
            }
        }

        return new UplinkProperties(values, ts);
    }

    public IList<UplinkEvent> DecodeEvents(ReadOnlySpan<Byte> payload, String? topicHint = null)
    {
        using var doc = JsonPayloadCodec.Parse(payload);
        if (doc == null || doc.RootElement.ValueKind != JsonValueKind.Object) return [];

        var root = doc.RootElement;
        var method = JsonPayloadCodec.GetString(root, "method") ?? "";
        var parts = method.Split('.');
        var name = parts.Length >= 4 && parts[0] == "thing" && parts[1] == "event" ? parts[2] : JsonPayloadCodec.GetString(root, "name");
        if (String.IsNullOrWhiteSpace(name)) return [];

        IDictionary<String, Object?>? data = null;
        Int64 ts = 0;
        if (root.TryGetProperty("params", out var ps) && ps.ValueKind == JsonValueKind.Object)
        {
            ts = JsonPayloadCodec.ReadTimestamp(ps);
            if (ps.TryGetProperty("value", out var v) && v.ValueKind == JsonValueKind.Object) data = JsonDefaults.ToDictionary(v);
        }

        return [new UplinkEvent(name, JsonPayloadCodec.GetString(root, "type"), null, data, ts)];
    }

    public UplinkReply? DecodeReply(ReadOnlySpan<Byte> payload)
    {
        using var doc = JsonPayloadCodec.Parse(payload);
        if (doc == null || doc.RootElement.ValueKind != JsonValueKind.Object) return null;

        var root = doc.RootElement;
        var idText = JsonPayloadCodec.GetString(root, "id")?.Trim('"');
        if (!Int64.TryParse(idText, out var id)) return null;

        var success = !root.TryGetProperty("code", out var code) || code.ValueKind != JsonValueKind.Number || code.GetInt32() == 200;
        var data = root.TryGetProperty("data", out var d) ? d.GetRawText() : null;

        return new UplinkReply(id, success, data);
    }

    public Byte[] EncodeServiceInvoke(ServiceRecord record, String service, IDictionary<String, Object?> args) =>
        JsonSerializer.SerializeToUtf8Bytes(new Dictionary<String, Object?>
        {
            ["id"] = record.Id.ToString(),
            ["version"] = "1.0",
            ["params"] = args,
            ["method"] = $"thing.service.{service}",
        }, JsonDefaults.Options);

    public Byte[] EncodePropertySet(ServiceRecord record, IDictionary<String, Object?> values) =>
        JsonSerializer.SerializeToUtf8Bytes(new Dictionary<String, Object?>
        {
            ["id"] = record.Id.ToString(),
            ["version"] = "1.0",
            ["params"] = values,
            ["method"] = "thing.service.property.set",
        }, JsonDefaults.Options);
}
