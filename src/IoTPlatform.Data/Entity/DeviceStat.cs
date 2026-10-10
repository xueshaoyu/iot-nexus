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

/// <summary>设备统计。按小时/天汇总设备数、在线数、消息量等指标，产品0表示全平台</summary>
[Serializable]
[DataObject]
[Description("设备统计。按小时/天汇总设备数、在线数、消息量等指标，产品0表示全平台")]
[BindIndex("IU_DeviceStat_Level_StatTime_ProductId", true, "Level,StatTime,ProductId")]
[BindIndex("IX_DeviceStat_ProductId_Level_StatTime", false, "ProductId,Level,StatTime")]
[BindTable("DeviceStat", Description = "设备统计。按小时/天汇总设备数、在线数、消息量等指标，产品0表示全平台", ConnName = "IoT", DbType = DatabaseType.None)]
public partial class DeviceStat
{
    #region 属性
    private Int32 _Id;
    /// <summary>编号</summary>
    [DisplayName("编号")]
    [Description("编号")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "编号", "")]
    public Int32 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private IoTPlatform.Data.StatLevels _Level;
    /// <summary>层级</summary>
    [DisplayName("层级")]
    [Description("层级")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Level", "层级", "")]
    public IoTPlatform.Data.StatLevels Level { get => _Level; set { if (OnPropertyChanging("Level", value)) { _Level = value; OnPropertyChanged("Level"); } } }

    private DateTime _StatTime;
    /// <summary>统计时间</summary>
    [DisplayName("统计时间")]
    [Description("统计时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("StatTime", "统计时间", "")]
    public DateTime StatTime { get => _StatTime; set { if (OnPropertyChanging("StatTime", value)) { _StatTime = value; OnPropertyChanged("StatTime"); } } }

    private Int32 _ProductId;
    /// <summary>产品</summary>
    [DisplayName("产品")]
    [Description("产品")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ProductId", "产品", "")]
    public Int32 ProductId { get => _ProductId; set { if (OnPropertyChanging("ProductId", value)) { _ProductId = value; OnPropertyChanged("ProductId"); } } }

    private Int32 _Total;
    /// <summary>设备总数</summary>
    [DisplayName("设备总数")]
    [Description("设备总数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Total", "设备总数", "")]
    public Int32 Total { get => _Total; set { if (OnPropertyChanging("Total", value)) { _Total = value; OnPropertyChanged("Total"); } } }

    private Int32 _Online;
    /// <summary>在线数。统计周期内最高在线数</summary>
    [DisplayName("在线数")]
    [Description("在线数。统计周期内最高在线数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Online", "在线数。统计周期内最高在线数", "")]
    public Int32 Online { get => _Online; set { if (OnPropertyChanging("Online", value)) { _Online = value; OnPropertyChanged("Online"); } } }

    private Int32 _Actives;
    /// <summary>活跃数。统计周期内有上报的设备数</summary>
    [DisplayName("活跃数")]
    [Description("活跃数。统计周期内有上报的设备数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Actives", "活跃数。统计周期内有上报的设备数", "")]
    public Int32 Actives { get => _Actives; set { if (OnPropertyChanging("Actives", value)) { _Actives = value; OnPropertyChanged("Actives"); } } }

    private Int32 _NewDevices;
    /// <summary>新增设备</summary>
    [DisplayName("新增设备")]
    [Description("新增设备")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("NewDevices", "新增设备", "")]
    public Int32 NewDevices { get => _NewDevices; set { if (OnPropertyChanging("NewDevices", value)) { _NewDevices = value; OnPropertyChanged("NewDevices"); } } }

    private Int64 _Messages;
    /// <summary>上行消息</summary>
    [DisplayName("上行消息")]
    [Description("上行消息")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Messages", "上行消息", "")]
    public Int64 Messages { get => _Messages; set { if (OnPropertyChanging("Messages", value)) { _Messages = value; OnPropertyChanged("Messages"); } } }

    private Int32 _Downlinks;
    /// <summary>下行消息</summary>
    [DisplayName("下行消息")]
    [Description("下行消息")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Downlinks", "下行消息", "")]
    public Int32 Downlinks { get => _Downlinks; set { if (OnPropertyChanging("Downlinks", value)) { _Downlinks = value; OnPropertyChanged("Downlinks"); } } }

    private Int32 _Alarms;
    /// <summary>告警数</summary>
    [DisplayName("告警数")]
    [Description("告警数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Alarms", "告警数", "")]
    public Int32 Alarms { get => _Alarms; set { if (OnPropertyChanging("Alarms", value)) { _Alarms = value; OnPropertyChanged("Alarms"); } } }

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
            "Level" => _Level,
            "StatTime" => _StatTime,
            "ProductId" => _ProductId,
            "Total" => _Total,
            "Online" => _Online,
            "Actives" => _Actives,
            "NewDevices" => _NewDevices,
            "Messages" => _Messages,
            "Downlinks" => _Downlinks,
            "Alarms" => _Alarms,
            "CreateTime" => _CreateTime,
            "UpdateTime" => _UpdateTime,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToInt(); break;
                case "Level": _Level = (IoTPlatform.Data.StatLevels)value.ToInt(); break;
                case "StatTime": _StatTime = value.ToDateTime(); break;
                case "ProductId": _ProductId = value.ToInt(); break;
                case "Total": _Total = value.ToInt(); break;
                case "Online": _Online = value.ToInt(); break;
                case "Actives": _Actives = value.ToInt(); break;
                case "NewDevices": _NewDevices = value.ToInt(); break;
                case "Messages": _Messages = value.ToLong(); break;
                case "Downlinks": _Downlinks = value.ToInt(); break;
                case "Alarms": _Alarms = value.ToInt(); break;
                case "CreateTime": _CreateTime = value.ToDateTime(); break;
                case "UpdateTime": _UpdateTime = value.ToDateTime(); break;
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

    #endregion

    #region 扩展查询
    /// <summary>根据编号查找</summary>
    /// <param name="id">编号</param>
    /// <returns>实体对象</returns>
    public static DeviceStat? FindById(Int32 id)
    {
        if (id < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="level">层级</param>
    /// <param name="productId">产品</param>
    /// <param name="start">统计时间开始</param>
    /// <param name="end">统计时间结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<DeviceStat> Search(IoTPlatform.Data.StatLevels level, Int32 productId, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (level >= 0) exp &= _.Level == level;
        if (productId >= 0) exp &= _.ProductId == productId;
        exp &= _.StatTime.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    /// <summary>取得设备统计字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>层级</summary>
        public static readonly Field Level = FindByName("Level");

        /// <summary>统计时间</summary>
        public static readonly Field StatTime = FindByName("StatTime");

        /// <summary>产品</summary>
        public static readonly Field ProductId = FindByName("ProductId");

        /// <summary>设备总数</summary>
        public static readonly Field Total = FindByName("Total");

        /// <summary>在线数。统计周期内最高在线数</summary>
        public static readonly Field Online = FindByName("Online");

        /// <summary>活跃数。统计周期内有上报的设备数</summary>
        public static readonly Field Actives = FindByName("Actives");

        /// <summary>新增设备</summary>
        public static readonly Field NewDevices = FindByName("NewDevices");

        /// <summary>上行消息</summary>
        public static readonly Field Messages = FindByName("Messages");

        /// <summary>下行消息</summary>
        public static readonly Field Downlinks = FindByName("Downlinks");

        /// <summary>告警数</summary>
        public static readonly Field Alarms = FindByName("Alarms");

        /// <summary>创建时间</summary>
        public static readonly Field CreateTime = FindByName("CreateTime");

        /// <summary>更新时间</summary>
        public static readonly Field UpdateTime = FindByName("UpdateTime");

        static Field FindByName(String name) => Meta.Table.FindByName(name)!;
    }

    /// <summary>取得设备统计字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>编号</summary>
        public const String Id = "Id";

        /// <summary>层级</summary>
        public const String Level = "Level";

        /// <summary>统计时间</summary>
        public const String StatTime = "StatTime";

        /// <summary>产品</summary>
        public const String ProductId = "ProductId";

        /// <summary>设备总数</summary>
        public const String Total = "Total";

        /// <summary>在线数。统计周期内最高在线数</summary>
        public const String Online = "Online";

        /// <summary>活跃数。统计周期内有上报的设备数</summary>
        public const String Actives = "Actives";

        /// <summary>新增设备</summary>
        public const String NewDevices = "NewDevices";

        /// <summary>上行消息</summary>
        public const String Messages = "Messages";

        /// <summary>下行消息</summary>
        public const String Downlinks = "Downlinks";

        /// <summary>告警数</summary>
        public const String Alarms = "Alarms";

        /// <summary>创建时间</summary>
        public const String CreateTime = "CreateTime";

        /// <summary>更新时间</summary>
        public const String UpdateTime = "UpdateTime";
    }
    #endregion
}
