using System.ComponentModel;

namespace IoTPlatform.Data;

/// <summary>告警级别</summary>
public enum AlarmLevels
{
    [Description("信息")]
    Info = 1,

    [Description("警告")]
    Warning = 2,

    [Description("严重")]
    Critical = 3,
}

/// <summary>告警状态</summary>
public enum AlarmStatus
{
    [Description("未处理")]
    Active = 0,

    [Description("已确认")]
    Acknowledged = 1,

    [Description("已关闭")]
    Closed = 2,
}

/// <summary>服务调用状态</summary>
public enum ServiceStatusKinds
{
    [Description("就绪")]
    Pending = 0,

    [Description("已下发")]
    Sent = 1,

    [Description("成功")]
    Success = 2,

    [Description("失败")]
    Failed = 3,

    [Description("超时")]
    Timeout = 4,
}

/// <summary>统计层级</summary>
public enum StatLevels
{
    [Description("小时")]
    Hour = 1,

    [Description("天")]
    Day = 2,
}
