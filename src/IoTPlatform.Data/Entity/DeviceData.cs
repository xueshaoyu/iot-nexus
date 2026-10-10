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

/// <summary>设备数据。属性上报的历史时序数据</summary>
[Serializable]
[DataObject]
[Description("设备数据。属性上报的历史时序数据")]
[BindIndex("IX_DeviceData_DeviceId_Id", false, "DeviceId,Id")]
[BindIndex("IX_DeviceData_DeviceId_Name_Id", false, "DeviceId,Name,Id")]
[BindTable("DeviceData", Description = "设备数据。属性上报的历史时序数据", ConnName = "IoT", DbType = DatabaseType.None)]
public partial class DeviceData
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

    private String _Name = null!;
    /// <summary>标识符</summary>
    [DisplayName("标识符")]
    [Description("标识符")]
    [DataObjectField(false, false, false, 50)]
    [BindColumn("Name", "标识符", "", Master = true)]
    public String Name { get => _Name; set { if (OnPropertyChanging("Name", value)) { _Name = value; OnPropertyChanged("Name"); } } }

    private String? _Value;
    /// <summary>数值</summary>
    [DisplayName("数值")]
    [Description("数值")]
    [DataObjectField(false, false, true, 2000)]
    [BindColumn("Value", "数值", "")]
    public String? Value { get => _Value; set { if (OnPropertyChanging("Value", value)) { _Value = value; OnPropertyChanged("Value"); } } }

    private Int64 _Timestamp;
    /// <summary>时间戳。设备端采集时间，Unix毫秒</summary>
    [DisplayName("时间戳")]
    [Description("时间戳。设备端采集时间，Unix毫秒")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Timestamp", "时间戳。设备端采集时间，Unix毫秒", "")]
    public Int64 Timestamp { get => _Timestamp; set { if (OnPropertyChanging("Timestamp", value)) { _Timestamp = value; OnPropertyChanged("Timestamp"); } } }

    private DateTime _CreateTime;
    /// <summary>创建时间</summary>
    [DisplayName("创建时间")]
    [Description("创建时间")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("CreateTime", "创建时间", "")]
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
            "DeviceId" => _DeviceId,
            "Name" => _Name,
            "Value" => _Value,
            "Timestamp" => _Timestamp,
            "CreateTime" => _CreateTime,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToLong(); break;
                case "DeviceId": _DeviceId = value.ToInt(); break;
                case "Name": _Name = Convert.ToString(value); break;
                case "Value": _Value = Convert.ToString(value); break;
                case "Timestamp": _Timestamp = value.ToLong(); break;
                case "CreateTime": _CreateTime = value.ToDateTime(); break;
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
    public static DeviceData? FindById(Int64 id)
    {
        if (id < 0) return null;

        return Find(_.Id == id);
    }

    /// <summary>根据设备查找</summary>
    /// <param name="deviceId">设备</param>
    /// <returns>实体列表</returns>
    public static IList<DeviceData> FindAllByDeviceId(Int32 deviceId)
    {
        if (deviceId < 0) return [];

        return FindAll(_.DeviceId == deviceId);
    }

    /// <summary>根据设备、标识符查找</summary>
    /// <param name="deviceId">设备</param>
    /// <param name="name">标识符</param>
    /// <returns>实体列表</returns>
    public static IList<DeviceData> FindAllByDeviceIdAndName(Int32 deviceId, String name)
    {
        if (deviceId < 0) return [];
        if (name.IsNullOrEmpty()) return [];

        return FindAll(_.DeviceId == deviceId & _.Name == name);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="deviceId">设备</param>
    /// <param name="start">编号开始</param>
    /// <param name="end">编号结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<DeviceData> Search(Int32 deviceId, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (deviceId >= 0) exp &= _.DeviceId == deviceId;
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
    /// <summary>取得设备数据字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>设备</summary>
        public static readonly Field DeviceId = FindByName("DeviceId");

        /// <summary>标识符</summary>
        public static readonly Field Name = FindByName("Name");

        /// <summary>数值</summary>
        public static readonly Field Value = FindByName("Value");

        /// <summary>时间戳。设备端采集时间，Unix毫秒</summary>
        public static readonly Field Timestamp = FindByName("Timestamp");

        /// <summary>创建时间</summary>
        public static readonly Field CreateTime = FindByName("CreateTime");

        static Field FindByName(String name) => Meta.Table.FindByName(name)!;
    }

    /// <summary>取得设备数据字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>编号</summary>
        public const String Id = "Id";

        /// <summary>设备</summary>
        public const String DeviceId = "DeviceId";

        /// <summary>标识符</summary>
        public const String Name = "Name";

        /// <summary>数值</summary>
        public const String Value = "Value";

        /// <summary>时间戳。设备端采集时间，Unix毫秒</summary>
        public const String Timestamp = "Timestamp";

        /// <summary>创建时间</summary>
        public const String CreateTime = "CreateTime";
    }
    #endregion
}
