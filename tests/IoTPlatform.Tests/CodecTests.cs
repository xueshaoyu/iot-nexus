using System.Text;
using System.Text.Json;
using IoTPlatform.Data;
using IoTPlatform.Protocols;
using IoTPlatform.Protocols.Codecs;
using IoTPlatform.Services;

namespace IoTPlatform.Tests;

public class CodecTests
{
    private static Byte[] B(String s) => Encoding.UTF8.GetBytes(s);

    [Fact]
    public void Json_Properties_AllShapes()
    {
        var codec = new JsonPayloadCodec();

        var flat = codec.DecodeProperties(B("""{"temperature":25.5,"switch":true,"ts":1700000000}"""))!;
        Assert.Equal(25.5, flat.Values["temperature"]);
        Assert.Equal(true, flat.Values["switch"]);
        Assert.False(flat.Values.ContainsKey("ts"));
        Assert.Equal(1700000000000, flat.Timestamp);

        var wrapped = codec.DecodeProperties(B("""{"ts":1700000000123,"params":{"humidity":60}}"""))!;
        Assert.Equal(60L, wrapped.Values["humidity"]);
        Assert.Equal(1700000000123, wrapped.Timestamp);

        var items = codec.DecodeProperties(B("""{"items":[{"name":"voltage","value":"220.1","time":1700000000000}]}"""))!;
        Assert.Equal("220.1", items.Values["voltage"]);

        Assert.Null(codec.DecodeProperties(B("not json")));
    }

    [Fact]
    public void Json_EventsAndReply()
    {
        var codec = new JsonPayloadCodec();

        var events = codec.DecodeEvents(B("""[{"type":"alert","name":"overheat","remark":"hot","data":{"temperature":40}},{"name":"lowBattery"}]"""));
        Assert.Equal(2, events.Count);
        Assert.Equal("overheat", events[0].Name);
        Assert.Equal(40L, events[0].Data!["temperature"]);

        var ok = codec.DecodeReply(B("""{"id":7508705076186447872,"status":2,"data":{"switch":false}}"""))!;
        Assert.Equal(7508705076186447872, ok.Id);
        Assert.True(ok.Success);
        Assert.Equal("""{"switch":false}""", ok.Data);

        Assert.False(codec.DecodeReply(B("""{"id":"5","status":4}"""))!.Success);
        Assert.False(codec.DecodeReply(B("""{"id":5,"success":false}"""))!.Success);
    }

    [Fact]
    public void Json_EncodeDownlink()
    {
        var codec = new JsonPayloadCodec();
        var record = new ServiceRecord { Id = 42, CreateTime = DateTime.Now };

        using var doc = JsonDocument.Parse(codec.EncodeServiceInvoke(record, "reboot", new Dictionary<String, Object?> { ["delay"] = 5 }));
        Assert.Equal(42, doc.RootElement.GetProperty("id").GetInt64());
        Assert.Equal("reboot", doc.RootElement.GetProperty("name").GetString());
        Assert.Equal(5, doc.RootElement.GetProperty("params").GetProperty("delay").GetInt32());
    }

    [Fact]
    public void Alink_RoundTrip()
    {
        var codec = new AlinkPayloadCodec();

        var props = codec.DecodeProperties(B("""{"id":"1","version":"1.0","params":{"temp":{"value":23.6,"time":1524448722000},"hum":60},"method":"thing.event.property.post"}"""))!;
        Assert.Equal(23.6, props.Values["temp"]);
        Assert.Equal(60L, props.Values["hum"]);
        Assert.Equal(1524448722000, props.Timestamp);

        var ev = Assert.Single(codec.DecodeEvents(B("""{"id":"2","params":{"value":{"code":1},"time":1524448722000},"method":"thing.event.overheat.post"}""")));
        Assert.Equal("overheat", ev.Name);

        var reply = codec.DecodeReply(B("""{"id":"123","code":200,"data":{}}"""))!;
        Assert.Equal(123, reply.Id);
        Assert.True(reply.Success);

        var set = Encoding.UTF8.GetString(codec.EncodePropertySet(new ServiceRecord { Id = 9 }, new Dictionary<String, Object?> { ["switch"] = true }));
        Assert.Contains("thing.service.property.set", set);
    }

    [Fact]
    public void Topics_Parse()
    {
        Assert.True(Topics.TryParse("iot/env-sensor/th-001/property/post", out var pk, out var dc, out var kind));
        Assert.Equal(("env-sensor", "th-001", Topics.PropertyPost), (pk, dc, kind));
        Assert.False(Topics.TryParse("alerts/th-001", out _, out _, out _));
        Assert.False(Topics.TryParse("iot/pk/dc", out _, out _, out _));
        Assert.Equal("iot/pk/dc/#", Topics.DeviceFilter("pk", "dc"));
    }

    [Fact]
    public void ThingValue_ConvertsByDataType()
    {
        Assert.Equal(26L, ThingValueConverter.Convert(new ThingProperty { DataType = "int" }, "25.6"));
        Assert.Equal(25.57, ThingValueConverter.Convert(new ThingProperty { DataType = "float", Step = 0.01 }, 25.5712));
        Assert.Equal(true, ThingValueConverter.Convert(new ThingProperty { DataType = "bool" }, "on"));
        Assert.Equal("x", ThingValueConverter.Convert(new ThingProperty { DataType = "string" }, "x"));
        Assert.Equal("abc", ThingValueConverter.Convert(new ThingProperty { DataType = "int" }, "abc"));
        Assert.Equal(220.5, (Double)ThingValueConverter.Scale(new ThingProperty { Scaling = 0.1 }, (UInt16)2205)!, 6);
    }
}
