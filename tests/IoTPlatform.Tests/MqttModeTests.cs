using System.Text;
using IoTPlatform.Data;
using IoTPlatform.Mqtt;
using IoTPlatform.Protocols;
using IoTPlatform.Services;
using Microsoft.Extensions.Configuration;

namespace IoTPlatform.Tests;

public class MqttModeTests
{
    [Fact]
    public void Options_BindExternalMode()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<String, String?>
        {
            ["IoT:Mqtt:Mode"] = "External",
            ["IoT:Mqtt:TopicRoot"] = "/tenant-a/iot/",
            ["IoT:Mqtt:External:Server"] = "tcp://emqx.local:8883",
            ["IoT:Mqtt:External:ClientId"] = "platform-01",
            ["IoT:Mqtt:External:UserName"] = "platform",
            ["IoT:Mqtt:External:Password"] = "p@ss",
            ["IoT:Mqtt:External:UseTls"] = "true",
            ["IoT:Mqtt:External:QoS"] = "0",
        }).Build();

        var options = config.GetSection(IoTOptions.SectionName).Get<IoTOptions>()!;
        var mqtt = options.Mqtt;

        Assert.True(mqtt.IsExternal);
        Assert.False(mqtt.IsEmbedded);
        Assert.Equal(("tcp://emqx.local:8883", "platform-01", "platform", "p@ss", true, 0),
            (mqtt.External.Server, mqtt.External.ClientId, mqtt.External.UserName, mqtt.External.Password, mqtt.External.UseTls, mqtt.External.QoS));
        Assert.Equal("tenant-a/iot", Topics.Normalize(mqtt.TopicRoot));
    }

    [Fact]
    public void Options_DefaultToEmbedded()
    {
        var options = new ConfigurationBuilder().Build().GetSection(IoTOptions.SectionName).Get<IoTOptions>() ?? new IoTOptions();

        Assert.True(options.Mqtt.IsEmbedded);
        Assert.Equal(MqttModes.Embedded, options.Mqtt.Mode);
        Assert.Equal("iot", options.Mqtt.TopicRoot);

        options.Mqtt.Enable = false;
        Assert.False(options.Mqtt.IsEmbedded);
        Assert.False(options.Mqtt.IsExternal);
    }

    [Fact]
    public void Topics_ParseWithMultiLevelRoot()
    {
        Assert.True(Topics.TryParse("tenant-a/iot/env-sensor/th-001/property/post", "tenant-a/iot", out var pk, out var dc, out var kind));
        Assert.Equal(("env-sensor", "th-001", Topics.PropertyPost), (pk, dc, kind));
        Assert.False(Topics.TryParse("iot/env-sensor/th-001/property/post", "tenant-a/iot", out _, out _, out _));
        Assert.Equal("iot", Topics.Normalize("iot/#"));
        Assert.Equal("iot", Topics.Normalize("  "));
        Assert.Contains(Topics.Status, Topics.Uplinks);
    }

    [Theory]
    [InlineData("online", true)]
    [InlineData("\"offline\"", false)]
    [InlineData("1", true)]
    [InlineData("{\"online\":false}", false)]
    [InlineData("{\"status\":\"online\"}", true)]
    [InlineData("hello", null)]
    public void Router_ParsesStatusPayload(String payload, Boolean? expected) =>
        Assert.Equal(expected, MqttUplinkRouter.ParseStatus(Encoding.UTF8.GetBytes(payload)));

    private static MqttOptions Bind(String json)
    {
        var config = new ConfigurationBuilder().AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(json))).Build();
        return (config.GetSection(IoTOptions.SectionName).Get<IoTOptions>() ?? new IoTOptions()).Mqtt;
    }

    [Fact]
    public void Brokers_BindListAndSelectById()
    {
        var mqtt = Bind("""
        {"IoT":{"Mqtt":{"Mode":"External","ActiveBroker":"emqx","Brokers":[
            {"Id":"mosquitto","Name":"Local Mosquitto","Server":"tcp://127.0.0.1:1884"},
            {"Id":"emqx","Name":"EMQX cluster","Server":"tcp://emqx.prod:8883","UseTls":true,"UserName":"platform","Password":"x","TopicRoot":"tenant-a/iot"}
        ]}}}
        """);

        Assert.Equal(2, mqtt.GetBrokers().Count);
        var active = mqtt.GetActiveBroker()!;
        Assert.Equal(("emqx", "tcp://emqx.prod:8883", true), (active.Id, active.Server, active.UseTls));
        Assert.Equal("tenant-a/iot", mqtt.EffectiveTopicRoot);

        mqtt.ActiveBroker = "Local Mosquitto";
        Assert.Equal("mosquitto", mqtt.GetActiveBroker()!.Id);
        Assert.Equal("iot", mqtt.EffectiveTopicRoot);

        mqtt.ActiveBroker = null;
        Assert.Equal("mosquitto", mqtt.GetActiveBroker()!.Id);

        mqtt.ActiveBroker = "hivemq";
        Assert.Null(mqtt.GetActiveBroker());
    }

    [Fact]
    public void Brokers_BindFromEnvironmentStyleKeys()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<String, String?>
        {
            ["IoT:Mqtt:Mode"] = "External",
            ["IoT:Mqtt:ActiveBroker"] = "b",
            ["IoT:Mqtt:Brokers:0:Id"] = "a",
            ["IoT:Mqtt:Brokers:0:Server"] = "tcp://a:1883",
            ["IoT:Mqtt:Brokers:1:Id"] = "b",
            ["IoT:Mqtt:Brokers:1:Server"] = "tcp://b:1883",
            ["IoT:Mqtt:Brokers:1:QoS"] = "0",
            ["IoT:Mqtt:Brokers:2:Server"] = "tcp://c:1883",
        }).Build();
        var mqtt = config.GetSection(IoTOptions.SectionName).Get<IoTOptions>()!.Mqtt;

        Assert.Equal(new[] { "a", "b", "broker-3" }, mqtt.GetBrokers().Select(e => e.Id));
        Assert.Equal(("tcp://b:1883", 0), (mqtt.GetActiveBroker()!.Server, mqtt.GetActiveBroker()!.QoS));
    }

    [Fact]
    public void Brokers_LegacySingleExternalStillWorks()
    {
        var mqtt = Bind("""{"IoT":{"Mqtt":{"Mode":"External","External":{"Server":"tcp://legacy:1883","ClientId":"old"}}}}""");

        var only = Assert.Single(mqtt.GetBrokers());
        Assert.Equal(("default", "tcp://legacy:1883", "old"), (only.Id, only.Server, only.ClientId));
        Assert.Same(only, mqtt.GetActiveBroker());

        mqtt.ActiveBroker = "default";
        Assert.Same(only, mqtt.GetActiveBroker());

        // 配置了 Brokers 时忽略旧版 External
        var both = Bind("""{"IoT":{"Mqtt":{"External":{"Server":"tcp://legacy:1883"},"Brokers":[{"Id":"new","Server":"tcp://new:1883"}]}}}""");
        Assert.Equal("new", Assert.Single(both.GetBrokers()).Id);
    }

    [Fact]
    public void Assignment_DeviceOverridesProductThenPlatform()
    {
        var mqtt = new MqttOptions
        {
            Mode = MqttModes.External,
            ActiveBroker = "emqx",
            Brokers =
            [
                new() { Id = "mosquitto", Name = "Local Mosquitto", Server = "tcp://127.0.0.1:1884" },
                new() { Id = "emqx", Name = "EMQX cluster", Server = "tcp://emqx.prod:8883" },
            ],
        };
        var catalog = new MqttBrokerCatalog(Microsoft.Extensions.Options.Options.Create(new IoTOptions { Mqtt = mqtt }));

        Assert.Equal("emqx", catalog.PlatformDefaultId);
        Assert.Equal("emqx", catalog.Resolve(deviceBrokerId: null, productBrokerId: null));
        Assert.Equal("mosquitto", catalog.Resolve(deviceBrokerId: null, productBrokerId: "mosquitto"));
        Assert.Equal("mosquitto", catalog.Resolve(deviceBrokerId: null, productBrokerId: "Local Mosquitto"));
        Assert.Equal(MqttBrokerIds.Embedded, catalog.Resolve(deviceBrokerId: "embedded", productBrokerId: "emqx"));
        Assert.Equal("mosquitto", catalog.Resolve(deviceBrokerId: "mosquitto", productBrokerId: "emqx"));
        Assert.True(catalog.IsKnown("embedded"));
        Assert.True(catalog.IsKnown("EMQX cluster"));
        Assert.True(catalog.IsKnown(null));
        Assert.False(catalog.IsKnown("hivemq"));
        Assert.Throws<PlatformException>(() => catalog.EnsureKnown("hivemq"));
    }

    [Fact]
    public void Assignment_EmbeddedIsPlatformDefault()
    {
        var catalog = new MqttBrokerCatalog(Microsoft.Extensions.Options.Options.Create(new IoTOptions()));

        Assert.Equal(MqttBrokerIds.Embedded, catalog.PlatformDefaultId);
        Assert.Equal(MqttBrokerIds.Embedded, catalog.Resolve(deviceBrokerId: null, productBrokerId: null));
        Assert.True(catalog.IsEmbedded("Embedded"));
        Assert.Equal(MqttBrokerIds.Embedded, catalog.Canonicalize("Embedded"));
        Assert.Null(catalog.Canonicalize("  "));
        Assert.Contains(catalog.Choices(), e => e.Id == MqttBrokerIds.Embedded && e.IsDefault);
    }
}
