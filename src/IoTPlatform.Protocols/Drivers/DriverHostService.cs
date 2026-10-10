using System.Collections.Concurrent;
using IoTPlatform.Data;
using IoTPlatform.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NewLife;
using NewLife.IoT;
using NewLife.IoT.Drivers;
using NewLife.IoT.ThingModels;
using NewLife.IoT.ThingSpecification;
using NewLife.Log;

namespace IoTPlatform.Protocols.Drivers;

/// <summary>驱动采集运行状态</summary>
public record DriverWorkerStatus(Int32 DeviceId, String DeviceCode, String Driver, Int32 Interval, Int32 Points, DateTime LastRead, Int32 Failures, String? LastError);

/// <summary>驱动主机。为 Driver 协议的设备创建 NewLife.IoT 驱动实例并定时采集，同时作为驱动设备的下行通道</summary>
/// <remarks>
/// 每 10 秒对账一次：新增/启用的设备启动采集，禁用/删除/配置变更的设备停止或重启采集。
/// 采集结果按物模型缩放后交给 <see cref="DataIngestService"/>，与 MQTT 上报走同一条处理链路。
/// 连续 3 次采集失败判定离线。
/// </remarks>
public class DriverHostService(DataIngestService ingestService, DeviceOnlineService onlineService, IOptions<IoTOptions> options) : BackgroundService, IDeviceCommandChannel
{
    private sealed class Worker
    {
        public required Int32 DeviceId { get; init; }
        public required String DeviceCode { get; init; }
        public required String DriverName { get; init; }
        public required String Hash { get; init; }
        public required Int32 Interval { get; init; }
        public required IDriver Driver { get; init; }
        public required INode Node { get; set; }
        public required IPoint[] Points { get; init; }
        public required IDictionary<String, ThingProperty> Properties { get; init; }
        public CancellationTokenSource Cts { get; } = new();
        public SemaphoreSlim Trigger { get; } = new(0);
        public Task? Loop { get; set; }
        public DateTime LastRead { get; set; }
        public Int32 Failures { get; set; }
        public String? LastError { get; set; }
    }

    private const Int32 OfflineAfterFailures = 3;
    private readonly ConcurrentDictionary<Int32, Worker> _workers = new();

    public String Name => "Driver";

    public IReadOnlyList<DriverWorkerStatus> GetStatus() => _workers.Values
        .Select(w => new DriverWorkerStatus(w.DeviceId, w.DeviceCode, w.DriverName, w.Interval, w.Points.Length, w.LastRead, w.Failures, w.LastError))
        .OrderBy(e => e.DeviceId).ToList();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        DriverRegistry.EnsureRegistered();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ReconcileAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                XTrace.WriteException(ex);
            }

            try { await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }

        foreach (var id in _workers.Keys.ToList()) await StopWorkerAsync(id, "Platform stopping");
    }

    /// <summary>对账：启动、重启、停止采集任务</summary>
    public async Task ReconcileAsync(CancellationToken cancellationToken)
    {
        var desired = new Dictionary<Int32, (Device Device, Product Product, IList<ThingProperty> Props, String Hash)>();
        foreach (var product in Product.FindAll().Where(e => e.Enable && e.Protocol == AccessProtocols.Driver && !e.DriverName.IsNullOrEmpty()))
        {
            if (!DriverFactory.Map.ContainsKey(product.DriverName!)) continue;

            var props = ThingProperty.GetCachedByProductId(product.Id).Values.Where(e => e.Enable).OrderBy(e => e.Sort).ThenBy(e => e.Id).ToList();
            var propHash = String.Join(",", props.Select(e => $"{e.Id}:{e.UpdateTime.Ticks}"));
            foreach (var device in Device.FindAllByProductId(product.Id).Where(e => e.Enable))
            {
                desired[device.Id] = (device, product, props, $"{product.UpdateTime.Ticks}|{device.DriverParameter}|{device.PollInterval}|{propHash}");
            }
        }

        foreach (var (id, worker) in _workers)
        {
            if (!desired.TryGetValue(id, out var d) || d.Hash != worker.Hash)
                await StopWorkerAsync(id, desired.ContainsKey(id) ? "Configuration changed" : "Device disabled or removed");
        }

        foreach (var (id, d) in desired)
        {
            if (_workers.ContainsKey(id)) continue;

            try
            {
                await StartWorkerAsync(d.Device, d.Product, d.Props, d.Hash, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                XTrace.WriteLine("驱动启动失败 {0} [{1}] {2}", d.Device.Code, d.Product.DriverName, ex.Message);
            }
        }
    }

    private async Task StartWorkerAsync(Device device, Product product, IList<ThingProperty> props, String hash, CancellationToken cancellationToken)
    {
        var driverName = product.DriverName!;
        var parameterJson = MergeParameters(product.DriverParameter, device.DriverParameter);

        var probe = DriverFactory.CreateParameter(driverName, parameterJson!);
        var identifier = probe is IDriverParameterKey key ? key.GetKey() : null;
        var driver = DriverFactory.Create(driverName, identifier!) ?? throw new InvalidOperationException($"Driver '{driverName}' not registered");
        var parameter = driver.CreateParameter(parameterJson);

        var points = props.Select(p => (IPoint)new PointModel
        {
            Name = p.Identifier,
            Address = p.Address.IsNullOrEmpty() ? p.Identifier : p.Address,
            Type = p.PointType.IsNullOrEmpty() ? p.DataType : p.PointType,
            Length = p.Length > 0 ? p.Length : TypeHelper.GetLength(p.PointType.IsNullOrEmpty() ? p.DataType : p.PointType),
        }).ToArray();

        // 交给驱动的物模型使用原始点位类型且不缩放，缩放由平台按 ThingProperty.Scaling 统一处理
        var spec = new ThingSpec
        {
            Profile = new Profile { ProductKey = product.Code },
            Properties = props.Select(p =>
            {
                var ps = PropertySpec.Create(p.Identifier, p.Name ?? p.Identifier, p.PointType.IsNullOrEmpty() ? p.DataType! : p.PointType!, p.Length, p.Address);
                var specs = ps.DataType!.Specs ??= new DataSpecs();
                specs.Min = p.Min;
                specs.Max = p.Max;
                specs.Step = p.Step;
                return ps;
            }).ToArray(),
        };

        var logical = new PlatformDevice { Code = device.Code, Specification = spec, Points = points };
        var node = await driver.OpenAsync(logical, parameter, cancellationToken);

        var interval = device.PollInterval > 0 ? device.PollInterval : product.PollInterval > 0 ? product.PollInterval : options.Value.DefaultPollInterval;
        var worker = new Worker
        {
            DeviceId = device.Id,
            DeviceCode = device.Code,
            DriverName = driverName,
            Hash = hash,
            Interval = Math.Max(500, interval),
            Driver = driver,
            Node = node,
            Points = points,
            Properties = props.ToDictionary(e => e.Identifier, StringComparer.OrdinalIgnoreCase),
        };
        _workers[device.Id] = worker;
        worker.Loop = Task.Run(() => PollLoopAsync(worker), CancellationToken.None);

        XTrace.WriteLine("驱动采集启动 {0} [{1}] 点位 {2} 间隔 {3}ms", device.Code, driverName, points.Length, worker.Interval);
    }

    private async Task StopWorkerAsync(Int32 deviceId, String reason)
    {
        if (!_workers.TryRemove(deviceId, out var worker)) return;

        worker.Cts.Cancel();
        try { if (worker.Loop != null) await worker.Loop; } catch { }
        try { await worker.Driver.CloseAsync(worker.Node); } catch (Exception ex) { XTrace.WriteLine("关闭驱动节点失败 {0}", ex.Message); }

        var device = Device.FindById(deviceId);
        if (device != null && device.Online) onlineService.SetOffline(device, reason);

        XTrace.WriteLine("驱动采集停止 {0} {1}", worker.DeviceCode, reason);
    }

    private async Task PollLoopAsync(Worker worker)
    {
        var token = worker.Cts.Token;
        while (!token.IsCancellationRequested)
        {
            await PollOnceAsync(worker, token);

            try { await worker.Trigger.WaitAsync(worker.Interval, token); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task PollOnceAsync(Worker worker, CancellationToken token)
    {
        var device = Device.FindById(worker.DeviceId);
        if (device == null) return;

        try
        {
            var result = await worker.Driver.ReadAsync(worker.Node, worker.Points, token);
            if (!result.IsSuccess) throw new InvalidOperationException(result.Message ?? result.Code.ToString());

            var values = new Dictionary<String, Object?>(StringComparer.OrdinalIgnoreCase);
            var points = result.Points ?? [];
            var raws = result.Values ?? [];
            for (var i = 0; i < points.Length && i < raws.Length; i++)
            {
                var name = points[i].Name;
                if (name.IsNullOrEmpty() || raws[i] is Byte[]) continue;

                values[name] = worker.Properties.TryGetValue(name, out var def) ? ThingValueConverter.Scale(def, raws[i]) : raws[i];
            }
            if (values.Count == 0) throw new InvalidOperationException("No point values returned");

            worker.Failures = 0;
            worker.LastError = null;
            worker.LastRead = DateTime.Now;
            ingestService.PostProperties(device, values, 0, $"{AccessProtocols.Driver}:{worker.DriverName}", $"driver:{worker.DriverName}");
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            worker.Failures++;
            worker.LastError = ex.Message;
            if (worker.Failures == OfflineAfterFailures)
            {
                XTrace.WriteLine("驱动采集连续失败 {0} [{1}] {2}", worker.DeviceCode, worker.DriverName, ex.Message);
                if (device.Online) onlineService.SetOffline(device, $"Driver read failed: {ex.Message}");
            }
        }
    }

    #region 下行通道
    public Boolean CanHandle(Product product) => product.Protocol == AccessProtocols.Driver;

    public async Task<CommandResult> SetPropertyAsync(Device device, Product product, ServiceRecord record, IDictionary<String, Object?> values, CancellationToken cancellationToken)
    {
        if (!_workers.TryGetValue(device.Id, out var worker)) return CommandResult.Failed("Driver is not running for this device");

        var requests = new List<WriteRequest>();
        foreach (var (name, value) in values)
        {
            var point = worker.Points.FirstOrDefault(e => name.EqualIgnoreCase(e.Name));
            if (point == null) return CommandResult.Failed($"Point '{name}' not found");

            var raw = value;
            if (worker.Properties.TryGetValue(name, out var def) && def.Scaling != 0 && def.Scaling != 1 && ThingValueConverter.TryDouble(value, out var d))
                raw = d / def.Scaling;
            requests.Add(WriteRequest.From(point, raw));
        }

        var result = await worker.Driver.WriteAsync(worker.Node, requests.ToArray(), cancellationToken);
        if (!result.IsSuccess) return CommandResult.Failed(result.Message ?? result.Code.ToString());

        worker.Trigger.Release();
        return CommandResult.Success($"written {requests.Count} point(s)");
    }

    public async Task<CommandResult> InvokeServiceAsync(Device device, Product product, ServiceRecord record, IDictionary<String, Object?> args, CancellationToken cancellationToken)
    {
        if (!_workers.TryGetValue(device.Id, out var worker)) return CommandResult.Failed("Driver is not running for this device");

        try
        {
            var result = await worker.Driver.ControlAsync(worker.Node, ControlRequest.Create(record.Name!, args!), cancellationToken);
            worker.Trigger.Release();

            return result.IsSuccess
                ? CommandResult.Success(result.OutputParameters == null ? null : JsonDefaults.Serialize(result.OutputParameters))
                : CommandResult.Failed(result.Message ?? result.Code.ToString());
        }
        catch (NotSupportedException)
        {
            return CommandResult.Failed($"Driver {worker.DriverName} does not support services");
        }
        catch (NotImplementedException)
        {
            return CommandResult.Failed($"Driver {worker.DriverName} does not support services");
        }
    }
    #endregion

    /// <summary>合并产品默认参数与设备参数，设备参数优先</summary>
    public static String? MergeParameters(String? productJson, String? deviceJson)
    {
        var p = JsonDefaults.ParseObject(productJson);
        var d = JsonDefaults.ParseObject(deviceJson);
        if (p == null) return d == null ? null : JsonDefaults.Serialize(d);
        if (d != null) foreach (var (k, v) in d) p[k] = v;

        return JsonDefaults.Serialize(p);
    }
}
