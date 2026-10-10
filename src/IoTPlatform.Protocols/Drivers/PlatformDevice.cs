using NewLife.IoT;
using NewLife.IoT.Models;
using NewLife.IoT.ThingModels;
using NewLife.IoT.ThingSpecification;

namespace IoTPlatform.Protocols.Drivers;

/// <summary>交给 NewLife.IoT 驱动的逻辑设备。驱动只读取编码、物模型与点位，数据上报由驱动主机统一处理</summary>
public class PlatformDevice : IDevice
{
    public String Code { get; set; } = "";

    public IDictionary<String, Object?> Properties { get; set; } = new Dictionary<String, Object?>();

    public ThingSpec? Specification { get; set; }

    public IPoint[]? Points { get; set; }

    public IDictionary<String, Delegate> Services { get; } = new Dictionary<String, Delegate>();

    public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<IDeviceInfo[]> SetOnlineAsync(IDeviceInfo[] devices, CancellationToken cancellationToken = default) => Task.FromResult(devices);

    public Task<IDeviceInfo[]> SetOfflineAsync(String[] devices, CancellationToken cancellationToken = default) => Task.FromResult(Array.Empty<IDeviceInfo>());

    public Task<Int32> PostPropertyAsync(String deviceCode, Object items, CancellationToken cancellationToken = default) => Task.FromResult(0);

    public Task<Int32> PostDataAsync(String deviceCode, DataModel[] items, CancellationToken cancellationToken = default) => Task.FromResult(0);

    public Task<Int32> PostEventAsync(String deviceCode, EventModel[] items, CancellationToken cancellationToken = default) => Task.FromResult(0);

    public void PostProperty() { }

    public void SetProperty(String name, Object? value) => Properties[name] = value;

    public Boolean AddData(String name, String? value) => false;

    public Boolean WriteEvent(String type, String name, String? remark) => false;

    public void RegisterService(String service, Delegate method) => Services[service] = method;
}
