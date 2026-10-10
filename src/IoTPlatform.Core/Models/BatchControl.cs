using System.ComponentModel.DataAnnotations;

namespace IoTPlatform.Models;

/// <summary>批量控制范围类型</summary>
public enum BatchScopeType
{
    /// <summary>产品下全部设备</summary>
    Product,

    /// <summary>分组（可含子分组）下全部设备</summary>
    Group,

    /// <summary>指定设备列表</summary>
    Devices,
}

/// <summary>离线设备处理方式</summary>
public enum BatchOnlineMode
{
    /// <summary>自动：MQTT/驱动设备离线时跳过（下行需要会话或采集任务）；HTTP 设备照常排队，等待设备下次拉取</summary>
    Auto,

    /// <summary>仅在线设备</summary>
    OnlineOnly,

    /// <summary>全部尝试，离线设备会记为失败</summary>
    All,
}

/// <summary>批量控制范围</summary>
public class BatchScope
{
    public BatchScopeType Type { get; set; } = BatchScopeType.Devices;

    public Int32 ProductId { get; set; }

    public Int32 GroupId { get; set; }

    /// <summary>分组范围是否包含子分组</summary>
    public Boolean IncludeSubGroups { get; set; } = true;

    public Int32[] DeviceIds { get; set; } = [];

    public BatchOnlineMode OnlineMode { get; set; } = BatchOnlineMode.Auto;
}

/// <summary>批量控制请求：范围 + 动作</summary>
public class BatchControlInput
{
    public BatchScope Scope { get; set; } = new();

    /// <summary>property 设置属性，service 调用服务</summary>
    [Required]
    public String Kind { get; set; } = "property";

    /// <summary>属性标识符（Kind=property）</summary>
    public String? Property { get; set; }

    /// <summary>属性值（Kind=property）</summary>
    public Object? Value { get; set; }

    /// <summary>服务标识符（Kind=service）</summary>
    public String? Service { get; set; }

    /// <summary>服务参数（Kind=service）</summary>
    public Dictionary<String, Object?> Args { get; set; } = [];

    /// <summary>每台设备等待回复的秒数，0 表示仅下发</summary>
    [Range(0, 30)]
    public Int32 WaitSeconds { get; set; } = 5;
}

/// <summary>范围展开后的目标设备，SkipReason 非空表示将被跳过</summary>
public record BatchTarget(Int32 DeviceId, String Code, String? Name, Int32 ProductId, String? ProductName, String? Protocol, Boolean Online, String? SkipReason);

/// <summary>单台设备执行状态</summary>
public enum BatchItemStatus
{
    Success,
    Sent,
    Pending,
    Failed,
    Timeout,
    Skipped,
}

/// <summary>单台设备执行结果</summary>
public class BatchDeviceResult
{
    public Int32 DeviceId { get; set; }
    public String DeviceCode { get; set; } = "";
    public String? DeviceName { get; set; }
    public BatchItemStatus Status { get; set; }
    public String? Message { get; set; }

    /// <summary>对应的指令记录，可在设备指令记录中追踪后续回复</summary>
    public Int64 RecordId { get; set; }
}

/// <summary>批量控制结果</summary>
public class BatchControlResult
{
    public Int64 JobId { get; set; }
    public String Kind { get; set; } = "";
    public String Name { get; set; } = "";
    public Int32 Total { get; set; }
    public Int32 Succeeded { get; set; }

    /// <summary>已下发/排队但尚未收到回复</summary>
    public Int32 Pending { get; set; }

    public Int32 Failed { get; set; }
    public Int32 Skipped { get; set; }
    public Boolean Completed { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public List<BatchDeviceResult> Results { get; set; } = [];
}

/// <summary>执行进度</summary>
public record BatchProgress(Int32 Done, Int32 Total);
