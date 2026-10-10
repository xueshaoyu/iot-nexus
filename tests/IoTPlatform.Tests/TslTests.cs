using System.Text;
using System.Text.Json;
using IoTPlatform.Data;
using IoTPlatform.Protocols;
using IoTPlatform.Protocols.Codecs;
using IoTPlatform.Services;
using IoTPlatform.ThingModel;

namespace IoTPlatform.Tests;

public class TslTests
{
    private const String Sample = """
        {
          "schema": "https://iotx-tsl.oss-ap-southeast-1.aliyuncs.com/schema.json",
          "profile": { "productKey": "env-sensor" },
          "properties": [
            {
              "identifier": "temperature",
              "name": "温度",
              "accessMode": "r",
              "required": true,
              "desc": "环境温度",
              "dataType": { "type": "float", "specs": { "min": "-20", "max": "60", "step": "0.1", "unit": "°C", "unitName": "摄氏度" } }
            },
            {
              "identifier": "power",
              "name": "开关",
              "accessMode": "rw",
              "required": false,
              "dataType": { "type": "bool", "specs": { "0": "关", "1": "开" } }
            },
            {
              "identifier": "location",
              "name": "位置",
              "accessMode": "rw",
              "dataType": { "type": "text", "specs": { "length": "64" } }
            },
            {
              "identifier": "reportTime",
              "name": "上报时间",
              "accessMode": "r",
              "dataType": { "type": "date", "specs": {} }
            },
            {
              "identifier": "comfort",
              "name": "舒适度",
              "accessMode": "r",
              "dataType": { "type": "enum", "specs": { "0": "冷", "1": "舒适", "2": "热" } }
            },
            {
              "identifier": "tags",
              "name": "标签",
              "accessMode": "rw",
              "dataType": { "type": "array", "specs": { "size": "8", "item": { "type": "text", "specs": { "length": "16" } } } }
            },
            {
              "identifier": "geo",
              "name": "坐标",
              "accessMode": "r",
              "dataType": {
                "type": "struct",
                "specs": [
                  { "identifier": "longitude", "name": "经度", "dataType": { "type": "double", "specs": { "min": "-180", "max": "180", "step": "0.000001", "unit": "°" } } },
                  { "identifier": "latitude", "name": "纬度", "dataType": { "type": "double", "specs": { "min": "-90", "max": "90", "step": "0.000001", "unit": "°" } } }
                ]
              }
            }
          ],
          "events": [
            {
              "identifier": "post",
              "name": "post",
              "type": "info",
              "required": true,
              "method": "thing.event.property.post",
              "outputData": [ { "identifier": "temperature", "name": "温度", "dataType": { "type": "float", "specs": { "min": "-20", "max": "60" } } } ]
            },
            {
              "identifier": "overheat",
              "name": "过热",
              "type": "alert",
              "required": false,
              "desc": "温度过高",
              "method": "thing.event.overheat.post",
              "outputData": [ { "identifier": "temperature", "name": "温度", "dataType": { "type": "float", "specs": { "min": "0", "max": "100", "step": "0.1", "unit": "°C" } } } ]
            },
            {
              "identifier": "fault",
              "name": "故障",
              "type": "error",
              "method": "thing.event.fault.post",
              "outputData": [
                { "identifier": "code", "name": "代码", "dataType": { "type": "int", "specs": { "min": "0", "max": "999", "step": "1" } } },
                { "identifier": "message", "name": "说明", "dataType": { "type": "text", "specs": { "length": "128" } } }
              ]
            }
          ],
          "services": [
            {
              "identifier": "set",
              "name": "set",
              "required": true,
              "callType": "async",
              "method": "thing.service.property.set",
              "inputData": [],
              "outputData": []
            },
            {
              "identifier": "get",
              "name": "get",
              "required": true,
              "callType": "async",
              "method": "thing.service.property.get",
              "inputData": [],
              "outputData": []
            },
            {
              "identifier": "reboot",
              "name": "重启",
              "required": false,
              "callType": "async",
              "method": "thing.service.reboot",
              "inputData": [ { "identifier": "delay", "name": "延时", "dataType": { "type": "int", "specs": { "min": "0", "max": "60", "step": "1", "unit": "s" } } } ],
              "outputData": []
            },
            {
              "identifier": "calibrate",
              "name": "校准",
              "callType": "sync",
              "method": "thing.service.calibrate",
              "inputData": [ { "identifier": "offset", "name": "偏移", "dataType": { "type": "float", "specs": { "min": "-5", "max": "5", "step": "0.1" } } } ],
              "outputData": [ { "identifier": "ok", "name": "结果", "dataType": { "type": "bool", "specs": { "0": "否", "1": "是" } } } ]
            }
          ]
        }
        """;

    [Fact]
    public void Parse_AliyunDocument_SkipsSystemFeatures()
    {
        Assert.True(TslDocument.LooksLikeAliyun(Sample));
        Assert.False(TslDocument.LooksLikeAliyun("""{"properties":[{"id":"temp","name":"Temp"}]}"""));
        Assert.False(TslDocument.LooksLikeAliyun("""{"schema":"http://iot.feifan.link/schema.json","properties":[{"identifier":"temp","dataType":{"type":"float","specs":{"min":0,"max":1}}}]}"""));

        var model = TslDocument.Parse(Sample);
        Assert.Equal("env-sensor", model.ProductKey);
        Assert.Equal(7, model.Properties.Count);
        Assert.DoesNotContain(model.Events, e => e.Identifier == "post");
        Assert.DoesNotContain(model.Services, e => e.Identifier is "set" or "get");
        Assert.Equal(["overheat", "fault"], model.Events.Select(e => e.Identifier));
        Assert.Equal(["reboot", "calibrate"], model.Services.Select(e => e.Identifier));

        var temperature = model.Properties.Single(e => e.Identifier == "temperature");
        Assert.Equal("float", temperature.DataType);
        Assert.Equal(-20, temperature.Min);
        Assert.Equal(60, temperature.Max);
        Assert.Equal(0.1, temperature.Step);
        Assert.Equal("°C", temperature.Unit);
        Assert.Equal("摄氏度", temperature.UnitName);
        Assert.Equal("r", temperature.AccessMode);
        Assert.True(temperature.Required);

        var power = model.Properties.Single(e => e.Identifier == "power");
        Assert.Equal("bool", power.DataType);
        Assert.Equal("0=关,1=开", power.Mapping);

        var tags = model.Properties.Single(e => e.Identifier == "tags");
        Assert.Equal("array", tags.DataType);
        Assert.Equal(8, tags.Length);
        Assert.Equal("text", tags.ItemType);

        var geo = model.Properties.Single(e => e.Identifier == "geo");
        Assert.Contains("longitude", geo.Specs);
        Assert.Equal("alert", model.Events.Single(e => e.Identifier == "overheat").Type);
        Assert.Equal("sync", model.Services.Single(e => e.Identifier == "calibrate").CallType);
        Assert.Equal("offset", model.Services.Single(e => e.Identifier == "calibrate").InputData[0].Identifier);
    }

    [Fact]
    public void Parse_CustomModuleFile()
    {
        var json = """
            {
              "schema": "https://iotx-tsl.oss-ap-southeast-1.aliyuncs.com/schema.json",
              "profile": { "productKey": "env-sensor" },
              "functionBlockId": "batteryPack",
              "functionBlockName": "电池包",
              "properties": [
                { "identifier": "health", "name": "健康度", "accessMode": "r", "dataType": { "type": "int", "specs": { "min": "0", "max": "100", "step": "1", "unit": "%" } } }
              ],
              "events": [],
              "services": []
            }
            """;

        var model = TslDocument.Parse(json);
        Assert.Equal("batteryPack", model.FunctionBlockId);
        Assert.Equal("电池包", model.FunctionBlockName);
        Assert.Equal("batteryPack", model.Properties.Single().FunctionBlockId);
        Assert.Equal(100, model.Properties.Single().Max);
    }

    [Fact]
    public void IdentifierAndTypeRules()
    {
        TslRules.EnsureIdentifier("temperature");
        TslRules.EnsureIdentifier("_ok");
        Assert.Throws<PlatformException>(() => TslRules.EnsureIdentifier("1temp"));
        Assert.Throws<PlatformException>(() => TslRules.EnsureIdentifier("a-b"));
        Assert.Throws<PlatformException>(() => TslRules.EnsureIdentifier(new String('a', 51)));
        Assert.Throws<PlatformException>(() => TslRules.EnsureName(new String('名', 31)));
        TslRules.EnsureBlock(null);
        Assert.Throws<PlatformException>(() => TslRules.EnsureBlock("bad-block"));

        Assert.Equal("text", TslTypes.Canonical("string"));
        Assert.Equal("int", TslTypes.Canonical("int32"));
        Assert.Equal("short", TslTypes.Canonical("short"));
        Assert.Equal("w", TslTypes.CanonicalAccess("W"));
        Assert.True(TslTypes.IsWritable("w"));
        Assert.False(TslTypes.IsWritable("r"));
        Assert.Throws<PlatformException>(() => TslTypes.Canonical("blob"));
        Assert.Throws<PlatformException>(() => TslTypes.CanonicalAccess("read"));
        var ex = Assert.Throws<PlatformException>(() => TslDocument.Parse("{"));
        Assert.Contains("Invalid TSL JSON", ex.Message);
    }

    [Fact]
    public void ValueChecker_CoversTslTypes()
    {
        var temperature = new ThingProperty { DataType = "float", Min = -20, Max = 60, Step = 0.1 };
        Assert.True(TslValueChecker.Check(temperature, 26.5).Ok);
        Assert.Equal("out of range", TslValueChecker.Check(temperature, 80).Error);
        Assert.True(TslValueChecker.Check(new ThingProperty { DataType = "int" }, 99).Ok);
        Assert.Equal("out of range", TslValueChecker.Check(new ThingProperty { DataType = "int", Min = 0, Max = 10 }, 11).Error);

        Assert.True(TslValueChecker.Check(new ThingProperty { DataType = "bool", Mapping = "0=关,1=开" }, "on").Ok);
        Assert.Equal("enum value is not defined", TslValueChecker.Check(new ThingProperty { DataType = "enum", Mapping = "0=冷,1=热" }, "9").Error);
        Assert.Equal("text longer than 4", TslValueChecker.Check(new ThingProperty { DataType = "text", Length = 4 }, "hello").Error);
        Assert.True(TslValueChecker.Check(new ThingProperty { DataType = "date" }, 1524448722000L).Ok);
        Assert.Equal("not a valid date", TslValueChecker.Check(new ThingProperty { DataType = "date" }, "yesterday").Error);

        var geo = new ThingProperty
        {
            DataType = "struct",
            Specs = """[{"identifier":"longitude","name":"经度","dataType":{"type":"double","specs":{"min":"-180","max":"180"}}}]""",
        };
        var ok = TslValueChecker.Check(geo, new Dictionary<String, Object?> { ["longitude"] = 121.5 });
        Assert.True(ok.Ok);
        Assert.Equal("unknown field latitude", TslValueChecker.Check(geo, new Dictionary<String, Object?> { ["latitude"] = 1 }).Error);

        var tags = new ThingProperty { DataType = "array", Length = 2, Specs = """{"size":"2","item":{"type":"int","specs":{"min":"0","max":"9"}}}""" };
        Assert.True(TslValueChecker.Check(tags, new List<Object?> { 1, 2 }).Ok);
        Assert.Equal("array size exceeds 2", TslValueChecker.Check(tags, new List<Object?> { 1, 2, 3 }).Error);
        Assert.Equal("out of range", TslValueChecker.Check(tags, new List<Object?> { 20 }).Error);

        var flat = new Dictionary<String, Object?>();
        TslValueChecker.Flatten(flat, "geo", new Dictionary<String, Object?> { ["longitude"] = 1.2 });
        Assert.Equal(1.2, flat["geo.longitude"]);
    }

    [Fact]
    public void AlinkTopics_AndEnvelope()
    {
        Assert.True(AlinkTopics.TryParse("/sys/pk/dn/thing/event/property/post", out var pk, out var dn, out var kind, out var id));
        Assert.Equal(("pk", "dn", AlinkTopics.Kind.PropertyPost, ""), (pk, dn, kind, id));
        Assert.True(AlinkTopics.TryParse("sys/pk/dn/thing/event/overheat/post", out _, out _, out kind, out id));
        Assert.Equal(AlinkTopics.Kind.EventPost, kind);
        Assert.Equal("overheat", id);
        Assert.True(AlinkTopics.TryParse("/sys/pk/dn/thing/service/property/set_reply", out _, out _, out kind, out _));
        Assert.Equal(AlinkTopics.Kind.PropertySetReply, kind);
        Assert.True(AlinkTopics.TryParse("/sys/pk/dn/thing/service/reboot_reply", out _, out _, out kind, out id));
        Assert.Equal((AlinkTopics.Kind.ServiceReply, "reboot"), (kind, id));
        Assert.False(AlinkTopics.TryParse("iot/pk/dn/property/post", out _, out _, out _, out _));
        Assert.Equal("/sys/pk/dn/thing/service/property/set", AlinkTopics.PropertySet("pk", "dn"));
        Assert.Contains("/sys/+/+/thing/event/property/post", AlinkTopics.UplinkFilters());

        Assert.True(AlinkEnvelope.TryRead(Encoding.UTF8.GetBytes("""{"id":"9","method":"thing.event.property.post"}"""), out var msgId, out var ack, out var method));
        Assert.Equal(("9", 1, "thing.event.property.post"), (msgId, ack, method));
        Assert.True(AlinkEnvelope.TryRead(Encoding.UTF8.GetBytes("""{"id":"9","sys":{"ack":0}}"""), out _, out ack, out _));
        Assert.Equal(0, ack);

        using var reply = JsonDocument.Parse(AlinkEnvelope.Reply("9", 200, "success", "thing.event.property.post"));
        Assert.Equal(200, reply.RootElement.GetProperty("code").GetInt32());
        Assert.Equal("9", reply.RootElement.GetProperty("id").GetString());
    }
}

[CollectionDefinition("platform-db")]
public sealed class PlatformDbCollection : ICollectionFixture<PlatformFixture>;

[Collection("platform-db")]
public class TslPlatformTests(PlatformFixture fixture)
{
    [Fact]
    public void ImportExport_RoundTrip_AndKeepsDriverFields()
    {
        var products = fixture.Get<ProductService>();
        var code = "tsl" + Guid.NewGuid().ToString("N")[..8];
        var product = products.Create(new Models.ProductInput { Name = "TSL", Code = code, Protocol = AccessProtocols.Mqtt });
        products.SaveProperty(product.Id, 0, new Models.ThingPropertyInput
        {
            Identifier = "temperature",
            DataType = "float",
            Address = "40001",
            PointType = "HoldingRegister",
            Scaling = 0.1,
        });

        var imported = products.ImportModel(product.Id, TslTestsSample.Json);
        Assert.Equal(7, imported.Properties);
        Assert.Equal(2, imported.Events);
        Assert.Equal(2, imported.Services);

        var temperature = ThingProperty.FindByProductIdAndFunctionBlockIdAndIdentifier(product.Id, "", "temperature");
        Assert.NotNull(temperature);
        Assert.Equal("40001", temperature!.Address);
        Assert.Equal("HoldingRegister", temperature.PointType);
        Assert.Equal(0.1, temperature.Scaling);
        Assert.Equal(-20, temperature.Min);
        Assert.Equal("float", temperature.DataType);

        var module = """
            {
              "functionBlockId": "batteryPack",
              "functionBlockName": "电池包",
              "properties": [
                { "identifier": "health", "name": "健康度", "accessMode": "r", "required": true, "dataType": { "type": "int", "specs": { "min": "0", "max": "100", "step": "1", "unit": "%" } } }
              ],
              "events": [],
              "services": []
            }
            """;
        products.ImportModel(product.Id, module);
        var health = ThingLookup.Property(product.Id, "batteryPack:health");
        Assert.NotNull(health);
        Assert.True(health!.Required);
        Assert.Equal(100, health.Max);

        var exported = products.ExportTsl(product.Id, full: true);
        var again = TslDocument.Parse(exported);
        Assert.Equal(code, again.ProductKey);
        Assert.Contains(again.Properties, e => e.Identifier == "temperature" && e.Max == 60 && e.Required);
        Assert.Contains(again.Properties, e => e.Identifier == "geo" && e.DataType == "struct");
        Assert.Contains(again.Events, e => e.Identifier == "overheat" && e.Type == "alert");
        Assert.Contains(again.Services, e => e.Identifier == "calibrate" && e.CallType == "sync");
        var block = Assert.Single(again.FunctionBlocks);
        Assert.Equal("batteryPack", block.FunctionBlockId);
        Assert.Equal("health", block.Properties.Single().Identifier);

        var blockFile = TslDocument.Parse(products.ExportTsl(product.Id, "batteryPack"));
        Assert.Equal("batteryPack", blockFile.FunctionBlockId);
        Assert.Equal("health", Assert.Single(blockFile.Properties).Identifier);
        Assert.Empty(blockFile.FunctionBlocks);

        var specJson = products.GetSpecification(product.Id).ToJson()!;
        Assert.False(TslDocument.LooksLikeAliyun(specJson));
        var thingSpec = """{"schema":"http://iot.feifan.link/schema.json","properties":[{"identifier":"voltage","name":"电压","accessMode":"r","dataType":{"type":"float","specs":{"min":0,"max":300,"unit":"V"}}}]}""";
        Assert.Equal(1, products.ImportModel(product.Id, thingSpec).Properties);
        Assert.Equal(300, ThingLookup.Property(product.Id, "voltage")!.Max);

        Assert.Throws<PlatformException>(() => products.SaveEvent(product.Id, 0, new Models.ThingEventInput { Identifier = "property", Name = "属性" }));
    }

    [Fact]
    public void AlinkRouter_ValidatesAndReplies()
    {
        var products = fixture.Get<ProductService>();
        var devices = fixture.Get<DeviceService>();
        var router = fixture.Get<Mqtt.MqttUplinkRouter>();
        var code = "ak" + Guid.NewGuid().ToString("N")[..8];
        var product = products.Create(new Models.ProductInput { Name = "Alink", Code = code, Protocol = AccessProtocols.Mqtt });
        products.ImportModel(product.Id, TslTestsSample.Json);
        var device = devices.Create(new Models.DeviceInput { ProductId = product.Id, Code = "dn-" + code, Secret = "secret" });

        var ok = router.Handle(
            $"/sys/{code}/{device.Code}/thing/event/property/post",
            Encoding.UTF8.GetBytes($$"""{"id":"42","version":"1.0","sys":{"ack":1},"params":{"temperature":{"value":26.54,"time":1524448722000},"power":1},"method":"thing.event.property.post"}"""),
            Context(device));
        Assert.True(ok.Accepted);
        Assert.Equal($"/sys/{code}/{device.Code}/thing/event/property/post_reply", ok.ReplyTopic);
        using (var doc = JsonDocument.Parse(ok.ReplyPayload!))
        {
            Assert.Equal(200, doc.RootElement.GetProperty("code").GetInt32());
            Assert.Equal("42", doc.RootElement.GetProperty("id").GetString());
        }
        Assert.Equal("26.5", DeviceProperty.FindByDeviceIdAndName(device.Id, "temperature")?.Value);

        var range = router.Handle(
            $"/sys/{code}/{device.Code}/thing/event/property/post",
            Encoding.UTF8.GetBytes("""{"id":"43","params":{"temperature":80},"method":"thing.event.property.post"}"""),
            Context(device));
        Assert.False(range.Accepted);
        using (var doc = JsonDocument.Parse(range.ReplyPayload!))
            Assert.Equal(400, doc.RootElement.GetProperty("code").GetInt32());
        Assert.Equal("26.5", DeviceProperty.FindByDeviceIdAndName(device.Id, "temperature")?.Value);

        var missing = router.Handle(
            $"/sys/{code}/{device.Code}/thing/event/property/post",
            Encoding.UTF8.GetBytes("""{"id":"44","params":{"unknown":1},"method":"thing.event.property.post"}"""),
            Context(device));
        using (var doc = JsonDocument.Parse(missing.ReplyPayload!))
            Assert.Equal(404, doc.RootElement.GetProperty("code").GetInt32());

        var quiet = router.Handle(
            $"sys/{code}/{device.Code}/thing/event/property/post",
            Encoding.UTF8.GetBytes("""{"id":"45","sys":{"ack":0},"params":{"power":0},"method":"thing.event.property.post"}"""),
            Context(device));
        Assert.True(quiet.Accepted);
        Assert.Null(quiet.ReplyTopic);

        var ev = router.Handle(
            $"/sys/{code}/{device.Code}/thing/event/overheat/post",
            Encoding.UTF8.GetBytes("""{"id":"46","params":{"value":{"temperature":101},"time":1524448722000},"method":"thing.event.overheat.post"}"""),
            Context(device));
        Assert.Equal($"/sys/{code}/{device.Code}/thing/event/overheat/post_reply", ev.ReplyTopic);
        using (var doc = JsonDocument.Parse(ev.ReplyPayload!))
            Assert.Equal(400, doc.RootElement.GetProperty("code").GetInt32());

        var legacy = router.Handle(
            $"iot/{code}/{device.Code}/property/post",
            Encoding.UTF8.GetBytes("""{"location":"lab","temperature":80,"extra":"kept"}"""),
            Context(device));
        Assert.True(legacy.Accepted);
        Assert.Null(legacy.ReplyTopic);
        Assert.Equal("26.5", DeviceProperty.FindByDeviceIdAndName(device.Id, "temperature")?.Value);
        Assert.Equal("lab", DeviceProperty.FindByDeviceIdAndName(device.Id, "location")?.Value);
        Assert.Equal("kept", DeviceProperty.FindByDeviceIdAndName(device.Id, "extra")?.Value);
    }

    private static Mqtt.MqttUplinkContext Context(Device device) =>
        new(device, "test", "client", "127.0.0.1", MqttBrokerIds.Embedded, Topics.Root);
}

/// <summary>供平台测试复用的阿里云 TSL 样例，与 <see cref="TslTests"/> 中的样例保持一致</summary>
file static class TslTestsSample
{
    public const String Json = """
        {
          "schema": "https://iotx-tsl.oss-ap-southeast-1.aliyuncs.com/schema.json",
          "profile": { "productKey": "env-sensor" },
          "properties": [
            { "identifier": "temperature", "name": "温度", "accessMode": "r", "required": true, "dataType": { "type": "float", "specs": { "min": "-20", "max": "60", "step": "0.1", "unit": "°C", "unitName": "摄氏度" } } },
            { "identifier": "power", "name": "开关", "accessMode": "rw", "dataType": { "type": "bool", "specs": { "0": "关", "1": "开" } } },
            { "identifier": "location", "name": "位置", "accessMode": "rw", "dataType": { "type": "text", "specs": { "length": "64" } } },
            { "identifier": "reportTime", "name": "上报时间", "accessMode": "r", "dataType": { "type": "date", "specs": {} } },
            { "identifier": "comfort", "name": "舒适度", "accessMode": "r", "dataType": { "type": "enum", "specs": { "0": "冷", "1": "舒适", "2": "热" } } },
            { "identifier": "tags", "name": "标签", "accessMode": "rw", "dataType": { "type": "array", "specs": { "size": "8", "item": { "type": "text", "specs": { "length": "16" } } } } },
            { "identifier": "geo", "name": "坐标", "accessMode": "r", "dataType": { "type": "struct", "specs": [ { "identifier": "longitude", "name": "经度", "dataType": { "type": "double", "specs": { "min": "-180", "max": "180" } } } ] } }
          ],
          "events": [
            { "identifier": "post", "name": "post", "type": "info", "method": "thing.event.property.post", "outputData": [] },
            { "identifier": "overheat", "name": "过热", "type": "alert", "method": "thing.event.overheat.post", "outputData": [ { "identifier": "temperature", "name": "温度", "dataType": { "type": "float", "specs": { "min": "0", "max": "100", "step": "0.1", "unit": "°C" } } } ] },
            { "identifier": "fault", "name": "故障", "type": "error", "method": "thing.event.fault.post", "outputData": [ { "identifier": "code", "name": "代码", "dataType": { "type": "int", "specs": { "min": "0", "max": "999" } } } ] }
          ],
          "services": [
            { "identifier": "set", "name": "set", "callType": "async", "method": "thing.service.property.set", "inputData": [], "outputData": [] },
            { "identifier": "get", "name": "get", "callType": "async", "method": "thing.service.property.get", "inputData": [], "outputData": [] },
            { "identifier": "reboot", "name": "重启", "callType": "async", "method": "thing.service.reboot", "inputData": [], "outputData": [] },
            { "identifier": "calibrate", "name": "校准", "callType": "sync", "method": "thing.service.calibrate", "inputData": [ { "identifier": "offset", "name": "偏移", "dataType": { "type": "float", "specs": { "min": "-5", "max": "5" } } } ], "outputData": [] }
          ]
        }
        """;
}
