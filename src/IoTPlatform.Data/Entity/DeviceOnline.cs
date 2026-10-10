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

/// <summary>设备在线。设备当前连接会话，下线时删除</summary>
[Serializable]
[DataObject]
[Description("设备在线。设备当前连接会话，下线时删除")]
[BindIndex("IU_DeviceOnline_DeviceId", true, "DeviceId")]
[BindIndex("IX_DeviceOnline_SessionId", false, "SessionId")]
[BindTable("DeviceOnline", Description = "设备在线。设备当前连接会话，下线时删除", ConnName = "IoT", DbType = DatabaseType.None)]
public partial class DeviceOnline
{
    #region 属性
    private Int32 _Id;
    /// <summary>编号</summary>
    [DisplayName("编号")]
    [Description("编号")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "编号", "")]
    public Int32 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private Int32 _DeviceId;
    /// <summary>设备</summary>
    [DisplayName("设备")]
    [Description("设备")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("DeviceId", "设备", "")]
    public Int32 DeviceId { get => _DeviceId; set { if (OnPropertyChanging("DeviceId", value)) { _DeviceId = value; OnPropertyChanged("DeviceId"); } } }

    private Int32 _ProductId;
    /// <summary>产品</summary>
    [DisplayName("产品")]
    [Description("产品")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ProductId", "产品", "")]
    public Int32 ProductId { get => _ProductId; set { if (OnPropertyChanging("ProductId", value)) { _ProductId = value; OnPropertyChanged("ProductId"); } } }

    private String? _SessionId;
    /// <summary>会话。MQTT会话编号或HTTP/驱动标识</summary>
    [DisplayName("会话")]
    [Description("会话。MQTT会话编号或HTTP/驱动标识")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("SessionId", "会话。MQTT会话编号或HTTP/驱动标识", "")]
    public String? SessionId { get => _SessionId; set { if (OnPropertyChanging("SessionId", value)) { _SessionId = value; OnPropertyChanged("SessionId"); } } }

    private String? _Protocol;
    /// <summary>协议</summary>
    [DisplayName("协议")]
    [Description("协议")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Protocol", "协议", "")]
    public String? Protocol { get => _Protocol; set { if (OnPropertyChanging("Protocol", value)) { _Protocol = value; OnPropertyChanged("Protocol"); } } }

    private String? _ClientId;
    /// <summary>客户端</summary>
    [DisplayName("客户端")]
    [Description("客户端")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("ClientId", "客户端", "")]
    public String? ClientId { get => _ClientId; set { if (OnPropertyChanging("ClientId", value)) { _ClientId = value; OnPropertyChanged("ClientId"); } } }

    private String? _IP;
    /// <summary>IP地址</summary>
    [DisplayName("IP地址")]
    [Description("IP地址")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("IP", "IP地址", "")]
    public String? IP { get => _IP; set { if (OnPropertyChanging("IP", value)) { _IP = value; OnPropertyChanged("IP"); } } }

    private Int32 _Messages;
    /// <summary>消息数。本次会话上行消息数</summary>
    [DisplayName("消息数")]
    [Description("消息数。本次会话上行消息数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Messages", "消息数。本次会话上行消息数", "")]
    public Int32 Messages { get => _Messages; set { if (OnPropertyChanging("Messages", value)) { _Messages = value; OnPropertyChanged("Messages"); } } }

    private DateTime _CreateTime;
    /// <summary>上线时间</summary>
    [DisplayName("上线时间")]
    [Description("上线时间")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("CreateTime", "上线时间", "")]
    public DateTime CreateTime { get => _CreateTime; set { if (OnPropertyChanging("CreateTime", value)) { _CreateTime = value; OnPropertyChanged("CreateTime"); } } }

    private DateTime _UpdateTime;
    /// <summary>活跃时间</summary>
    [DisplayName("活跃时间")]
    [Description("活跃时间")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("UpdateTime", "活跃时间", "")]
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
            "DeviceId" => _DeviceId,
            "ProductId" => _ProductId,
            "SessionId" => _SessionId,
            "Protocol" => _Protocol,
            "ClientId" => _ClientId,
            "IP" => _IP,
            "Messages" => _Messages,
            "CreateTime" => _CreateTime,
            "UpdateTime" => _UpdateTime,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToInt(); break;
                case "DeviceId": _DeviceId = value.ToInt(); break;
                case "ProductId": _ProductId = value.ToInt(); break;
                case "SessionId": _SessionId = Convert.ToString(value); break;
                case "Protocol": _Protocol = Convert.ToString(value); break;
                case "ClientId": _ClientId = Convert.ToString(value); break;
                case "IP": _IP = Convert.ToString(value); break;
                case "Messages": _Messages = value.ToInt(); break;
                case "CreateTime": _CreateTime = value.ToDateTime(); break;
                case "UpdateTime": _UpdateTime = value.ToDateTime(); break;
                default: base[name] = value; break;
            }
        }
    }
    #endregion

    #region 关联映射
    /// <summary>设备</summary>
    [XmlIgnore, IgnoreDataMember, ScriptIgnore]
    public Device? Device => Extends.Get(nameof(Device), k => Device.FindById(DeviceId));

    /// <summary>设备</summary>
    [Map(nameof(DeviceId), typeof(Device), "Id")]
    public String? DeviceName => Device?.Name;

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
    public static DeviceOnline? FindById(Int32 id)
    {
        if (id < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }

    /// <summary>根据设备查找</summary>
    /// <param name="deviceId">设备</param>
    /// <returns>实体对象</returns>
    public static DeviceOnline? FindByDeviceId(Int32 deviceId)
    {
        if (deviceId < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.DeviceId == deviceId);

        return Find(_.DeviceId == deviceId);
    }

    /// <summary>根据会话查找</summary>
    /// <param name="sessionId">会话</param>
    /// <returns>实体列表</returns>
    public static IList<DeviceOnline> FindAllBySessionId(String? sessionId)
    {
        if (sessionId == null) return [];

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.FindAll(e => e.SessionId.EqualIgnoreCase(sessionId));

        return FindAll(_.SessionId == sessionId);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="deviceId">设备</param>
    /// <param name="sessionId">会话。MQTT会话编号或HTTP/驱动标识</param>
    /// <param name="productId">产品</param>
    /// <param name="start">活跃时间开始</param>
    /// <param name="end">活跃时间结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<DeviceOnline> Search(Int32 deviceId, String? sessionId, Int32 productId, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (deviceId >= 0) exp &= _.DeviceId == deviceId;
        if (!sessionId.IsNullOrEmpty()) exp &= _.SessionId == sessionId;
        if (productId >= 0) exp &= _.ProductId == productId;
        exp &= _.UpdateTime.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    /// <summary>取得设备在线字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>设备</summary>
        public static readonly Field DeviceId = FindByName("DeviceId");

        /// <summary>产品</summary>
        public static readonly Field ProductId = FindByName("ProductId");

        /// <summary>会话。MQTT会话编号或HTTP/驱动标识</summary>
        public static readonly Field SessionId = FindByName("SessionId");

        /// <summary>协议</summary>
        public static readonly Field Protocol = FindByName("Protocol");

        /// <summary>客户端</summary>
        public static readonly Field ClientId = FindByName("ClientId");

        /// <summary>IP地址</summary>
        public static readonly Field IP = FindByName("IP");

        /// <summary>消息数。本次会话上行消息数</summary>
        public static readonly Field Messages = FindByName("Messages");

        /// <summary>上线时间</summary>
        public static readonly Field CreateTime = FindByName("CreateTime");

        /// <summary>活跃时间</summary>
        public static readonly Field UpdateTime = FindByName("UpdateTime");

        static Field FindByName(String name) => Meta.Table.FindByName(name)!;
    }

    /// <summary>取得设备在线字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>编号</summary>
        public const String Id = "Id";

        /// <summary>设备</summary>
        public const String DeviceId = "DeviceId";

        /// <summary>产品</summary>
        public const String ProductId = "ProductId";

        /// <summary>会话。MQTT会话编号或HTTP/驱动标识</summary>
        public const String SessionId = "SessionId";

        /// <summary>协议</summary>
        public const String Protocol = "Protocol";

        /// <summary>客户端</summary>
        public const String ClientId = "ClientId";

        /// <summary>IP地址</summary>
        public const String IP = "IP";

        /// <summary>消息数。本次会话上行消息数</summary>
        public const String Messages = "Messages";

        /// <summary>上线时间</summary>
        public const String CreateTime = "CreateTime";

        /// <summary>活跃时间</summary>
        public const String UpdateTime = "UpdateTime";
    }
    #endregion
}
