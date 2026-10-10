using System.Collections.Concurrent;
using System.ComponentModel;
using NewLife.IoT;
using NewLife.IoT.Drivers;
using NewLife.IoT.ThingModels;

namespace IoTPlatform.Protocols.Drivers;

/// <summary>模拟驱动参数</summary>
public class SimulatorParameter : IDriverParameter
{
    /// <summary>波形周期，单位秒</summary>
    public Int32 Period { get; set; } = 120;

    /// <summary>噪声幅度，取值范围比例 0~1</summary>
    public Double Noise { get; set; } = 0.03;

    /// <summary>模拟采集失败概率 0~1，用于演示离线判定</summary>
    public Double FailureRate { get; set; }
}

/// <summary>虚拟设备驱动。按物模型的最小/最大值生成正弦波数据，无需硬件即可演示采集链路</summary>
/// <remarks>
/// 作为自定义驱动的参考实现：继承 NewLife.IoT 的 DriverBase，标记 [Driver] 名称即可被 DriverFactory 发现。
/// 支持写入（WriteAsync 保存覆盖值）与服务调用（ControlAsync 回显参数）。
/// </remarks>
[Driver("Simulator")]
[DisplayName("Virtual simulator")]
[Description("Generates sine-wave values inside each property's min/max range. No hardware required.")]
public class SimulatorDriver : DriverBase<Node, SimulatorParameter>
{
    private readonly ConcurrentDictionary<String, Object?> _overrides = new(StringComparer.OrdinalIgnoreCase);

    public override Task<ReadResult> ReadAsync(INode node, IPoint[] points, CancellationToken cancellationToken = default)
    {
        var p = node.Parameter as SimulatorParameter ?? new SimulatorParameter();
        if (p.FailureRate > 0 && Random.Shared.NextDouble() < p.FailureRate)
            return Task.FromResult(ReadResult.Fail(IoTErrorCode.Timeout, "Simulated read failure"));

        var spec = node.Device?.Specification;
        var t = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
        var values = new Object?[points.Length];

        for (var i = 0; i < points.Length; i++)
        {
            var point = points[i];
            var key = $"{node.Device?.Code}:{point.Name}";
            if (_overrides.TryGetValue(key, out var written))
            {
                values[i] = written;
                continue;
            }

            var ps = spec?.GetProperty(point.Name ?? "");
            var type = (ps?.DataType?.Type ?? point.Type ?? "double").ToLowerInvariant();
            var min = ps?.DataType?.Specs?.Min ?? 0;
            var max = ps?.DataType?.Specs?.Max ?? 0;
            if (max <= min) { min = 0; max = 100; }

            var phase = (StableHash(point.Name) % 1000) / 1000.0 * Math.PI * 2;
            var wave = Math.Sin(2 * Math.PI * t / Math.Max(10, p.Period) + phase);
            var noise = (Random.Shared.NextDouble() * 2 - 1) * p.Noise * (max - min);
            var v = Math.Clamp((min + max) / 2 + wave * (max - min) * 0.4 + noise, min, max);

            values[i] = type switch
            {
                "bool" or "boolean" or "bit" => wave > 0,
                "int" or "int32" or "long" or "int64" or "short" or "int16" or "ushort" or "uint16" or "enum" or "byte" => (Int64)Math.Round(v),
                "string" or "text" => $"sim-{(Int64)t % 10000}",
                _ => Math.Round(v, 2),
            };
        }

        return Task.FromResult(ReadResult.Success(points, values, default, null, null));
    }

    public override Task<WriteResult> WriteAsync(INode node, WriteRequest[] requests, CancellationToken cancellationToken = default)
    {
        foreach (var r in requests)
        {
            if (r.Point?.Name != null) _overrides[$"{node.Device?.Code}:{r.Point.Name}"] = r.Value;
        }

        return Task.FromResult(WriteResult.SuccessBatch(requests.Length));
    }

    public override Task<ControlResult> ControlAsync(INode node, ControlRequest request, CancellationToken cancellationToken = default)
    {
        if (request.ServiceName.Equals("reset", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var key in _overrides.Keys.Where(e => e.StartsWith($"{node.Device?.Code}:")).ToList()) _overrides.TryRemove(key, out _);
        }

        return Task.FromResult(ControlResult.Success(new Dictionary<String, Object?>
        {
            ["service"] = request.ServiceName,
            ["echo"] = request.Parameters,
            ["time"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
        }!));
    }

    private static Int32 StableHash(String? text)
    {
        unchecked
        {
            var h = 23;
            foreach (var c in text ?? "") h = h * 31 + c;
            return h & 0x7FFFFFFF;
        }
    }
}
