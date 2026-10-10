using IoTPlatform.Data;
using IoTPlatform.Models;
using NewLife;
using NewLife.Data;
using XCode;

namespace IoTPlatform.Services;

/// <summary>告警管理</summary>
public class AlarmService(MessageCounter counter)
{
    public PagedResult<Alarm> Query(AlarmStatus? status, AlarmLevels? level, Int32 deviceId, Int32 ruleId, Int32 pageIndex = 1, Int32 pageSize = 20)
    {
        var exp = new WhereExpression();
        if (status != null) exp &= Alarm._.Status == status.Value;
        if (level != null) exp &= Alarm._.Level == level.Value;
        if (deviceId > 0) exp &= Alarm._.DeviceId == deviceId;
        if (ruleId > 0) exp &= Alarm._.RuleId == ruleId;

        var page = new PageParameter { PageIndex = Math.Max(1, pageIndex), PageSize = Math.Clamp(pageSize, 1, 500), Sort = nameof(Alarm.Id), Desc = true };
        var list = Alarm.FindAll(exp, page);

        return new PagedResult<Alarm>(list, page.TotalCount, page.PageIndex, page.PageSize);
    }

    /// <summary>产生告警。同一规则同一设备存在未处理告警时合并更新，避免告警风暴</summary>
    public Alarm Raise(Int32 ruleId, Device device, AlarmLevels level, String title, String? content)
    {
        return SqliteBusy.RetryOrFallback(
            () => RaiseOnce(ruleId, device, level, title, content, persist: true),
            () => RaiseOnce(ruleId, device, level, title, content, persist: false));
    }

    Alarm RaiseOnce(Int32 ruleId, Device device, AlarmLevels level, String title, String? content, Boolean persist)
    {
        var now = DateTime.Now;
        var alarm = ruleId > 0
            ? Alarm.Find(Alarm._.RuleId == ruleId & Alarm._.DeviceId == device.Id & Alarm._.Status == AlarmStatus.Active)
            : null;

        if (alarm != null)
        {
            alarm.Level = (AlarmLevels)Math.Max((Int32)alarm.Level, (Int32)level);
            alarm.Content = content?.Cut(2000);
            alarm.UpdateTime = now;
            if (persist) alarm.Update();
            else alarm.SaveAsync(SqliteSettings.SaveAsyncDelayMs);
            return alarm;
        }

        alarm = new Alarm
        {
            Id = Alarm.Meta.Factory.Snow.NewId(),
            RuleId = ruleId,
            DeviceId = device.Id,
            Level = level,
            Title = title.Cut(50),
            Content = content?.Cut(2000),
            Status = AlarmStatus.Active,
            CreateTime = now,
            UpdateTime = now,
        };
        if (persist) alarm.Insert();
        else alarm.SaveAsync(SqliteSettings.SaveAsyncDelayMs);
        counter.Alarm(device.ProductId);

        return alarm;
    }

    public Alarm Handle(Int64 id, AlarmHandleInput input)
    {
        var alarm = Alarm.FindById(id) ?? throw PlatformException.NotFound("Alarm");
        if (input.Status == AlarmStatus.Active) throw new PlatformException("Target status must be Acknowledged or Closed");

        alarm.Status = input.Status;
        alarm.HandleTime = DateTime.Now;
        alarm.HandleRemark = input.Remark?.Cut(500);
        alarm.UpdateTime = DateTime.Now;
        alarm.Update();

        return alarm;
    }
}
