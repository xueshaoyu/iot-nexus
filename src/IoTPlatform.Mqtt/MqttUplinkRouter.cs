using System.Text;
using IoTPlatform.Data;
using IoTPlatform.Protocols;
using IoTPlatform.Protocols.Codecs;
using IoTPlatform.Services;
using IoTPlatform.ThingModel;
using NewLife;
using NewLife.Log;

namespace IoTPlatform.Mqtt;

/// <summary>上行消息来源信息</summary>
/// <param name="Device">已认证的设备（内置服务端会话），外部服务器模式为空，按主题解析</param>
/// <param name="SessionId">会话标识</param>
/// <param name="ClientId">客户端标识</param>
/// <param name="IP">来源地址</param>
/// <param name="BrokerProfileId">消息来自的 MQTT 服务器。非空时必须与设备解析结果一致</param>
/// <param name="TopicRoot">该服务器的主题根，空则使用全局 Topics.Root</param>
public record MqttUplinkContext(Device? Device, String? SessionId, String? ClientId = null, String? IP = null, String? BrokerProfileId = null, String? TopicRoot = null);

/// <summary>上行处理结果。Alink 主题在需要应答时带上回复主题和报文</summary>
public sealed class MqttUplinkResult
{
    public Boolean Accepted { get; init; }

    public String? ReplyTopic { get; init; }

    public Byte[]? ReplyPayload { get; init; }

    public static MqttUplinkResult Ignore { get; } = new();

    public static MqttUplinkResult Ok(Boolean accepted = true) => new() { Accepted = accepted };

    public static MqttUplinkResult Reply(Boolean accepted, String topic, Byte[] payload) =>
        new() { Accepted = accepted, ReplyTopic = topic, ReplyPayload = payload };
}

/// <summary>设备上行消息路由：解析主题 → 校验设备 → 按产品编解码器解码 → 交给接入服务。内置与外部两种模式共用</summary>
/// <remarks>
/// 外部服务器模式下平台看不到连接层的身份，只信任主题中的产品与设备编码：
/// 设备必须存在、启用、属于该产品且产品接入协议为 MQTT。发布者身份需由外部服务器的 ACL 保证。
/// 消息必须到达设备所分配的服务器，其它服务器上的同主题上报会被丢弃。
/// </remarks>
public class MqttUplinkRouter(DataIngestService ingestService, DeviceOnlineService onlineService, PayloadCodecFactory codecs, MqttBrokerCatalog brokers)
{
    private const String Protocol = AccessProtocols.Mqtt;

    /// <summary>处理一条上行消息。原有 iot/ 主题与阿里云 /sys/ Alink 主题都在这里接入</summary>
    public MqttUplinkResult Handle(String topic, Byte[] payload, MqttUplinkContext context)
    {
        if (AlinkTopics.TryParse(topic, out var apk, out var adc, out var alinkKind, out var identifier) && AlinkTopics.IsUplink(alinkKind))
            return HandleAlink(apk, adc, alinkKind, identifier, payload, context);

        var root = context.TopicRoot.IsNullOrEmpty() ? Topics.Root : context.TopicRoot;
        if (!Topics.TryParse(topic, root, out var pk, out var dc, out var kind) || !Topics.Uplinks.Contains(kind)) return MqttUplinkResult.Ignore;

        if (!TryAccept(pk, dc, context, out var device)) return MqttUplinkResult.Ignore;

        try
        {
            var codec = codecs.Get(device!.Product!.Codec);
            switch (kind)
            {
                case Topics.PropertyPost:
                    var props = codec.DecodeProperties(payload);
                    if (props == null || props.Values.Count == 0) return MqttUplinkResult.Ignore;
                    return MqttUplinkResult.Ok(ingestService.PostProperties(device, props.Values, props.Timestamp, Protocol, context.SessionId, context.IP) > 0);

                case Topics.EventPost:
                    var events = codec.DecodeEvents(payload);
                    foreach (var e in events) ingestService.PostEvent(device, e.Type, e.Name, e.Remark, e.Data, e.Timestamp, Protocol, context.SessionId, context.IP);
                    return MqttUplinkResult.Ok(events.Count > 0);

                case Topics.ServiceReply:
                    var reply = codec.DecodeReply(payload);
                    return MqttUplinkResult.Ok(reply != null && ingestService.PostReply(device, reply.Id, reply.Success, reply.Data, Protocol, context.SessionId));

                case Topics.Heartbeat:
                    ingestService.Heartbeat(device, Protocol, context.SessionId, context.IP);
                    return MqttUplinkResult.Ok();

                case Topics.Status:
                    var online = ParseStatus(payload);
                    if (online == true) onlineService.SetOnline(device, Protocol, context.SessionId, context.ClientId, context.IP);
                    else if (online == false && context.Device == null) onlineService.SetOffline(device, "MQTT status: offline");
                    return MqttUplinkResult.Ok(online != null);
            }
        }
        catch (Exception ex)
        {
            XTrace.WriteLine("处理设备消息失败 {0} {1} {2}", device?.Code, topic, ex.Message);
        }

        return MqttUplinkResult.Ignore;
    }

    private MqttUplinkResult HandleAlink(String productKey, String deviceCode, AlinkTopics.Kind kind, String identifier, Byte[] payload, MqttUplinkContext context)
    {
        if (!TryAccept(productKey, deviceCode, context, out var device)) return MqttUplinkResult.Ignore;

        var codec = new AlinkPayloadCodec();
        AlinkEnvelope.TryRead(payload, out var id, out var ack, out var method);
        try
        {
            switch (kind)
            {
                case AlinkTopics.Kind.PropertyPost:
                {
                    var props = codec.DecodeProperties(payload);
                    var errors = new List<String>();
                    var values = FilterDefined(device!, props?.Values, errors);
                    var accepted = values.Count == 0 ? 0 : ingestService.PostProperties(device!, values, props?.Timestamp ?? 0, Protocol, context.SessionId, context.IP, errors);
                    return AlinkResult(accepted > 0 && errors.Count == 0, accepted > 0, ack, AlinkTopics.PropertyPostReply(productKey, deviceCode), id, method ?? "thing.event.property.post", errors);
                }
                case AlinkTopics.Kind.EventPost:
                {
                    var parsed = codec.DecodeEvents(payload, identifier);
                    var name = identifier.IsNullOrEmpty() ? parsed.FirstOrDefault()?.Name : identifier;
                    if (name.IsNullOrEmpty())
                        return AlinkResult(false, false, ack, AlinkTopics.EventPostReply(productKey, deviceCode, identifier), id, method, ["event identifier is required"]);

                    var ev = parsed.FirstOrDefault();
                    var errors = new List<String>();
                    var stored = ingestService.PostEvent(device!, ev?.Type, name, ev?.Remark, ev?.Data, ev?.Timestamp ?? 0, Protocol, context.SessionId, context.IP, true, errors);
                    var replyId = name;
                    return AlinkResult(stored && errors.Count == 0, stored, ack, AlinkTopics.EventPostReply(productKey, deviceCode, replyId), id, method ?? $"thing.event.{name}.post", errors);
                }
                case AlinkTopics.Kind.PropertySetReply:
                case AlinkTopics.Kind.ServiceReply:
                {
                    var reply = codec.DecodeReply(payload);
                    return MqttUplinkResult.Ok(reply != null && ingestService.PostReply(device!, reply.Id, reply.Success, reply.Data, Protocol, context.SessionId));
                }
            }
        }
        catch (Exception ex)
        {
            XTrace.WriteLine("处理 Alink 消息失败 {0} {1} {2}", device!.Code, kind, ex.Message);
            var topic = kind == AlinkTopics.Kind.EventPost
                ? AlinkTopics.EventPostReply(productKey, deviceCode, identifier)
                : AlinkTopics.PropertyPostReply(productKey, deviceCode);
            return AlinkResult(false, false, ack, topic, id, method, [ex.Message]);
        }

        return MqttUplinkResult.Ignore;
    }

    private static Dictionary<String, Object?> FilterDefined(Device device, IDictionary<String, Object?>? values, List<String> errors)
    {
        var defined = ThingProperty.GetCachedByProductId(device.ProductId);
        var kept = new Dictionary<String, Object?>(StringComparer.OrdinalIgnoreCase);
        if (values == null) return kept;

        foreach (var (key, value) in values)
        {
            if (!defined.TryGetValue(key, out var def) || !def.Enable)
            {
                errors.Add($"{key}: not defined");
                continue;
            }

            kept[key] = value;
        }

        return kept;
    }

    private static MqttUplinkResult AlinkResult(Boolean success, Boolean accepted, Int32 ack, String topic, String? id, String? method, IList<String> errors)
    {
        if (ack == 0) return MqttUplinkResult.Ok(accepted);

        var code = success ? 200 : errors.Any(e => e.EndsWith("not defined", StringComparison.Ordinal)) && !accepted ? 404 : 400;
        var message = success ? "success" : String.Join("; ", errors);
        if (message.Length == 0) message = "invalid payload";
        return MqttUplinkResult.Reply(accepted, topic, AlinkEnvelope.Reply(id, code, message, method, success ? new Dictionary<String, Object?>() : new Dictionary<String, Object?> { ["errors"] = errors }));
    }

    private Boolean TryAccept(String productKey, String deviceCode, MqttUplinkContext context, out Device? device)
    {
        device = context.Device ?? Device.FindByCode(deviceCode);
        if (device == null || !device.Code.EqualIgnoreCase(deviceCode) || !device.Enable) return false;

        var product = device.Product;
        if (product == null || !product.Enable || !product.Code.EqualIgnoreCase(productKey) || product.Protocol != AccessProtocols.Mqtt)
            return false;

        return context.BrokerProfileId.IsNullOrEmpty() || brokers.Same(brokers.Resolve(device, product), context.BrokerProfileId);
    }

    /// <summary>解析状态载荷：online/offline、1/0、true/false，或 {"online":true} / {"status":"online"}</summary>
    public static Boolean? ParseStatus(Byte[] payload)
    {
        var text = Encoding.UTF8.GetString(payload).Trim().Trim('"').ToLowerInvariant();
        switch (text)
        {
            case "online" or "1" or "true" or "connected": return true;
            case "offline" or "0" or "false" or "disconnected": return false;
        }

        var json = JsonDefaults.ParseObject(text);
        if (json == null) return null;
        if (json.TryGetValue("online", out var o) && ThingValueConverter.TryBool(o, out var b)) return b;
        if (json.TryGetValue("status", out var s)) return ParseStatus(Encoding.UTF8.GetBytes(JsonDefaults.ToText(s) ?? ""));

        return null;
    }
}
