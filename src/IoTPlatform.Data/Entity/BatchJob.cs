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

/// <summary>批量控制。按产品、分组或设备列表批量下发的属性设置或服务调用任务</summary>
[Serializable]
[DataObject]
[Description("批量控制。按产品、分组或设备列表批量下发的属性设置或服务调用任务")]
[BindTable("BatchJob", Description = "批量控制。按产品、分组或设备列表批量下发的属性设置或服务调用任务", ConnName = "IoT", DbType = DatabaseType.None)]
public partial class BatchJob
{
    #region 属性
    private Int64 _Id;
    /// <summary>编号</summary>
    [DisplayName("编号")]
    [Description("编号")]
    [DataObjectField(true, false, false, 0)]
    [BindColumn("Id", "编号", "", DataScale = "time")]
    public Int64 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private String? _Kind;
    /// <summary>种类。property属性设置，service服务调用</summary>
    [DisplayName("种类")]
    [Description("种类。property属性设置，service服务调用")]
    [DataObjectField(false, false, true, 20)]
    [BindColumn("Kind", "种类。property属性设置，service服务调用", "")]
    public String? Kind { get => _Kind; set { if (OnPropertyChanging("Kind", value)) { _Kind = value; OnPropertyChanged("Kind"); } } }

    private String? _Name;
    /// <summary>名称。属性或服务标识符</summary>
    [DisplayName("名称")]
    [Description("名称。属性或服务标识符")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Name", "名称。属性或服务标识符", "", Master = true)]
    public String? Name { get => _Name; set { if (OnPropertyChanging("Name", value)) { _Name = value; OnPropertyChanged("Name"); } } }

    private String? _ScopeType;
    /// <summary>范围类型。Product/Group/Devices</summary>
    [DisplayName("范围类型")]
    [Description("范围类型。Product/Group/Devices")]
    [DataObjectField(false, false, true, 20)]
    [BindColumn("ScopeType", "范围类型。Product/Group/Devices", "")]
    public String? ScopeType { get => _ScopeType; set { if (OnPropertyChanging("ScopeType", value)) { _ScopeType = value; OnPropertyChanged("ScopeType"); } } }

    private Int32 _ScopeId;
    /// <summary>范围编号。产品或分组编号</summary>
    [DisplayName("范围编号")]
    [Description("范围编号。产品或分组编号")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ScopeId", "范围编号。产品或分组编号", "")]
    public Int32 ScopeId { get => _ScopeId; set { if (OnPropertyChanging("ScopeId", value)) { _ScopeId = value; OnPropertyChanged("ScopeId"); } } }

    private String? _Scope;
    /// <summary>范围。Json描述</summary>
    [DisplayName("范围")]
    [Description("范围。Json描述")]
    [DataObjectField(false, false, true, 2000)]
    [BindColumn("Scope", "范围。Json描述", "")]
    public String? Scope { get => _Scope; set { if (OnPropertyChanging("Scope", value)) { _Scope = value; OnPropertyChanged("Scope"); } } }

    private String? _InputData;
    /// <summary>输入。属性值或服务参数</summary>
    [DisplayName("输入")]
    [Description("输入。属性值或服务参数")]
    [DataObjectField(false, false, true, 2000)]
    [BindColumn("InputData", "输入。属性值或服务参数", "")]
    public String? InputData { get => _InputData; set { if (OnPropertyChanging("InputData", value)) { _InputData = value; OnPropertyChanged("InputData"); } } }

    private Int32 _Total;
    /// <summary>总数</summary>
    [DisplayName("总数")]
    [Description("总数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Total", "总数", "")]
    public Int32 Total { get => _Total; set { if (OnPropertyChanging("Total", value)) { _Total = value; OnPropertyChanged("Total"); } } }

    private Int32 _Succeeded;
    /// <summary>成功</summary>
    [DisplayName("成功")]
    [Description("成功")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Succeeded", "成功", "")]
    public Int32 Succeeded { get => _Succeeded; set { if (OnPropertyChanging("Succeeded", value)) { _Succeeded = value; OnPropertyChanged("Succeeded"); } } }

    private Int32 _Pending;
    /// <summary>待回复。已下发或排队等待设备拉取</summary>
    [DisplayName("待回复")]
    [Description("待回复。已下发或排队等待设备拉取")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Pending", "待回复。已下发或排队等待设备拉取", "")]
    public Int32 Pending { get => _Pending; set { if (OnPropertyChanging("Pending", value)) { _Pending = value; OnPropertyChanged("Pending"); } } }

    private Int32 _Failed;
    /// <summary>失败</summary>
    [DisplayName("失败")]
    [Description("失败")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Failed", "失败", "")]
    public Int32 Failed { get => _Failed; set { if (OnPropertyChanging("Failed", value)) { _Failed = value; OnPropertyChanged("Failed"); } } }

    private Int32 _Skipped;
    /// <summary>跳过</summary>
    [DisplayName("跳过")]
    [Description("跳过")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Skipped", "跳过", "")]
    public Int32 Skipped { get => _Skipped; set { if (OnPropertyChanging("Skipped", value)) { _Skipped = value; OnPropertyChanged("Skipped"); } } }

    private Boolean _Completed;
    /// <summary>已完成</summary>
    [DisplayName("已完成")]
    [Description("已完成")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Completed", "已完成", "")]
    public Boolean Completed { get => _Completed; set { if (OnPropertyChanging("Completed", value)) { _Completed = value; OnPropertyChanged("Completed"); } } }

    private String? _Source;
    /// <summary>来源。Api/Console</summary>
    [DisplayName("来源")]
    [Description("来源。Api/Console")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Source", "来源。Api/Console", "")]
    public String? Source { get => _Source; set { if (OnPropertyChanging("Source", value)) { _Source = value; OnPropertyChanged("Source"); } } }

    private String? _Results;
    /// <summary>结果。每台设备的执行结果Json</summary>
    [DisplayName("结果")]
    [Description("结果。每台设备的执行结果Json")]
    [DataObjectField(false, false, true, -1)]
    [BindColumn("Results", "结果。每台设备的执行结果Json", "")]
    public String? Results { get => _Results; set { if (OnPropertyChanging("Results", value)) { _Results = value; OnPropertyChanged("Results"); } } }

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
            "Kind" => _Kind,
            "Name" => _Name,
            "ScopeType" => _ScopeType,
            "ScopeId" => _ScopeId,
            "Scope" => _Scope,
            "InputData" => _InputData,
            "Total" => _Total,
            "Succeeded" => _Succeeded,
            "Pending" => _Pending,
            "Failed" => _Failed,
            "Skipped" => _Skipped,
            "Completed" => _Completed,
            "Source" => _Source,
            "Results" => _Results,
            "CreateTime" => _CreateTime,
            "UpdateTime" => _UpdateTime,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToLong(); break;
                case "Kind": _Kind = Convert.ToString(value); break;
                case "Name": _Name = Convert.ToString(value); break;
                case "ScopeType": _ScopeType = Convert.ToString(value); break;
                case "ScopeId": _ScopeId = value.ToInt(); break;
                case "Scope": _Scope = Convert.ToString(value); break;
                case "InputData": _InputData = Convert.ToString(value); break;
                case "Total": _Total = value.ToInt(); break;
                case "Succeeded": _Succeeded = value.ToInt(); break;
                case "Pending": _Pending = value.ToInt(); break;
                case "Failed": _Failed = value.ToInt(); break;
                case "Skipped": _Skipped = value.ToInt(); break;
                case "Completed": _Completed = value.ToBoolean(); break;
                case "Source": _Source = Convert.ToString(value); break;
                case "Results": _Results = Convert.ToString(value); break;
                case "CreateTime": _CreateTime = value.ToDateTime(); break;
                case "UpdateTime": _UpdateTime = value.ToDateTime(); break;
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
    public static BatchJob? FindById(Int64 id)
    {
        if (id < 0) return null;

        return Find(_.Id == id);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="completed">已完成</param>
    /// <param name="start">编号开始</param>
    /// <param name="end">编号结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<BatchJob> Search(Boolean? completed, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (completed != null) exp &= _.Completed == completed;
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
    /// <summary>取得批量控制字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>种类。property属性设置，service服务调用</summary>
        public static readonly Field Kind = FindByName("Kind");

        /// <summary>名称。属性或服务标识符</summary>
        public static readonly Field Name = FindByName("Name");

        /// <summary>范围类型。Product/Group/Devices</summary>
        public static readonly Field ScopeType = FindByName("ScopeType");

        /// <summary>范围编号。产品或分组编号</summary>
        public static readonly Field ScopeId = FindByName("ScopeId");

        /// <summary>范围。Json描述</summary>
        public static readonly Field Scope = FindByName("Scope");

        /// <summary>输入。属性值或服务参数</summary>
        public static readonly Field InputData = FindByName("InputData");

        /// <summary>总数</summary>
        public static readonly Field Total = FindByName("Total");

        /// <summary>成功</summary>
        public static readonly Field Succeeded = FindByName("Succeeded");

        /// <summary>待回复。已下发或排队等待设备拉取</summary>
        public static readonly Field Pending = FindByName("Pending");

        /// <summary>失败</summary>
        public static readonly Field Failed = FindByName("Failed");

        /// <summary>跳过</summary>
        public static readonly Field Skipped = FindByName("Skipped");

        /// <summary>已完成</summary>
        public static readonly Field Completed = FindByName("Completed");

        /// <summary>来源。Api/Console</summary>
        public static readonly Field Source = FindByName("Source");

        /// <summary>结果。每台设备的执行结果Json</summary>
        public static readonly Field Results = FindByName("Results");

        /// <summary>创建时间</summary>
        public static readonly Field CreateTime = FindByName("CreateTime");

        /// <summary>更新时间</summary>
        public static readonly Field UpdateTime = FindByName("UpdateTime");

        static Field FindByName(String name) => Meta.Table.FindByName(name)!;
    }

    /// <summary>取得批量控制字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>编号</summary>
        public const String Id = "Id";

        /// <summary>种类。property属性设置，service服务调用</summary>
        public const String Kind = "Kind";

        /// <summary>名称。属性或服务标识符</summary>
        public const String Name = "Name";

        /// <summary>范围类型。Product/Group/Devices</summary>
        public const String ScopeType = "ScopeType";

        /// <summary>范围编号。产品或分组编号</summary>
        public const String ScopeId = "ScopeId";

        /// <summary>范围。Json描述</summary>
        public const String Scope = "Scope";

        /// <summary>输入。属性值或服务参数</summary>
        public const String InputData = "InputData";

        /// <summary>总数</summary>
        public const String Total = "Total";

        /// <summary>成功</summary>
        public const String Succeeded = "Succeeded";

        /// <summary>待回复。已下发或排队等待设备拉取</summary>
        public const String Pending = "Pending";

        /// <summary>失败</summary>
        public const String Failed = "Failed";

        /// <summary>跳过</summary>
        public const String Skipped = "Skipped";

        /// <summary>已完成</summary>
        public const String Completed = "Completed";

        /// <summary>来源。Api/Console</summary>
        public const String Source = "Source";

        /// <summary>结果。每台设备的执行结果Json</summary>
        public const String Results = "Results";

        /// <summary>创建时间</summary>
        public const String CreateTime = "CreateTime";

        /// <summary>更新时间</summary>
        public const String UpdateTime = "UpdateTime";
    }
    #endregion
}
