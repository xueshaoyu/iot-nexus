using IoTPlatform.Data;

namespace IoTPlatform.Protocols.Codecs;

/// <summary>上行事件</summary>
public record UplinkEvent(String Name, String? Type, String? Remark, IDictionary<String, Object?>? Data, Int64 Timestamp);

/// <summary>设备回复</summary>
public record UplinkReply(Int64 Id, Boolean Success, String? Data);

/// <summary>属性上报</summary>
public record UplinkProperties(IDictionary<String, Object?> Values, Int64 Timestamp);

/// <summary>载荷编解码器。把 MQTT/HTTP 的原始载荷翻译为平台统一模型，实现该接口即可接入新的数据格式</summary>
/// <remarks>产品的 Codec 字段选择编解码器，注册方式：services.AddSingleton&lt;IPayloadCodec, MyCodec&gt;()</remarks>
public interface IPayloadCodec
{
    /// <summary>名称，对应产品 Codec 字段</summary>
    String Name { get; }

    String Description { get; }

    UplinkProperties? DecodeProperties(ReadOnlySpan<Byte> payload);

    IList<UplinkEvent> DecodeEvents(ReadOnlySpan<Byte> payload, String? topicHint = null);

    UplinkReply? DecodeReply(ReadOnlySpan<Byte> payload);

    Byte[] EncodeServiceInvoke(ServiceRecord record, String service, IDictionary<String, Object?> args);

    Byte[] EncodePropertySet(ServiceRecord record, IDictionary<String, Object?> values);
}

/// <summary>编解码器注册表</summary>
public class PayloadCodecFactory(IEnumerable<IPayloadCodec> codecs)
{
    public IReadOnlyList<IPayloadCodec> Codecs { get; } = codecs.ToList();

    /// <summary>按名称获取，找不到时回退到 Json</summary>
    public IPayloadCodec Get(String? name) =>
        Codecs.FirstOrDefault(e => e.Name.Equals(name ?? "", StringComparison.OrdinalIgnoreCase))
        ?? Codecs.First(e => e.Name == JsonPayloadCodec.CodecName);
}
