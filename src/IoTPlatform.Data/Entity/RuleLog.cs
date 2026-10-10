using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;
using NewLife;
using NewLife.Data;
using XCode;
using XCode.Cache;
using XCode.Configuration;
using XCode.DataAccessLayer;

namespace IoTPlatform.Data;

/// <summary>规则日志。每次规则触发的上下文与动作执行结果</summary>
[Serializable]
[DataObject]
[Description("规则日志。每次规则触发的上下文与动作执行结果")]
[BindIndex("IX_RuleLog_RuleId_Id", false, "RuleId,Id")]
[BindIndex("IX_RuleLog_DeviceId_Id", false, "DeviceId,Id")]
[BindTable("RuleLog", Description = "规则日志。每次规则触发的上下文与动作执行结果", ConnName = "IoT", DbType = DatabaseType.None)]
public partial class RuleLog
{
    #region 属性
    private Int64 _Id;
    /// <summary>编号</summary>
    [DisplayName("编号")]
    [Description("编号")]
    [DataObjectField(true, false, false, 0)]
    [BindColumn("Id", "编号", "", DataScale = "time")]
    public Int64 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private Int32 _RuleId;
    /// <summary>规则</summary>
    [DisplayName("规则")]
    [Description("规则")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("RuleId", "规则", "")]
    public Int32 RuleId { get => _RuleId; set { if (OnPropertyChanging("RuleId", value)) { _RuleId = value; OnPropertyChanged("RuleId"); } } }

    private Int32 _DeviceId;
    /// <summary>设备</summary>
    [DisplayName("设备")]
    [Description("设备")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("DeviceId", "设备", "")]
    public Int32 DeviceId { get => _DeviceId; set { if (OnPropertyChanging("DeviceId", value)) { _DeviceId = value; OnPropertyChanged("DeviceId"); } } }

    private String? _TriggerType;
    /// <summary>触发类型</summary>
    [DisplayName("触发类型")]
    [Description("触发类型")]
    [DataObjectField(false, false, true, 20)]
    [BindColumn("TriggerType", "触发类型", "")]
    public String? TriggerType { get => _TriggerType; set { if (OnPropertyChanging("TriggerType", value)) { _TriggerType = value; OnPropertyChanged("TriggerType"); } } }

    private String? _Snapshot;
    /// <summary>快照。触发时的数据</summary>
    [DisplayName("快照")]
    [Description("快照。触发时的数据")]
    [DataObjectField(false, false, true, 2000)]
    [BindColumn("Snapshot", "快照。触发时的数据", "")]
    public String? Snapshot { get => _Snapshot; set { if (OnPropertyChanging("Snapshot", value)) { _Snapshot = value; OnPropertyChanged("Snapshot"); } } }

    private Boolean _Success;
    /// <summary>成功</summary>
    [DisplayName("成功")]
    [Description("成功")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Success", "成功", "")]
    public Boolean Success { get => _Success; set { if (OnPropertyChanging("Success", value)) { _Success = value; OnPropertyChanged("Success"); } } }

    private String? _Result;
    /// <summary>结果。各动作执行结果</summary>
    [DisplayName("结果")]
    [Description("结果。各动作执行结果")]
    [DataObjectField(false, false, true, 2000)]
    [BindColumn("Result", "结果。各动作执行结果", "")]
    public String? Result { get => _Result; set { if (OnPropertyChanging("Result", value)) { _Result = value; OnPropertyChanged("Result"); } } }

    private DateTime _CreateTime;
    /// <summary>时间</summary>
    [DisplayName("时间")]
    [Description("时间")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("CreateTime", "时间", "")]
    public DateTime CreateTime { get => _CreateTime; set { if (OnPropertyChanging("CreateTime", value)) { _CreateTime = value; OnPropertyChanged("CreateTime"); } } }
    #endregion

    #region 获取/设置 字段值
    /// <summary>获取/设置 字段值</summary>
    /// <param name="name">字段名</param>
    /// <returns></returns>
    public override Object? this[String name]
    {
        get => name switch
        {
            "Id" => _Id,
            "RuleId" => _RuleId,
            "DeviceId" => _DeviceId,
            "TriggerType" => _TriggerType,
            "Snapshot" => _Snapshot,
            "Success" => _Success,
            "Result" => _Result,
            "CreateTime" => _CreateTime,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToLong(); break;
                case "RuleId": _RuleId = value.ToInt(); break;
                case "DeviceId": _DeviceId = value.ToInt(); break;
                case "TriggerType": _TriggerType = Convert.ToString(value); break;
                case "Snapshot": _Snapshot = Convert.ToString(value); break;
                case "Success": _Success = value.ToBoolean(); break;
                case "Result": _Result = Convert.ToString(value); break;
                case "CreateTime": _CreateTime = value.ToDateTime(); break;
                default: base[name] = value; break;
            }
        }
    }
    #endregion

    #region 关联映射
    /// <summary>规则</summary>
    [XmlIgnore, IgnoreDataMember, ScriptIgnore]
    public Rule? Rule => Extends.Get(nameof(Rule), k => Rule.FindById(RuleId));

    /// <summary>规则</summary>
    [Map(nameof(RuleId), typeof(Rule), "Id")]
    public String? RuleName => Rule?.Name;

    /// <summary>设备</summary>
    [XmlIgnore, IgnoreDataMember, ScriptIgnore]
    public Device? Device => Extends.Get(nameof(Device), k => Device.FindById(DeviceId));

    /// <summary>设备</summary>
    [Map(nameof(DeviceId), typeof(Device), "Id")]
    public String? DeviceName => Device?.Name;

    #endregion

    #region 扩展查询
    /// <summary>根据编号查找</summary>
    /// <param name="id">编号</param>
    /// <returns>实体对象</returns>
    public static RuleLog? FindById(Int64 id)
    {
        if (id < 0) return null;

        return Find(_.Id == id);
    }

    /// <summary>根据规则查找</summary>
    /// <param name="ruleId">规则</param>
    /// <returns>实体列表</returns>
    public static IList<RuleLog> FindAllByRuleId(Int32 ruleId)
    {
        if (ruleId < 0) return [];

        return FindAll(_.RuleId == ruleId);
    }

    /// <summary>根据设备查找</summary>
    /// <param name="deviceId">设备</param>
    /// <returns>实体列表</returns>
    public static IList<RuleLog> FindAllByDeviceId(Int32 deviceId)
    {
        if (deviceId < 0) return [];

        return FindAll(_.DeviceId == deviceId);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="ruleId">规则</param>
    /// <param name="deviceId">设备</param>
    /// <param name="success">成功</param>
    /// <param name="start">编号开始</param>
    /// <param name="end">编号结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<RuleLog> Search(Int32 ruleId, Int32 deviceId, Boolean? success, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (ruleId >= 0) exp &= _.RuleId == ruleId;
        if (deviceId >= 0) exp &= _.DeviceId == deviceId;
        if (success != null) exp &= _.Success == success;
        exp &= _.Id.Between(start, end, Meta.Factory.Snow);
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 数据清理
    /// <summary>清理指定时间段内的数据</summary>
    /// <param name="start">开始时间。未指定时清理小于指定时间的所有数据</param>
    /// <param name="end">结束时间</param>
    /// <param name="maximumRows">最大删除行数。清理历史数据时，避免一次性删除过多导致数据库IO跟不上，0表示所有</param>
    /// <returns>清理行数</returns>
    public static Int32 DeleteWith(DateTime start, DateTime end, Int32 maximumRows = 0)
    {
        return Delete(_.Id.Between(start, end, Meta.Factory.Snow), maximumRows);
    }
    #endregion

    #region 字段名
    /// <summary>取得规则日志字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>规则</summary>
        public static readonly Field RuleId = FindByName("RuleId");

        /// <summary>设备</summary>
        public static readonly Field DeviceId = FindByName("DeviceId");

        /// <summary>触发类型</summary>
        public static readonly Field TriggerType = FindByName("TriggerType");

        /// <summary>快照。触发时的数据</summary>
        public static readonly Field Snapshot = FindByName("Snapshot");

        /// <summary>成功</summary>
        public static readonly Field Success = FindByName("Success");

        /// <summary>结果。各动作执行结果</summary>
        public static readonly Field Result = FindByName("Result");

        /// <summary>时间</summary>
        public static readonly Field CreateTime = FindByName("CreateTime");

        static Field FindByName(String name) => Meta.Table.FindByName(name)!;
    }

    /// <summary>取得规则日志字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>编号</summary>
        public const String Id = "Id";

        /// <summary>规则</summary>
        public const String RuleId = "RuleId";

        /// <summary>设备</summary>
        public const String DeviceId = "DeviceId";

        /// <summary>触发类型</summary>
        public const String TriggerType = "TriggerType";

        /// <summary>快照。触发时的数据</summary>
        public const String Snapshot = "Snapshot";

        /// <summary>成功</summary>
        public const String Success = "Success";

        /// <summary>结果。各动作执行结果</summary>
        public const String Result = "Result";

        /// <summary>时间</summary>
        public const String CreateTime = "CreateTime";
    }
    #endregion
}
