using IoTPlatform.Data;
using NewLife;
using NewLife.Log;
using XCode;

namespace IoTPlatform.Services;

/// <summary>平台概览</summary>
public record StatOverview(
    Int32 Products,
    Int32 Devices,
    Int32 EnabledDevices,
    Int32 OnlineDevices,
    Double OnlineRate,
    Int32 ActiveToday,
    Int32 NewToday,
    Int64 MessagesToday,
    Int64 MessagesLastMinute,
    Int32 DownlinksToday,
    Int32 ActiveAlarms,
    Int32 AlarmsToday,
    Int32 Groups,
    Int32 EnabledRules,
    DateTime Time);

/// <summary>按产品统计</summary>
public record ProductStat(Int32 ProductId, String ProductName, String? Protocol, Int32 Devices, Int32 Online, Double OnlineRate, Int64 MessagesToday);

/// <summary>趋势点</summary>
public record TrendPoint(DateTime Time, Int32 Total, Int32 Online, Int32 Actives, Int64 Messages, Int32 Downlinks, Int32 Alarms);

/// <summary>设备统计：实时概览、按产品分布、小时/天趋势</summary>
public class StatService(MessageCounter counter)
{
    public StatOverview GetOverview()
    {
        var devices = Device.FindAll();
        var today = DateTime.Today;
        var total = devices.Count;
        var online = devices.Count(e => e.Online);
        var day = FindStat(StatLevels.Day, today, 0);

        return new StatOverview(
            Products: (Int32)Product.FindCount(),
            Devices: total,
            EnabledDevices: devices.Count(e => e.Enable),
            OnlineDevices: online,
            OnlineRate: total == 0 ? 0 : Math.Round(online * 100.0 / total, 1),
            ActiveToday: devices.Count(e => e.LastActive >= today),
            NewToday: devices.Count(e => e.CreateTime >= today),
            MessagesToday: (day?.Messages ?? 0) + counter.PendingUplink(),
            MessagesLastMinute: counter.UplinkLastMinute(),
            DownlinksToday: day?.Downlinks ?? 0,
            ActiveAlarms: (Int32)Alarm.FindCount(Alarm._.Status == AlarmStatus.Active),
            AlarmsToday: (Int32)Alarm.FindCount(Alarm._.Id >= Alarm.Meta.Factory.Snow.GetId(today)),
            Groups: (Int32)DeviceGroup.FindCount(),
            EnabledRules: Rule.FindAllWithCache().Count(e => e.Enable),
            Time: DateTime.Now);
    }

    public IList<ProductStat> GetProductStats()
    {
        var devices = Device.FindAll();
        var today = DateTime.Today;
        var stats = DeviceStat.FindAll(DeviceStat._.Level == StatLevels.Day & DeviceStat._.StatTime == today & DeviceStat._.ProductId > 0).ToDictionary(e => e.ProductId);

        return Product.FindAll().Select(p =>
        {
            var list = devices.Where(e => e.ProductId == p.Id).ToList();
            var on = list.Count(e => e.Online);
            var messages = (stats.GetValueOrDefault(p.Id)?.Messages ?? 0) + counter.PendingUplink(p.Id);
            return new ProductStat(p.Id, p.Name, p.Protocol, list.Count, on, list.Count == 0 ? 0 : Math.Round(on * 100.0 / list.Count, 1), messages);
        }).OrderByDescending(e => e.Devices).ToList();
    }

    /// <summary>趋势。hour 级别返回最近 N 小时，day 级别返回最近 N 天，缺失的时间点补零</summary>
    public IList<TrendPoint> GetTrend(StatLevels level, Int32 count, Int32 productId = 0)
    {
        count = Math.Clamp(count, 1, level == StatLevels.Hour ? 168 : 366);
        var now = DateTime.Now;
        var end = level == StatLevels.Hour ? now.Date.AddHours(now.Hour) : now.Date;
        var start = level == StatLevels.Hour ? end.AddHours(1 - count) : end.AddDays(1 - count);

        var rows = DeviceStat.FindAll(DeviceStat._.Level == level & DeviceStat._.ProductId == productId & DeviceStat._.StatTime >= start & DeviceStat._.StatTime <= end)
            .ToDictionary(e => e.StatTime);
        var pending = counter.PendingUplink(productId);

        var result = new List<TrendPoint>(count);
        for (var t = start; t <= end; t = level == StatLevels.Hour ? t.AddHours(1) : t.AddDays(1))
        {
            var r = rows.GetValueOrDefault(t);
            var messages = (r?.Messages ?? 0) + (t == end ? pending : 0);
            result.Add(new TrendPoint(t, r?.Total ?? 0, r?.Online ?? 0, r?.Actives ?? 0, messages, r?.Downlinks ?? 0, r?.Alarms ?? 0));
        }

        return result;
    }

    /// <summary>把内存计数与设备快照写入小时/天统计表，由后台任务每分钟调用</summary>
    public void Flush()
    {
        var drained = counter.Drain();
        var devices = Device.FindAll();
        var now = DateTime.Now;
        var hour = now.Date.AddHours(now.Hour);
        var day = now.Date;

        var productIds = devices.Select(e => e.ProductId).Concat(drained.Keys).Where(e => e > 0).Distinct().Append(0).ToList();
        foreach (var productId in productIds)
        {
            var scope = productId == 0 ? devices : devices.Where(e => e.ProductId == productId).ToList();
            var delta = productId == 0
                ? (drained.Values.Sum(e => e.Uplink), drained.Values.Sum(e => e.Downlink), drained.Values.Sum(e => e.Alarms))
                : drained.GetValueOrDefault(productId);

            Upsert(StatLevels.Hour, hour, productId, scope, delta, now);
            Upsert(StatLevels.Day, day, productId, scope, delta, now);
        }
    }

    /// <summary>清理过期的历史数据</summary>
    public Int32 Cleanup(Int32 retentionDays)
    {
        if (retentionDays <= 0) return 0;

        var end = DateTime.Today.AddDays(-retentionDays);
        var rows = DeviceData.DeleteWith(DateTime.MinValue, end, 100_000)
            + DeviceEvent.DeleteWith(DateTime.MinValue, end, 100_000)
            + DeviceHistory.DeleteWith(DateTime.MinValue, end, 100_000)
            + RuleLog.DeleteWith(DateTime.MinValue, end, 100_000);
        rows += DeviceStat.Delete(DeviceStat._.Level == StatLevels.Hour & DeviceStat._.StatTime < end);

        if (rows > 0) XTrace.WriteLine("清理 {0} 天前的历史数据 {1} 行", retentionDays, rows);
        return rows;
    }

    private static void Upsert(StatLevels level, DateTime time, Int32 productId, IList<Device> scope, (Int64 Uplink, Int64 Downlink, Int64 Alarms) delta, DateTime now)
    {
        var st = FindStat(level, time, productId) ?? new DeviceStat { Level = level, StatTime = time, ProductId = productId, CreateTime = now };
        var online = scope.Count(e => e.Online);

        st.Total = scope.Count;
        st.Online = Math.Max(st.Online, online);
        st.Actives = scope.Count(e => e.LastActive >= time);
        st.NewDevices = scope.Count(e => e.CreateTime >= time);
        st.Messages += delta.Uplink;
        st.Downlinks += (Int32)delta.Downlink;
        st.Alarms += (Int32)delta.Alarms;
        st.UpdateTime = now;
        st.Save();
    }

    private static DeviceStat? FindStat(StatLevels level, DateTime time, Int32 productId) =>
        DeviceStat.Find(DeviceStat._.Level == level & DeviceStat._.StatTime == time & DeviceStat._.ProductId == productId);
}
