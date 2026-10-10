using System.Collections.Concurrent;
using IoTPlatform.Data;
using IoTPlatform.Models;
using IoTPlatform.ThingModel;
using NewLife;
using NewLife.Log;
using XCode;

namespace IoTPlatform.Services;

/// <summary>批量控制：把范围（产品/分组/设备列表）展开为具体设备，再逐台复用 <see cref="DeviceCommandService"/> 下发</summary>
/// <remarks>
/// 跳过规则：设备或产品禁用、产品物模型中没有该属性/服务、属性只读、按 <see cref="BatchOnlineMode"/> 判定的离线设备。
/// 每台设备的指令都会生成独立的 ServiceRecord（Source=Batch:{任务编号}），设备回复后与单设备控制一样更新状态。
/// </remarks>
public class BatchControlService(DeviceCommandService commands)
{
    public const String KindProperty = DeviceCommandService.KindProperty;
    public const String KindService = DeviceCommandService.KindService;

    /// <summary>并发下发的设备数</summary>
    public Int32 Parallelism { get; set; } = 8;

    /// <summary>展开范围。传入动作时同时检查设备是否具备对应能力</summary>
    public IList<BatchTarget> Resolve(BatchScope scope, BatchControlInput? action = null)
    {
        IEnumerable<Device> devices;
        switch (scope.Type)
        {
            case BatchScopeType.Product:
                if (Product.FindById(scope.ProductId) == null) throw PlatformException.NotFound("Product");
                devices = Device.FindAllByProductId(scope.ProductId);
                break;

            case BatchScopeType.Group:
                if (DeviceGroup.FindById(scope.GroupId) == null) throw PlatformException.NotFound("Group");
                var ids = scope.IncludeSubGroups ? DeviceGroupService.GetDescendantIds(scope.GroupId) : [];
                ids.Add(scope.GroupId);
                devices = Device.FindAll(Device._.GroupId.In(ids), null, null, 0, 0);
                break;

            default:
                if (scope.DeviceIds.Length == 0) throw new PlatformException("Select at least one device");
                devices = scope.DeviceIds.Distinct().Select(Device.FindById).Where(e => e != null).Cast<Device>();
                break;
        }

        return devices.OrderBy(e => e.Id).Select(d =>
        {
            var product = d.Product;
            return new BatchTarget(d.Id, d.Code, d.Name, d.ProductId, product?.Name, product?.Protocol, d.Online, GetSkipReason(d, product, scope.OnlineMode, action));
        }).ToList();
    }

    /// <summary>执行批量控制</summary>
    public async Task<BatchControlResult> ExecuteAsync(BatchControlInput input, String source, IProgress<BatchProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var (kind, name) = Validate(input);
        var targets = Resolve(input.Scope, input);
        if (targets.Count == 0) throw new PlatformException("No devices matched the scope");

        var now = DateTime.Now;
        var job = new BatchJob
        {
            Id = BatchJob.Meta.Factory.Snow.NewId(),
            Kind = kind,
            Name = name,
            ScopeType = input.Scope.Type.ToString(),
            ScopeId = input.Scope.Type switch { BatchScopeType.Product => input.Scope.ProductId, BatchScopeType.Group => input.Scope.GroupId, _ => 0 },
            Scope = JsonDefaults.Serialize(input.Scope).Cut(2000),
            InputData = JsonDefaults.Serialize(kind == KindProperty ? JsonDefaults.Normalize(input.Value) : input.Args.ToDictionary(e => e.Key, e => JsonDefaults.Normalize(e.Value))).Cut(2000),
            Total = targets.Count,
            Source = source,
            CreateTime = now,
            UpdateTime = now,
        };
        job.Insert();

        var results = new ConcurrentDictionary<Int32, BatchDeviceResult>();
        var done = 0;
        foreach (var t in targets.Where(e => e.SkipReason != null))
        {
            results[t.DeviceId] = new BatchDeviceResult { DeviceId = t.DeviceId, DeviceCode = t.Code, DeviceName = t.Name, Status = BatchItemStatus.Skipped, Message = t.SkipReason };
            done++;
        }
        progress?.Report(new BatchProgress(done, targets.Count));

        using var gate = new SemaphoreSlim(Math.Max(1, Parallelism));
        var tasks = targets.Where(e => e.SkipReason == null).Select(async t =>
        {
            await gate.WaitAsync(cancellationToken);
            try
            {
                results[t.DeviceId] = await SendAsync(t, input, kind, name, $"Batch:{job.Id}", cancellationToken);
            }
            finally
            {
                gate.Release();
                progress?.Report(new BatchProgress(Interlocked.Increment(ref done), targets.Count));
            }
        });
        await Task.WhenAll(tasks);

        var list = targets.Select(t => results[t.DeviceId]).ToList();
        var result = new BatchControlResult
        {
            JobId = job.Id,
            Kind = kind,
            Name = name,
            Total = list.Count,
            Succeeded = list.Count(e => e.Status == BatchItemStatus.Success),
            Pending = list.Count(e => e.Status is BatchItemStatus.Sent or BatchItemStatus.Pending),
            Failed = list.Count(e => e.Status is BatchItemStatus.Failed or BatchItemStatus.Timeout),
            Skipped = list.Count(e => e.Status == BatchItemStatus.Skipped),
            Completed = true,
            StartTime = now,
            EndTime = DateTime.Now,
            Results = list,
        };

        job.Succeeded = result.Succeeded;
        job.Pending = result.Pending;
        job.Failed = result.Failed;
        job.Skipped = result.Skipped;
        job.Completed = true;
        job.Results = JsonDefaults.Serialize(list);
        job.UpdateTime = result.EndTime;
        job.Update();

        XTrace.WriteLine("批量控制 #{0} {1} {2}：共 {3}，成功 {4}，待回复 {5}，失败 {6}，跳过 {7}",
            job.Id, kind, name, result.Total, result.Succeeded, result.Pending, result.Failed, result.Skipped);

        return result;
    }

    /// <summary>最近的批量任务</summary>
    public IList<BatchJob> GetRecent(Int32 count = 20) => BatchJob.FindAll(null, "Id desc", null, 0, Math.Clamp(count, 1, 200));

    /// <summary>读取批量任务结果。待回复的设备按指令记录的最新状态刷新</summary>
    public BatchControlResult Get(Int64 jobId)
    {
        var job = BatchJob.FindById(jobId) ?? throw PlatformException.NotFound("Batch job");
        var list = JsonDefaults.Deserialize<List<BatchDeviceResult>>(job.Results) ?? [];
        foreach (var item in list.Where(e => e.RecordId > 0 && e.Status is BatchItemStatus.Sent or BatchItemStatus.Pending))
        {
            var record = ServiceRecord.FindById(item.RecordId);
            if (record == null) continue;

            item.Status = Map(record.Status);
            item.Message = record.OutputData;
        }

        return new BatchControlResult
        {
            JobId = job.Id,
            Kind = job.Kind ?? "",
            Name = job.Name ?? "",
            Total = job.Total,
            Succeeded = list.Count(e => e.Status == BatchItemStatus.Success),
            Pending = list.Count(e => e.Status is BatchItemStatus.Sent or BatchItemStatus.Pending),
            Failed = list.Count(e => e.Status is BatchItemStatus.Failed or BatchItemStatus.Timeout),
            Skipped = list.Count(e => e.Status == BatchItemStatus.Skipped),
            Completed = job.Completed,
            StartTime = job.CreateTime,
            EndTime = job.UpdateTime,
            Results = list,
        };
    }

    private async Task<BatchDeviceResult> SendAsync(BatchTarget target, BatchControlInput input, String kind, String name, String source, CancellationToken cancellationToken)
    {
        var item = new BatchDeviceResult { DeviceId = target.DeviceId, DeviceCode = target.Code, DeviceName = target.Name };
        var device = Device.FindById(target.DeviceId);
        if (device == null)
        {
            item.Status = BatchItemStatus.Skipped;
            item.Message = "Device not found";
            return item;
        }

        try
        {
            var record = kind == KindProperty
                ? await commands.SetPropertiesAsync(device, new Dictionary<String, Object?> { [name] = input.Value }, source, input.WaitSeconds, cancellationToken)
                : await commands.InvokeServiceAsync(device, name, input.Args, source, input.WaitSeconds, cancellationToken);

            item.RecordId = record.Id;
            item.Status = Map(record.Status);
            item.Message = record.OutputData;
        }
        catch (PlatformException ex)
        {
            item.Status = BatchItemStatus.Failed;
            item.Message = ex.Message;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            item.Status = BatchItemStatus.Failed;
            item.Message = ex.Message;
        }

        return item;
    }

    private static (String Kind, String Name) Validate(BatchControlInput input)
    {
        if (input.Kind.EqualIgnoreCase(KindProperty))
        {
            if (input.Property.IsNullOrWhiteSpace()) throw new PlatformException("Property is required");
            return (KindProperty, input.Property.Trim());
        }
        if (input.Kind.EqualIgnoreCase(KindService))
        {
            if (input.Service.IsNullOrWhiteSpace()) throw new PlatformException("Service is required");
            return (KindService, input.Service.Trim());
        }

        throw new PlatformException("Kind must be 'property' or 'service'");
    }

    private static String? GetSkipReason(Device device, Product? product, BatchOnlineMode mode, BatchControlInput? action)
    {
        if (!device.Enable) return "Device is disabled";
        if (product == null || !product.Enable) return "Product is disabled";

        if (action != null)
        {
            if (action.Kind.EqualIgnoreCase(KindProperty) && !action.Property.IsNullOrWhiteSpace())
            {
                var def = ThingLookup.Property(device.ProductId, action.Property.Trim());
                if (def == null || !def.Enable) return "Property is not defined for this product";
                if (!TslTypes.IsWritable(def.AccessMode)) return "Property is read-only";
            }
            else if (action.Kind.EqualIgnoreCase(KindService) && !action.Service.IsNullOrWhiteSpace())
            {
                var def = ThingLookup.Service(device.ProductId, action.Service.Trim());
                if (def == null || !def.Enable) return "Service is not defined for this product";
            }
        }

        if (!device.Online)
        {
            if (mode == BatchOnlineMode.OnlineOnly) return "Device is offline";
            if (mode == BatchOnlineMode.Auto && product.Protocol != AccessProtocols.Http) return "Device is offline";
        }

        return null;
    }

    private static BatchItemStatus Map(ServiceStatusKinds status) => status switch
    {
        ServiceStatusKinds.Success => BatchItemStatus.Success,
        ServiceStatusKinds.Sent => BatchItemStatus.Sent,
        ServiceStatusKinds.Pending => BatchItemStatus.Pending,
        ServiceStatusKinds.Timeout => BatchItemStatus.Timeout,
        _ => BatchItemStatus.Failed,
    };
}
