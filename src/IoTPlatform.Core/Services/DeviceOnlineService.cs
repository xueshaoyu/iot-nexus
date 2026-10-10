using System.Collections.Concurrent;
using IoTPlatform.Data;
using IoTPlatform.Rules;
using Microsoft.Extensions.Options;
using NewLife;
using NewLife.Log;
using XCode;

namespace IoTPlatform.Services;

/// <summary>设备在线状态维护</summary>
/// <remarks>
/// 三种来源统一在这里处理：
/// MQTT 连接/断开/PINGREQ，HTTP 上报与心跳，驱动采集成功/失败。
/// 所有来源都会刷新 LastActive，后台定时扫描超时设备置为离线。
/// </remarks>
public class DeviceOnlineService(IOptions<IoTOptions> options, RuleEngine ruleEngine)
{
    private static readonly TimeSpan SaveInterval = TimeSpan.FromSeconds(15);
    private readonly Object[] _locks = Enumerable.Range(0, 64).Select(_ => new Object()).ToArray();
    private readonly ConcurrentDictionary<Int32, DateTime> _lastSaved = new();

    /// <summary>设备上线，或刷新已有会话信息</summary>
    public void SetOnline(Device device, String protocol, String? sessionId, String? clientId = null, String? ip = null)
    {
        var now = DateTime.Now;
        Boolean changed;
        lock (GetLock(device.Id))
        {
            var online = DeviceOnline.FindByDeviceId(device.Id) ?? new DeviceOnline { DeviceId = device.Id, CreateTime = now };
            online.ProductId = device.ProductId;
            online.SessionId = sessionId;
            online.Protocol = protocol;
            online.ClientId = clientId;
            if (!ip.IsNullOrEmpty()) online.IP = ip;
            online.UpdateTime = now;
            online.Save();

            changed = !device.Online;
            device.LastActive = now;
            if (!ip.IsNullOrEmpty()) device.IP = ip;
            if (changed)
            {
                device.Online = true;
                device.LastOnline = now;
                device.Logins++;
            }
            device.Update();
            _lastSaved[device.Id] = now;
        }

        if (changed)
        {
            DeviceHistory.Add(device.Id, DeviceActions.Online, true, $"{protocol} {ip} {clientId}".Trim());
            XTrace.WriteLine("设备上线 {0} [{1}] {2}", device.Code, protocol, ip);
            ruleEngine.Post(new RuleContext { Device = device, Product = device.Product, TriggerType = RuleTriggers.Online, Remark = protocol });
        }
    }

    /// <summary>心跳/数据活跃。离线设备会自动上线，在线设备节流落库</summary>
    public void Touch(Device device, String protocol, String? sessionId, String? clientId = null, String? ip = null, Int32 messages = 0)
    {
        if (!device.Online)
        {
            SetOnline(device, protocol, sessionId, clientId, ip);
            return;
        }

        var now = DateTime.Now;
        device.LastActive = now;
        if (_lastSaved.TryGetValue(device.Id, out var last) && now - last < SaveInterval) return;

        lock (GetLock(device.Id))
        {
            // Merge Messages++ / LastActive in the XCode entity queue instead of a
            // synchronous UPDATE on every heartbeat window.
            device.SaveAsync(SqliteSettings.DeviceCounterDelayMs);
            var online = DeviceOnline.FindByDeviceId(device.Id);
            if (online != null)
            {
                online.UpdateTime = now;
                online.Messages += messages;
                online.SaveAsync(SqliteSettings.DeviceCounterDelayMs);
            }
            _lastSaved[device.Id] = now;
        }
    }

    /// <summary>设备下线。指定 sessionId 时只有当前会话匹配才下线，避免重连时旧会话断开把新会话踢下线</summary>
    public Boolean SetOffline(Device device, String reason, String? sessionId = null)
    {
        var now = DateTime.Now;
        lock (GetLock(device.Id))
        {
            var online = DeviceOnline.FindByDeviceId(device.Id);
            if (sessionId != null && online != null && online.SessionId != sessionId) return false;

            online?.Delete();
            if (!device.Online) return false;

            device.Online = false;
            device.LastOffline = now;
            if (device.LastOnline.Year > 2000) device.OnlineTime += (Int32)Math.Max(0, (now - device.LastOnline).TotalSeconds);
            device.Update();
            _lastSaved.TryRemove(device.Id, out _);
        }

        DeviceHistory.Add(device.Id, DeviceActions.Offline, true, reason);
        XTrace.WriteLine("设备下线 {0} {1}", device.Code, reason);
        ruleEngine.Post(new RuleContext { Device = device, Product = device.Product, TriggerType = RuleTriggers.Offline, Remark = reason });

        return true;
    }

    /// <summary>扫描心跳超时的设备并置为离线，返回处理数量</summary>
    public Int32 CheckTimeout()
    {
        var now = DateTime.Now;
        var count = 0;
        foreach (var device in Device.FindAll(Device._.Online == true, null, null, 0, 0))
        {
            var product = device.Product;
            var timeout = product?.OnlineTimeout > 0 ? product.OnlineTimeout : options.Value.OfflineTimeout;
            var active = device.LastActive > device.LastOnline ? device.LastActive : device.LastOnline;
            if (active.AddSeconds(timeout) < now && SetOffline(device, $"Heartbeat timeout ({timeout}s)")) count++;
        }

        return count;
    }

    /// <summary>进程启动时清理残留的在线状态</summary>
    public void Reset()
    {
        DeviceOnline.FindAll().Delete();
        foreach (var device in Device.FindAll(Device._.Online == true, null, null, 0, 0))
        {
            device.Online = false;
            device.LastOffline = DateTime.Now;
            device.Update();
        }
    }

    private Object GetLock(Int32 deviceId) => _locks[(deviceId & 0x7FFFFFFF) % _locks.Length];
}
