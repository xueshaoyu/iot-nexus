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

/// <summary>设备属性。设备影子，保存每个属性的最新值</summary>
[Serializable]
[DataObject]
[Description("设备属性。设备影子，保存每个属性的最新值")]
[BindIndex("IU_DeviceProperty_DeviceId_Name", true, "DeviceId,Name")]
[BindTable("DeviceProperty", Description = "设备属性。设备影子，保存每个属性的最新值", ConnName = "IoT", DbType = DatabaseType.None)]
public partial class DeviceProperty
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

    private String _Name = null!;
    /// <summary>标识符</summary>
    [DisplayName("标识符")]
    [Description("标识符")]
    [DataObjectField(false, false, false, 50)]
    [BindColumn("Name", "标识符", "")]
    public String Name { get => _Name; set { if (OnPropertyChanging("Name", value)) { _Name = value; OnPropertyChanged("Name"); } } }

    private String? _NickName;
    /// <summary>名称</summary>
    [DisplayName("名称")]
    [Description("名称")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("NickName", "名称", "", Master = true)]
    public String? NickName { get => _NickName; set { if (OnPropertyChanging("NickName", value)) { _NickName = value; OnPropertyChanged("NickName"); } } }

    private String? _DataType;
    /// <summary>数据类型</summary>
    [DisplayName("数据类型")]
    [Description("数据类型")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("DataType", "数据类型", "")]
    public String? DataType { get => _DataType; set { if (OnPropertyChanging("DataType", value)) { _DataType = value; OnPropertyChanged("DataType"); } } }

    private String? _Value;
    /// <summary>数值</summary>
    [DisplayName("数值")]
    [Description("数值")]
    [DataObjectField(false, false, true, 2000)]
    [BindColumn("Value", "数值", "")]
    public String? Value { get => _Value; set { if (OnPropertyChanging("Value", value)) { _Value = value; OnPropertyChanged("Value"); } } }

    private String? _Unit;
    /// <summary>单位</summary>
    [DisplayName("单位")]
    [Description("单位")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Unit", "单位", "")]
    public String? Unit { get => _Unit; set { if (OnPropertyChanging("Unit", value)) { _Unit = value; OnPropertyChanged("Unit"); } } }

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
            "Name" => _Name,
            "NickName" => _NickName,
            "DataType" => _DataType,
            "Value" => _Value,
            "Unit" => _Unit,
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
                case "Name": _Name = Convert.ToString(value); break;
                case "NickName": _NickName = Convert.ToString(value); break;
                case "DataType": _DataType = Convert.ToString(value); break;
                case "Value": _Value = Convert.ToString(value); break;
                case "Unit": _Unit = Convert.ToString(value); break;
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
    public static DeviceProperty? FindById(Int32 id)
    {
        if (id < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }

    /// <summary>根据设备、标识符查找</summary>
    /// <param name="deviceId">设备</param>
    /// <param name="name">标识符</param>
    /// <returns>实体对象</returns>
    public static DeviceProperty? FindByDeviceIdAndName(Int32 deviceId, String name)
    {
        if (deviceId < 0) return null;
        if (name.IsNullOrEmpty()) return null;

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.DeviceId == deviceId && e.Name.EqualIgnoreCase(name));

        return Find(_.DeviceId == deviceId & _.Name == name);
    }

    /// <summary>根据设备查找</summary>
    /// <param name="deviceId">设备</param>
    /// <returns>实体列表</returns>
    public static IList<DeviceProperty> FindAllByDeviceId(Int32 deviceId)
    {
        if (deviceId < 0) return [];

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.FindAll(e => e.DeviceId == deviceId);

        return FindAll(_.DeviceId == deviceId);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="deviceId">设备</param>
    /// <param name="name">标识符</param>
    /// <param name="start">更新时间开始</param>
    /// <param name="end">更新时间结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<DeviceProperty> Search(Int32 deviceId, String name, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (deviceId >= 0) exp &= _.DeviceId == deviceId;
        if (!name.IsNullOrEmpty()) exp &= _.Name == name;
        exp &= _.UpdateTime.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    /// <summary>取得设备属性字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>设备</summary>
        public static readonly Field DeviceId = FindByName("DeviceId");

        /// <summary>标识符</summary>
        public static readonly Field Name = FindByName("Name");

        /// <summary>名称</summary>
        public static readonly Field NickName = FindByName("NickName");

        /// <summary>数据类型</summary>
        public static readonly Field DataType = FindByName("DataType");

        /// <summary>数值</summary>
        public static readonly Field Value = FindByName("Value");

        /// <summary>单位</summary>
        public static readonly Field Unit = FindByName("Unit");

        /// <summary>创建时间</summary>
        public static readonly Field CreateTime = FindByName("CreateTime");

        /// <summary>更新时间</summary>
        public static readonly Field UpdateTime = FindByName("UpdateTime");

        static Field FindByName(String name) => Meta.Table.FindByName(name)!;
    }

    /// <summary>取得设备属性字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>编号</summary>
        public const String Id = "Id";

        /// <summary>设备</summary>
        public const String DeviceId = "DeviceId";

        /// <summary>标识符</summary>
        public const String Name = "Name";

        /// <summary>名称</summary>
        public const String NickName = "NickName";

        /// <summary>数据类型</summary>
        public const String DataType = "DataType";

        /// <summary>数值</summary>
        public const String Value = "Value";

        /// <summary>单位</summary>
        public const String Unit = "Unit";

        /// <summary>创建时间</summary>
        public const String CreateTime = "CreateTime";

        /// <summary>更新时间</summary>
        public const String UpdateTime = "UpdateTime";
    }
    #endregion
}
