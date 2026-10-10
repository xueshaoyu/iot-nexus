using IoTPlatform.Data;
using IoTPlatform.Models;
using IoTPlatform.Rules;
using IoTPlatform.Services;
using NewLife.Log;

namespace IoTPlatform.Web.Infrastructure;

/// <summary>演示数据：5 个产品覆盖 MQTT / HTTP / Modbus TCP / 虚拟驱动，12 台设备、分组树与 5 条规则</summary>
public class DataSeeder(ProductService products, DeviceService devices, DeviceGroupService groups, RuleService rules)
{
    public const String EnvSensor = "env-sensor";
    public const String SmartLight = "smart-light";
    public const String PowerMeter = "power-meter";
    public const String Boiler = "boiler";
    public const String HttpGateway = "http-gateway";

    /// <summary>演示设备的密钥规则，模拟器使用同样规则连接</summary>
    public static String DemoSecret(String deviceCode) => $"{deviceCode}-secret";

    private Boolean _zh = true;

    /// <summary>演示数据语言由 IoT:Demo:Language 决定（默认 zh-CN）</summary>
    private String T(String en, String zh) => _zh ? zh : en;

    public Boolean Seed(IoTOptions options)
    {
        if (Product.FindCount() > 0) return false;

        _zh = options.Demo.Language?.StartsWith("zh", StringComparison.OrdinalIgnoreCase) ?? true;

        XTrace.WriteLine("写入演示数据……");

        // 分组
        var hq = groups.Create(new GroupInput { Name = T("Headquarters", "总部"), Sort = 1, Remark = T("Office campus", "办公园区") });
        var buildingA = groups.Create(new GroupInput { Name = T("Building A", "A 栋"), ParentId = hq.Id, Sort = 1 });
        var floor1 = groups.Create(new GroupInput { Name = T("Floor 1", "1 楼"), ParentId = buildingA.Id, Sort = 1 });
        var floor2 = groups.Create(new GroupInput { Name = T("Floor 2", "2 楼"), ParentId = buildingA.Id, Sort = 2 });
        var factory = groups.Create(new GroupInput { Name = T("Factory", "工厂"), Sort = 2, Remark = T("Production site", "生产基地") });
        var powerRoom = groups.Create(new GroupInput { Name = T("Power Room", "配电室"), ParentId = factory.Id, Sort = 1 });
        var boilerRoom = groups.Create(new GroupInput { Name = T("Boiler Room", "锅炉房"), ParentId = factory.Id, Sort = 2 });
        var field = groups.Create(new GroupInput { Name = T("Field Gateways", "野外网关"), Sort = 3, Remark = T("Remote sites reporting over HTTP", "通过 HTTP 上报的远程站点") });

        // 产品：温湿度传感器（MQTT）
        var env = products.Create(new ProductInput { Name = T("Environment Sensor", "温湿度传感器"), Code = EnvSensor, Category = T("Sensor", "传感器"), Protocol = AccessProtocols.Mqtt, Codec = "Json", OnlineTimeout = 60, Remark = T("Temperature / humidity sensor reporting over MQTT", "通过 MQTT 上报的温湿度传感器") });
        Prop(env, "temperature", T("Temperature", "温度"), "float", "°C", -20, 60, 0.1, unitName: T("Celsius", "摄氏度"), required: true);
        Prop(env, "humidity", T("Humidity", "湿度"), "float", "%RH", 0, 100, 0.1, unitName: T("Relative humidity", "相对湿度"), required: true);
        Prop(env, "battery", T("Battery", "电量"), "int", "%", 0, 100, 1, unitName: T("Percent", "百分比"), required: true);
        Prop(env, "location", T("Location", "位置"), "text", length: 64);
        Prop(env, "reportTime", T("Report time", "上报时间"), "date");
        Prop(env, "comfort", T("Comfort", "舒适度"), "enum", mapping: T("0=Cold,1=Comfortable,2=Hot", "0=寒冷,1=舒适,2=炎热"));
        Prop(env, "tags", T("Tags", "标签"), "array", length: 8, itemType: "text");
        Prop(env, "geo", T("Coordinate", "坐标"), "struct", specs: """[{"identifier":"longitude","name":"经度","dataType":{"type":"double","specs":{"min":"-180","max":"180","unit":"°","unitName":"度"}}},{"identifier":"latitude","name":"纬度","dataType":{"type":"double","specs":{"min":"-90","max":"90","unit":"°","unitName":"度"}}}]""");
        Prop(env, "health", T("Health", "健康度"), "int", "%", 0, 100, 1, block: "batteryPack", blockName: T("Battery pack", "电池包"));
        Prop(env, "cycles", T("Cycles", "循环次数"), "int", null, 0, 10000, 1, block: "batteryPack", blockName: T("Battery pack", "电池包"));
        products.SaveEvent(env.Id, 0, new ThingEventInput { Identifier = "overheat", Name = T("Overheat", "过热"), Type = "alert", Required = true, OutputData = [new() { Identifier = "temperature", Name = T("Temperature", "温度"), DataType = "float", Min = -20, Max = 80, Step = 0.1, Unit = "°C", UnitName = T("Celsius", "摄氏度") }] });
        products.SaveEvent(env.Id, 0, new ThingEventInput { Identifier = "lowBattery", Name = T("Low battery", "电量低"), Type = "info", OutputData = [new() { Identifier = "battery", Name = T("Battery", "电量"), DataType = "int", Min = 0, Max = 100, Unit = "%" }] });
        products.SaveEvent(env.Id, 0, new ThingEventInput { Identifier = "fault", Name = T("Fault", "故障"), Type = "error", OutputData = [new() { Identifier = "code", Name = T("Code", "错误码"), DataType = "int" }, new() { Identifier = "message", Name = T("Message", "描述"), DataType = "text", Length = 128 }] });
        products.SaveService(env.Id, 0, new ThingServiceInput { Identifier = "reboot", Name = T("Reboot", "重启"), CallType = "async", Remark = T("Device disconnects and reconnects after a few seconds", "设备断开并在几秒后重新连接") });
        products.SaveService(env.Id, 0, new ThingServiceInput { Identifier = "calibrate", Name = T("Calibrate", "校准"), CallType = "sync", InputData = [new() { Identifier = "offset", Name = T("Offset", "偏移量"), DataType = "float", Min = -10, Max = 10, Step = 0.1, Unit = "°C" }], OutputData = [new() { Identifier = "offset", Name = T("Offset", "偏移量"), DataType = "float", Unit = "°C" }] });
        products.SaveService(env.Id, 0, new ThingServiceInput { Identifier = "getStatus", Name = T("Get status", "读取状态"), CallType = "sync", OutputData = [new() { Identifier = "temperature", DataType = "float", Unit = "°C" }, new() { Identifier = "humidity", DataType = "float", Unit = "%RH" }] });

        // 产品：智能灯（MQTT，可读写属性）
        var light = products.Create(new ProductInput { Name = T("Smart Light", "智能灯"), Code = SmartLight, Category = T("Actuator", "执行器"), Protocol = AccessProtocols.Mqtt, Codec = "Json", OnlineTimeout = 60, BrokerProfileId = MqttBrokerIds.Embedded, Remark = T("Dimmable light with remote switch", "可远程开关与调光的灯") });
        Prop(light, "switch", T("Switch", "开关"), "bool", null, 0, 1, 1, "rw", mapping: T("0=Off,1=On", "0=关,1=开"), required: true);
        Prop(light, "brightness", T("Brightness", "亮度"), "int", "%", 0, 100, 1, "rw");
        Prop(light, "power", T("Power", "功率"), "float", "W", 0, 20, 0.01);
        products.SaveService(light.Id, 0, new ThingServiceInput { Identifier = "toggle", Name = T("Toggle", "切换"), OutputData = [new() { Identifier = "switch", DataType = "bool" }] });
        products.SaveService(light.Id, 0, new ThingServiceInput { Identifier = "reboot", Name = T("Reboot", "重启") });

        // 产品：电表（Modbus TCP 驱动采集）
        var meter = products.Create(new ProductInput
        {
            Name = T("Power Meter", "电力仪表"),
            Code = PowerMeter,
            Category = T("Meter", "仪表"),
            Protocol = AccessProtocols.Driver,
            DriverName = "ModbusTcp",
            DriverParameter = $"{{\"Server\":\"127.0.0.1:{options.Demo.ModbusPort}\",\"Host\":1,\"ReadCode\":3,\"WriteCode\":6,\"Timeout\":3000}}",
            PollInterval = 3000,
            OnlineTimeout = 30,
            Remark = T("Three-phase meter polled over Modbus TCP (holding registers 0-6)", "通过 Modbus TCP 采集的三相电表（保持寄存器 0-6）"),
        });
        Prop(meter, "voltage", T("Voltage", "电压"), "float", "V", 0, 300, 0.1, address: "4x0", pointType: "ushort", scaling: 0.1);
        Prop(meter, "current", T("Current", "电流"), "float", "A", 0, 100, 0.01, address: "4x1", pointType: "ushort", scaling: 0.01);
        Prop(meter, "power", T("Active power", "有功功率"), "float", "W", 0, 20000, 1, address: "4x2", pointType: "ushort");
        Prop(meter, "frequency", T("Frequency", "频率"), "float", "Hz", 45, 55, 0.01, address: "4x3", pointType: "ushort", scaling: 0.01);
        Prop(meter, "energy", T("Energy", "电能"), "float", "kWh", 0, 6553.5, 0.1, address: "4x4", pointType: "ushort", scaling: 0.1);
        Prop(meter, "temperature", T("Temperature", "温度"), "float", "°C", 0, 120, 0.1, address: "4x5", pointType: "ushort", scaling: 0.1);
        Prop(meter, "relay", T("Relay", "继电器"), "int", null, 0, 1, 1, "rw", address: "4x6", pointType: "ushort", mapping: T("0=Open,1=Closed", "0=断开,1=闭合"));

        // 产品：锅炉（虚拟驱动）
        var boiler = products.Create(new ProductInput
        {
            Name = T("Industrial Boiler", "工业锅炉"),
            Code = Boiler,
            Category = T("Equipment", "设备"),
            Protocol = AccessProtocols.Driver,
            DriverName = "Simulator",
            DriverParameter = "{\"Period\":90,\"Noise\":0.02}",
            PollInterval = 4000,
            OnlineTimeout = 30,
            Remark = T("Virtual device generated by the Simulator driver", "由 Simulator 驱动生成数据的虚拟设备"),
        });
        Prop(boiler, "waterTemp", T("Water temperature", "水温"), "float", "°C", 60, 96, 0.1);
        Prop(boiler, "pressure", T("Pressure", "压力"), "float", "MPa", 0.2, 1.6, 0.01);
        Prop(boiler, "flowRate", T("Flow rate", "流量"), "float", "m³/h", 5, 25, 0.1);
        Prop(boiler, "burner", T("Burner", "燃烧器"), "bool", null, 0, 1, 1, "rw", mapping: T("0=Off,1=On", "0=关,1=开"));
        Prop(boiler, "targetTemp", T("Target temperature", "目标温度"), "int", "°C", 60, 95, 1, "rw");
        products.SaveService(boiler.Id, 0, new ThingServiceInput { Identifier = "reset", Name = T("Reset overrides", "重置覆盖值"), Remark = T("Clears written values so the simulator resumes generating data", "清除写入的值，模拟器恢复生成数据") });

        // 产品：HTTP 网关
        var gateway = products.Create(new ProductInput { Name = T("HTTP Gateway", "HTTP 网关"), Code = HttpGateway, Category = T("Gateway", "网关"), Protocol = AccessProtocols.Http, Codec = "Json", OnlineTimeout = 60, Remark = T("Field gateway reporting via REST (POST /api/device-access/{code}/properties)", "通过 REST 上报的野外网关（POST /api/device-access/{code}/properties）") });
        Prop(gateway, "temperature", T("Temperature", "温度"), "float", "°C", -20, 60, 0.1);
        Prop(gateway, "humidity", T("Humidity", "湿度"), "float", "%RH", 0, 100, 0.1);
        Prop(gateway, "battery", T("Battery", "电量"), "int", "%", 0, 100, 1);
        products.SaveService(gateway.Id, 0, new ThingServiceInput { Identifier = "reboot", Name = T("Reboot", "重启") });

        // 设备
        Dev(env, "th-001", T("Lobby sensor", "大堂传感器"), floor1, T("Building A · Floor 1 · Lobby", "A 栋 · 1 楼 · 大堂"));
        Dev(env, "th-002", T("Meeting room sensor", "会议室传感器"), floor1, T("Building A · Floor 1 · Room 101", "A 栋 · 1 楼 · 101 会议室"));
        Dev(env, "th-003", T("Server room sensor", "机房传感器"), floor2, T("Building A · Floor 2 · Server room", "A 栋 · 2 楼 · 机房"));
        Dev(env, "th-004", T("Open office sensor", "开放办公区传感器"), floor2, T("Building A · Floor 2 · Open office", "A 栋 · 2 楼 · 开放办公区"));
        Dev(env, "th-005", T("Warehouse sensor (spare)", "仓库传感器（备用）"), hq, T("Warehouse — not connected", "仓库——未接入"));
        Dev(light, "light-001", T("Lobby light", "大堂灯"), floor1, T("Building A · Floor 1 · Lobby", "A 栋 · 1 楼 · 大堂"));
        Dev(light, "light-002", T("Office light east", "办公区东侧灯"), floor2, T("Building A · Floor 2", "A 栋 · 2 楼"));
        Dev(light, "light-003", T("Office light west", "办公区西侧灯"), floor2, T("Building A · Floor 2", "A 栋 · 2 楼"));
        Dev(meter, "meter-001", T("Main incomer meter", "总进线电表"), powerRoom, T("Factory · Power room · Panel 1", "工厂 · 配电室 · 1 号柜"));
        Dev(boiler, "boiler-001", T("Boiler #1", "1 号锅炉"), boilerRoom, T("Factory · Boiler room", "工厂 · 锅炉房"));
        Dev(boiler, "boiler-002", T("Boiler #2", "2 号锅炉"), boilerRoom, T("Factory · Boiler room", "工厂 · 锅炉房"));
        Dev(gateway, "http-gw-001", T("Field gateway North", "北区野外网关"), field, T("Remote site North", "北区远程站点"));

        // 规则
        rules.Create(new RuleInput
        {
            Name = T("High temperature alarm", "高温告警"),
            TriggerType = RuleTriggers.Property,
            ProductId = env.Id,
            Conditions = [new() { Property = "temperature", Operator = ">", Value = "32" }],
            Actions = [new() { Type = "alarm", Parameters = new() { ["level"] = "Warning", ["title"] = T("High temperature: {device.name}", "高温：{device.name}"), ["content"] = T("Temperature {temperature}°C exceeded 32°C at {device.name}", "{device.name} 温度 {temperature}°C 超过 32°C") } }],
            SilenceSeconds = 60,
            Remark = T("Threshold rule on a thing-model property", "基于物模型属性的阈值规则"),
        });
        rules.Create(new RuleInput
        {
            Name = T("Overheat event → critical alarm", "过热事件 → 严重告警"),
            TriggerType = RuleTriggers.Event,
            EventName = "overheat",
            Actions =
            [
                new() { Type = "alarm", Parameters = new() { ["level"] = "Critical", ["title"] = T("Overheat reported by {device.name}", "{device.name} 上报过热"), ["content"] = "{event.remark}" } },
                new() { Type = "mqtt-publish", Parameters = new() { ["topic"] = "alerts/{device.code}", ["qos"] = "0" } },
            ],
            SilenceSeconds = 30,
            Remark = T("Event trigger with alarm + MQTT notification", "事件触发：告警 + MQTT 通知"),
        });
        rules.Create(new RuleInput
        {
            Name = T("Boiler over-temperature → shut burner", "锅炉超温 → 关闭燃烧器"),
            TriggerType = RuleTriggers.Property,
            ProductId = boiler.Id,
            Conditions = [new() { Property = "waterTemp", Operator = ">", Value = "93" }, new() { Property = "burner", Operator = "==", Value = "true" }],
            Actions =
            [
                new() { Type = "set-property", Parameters = new() { ["property"] = "burner", ["value"] = "false" } },
                new() { Type = "alarm", Parameters = new() { ["level"] = "Critical", ["title"] = T("Boiler over-temperature: {device.name}", "锅炉超温：{device.name}"), ["content"] = T("Water {waterTemp}°C > 93°C, burner switched off automatically", "水温 {waterTemp}°C > 93°C，已自动关闭燃烧器") } },
            ],
            SilenceSeconds = 120,
            Remark = T("Device linkage: write a property back through the driver", "设备联动：通过驱动回写属性"),
        });
        rules.Create(new RuleInput
        {
            Name = T("Device offline notice", "设备离线通知"),
            TriggerType = RuleTriggers.Offline,
            Actions =
            [
                new() { Type = "alarm", Parameters = new() { ["level"] = "Info", ["title"] = T("Offline: {device.name}", "离线：{device.name}"), ["content"] = T("{device.code} went offline ({event.remark})", "{device.code} 已离线（{event.remark}）") } },
                new() { Type = "log", Parameters = new() { ["message"] = T("{device.code} offline", "{device.code} 离线") } },
            ],
            SilenceSeconds = 300,
            Remark = T("Online/offline trigger for all products", "适用于全部产品的上下线触发"),
        });
        rules.Create(new RuleInput
        {
            Name = T("Meter high load", "电表高负载"),
            TriggerType = RuleTriggers.Property,
            ProductId = meter.Id,
            GroupId = factory.Id,
            Conditions = [new() { Property = "power", Operator = ">", Value = "1250" }],
            Actions = [new() { Type = "mqtt-publish", Parameters = new() { ["topic"] = "alerts/load/{device.code}", ["payload"] = "{\"device\":\"{device.code}\",\"power\":{power}}", ["qos"] = "0" } }],
            SilenceSeconds = 60,
            Remark = T("Group-scoped rule publishing to MQTT", "按分组生效、发布到 MQTT 的规则"),
        });

        XTrace.WriteLine("演示数据写入完成");
        return true;
    }

    private void Prop(Product product, String id, String name, String type, String? unit = null, Double min = 0, Double max = 0, Double step = 0, String access = "r",
        String? address = null, String? pointType = null, Double scaling = 0, String? mapping = null, String? unitName = null, Boolean required = false,
        Int32 length = 0, String? itemType = null, String? specs = null, String? block = null, String? blockName = null)
    {
        products.SaveProperty(product.Id, 0, new ThingPropertyInput
        {
            Identifier = id,
            Name = name,
            DataType = type,
            Unit = unit,
            UnitName = unitName,
            Min = min,
            Max = max,
            Step = step,
            Length = length,
            AccessMode = access,
            Required = required,
            Address = address,
            PointType = pointType,
            Scaling = scaling,
            Mapping = mapping,
            ItemType = itemType,
            Specs = specs,
            FunctionBlockId = block,
            FunctionBlockName = blockName,
            Sort = ThingProperty.FindAllByProductId(product.Id).Count,
        });
    }

    private void Dev(Product product, String code, String name, DeviceGroup group, String location) =>
        devices.Create(new DeviceInput { ProductId = product.Id, GroupId = group.Id, Code = code, Name = name, Secret = DemoSecret(code), Location = location, Enable = true });
}
