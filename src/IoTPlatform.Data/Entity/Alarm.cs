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

/// <summary>告警。由规则告警动作产生，支持确认处理</summary>
[Serializable]
[DataObject]
[Description("告警。由规则告警动作产生，支持确认处理")]
[BindIndex("IX_Alarm_Status_Id", false, "Status,Id")]
[BindIndex("IX_Alarm_DeviceId_Id", false, "DeviceId,Id")]
[BindTable("Alarm", Description = "告警。由规则告警动作产生，支持确认处理", ConnName = "IoT", DbType = DatabaseType.None)]
public partial class Alarm
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

    private IoTPlatform.Data.AlarmLevels _Level;
    /// <summary>级别</summary>
    [DisplayName("级别")]
    [Description("级别")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Level", "级别", "")]
    public IoTPlatform.Data.AlarmLevels Level { get => _Level; set { if (OnPropertyChanging("Level", value)) { _Level = value; OnPropertyChanged("Level"); } } }

    private String? _Title;
    /// <summary>标题</summary>
    [DisplayName("标题")]
    [Description("标题")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Title", "标题", "", Master = true)]
    public String? Title { get => _Title; set { if (OnPropertyChanging("Title", value)) { _Title = value; OnPropertyChanged("Title"); } } }

    private String? _Content;
    /// <summary>内容</summary>
    [DisplayName("内容")]
    [Description("内容")]
    [DataObjectField(false, false, true, 2000)]
    [BindColumn("Content", "内容", "")]
    public String? Content { get => _Content; set { if (OnPropertyChanging("Content", value)) { _Content = value; OnPropertyChanged("Content"); } } }

    private IoTPlatform.Data.AlarmStatus _Status;
    /// <summary>状态</summary>
    [DisplayName("状态")]
    [Description("状态")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Status", "状态", "")]
    public IoTPlatform.Data.AlarmStatus Status { get => _Status; set { if (OnPropertyChanging("Status", value)) { _Status = value; OnPropertyChanged("Status"); } } }

    private DateTime _HandleTime;
    /// <summary>处理时间</summary>
    [DisplayName("处理时间")]
    [Description("处理时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("HandleTime", "处理时间", "")]
    public DateTime HandleTime { get => _HandleTime; set { if (OnPropertyChanging("HandleTime", value)) { _HandleTime = value; OnPropertyChanged("HandleTime"); } } }

    private String? _HandleRemark;
    /// <summary>处理备注</summary>
    [DisplayName("处理备注")]
    [Description("处理备注")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("HandleRemark", "处理备注", "")]
    public String? HandleRemark { get => _HandleRemark; set { if (OnPropertyChanging("HandleRemark", value)) { _HandleRemark = value; OnPropertyChanged("HandleRemark"); } } }

    private DateTime _CreateTime;
    /// <summary>创建时间</summary>
    [DisplayName("创建时间")]
    [Description("创建时间")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("CreateTime", "创建时间", "")]
    public DateTime CreateTime { get => _CreateTime; set { if (OnPropertyChanging("CreateTime", value)) { _CreateTime = value; OnPropertyChanged("CreateTime"); } } }

    private DateTime _UpdateTime;
    /// <summary>更新时间</summary>
    [DisplayName("更新时间")]
    [Description("更新时间")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("UpdateTime", "更新时间", "")]
    public DateTime UpdateTime { get => _UpdateTime; set { if (OnPropertyChanging("UpdateTime", value)) { _UpdateTime = value; OnPropertyChanged("UpdateTime"); } } }
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
            "Level" => _Level,
            "Title" => _Title,
            "Content" => _Content,
            "Status" => _Status,
            "HandleTime" => _HandleTime,
            "HandleRemark" => _HandleRemark,
            "CreateTime" => _CreateTime,
            "UpdateTime" => _UpdateTime,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToLong(); break;
                case "RuleId": _RuleId = value.ToInt(); break;
                case "DeviceId": _DeviceId = value.ToInt(); break;
                case "Level": _Level = (IoTPlatform.Data.AlarmLevels)value.ToInt(); break;
                case "Title": _Title = Convert.ToString(value); break;
                case "Content": _Content = Convert.ToString(value); break;
                case "Status": _Status = (IoTPlatform.Data.AlarmStatus)value.ToInt(); break;
                case "HandleTime": _HandleTime = value.ToDateTime(); break;
                case "HandleRemark": _HandleRemark = Convert.ToString(value); break;
                case "CreateTime": _CreateTime = value.ToDateTime(); break;
                case "UpdateTime": _UpdateTime = value.ToDateTime(); break;
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
    public static Alarm? FindById(Int64 id)
    {
        if (id < 0) return null;

        return Find(_.Id == id);
    }

    /// <summary>根据状态查找</summary>
    /// <param name="status">状态</param>
    /// <returns>实体列表</returns>
    public static IList<Alarm> FindAllByStatus(IoTPlatform.Data.AlarmStatus status)
    {
        if (status < 0) return [];

        return FindAll(_.Status == status);
    }

    /// <summary>根据设备查找</summary>
    /// <param name="deviceId">设备</param>
    /// <returns>实体列表</returns>
    public static IList<Alarm> FindAllByDeviceId(Int32 deviceId)
    {
        if (deviceId < 0) return [];

        return FindAll(_.DeviceId == deviceId);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="deviceId">设备</param>
    /// <param name="status">状态</param>
    /// <param name="ruleId">规则</param>
    /// <param name="level">级别</param>
    /// <param name="start">编号开始</param>
    /// <param name="end">编号结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<Alarm> Search(Int32 deviceId, IoTPlatform.Data.AlarmStatus status, Int32 ruleId, IoTPlatform.Data.AlarmLevels level, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (deviceId >= 0) exp &= _.DeviceId == deviceId;
        if (status >= 0) exp &= _.Status == status;
        if (ruleId >= 0) exp &= _.RuleId == ruleId;
        if (level >= 0) exp &= _.Level == level;
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
    /// <summary>取得告警字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>规则</summary>
        public static readonly Field RuleId = FindByName("RuleId");

        /// <summary>设备</summary>
        public static readonly Field DeviceId = FindByName("DeviceId");

        /// <summary>级别</summary>
        public static readonly Field Level = FindByName("Level");

        /// <summary>标题</summary>
        public static readonly Field Title = FindByName("Title");

        /// <summary>内容</summary>
        public static readonly Field Content = FindByName("Content");

        /// <summary>状态</summary>
        public static readonly Field Status = FindByName("Status");

        /// <summary>处理时间</summary>
        public static readonly Field HandleTime = FindByName("HandleTime");

        /// <summary>处理备注</summary>
        public static readonly Field HandleRemark = FindByName("HandleRemark");

        /// <summary>创建时间</summary>
        public static readonly Field CreateTime = FindByName("CreateTime");

        /// <summary>更新时间</summary>
        public static readonly Field UpdateTime = FindByName("UpdateTime");

        static Field FindByName(String name) => Meta.Table.FindByName(name)!;
    }

    /// <summary>取得告警字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>编号</summary>
        public const String Id = "Id";

        /// <summary>规则</summary>
        public const String RuleId = "RuleId";

        /// <summary>设备</summary>
        public const String DeviceId = "DeviceId";

        /// <summary>级别</summary>
        public const String Level = "Level";

        /// <summary>标题</summary>
        public const String Title = "Title";

        /// <summary>内容</summary>
        public const String Content = "Content";

        /// <summary>状态</summary>
        public const String Status = "Status";

        /// <summary>处理时间</summary>
        public const String HandleTime = "HandleTime";

        /// <summary>处理备注</summary>
        public const String HandleRemark = "HandleRemark";

        /// <summary>创建时间</summary>
        public const String CreateTime = "CreateTime";

        /// <summary>更新时间</summary>
        public const String UpdateTime = "UpdateTime";
    }
    #endregion
}
