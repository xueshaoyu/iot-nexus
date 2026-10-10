using System.ComponentModel.DataAnnotations;
using IoTPlatform.Data;

namespace IoTPlatform.Models;

/// <summary>分页结果</summary>
public record PagedResult<T>(IList<T> Items, Int64 Total, Int32 PageIndex, Int32 PageSize);

/// <summary>产品输入</summary>
public class ProductInput
{
    [Required, StringLength(50)]
    public String Name { get; set; } = "";

    /// <summary>ProductKey，留空自动生成</summary>
    [StringLength(50), RegularExpression("^[A-Za-z0-9_\\-]*$", ErrorMessage = "Code only allows letters, digits, '_' and '-'")]
    public String? Code { get; set; }

    [StringLength(50)]
    public String? Category { get; set; }

    /// <summary>接入协议 MQTT/HTTP/Driver</summary>
    [Required]
    public String Protocol { get; set; } = AccessProtocols.Mqtt;

    /// <summary>载荷编解码 Json/Alink</summary>
    public String? Codec { get; set; } = "Json";

    public String? DriverName { get; set; }

    public String? DriverParameter { get; set; }

    [Range(0, 3600_000)]
    public Int32 PollInterval { get; set; }

    [Range(0, 86400)]
    public Int32 OnlineTimeout { get; set; }

    /// <summary>MQTT 产品的默认服务器。空表示使用平台默认（Mode / ActiveBroker）</summary>
    [StringLength(50)]
    public String? BrokerProfileId { get; set; }

    public Boolean Enable { get; set; } = true;

    [StringLength(500)]
    public String? Remark { get; set; }

    public static ProductInput From(Product p) => new()
    {
        Name = p.Name,
        Code = p.Code,
        Category = p.Category,
        Protocol = p.Protocol ?? AccessProtocols.Mqtt,
        Codec = p.Codec,
        DriverName = p.DriverName,
        DriverParameter = p.DriverParameter,
        PollInterval = p.PollInterval,
        OnlineTimeout = p.OnlineTimeout,
        BrokerProfileId = p.BrokerProfileId,
        Enable = p.Enable,
        Remark = p.Remark,
    };
}

/// <summary>物模型属性输入</summary>
public class ThingPropertyInput
{
    [Required, StringLength(50), RegularExpression("^[A-Za-z_][A-Za-z0-9_]{0,49}$", ErrorMessage = "Identifier must be a valid identifier")]
    public String Identifier { get; set; } = "";

    [StringLength(30)]
    public String? Name { get; set; }

    /// <summary>int/float/double/text/date/bool/enum/struct/array，兼容 string/short/long</summary>
    [Required]
    public String DataType { get; set; } = "float";

    public String? Unit { get; set; }
    public String? UnitName { get; set; }
    public Double Min { get; set; }
    public Double Max { get; set; }
    public Double Step { get; set; }
    public Int32 Length { get; set; }

    /// <summary>r 只读 / w 只写 / rw 读写</summary>
    public String AccessMode { get; set; } = "r";

    public Boolean Required { get; set; }

    /// <summary>自定义模块标识。空表示默认模块</summary>
    [StringLength(50)]
    public String? FunctionBlockId { get; set; }

    /// <summary>自定义模块名称，仅在首次出现该模块时写入产品</summary>
    [StringLength(30)]
    public String? FunctionBlockName { get; set; }

    /// <summary>array 的元素类型</summary>
    public String? ItemType { get; set; }

    /// <summary>struct 的 specs JSON，或 array 的完整 specs</summary>
    public String? Specs { get; set; }

    /// <summary>驱动点位地址，如 Modbus 4x0</summary>
    public String? Address { get; set; }

    /// <summary>驱动原始数据类型，如 ushort，空表示与 DataType 一致</summary>
    public String? PointType { get; set; }

    public Double Scaling { get; set; }
    public String? Mapping { get; set; }
    public Int32 Sort { get; set; }
    public Boolean Enable { get; set; } = true;
    public String? Remark { get; set; }

    public static ThingPropertyInput From(ThingProperty e) => new()
    {
        Identifier = e.Identifier,
        Name = e.Name,
        DataType = e.DataType ?? "float",
        Unit = e.Unit,
        UnitName = e.UnitName,
        Min = e.Min,
        Max = e.Max,
        Step = e.Step,
        Length = e.Length,
        AccessMode = e.AccessMode ?? "r",
        Required = e.Required,
        FunctionBlockId = e.FunctionBlockId,
        Specs = e.Specs,
        Address = e.Address,
        PointType = e.PointType,
        Scaling = e.Scaling,
        Mapping = e.Mapping,
        Sort = e.Sort,
        Enable = e.Enable,
        Remark = e.Remark,
    };
}

/// <summary>物模型参数（事件输出、服务输入输出）。DataType 为 TSL 类型，规格字段与属性相同</summary>
public class ThingParameter
{
    public String Identifier { get; set; } = "";
    public String? Name { get; set; }
    public String DataType { get; set; } = "text";
    public Double Min { get; set; }
    public Double Max { get; set; }
    public Double Step { get; set; }
    public String? Unit { get; set; }
    public String? UnitName { get; set; }
    public Int32 Length { get; set; }
    public String? Mapping { get; set; }
    public String? ItemType { get; set; }
    public String? Specs { get; set; }
}

/// <summary>物模型事件输入</summary>
public class ThingEventInput
{
    [Required, StringLength(50), RegularExpression("^[A-Za-z_][A-Za-z0-9_]{0,49}$", ErrorMessage = "Identifier must be a valid identifier")]
    public String Identifier { get; set; } = "";

    [StringLength(30)]
    public String? Name { get; set; }

    /// <summary>info/alert/error</summary>
    public String Type { get; set; } = "info";

    public Boolean Required { get; set; }

    [StringLength(50)]
    public String? FunctionBlockId { get; set; }

    public List<ThingParameter> OutputData { get; set; } = [];
    public Int32 Sort { get; set; }
    public Boolean Enable { get; set; } = true;
    public String? Remark { get; set; }
}

/// <summary>物模型服务输入</summary>
public class ThingServiceInput
{
    [Required, StringLength(50), RegularExpression("^[A-Za-z_][A-Za-z0-9_]{0,49}$", ErrorMessage = "Identifier must be a valid identifier")]
    public String Identifier { get; set; } = "";

    [StringLength(30)]
    public String? Name { get; set; }

    /// <summary>async/sync</summary>
    public String CallType { get; set; } = "async";

    public Boolean Required { get; set; }

    [StringLength(50)]
    public String? FunctionBlockId { get; set; }

    public List<ThingParameter> InputData { get; set; } = [];
    public List<ThingParameter> OutputData { get; set; } = [];
    public Int32 Sort { get; set; }
    public Boolean Enable { get; set; } = true;
    public String? Remark { get; set; }
}

/// <summary>设备输入</summary>
public class DeviceInput
{
    [StringLength(50)]
    public String? Name { get; set; }

    /// <summary>DeviceName，留空自动生成</summary>
    [StringLength(50), RegularExpression("^[A-Za-z0-9_\\-\\.]*$", ErrorMessage = "Code only allows letters, digits, '_', '-' and '.'")]
    public String? Code { get; set; }

    [Range(1, Int32.MaxValue, ErrorMessage = "Product is required")]
    public Int32 ProductId { get; set; }

    public Int32 GroupId { get; set; }

    /// <summary>设备密钥，留空自动生成</summary>
    [StringLength(50)]
    public String? Secret { get; set; }

    public Boolean Enable { get; set; } = true;

    public String? Location { get; set; }

    public String? DriverParameter { get; set; }

    [Range(0, 3600_000)]
    public Int32 PollInterval { get; set; }

    /// <summary>设备使用的 MQTT 服务器。空表示使用产品默认，再回退平台默认</summary>
    [StringLength(50)]
    public String? BrokerProfileId { get; set; }

    [StringLength(500)]
    public String? Remark { get; set; }

    public static DeviceInput From(Device d) => new()
    {
        Name = d.Name,
        Code = d.Code,
        ProductId = d.ProductId,
        GroupId = d.GroupId,
        Secret = d.Secret,
        Enable = d.Enable,
        Location = d.Location,
        DriverParameter = d.DriverParameter,
        PollInterval = d.PollInterval,
        BrokerProfileId = d.BrokerProfileId,
        Remark = d.Remark,
    };
}

/// <summary>批量注册设备</summary>
public class DeviceBatchInput
{
    [Range(1, Int32.MaxValue, ErrorMessage = "Product is required")]
    public Int32 ProductId { get; set; }

    public Int32 GroupId { get; set; }

    [Required, StringLength(40), RegularExpression("^[A-Za-z0-9_\\-\\.]+$")]
    public String Prefix { get; set; } = "dev";

    [Range(1, 1000)]
    public Int32 Count { get; set; } = 10;
}

/// <summary>设备查询</summary>
public class DeviceQuery
{
    public Int32 ProductId { get; set; }
    public Int32 GroupId { get; set; }

    /// <summary>包含子分组</summary>
    public Boolean IncludeSubGroups { get; set; } = true;

    public Boolean? Online { get; set; }
    public Boolean? Enable { get; set; }
    public String? Keyword { get; set; }
    public Int32 PageIndex { get; set; } = 1;
    public Int32 PageSize { get; set; } = 20;
}

/// <summary>分组输入</summary>
public class GroupInput
{
    [Required, StringLength(50)]
    public String Name { get; set; } = "";

    public Int32 ParentId { get; set; }
    public Int32 Sort { get; set; }
    public Boolean Enable { get; set; } = true;

    [StringLength(500)]
    public String? Remark { get; set; }
}

/// <summary>设备批量移动分组</summary>
public class GroupAssignInput
{
    [Required, MinLength(1)]
    public Int32[] DeviceIds { get; set; } = [];

    /// <summary>目标分组，0表示移出分组</summary>
    public Int32 GroupId { get; set; }
}

/// <summary>服务调用</summary>
public class ServiceInvokeInput
{
    [Required]
    public String Service { get; set; } = "";

    public Dictionary<String, Object?> Args { get; set; } = [];

    /// <summary>等待设备回复的秒数，0表示仅下发不等待</summary>
    [Range(0, 60)]
    public Int32 WaitSeconds { get; set; }
}

/// <summary>属性设置</summary>
public class PropertySetInput
{
    [Required, MinLength(1)]
    public Dictionary<String, Object?> Values { get; set; } = [];

    [Range(0, 60)]
    public Int32 WaitSeconds { get; set; }
}

/// <summary>告警处理</summary>
public class AlarmHandleInput
{
    public AlarmStatus Status { get; set; } = AlarmStatus.Acknowledged;
    public String? Remark { get; set; }
}
