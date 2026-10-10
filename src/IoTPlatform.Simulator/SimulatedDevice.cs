using System.Text.Json;
using System.Text.Json.Nodes;

namespace IoTPlatform.Simulator;

/// <summary>模拟设备配置</summary>
public record DeviceProfile(String ProductKey, String DeviceCode, String Secret, String Kind);

/// <summary>模拟设备行为：生成遥测、事件，响应属性设置与服务调用。与传输方式（MQTT/HTTP）无关</summary>
public abstract class SimulatedDevice(DeviceProfile profile)
{
    public DeviceProfile Profile { get; } = profile;

    protected Random Random { get; } = new(profile.DeviceCode.GetHashCode());

    /// <summary>生成一帧属性</summary>
    public abstract Dictionary<String, Object?> NextTelemetry();

    /// <summary>生成需要上报的事件（可能为空）</summary>
    public virtual IEnumerable<Dictionary<String, Object?>> NextEvents() => [];

    /// <summary>处理属性设置，返回是否成功</summary>
    public abstract Boolean SetProperty(String name, JsonNode? value);

    /// <summary>处理服务调用，返回 (成功, 输出)</summary>
    public virtual (Boolean Success, Object? Output, Boolean Reboot) Invoke(String service, JsonObject? args) =>
        service switch
        {
            "reboot" => (true, new { message = "rebooting" }, true),
            _ => (false, new { message = $"unknown service {service}" }, false),
        };

    public static SimulatedDevice Create(DeviceProfile profile) => profile.Kind switch
    {
        "light" => new SmartLight(profile),
        _ => new EnvSensor(profile),
    };

    protected static Double Round(Double v, Int32 digits = 1) => Math.Round(v, digits);
}

/// <summary>温湿度传感器：temperature/humidity/battery，温度偶发尖峰触发 overheat 事件</summary>
public class EnvSensor(DeviceProfile profile) : SimulatedDevice(profile)
{
    private Double _temperature = 24;
    private Double _humidity = 55;
    private Double _battery = 100;
    private Double _offset;
    private Boolean _overheatSent;
    private Boolean _lowBatterySent;

    public override Dictionary<String, Object?> NextTelemetry()
    {
        // 随机游走 + 小概率尖峰，便于演示阈值规则
        _temperature += (Random.NextDouble() - 0.5) * 0.8 + (24 - _temperature) * 0.05;
        if (Random.NextDouble() < 0.04) _temperature = 35 + Random.NextDouble() * 4;
        _humidity = Math.Clamp(_humidity + (Random.NextDouble() - 0.5) * 2, 30, 90);
        _battery = Math.Max(5, _battery - Random.NextDouble() * 0.05);

        return new()
        {
            ["temperature"] = Round(_temperature + _offset),
            ["humidity"] = Round(_humidity),
            ["battery"] = (Int32)_battery,
        };
    }

    public override IEnumerable<Dictionary<String, Object?>> NextEvents()
    {
        if (_temperature > 35 && !_overheatSent)
        {
            _overheatSent = true;
            yield return new()
            {
                ["type"] = "alert",
                ["name"] = "overheat",
                ["remark"] = $"Temperature {Round(_temperature)}°C exceeds sensor limit",
                ["data"] = new Dictionary<String, Object?> { ["temperature"] = Round(_temperature) },
            };
        }
        else if (_temperature < 30)
        {
            _overheatSent = false;
        }

        if (_battery < 20 && !_lowBatterySent)
        {
            _lowBatterySent = true;
            yield return new() { ["type"] = "info", ["name"] = "lowBattery", ["remark"] = "Battery below 20%" };
        }
    }

    public override Boolean SetProperty(String name, JsonNode? value) => false;

    public override (Boolean Success, Object? Output, Boolean Reboot) Invoke(String service, JsonObject? args)
    {
        if (service == "calibrate")
        {
            _offset = args?["offset"]?.GetValue<Double>() ?? 0;
            return (true, new { offset = _offset }, false);
        }

        return base.Invoke(service, args);
    }
}

/// <summary>智能灯：switch/brightness/power，支持远程开关与调光</summary>
public class SmartLight(DeviceProfile profile) : SimulatedDevice(profile)
{
    private Boolean _on = true;
    private Int32 _brightness = 80;

    public override Dictionary<String, Object?> NextTelemetry() => new()
    {
        ["switch"] = _on,
        ["brightness"] = _brightness,
        ["power"] = _on ? Round(_brightness * 0.12 + Random.NextDouble() * 0.3, 2) : 0.0,
    };

    public override Boolean SetProperty(String name, JsonNode? value)
    {
        try
        {
            switch (name)
            {
                case "switch":
                    _on = value?.GetValueKind() switch
                    {
                        JsonValueKind.True => true,
                        JsonValueKind.False => false,
                        JsonValueKind.Number => value.GetValue<Double>() != 0,
                        _ => value?.ToString() is "true" or "1" or "on",
                    };
                    return true;
                case "brightness":
                    _brightness = Math.Clamp((Int32)(value?.GetValueKind() == JsonValueKind.Number ? value.GetValue<Double>() : Double.Parse(value?.ToString() ?? "0")), 0, 100);
                    return true;
                default:
                    return false;
            }
        }
        catch (Exception)
        {
            return false;
        }
    }

    public override (Boolean Success, Object? Output, Boolean Reboot) Invoke(String service, JsonObject? args)
    {
        if (service == "toggle")
        {
            _on = !_on;
            return (true, new { @switch = _on }, false);
        }

        return base.Invoke(service, args);
    }
}
