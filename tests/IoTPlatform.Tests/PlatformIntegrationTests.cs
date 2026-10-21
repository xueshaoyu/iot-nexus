using System.Net;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using IoTPlatform.Data;
using IoTPlatform.Models;
using IoTPlatform.Mqtt;
using IoTPlatform.Protocols;
using IoTPlatform.Protocols.Drivers;
using IoTPlatform.Rules;
using IoTPlatform.Services;
using IoTPlatform.Simulator;
using Microsoft.Extensions.Hosting;
using NewLife.Data;
using NewLife.MQTT;
using NewLife.MQTT.Messaging;

namespace IoTPlatform.Tests;

/// <summary>启动真实的核心服务、MQTT 服务端与驱动主机，验证端到端链路</summary>
public sealed class PlatformFixture : IAsyncLifetime
{
    public ServiceProvider Services { get; private set; } = default!;
    public Int32 MqttPort { get; } = FreePort();
    public Int32 ModbusPort { get; } = FreePort();

    private readonly List<IHostedService> _hosted = [];

    public async Task InitializeAsync()
    {
        var db = Path.Combine(Path.GetTempPath(), $"iot-test-{Guid.NewGuid():N}.db");
        XCode.XCodeSetting.Current.ShowSQL = false;
        IoTDatabase.Init($"Data Source={db}");

        var services = new ServiceCollection();
        services.Configure<IoTOptions>(o =>
        {
            o.Mqtt.Port = MqttPort;
            o.OfflineTimeout = 30;
        });
        services.AddIoTCore().AddIoTProtocols().AddIoTMqtt();
        Services = services.BuildServiceProvider();

        foreach (var h in new IHostedService[] { Services.GetRequiredService<RuleEngine>(), Services.GetRequiredService<MqttServerHost>() })
        {
            await h.StartAsync(CancellationToken.None);
            _hosted.Add(h);
        }
    }

    public async Task DisposeAsync()
    {
        foreach (var h in _hosted) await h.StopAsync(CancellationToken.None);
        await Services.DisposeAsync();
    }

    public T Get<T>() where T : notnull => Services.GetRequiredService<T>();

    private static Int32 FreePort()
    {
        var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        var port = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return port;
    }
}

[Collection("platform-db")]
public class PlatformIntegrationTests(PlatformFixture fixture)
{
    private static async Task<Boolean> WaitFor(Func<Boolean> condition, Int32 timeoutMs = 8000)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            if (condition()) return true;
            await Task.Delay(100);
        }

        return condition();
    }

    [Fact]
    public async Task Mqtt_Device_EndToEnd()
    {
        var products = fixture.Get<ProductService>();
        var devices = fixture.Get<DeviceService>();
        var rules = fixture.Get<RuleService>();
        var commands = fixture.Get<DeviceCommandService>();

        var product = products.Create(new ProductInput { Name = "Test sensor", Code = "t-sensor", Protocol = AccessProtocols.Mqtt });
        products.SaveProperty(product.Id, 0, new ThingPropertyInput { Identifier = "temperature", DataType = "float", Step = 0.1 });
        products.SaveProperty(product.Id, 0, new ThingPropertyInput { Identifier = "switch", DataType = "bool", AccessMode = "rw" });
        products.SaveService(product.Id, 0, new ThingServiceInput { Identifier = "reboot" });
        var device = devices.Create(new DeviceInput { ProductId = product.Id, Code = "t-001", Secret = "s3cret" });
        rules.Create(new RuleInput
        {
            Name = "hot",
            ProductId = product.Id,
            Conditions = [new() { Property = "temperature", Operator = ">", Value = "30" }],
            Actions = [new() { Type = "alarm", Parameters = new() { ["level"] = "Critical", ["title"] = "Hot {device.code}" } }],
            SilenceSeconds = 0,
        });

        // 错误密钥被拒绝
        using (var bad = new MqttClient { Server = $"tcp://127.0.0.1:{fixture.MqttPort}", ClientId = "t-001", UserName = "t-001", Password = "wrong" })
        {
            var ex = await Assert.ThrowsAnyAsync<Exception>(() => bad.ConnectAsync());
            Assert.Contains(nameof(ConnectReturnCode.RefusedBadUsernameOrPassword), ex.Message);
        }

        using var client = new MqttClient { Server = $"tcp://127.0.0.1:{fixture.MqttPort}", ClientId = "t-001", UserName = "t-001", Password = "s3cret" };
        Assert.Equal(ConnectReturnCode.Accepted, (await client.ConnectAsync())!.ReturnCode);
        Assert.True(await WaitFor(() => Device.FindById(device.Id)!.Online), "device should be online after CONNECT");

        await client.SubscribeAsync("iot/t-sensor/t-001/service/invoke", msg =>
        {
            using var doc = JsonDocument.Parse(msg.Payload!.ToStr());
            var id = doc.RootElement.GetProperty("id").GetInt64();
            _ = client.PublishAsync(new PublishMessage
            {
                Topic = "iot/t-sensor/t-001/service/reply",
                Payload = new ArrayPacket(Encoding.UTF8.GetBytes($$"""{"id":{{id}},"status":2,"data":"rebooting"}""")),
            });
        });

        await client.PublishAsync(new PublishMessage
        {
            Topic = "iot/t-sensor/t-001/property/post",
            Payload = new ArrayPacket(Encoding.UTF8.GetBytes("""{"temperature":35.26,"switch":true}""")),
            QoS = QualityOfService.AtLeastOnce,
        });

        Assert.True(await WaitFor(() => DeviceProperty.FindByDeviceIdAndName(device.Id, "temperature")?.Value == "35.3"), "shadow should hold the step-rounded value");
        Assert.True(await WaitFor(() => Alarm.FindCount(Alarm._.DeviceId == device.Id) > 0), "threshold rule should raise an alarm");

        // 不能冒充其它设备发布
        await client.PublishAsync(new PublishMessage { Topic = "iot/t-sensor/other/property/post", Payload = new ArrayPacket(Encoding.UTF8.GetBytes("""{"temperature":1}""")) });

        var record = await commands.InvokeServiceAsync(Device.FindById(device.Id)!, "reboot", new Dictionary<String, Object?>(), "test", waitSeconds: 5);
        Assert.Equal(ServiceStatusKinds.Success, record.Status);
        Assert.Equal("rebooting", record.OutputData);

        await Assert.ThrowsAsync<PlatformException>(() => commands.SetPropertiesAsync(device, new Dictionary<String, Object?> { ["temperature"] = 1 }, "test"));

        await client.DisconnectAsync();
        Assert.True(await WaitFor(() => !Device.FindById(device.Id)!.Online), "device should go offline after DISCONNECT");
        Assert.True(await WaitFor(() => DeviceHistory.FindCount(DeviceHistory._.DeviceId == device.Id & DeviceHistory._.Action == DeviceActions.Offline) > 0), "offline should be recorded in device history");
    }

    [Fact]
    public async Task Drivers_Simulator_And_ModbusTcp()
    {
        var products = fixture.Get<ProductService>();
        var devices = fixture.Get<DeviceService>();
        var host = fixture.Get<DriverHostService>();
        DriverRegistry.EnsureRegistered();

        var boiler = products.Create(new ProductInput { Name = "Boiler", Code = "t-boiler", Protocol = AccessProtocols.Driver, DriverName = "Simulator", PollInterval = 500 });
        products.SaveProperty(boiler.Id, 0, new ThingPropertyInput { Identifier = "waterTemp", DataType = "float", Min = 60, Max = 90 });
        products.SaveProperty(boiler.Id, 0, new ThingPropertyInput { Identifier = "burner", DataType = "bool", AccessMode = "rw" });
        var b1 = devices.Create(new DeviceInput { ProductId = boiler.Id, Code = "t-boiler-1" });

        using var slave = new ModbusSlaveSimulator(fixture.ModbusPort);
        slave.Start();
        var meter = products.Create(new ProductInput
        {
            Name = "Meter",
            Code = "t-meter",
            Protocol = AccessProtocols.Driver,
            DriverName = "ModbusTcp",
            DriverParameter = $$"""{"Server":"127.0.0.1:{{fixture.ModbusPort}}","Host":1,"ReadCode":3,"WriteCode":6}""",
            PollInterval = 500,
        });
        products.SaveProperty(meter.Id, 0, new ThingPropertyInput { Identifier = "voltage", DataType = "float", Address = "4x0", PointType = "ushort", Scaling = 0.1 });
        products.SaveProperty(meter.Id, 0, new ThingPropertyInput { Identifier = "relay", DataType = "int", Address = "4x6", PointType = "ushort", AccessMode = "rw" });
        var m1 = devices.Create(new DeviceInput { ProductId = meter.Id, Code = "t-meter-1" });

        await host.ReconcileAsync(CancellationToken.None);

        Assert.True(await WaitFor(() => Device.FindById(b1.Id)!.Online && DeviceProperty.FindByDeviceIdAndName(b1.Id, "waterTemp") != null), "simulator device should report");
        var waterTemp = Double.Parse(DeviceProperty.FindByDeviceIdAndName(b1.Id, "waterTemp")!.Value!, System.Globalization.CultureInfo.InvariantCulture);
        Assert.InRange(waterTemp, 60, 90);

        Assert.True(await WaitFor(() => DeviceProperty.FindByDeviceIdAndName(m1.Id, "voltage") != null, 10000), "modbus device should report voltage");
        var voltage = Double.Parse(DeviceProperty.FindByDeviceIdAndName(m1.Id, "voltage")!.Value!, System.Globalization.CultureInfo.InvariantCulture);
        Assert.InRange(voltage, 210, 230);

        var commands = fixture.Get<DeviceCommandService>();
        var write = await commands.SetPropertiesAsync(Device.FindById(m1.Id)!, new Dictionary<String, Object?> { ["relay"] = 0 }, "test");
        Assert.Equal(ServiceStatusKinds.Success, write.Status);
        Assert.True(await WaitFor(() => DeviceProperty.FindByDeviceIdAndName(m1.Id, "relay")?.Value == "0"), "relay write should be read back");

        devices.SetEnable(b1.Id, false);
        await host.ReconcileAsync(CancellationToken.None);
        Assert.False(Device.FindById(b1.Id)!.Online);
    }

    [Fact]
    public async Task Drivers_ModbusTcp_ReadsAndWritesBitFields()
    {
        var products = fixture.Get<ProductService>();
        var devices = fixture.Get<DeviceService>();
        var host = fixture.Get<DriverHostService>();
        DriverRegistry.EnsureRegistered();

        var port = FreePortForTest();
        using var slave = new ModbusSlaveSimulator(port);
        slave.Start();

        var meter = products.Create(new ProductInput
        {
            Name = "Bit meter",
            Code = "t-bit-meter",
            Protocol = AccessProtocols.Driver,
            DriverName = "ModbusTcp",
            DriverParameter = $$"""{"Server":"127.0.0.1:{{port}}","Host":1,"ReadCode":3,"WriteCode":6}""",
            PollInterval = 500,
        });
        products.SaveProperty(meter.Id, 0, new ThingPropertyInput { Identifier = "running", DataType = "int", Address = "4x6.0", PointType = "ushort", AccessMode = "rw" });
        products.SaveProperty(meter.Id, 0, new ThingPropertyInput { Identifier = "alarm", DataType = "int", Address = "4x6.1", PointType = "ushort", AccessMode = "rw" });
        var device = devices.Create(new DeviceInput { ProductId = meter.Id, Code = "t-bit-meter-1" });

        await host.ReconcileAsync(CancellationToken.None);

        Assert.True(await WaitFor(() => Shadow(device.Id, "running") == "1" && Shadow(device.Id, "alarm") == "0"), "bit 0 of register 6 starts set");

        var commands = fixture.Get<DeviceCommandService>();
        var write = await commands.SetPropertiesAsync(Device.FindById(device.Id)!, new Dictionary<String, Object?> { ["alarm"] = 1 }, "test");
        Assert.Equal(ServiceStatusKinds.Success, write.Status);
        Assert.True(await WaitFor(() => Shadow(device.Id, "running") == "1" && Shadow(device.Id, "alarm") == "1"), "setting bit 1 keeps bit 0");

        write = await commands.SetPropertiesAsync(Device.FindById(device.Id)!, new Dictionary<String, Object?> { ["running"] = 0 }, "test");
        Assert.Equal(ServiceStatusKinds.Success, write.Status);
        Assert.True(await WaitFor(() => Shadow(device.Id, "running") == "0" && Shadow(device.Id, "alarm") == "1"), "clearing bit 0 keeps bit 1");
    }

    private static String? Shadow(Int32 deviceId, String name) => DeviceProperty.FindByDeviceIdAndName(deviceId, name)?.Value;

    private static Device ForceOnline(Device device, Boolean online)
    {
        device.Online = online;
        device.LastActive = DateTime.Now;
        device.Update();
        return device;
    }

    [Fact]
    public void Batch_Resolve_ProductGroupAndList()
    {
        var products = fixture.Get<ProductService>();
        var devices = fixture.Get<DeviceService>();
        var groups = fixture.Get<DeviceGroupService>();
        var batch = fixture.Get<BatchControlService>();

        var lamp = products.Create(new ProductInput { Name = "Batch lamp", Code = "b-lamp", Protocol = AccessProtocols.Mqtt });
        products.SaveProperty(lamp.Id, 0, new ThingPropertyInput { Identifier = "switch", DataType = "bool", AccessMode = "rw" });
        products.SaveProperty(lamp.Id, 0, new ThingPropertyInput { Identifier = "power", DataType = "float" });
        products.SaveService(lamp.Id, 0, new ThingServiceInput { Identifier = "blink" });
        var gauge = products.Create(new ProductInput { Name = "Batch gauge", Code = "b-gauge", Protocol = AccessProtocols.Http });
        products.SaveService(gauge.Id, 0, new ThingServiceInput { Identifier = "blink" });

        var parent = groups.Create(new GroupInput { Name = "Batch site" });
        var child = groups.Create(new GroupInput { Name = "Batch floor", ParentId = parent.Id });

        var d1 = ForceOnline(devices.Create(new DeviceInput { ProductId = lamp.Id, GroupId = parent.Id, Code = "b-1" }), true);
        var d2 = devices.Create(new DeviceInput { ProductId = lamp.Id, GroupId = child.Id, Code = "b-2" });
        var d3 = devices.Create(new DeviceInput { ProductId = lamp.Id, GroupId = parent.Id, Code = "b-3", Enable = false });
        var d4 = devices.Create(new DeviceInput { ProductId = gauge.Id, GroupId = child.Id, Code = "b-4" });

        var setSwitch = new BatchControlInput { Kind = "property", Property = "switch", Value = true };
        String? Reason(IList<BatchTarget> list, Device d) => list.Single(e => e.DeviceId == d.Id).SkipReason;

        // 产品范围：自动模式跳过离线 MQTT 设备与禁用设备
        var byProduct = batch.Resolve(new BatchScope { Type = BatchScopeType.Product, ProductId = lamp.Id }, setSwitch);
        Assert.Equal(new[] { d1.Id, d2.Id, d3.Id }, byProduct.Select(e => e.DeviceId));
        Assert.Null(Reason(byProduct, d1));
        Assert.Equal("Device is offline", Reason(byProduct, d2));
        Assert.Equal("Device is disabled", Reason(byProduct, d3));

        var all = batch.Resolve(new BatchScope { Type = BatchScopeType.Product, ProductId = lamp.Id, OnlineMode = BatchOnlineMode.All }, setSwitch);
        Assert.Null(Reason(all, d2));

        // 分组范围：是否包含子分组
        var flat = batch.Resolve(new BatchScope { Type = BatchScopeType.Group, GroupId = parent.Id, IncludeSubGroups = false });
        Assert.Equal(new[] { d1.Id, d3.Id }, flat.Select(e => e.DeviceId));
        var deep = batch.Resolve(new BatchScope { Type = BatchScopeType.Group, GroupId = parent.Id, IncludeSubGroups = true });
        Assert.Equal(new[] { d1.Id, d2.Id, d3.Id, d4.Id }, deep.Select(e => e.DeviceId));

        // 设备列表：跨产品时按能力跳过；HTTP 设备离线也可排队
        var list = batch.Resolve(new BatchScope { Type = BatchScopeType.Devices, DeviceIds = [d4.Id, d1.Id, d1.Id] }, setSwitch);
        Assert.Equal(new[] { d1.Id, d4.Id }, list.Select(e => e.DeviceId));
        Assert.Equal("Property is not defined for this product", Reason(list, d4));

        var blink = batch.Resolve(new BatchScope { Type = BatchScopeType.Devices, DeviceIds = [d1.Id, d4.Id] }, new BatchControlInput { Kind = "service", Service = "blink" });
        Assert.All(blink, e => Assert.Null(e.SkipReason));

        var readOnly = batch.Resolve(new BatchScope { Type = BatchScopeType.Devices, DeviceIds = [d1.Id] }, new BatchControlInput { Kind = "property", Property = "power" });
        Assert.Equal("Property is read-only", Reason(readOnly, d1));

        Assert.Throws<PlatformException>(() => batch.Resolve(new BatchScope { Type = BatchScopeType.Devices }));
        Assert.Throws<PlatformException>(() => batch.Resolve(new BatchScope { Type = BatchScopeType.Product, ProductId = 99999 }));

        ForceOnline(d1, false);
    }

    [Fact]
    public async Task Batch_Mqtt_SetProperty_And_HttpQueue()
    {
        var products = fixture.Get<ProductService>();
        var devices = fixture.Get<DeviceService>();
        var batch = fixture.Get<BatchControlService>();

        var light = products.Create(new ProductInput { Name = "Batch light", Code = "bl", Protocol = AccessProtocols.Mqtt });
        products.SaveProperty(light.Id, 0, new ThingPropertyInput { Identifier = "brightness", DataType = "int", AccessMode = "rw" });
        var codes = new[] { "bl-1", "bl-2" };
        foreach (var code in codes) devices.Create(new DeviceInput { ProductId = light.Id, Code = code, Secret = "s" });
        var offline = devices.Create(new DeviceInput { ProductId = light.Id, Code = "bl-3", Secret = "s" });

        var clients = new List<MqttClient>();
        try
        {
            foreach (var code in codes)
            {
                var client = new MqttClient { Server = $"tcp://127.0.0.1:{fixture.MqttPort}", ClientId = code, UserName = code, Password = "s" };
                await client.ConnectAsync();
                await client.SubscribeAsync($"iot/bl/{code}/property/set", msg =>
                {
                    using var doc = JsonDocument.Parse(msg.Payload!.ToStr());
                    var id = doc.RootElement.GetProperty("id").GetInt64();
                    var brightness = doc.RootElement.GetProperty("params").GetProperty("brightness").GetInt32();
                    _ = client.PublishAsync(new PublishMessage
                    {
                        Topic = $"iot/bl/{code}/service/reply",
                        Payload = new ArrayPacket(Encoding.UTF8.GetBytes($$"""{"id":{{id}},"status":2,"data":"brightness={{brightness}}"}""")),
                    });
                });
                clients.Add(client);
            }
            Assert.True(await WaitFor(() => codes.All(c => Device.FindByCode(c)!.Online)));

            var progress = new List<BatchProgress>();
            var result = await batch.ExecuteAsync(new BatchControlInput
            {
                Scope = new BatchScope { Type = BatchScopeType.Product, ProductId = light.Id },
                Kind = "property",
                Property = "brightness",
                Value = "42",
                WaitSeconds = 5,
            }, "test", new SyncProgress(progress));

            Assert.Equal(3, result.Total);
            Assert.Equal(2, result.Succeeded);
            Assert.Equal(1, result.Skipped);
            Assert.Equal(BatchItemStatus.Skipped, result.Results.Single(e => e.DeviceId == offline.Id).Status);
            Assert.All(result.Results.Where(e => e.DeviceId != offline.Id), e =>
            {
                Assert.Equal(BatchItemStatus.Success, e.Status);
                Assert.Equal("brightness=42", e.Message);
                Assert.Equal($"Batch:{result.JobId}", ServiceRecord.FindById(e.RecordId)!.Source);
            });
            Assert.Equal(3, progress.Last().Done);

            var stored = batch.Get(result.JobId);
            Assert.Equal(2, stored.Succeeded);
            Assert.Equal(3, stored.Results.Count);
        }
        finally
        {
            foreach (var c in clients) { await c.DisconnectAsync(); c.Dispose(); }
        }

        // HTTP 设备离线时指令排队，等待设备拉取
        var gateway = products.Create(new ProductInput { Name = "Batch gateway", Code = "bg", Protocol = AccessProtocols.Http });
        products.SaveService(gateway.Id, 0, new ThingServiceInput { Identifier = "reboot" });
        var gw = devices.Create(new DeviceInput { ProductId = gateway.Id, Code = "bg-1" });
        var queued = await batch.ExecuteAsync(new BatchControlInput
        {
            Scope = new BatchScope { Type = BatchScopeType.Devices, DeviceIds = [gw.Id] },
            Kind = "service",
            Service = "reboot",
            WaitSeconds = 0,
        }, "test");
        Assert.Equal(1, queued.Pending);
        Assert.Single(fixture.Get<DeviceCommandService>().FetchPending(Device.FindById(gw.Id)!));
    }

    private sealed class SyncProgress(List<BatchProgress> list) : IProgress<BatchProgress>
    {
        public void Report(BatchProgress value) { lock (list) list.Add(value); }
    }

    private Bunit.BunitContext CreateUiContext()
    {
        var ctx = new Bunit.BunitContext();
        ctx.Services.AddLocalization(o => o.ResourcesPath = "Resources");
        ctx.Services.AddSingleton(fixture.Get<DeviceCommandService>());
        ctx.Services.AddSingleton(fixture.Get<ProductService>());
        ctx.Services.AddSingleton(fixture.Get<DeviceService>());
        ctx.Services.AddSingleton(fixture.Get<BatchControlService>());
        ctx.Services.AddSingleton(new IoTPlatform.Web.Services.ToastService());
        IoTPlatform.Web.Services.Fmt.Localizer = ctx.Services.GetRequiredService<Microsoft.Extensions.Localization.IStringLocalizer<IoTPlatform.Web.SharedResource>>();
        return ctx;
    }

    [Fact]
    public async Task Ui_DeviceControlPanel_SendsTypedCommands_AndShowsStatus()
    {
        var original = System.Globalization.CultureInfo.CurrentUICulture;
        System.Globalization.CultureInfo.CurrentUICulture = new System.Globalization.CultureInfo("zh-CN");
        try
        {
            var products = fixture.Get<ProductService>();
            var product = products.Create(new ProductInput { Name = "UI box", Code = "ui-box", Protocol = AccessProtocols.Http });
            products.SaveProperty(product.Id, 0, new ThingPropertyInput { Identifier = "switch", DataType = "bool", AccessMode = "rw", Mapping = "0=Off,1=On" });
            products.SaveProperty(product.Id, 0, new ThingPropertyInput { Identifier = "level", DataType = "int", AccessMode = "rw", Min = 0, Max = 10 });
            products.SaveService(product.Id, 0, new ThingServiceInput { Identifier = "beep", InputData = [new() { Identifier = "times", DataType = "int" }] });
            var device = fixture.Get<DeviceService>().Create(new DeviceInput { ProductId = product.Id, Code = "ui-box-1" });

            using var ctx = CreateUiContext();
            var cut = ctx.Render<IoTPlatform.Web.Components.Shared.DeviceControlPanel>(p => p.Add(e => e.DeviceId, device.Id));

            Assert.Contains("可写属性", cut.Markup);
            Assert.Contains("HTTP 设备", cut.Markup);

            // 布尔属性：开关按钮直接下发
            await cut.FindAll(".segmented button")[1].ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
            cut.WaitForAssertion(() => Assert.Contains("排队中，等待设备拉取", cut.Markup));
            // 相对时间放在固定宽度容器中，定时刷新时不会撑动列宽
            Assert.NotEmpty(cut.FindAll(".cmd-status .ago"));
            Assert.NotEmpty(cut.FindAll(".value-cell .ago"));
            var set = ServiceRecord.FindAll(ServiceRecord._.DeviceId == device.Id, "Id desc", null, 0, 1).Single();
            Assert.Equal("""{"switch":true}""", set.InputData);
            Assert.Equal(ServiceStatusKinds.Pending, set.Status);

            // 有范围的数值属性使用滑块，松开即下发
            Assert.Single(cut.FindAll("input[type=range]"));
            Assert.Empty(cut.FindAll(".slider input[type=number]"));
            Assert.Empty(cut.FindAll(".slider-scale"));
            await cut.Find("input[type=range]").ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = "7" });
            cut.WaitForAssertion(() => Assert.Equal(2, ServiceRecord.FindCount(ServiceRecord._.DeviceId == device.Id)));
            var slide = ServiceRecord.FindAll(ServiceRecord._.DeviceId == device.Id, "Id desc", null, 0, 1).Single();
            Assert.Equal("""{"level":7}""", slide.InputData);

            // 服务参数按物模型类型转换
            await cut.Find(".action-card input[type=number]").ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = "3" });
            await cut.FindAll("button").First(b => b.TextContent.Contains("调用")).ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
            cut.WaitForAssertion(() => Assert.Equal(3, ServiceRecord.FindCount(ServiceRecord._.DeviceId == device.Id)));
            var beep = ServiceRecord.FindAll(ServiceRecord._.DeviceId == device.Id & ServiceRecord._.Kind == "service", null, null, 0, 0).Single();
            Assert.Equal("""{"times":3}""", beep.InputData);
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentUICulture = original;
        }
    }

    [Fact]
    public async Task Ui_BatchControlDialog_PreviewsAndRuns()
    {
        var original = System.Globalization.CultureInfo.CurrentUICulture;
        System.Globalization.CultureInfo.CurrentUICulture = new System.Globalization.CultureInfo("zh-CN");
        try
        {
            var products = fixture.Get<ProductService>();
            var product = products.Create(new ProductInput { Name = "UI valve", Code = "ui-valve", Protocol = AccessProtocols.Http });
            products.SaveProperty(product.Id, 0, new ThingPropertyInput { Identifier = "opening", DataType = "int", AccessMode = "rw", Min = 0, Max = 100 });
            var devices = fixture.Get<DeviceService>();
            devices.Create(new DeviceInput { ProductId = product.Id, Code = "ui-valve-1" });
            devices.Create(new DeviceInput { ProductId = product.Id, Code = "ui-valve-2" });
            devices.Create(new DeviceInput { ProductId = product.Id, Code = "ui-valve-3", Enable = false });

            using var ctx = CreateUiContext();
            var cut = ctx.Render<IoTPlatform.Web.Components.Shared.BatchControlDialog>(p => p
                .Add(e => e.Show, true)
                .Add(e => e.Title, "批量控制")
                .Add(e => e.Scope, new BatchScope { Type = BatchScopeType.Product, ProductId = product.Id }));

            Assert.Contains("3 台设备", cut.Markup);
            Assert.Contains("2 台将执行 · 1 台跳过", cut.Markup);
            Assert.Contains("设备已禁用", cut.Markup);

            // 有范围的数值属性使用滑块，默认取最小值
            Assert.Single(cut.FindAll(".modal input[type=range]"));
            await cut.Find(".modal input[type=range]").ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = "40" });
            // 气泡内容由浏览器端 slider.js 计算（悬停预计值），这里校验其所需的范围与本地化标签
            var slider = cut.Find(".modal .slider");
            Assert.Equal(("0", "100", "1", "预计"), (slider.GetAttribute("data-min"), slider.GetAttribute("data-max"), slider.GetAttribute("data-step"), slider.GetAttribute("data-preview")));
            Assert.Contains("40", cut.Find(".modal label strong").TextContent);
            Assert.Empty(cut.FindAll(".modal .slider input[type=number]"));
            await cut.FindAll("button").First(b => b.TextContent.Contains("下发到 2 台设备")).ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

            cut.WaitForAssertion(() => Assert.Contains("刷新状态", cut.Markup), TimeSpan.FromSeconds(10));
            var job = fixture.Get<BatchControlService>().GetRecent(1).Single();
            Assert.Equal(("opening", 3, 2, 1), (job.Name, job.Total, job.Pending, job.Skipped));
            Assert.Contains("40", job.InputData);
            Assert.Contains("ui-valve-1", cut.Markup);
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentUICulture = original;
        }
    }

    [Fact]
    public async Task ExternalBroker_UplinkDownlinkAndStatus()
    {
        // 独立的 NewLife.MQTT 服务端充当“外部服务器”（无平台认证，任何客户端可连接）
        var brokerPort = FreePortForTest();
        var broker = new MqttServer { Port = brokerPort, ServiceProvider = new ServiceCollection().BuildServiceProvider() };
        broker.Start();

        var services = new ServiceCollection();
        services.Configure<IoTOptions>(o =>
        {
            o.Mqtt.Mode = MqttModes.External;
            o.Mqtt.Port = FreePortForTest();
            o.Mqtt.ActiveBroker = "test-broker";
            // 两个外部服务器配置：列表第一个不可达，选用的是第二个
            o.Mqtt.Brokers.Add(new ExternalMqttOptions { Id = "unreachable", Name = "Nowhere", Server = "tcp://127.0.0.1:1", ReconnectDelay = 1 });
            o.Mqtt.Brokers.Add(new ExternalMqttOptions { Id = "test-broker", Name = "Test broker", Server = $"tcp://127.0.0.1:{brokerPort}", ClientId = "platform-under-test", Password = "p@ss-hidden", ReconnectDelay = 1 });
        });
        services.AddIoTCore().AddIoTProtocols().AddIoTMqtt();
        await using var platform = services.BuildServiceProvider();
        var external = platform.GetRequiredService<ExternalMqttTransport>();
        var hub = platform.GetRequiredService<IMqttTransport>();
        Assert.Same(platform.GetRequiredService<MqttBrokerHub>(), hub);

        await platform.GetRequiredService<MqttServerHost>().StartAsync(CancellationToken.None);
        await external.StartAsync(CancellationToken.None);
        using var device = new MqttClient { Server = $"tcp://127.0.0.1:{brokerPort}", ClientId = "ext-1" };
        try
        {
            Assert.True(await WaitFor(() => hub.IsReady), "platform should connect to the external broker");
            var status = hub.GetStatus();
            Assert.Equal(Topics.Uplinks.Length + AlinkTopics.UplinkFilters().Length, status.Subscriptions.Count);
            Assert.Equal(("test-broker", $"tcp://127.0.0.1:{brokerPort}", "platform-under-test"), (status.BrokerId, status.Server, status.ClientId));

            // 配置接口列出全部服务器与占用数，但不泄露密码
            var controller = new IoTPlatform.Web.Controllers.ProtocolsController(hub, platform.GetRequiredService<IoTPlatform.Protocols.Codecs.PayloadCodecFactory>(),
                platform.GetRequiredService<DriverHostService>(), platform.GetRequiredService<Microsoft.Extensions.Options.IOptions<IoTOptions>>(),
                platform.GetRequiredService<MqttBrokerCatalog>());
            var json = JsonSerializer.Serialize(controller.MqttConfig());
            Assert.Contains("\"defaultBroker\":\"test-broker\"", json);
            Assert.Contains("unreachable", json);
            Assert.Contains("\"hasPassword\":true", json);
            Assert.DoesNotContain("p@ss-hidden", json);

            var products = platform.GetRequiredService<ProductService>();
            var product = products.Create(new ProductInput { Name = "External lamp", Code = "ext-lamp", Protocol = AccessProtocols.Mqtt });
            products.SaveProperty(product.Id, 0, new ThingPropertyInput { Identifier = "level", DataType = "int", AccessMode = "rw" });
            var registered = platform.GetRequiredService<DeviceService>().Create(new DeviceInput { ProductId = product.Id, Code = "ext-1" });

            await device.ConnectAsync();
            await device.SubscribeAsync("iot/ext-lamp/ext-1/property/set", msg =>
            {
                using var doc = JsonDocument.Parse(msg.Payload!.ToStr());
                var id = doc.RootElement.GetProperty("id").GetInt64();
                _ = device.PublishAsync(new PublishMessage
                {
                    Topic = "iot/ext-lamp/ext-1/service/reply",
                    Payload = new ArrayPacket(Encoding.UTF8.GetBytes($$"""{"id":{{id}},"status":2,"data":"applied"}""")),
                });
            });

            // 上线：status 主题
            await device.PublishAsync(new PublishMessage { Topic = "iot/ext-lamp/ext-1/status", Payload = new ArrayPacket(Encoding.UTF8.GetBytes("online")), QoS = QualityOfService.AtLeastOnce });
            Assert.True(await WaitFor(() => Device.FindById(registered.Id)!.Online), "status=online should mark the device online");

            // 上行属性
            await device.PublishAsync(new PublishMessage { Topic = "iot/ext-lamp/ext-1/property/post", Payload = new ArrayPacket(Encoding.UTF8.GetBytes("""{"level":3}""")), QoS = QualityOfService.AtLeastOnce });
            Assert.True(await WaitFor(() => DeviceProperty.FindByDeviceIdAndName(registered.Id, "level")?.Value == "3"), "property post should be ingested");

            // 未注册设备的消息被忽略
            await device.PublishAsync(new PublishMessage { Topic = "iot/ext-lamp/ghost/property/post", Payload = new ArrayPacket(Encoding.UTF8.GetBytes("""{"level":1}""")) });

            // 下行属性设置：经外部服务器送达设备，回复再经服务器回到平台
            var record = await platform.GetRequiredService<DeviceCommandService>()
                .SetPropertiesAsync(Device.FindById(registered.Id)!, new Dictionary<String, Object?> { ["level"] = 9 }, "test", waitSeconds: 5);
            Assert.Equal(ServiceStatusKinds.Success, record.Status);
            Assert.Equal("applied", record.OutputData);
            Assert.Equal("MQTT", record.Channel);

            // 外部模式无法踢下线
            Assert.False(hub.Disconnect(Device.FindById(registered.Id)!, "test"));

            // 下线：status 主题（遗嘱）
            await device.PublishAsync(new PublishMessage { Topic = "iot/ext-lamp/ext-1/status", Payload = new ArrayPacket(Encoding.UTF8.GetBytes("offline")), QoS = QualityOfService.AtLeastOnce });
            Assert.True(await WaitFor(() => !Device.FindById(registered.Id)!.Online), "status=offline should mark the device offline");
        }
        finally
        {
            try { await device.DisconnectAsync(); } catch { }
            await external.StopAsync(CancellationToken.None);
            await platform.GetRequiredService<MqttServerHost>().StopAsync(CancellationToken.None);
            broker.Stop("test");
        }
    }

    [Fact]
    public void BrokerAssignment_DeviceThenProductThenPlatform_AndRejectsUnknown()
    {
        var products = fixture.Get<ProductService>();
        var devices = fixture.Get<DeviceService>();
        var catalog = fixture.Get<MqttBrokerCatalog>();

        var product = products.Create(new ProductInput { Name = "Assign lamp", Code = "assign-lamp", Protocol = AccessProtocols.Mqtt, BrokerProfileId = MqttBrokerIds.Embedded });
        Assert.Equal(MqttBrokerIds.Embedded, product.BrokerProfileId);

        var inherit = devices.Create(new DeviceInput { ProductId = product.Id, Code = "assign-inherit" });
        Assert.True(String.IsNullOrEmpty(inherit.BrokerProfileId));
        Assert.Equal(MqttBrokerIds.Embedded, catalog.Resolve(inherit, product));

        var overrideDev = devices.Create(new DeviceInput { ProductId = product.Id, Code = "assign-override", BrokerProfileId = MqttBrokerIds.Embedded });
        Assert.Equal(MqttBrokerIds.Embedded, overrideDev.BrokerProfileId);

        var ex = Assert.Throws<PlatformException>(() => devices.Create(new DeviceInput { ProductId = product.Id, Code = "assign-bad", BrokerProfileId = "no-such-broker" }));
        Assert.Contains("no-such-broker", ex.Message);

        products.Update(product.Id, new ProductInput { Name = product.Name, Protocol = AccessProtocols.Mqtt, BrokerProfileId = null });
        Assert.Null(Product.FindById(product.Id)!.BrokerProfileId);
        Assert.Equal(catalog.PlatformDefaultId, catalog.Resolve(Device.FindById(inherit.Id)!, Product.FindById(product.Id)));

        devices.Delete(inherit.Id);
        devices.Delete(overrideDev.Id);
        products.Delete(product.Id);
    }

    [Fact]
    public async Task MultiBroker_DownlinkGoesToAssignedBroker_UplinkFromWrongBrokerIgnored()
    {
        var portA = FreePortForTest();
        var portB = FreePortForTest();
        var brokerA = new MqttServer { Port = portA, ServiceProvider = new ServiceCollection().BuildServiceProvider() };
        var brokerB = new MqttServer { Port = portB, ServiceProvider = new ServiceCollection().BuildServiceProvider() };
        brokerA.Start();
        brokerB.Start();

        var services = new ServiceCollection();
        services.Configure<IoTOptions>(o =>
        {
            o.Mqtt.Mode = MqttModes.External;
            o.Mqtt.Port = FreePortForTest();
            o.Mqtt.ActiveBroker = "alpha";
            o.Mqtt.Brokers.Add(new ExternalMqttOptions { Id = "alpha", Name = "Alpha", Server = $"tcp://127.0.0.1:{portA}", ClientId = "platform-alpha", ReconnectDelay = 1 });
            o.Mqtt.Brokers.Add(new ExternalMqttOptions { Id = "beta", Name = "Beta", Server = $"tcp://127.0.0.1:{portB}", ClientId = "platform-beta", ReconnectDelay = 1 });
        });
        services.AddIoTCore().AddIoTProtocols().AddIoTMqtt();
        await using var platform = services.BuildServiceProvider();

        var products = platform.GetRequiredService<ProductService>();
        var devices = platform.GetRequiredService<DeviceService>();
        var product = products.Create(new ProductInput { Name = "Twin lamp", Code = "twin-lamp", Protocol = AccessProtocols.Mqtt });
        products.SaveProperty(product.Id, 0, new ThingPropertyInput { Identifier = "level", DataType = "int", AccessMode = "rw" });
        var devA = devices.Create(new DeviceInput { ProductId = product.Id, Code = "twin-a", BrokerProfileId = "alpha" });
        var devB = devices.Create(new DeviceInput { ProductId = product.Id, Code = "twin-b", BrokerProfileId = "beta" });

        var catalog = platform.GetRequiredService<MqttBrokerCatalog>();
        Assert.Equal("alpha", catalog.Resolve(devA, product));
        Assert.Equal("beta", catalog.Resolve(devB, product));
        Assert.Contains("alpha", catalog.RequiredIds());
        Assert.Contains("beta", catalog.RequiredIds());

        var hub = platform.GetRequiredService<IMqttTransport>();
        await platform.GetRequiredService<MqttServerHost>().StartAsync(CancellationToken.None);
        await platform.GetRequiredService<ExternalMqttTransport>().StartAsync(CancellationToken.None);

        using var clientA = new MqttClient { Server = $"tcp://127.0.0.1:{portA}", ClientId = "twin-a" };
        using var clientB = new MqttClient { Server = $"tcp://127.0.0.1:{portB}", ClientId = "twin-b" };
        var gotA = new TaskCompletionSource<String>(TaskCreationOptions.RunContinuationsAsynchronously);
        var leakToB = 0;
        try
        {
            Assert.True(await WaitFor(() => hub.IsReadyFor("alpha") && hub.IsReadyFor("beta")), "both external brokers should connect");

            var cfg = JsonSerializer.Serialize(new IoTPlatform.Web.Controllers.ProtocolsController(hub,
                platform.GetRequiredService<IoTPlatform.Protocols.Codecs.PayloadCodecFactory>(),
                platform.GetRequiredService<DriverHostService>(),
                platform.GetRequiredService<Microsoft.Extensions.Options.IOptions<IoTOptions>>(),
                catalog).MqttConfig());
            Assert.Contains("\"Id\":\"alpha\"", cfg);
            Assert.Contains("\"Id\":\"beta\"", cfg);
            Assert.Contains("\"hasPassword\":false", cfg);

            await clientA.ConnectAsync();
            await clientB.ConnectAsync();
            await clientA.SubscribeAsync("iot/twin-lamp/twin-a/property/set", msg =>
            {
                gotA.TrySetResult(msg.Payload?.ToStr() ?? "");
            });
            await clientB.SubscribeAsync("iot/twin-lamp/twin-a/property/set", _ => Interlocked.Increment(ref leakToB));

            await clientA.PublishAsync(new PublishMessage { Topic = "iot/twin-lamp/twin-a/status", Payload = new ArrayPacket(Encoding.UTF8.GetBytes("online")), QoS = QualityOfService.AtLeastOnce });
            Assert.True(await WaitFor(() => Device.FindById(devA.Id)!.Online), "twin-a should come online on alpha");

            await clientA.PublishAsync(new PublishMessage { Topic = "iot/twin-lamp/twin-a/property/post", Payload = new ArrayPacket(Encoding.UTF8.GetBytes("""{"level":4}""")), QoS = QualityOfService.AtLeastOnce });
            Assert.True(await WaitFor(() => DeviceProperty.FindByDeviceIdAndName(devA.Id, "level")?.Value == "4"), "uplink on the assigned broker should be ingested");

            await clientB.PublishAsync(new PublishMessage { Topic = "iot/twin-lamp/twin-a/property/post", Payload = new ArrayPacket(Encoding.UTF8.GetBytes("""{"level":99}""")), QoS = QualityOfService.AtLeastOnce });
            await Task.Delay(400);
            Assert.Equal("4", DeviceProperty.FindByDeviceIdAndName(devA.Id, "level")?.Value);

            var record = await platform.GetRequiredService<DeviceCommandService>()
                .SetPropertiesAsync(Device.FindById(devA.Id)!, new Dictionary<String, Object?> { ["level"] = 7 }, "test", waitSeconds: 0);
            Assert.Equal(ServiceStatusKinds.Sent, record.Status);
            var payload = await gotA.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Contains("7", payload);
            await Task.Delay(300);
            Assert.Equal(0, leakToB);
        }
        finally
        {
            try { await clientA.DisconnectAsync(); } catch { }
            try { await clientB.DisconnectAsync(); } catch { }
            await platform.GetRequiredService<ExternalMqttTransport>().StopAsync(CancellationToken.None);
            await platform.GetRequiredService<MqttServerHost>().StopAsync(CancellationToken.None);
            brokerA.Stop("test");
            brokerB.Stop("test");
        }
    }

    [Fact]
    public void Product_property_cache_skips_reload_until_invalidated()
    {
        var products = fixture.Get<ProductService>();
        var product = products.Create(new ProductInput { Name = "Cache probe", Code = $"cache-{Guid.NewGuid():N}"[..16], Protocol = AccessProtocols.Mqtt });
        products.SaveProperty(product.Id, 0, new ThingPropertyInput { Identifier = "temperature", DataType = "float" });

        var first = ThingProperty.GetCachedByProductId(product.Id);
        Assert.Single(first);
        Assert.True(first.ContainsKey("temperature"));

        new ThingProperty { ProductId = product.Id, Identifier = "humidity", Name = "Humidity", DataType = "float" }.Insert();

        var cached = ThingProperty.GetCachedByProductId(product.Id);
        Assert.Single(cached);
        Assert.False(cached.ContainsKey("humidity"));

        ThingProperty.InvalidateCache(product.Id);
        var reloaded = ThingProperty.GetCachedByProductId(product.Id);
        Assert.Equal(2, reloaded.Count);
        Assert.True(reloaded.ContainsKey("humidity"));
    }

    private static Int32 FreePortForTest()
    {
        var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        var port = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return port;
    }
}
