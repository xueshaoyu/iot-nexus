using NewLife.IoT;
using NewLife.IoT.Models;
using NewLife.Log;

namespace IoTPlatform.Simulator;

/// <summary>Modbus TCP 从站模拟器（NewLife.Modbus ModbusSlave），模拟一台三相电表</summary>
/// <remarks>
/// 保持寄存器布局（功能码 03 读，06/16 写）：
///   0 电压 V ×10      1 电流 A ×100     2 有功功率 W
///   3 频率 Hz ×100    4 累计电能 kWh ×10  5 温度 °C ×10    6 继电器 0/1（可写）
/// </remarks>
public sealed class ModbusSlaveSimulator(Int32 port, ILog? log = null) : IDisposable
{
    private readonly ModbusSlave _slave = new() { Port = port, Log = log ?? Logger.Null };
    private readonly Random _random = new();
    private Timer? _timer;
    private Double _energy = 1234.5;

    public const Int32 RegisterCount = 7;

    public Int32 Port => port;

    public void Start()
    {
        for (var i = 0; i < RegisterCount; i++) _slave.Registers.Add(new RegisterUnit { Address = i, Value = 0 });
        Set(6, 1);
        Update();

        _slave.Start();
        _timer = new Timer(_ => Update(), null, 1000, 1000);
    }

    public void Dispose()
    {
        _timer?.Dispose();
        _slave.Stop("dispose");
        _slave.Dispose();
    }

    private void Update()
    {
        var relayOn = Get(6) != 0;
        var voltage = 220 + (_random.NextDouble() - 0.5) * 6;
        var current = relayOn ? 4.5 + Math.Sin(DateTime.Now.TimeOfDay.TotalSeconds / 30) * 2 + _random.NextDouble() * 0.3 : 0;
        var power = voltage * current * 0.95;
        _energy += power / 3600 / 1000;

        Set(0, voltage * 10);
        Set(1, current * 100);
        Set(2, power);
        Set(3, (50 + (_random.NextDouble() - 0.5) * 0.1) * 100);
        Set(4, _energy * 10 % 65535);
        Set(5, (32 + current * 1.5 + _random.NextDouble()) * 10);
    }

    private void Set(Int32 address, Double value)
    {
        var unit = _slave.Registers.FirstOrDefault(e => e.Address == address);
        if (unit != null) unit.Value = (UInt16)Math.Clamp(Math.Round(value), 0, UInt16.MaxValue);
    }

    private UInt16 Get(Int32 address) => _slave.Registers.FirstOrDefault(e => e.Address == address)?.Value ?? 0;
}
