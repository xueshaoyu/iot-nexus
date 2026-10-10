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

/// <summary>产品属性。物模型属性定义，描述设备运行时状态，可附带驱动点位地址</summary>
[Serializable]
[DataObject]
[Description("产品属性。物模型属性定义，描述设备运行时状态，可附带驱动点位地址")]
[BindIndex("IU_ThingProperty_ProductId_FunctionBlockId_Identifier", true, "ProductId,FunctionBlockId,Identifier")]
[BindTable("ThingProperty", Description = "产品属性。物模型属性定义，描述设备运行时状态，可附带驱动点位地址", ConnName = "IoT", DbType = DatabaseType.None)]
public partial class ThingProperty
{
    #region 属性
    private Int32 _Id;
    /// <summary>编号</summary>
    [DisplayName("编号")]
    [Description("编号")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "编号", "")]
    public Int32 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private Int32 _ProductId;
    /// <summary>产品</summary>
    [DisplayName("产品")]
    [Description("产品")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ProductId", "产品", "")]
    public Int32 ProductId { get => _ProductId; set { if (OnPropertyChanging("ProductId", value)) { _ProductId = value; OnPropertyChanged("ProductId"); } } }

    private String _Identifier = null!;
    /// <summary>标识符。属性唯一标识，如temperature</summary>
    [DisplayName("标识符")]
    [Description("标识符。属性唯一标识，如temperature")]
    [DataObjectField(false, false, false, 50)]
    [BindColumn("Identifier", "标识符。属性唯一标识，如temperature", "")]
    public String Identifier { get => _Identifier; set { if (OnPropertyChanging("Identifier", value)) { _Identifier = value; OnPropertyChanged("Identifier"); } } }

    private String? _Name;
    /// <summary>名称</summary>
    [DisplayName("名称")]
    [Description("名称")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Name", "名称", "", Master = true)]
    public String? Name { get => _Name; set { if (OnPropertyChanging("Name", value)) { _Name = value; OnPropertyChanged("Name"); } } }

    private String? _DataType;
    /// <summary>数据类型。int/float/double/text/date/bool/enum/struct/array，兼容历史 string</summary>
    [DisplayName("数据类型")]
    [Description("数据类型。int/float/double/text/date/bool/enum/struct/array，兼容历史 string")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("DataType", "数据类型。int/float/double/text/date/bool/enum/struct/array，兼容历史 string", "")]
    public String? DataType { get => _DataType; set { if (OnPropertyChanging("DataType", value)) { _DataType = value; OnPropertyChanged("DataType"); } } }

    private String? _Unit;
    /// <summary>单位</summary>
    [DisplayName("单位")]
    [Description("单位")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Unit", "单位", "")]
    public String? Unit { get => _Unit; set { if (OnPropertyChanging("Unit", value)) { _Unit = value; OnPropertyChanged("Unit"); } } }

    private String? _UnitName;
    /// <summary>单位名称</summary>
    [DisplayName("单位名称")]
    [Description("单位名称")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("UnitName", "单位名称", "")]
    public String? UnitName { get => _UnitName; set { if (OnPropertyChanging("UnitName", value)) { _UnitName = value; OnPropertyChanged("UnitName"); } } }

    private Double _Min;
    /// <summary>最小值</summary>
    [DisplayName("最小值")]
    [Description("最小值")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Min", "最小值", "")]
    public Double Min { get => _Min; set { if (OnPropertyChanging("Min", value)) { _Min = value; OnPropertyChanged("Min"); } } }

    private Double _Max;
    /// <summary>最大值</summary>
    [DisplayName("最大值")]
    [Description("最大值")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Max", "最大值", "")]
    public Double Max { get => _Max; set { if (OnPropertyChanging("Max", value)) { _Max = value; OnPropertyChanged("Max"); } } }

    private Double _Step;
    /// <summary>步长</summary>
    [DisplayName("步长")]
    [Description("步长")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Step", "步长", "")]
    public Double Step { get => _Step; set { if (OnPropertyChanging("Step", value)) { _Step = value; OnPropertyChanged("Step"); } } }

    private Int32 _Length;
    /// <summary>长度。text 最大长度，或 array 元素个数上限</summary>
    [DisplayName("长度")]
    [Description("长度。text 最大长度，或 array 元素个数上限")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Length", "长度。text 最大长度，或 array 元素个数上限", "")]
    public Int32 Length { get => _Length; set { if (OnPropertyChanging("Length", value)) { _Length = value; OnPropertyChanged("Length"); } } }

    private String? _AccessMode;
    /// <summary>访问模式。r只读，w只写，rw读写</summary>
    [DisplayName("访问模式")]
    [Description("访问模式。r只读，w只写，rw读写")]
    [DataObjectField(false, false, true, 10)]
    [BindColumn("AccessMode", "访问模式。r只读，w只写，rw读写", "")]
    public String? AccessMode { get => _AccessMode; set { if (OnPropertyChanging("AccessMode", value)) { _AccessMode = value; OnPropertyChanged("AccessMode"); } } }

    private Boolean _Required;
    /// <summary>是否必选</summary>
    [DisplayName("是否必选")]
    [Description("是否必选")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Required", "是否必选", "")]
    public Boolean Required { get => _Required; set { if (OnPropertyChanging("Required", value)) { _Required = value; OnPropertyChanged("Required"); } } }

    private String? _Specs;
    /// <summary>数据类型规格。阿里云 TSL dataType.specs 的 Json。struct 为数组，array 含 size 与 item</summary>
    [DisplayName("数据类型规格")]
    [Description("数据类型规格。阿里云 TSL dataType.specs 的 Json。struct 为数组，array 含 size 与 item")]
    [DataObjectField(false, false, true, 4000)]
    [BindColumn("Specs", "数据类型规格。阿里云 TSL dataType.specs 的 Json。struct 为数组，array 含 size 与 item", "")]
    public String? Specs { get => _Specs; set { if (OnPropertyChanging("Specs", value)) { _Specs = value; OnPropertyChanged("Specs"); } } }

    private String? _FunctionBlockId;
    /// <summary>模块标识。空表示默认模块</summary>
    [DisplayName("模块标识")]
    [Description("模块标识。空表示默认模块")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("FunctionBlockId", "模块标识。空表示默认模块", "")]
    public String? FunctionBlockId { get => _FunctionBlockId; set { if (OnPropertyChanging("FunctionBlockId", value)) { _FunctionBlockId = value; OnPropertyChanged("FunctionBlockId"); } } }

    private String? _Address;
    /// <summary>点位地址。驱动采集地址，如Modbus的4x0</summary>
    [DisplayName("点位地址")]
    [Description("点位地址。驱动采集地址，如Modbus的4x0")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Address", "点位地址。驱动采集地址，如Modbus的4x0", "")]
    public String? Address { get => _Address; set { if (OnPropertyChanging("Address", value)) { _Address = value; OnPropertyChanged("Address"); } } }

    private String? _PointType;
    /// <summary>点位类型。驱动采集的原始数据类型，如short/ushort/int/float，空表示与数据类型一致</summary>
    [DisplayName("点位类型")]
    [Description("点位类型。驱动采集的原始数据类型，如short/ushort/int/float，空表示与数据类型一致")]
    [DataObjectField(false, false, true, 20)]
    [BindColumn("PointType", "点位类型。驱动采集的原始数据类型，如short/ushort/int/float，空表示与数据类型一致", "")]
    public String? PointType { get => _PointType; set { if (OnPropertyChanging("PointType", value)) { _PointType = value; OnPropertyChanged("PointType"); } } }

    private Double _Scaling;
    /// <summary>缩放因子。原始值乘以该系数得到工程值，0表示不缩放</summary>
    [DisplayName("缩放因子")]
    [Description("缩放因子。原始值乘以该系数得到工程值，0表示不缩放")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Scaling", "缩放因子。原始值乘以该系数得到工程值，0表示不缩放", "")]
    public Double Scaling { get => _Scaling; set { if (OnPropertyChanging("Scaling", value)) { _Scaling = value; OnPropertyChanged("Scaling"); } } }

    private String? _Mapping;
    /// <summary>枚举或布尔映射。如0=关,1=开，对应 TSL specs 的键值</summary>
    [DisplayName("枚举或布尔映射")]
    [Description("枚举或布尔映射。如0=关,1=开，对应 TSL specs 的键值")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("Mapping", "枚举或布尔映射。如0=关,1=开，对应 TSL specs 的键值", "")]
    public String? Mapping { get => _Mapping; set { if (OnPropertyChanging("Mapping", value)) { _Mapping = value; OnPropertyChanged("Mapping"); } } }

    private Int32 _Sort;
    /// <summary>排序</summary>
    [DisplayName("排序")]
    [Description("排序")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Sort", "排序", "")]
    public Int32 Sort { get => _Sort; set { if (OnPropertyChanging("Sort", value)) { _Sort = value; OnPropertyChanged("Sort"); } } }

    private Boolean _Enable;
    /// <summary>启用</summary>
    [DisplayName("启用")]
    [Description("启用")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Enable", "启用", "")]
    public Boolean Enable { get => _Enable; set { if (OnPropertyChanging("Enable", value)) { _Enable = value; OnPropertyChanged("Enable"); } } }

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
            "ProductId" => _ProductId,
            "Identifier" => _Identifier,
            "Name" => _Name,
            "DataType" => _DataType,
            "Unit" => _Unit,
            "UnitName" => _UnitName,
            "Min" => _Min,
            "Max" => _Max,
            "Step" => _Step,
            "Length" => _Length,
            "AccessMode" => _AccessMode,
            "Required" => _Required,
            "Specs" => _Specs,
            "FunctionBlockId" => _FunctionBlockId,
            "Address" => _Address,
            "PointType" => _PointType,
            "Scaling" => _Scaling,
            "Mapping" => _Mapping,
            "Sort" => _Sort,
            "Enable" => _Enable,
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
                case "ProductId": _ProductId = value.ToInt(); break;
                case "Identifier": _Identifier = Convert.ToString(value); break;
                case "Name": _Name = Convert.ToString(value); break;
                case "DataType": _DataType = Convert.ToString(value); break;
                case "Unit": _Unit = Convert.ToString(value); break;
                case "UnitName": _UnitName = Convert.ToString(value); break;
                case "Min": _Min = value.ToDouble(); break;
                case "Max": _Max = value.ToDouble(); break;
                case "Step": _Step = value.ToDouble(); break;
                case "Length": _Length = value.ToInt(); break;
                case "AccessMode": _AccessMode = Convert.ToString(value); break;
                case "Required": _Required = value.ToBoolean(); break;
                case "Specs": _Specs = Convert.ToString(value); break;
                case "FunctionBlockId": _FunctionBlockId = Convert.ToString(value); break;
                case "Address": _Address = Convert.ToString(value); break;
                case "PointType": _PointType = Convert.ToString(value); break;
                case "Scaling": _Scaling = value.ToDouble(); break;
                case "Mapping": _Mapping = Convert.ToString(value); break;
                case "Sort": _Sort = value.ToInt(); break;
                case "Enable": _Enable = value.ToBoolean(); break;
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

    #endregion

    #region 扩展查询
    /// <summary>根据编号查找</summary>
    /// <param name="id">编号</param>
    /// <returns>实体对象</returns>
    public static ThingProperty? FindById(Int32 id)
    {
        if (id < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }

    /// <summary>根据产品、模块标识、标识符查找</summary>
    /// <param name="productId">产品</param>
    /// <param name="functionBlockId">模块标识</param>
    /// <param name="identifier">标识符</param>
    /// <returns>实体对象</returns>
    public static ThingProperty? FindByProductIdAndFunctionBlockIdAndIdentifier(Int32 productId, String? functionBlockId, String identifier)
    {
        if (productId < 0) return null;
        if (functionBlockId == null) return null;
        if (identifier.IsNullOrEmpty()) return null;

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.ProductId == productId && e.FunctionBlockId.EqualIgnoreCase(functionBlockId) && e.Identifier.EqualIgnoreCase(identifier));

        return Find(_.ProductId == productId & _.FunctionBlockId == functionBlockId & _.Identifier == identifier);
    }

    /// <summary>根据产品查找</summary>
    /// <param name="productId">产品</param>
    /// <returns>实体列表</returns>
    public static IList<ThingProperty> FindAllByProductId(Int32 productId)
    {
        if (productId < 0) return [];

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.FindAll(e => e.ProductId == productId);

        return FindAll(_.ProductId == productId);
    }

    /// <summary>根据产品、模块标识查找</summary>
    /// <param name="productId">产品</param>
    /// <param name="functionBlockId">模块标识</param>
    /// <returns>实体列表</returns>
    public static IList<ThingProperty> FindAllByProductIdAndFunctionBlockId(Int32 productId, String? functionBlockId)
    {
        if (productId < 0) return [];
        if (functionBlockId == null) return [];

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.FindAll(e => e.ProductId == productId && e.FunctionBlockId.EqualIgnoreCase(functionBlockId));

        return FindAll(_.ProductId == productId & _.FunctionBlockId == functionBlockId);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="productId">产品</param>
    /// <param name="identifier">标识符。属性唯一标识，如temperature</param>
    /// <param name="functionBlockId">模块标识。空表示默认模块</param>
    /// <param name="@required">是否必选</param>
    /// <param name="enable">启用</param>
    /// <param name="start">更新时间开始</param>
    /// <param name="end">更新时间结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<ThingProperty> Search(Int32 productId, String identifier, String? functionBlockId, Boolean? @required, Boolean? enable, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (productId >= 0) exp &= _.ProductId == productId;
        if (!identifier.IsNullOrEmpty()) exp &= _.Identifier == identifier;
        if (!functionBlockId.IsNullOrEmpty()) exp &= _.FunctionBlockId == functionBlockId;
        if (@required != null) exp &= _.Required == @required;
        if (enable != null) exp &= _.Enable == enable;
        exp &= _.UpdateTime.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    /// <summary>取得产品属性字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>产品</summary>
        public static readonly Field ProductId = FindByName("ProductId");

        /// <summary>标识符。属性唯一标识，如temperature</summary>
        public static readonly Field Identifier = FindByName("Identifier");

        /// <summary>名称</summary>
        public static readonly Field Name = FindByName("Name");

        /// <summary>数据类型。int/float/double/text/date/bool/enum/struct/array，兼容历史 string</summary>
        public static readonly Field DataType = FindByName("DataType");

        /// <summary>单位</summary>
        public static readonly Field Unit = FindByName("Unit");

        /// <summary>单位名称</summary>
        public static readonly Field UnitName = FindByName("UnitName");

        /// <summary>最小值</summary>
        public static readonly Field Min = FindByName("Min");

        /// <summary>最大值</summary>
        public static readonly Field Max = FindByName("Max");

        /// <summary>步长</summary>
        public static readonly Field Step = FindByName("Step");

        /// <summary>长度。text 最大长度，或 array 元素个数上限</summary>
        public static readonly Field Length = FindByName("Length");

        /// <summary>访问模式。r只读，w只写，rw读写</summary>
        public static readonly Field AccessMode = FindByName("AccessMode");

        /// <summary>是否必选</summary>
        public static readonly Field Required = FindByName("Required");

        /// <summary>数据类型规格。阿里云 TSL dataType.specs 的 Json。struct 为数组，array 含 size 与 item</summary>
        public static readonly Field Specs = FindByName("Specs");

        /// <summary>模块标识。空表示默认模块</summary>
        public static readonly Field FunctionBlockId = FindByName("FunctionBlockId");

        /// <summary>点位地址。驱动采集地址，如Modbus的4x0</summary>
        public static readonly Field Address = FindByName("Address");

        /// <summary>点位类型。驱动采集的原始数据类型，如short/ushort/int/float，空表示与数据类型一致</summary>
        public static readonly Field PointType = FindByName("PointType");

        /// <summary>缩放因子。原始值乘以该系数得到工程值，0表示不缩放</summary>
        public static readonly Field Scaling = FindByName("Scaling");

        /// <summary>枚举或布尔映射。如0=关,1=开，对应 TSL specs 的键值</summary>
        public static readonly Field Mapping = FindByName("Mapping");

        /// <summary>排序</summary>
        public static readonly Field Sort = FindByName("Sort");

        /// <summary>启用</summary>
        public static readonly Field Enable = FindByName("Enable");

        /// <summary>创建时间</summary>
        public static readonly Field CreateTime = FindByName("CreateTime");

        /// <summary>更新时间</summary>
        public static readonly Field UpdateTime = FindByName("UpdateTime");

        /// <summary>备注</summary>
        public static readonly Field Remark = FindByName("Remark");

        static Field FindByName(String name) => Meta.Table.FindByName(name)!;
    }

    /// <summary>取得产品属性字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>编号</summary>
        public const String Id = "Id";

        /// <summary>产品</summary>
        public const String ProductId = "ProductId";

        /// <summary>标识符。属性唯一标识，如temperature</summary>
        public const String Identifier = "Identifier";

        /// <summary>名称</summary>
        public const String Name = "Name";

        /// <summary>数据类型。int/float/double/text/date/bool/enum/struct/array，兼容历史 string</summary>
        public const String DataType = "DataType";

        /// <summary>单位</summary>
        public const String Unit = "Unit";

        /// <summary>单位名称</summary>
        public const String UnitName = "UnitName";

        /// <summary>最小值</summary>
        public const String Min = "Min";

        /// <summary>最大值</summary>
        public const String Max = "Max";

        /// <summary>步长</summary>
        public const String Step = "Step";

        /// <summary>长度。text 最大长度，或 array 元素个数上限</summary>
        public const String Length = "Length";

        /// <summary>访问模式。r只读，w只写，rw读写</summary>
        public const String AccessMode = "AccessMode";

        /// <summary>是否必选</summary>
        public const String Required = "Required";

        /// <summary>数据类型规格。阿里云 TSL dataType.specs 的 Json。struct 为数组，array 含 size 与 item</summary>
        public const String Specs = "Specs";

        /// <summary>模块标识。空表示默认模块</summary>
        public const String FunctionBlockId = "FunctionBlockId";

        /// <summary>点位地址。驱动采集地址，如Modbus的4x0</summary>
        public const String Address = "Address";

        /// <summary>点位类型。驱动采集的原始数据类型，如short/ushort/int/float，空表示与数据类型一致</summary>
        public const String PointType = "PointType";

        /// <summary>缩放因子。原始值乘以该系数得到工程值，0表示不缩放</summary>
        public const String Scaling = "Scaling";

        /// <summary>枚举或布尔映射。如0=关,1=开，对应 TSL specs 的键值</summary>
        public const String Mapping = "Mapping";

        /// <summary>排序</summary>
        public const String Sort = "Sort";

        /// <summary>启用</summary>
        public const String Enable = "Enable";

        /// <summary>创建时间</summary>
        public const String CreateTime = "CreateTime";

        /// <summary>更新时间</summary>
        public const String UpdateTime = "UpdateTime";

        /// <summary>备注</summary>
        public const String Remark = "Remark";
    }
    #endregion
}
