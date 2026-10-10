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

/// <summary>访问记录。管理端页面与 API 的访问日志，不含静态资源和 Blazor 心跳</summary>
[Serializable]
[DataObject]
[Description("访问记录。管理端页面与 API 的访问日志，不含静态资源和 Blazor 心跳")]
[BindIndex("IX_AccessLog_CreateTime", false, "CreateTime")]
[BindIndex("IX_AccessLog_ClientIp_CreateTime", false, "ClientIp,CreateTime")]
[BindTable("AccessLog", Description = "访问记录。管理端页面与 API 的访问日志，不含静态资源和 Blazor 心跳", ConnName = "IoT", DbType = DatabaseType.None)]
public partial class AccessLog
{
    #region 属性
    private Int64 _Id;
    /// <summary>编号</summary>
    [DisplayName("编号")]
    [Description("编号")]
    [DataObjectField(true, false, false, 0)]
    [BindColumn("Id", "编号", "", DataScale = "time")]
    public Int64 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private String _ClientIp = null!;
    /// <summary>客户端 IP。已解析转发头</summary>
    [DisplayName("客户端IP")]
    [Description("客户端 IP。已解析转发头")]
    [DataObjectField(false, false, false, 50)]
    [BindColumn("ClientIp", "客户端 IP。已解析转发头", "")]
    public String ClientIp { get => _ClientIp; set { if (OnPropertyChanging("ClientIp", value)) { _ClientIp = value; OnPropertyChanged("ClientIp"); } } }

    private String? _Country;
    /// <summary>国家或地区。优先 CF-IPCountry</summary>
    [DisplayName("国家或地区")]
    [Description("国家或地区。优先 CF-IPCountry")]
    [DataObjectField(false, false, true, 8)]
    [BindColumn("Country", "国家或地区。优先 CF-IPCountry", "")]
    public String? Country { get => _Country; set { if (OnPropertyChanging("Country", value)) { _Country = value; OnPropertyChanged("Country"); } } }

    private String? _Referer;
    /// <summary>来源页面</summary>
    [DisplayName("来源页面")]
    [Description("来源页面")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("Referer", "来源页面", "")]
    public String? Referer { get => _Referer; set { if (OnPropertyChanging("Referer", value)) { _Referer = value; OnPropertyChanged("Referer"); } } }

    private String? _RefererHost;
    /// <summary>来源主机。用于来源统计</summary>
    [DisplayName("来源主机")]
    [Description("来源主机。用于来源统计")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("RefererHost", "来源主机。用于来源统计", "")]
    public String? RefererHost { get => _RefererHost; set { if (OnPropertyChanging("RefererHost", value)) { _RefererHost = value; OnPropertyChanged("RefererHost"); } } }

    private String? _UserAgent;
    /// <summary>User-Agent</summary>
    [DisplayName("User-Agent")]
    [Description("User-Agent")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("UserAgent", "User-Agent", "")]
    public String? UserAgent { get => _UserAgent; set { if (OnPropertyChanging("UserAgent", value)) { _UserAgent = value; OnPropertyChanged("UserAgent"); } } }

    private String? _Browser;
    /// <summary>浏览器</summary>
    [DisplayName("浏览器")]
    [Description("浏览器")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Browser", "浏览器", "")]
    public String? Browser { get => _Browser; set { if (OnPropertyChanging("Browser", value)) { _Browser = value; OnPropertyChanged("Browser"); } } }

    private String? _Os;
    /// <summary>操作系统</summary>
    [DisplayName("操作系统")]
    [Description("操作系统")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Os", "操作系统", "")]
    public String? Os { get => _Os; set { if (OnPropertyChanging("Os", value)) { _Os = value; OnPropertyChanged("Os"); } } }

    private Boolean _Bot;
    /// <summary>是否爬虫</summary>
    [DisplayName("是否爬虫")]
    [Description("是否爬虫")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Bot", "是否爬虫", "")]
    public Boolean Bot { get => _Bot; set { if (OnPropertyChanging("Bot", value)) { _Bot = value; OnPropertyChanged("Bot"); } } }

    private String _Method = null!;
    /// <summary>方法</summary>
    [DisplayName("方法")]
    [Description("方法")]
    [DataObjectField(false, false, false, 16)]
    [BindColumn("Method", "方法", "")]
    public String Method { get => _Method; set { if (OnPropertyChanging("Method", value)) { _Method = value; OnPropertyChanged("Method"); } } }

    private String _Path = null!;
    /// <summary>路径</summary>
    [DisplayName("路径")]
    [Description("路径")]
    [DataObjectField(false, false, false, 300)]
    [BindColumn("Path", "路径", "")]
    public String Path { get => _Path; set { if (OnPropertyChanging("Path", value)) { _Path = value; OnPropertyChanged("Path"); } } }

    private String? _QueryString;
    /// <summary>查询串。不含问号</summary>
    [DisplayName("查询串")]
    [Description("查询串。不含问号")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("QueryString", "查询串。不含问号", "")]
    public String? QueryString { get => _QueryString; set { if (OnPropertyChanging("QueryString", value)) { _QueryString = value; OnPropertyChanged("QueryString"); } } }

    private Int32 _StatusCode;
    /// <summary>状态码</summary>
    [DisplayName("状态码")]
    [Description("状态码")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("StatusCode", "状态码", "")]
    public Int32 StatusCode { get => _StatusCode; set { if (OnPropertyChanging("StatusCode", value)) { _StatusCode = value; OnPropertyChanged("StatusCode"); } } }

    private Int32 _DurationMs;
    /// <summary>耗时。毫秒</summary>
    [DisplayName("耗时")]
    [Description("耗时。毫秒")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("DurationMs", "耗时。毫秒", "")]
    public Int32 DurationMs { get => _DurationMs; set { if (OnPropertyChanging("DurationMs", value)) { _DurationMs = value; OnPropertyChanged("DurationMs"); } } }

    private String? _Host;
    /// <summary>Host</summary>
    [DisplayName("Host")]
    [Description("Host")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Host", "Host", "")]
    public String? Host { get => _Host; set { if (OnPropertyChanging("Host", value)) { _Host = value; OnPropertyChanged("Host"); } } }

    private DateTime _CreateTime;
    /// <summary>时间</summary>
    [DisplayName("时间")]
    [Description("时间")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("CreateTime", "时间", "")]
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
            "ClientIp" => _ClientIp,
            "Country" => _Country,
            "Referer" => _Referer,
            "RefererHost" => _RefererHost,
            "UserAgent" => _UserAgent,
            "Browser" => _Browser,
            "Os" => _Os,
            "Bot" => _Bot,
            "Method" => _Method,
            "Path" => _Path,
            "QueryString" => _QueryString,
            "StatusCode" => _StatusCode,
            "DurationMs" => _DurationMs,
            "Host" => _Host,
            "CreateTime" => _CreateTime,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToLong(); break;
                case "ClientIp": _ClientIp = Convert.ToString(value); break;
                case "Country": _Country = Convert.ToString(value); break;
                case "Referer": _Referer = Convert.ToString(value); break;
                case "RefererHost": _RefererHost = Convert.ToString(value); break;
                case "UserAgent": _UserAgent = Convert.ToString(value); break;
                case "Browser": _Browser = Convert.ToString(value); break;
                case "Os": _Os = Convert.ToString(value); break;
                case "Bot": _Bot = value.ToBoolean(); break;
                case "Method": _Method = Convert.ToString(value); break;
                case "Path": _Path = Convert.ToString(value); break;
                case "QueryString": _QueryString = Convert.ToString(value); break;
                case "StatusCode": _StatusCode = value.ToInt(); break;
                case "DurationMs": _DurationMs = value.ToInt(); break;
                case "Host": _Host = Convert.ToString(value); break;
                case "CreateTime": _CreateTime = value.ToDateTime(); break;
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
    public static AccessLog? FindById(Int64 id)
    {
        if (id < 0) return null;

        return Find(_.Id == id);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="clientIp">客户端 IP。已解析转发头</param>
    /// <param name="createTime">时间</param>
    /// <param name="bot">是否爬虫</param>
    /// <param name="start">编号开始</param>
    /// <param name="end">编号结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<AccessLog> Search(String clientIp, DateTime createTime, Boolean? bot, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (!clientIp.IsNullOrEmpty()) exp &= _.ClientIp == clientIp;
        if (bot != null) exp &= _.Bot == bot;
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
    /// <summary>取得访问记录字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>客户端 IP。已解析转发头</summary>
        public static readonly Field ClientIp = FindByName("ClientIp");

        /// <summary>国家或地区。优先 CF-IPCountry</summary>
        public static readonly Field Country = FindByName("Country");

        /// <summary>来源页面</summary>
        public static readonly Field Referer = FindByName("Referer");

        /// <summary>来源主机。用于来源统计</summary>
        public static readonly Field RefererHost = FindByName("RefererHost");

        /// <summary>User-Agent</summary>
        public static readonly Field UserAgent = FindByName("UserAgent");

        /// <summary>浏览器</summary>
        public static readonly Field Browser = FindByName("Browser");

        /// <summary>操作系统</summary>
        public static readonly Field Os = FindByName("Os");

        /// <summary>是否爬虫</summary>
        public static readonly Field Bot = FindByName("Bot");

        /// <summary>方法</summary>
        public static readonly Field Method = FindByName("Method");

        /// <summary>路径</summary>
        public static readonly Field Path = FindByName("Path");

        /// <summary>查询串。不含问号</summary>
        public static readonly Field QueryString = FindByName("QueryString");

        /// <summary>状态码</summary>
        public static readonly Field StatusCode = FindByName("StatusCode");

        /// <summary>耗时。毫秒</summary>
        public static readonly Field DurationMs = FindByName("DurationMs");

        /// <summary>Host</summary>
        public static readonly Field Host = FindByName("Host");

        /// <summary>时间</summary>
        public static readonly Field CreateTime = FindByName("CreateTime");

        static Field FindByName(String name) => Meta.Table.FindByName(name)!;
    }

    /// <summary>取得访问记录字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>编号</summary>
        public const String Id = "Id";

        /// <summary>客户端 IP。已解析转发头</summary>
        public const String ClientIp = "ClientIp";

        /// <summary>国家或地区。优先 CF-IPCountry</summary>
        public const String Country = "Country";

        /// <summary>来源页面</summary>
        public const String Referer = "Referer";

        /// <summary>来源主机。用于来源统计</summary>
        public const String RefererHost = "RefererHost";

        /// <summary>User-Agent</summary>
        public const String UserAgent = "UserAgent";

        /// <summary>浏览器</summary>
        public const String Browser = "Browser";

        /// <summary>操作系统</summary>
        public const String Os = "Os";

        /// <summary>是否爬虫</summary>
        public const String Bot = "Bot";

        /// <summary>方法</summary>
        public const String Method = "Method";

        /// <summary>路径</summary>
        public const String Path = "Path";

        /// <summary>查询串。不含问号</summary>
        public const String QueryString = "QueryString";

        /// <summary>状态码</summary>
        public const String StatusCode = "StatusCode";

        /// <summary>耗时。毫秒</summary>
        public const String DurationMs = "DurationMs";

        /// <summary>Host</summary>
        public const String Host = "Host";

        /// <summary>时间</summary>
        public const String CreateTime = "CreateTime";
    }
    #endregion
}
