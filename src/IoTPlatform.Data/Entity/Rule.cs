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

/// <summary>规则。基于属性、事件、上下线触发的条件动作规则</summary>
[Serializable]
[DataObject]
[Description("规则。基于属性、事件、上下线触发的条件动作规则")]
[BindIndex("IX_Rule_Name", false, "Name")]
[BindTable("Rule", Description = "规则。基于属性、事件、上下线触发的条件动作规则", ConnName = "IoT", DbType = DatabaseType.None)]
public partial class Rule
{
    #region 属性
    private Int32 _Id;
    /// <summary>编号</summary>
    [DisplayName("编号")]
    [Description("编号")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "编号", "")]
    public Int32 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private String _Name = null!;
    /// <summary>名称</summary>
    [DisplayName("名称")]
    [Description("名称")]
    [DataObjectField(false, false, false, 50)]
    [BindColumn("Name", "名称", "", Master = true)]
    public String Name { get => _Name; set { if (OnPropertyChanging("Name", value)) { _Name = value; OnPropertyChanged("Name"); } } }

    private Boolean _Enable;
    /// <summary>启用</summary>
    [DisplayName("启用")]
    [Description("启用")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Enable", "启用", "")]
    public Boolean Enable { get => _Enable; set { if (OnPropertyChanging("Enable", value)) { _Enable = value; OnPropertyChanged("Enable"); } } }

    private String? _TriggerType;
    /// <summary>触发类型。Property/Event/Online/Offline</summary>
    [DisplayName("触发类型")]
    [Description("触发类型。Property/Event/Online/Offline")]
    [DataObjectField(false, false, true, 20)]
    [BindColumn("TriggerType", "触发类型。Property/Event/Online/Offline", "")]
    public String? TriggerType { get => _TriggerType; set { if (OnPropertyChanging("TriggerType", value)) { _TriggerType = value; OnPropertyChanged("TriggerType"); } } }

    private Int32 _ProductId;
    /// <summary>产品。0表示全部</summary>
    [DisplayName("产品")]
    [Description("产品。0表示全部")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ProductId", "产品。0表示全部", "")]
    public Int32 ProductId { get => _ProductId; set { if (OnPropertyChanging("ProductId", value)) { _ProductId = value; OnPropertyChanged("ProductId"); } } }

    private Int32 _GroupId;
    /// <summary>分组。0表示全部</summary>
    [DisplayName("分组")]
    [Description("分组。0表示全部")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("GroupId", "分组。0表示全部", "")]
    public Int32 GroupId { get => _GroupId; set { if (OnPropertyChanging("GroupId", value)) { _GroupId = value; OnPropertyChanged("GroupId"); } } }

    private Int32 _DeviceId;
    /// <summary>设备。0表示全部</summary>
    [DisplayName("设备")]
    [Description("设备。0表示全部")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("DeviceId", "设备。0表示全部", "")]
    public Int32 DeviceId { get => _DeviceId; set { if (OnPropertyChanging("DeviceId", value)) { _DeviceId = value; OnPropertyChanged("DeviceId"); } } }

    private String? _EventName;
    /// <summary>事件名。事件触发时匹配的事件标识，空表示任意</summary>
    [DisplayName("事件名")]
    [Description("事件名。事件触发时匹配的事件标识，空表示任意")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("EventName", "事件名。事件触发时匹配的事件标识，空表示任意", "")]
    public String? EventName { get => _EventName; set { if (OnPropertyChanging("EventName", value)) { _EventName = value; OnPropertyChanged("EventName"); } } }

    private String? _ConditionLogic;
    /// <summary>条件逻辑。All全部满足，Any任一满足</summary>
    [DisplayName("条件逻辑")]
    [Description("条件逻辑。All全部满足，Any任一满足")]
    [DataObjectField(false, false, true, 10)]
    [BindColumn("ConditionLogic", "条件逻辑。All全部满足，Any任一满足", "")]
    public String? ConditionLogic { get => _ConditionLogic; set { if (OnPropertyChanging("ConditionLogic", value)) { _ConditionLogic = value; OnPropertyChanged("ConditionLogic"); } } }

    private String? _Conditions;
    /// <summary>条件。Json数组，元素为{property,operator,value}</summary>
    [DisplayName("条件")]
    [Description("条件。Json数组，元素为{property,operator,value}")]
    [DataObjectField(false, false, true, -1)]
    [BindColumn("Conditions", "条件。Json数组，元素为{property,operator,value}", "")]
    public String? Conditions { get => _Conditions; set { if (OnPropertyChanging("Conditions", value)) { _Conditions = value; OnPropertyChanged("Conditions"); } } }

    private String? _Actions;
    /// <summary>动作。Json数组，元素为{type,config}</summary>
    [DisplayName("动作")]
    [Description("动作。Json数组，元素为{type,config}")]
    [DataObjectField(false, false, true, -1)]
    [BindColumn("Actions", "动作。Json数组，元素为{type,config}", "")]
    public String? Actions { get => _Actions; set { if (OnPropertyChanging("Actions", value)) { _Actions = value; OnPropertyChanged("Actions"); } } }

    private Int32 _SilenceSeconds;
    /// <summary>静默期。同一设备触发后在该时间内不再重复执行，单位秒</summary>
    [DisplayName("静默期")]
    [Description("静默期。同一设备触发后在该时间内不再重复执行，单位秒")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("SilenceSeconds", "静默期。同一设备触发后在该时间内不再重复执行，单位秒", "")]
    public Int32 SilenceSeconds { get => _SilenceSeconds; set { if (OnPropertyChanging("SilenceSeconds", value)) { _SilenceSeconds = value; OnPropertyChanged("SilenceSeconds"); } } }

    private Int32 _Triggers;
    /// <summary>触发次数</summary>
    [DisplayName("触发次数")]
    [Description("触发次数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Triggers", "触发次数", "")]
    public Int32 Triggers { get => _Triggers; set { if (OnPropertyChanging("Triggers", value)) { _Triggers = value; OnPropertyChanged("Triggers"); } } }

    private DateTime _LastTrigger;
    /// <summary>最后触发</summary>
    [DisplayName("最后触发")]
    [Description("最后触发")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("LastTrigger", "最后触发", "")]
    public DateTime LastTrigger { get => _LastTrigger; set { if (OnPropertyChanging("LastTrigger", value)) { _LastTrigger = value; OnPropertyChanged("LastTrigger"); } } }

    private DateTime _CreateTime;
    /// <summary>创建时间</summary>
    [Category("扩展")]
    [DisplayName("创建时间")]
    [Description("创建时间")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("CreateTime", "创建时间", "")]
    public DateTime CreateTime { get => _CreateTime; set { if (OnPropertyChanging("CreateTime", value)) { _CreateTime = value; OnPropertyChanged("CreateTime"); } } }

    private DateTime _UpdateTime;
    /// <summary>更新时间</summary>
    [Category("扩展")]
    [DisplayName("更新时间")]
    [Description("更新时间")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("UpdateTime", "更新时间", "")]
    public DateTime UpdateTime { get => _UpdateTime; set { if (OnPropertyChanging("UpdateTime", value)) { _UpdateTime = value; OnPropertyChanged("UpdateTime"); } } }

    private String? _Remark;
    /// <summary>备注</summary>
    [Category("扩展")]
    [DisplayName("备注")]
    [Description("备注")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("Remark", "备注", "")]
    public String? Remark { get => _Remark; set { if (OnPropertyChanging("Remark", value)) { _Remark = value; OnPropertyChanged("Remark"); } } }
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
            "Name" => _Name,
            "Enable" => _Enable,
            "TriggerType" => _TriggerType,
            "ProductId" => _ProductId,
            "GroupId" => _GroupId,
            "DeviceId" => _DeviceId,
            "EventName" => _EventName,
            "ConditionLogic" => _ConditionLogic,
            "Conditions" => _Conditions,
            "Actions" => _Actions,
            "SilenceSeconds" => _SilenceSeconds,
            "Triggers" => _Triggers,
            "LastTrigger" => _LastTrigger,
            "CreateTime" => _CreateTime,
            "UpdateTime" => _UpdateTime,
            "Remark" => _Remark,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToInt(); break;
                case "Name": _Name = Convert.ToString(value); break;
                case "Enable": _Enable = value.ToBoolean(); break;
                case "TriggerType": _TriggerType = Convert.ToString(value); break;
                case "ProductId": _ProductId = value.ToInt(); break;
                case "GroupId": _GroupId = value.ToInt(); break;
                case "DeviceId": _DeviceId = value.ToInt(); break;
                case "EventName": _EventName = Convert.ToString(value); break;
                case "ConditionLogic": _ConditionLogic = Convert.ToString(value); break;
                case "Conditions": _Conditions = Convert.ToString(value); break;
                case "Actions": _Actions = Convert.ToString(value); break;
                case "SilenceSeconds": _SilenceSeconds = value.ToInt(); break;
                case "Triggers": _Triggers = value.ToInt(); break;
                case "LastTrigger": _LastTrigger = value.ToDateTime(); break;
                case "CreateTime": _CreateTime = value.ToDateTime(); break;
                case "UpdateTime": _UpdateTime = value.ToDateTime(); break;
                case "Remark": _Remark = Convert.ToString(value); break;
                default: base[name] = value; break;
            }
        }
    }
    #endregion

    #region 关联映射
    /// <summary>产品</summary>
    [XmlIgnore, IgnoreDataMember, ScriptIgnore]
    public Product? Product => Extends.Get(nameof(Product), k => Product.FindById(ProductId));

    /// <summary>产品</summary>
    [Map(nameof(ProductId), typeof(Product), "Id")]
    public String? ProductName => Product?.Name;

    /// <summary>分组</summary>
    [XmlIgnore, IgnoreDataMember, ScriptIgnore]
    public DeviceGroup? Group => Extends.Get(nameof(Group), k => DeviceGroup.FindById(GroupId));

    /// <summary>分组</summary>
    [Map(nameof(GroupId), typeof(DeviceGroup), "Id")]
    public String? GroupName => Group?.Name;

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
    public static Rule? FindById(Int32 id)
    {
        if (id < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }

    /// <summary>根据名称查找</summary>
    /// <param name="name">名称</param>
    /// <returns>实体列表</returns>
    public static IList<Rule> FindAllByName(String name)
    {
        if (name.IsNullOrEmpty()) return [];

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.FindAll(e => e.Name.EqualIgnoreCase(name));

        return FindAll(_.Name == name);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="productId">产品。0表示全部</param>
    /// <param name="groupId">分组。0表示全部</param>
    /// <param name="deviceId">设备。0表示全部</param>
    /// <param name="enable">启用</param>
    /// <param name="start">更新时间开始</param>
    /// <param name="end">更新时间结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<Rule> Search(Int32 productId, Int32 groupId, Int32 deviceId, Boolean? enable, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (productId >= 0) exp &= _.ProductId == productId;
        if (groupId >= 0) exp &= _.GroupId == groupId;
        if (deviceId >= 0) exp &= _.DeviceId == deviceId;
        if (enable != null) exp &= _.Enable == enable;
        exp &= _.UpdateTime.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    /// <summary>取得规则字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>名称</summary>
        public static readonly Field Name = FindByName("Name");

        /// <summary>启用</summary>
        public static readonly Field Enable = FindByName("Enable");

        /// <summary>触发类型。Property/Event/Online/Offline</summary>
        public static readonly Field TriggerType = FindByName("TriggerType");

        /// <summary>产品。0表示全部</summary>
        public static readonly Field ProductId = FindByName("ProductId");

        /// <summary>分组。0表示全部</summary>
        public static readonly Field GroupId = FindByName("GroupId");

        /// <summary>设备。0表示全部</summary>
        public static readonly Field DeviceId = FindByName("DeviceId");

        /// <summary>事件名。事件触发时匹配的事件标识，空表示任意</summary>
        public static readonly Field EventName = FindByName("EventName");

        /// <summary>条件逻辑。All全部满足，Any任一满足</summary>
        public static readonly Field ConditionLogic = FindByName("ConditionLogic");

        /// <summary>条件。Json数组，元素为{property,operator,value}</summary>
        public static readonly Field Conditions = FindByName("Conditions");

        /// <summary>动作。Json数组，元素为{type,config}</summary>
        public static readonly Field Actions = FindByName("Actions");

        /// <summary>静默期。同一设备触发后在该时间内不再重复执行，单位秒</summary>
        public static readonly Field SilenceSeconds = FindByName("SilenceSeconds");

        /// <summary>触发次数</summary>
        public static readonly Field Triggers = FindByName("Triggers");

        /// <summary>最后触发</summary>
        public static readonly Field LastTrigger = FindByName("LastTrigger");

        /// <summary>创建时间</summary>
        public static readonly Field CreateTime = FindByName("CreateTime");

        /// <summary>更新时间</summary>
        public static readonly Field UpdateTime = FindByName("UpdateTime");

        /// <summary>备注</summary>
        public static readonly Field Remark = FindByName("Remark");

        static Field FindByName(String name) => Meta.Table.FindByName(name)!;
    }

    /// <summary>取得规则字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>编号</summary>
        public const String Id = "Id";

        /// <summary>名称</summary>
        public const String Name = "Name";

        /// <summary>启用</summary>
        public const String Enable = "Enable";

        /// <summary>触发类型。Property/Event/Online/Offline</summary>
        public const String TriggerType = "TriggerType";

        /// <summary>产品。0表示全部</summary>
        public const String ProductId = "ProductId";

        /// <summary>分组。0表示全部</summary>
        public const String GroupId = "GroupId";

        /// <summary>设备。0表示全部</summary>
        public const String DeviceId = "DeviceId";

        /// <summary>事件名。事件触发时匹配的事件标识，空表示任意</summary>
        public const String EventName = "EventName";

        /// <summary>条件逻辑。All全部满足，Any任一满足</summary>
        public const String ConditionLogic = "ConditionLogic";

        /// <summary>条件。Json数组，元素为{property,operator,value}</summary>
        public const String Conditions = "Conditions";

        /// <summary>动作。Json数组，元素为{type,config}</summary>
        public const String Actions = "Actions";

        /// <summary>静默期。同一设备触发后在该时间内不再重复执行，单位秒</summary>
        public const String SilenceSeconds = "SilenceSeconds";

        /// <summary>触发次数</summary>
        public const String Triggers = "Triggers";

        /// <summary>最后触发</summary>
        public const String LastTrigger = "LastTrigger";

        /// <summary>创建时间</summary>
        public const String CreateTime = "CreateTime";

        /// <summary>更新时间</summary>
        public const String UpdateTime = "UpdateTime";

        /// <summary>备注</summary>
        public const String Remark = "Remark";
    }
    #endregion
}
