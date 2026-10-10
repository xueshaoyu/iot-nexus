using IoTPlatform.Data;

namespace IoTPlatform.Services;

/// <summary>下行通道。把服务调用、属性设置送达设备，由协议层实现（MQTT、驱动等）</summary>
public interface IDeviceCommandChannel
{
    /// <summary>通道名称</summary>
    String Name { get; }

    /// <summary>是否能处理该产品的设备</summary>
    Boolean CanHandle(Product product);

    /// <summary>调用设备服务。同步通道可直接返回结果，异步通道返回 Sent 并等待设备回复</summary>
    Task<CommandResult> InvokeServiceAsync(Device device, Product product, ServiceRecord record, IDictionary<String, Object?> args, CancellationToken cancellationToken);

    /// <summary>设置设备属性</summary>
    Task<CommandResult> SetPropertyAsync(Device device, Product product, ServiceRecord record, IDictionary<String, Object?> values, CancellationToken cancellationToken);
}

/// <summary>下行结果</summary>
public record CommandResult(ServiceStatusKinds Status, String? Output = null)
{
    public static CommandResult Sent() => new(ServiceStatusKinds.Sent);
    public static CommandResult Success(String? output = null) => new(ServiceStatusKinds.Success, output);
    public static CommandResult Failed(String message) => new(ServiceStatusKinds.Failed, message);
}

/// <summary>设备会话管理。用于禁用、删除设备时踢下线，由协议层实现</summary>
public interface IDeviceSessionManager
{
    /// <summary>断开设备连接，返回是否找到会话</summary>
    Boolean Disconnect(Device device, String reason);
}
