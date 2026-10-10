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

/// <summary>产品。设备的类型定义，包含物模型（属性、事件、服务）与接入协议</summary>
[Serializable]
[DataObject]
[Description("产品。设备的类型定义，包含物模型（属性、事件、服务）与接入协议")]
[BindIndex("IU_Product_Code", true, "Code")]
[BindTable("Product", Description = "产品。设备的类型定义，包含物模型（属性、事件、服务）与接入协议", ConnName = "IoT", DbType = DatabaseType.None)]
public partial class Product
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

    private String _Code = null!;
    /// <summary>编码。ProductKey，全局唯一，用于MQTT主题</summary>
    [DisplayName("编码")]
    [Description("编码。ProductKey，全局唯一，用于MQTT主题")]
    [DataObjectField(false, false, false, 50)]
    [BindColumn("Code", "编码。ProductKey，全局唯一，用于MQTT主题", "")]
    public String Code { get => _Code; set { if (OnPropertyChanging("Code", value)) { _Code = value; OnPropertyChanged("Code"); } } }

    private String? _Category;
    /// <summary>类别</summary>
    [DisplayName("类别")]
    [Description("类别")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Category", "类别", "")]
    public String? Category { get => _Category; set { if (OnPropertyChanging("Category", value)) { _Category = value; OnPropertyChanged("Category"); } } }

    private String? _Protocol;
    /// <summary>接入协议。MQTT/HTTP/Driver</summary>
    [DisplayName("接入协议")]
    [Description("接入协议。MQTT/HTTP/Driver")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Protocol", "接入协议。MQTT/HTTP/Driver", "")]
    public String? Protocol { get => _Protocol; set { if (OnPropertyChanging("Protocol", value)) { _Protocol = value; OnPropertyChanged("Protocol"); } } }

    private String? _BrokerProfileId;
    /// <summary>默认 MQTT 服务器。空表示使用平台默认（Mode/ActiveBroker）</summary>
    [DisplayName("默认MQTT服务器")]
    [Description("默认 MQTT 服务器。空表示使用平台默认（Mode/ActiveBroker）")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("BrokerProfileId", "默认 MQTT 服务器。空表示使用平台默认（Mode/ActiveBroker）", "")]
    public String? BrokerProfileId { get => _BrokerProfileId; set { if (OnPropertyChanging("BrokerProfileId", value)) { _BrokerProfileId = value; OnPropertyChanged("BrokerProfileId"); } } }

    private String? _Codec;
    /// <summary>数据编解码。MQTT/HTTP上报的载荷格式，如Json/Alink</summary>
    [DisplayName("数据编解码")]
    [Description("数据编解码。MQTT/HTTP上报的载荷格式，如Json/Alink")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Codec", "数据编解码。MQTT/HTTP上报的载荷格式，如Json/Alink", "")]
    public String? Codec { get => _Codec; set { if (OnPropertyChanging("Codec", value)) { _Codec = value; OnPropertyChanged("Codec"); } } }

    private String? _DriverName;
    /// <summary>驱动名称。Driver协议时使用的NewLife.IoT驱动，如ModbusTcp/Simulator</summary>
    [DisplayName("驱动名称")]
    [Description("驱动名称。Driver协议时使用的NewLife.IoT驱动，如ModbusTcp/Simulator")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("DriverName", "驱动名称。Driver协议时使用的NewLife.IoT驱动，如ModbusTcp/Simulator", "")]
    public String? DriverName { get => _DriverName; set { if (OnPropertyChanging("DriverName", value)) { _DriverName = value; OnPropertyChanged("DriverName"); } } }

    private String? _DriverParameter;
    /// <summary>驱动参数。Json格式的默认驱动参数，设备可覆盖</summary>
    [DisplayName("驱动参数")]
    [Description("驱动参数。Json格式的默认驱动参数，设备可覆盖")]
    [DataObjectField(false, false, true, 2000)]
    [BindColumn("DriverParameter", "驱动参数。Json格式的默认驱动参数，设备可覆盖", "")]
    public String? DriverParameter { get => _DriverParameter; set { if (OnPropertyChanging("DriverParameter", value)) { _DriverParameter = value; OnPropertyChanged("DriverParameter"); } } }

    private Int32 _PollInterval;
    /// <summary>采集间隔。驱动轮询间隔，单位毫秒</summary>
    [DisplayName("采集间隔")]
    [Description("采集间隔。驱动轮询间隔，单位毫秒")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("PollInterval", "采集间隔。驱动轮询间隔，单位毫秒", "")]
    public Int32 PollInterval { get => _PollInterval; set { if (OnPropertyChanging("PollInterval", value)) { _PollInterval = value; OnPropertyChanged("PollInterval"); } } }

    private Int32 _OnlineTimeout;
    /// <summary>离线超时。超过该时间无心跳判定离线，单位秒，0表示使用全局配置</summary>
    [DisplayName("离线超时")]
    [Description("离线超时。超过该时间无心跳判定离线，单位秒，0表示使用全局配置")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("OnlineTimeout", "离线超时。超过该时间无心跳判定离线，单位秒，0表示使用全局配置", "")]
    public Int32 OnlineTimeout { get => _OnlineTimeout; set { if (OnPropertyChanging("OnlineTimeout", value)) { _OnlineTimeout = value; OnPropertyChanged("OnlineTimeout"); } } }

    private Boolean _Enable;
    /// <summary>启用</summary>
    [DisplayName("启用")]
    [Description("启用")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Enable", "启用", "")]
    public Boolean Enable { get => _Enable; set { if (OnPropertyChanging("Enable", value)) { _Enable = value; OnPropertyChanged("Enable"); } } }

    private Int32 _DeviceCount;
    /// <summary>设备数</summary>
    [DisplayName("设备数")]
    [Description("设备数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("DeviceCount", "设备数", "")]
    public Int32 DeviceCount { get => _DeviceCount; set { if (OnPropertyChanging("DeviceCount", value)) { _DeviceCount = value; OnPropertyChanged("DeviceCount"); } } }

    private String? _FunctionBlocks;
    /// <summary>自定义模块。Json数组，元素为{functionBlockId,functionBlockName}。空表示仅默认模块</summary>
    [DisplayName("自定义模块")]
    [Description("自定义模块。Json数组，元素为{functionBlockId,functionBlockName}。空表示仅默认模块")]
    [DataObjectField(false, false, true, 4000)]
    [BindColumn("FunctionBlocks", "自定义模块。Json数组，元素为{functionBlockId,functionBlockName}。空表示仅默认模块", "")]
    public String? FunctionBlocks { get => _FunctionBlocks; set { if (OnPropertyChanging("FunctionBlocks", value)) { _FunctionBlocks = value; OnPropertyChanged("FunctionBlocks"); } } }

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
            "Code" => _Code,
            "Category" => _Category,
            "Protocol" => _Protocol,
            "BrokerProfileId" => _BrokerProfileId,
            "Codec" => _Codec,
            "DriverName" => _DriverName,
            "DriverParameter" => _DriverParameter,
            "PollInterval" => _PollInterval,
            "OnlineTimeout" => _OnlineTimeout,
            "Enable" => _Enable,
            "DeviceCount" => _DeviceCount,
            "FunctionBlocks" => _FunctionBlocks,
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
                case "Code": _Code = Convert.ToString(value); break;
                case "Category": _Category = Convert.ToString(value); break;
                case "Protocol": _Protocol = Convert.ToString(value); break;
                case "BrokerProfileId": _BrokerProfileId = Convert.ToString(value); break;
                case "Codec": _Codec = Convert.ToString(value); break;
                case "DriverName": _DriverName = Convert.ToString(value); break;
                case "DriverParameter": _DriverParameter = Convert.ToString(value); break;
                case "PollInterval": _PollInterval = value.ToInt(); break;
                case "OnlineTimeout": _OnlineTimeout = value.ToInt(); break;
                case "Enable": _Enable = value.ToBoolean(); break;
                case "DeviceCount": _DeviceCount = value.ToInt(); break;
                case "FunctionBlocks": _FunctionBlocks = Convert.ToString(value); break;
                case "CreateTime": _CreateTime = value.ToDateTime(); break;
                case "UpdateTime": _UpdateTime = value.ToDateTime(); break;
                case "Remark": _Remark = Convert.ToString(value); break;
                default: base[name] = value; break;
            }
        }
    }
    #endregion

    #region 关联映射
    #endregion

    #region 扩展查询
    /// <summary>根据编号查找</summary>
    /// <param name="id">编号</param>
    /// <returns>实体对象</returns>
    public static Product? FindById(Int32 id)
    {
        if (id < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }

    /// <summary>根据编码查找</summary>
    /// <param name="code">编码</param>
    /// <returns>实体对象</returns>
    public static Product? FindByCode(String code)
    {
        if (code.IsNullOrEmpty()) return null;

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.Code.EqualIgnoreCase(code));

        return Find(_.Code == code);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="code">编码。ProductKey，全局唯一，用于MQTT主题</param>
    /// <param name="enable">启用</param>
    /// <param name="start">更新时间开始</param>
    /// <param name="end">更新时间结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<Product> Search(String code, Boolean? enable, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (!code.IsNullOrEmpty()) exp &= _.Code == code;
        if (enable != null) exp &= _.Enable == enable;
        exp &= _.UpdateTime.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    /// <summary>取得产品字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>名称</summary>
        public static readonly Field Name = FindByName("Name");

        /// <summary>编码。ProductKey，全局唯一，用于MQTT主题</summary>
        public static readonly Field Code = FindByName("Code");

        /// <summary>类别</summary>
        public static readonly Field Category = FindByName("Category");

        /// <summary>接入协议。MQTT/HTTP/Driver</summary>
        public static readonly Field Protocol = FindByName("Protocol");

        /// <summary>默认 MQTT 服务器。空表示使用平台默认（Mode/ActiveBroker）</summary>
        public static readonly Field BrokerProfileId = FindByName("BrokerProfileId");

        /// <summary>数据编解码。MQTT/HTTP上报的载荷格式，如Json/Alink</summary>
        public static readonly Field Codec = FindByName("Codec");

        /// <summary>驱动名称。Driver协议时使用的NewLife.IoT驱动，如ModbusTcp/Simulator</summary>
        public static readonly Field DriverName = FindByName("DriverName");

        /// <summary>驱动参数。Json格式的默认驱动参数，设备可覆盖</summary>
        public static readonly Field DriverParameter = FindByName("DriverParameter");

        /// <summary>采集间隔。驱动轮询间隔，单位毫秒</summary>
        public static readonly Field PollInterval = FindByName("PollInterval");

        /// <summary>离线超时。超过该时间无心跳判定离线，单位秒，0表示使用全局配置</summary>
        public static readonly Field OnlineTimeout = FindByName("OnlineTimeout");

        /// <summary>启用</summary>
        public static readonly Field Enable = FindByName("Enable");

        /// <summary>设备数</summary>
        public static readonly Field DeviceCount = FindByName("DeviceCount");

        /// <summary>自定义模块。Json数组，元素为{functionBlockId,functionBlockName}。空表示仅默认模块</summary>
        public static readonly Field FunctionBlocks = FindByName("FunctionBlocks");

        /// <summary>创建时间</summary>
        public static readonly Field CreateTime = FindByName("CreateTime");

        /// <summary>更新时间</summary>
        public static readonly Field UpdateTime = FindByName("UpdateTime");

        /// <summary>备注</summary>
        public static readonly Field Remark = FindByName("Remark");

        static Field FindByName(String name) => Meta.Table.FindByName(name)!;
    }

    /// <summary>取得产品字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>编号</summary>
        public const String Id = "Id";

        /// <summary>名称</summary>
        public const String Name = "Name";

        /// <summary>编码。ProductKey，全局唯一，用于MQTT主题</summary>
        public const String Code = "Code";

        /// <summary>类别</summary>
        public const String Category = "Category";

        /// <summary>接入协议。MQTT/HTTP/Driver</summary>
        public const String Protocol = "Protocol";

        /// <summary>默认 MQTT 服务器。空表示使用平台默认（Mode/ActiveBroker）</summary>
        public const String BrokerProfileId = "BrokerProfileId";

        /// <summary>数据编解码。MQTT/HTTP上报的载荷格式，如Json/Alink</summary>
        public const String Codec = "Codec";

        /// <summary>驱动名称。Driver协议时使用的NewLife.IoT驱动，如ModbusTcp/Simulator</summary>
        public const String DriverName = "DriverName";

        /// <summary>驱动参数。Json格式的默认驱动参数，设备可覆盖</summary>
        public const String DriverParameter = "DriverParameter";

        /// <summary>采集间隔。驱动轮询间隔，单位毫秒</summary>
        public const String PollInterval = "PollInterval";

        /// <summary>离线超时。超过该时间无心跳判定离线，单位秒，0表示使用全局配置</summary>
        public const String OnlineTimeout = "OnlineTimeout";

        /// <summary>启用</summary>
        public const String Enable = "Enable";

        /// <summary>设备数</summary>
        public const String DeviceCount = "DeviceCount";

        /// <summary>自定义模块。Json数组，元素为{functionBlockId,functionBlockName}。空表示仅默认模块</summary>
        public const String FunctionBlocks = "FunctionBlocks";

        /// <summary>创建时间</summary>
        public const String CreateTime = "CreateTime";

        /// <summary>更新时间</summary>
        public const String UpdateTime = "UpdateTime";

        /// <summary>备注</summary>
        public const String Remark = "Remark";
    }
    #endregion
}
