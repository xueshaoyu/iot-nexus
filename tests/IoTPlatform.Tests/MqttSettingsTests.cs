using IoTPlatform.Services;
using Microsoft.Extensions.Options;

namespace IoTPlatform.Tests;

public class MqttSettingsTests
{
    [Fact]
    public void EnvLocks_DetectsIoTMqttKeys()
    {
        var locks = MqttEnvLocks.Detect(
        [
            "IoT__Mqtt__Port",
            "IoT:Mqtt:AdminPassword",
            "PATH",
            "IoT__Mqtt__Brokers__0__Server",
            "IoT__Demo__SeedData",
        ]);

        Assert.True(MqttEnvLocks.IsLocked(locks, "Port"));
        Assert.True(MqttEnvLocks.IsLocked(locks, "AdminPassword"));
        Assert.True(MqttEnvLocks.BrokersLocked(locks));
        Assert.False(MqttEnvLocks.IsLocked(locks, "Enable"));
        Assert.Null(MqttEnvLocks.Normalize("PATH"));
        Assert.Equal("Brokers:0:Server", MqttEnvLocks.Normalize("IoT__Mqtt__Brokers__0__Server"));
    }

    [Fact]
    public void Merge_UiOverridesBaseline_ExceptLockedFields()
    {
        var baseline = new MqttOptions
        {
            Enable = true,
            Port = 1883,
            AdminUser = "admin",
            AdminPassword = "from-appsettings",
            Mode = MqttModes.Embedded,
            Brokers = [new() { Id = "local", Server = "tcp://127.0.0.1:1884" }],
        };
        var ui = new MqttUiSettings
        {
            Port = 18830,
            AdminPassword = "from-ui",
            Enable = false,
            Brokers = [new() { Id = "emqx", Server = "tcp://emqx:8883", UseTls = true, Enable = true }],
        };
        var locks = MqttEnvLocks.Detect(["IoT__Mqtt__Port"]);

        var merged = MqttSettingsMerger.Merge(baseline, ui, locks);

        Assert.Equal(1883, merged.Port);
        Assert.Equal("from-ui", merged.AdminPassword);
        Assert.False(merged.Enable);
        Assert.Equal("emqx", Assert.Single(merged.GetBrokers()).Id);
    }

    [Fact]
    public void Merge_LockedBrokersKeepBaselineList()
    {
        var baseline = new MqttOptions
        {
            Brokers = [new() { Id = "env-broker", Server = "tcp://from-env:1883" }],
        };
        var ui = new MqttUiSettings
        {
            Brokers = [new() { Id = "ui-broker", Server = "tcp://from-ui:1883" }],
        };

        var merged = MqttSettingsMerger.Merge(baseline, ui, MqttEnvLocks.Detect(["IoT__Mqtt__Brokers__0__Id"]));

        Assert.Equal("env-broker", Assert.Single(merged.GetBrokers()).Id);
    }

    [Fact]
    public void KeepPasswords_EmptyMeansUnchanged()
    {
        var previous = new MqttUiSettings
        {
            AdminPassword = "secret-admin",
            Brokers = [new() { Id = "emqx", Password = "broker-secret" }],
        };
        var incoming = new MqttUiSettings
        {
            AdminPassword = "",
            Brokers = [new() { Id = "emqx", Server = "tcp://emqx:8883", Password = "" }],
        };

        MqttSettingsMerger.KeepPasswords(incoming, previous, new MqttOptions());

        Assert.Equal("secret-admin", incoming.AdminPassword);
        Assert.Equal("broker-secret", incoming.Brokers![0].Password);
    }

    [Fact]
    public void RestartClassification_PortAndModeNeedEmbeddedRestart()
    {
        var before = new MqttOptions { Port = 1883, Mode = MqttModes.Embedded, AdminPassword = "a" };
        var afterPort = MqttSettingsMerger.Clone(before);
        afterPort.Port = 1884;
        Assert.True(MqttSettingsMerger.NeedsEmbeddedRestart(before, afterPort));

        var afterPassword = MqttSettingsMerger.Clone(before);
        afterPassword.AdminPassword = "b";
        Assert.True(MqttSettingsMerger.IsHotReloadOnly(before, afterPassword));
    }

    [Fact]
    public void GetActiveBroker_SkipsDisabled()
    {
        var mqtt = new MqttOptions
        {
            Mode = MqttModes.External,
            ActiveBroker = "emqx",
            Brokers =
            [
                new() { Id = "mosquitto", Server = "tcp://127.0.0.1:1884", Enable = true },
                new() { Id = "emqx", Server = "tcp://emqx:8883", Enable = false },
            ],
        };

        Assert.Null(mqtt.GetActiveBroker());
        Assert.Equal("emqx", mqtt.GetPlatformDefaultId());

        mqtt.ActiveBroker = null;
        Assert.Equal("mosquitto", mqtt.GetActiveBroker()!.Id);
        Assert.Equal("mosquitto", mqtt.GetPlatformDefaultId());
    }

    [Fact]
    public void Store_UsesNewLifeDataPathEnv()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"mqtt-data-{Guid.NewGuid():N}");
        var previous = Environment.GetEnvironmentVariable("NEWLIFE_DATAPATH");
        try
        {
            Environment.SetEnvironmentVariable("NEWLIFE_DATAPATH", dir);
            var store = new MqttSettingsStore();
            Assert.Equal(Path.GetFullPath(Path.Combine(dir, "Config", "mqtt-ui.json")), store.FilePath);
        }
        finally
        {
            Environment.SetEnvironmentVariable("NEWLIFE_DATAPATH", previous);
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    [Fact]
    public async Task StoreAndService_SaveThenReload_EnvWins()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"mqtt-ui-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var live = new IoTOptions
            {
                Mqtt = new MqttOptions
                {
                    Port = 1883,
                    AdminUser = "admin",
                    AdminPassword = "admin123",
                    Mode = MqttModes.Embedded,
                    Brokers = [new() { Id = "local", Name = "Local", Server = "tcp://127.0.0.1:1884" }],
                },
            };
            var store = new MqttSettingsStore(dir);
            var runtime = new RecordingRuntime();
            var service = new MqttSettingsService(Options.Create(live), store, new HashSet<String> { "Port" }, runtime);

            service.ApplyAtStartup();
            Assert.Equal(1883, live.Mqtt.Port);

            await service.SaveBuiltInAsync(new MqttBuiltInInput
            {
                Enable = true,
                Mode = MqttModes.External,
                TopicRoot = "tenant/iot",
                Port = 1883,
                AdminUser = "ops",
                AdminPassword = "n3w",
                ActiveBroker = "local",
            });

            Assert.Equal(1883, live.Mqtt.Port);
            Assert.Equal(MqttModes.External, live.Mqtt.Mode);
            Assert.Equal("tenant/iot", live.Mqtt.TopicRoot);
            Assert.Equal("ops", live.Mqtt.AdminUser);
            Assert.Equal("n3w", live.Mqtt.AdminPassword);
            Assert.Equal(1, runtime.Calls);

            await Assert.ThrowsAsync<PlatformException>(() => service.SaveBuiltInAsync(new MqttBuiltInInput
            {
                Enable = true,
                Mode = MqttModes.External,
                TopicRoot = "tenant/iot",
                Port = 1999,
                AdminUser = "ops",
            }));

            await service.UpsertBrokerAsync(new MqttBrokerInput
            {
                Id = "emqx",
                Name = "EMQX",
                Server = "tcp://emqx.example.com:8883",
                UseTls = true,
                Enable = false,
            });
            Assert.Equal(2, live.Mqtt.GetBrokers().Count);
            Assert.False(live.Mqtt.FindBroker("emqx")!.Enable);

            var reloaded = new MqttSettingsStore(dir).Load();
            Assert.NotNull(reloaded);
            Assert.Equal("n3w", reloaded!.AdminPassword);
            Assert.Contains(reloaded.Brokers!, e => e.Id == "emqx");
            Assert.Contains("n3w", File.ReadAllText(store.FilePath));
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    [Fact]
    public async Task Service_EmptyPasswordKeepsPrevious_AndRejectsReservedId()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"mqtt-ui-{Guid.NewGuid():N}");
        var live = new IoTOptions { Mqtt = new MqttOptions { AdminPassword = "keep-me", Brokers = [] } };
        var service = new MqttSettingsService(Options.Create(live), new MqttSettingsStore(dir), new HashSet<String>());

        await service.SaveBuiltInAsync(new MqttBuiltInInput { Enable = true, Mode = MqttModes.Embedded, Port = 1883, AdminPassword = "first" });
        await service.SaveBuiltInAsync(new MqttBuiltInInput { Enable = true, Mode = MqttModes.Embedded, Port = 1883, AdminPassword = "" });
        Assert.Equal("first", live.Mqtt.AdminPassword);

        var ex = await Assert.ThrowsAsync<PlatformException>(() => service.UpsertBrokerAsync(new MqttBrokerInput
        {
            Id = "embedded",
            Server = "tcp://127.0.0.1:1883",
        }));
        Assert.Equal("MQTT broker id '{0}' is reserved", ex.Template);

        try { Directory.Delete(dir, true); } catch { }
    }

    [Fact]
    public void Connector_BuildsWssWebSocketOptions()
    {
        var options = IoTPlatform.Mqtt.ExternalMqttConnector.BuildOptions(new ExternalMqttOptions
        {
            Scheme = "wss",
            Host = "mqtt.example.com",
            Port = 443,
            Path = "/mqtt",
            ClientId = "probe",
            UserName = "u",
            KeepAlive = 20,
        });

        Assert.Equal("probe", options.ClientId);
        var channel = Assert.IsType<MQTTnet.Client.MqttClientWebSocketOptions>(options.ChannelOptions);
        Assert.Equal("wss://mqtt.example.com:443/mqtt", channel.Uri);
        Assert.NotNull(options.ChannelOptions.TlsOptions);
        Assert.True(options.ChannelOptions.TlsOptions!.UseTls);
    }

    [Fact]
    public async Task Tester_RejectsEmptyServer()
    {
        var tester = new IoTPlatform.Mqtt.MqttConnectionTester();
        var result = await tester.TestAsync(new ExternalMqttOptions { Server = "" });
        Assert.False(result.Ok);
        Assert.Equal("Server is required", result.Error);
    }

    [Fact]
    public void Endpoint_ParsesWssAndRepairsTypo()
    {
        var typed = MqttBrokerEndpoint.Parse("wss:/mqtt.example.com");
        Assert.Equal(("wss", "mqtt.example.com", 443, "/", true, "wss://mqtt.example.com:443/"),
            (typed.Scheme, typed.Host, typed.Port, typed.Path, typed.UseTls, typed.Server));

        var withPath = MqttBrokerEndpoint.Parse("wss://mqtt.example.com:443/mqtt");
        Assert.Equal(("wss", "mqtt.example.com", 443, "/mqtt", "wss://mqtt.example.com:443/mqtt"),
            (withPath.Scheme, withPath.Host, withPath.Port, withPath.Path, withPath.Server));

        var root = MqttBrokerEndpoint.Parse("wss://mqtt.example.com/");
        Assert.Equal("/", root.Path);

        var ws = MqttBrokerEndpoint.Parse("ws://broker.local:8083/ws");
        Assert.Equal(("ws", 8083, "/ws", false), (ws.Scheme, ws.Port, ws.Path, ws.UseTls));

        var mqtts = MqttBrokerEndpoint.Parse("mqtts://emqx.example.com");
        Assert.Equal(("ssl", "emqx.example.com", 8883, "", true, "ssl://emqx.example.com:8883"),
            (mqtts.Scheme, mqtts.Host, mqtts.Port, mqtts.Path, mqtts.UseTls, mqtts.Server));

        var tcp = MqttBrokerEndpoint.Parse("127.0.0.1:1884");
        Assert.Equal(("tcp", "127.0.0.1", 1884, "", false), (tcp.Scheme, tcp.Host, tcp.Port, tcp.Path, tcp.UseTls));
    }

    [Fact]
    public void Endpoint_StructuredFieldsComposeServer_AndTcpPlusTlsBecomesSsl()
    {
        var wss = new ExternalMqttOptions
        {
            Scheme = "wss",
            Host = "mqtt.example.com",
            Port = 443,
            Path = "/mqtt",
        };
        MqttBrokerEndpoint.Apply(wss);
        Assert.Equal("wss://mqtt.example.com:443/mqtt", wss.Server);
        Assert.True(wss.UseTls);
        Assert.Equal("/mqtt", wss.Path);

        var promoted = new ExternalMqttOptions { Server = "tcp://emqx.example.com:8883", UseTls = true };
        MqttBrokerEndpoint.Apply(promoted);
        Assert.Equal("ssl", promoted.Scheme);
        Assert.Equal("ssl://emqx.example.com:8883", promoted.Server);
        Assert.True(promoted.UseTls);
    }

    [Fact]
    public async Task Service_UpsertBroker_ComposesWssPath()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"mqtt-ui-{Guid.NewGuid():N}");
        var live = new IoTOptions { Mqtt = new MqttOptions { Brokers = [] } };
        var service = new MqttSettingsService(Options.Create(live), new MqttSettingsStore(dir), new HashSet<String>());

        await service.UpsertBrokerAsync(new MqttBrokerInput
        {
            Id = "cloud",
            Name = "Cloud WSS",
            Scheme = "wss",
            Host = "mqtt.example.com",
            Port = 443,
            Path = "/mqtt",
            UserName = "platform",
            Enable = true,
        });

        var saved = live.Mqtt.FindBroker("cloud")!;
        Assert.Equal("wss://mqtt.example.com:443/mqtt", saved.Server);
        Assert.Equal("wss", saved.Scheme);
        Assert.Equal("/mqtt", saved.Path);
        Assert.True(saved.UseTls);

        var ex = await Assert.ThrowsAsync<PlatformException>(() => service.UpsertBrokerAsync(new MqttBrokerInput
        {
            Id = "bad",
            Scheme = "quic",
            Host = "x",
            Server = "quic://x",
        }));
        Assert.Equal("MQTT broker scheme '{0}' is invalid", ex.Template);

        try { Directory.Delete(dir, true); } catch { }
    }

    sealed class RecordingRuntime : IMqttRuntimeControl
    {
        public Int32 Calls;
        public Task ApplyAsync(MqttOptions before, MqttOptions after, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.CompletedTask;
        }
    }
}
