using System.Collections.Concurrent;
using IoTPlatform.Data;
using IoTPlatform.ThingModel;
using NewLife;
using NewLife.Log;
using XCode;

namespace IoTPlatform.Services;

/// <summary>下行指令：服务调用与属性设置</summary>
/// <remarks>
/// 先落库 ServiceRecord，再按产品协议选择下行通道（MQTT / 驱动 / HTTP 轮询）。
/// 异步通道返回 Sent，设备回复后由 <see cref="OnReply"/> 更新状态并唤醒等待方。
/// </remarks>
public class DeviceCommandService(IEnumerable<IDeviceCommandChannel> channels, MessageCounter counter, CommandTracker tracker)
{
    public const String KindService = "service";
    public const String KindProperty = "property";

    public IReadOnlyList<IDeviceCommandChannel> Channels { get; } = channels.ToList();

    /// <summary>调用设备服务</summary>
    public async Task<ServiceRecord> InvokeServiceAsync(Device device, String service, IDictionary<String, Object?> args, String source, Int32 waitSeconds = 0, CancellationToken cancellationToken = default)
    {
        var def = ThingLookup.Service(device.ProductId, service);
        if (def == null || !def.Enable) throw new PlatformException("Service '{0}' is not defined in the product thing model", service);

        // 按物模型输入参数做类型与范围校验
        var parameters = ProductService.ParseParameters(def.InputData).ToDictionary(e => e.Identifier, StringComparer.OrdinalIgnoreCase);
        var normalized = new Dictionary<String, Object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in args.Where(e => e.Value is not String s || s.Length > 0))
        {
            if (!parameters.TryGetValue(key, out var parameter))
            {
                normalized[key] = JsonDefaults.Normalize(value);
                continue;
            }

            var check = TslValueChecker.Check(parameter, value);
            if (!check.Ok) throw new PlatformException("Property '{0}' value is invalid: {1}", key, check.Error);
            normalized[parameter.Identifier] = check.Value;
        }

        var wait = waitSeconds;
        if (wait == 0 && def.CallType.EqualIgnoreCase("sync")) wait = 8;
        return await SendAsync(device, KindService, ThingKey.Qualify(def.FunctionBlockId, def.Identifier), normalized, source, wait, cancellationToken);
    }

    /// <summary>设置设备属性（仅限读写属性）</summary>
    public async Task<ServiceRecord> SetPropertiesAsync(Device device, IDictionary<String, Object?> values, String source, Int32 waitSeconds = 0, CancellationToken cancellationToken = default)
    {
        var converted = new Dictionary<String, Object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in values)
        {
            var def = ThingLookup.Property(device.ProductId, key);
            if (def == null || !def.Enable) throw new PlatformException("Property '{0}' is not defined in the product thing model", key);
            if (!TslTypes.IsWritable(def.AccessMode)) throw new PlatformException("Property '{0}' is read-only", key);

            var check = TslValueChecker.Check(def, value);
            if (!check.Ok) throw new PlatformException("Property '{0}' value is invalid: {1}", key, check.Error);
            converted[ThingKey.Qualify(def.FunctionBlockId, def.Identifier)] = check.Value;
        }

        return await SendAsync(device, KindProperty, "set", converted, source, waitSeconds, cancellationToken);
    }

    private async Task<ServiceRecord> SendAsync(Device device, String kind, String name, IDictionary<String, Object?> args, String source, Int32 waitSeconds, CancellationToken cancellationToken)
    {
        if (!device.Enable) throw new PlatformException("Device is disabled");
        var product = device.Product ?? throw PlatformException.NotFound("Product");

        var now = DateTime.Now;
        var record = new ServiceRecord
        {
            Id = ServiceRecord.Meta.Factory.Snow.NewId(),
            DeviceId = device.Id,
            Kind = kind,
            Name = name,
            InputData = JsonDefaults.Serialize(args).Cut(2000),
            Status = ServiceStatusKinds.Pending,
            Source = source,
            CreateTime = now,
            UpdateTime = now,
        };
        record.Insert();

        var channel = Channels.FirstOrDefault(e => e.CanHandle(product));
        var waiter = waitSeconds > 0 ? tracker.Watch(record.Id) : null;

        try
        {
            CommandResult result;
            if (channel == null)
                result = CommandResult.Failed($"No downlink channel for protocol {product.Protocol}");
            else if (!device.Online && channel.Name != "HTTP")
                result = CommandResult.Failed("Device is offline");
            else
            {
                record.Channel = channel.Name;
                result = kind == KindService
                    ? await channel.InvokeServiceAsync(device, product, record, args, cancellationToken)
                    : await channel.SetPropertyAsync(device, product, record, args, cancellationToken);
            }

            // 设备可能在通道返回前就已回复
            var latest = ServiceRecord.FindById(record.Id);
            if (latest == null || latest.Status is ServiceStatusKinds.Pending or ServiceStatusKinds.Sent)
            {
                record.Status = result.Status;
                if (result.Output != null) record.OutputData = result.Output.Cut(2000);
                record.UpdateTime = DateTime.Now;
                record.Update();
            }
            else
            {
                record = latest;
            }

            if (record.Status != ServiceStatusKinds.Failed) counter.Downlink(device.ProductId);

            if (waiter != null && record.Status is ServiceStatusKinds.Sent or ServiceStatusKinds.Pending)
            {
                var done = await Task.WhenAny(waiter.Task, Task.Delay(TimeSpan.FromSeconds(waitSeconds), cancellationToken));
                if (done == waiter.Task) record = waiter.Task.Result;
            }

            DeviceHistory.Add(device.Id, kind == KindService ? DeviceActions.Service : DeviceActions.SetProperty, record.Status != ServiceStatusKinds.Failed,
                $"{name} {record.InputData} => {record.Status}{(record.OutputData.IsNullOrEmpty() ? "" : " " + record.OutputData)}");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            XTrace.WriteLine("下行指令失败 {0} {1} {2}", device.Code, name, ex.Message);
            record.Status = ServiceStatusKinds.Failed;
            record.OutputData = ex.Message.Cut(2000);
            record.UpdateTime = DateTime.Now;
            record.Update();
        }
        finally
        {
            if (waiter != null) tracker.Unwatch(record.Id);
        }

        return record;
    }

    /// <summary>处理设备回复</summary>
    public Boolean OnReply(Device device, Int64 id, ServiceStatusKinds status, String? data) => tracker.OnReply(device, id, status, data);

    /// <summary>HTTP 设备拉取待执行指令，拉取后标记为已下发</summary>
    public IList<ServiceRecord> FetchPending(Device device, Int32 max = 10)
    {
        var list = ServiceRecord.FindAll(ServiceRecord._.DeviceId == device.Id & ServiceRecord._.Status == ServiceStatusKinds.Pending, nameof(ServiceRecord.Id), null, 0, max);
        foreach (var item in list)
        {
            item.Status = ServiceStatusKinds.Sent;
            item.UpdateTime = DateTime.Now;
            item.Update();
        }

        return list;
    }

    /// <summary>把长时间未回复的指令标记为超时</summary>
    public Int32 ExpireTimeouts(TimeSpan timeout)
    {
        var deadline = DateTime.Now - timeout;
        var list = ServiceRecord.FindAll(ServiceRecord._.Status.In(new[] { (Int32)ServiceStatusKinds.Pending, (Int32)ServiceStatusKinds.Sent }) & ServiceRecord._.UpdateTime < deadline, null, null, 0, 1000);
        foreach (var item in list)
        {
            item.Status = ServiceStatusKinds.Timeout;
            item.UpdateTime = DateTime.Now;
            item.Update();
        }

        return list.Count;
    }
}

/// <summary>指令回复跟踪。独立于下行通道，供上行接入与下行指令共同使用</summary>
public class CommandTracker
{
    private readonly ConcurrentDictionary<Int64, TaskCompletionSource<ServiceRecord>> _waiters = new();

    public TaskCompletionSource<ServiceRecord> Watch(Int64 id) =>
        _waiters[id] = new TaskCompletionSource<ServiceRecord>(TaskCreationOptions.RunContinuationsAsynchronously);

    public void Unwatch(Int64 id) => _waiters.TryRemove(id, out _);

    /// <summary>更新指令状态并唤醒等待方</summary>
    public Boolean OnReply(Device device, Int64 id, ServiceStatusKinds status, String? data)
    {
        var record = ServiceRecord.FindById(id);
        if (record == null || record.DeviceId != device.Id) return false;

        record.Status = status;
        record.OutputData = data?.Cut(2000);
        record.UpdateTime = DateTime.Now;
        record.Update();

        if (_waiters.TryRemove(id, out var waiter)) waiter.TrySetResult(record);

        return true;
    }
}

/// <summary>HTTP 设备的下行通道：指令保持 Pending，由设备通过 HTTP 接口拉取</summary>
public class HttpPollingChannel : IDeviceCommandChannel
{
    public String Name => "HTTP";

    public Boolean CanHandle(Product product) => product.Protocol.EqualIgnoreCase(AccessProtocols.Http);

    public Task<CommandResult> InvokeServiceAsync(Device device, Product product, ServiceRecord record, IDictionary<String, Object?> args, CancellationToken cancellationToken) =>
        Task.FromResult(new CommandResult(ServiceStatusKinds.Pending, "Queued for device polling"));

    public Task<CommandResult> SetPropertyAsync(Device device, Product product, ServiceRecord record, IDictionary<String, Object?> values, CancellationToken cancellationToken) =>
        Task.FromResult(new CommandResult(ServiceStatusKinds.Pending, "Queued for device polling"));
}
