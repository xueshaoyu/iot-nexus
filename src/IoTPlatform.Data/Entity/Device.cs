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

/// <summary>设备。接入平台的物理或逻辑设备</summary>
[Serializable]
[DataObject]
[Description("设备。接入平台的物理或逻辑设备")]
[BindIndex("IU_Device_Code", true, "Code")]
[BindIndex("IX_Device_ProductId", false, "ProductId")]
[BindIndex("IX_Device_GroupId", false, "GroupId")]
[BindIndex("IX_Device_UpdateTime", false, "UpdateTime")]
[BindTable("Device", Description = "设备。接入平台的物理或逻辑设备", ConnName = "IoT", DbType = DatabaseType.None)]
public partial class Device
{
    #region 属性
    private Int32 _Id;
    /// <summary>编号</summary>
    [DisplayName("编号")]
    [Description("编号")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "编号", "")]
    public Int32 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private String? _Name;
    /// <summary>名称</summary>
    [DisplayName("名称")]
    [Description("名称")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Name", "名称", "", Master = true)]
    public String? Name { get => _Name; set { if (OnPropertyChanging("Name", value)) { _Name = value; OnPropertyChanged("Name"); } } }

    private String _Code = null!;
    /// <summary>编码。DeviceName，全局唯一，用于MQTT认证与主题</summary>
    [DisplayName("编码")]
    [Description("编码。DeviceName，全局唯一，用于MQTT认证与主题")]
    [DataObjectField(false, false, false, 50)]
    [BindColumn("Code", "编码。DeviceName，全局唯一，用于MQTT认证与主题", "")]
    public String Code { get => _Code; set { if (OnPropertyChanging("Code", value)) { _Code = value; OnPropertyChanged("Code"); } } }

    private Int32 _ProductId;
    /// <summary>产品</summary>
    [DisplayName("产品")]
    [Description("产品")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ProductId", "产品", "")]
    public Int32 ProductId { get => _ProductId; set { if (OnPropertyChanging("ProductId", value)) { _ProductId = value; OnPropertyChanged("ProductId"); } } }

    private Int32 _GroupId;
    /// <summary>分组</summary>
    [DisplayName("分组")]
    [Description("分组")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("GroupId", "分组", "")]
    public Int32 GroupId { get => _GroupId; set { if (OnPropertyChanging("GroupId", value)) { _GroupId = value; OnPropertyChanged("GroupId"); } } }

    private String? _BrokerProfileId;
    /// <summary>MQTT 服务器。空表示使用产品默认，再回退平台默认</summary>
    [DisplayName("MQTT服务器")]
    [Description("MQTT 服务器。空表示使用产品默认，再回退平台默认")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("BrokerProfileId", "MQTT 服务器。空表示使用产品默认，再回退平台默认", "")]
    public String? BrokerProfileId { get => _BrokerProfileId; set { if (OnPropertyChanging("BrokerProfileId", value)) { _BrokerProfileId = value; OnPropertyChanged("BrokerProfileId"); } } }

    private String? _Secret;
    /// <summary>密钥。设备接入认证密钥</summary>
    [DisplayName("密钥")]
    [Description("密钥。设备接入认证密钥")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Secret", "密钥。设备接入认证密钥", "")]
    public String? Secret { get => _Secret; set { if (OnPropertyChanging("Secret", value)) { _Secret = value; OnPropertyChanged("Secret"); } } }

    private Boolean _Enable;
    /// <summary>启用</summary>
    [DisplayName("启用")]
    [Description("启用")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Enable", "启用", "")]
    public Boolean Enable { get => _Enable; set { if (OnPropertyChanging("Enable", value)) { _Enable = value; OnPropertyChanged("Enable"); } } }

    private Boolean _Online;
    /// <summary>在线</summary>
    [DisplayName("在线")]
    [Description("在线")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Online", "在线", "")]
    public Boolean Online { get => _Online; set { if (OnPropertyChanging("Online", value)) { _Online = value; OnPropertyChanged("Online"); } } }

    private String? _IP;
    /// <summary>IP地址</summary>
    [DisplayName("IP地址")]
    [Description("IP地址")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("IP", "IP地址", "")]
    public String? IP { get => _IP; set { if (OnPropertyChanging("IP", value)) { _IP = value; OnPropertyChanged("IP"); } } }

    private String? _Version;
    /// <summary>固件版本</summary>
    [DisplayName("固件版本")]
    [Description("固件版本")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Version", "固件版本", "")]
    public String? Version { get => _Version; set { if (OnPropertyChanging("Version", value)) { _Version = value; OnPropertyChanged("Version"); } } }

    private String? _Location;
    /// <summary>位置</summary>
    [DisplayName("位置")]
    [Description("位置")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Location", "位置", "")]
    public String? Location { get => _Location; set { if (OnPropertyChanging("Location", value)) { _Location = value; OnPropertyChanged("Location"); } } }

    private String? _DriverParameter;
    /// <summary>驱动参数。Json格式，覆盖产品默认参数</summary>
    [DisplayName("驱动参数")]
    [Description("驱动参数。Json格式，覆盖产品默认参数")]
    [DataObjectField(false, false, true, 2000)]
    [BindColumn("DriverParameter", "驱动参数。Json格式，覆盖产品默认参数", "")]
    public String? DriverParameter { get => _DriverParameter; set { if (OnPropertyChanging("DriverParameter", value)) { _DriverParameter = value; OnPropertyChanged("DriverParameter"); } } }

    private Int32 _PollInterval;
    /// <summary>采集间隔。单位毫秒，0表示使用产品配置</summary>
    [DisplayName("采集间隔")]
    [Description("采集间隔。单位毫秒，0表示使用产品配置")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("PollInterval", "采集间隔。单位毫秒，0表示使用产品配置", "")]
    public Int32 PollInterval { get => _PollInterval; set { if (OnPropertyChanging("PollInterval", value)) { _PollInterval = value; OnPropertyChanged("PollInterval"); } } }

    private Int32 _Logins;
    /// <summary>上线次数</summary>
    [DisplayName("上线次数")]
    [Description("上线次数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Logins", "上线次数", "")]
    public Int32 Logins { get => _Logins; set { if (OnPropertyChanging("Logins", value)) { _Logins = value; OnPropertyChanged("Logins"); } } }

    private Int32 _OnlineTime;
    /// <summary>在线时长。累计在线时间，单位秒</summary>
    [DisplayName("在线时长")]
    [Description("在线时长。累计在线时间，单位秒")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("OnlineTime", "在线时长。累计在线时间，单位秒", "", ItemType = "TimeSpan")]
    public Int32 OnlineTime { get => _OnlineTime; set { if (OnPropertyChanging("OnlineTime", value)) { _OnlineTime = value; OnPropertyChanged("OnlineTime"); } } }

    private Int64 _Messages;
    /// <summary>消息数。累计上行消息数</summary>
    [DisplayName("消息数")]
    [Description("消息数。累计上行消息数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Messages", "消息数。累计上行消息数", "")]
    public Int64 Messages { get => _Messages; set { if (OnPropertyChanging("Messages", value)) { _Messages = value; OnPropertyChanged("Messages"); } } }

    private DateTime _LastOnline;
    /// <summary>最后上线</summary>
    [DisplayName("最后上线")]
    [Description("最后上线")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("LastOnline", "最后上线", "")]
    public DateTime LastOnline { get => _LastOnline; set { if (OnPropertyChanging("LastOnline", value)) { _LastOnline = value; OnPropertyChanged("LastOnline"); } } }

    private DateTime _LastOffline;
    /// <summary>最后下线</summary>
    [DisplayName("最后下线")]
    [Description("最后下线")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("LastOffline", "最后下线", "")]
    public DateTime LastOffline { get => _LastOffline; set { if (OnPropertyChanging("LastOffline", value)) { _LastOffline = value; OnPropertyChanged("LastOffline"); } } }

    private DateTime _LastActive;
    /// <summary>最后活跃。最后一次心跳或数据上报时间</summary>
    [DisplayName("最后活跃")]
    [Description("最后活跃。最后一次心跳或数据上报时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("LastActive", "最后活跃。最后一次心跳或数据上报时间", "")]
    public DateTime LastActive { get => _LastActive; set { if (OnPropertyChanging("LastActive", value)) { _LastActive = value; OnPropertyChanged("LastActive"); } } }

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
            "ProductId" => _ProductId,
            "GroupId" => _GroupId,
            "BrokerProfileId" => _BrokerProfileId,
            "Secret" => _Secret,
            "Enable" => _Enable,
            "Online" => _Online,
            "IP" => _IP,
            "Version" => _Version,
            "Location" => _Location,
            "DriverParameter" => _DriverParameter,
            "PollInterval" => _PollInterval,
            "Logins" => _Logins,
            "OnlineTime" => _OnlineTime,
            "Messages" => _Messages,
            "LastOnline" => _LastOnline,
            "LastOffline" => _LastOffline,
            "LastActive" => _LastActive,
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
                case "ProductId": _ProductId = value.ToInt(); break;
                case "GroupId": _GroupId = value.ToInt(); break;
                case "BrokerProfileId": _BrokerProfileId = Convert.ToString(value); break;
                case "Secret": _Secret = Convert.ToString(value); break;
                case "Enable": _Enable = value.ToBoolean(); break;
                case "Online": _Online = value.ToBoolean(); break;
                case "IP": _IP = Convert.ToString(value); break;
                case "Version": _Version = Convert.ToString(value); break;
                case "Location": _Location = Convert.ToString(value); break;
                case "DriverParameter": _DriverParameter = Convert.ToString(value); break;
                case "PollInterval": _PollInterval = value.ToInt(); break;
                case "Logins": _Logins = value.ToInt(); break;
                case "OnlineTime": _OnlineTime = value.ToInt(); break;
                case "Messages": _Messages = value.ToLong(); break;
                case "LastOnline": _LastOnline = value.ToDateTime(); break;
                case "LastOffline": _LastOffline = value.ToDateTime(); break;
                case "LastActive": _LastActive = value.ToDateTime(); break;
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

    #endregion

    #region 扩展查询
    /// <summary>根据编号查找</summary>
    /// <param name="id">编号</param>
    /// <returns>实体对象</returns>
    public static Device? FindById(Int32 id)
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
    public static Device? FindByCode(String code)
    {
        if (code.IsNullOrEmpty()) return null;

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.Code.EqualIgnoreCase(code));

        return Find(_.Code == code);
    }

    /// <summary>根据产品查找</summary>
    /// <param name="productId">产品</param>
    /// <returns>实体列表</returns>
    public static IList<Device> FindAllByProductId(Int32 productId)
    {
        if (productId < 0) return [];

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.FindAll(e => e.ProductId == productId);

        return FindAll(_.ProductId == productId);
    }

    /// <summary>根据分组查找</summary>
    /// <param name="groupId">分组</param>
    /// <returns>实体列表</returns>
    public static IList<Device> FindAllByGroupId(Int32 groupId)
    {
        if (groupId < 0) return [];

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.FindAll(e => e.GroupId == groupId);

        return FindAll(_.GroupId == groupId);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="code">编码。DeviceName，全局唯一，用于MQTT认证与主题</param>
    /// <param name="productId">产品</param>
    /// <param name="groupId">分组</param>
    /// <param name="online">在线</param>
    /// <param name="enable">启用</param>
    /// <param name="start">更新时间开始</param>
    /// <param name="end">更新时间结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<Device> Search(String code, Int32 productId, Int32 groupId, Boolean? online, Boolean? enable, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (!code.IsNullOrEmpty()) exp &= _.Code == code;
        if (productId >= 0) exp &= _.ProductId == productId;
        if (groupId >= 0) exp &= _.GroupId == groupId;
        if (online != null) exp &= _.Online == online;
        if (enable != null) exp &= _.Enable == enable;
        exp &= _.UpdateTime.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    /// <summary>取得设备字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>名称</summary>
        public static readonly Field Name = FindByName("Name");

        /// <summary>编码。DeviceName，全局唯一，用于MQTT认证与主题</summary>
        public static readonly Field Code = FindByName("Code");

        /// <summary>产品</summary>
        public static readonly Field ProductId = FindByName("ProductId");

        /// <summary>分组</summary>
        public static readonly Field GroupId = FindByName("GroupId");

        /// <summary>MQTT 服务器。空表示使用产品默认，再回退平台默认</summary>
        public static readonly Field BrokerProfileId = FindByName("BrokerProfileId");

        /// <summary>密钥。设备接入认证密钥</summary>
        public static readonly Field Secret = FindByName("Secret");

        /// <summary>启用</summary>
        public static readonly Field Enable = FindByName("Enable");

        /// <summary>在线</summary>
        public static readonly Field Online = FindByName("Online");

        /// <summary>IP地址</summary>
        public static readonly Field IP = FindByName("IP");

        /// <summary>固件版本</summary>
        public static readonly Field Version = FindByName("Version");

        /// <summary>位置</summary>
        public static readonly Field Location = FindByName("Location");

        /// <summary>驱动参数。Json格式，覆盖产品默认参数</summary>
        public static readonly Field DriverParameter = FindByName("DriverParameter");

        /// <summary>采集间隔。单位毫秒，0表示使用产品配置</summary>
        public static readonly Field PollInterval = FindByName("PollInterval");

        /// <summary>上线次数</summary>
        public static readonly Field Logins = FindByName("Logins");

        /// <summary>在线时长。累计在线时间，单位秒</summary>
        public static readonly Field OnlineTime = FindByName("OnlineTime");

        /// <summary>消息数。累计上行消息数</summary>
        public static readonly Field Messages = FindByName("Messages");

        /// <summary>最后上线</summary>
        public static readonly Field LastOnline = FindByName("LastOnline");

        /// <summary>最后下线</summary>
        public static readonly Field LastOffline = FindByName("LastOffline");

        /// <summary>最后活跃。最后一次心跳或数据上报时间</summary>
        public static readonly Field LastActive = FindByName("LastActive");

        /// <summary>创建时间</summary>
        public static readonly Field CreateTime = FindByName("CreateTime");

        /// <summary>更新时间</summary>
        public static readonly Field UpdateTime = FindByName("UpdateTime");

        /// <summary>备注</summary>
        public static readonly Field Remark = FindByName("Remark");

        static Field FindByName(String name) => Meta.Table.FindByName(name)!;
    }

    /// <summary>取得设备字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>编号</summary>
        public const String Id = "Id";

        /// <summary>名称</summary>
        public const String Name = "Name";

        /// <summary>编码。DeviceName，全局唯一，用于MQTT认证与主题</summary>
        public const String Code = "Code";

        /// <summary>产品</summary>
        public const String ProductId = "ProductId";

        /// <summary>分组</summary>
        public const String GroupId = "GroupId";

        /// <summary>MQTT 服务器。空表示使用产品默认，再回退平台默认</summary>
        public const String BrokerProfileId = "BrokerProfileId";

        /// <summary>密钥。设备接入认证密钥</summary>
        public const String Secret = "Secret";

        /// <summary>启用</summary>
        public const String Enable = "Enable";

        /// <summary>在线</summary>
        public const String Online = "Online";

        /// <summary>IP地址</summary>
        public const String IP = "IP";

        /// <summary>固件版本</summary>
        public const String Version = "Version";

        /// <summary>位置</summary>
        public const String Location = "Location";

        /// <summary>驱动参数。Json格式，覆盖产品默认参数</summary>
        public const String DriverParameter = "DriverParameter";

        /// <summary>采集间隔。单位毫秒，0表示使用产品配置</summary>
        public const String PollInterval = "PollInterval";

        /// <summary>上线次数</summary>
        public const String Logins = "Logins";

        /// <summary>在线时长。累计在线时间，单位秒</summary>
        public const String OnlineTime = "OnlineTime";

        /// <summary>消息数。累计上行消息数</summary>
        public const String Messages = "Messages";

        /// <summary>最后上线</summary>
        public const String LastOnline = "LastOnline";

        /// <summary>最后下线</summary>
        public const String LastOffline = "LastOffline";

        /// <summary>最后活跃。最后一次心跳或数据上报时间</summary>
        public const String LastActive = "LastActive";

        /// <summary>创建时间</summary>
        public const String CreateTime = "CreateTime";

        /// <summary>更新时间</summary>
        public const String UpdateTime = "UpdateTime";

        /// <summary>备注</summary>
        public const String Remark = "Remark";
    }
    #endregion
}
