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

/// <summary>服务调用。平台下发给设备的服务调用与属性设置记录</summary>
[Serializable]
[DataObject]
[Description("服务调用。平台下发给设备的服务调用与属性设置记录")]
[BindIndex("IX_ServiceRecord_DeviceId_Id", false, "DeviceId,Id")]
[BindTable("ServiceRecord", Description = "服务调用。平台下发给设备的服务调用与属性设置记录", ConnName = "IoT", DbType = DatabaseType.None)]
public partial class ServiceRecord
{
    #region 属性
    private Int64 _Id;
    /// <summary>编号</summary>
    [DisplayName("编号")]
    [Description("编号")]
    [DataObjectField(true, false, false, 0)]
    [BindColumn("Id", "编号", "", DataScale = "time")]
    public Int64 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private Int32 _DeviceId;
    /// <summary>设备</summary>
    [DisplayName("设备")]
    [Description("设备")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("DeviceId", "设备", "")]
    public Int32 DeviceId { get => _DeviceId; set { if (OnPropertyChanging("DeviceId", value)) { _DeviceId = value; OnPropertyChanged("DeviceId"); } } }

    private String? _Kind;
    /// <summary>种类。service服务调用，property属性设置</summary>
    [DisplayName("种类")]
    [Description("种类。service服务调用，property属性设置")]
    [DataObjectField(false, false, true, 20)]
    [BindColumn("Kind", "种类。service服务调用，property属性设置", "")]
    public String? Kind { get => _Kind; set { if (OnPropertyChanging("Kind", value)) { _Kind = value; OnPropertyChanged("Kind"); } } }

    private String? _Name;
    /// <summary>名称。服务标识符</summary>
    [DisplayName("名称")]
    [Description("名称。服务标识符")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Name", "名称。服务标识符", "", Master = true)]
    public String? Name { get => _Name; set { if (OnPropertyChanging("Name", value)) { _Name = value; OnPropertyChanged("Name"); } } }

    private String? _InputData;
    /// <summary>输入</summary>
    [DisplayName("输入")]
    [Description("输入")]
    [DataObjectField(false, false, true, 2000)]
    [BindColumn("InputData", "输入", "")]
    public String? InputData { get => _InputData; set { if (OnPropertyChanging("InputData", value)) { _InputData = value; OnPropertyChanged("InputData"); } } }

    private IoTPlatform.Data.ServiceStatusKinds _Status;
    /// <summary>状态</summary>
    [DisplayName("状态")]
    [Description("状态")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Status", "状态", "")]
    public IoTPlatform.Data.ServiceStatusKinds Status { get => _Status; set { if (OnPropertyChanging("Status", value)) { _Status = value; OnPropertyChanged("Status"); } } }

    private String? _OutputData;
    /// <summary>输出</summary>
    [DisplayName("输出")]
    [Description("输出")]
    [DataObjectField(false, false, true, 2000)]
    [BindColumn("OutputData", "输出", "")]
    public String? OutputData { get => _OutputData; set { if (OnPropertyChanging("OutputData", value)) { _OutputData = value; OnPropertyChanged("OutputData"); } } }

    private String? _Channel;
    /// <summary>通道。MQTT/Driver</summary>
    [DisplayName("通道")]
    [Description("通道。MQTT/Driver")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Channel", "通道。MQTT/Driver", "")]
    public String? Channel { get => _Channel; set { if (OnPropertyChanging("Channel", value)) { _Channel = value; OnPropertyChanged("Channel"); } } }

    private String? _Source;
    /// <summary>来源。Api/Rule等</summary>
    [DisplayName("来源")]
    [Description("来源。Api/Rule等")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Source", "来源。Api/Rule等", "")]
    public String? Source { get => _Source; set { if (OnPropertyChanging("Source", value)) { _Source = value; OnPropertyChanged("Source"); } } }

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
            "DeviceId" => _DeviceId,
            "Kind" => _Kind,
            "Name" => _Name,
            "InputData" => _InputData,
            "Status" => _Status,
            "OutputData" => _OutputData,
            "Channel" => _Channel,
            "Source" => _Source,
            "CreateTime" => _CreateTime,
            "UpdateTime" => _UpdateTime,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToLong(); break;
                case "DeviceId": _DeviceId = value.ToInt(); break;
                case "Kind": _Kind = Convert.ToString(value); break;
                case "Name": _Name = Convert.ToString(value); break;
                case "InputData": _InputData = Convert.ToString(value); break;
                case "Status": _Status = (IoTPlatform.Data.ServiceStatusKinds)value.ToInt(); break;
                case "OutputData": _OutputData = Convert.ToString(value); break;
                case "Channel": _Channel = Convert.ToString(value); break;
                case "Source": _Source = Convert.ToString(value); break;
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

    #endregion

    #region 扩展查询
    /// <summary>根据编号查找</summary>
    /// <param name="id">编号</param>
    /// <returns>实体对象</returns>
    public static ServiceRecord? FindById(Int64 id)
    {
        if (id < 0) return null;

        return Find(_.Id == id);
    }

    /// <summary>根据设备查找</summary>
    /// <param name="deviceId">设备</param>
    /// <returns>实体列表</returns>
    public static IList<ServiceRecord> FindAllByDeviceId(Int32 deviceId)
    {
        if (deviceId < 0) return [];

        return FindAll(_.DeviceId == deviceId);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="deviceId">设备</param>
    /// <param name="status">状态</param>
    /// <param name="start">编号开始</param>
    /// <param name="end">编号结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<ServiceRecord> Search(Int32 deviceId, IoTPlatform.Data.ServiceStatusKinds status, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (deviceId >= 0) exp &= _.DeviceId == deviceId;
        if (status >= 0) exp &= _.Status == status;
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
    /// <summary>取得服务调用字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>设备</summary>
        public static readonly Field DeviceId = FindByName("DeviceId");

        /// <summary>种类。service服务调用，property属性设置</summary>
        public static readonly Field Kind = FindByName("Kind");

        /// <summary>名称。服务标识符</summary>
        public static readonly Field Name = FindByName("Name");

        /// <summary>输入</summary>
        public static readonly Field InputData = FindByName("InputData");

        /// <summary>状态</summary>
        public static readonly Field Status = FindByName("Status");

        /// <summary>输出</summary>
        public static readonly Field OutputData = FindByName("OutputData");

        /// <summary>通道。MQTT/Driver</summary>
        public static readonly Field Channel = FindByName("Channel");

        /// <summary>来源。Api/Rule等</summary>
        public static readonly Field Source = FindByName("Source");

        /// <summary>创建时间</summary>
        public static readonly Field CreateTime = FindByName("CreateTime");

        /// <summary>更新时间</summary>
        public static readonly Field UpdateTime = FindByName("UpdateTime");

        static Field FindByName(String name) => Meta.Table.FindByName(name)!;
    }

    /// <summary>取得服务调用字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>编号</summary>
        public const String Id = "Id";

        /// <summary>设备</summary>
        public const String DeviceId = "DeviceId";

        /// <summary>种类。service服务调用，property属性设置</summary>
        public const String Kind = "Kind";

        /// <summary>名称。服务标识符</summary>
        public const String Name = "Name";

        /// <summary>输入</summary>
        public const String InputData = "InputData";

        /// <summary>状态</summary>
        public const String Status = "Status";

        /// <summary>输出</summary>
        public const String OutputData = "OutputData";

        /// <summary>通道。MQTT/Driver</summary>
        public const String Channel = "Channel";

        /// <summary>来源。Api/Rule等</summary>
        public const String Source = "Source";

        /// <summary>创建时间</summary>
        public const String CreateTime = "CreateTime";

        /// <summary>更新时间</summary>
        public const String UpdateTime = "UpdateTime";
    }
    #endregion
}
