using System.Text;
using System.Text.Json;
using IoTPlatform.Services;

namespace IoTPlatform.Protocols.Codecs;

/// <summary>Alink JSON 信封：id、sys.ack 与云端回复</summary>
public static class AlinkEnvelope
{
    public static Boolean TryRead(ReadOnlySpan<Byte> payload, out String id, out Int32 ack, out String? method)
    {
        id = "";
        ack = 1;
        method = null;
        try
        {
            using var doc = JsonPayloadCodec.Parse(payload);
            if (doc == null || doc.RootElement.ValueKind != JsonValueKind.Object) return false;

            var root = doc.RootElement;
            id = JsonPayloadCodec.GetString(root, "id") ?? "";
            method = JsonPayloadCodec.GetString(root, "method");
            if (root.TryGetProperty("sys", out var sys) && sys.ValueKind == JsonValueKind.Object && sys.TryGetProperty("ack", out var ackNode) && ackNode.ValueKind == JsonValueKind.Number)
                ack = ackNode.GetInt32();

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static Byte[] Reply(String? id, Int32 code, String message, String? method, Object? data = null) =>
        JsonSerializer.SerializeToUtf8Bytes(new Dictionary<String, Object?>
        {
            ["id"] = id ?? "",
            ["code"] = code,
            ["data"] = data ?? new Dictionary<String, Object?>(),
            ["message"] = message,
            ["method"] = method,
            ["version"] = "1.0",
        }, JsonDefaults.Options);

    public static String Text(Byte[] payload) => Encoding.UTF8.GetString(payload);
}
