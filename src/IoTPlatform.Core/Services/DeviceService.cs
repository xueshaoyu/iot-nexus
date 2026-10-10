using IoTPlatform.Data;
using IoTPlatform.Models;
using NewLife;
using NewLife.Data;
using NewLife.Security;
using XCode;

namespace IoTPlatform.Services;

/// <summary>设备影子条目：物模型定义 + 最新值</summary>
public record DeviceShadowItem(String Identifier, String Name, String? DataType, String? Unit, String AccessMode, String? Value, DateTime? UpdateTime, Boolean Defined);

/// <summary>设备管理</summary>
public class DeviceService(ProductService productService, DeviceOnlineService onlineService, IEnumerable<IDeviceSessionManager> sessionManagers, MqttBrokerCatalog brokers)
{
    #region 查询
    public PagedResult<Device> Query(DeviceQuery q)
    {
        var exp = new WhereExpression();
        if (q.ProductId > 0) exp &= Device._.ProductId == q.ProductId;
        if (q.GroupId > 0)
        {
            var ids = q.IncludeSubGroups ? DeviceGroupService.GetDescendantIds(q.GroupId) : [];
            ids.Add(q.GroupId);
            exp &= Device._.GroupId.In(ids);
        }
        else if (q.GroupId < 0)
        {
            exp &= Device._.GroupId == 0;
        }
        if (q.Online != null) exp &= Device._.Online == q.Online.Value;
        if (q.Enable != null) exp &= Device._.Enable == q.Enable.Value;
        if (!q.Keyword.IsNullOrWhiteSpace())
        {
            var k = q.Keyword.Trim();
            exp &= Device._.Code.Contains(k) | Device._.Name.Contains(k) | Device._.IP.Contains(k) | Device._.Location.Contains(k);
        }

        var page = new PageParameter { PageIndex = Math.Max(1, q.PageIndex), PageSize = Math.Clamp(q.PageSize, 1, 500), Sort = nameof(Device.Id), Desc = false };
        var list = Device.FindAll(exp, page);

        return new PagedResult<Device>(list, page.TotalCount, page.PageIndex, page.PageSize);
    }

    public Device Get(Int32 id) => Device.FindById(id) ?? throw PlatformException.NotFound("Device");

    public Device GetByCode(String code) => Device.FindByCode(code) ?? throw PlatformException.NotFound("Device");

    /// <summary>设备影子：按物模型列出全部属性及最新值，并附带物模型之外的上报属性</summary>
    public IList<DeviceShadowItem> GetShadow(Int32 deviceId)
    {
        var device = Get(deviceId);
        var values = DeviceProperty.FindAllByDeviceId(device.Id).ToDictionary(e => e.Name, StringComparer.OrdinalIgnoreCase);
        var result = new List<DeviceShadowItem>();

        foreach (var p in productService.GetProperties(device.ProductId).Where(e => e.Enable))
        {
            var key = ThingKey.Qualify(p.FunctionBlockId, p.Identifier);
            values.Remove(key, out var v);
            result.Add(new DeviceShadowItem(key, p.Name ?? key, p.DataType, p.Unit, p.AccessMode ?? "r", v?.Value, v?.UpdateTime, true));
        }
        foreach (var v in values.Values.OrderBy(e => e.Name))
        {
            result.Add(new DeviceShadowItem(v.Name, v.NickName ?? v.Name, v.DataType, v.Unit, "r", v.Value, v.UpdateTime, false));
        }

        return result;
    }

    public PagedResult<DeviceData> GetData(Int32 deviceId, String? name, DateTime? start, DateTime? end, Int32 pageIndex = 1, Int32 pageSize = 50)
    {
        var exp = DeviceData._.DeviceId == deviceId;
        if (!name.IsNullOrEmpty()) exp &= DeviceData._.Name == name;
        exp &= DeviceData._.Id.Between(start ?? DateTime.MinValue, end ?? DateTime.MinValue, DeviceData.Meta.Factory.Snow);

        return Page<DeviceData>(exp, pageIndex, pageSize);
    }

    public PagedResult<DeviceEvent> GetEvents(Int32 deviceId, Int32 pageIndex = 1, Int32 pageSize = 20) =>
        Page<DeviceEvent>(DeviceEvent._.DeviceId == deviceId, pageIndex, pageSize);

    public PagedResult<DeviceHistory> GetHistory(Int32 deviceId, Int32 pageIndex = 1, Int32 pageSize = 20) =>
        Page<DeviceHistory>(DeviceHistory._.DeviceId == deviceId, pageIndex, pageSize);

    public PagedResult<ServiceRecord> GetServiceRecords(Int32 deviceId, Int32 pageIndex = 1, Int32 pageSize = 20) =>
        Page<ServiceRecord>(ServiceRecord._.DeviceId == deviceId, pageIndex, pageSize);

    private static PagedResult<T> Page<T>(Expression exp, Int32 pageIndex, Int32 pageSize) where T : Entity<T>, new()
    {
        var page = new PageParameter { PageIndex = Math.Max(1, pageIndex), PageSize = Math.Clamp(pageSize, 1, 1000), Sort = "Id", Desc = true };
        var list = Entity<T>.FindAll(exp, page);

        return new PagedResult<T>(list, page.TotalCount, page.PageIndex, page.PageSize);
    }
    #endregion

    #region 注册与维护
    public Device Create(DeviceInput input)
    {
        var product = productService.Get(input.ProductId);
        if (input.GroupId > 0 && DeviceGroup.FindById(input.GroupId) == null) throw PlatformException.NotFound("Group");

        var code = input.Code.IsNullOrWhiteSpace() ? $"{product.Code}-{Rand.NextString(6, false).ToLowerInvariant()}" : input.Code.Trim();
        if (Device.FindByCode(code) != null) throw new PlatformException(409, "Device code '{0}' already exists", code);
        ValidateParameter(input.DriverParameter);

        var device = new Device
        {
            Code = code,
            Secret = input.Secret.IsNullOrWhiteSpace() ? NewSecret() : input.Secret.Trim(),
        };
        Apply(device, input);
        device.Insert();

        DeviceHistory.Add(device.Id, DeviceActions.Register, true, $"Registered under product {product.Name}");
        productService.RefreshDeviceCount(product.Id);

        return device;
    }

    /// <summary>批量注册，编码为 {prefix}-{序号}</summary>
    public IList<Device> CreateBatch(DeviceBatchInput input)
    {
        var product = productService.Get(input.ProductId);
        var list = new List<Device>();
        var n = 1;
        while (list.Count < input.Count && n < input.Count + 10_000)
        {
            var code = $"{input.Prefix}-{n++:000}";
            if (Device.FindByCode(code) != null) continue;

            list.Add(Create(new DeviceInput { ProductId = product.Id, GroupId = input.GroupId, Code = code, Name = code, Enable = true }));
        }

        return list;
    }

    public Device Update(Int32 id, DeviceInput input)
    {
        var device = Get(id);
        var oldProduct = device.ProductId;
        productService.Get(input.ProductId);
        if (input.GroupId > 0 && DeviceGroup.FindById(input.GroupId) == null) throw PlatformException.NotFound("Group");
        ValidateParameter(input.DriverParameter);

        if (!input.Code.IsNullOrWhiteSpace() && !input.Code.EqualIgnoreCase(device.Code))
        {
            if (Device.FindByCode(input.Code) != null) throw new PlatformException(409, "Device code '{0}' already exists", input.Code);
            device.Code = input.Code.Trim();
        }
        if (!input.Secret.IsNullOrWhiteSpace()) device.Secret = input.Secret.Trim();

        var wasEnabled = device.Enable;
        Apply(device, input);
        device.Update();

        if (oldProduct != device.ProductId)
        {
            productService.RefreshDeviceCount(oldProduct);
            productService.RefreshDeviceCount(device.ProductId);
        }
        if (wasEnabled && !device.Enable) Kick(device, "Device disabled");

        return device;
    }

    public void Delete(Int32 id)
    {
        var device = Get(id);
        Kick(device, "Device deleted");

        using (var tran = Device.Meta.CreateTrans())
        {
            DeviceProperty.FindAllByDeviceId(id).Delete();
            DeviceOnline.FindByDeviceId(id)?.Delete();
            device.Delete();
            tran.Commit();
        }

        productService.RefreshDeviceCount(device.ProductId);
    }

    public Device SetEnable(Int32 id, Boolean enable)
    {
        var device = Get(id);
        if (device.Enable == enable) return device;

        device.Enable = enable;
        device.Update();
        DeviceHistory.Add(device.Id, enable ? DeviceActions.Enable : DeviceActions.Disable, true, null);

        if (!enable) Kick(device, "Device disabled");

        return device;
    }

    public Device ResetSecret(Int32 id)
    {
        var device = Get(id);
        device.Secret = NewSecret();
        device.Update();
        Kick(device, "Secret reset");

        return device;
    }

    /// <summary>设备认证。编码与密钥匹配，且设备与产品均启用</summary>
    public static Device? Authenticate(String? code, String? secret, out String? error)
    {
        error = null;
        var device = code.IsNullOrEmpty() ? null : Device.FindByCode(code);
        if (device == null) { error = "unknown device"; return null; }
        if (device.Secret.IsNullOrEmpty() || device.Secret != secret) { error = "bad secret"; return null; }
        if (!device.Enable) { error = "device disabled"; return null; }

        var product = device.Product;
        if (product == null || !product.Enable) { error = "product disabled"; return null; }

        return device;
    }

    private void Kick(Device device, String reason)
    {
        foreach (var m in sessionManagers) m.Disconnect(device, reason);
        onlineService.SetOffline(device, reason);
    }

    private void Apply(Device device, DeviceInput input)
    {
        device.Name = input.Name.IsNullOrWhiteSpace() ? device.Code : input.Name.Trim();
        device.ProductId = input.ProductId;
        device.GroupId = input.GroupId;
        device.Enable = input.Enable;
        device.Location = input.Location;
        device.DriverParameter = input.DriverParameter.IsNullOrWhiteSpace() ? null : input.DriverParameter;
        device.PollInterval = input.PollInterval;
        device.BrokerProfileId = brokers.Canonicalize(input.BrokerProfileId);
        brokers.EnsureKnown(device.BrokerProfileId);
        device.Remark = input.Remark;
    }

    private static void ValidateParameter(String? json)
    {
        if (!json.IsNullOrWhiteSpace() && JsonDefaults.ParseObject(json) == null)
            throw new PlatformException("DriverParameter must be a JSON object");
    }

    private static String NewSecret() => Rand.NextString(16, false);
    #endregion
}
