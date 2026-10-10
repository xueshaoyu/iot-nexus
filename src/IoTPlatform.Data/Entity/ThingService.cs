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

/// <summary>产品服务。物模型服务定义，平台可远程调用的设备能力</summary>
[Serializable]
[DataObject]
[Description("产品服务。物模型服务定义，平台可远程调用的设备能力")]
[BindIndex("IU_ThingService_ProductId_FunctionBlockId_Identifier", true, "ProductId,FunctionBlockId,Identifier")]
[BindTable("ThingService", Description = "产品服务。物模型服务定义，平台可远程调用的设备能力", ConnName = "IoT", DbType = DatabaseType.None)]
public partial class ThingService
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
    /// <summary>标识符</summary>
    [DisplayName("标识符")]
    [Description("标识符")]
    [DataObjectField(false, false, false, 50)]
    [BindColumn("Identifier", "标识符", "")]
    public String Identifier { get => _Identifier; set { if (OnPropertyChanging("Identifier", value)) { _Identifier = value; OnPropertyChanged("Identifier"); } } }

    private String? _Name;
    /// <summary>名称</summary>
    [DisplayName("名称")]
    [Description("名称")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Name", "名称", "", Master = true)]
    public String? Name { get => _Name; set { if (OnPropertyChanging("Name", value)) { _Name = value; OnPropertyChanged("Name"); } } }

    private String? _CallType;
    /// <summary>调用方式。async异步，sync同步</summary>
    [DisplayName("调用方式")]
    [Description("调用方式。async异步，sync同步")]
    [DataObjectField(false, false, true, 20)]
    [BindColumn("CallType", "调用方式。async异步，sync同步", "")]
    public String? CallType { get => _CallType; set { if (OnPropertyChanging("CallType", value)) { _CallType = value; OnPropertyChanged("CallType"); } } }

    private Boolean _Required;
    /// <summary>是否必选</summary>
    [DisplayName("是否必选")]
    [Description("是否必选")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Required", "是否必选", "")]
    public Boolean Required { get => _Required; set { if (OnPropertyChanging("Required", value)) { _Required = value; OnPropertyChanged("Required"); } } }

    private String? _FunctionBlockId;
    /// <summary>模块标识。空表示默认模块</summary>
    [DisplayName("模块标识")]
    [Description("模块标识。空表示默认模块")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("FunctionBlockId", "模块标识。空表示默认模块", "")]
    public String? FunctionBlockId { get => _FunctionBlockId; set { if (OnPropertyChanging("FunctionBlockId", value)) { _FunctionBlockId = value; OnPropertyChanged("FunctionBlockId"); } } }

    private String? _InputData;
    /// <summary>输入参数。Json数组，元素含 identifier、name、dataType 与 specs</summary>
    [DisplayName("输入参数")]
    [Description("输入参数。Json数组，元素含 identifier、name、dataType 与 specs")]
    [DataObjectField(false, false, true, 4000)]
    [BindColumn("InputData", "输入参数。Json数组，元素含 identifier、name、dataType 与 specs", "")]
    public String? InputData { get => _InputData; set { if (OnPropertyChanging("InputData", value)) { _InputData = value; OnPropertyChanged("InputData"); } } }

    private String? _OutputData;
    /// <summary>输出参数。Json数组，元素含 identifier、name、dataType 与 specs</summary>
    [DisplayName("输出参数")]
    [Description("输出参数。Json数组，元素含 identifier、name、dataType 与 specs")]
    [DataObjectField(false, false, true, 4000)]
    [BindColumn("OutputData", "输出参数。Json数组，元素含 identifier、name、dataType 与 specs", "")]
    public String? OutputData { get => _OutputData; set { if (OnPropertyChanging("OutputData", value)) { _OutputData = value; OnPropertyChanged("OutputData"); } } }

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
            "CallType" => _CallType,
            "Required" => _Required,
            "FunctionBlockId" => _FunctionBlockId,
            "InputData" => _InputData,
            "OutputData" => _OutputData,
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
                case "CallType": _CallType = Convert.ToString(value); break;
                case "Required": _Required = value.ToBoolean(); break;
                case "FunctionBlockId": _FunctionBlockId = Convert.ToString(value); break;
                case "InputData": _InputData = Convert.ToString(value); break;
                case "OutputData": _OutputData = Convert.ToString(value); break;
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
    public static ThingService? FindById(Int32 id)
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
    public static ThingService? FindByProductIdAndFunctionBlockIdAndIdentifier(Int32 productId, String? functionBlockId, String identifier)
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
    public static IList<ThingService> FindAllByProductId(Int32 productId)
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
    public static IList<ThingService> FindAllByProductIdAndFunctionBlockId(Int32 productId, String? functionBlockId)
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
    /// <param name="identifier">标识符</param>
    /// <param name="functionBlockId">模块标识。空表示默认模块</param>
    /// <param name="@required">是否必选</param>
    /// <param name="enable">启用</param>
    /// <param name="start">更新时间开始</param>
    /// <param name="end">更新时间结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<ThingService> Search(Int32 productId, String identifier, String? functionBlockId, Boolean? @required, Boolean? enable, DateTime start, DateTime end, String key, PageParameter page)
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
    /// <summary>取得产品服务字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>产品</summary>
        public static readonly Field ProductId = FindByName("ProductId");

        /// <summary>标识符</summary>
        public static readonly Field Identifier = FindByName("Identifier");

        /// <summary>名称</summary>
        public static readonly Field Name = FindByName("Name");

        /// <summary>调用方式。async异步，sync同步</summary>
        public static readonly Field CallType = FindByName("CallType");

        /// <summary>是否必选</summary>
        public static readonly Field Required = FindByName("Required");

        /// <summary>模块标识。空表示默认模块</summary>
        public static readonly Field FunctionBlockId = FindByName("FunctionBlockId");

        /// <summary>输入参数。Json数组，元素含 identifier、name、dataType 与 specs</summary>
        public static readonly Field InputData = FindByName("InputData");

        /// <summary>输出参数。Json数组，元素含 identifier、name、dataType 与 specs</summary>
        public static readonly Field OutputData = FindByName("OutputData");

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

    /// <summary>取得产品服务字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>编号</summary>
        public const String Id = "Id";

        /// <summary>产品</summary>
        public const String ProductId = "ProductId";

        /// <summary>标识符</summary>
        public const String Identifier = "Identifier";

        /// <summary>名称</summary>
        public const String Name = "Name";

        /// <summary>调用方式。async异步，sync同步</summary>
        public const String CallType = "CallType";

        /// <summary>是否必选</summary>
        public const String Required = "Required";

        /// <summary>模块标识。空表示默认模块</summary>
        public const String FunctionBlockId = "FunctionBlockId";

        /// <summary>输入参数。Json数组，元素含 identifier、name、dataType 与 specs</summary>
        public const String InputData = "InputData";

        /// <summary>输出参数。Json数组，元素含 identifier、name、dataType 与 specs</summary>
        public const String OutputData = "OutputData";

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
