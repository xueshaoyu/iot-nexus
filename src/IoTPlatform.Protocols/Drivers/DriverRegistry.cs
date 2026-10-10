using NewLife.IoT.Drivers;

namespace IoTPlatform.Protocols.Drivers;

/// <summary>驱动注册。扫描已加载程序集中标记 [Driver] 的 NewLife.IoT 驱动，并确保内置驱动可用</summary>
/// <remarks>
/// 扩展新协议：引用驱动包（如 NewLife.Siemens、NewLife.IoTSocket）或自行实现 IDriver 并标记 [Driver("Name")]，
/// 启动时会被 DriverFactory.ScanAll 自动发现，产品选择 Driver 协议并填写驱动名即可使用。
/// </remarks>
public static class DriverRegistry
{
    private static readonly Object _lock = new();
    private static Boolean _registered;

    /// <summary>已注册驱动（含默认参数模板）</summary>
    public static IReadOnlyList<DriverInfo> Drivers
    {
        get
        {
            EnsureRegistered();
            return (IReadOnlyList<DriverInfo>?)DriverFactory.Drivers?.OrderBy(e => e.Name).ToList() ?? [];
        }
    }

    public static void EnsureRegistered()
    {
        if (_registered) return;

        lock (_lock)
        {
            if (_registered) return;

            // 触发程序集加载，保证扫描能发现 NewLife.Modbus 中的驱动
            _ = typeof(ModbusTcpDriver);
            _ = typeof(SimulatorDriver);

            try
            {
                DriverFactory.ScanAll();
            }
            catch (Exception ex)
            {
                NewLife.Log.XTrace.WriteException(ex);
            }

            if (!DriverFactory.Map.ContainsKey("Simulator")) DriverFactory.Register<SimulatorDriver>("Simulator");
            if (!DriverFactory.Map.ContainsKey("ModbusTcp")) DriverFactory.Register<ModbusTcpDriver>("ModbusTcp");

            _registered = true;
        }
    }
}
