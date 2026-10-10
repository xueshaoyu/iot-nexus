using System.Collections.Concurrent;
using IoTPlatform.Data;
using IoTPlatform.Rules;
using IoTPlatform.ThingModel;
using NewLife;
using XCode;

namespace IoTPlatform.Services;

/// <summary>上行数据接入。MQTT、HTTP、驱动采集的数据都汇聚到这里</summary>
/// <remarks>
/// 职责：按物模型转换类型 → 更新设备影子 → 写历史数据（XCode 异步队列批量落库）
/// → 刷新在线心跳 → 计数 → 投递规则引擎。
/// </remarks>
public class DataIngestService(DeviceOnlineService onlineService, RuleEngine ruleEngine, MessageCounter counter, CommandTracker commandTracker)
{
    private readonly ConcurrentDictionary<Int32, ConcurrentDictionary<String, DeviceProperty>> _shadows = new();

    /// <summary>上报属性</summary>
    /// <param name="device">设备</param>
    /// <param name="values">属性名值</param>
    /// <param name="timestamp">设备端时间戳（Unix毫秒），0表示使用平台时间</param>
    /// <param name="protocol">接入协议</param>
    /// <param name="sessionId">会话</param>
    /// <param name="ip">来源地址</param>
    /// <returns>接受的属性数</returns>
    public Int32 PostProperties(Device device, IDictionary<String, Object?> values, Int64 timestamp, String protocol, String? sessionId = null, String? ip = null, ICollection<String>? errors = null)
    {
        if (!device.Enable || values.Count == 0) return 0;

        var product = device.Product;
        var definitions = ThingProperty.GetCachedByProductId(device.ProductId);
        var shadow = GetShadow(device.Id);
        var now = DateTime.Now;
        var ts = timestamp > 0 ? timestamp : new DateTimeOffset(now).ToUnixTimeMilliseconds();

        var current = new Dictionary<String, Object?>(StringComparer.OrdinalIgnoreCase);
        var previous = new Dictionary<String, Object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, raw) in values)
        {
            if (key.IsNullOrWhiteSpace()) continue;

            definitions.TryGetValue(key, out var def);
            if (def is { Enable: false }) def = null;
            var check = def == null
                ? new TslValueChecker.Result(true, ThingValueConverter.Convert(null, raw), null)
                : TslValueChecker.Check(def, raw);
            if (!check.Ok)
            {
                errors?.Add($"{key}: {check.Error}");
                continue;
            }

            var name = def == null ? key.Trim() : ThingKey.Qualify(def.FunctionBlockId, def.Identifier);
            var value = check.Value;
            var text = JsonDefaults.ToText(value)?.Cut(2000);
            TslValueChecker.Flatten(current, name, value);

            if (shadow.TryGetValue(name, out var prop))
            {
                previous[name] = prop.Value;
                prop.Value = text;
                prop.UpdateTime = now;
                prop.SaveAsync(SqliteSettings.SaveAsyncDelayMs);
            }
            else
            {
                prop = new DeviceProperty
                {
                    DeviceId = device.Id,
                    Name = name,
                    NickName = def?.Name ?? name,
                    DataType = def?.DataType,
                    Unit = def?.Unit,
                    Value = text,
                    CreateTime = now,
                    UpdateTime = now,
                };
                prop.Insert();
                shadow[name] = prop;
            }

            new DeviceData
            {
                Id = DeviceData.Meta.Factory.Snow.NewId(),
                DeviceId = device.Id,
                Name = name,
                Value = text,
                Timestamp = ts,
                CreateTime = now,
            }.SaveAsync(SqliteSettings.SaveAsyncDelayMs);
        }

        if (current.Count == 0) return 0;

        device.Messages++;
        onlineService.Touch(device, protocol, sessionId, null, ip, 1);
        counter.Uplink(device.ProductId);

        ruleEngine.Post(new RuleContext
        {
            Device = device,
            Product = product,
            TriggerType = RuleTriggers.Property,
            Values = current,
            Previous = previous,
            Shadow = shadow.ToDictionary(e => e.Key, e => (Object?)e.Value.Value, StringComparer.OrdinalIgnoreCase),
            Time = now,
        });

        return current.Count;
    }

    /// <summary>上报事件</summary>
    public Boolean PostEvent(Device device, String? type, String name, String? remark, IDictionary<String, Object?>? data, Int64 timestamp, String protocol, String? sessionId = null, String? ip = null, Boolean requireDefinition = false, ICollection<String>? errors = null)
    {
        if (!device.Enable || name.IsNullOrWhiteSpace()) return false;

        var def = ThingLookup.Event(device.ProductId, name);
        if (requireDefinition && (def == null || !def.Enable))
        {
            errors?.Add($"{name}: not defined");
            return false;
        }

        var now = DateTime.Now;
        var values = data?.ToDictionary(e => e.Key, e => JsonDefaults.Normalize(e.Value), StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<String, Object?>(StringComparer.OrdinalIgnoreCase);
        if (def is { Enable: true })
        {
            foreach (var parameter in ProductService.ParseParameters(def.OutputData))
            {
                if (!values.TryGetValue(parameter.Identifier, out var raw)) continue;

                var check = TslValueChecker.Check(parameter, raw);
                if (!check.Ok)
                {
                    errors?.Add($"{parameter.Identifier}: {check.Error}");
                    values.Remove(parameter.Identifier);
                }
                else values[parameter.Identifier] = check.Value;
            }
        }

        var flat = new Dictionary<String, Object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in values) TslValueChecker.Flatten(flat, key, value);
        values = flat;

        new DeviceEvent
        {
            Id = DeviceEvent.Meta.Factory.Snow.NewId(),
            DeviceId = device.Id,
            Type = type.IsNullOrEmpty() ? def?.Type ?? "info" : type,
            Name = name,
            Remark = remark?.Cut(500),
            Data = values.Count > 0 ? JsonDefaults.Serialize(values).Cut(2000) : null,
            EventTime = timestamp > 0 ? DateTimeOffset.FromUnixTimeMilliseconds(timestamp).LocalDateTime : now,
            CreateTime = now,
        }.SaveAsync(SqliteSettings.SaveAsyncDelayMs);

        device.Messages++;
        onlineService.Touch(device, protocol, sessionId, null, ip, 1);
        counter.Uplink(device.ProductId);

        ruleEngine.Post(new RuleContext
        {
            Device = device,
            Product = device.Product,
            TriggerType = RuleTriggers.Event,
            Values = values,
            Shadow = GetShadow(device.Id).ToDictionary(e => e.Key, e => (Object?)e.Value.Value, StringComparer.OrdinalIgnoreCase),
            EventName = def == null ? name : ThingKey.Qualify(def.FunctionBlockId, def.Identifier),
            EventType = type ?? def?.Type,
            Remark = remark,
            Time = now,
        });

        return true;
    }

    /// <summary>设备回复服务调用/属性设置</summary>
    public Boolean PostReply(Device device, Int64 id, Boolean success, String? data, String protocol, String? sessionId = null)
    {
        onlineService.Touch(device, protocol, sessionId);
        counter.Uplink(device.ProductId);

        return commandTracker.OnReply(device, id, success ? ServiceStatusKinds.Success : ServiceStatusKinds.Failed, data);
    }

    /// <summary>心跳</summary>
    public void Heartbeat(Device device, String protocol, String? sessionId = null, String? ip = null) => onlineService.Touch(device, protocol, sessionId, null, ip);

    /// <summary>设备被删除或属性定义变化后清理影子缓存</summary>
    public void Evict(Int32 deviceId) => _shadows.TryRemove(deviceId, out _);

    private ConcurrentDictionary<String, DeviceProperty> GetShadow(Int32 deviceId) =>
        _shadows.GetOrAdd(deviceId, id => new ConcurrentDictionary<String, DeviceProperty>(
            DeviceProperty.FindAllByDeviceId(id).ToDictionary(e => e.Name, StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase));
}
