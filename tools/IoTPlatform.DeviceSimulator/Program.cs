using IoTPlatform.Simulator;
using NewLife.Log;

// 独立设备模拟器：通过 MQTT / HTTP 连接平台，或启动 Modbus TCP 从站供平台驱动采集
XTrace.UseConsole();

var mqtt = "tcp://127.0.0.1:1883";
String? http = null;
var modbusPort = 0;
var interval = 3000;
var topicRoot = "iot";
var tls = false;
var mqttDevices = new List<DeviceProfile>();
var httpDevices = new List<DeviceProfile>();

for (var i = 0; i < args.Length; i++)
{
    String Next() => i + 1 < args.Length ? args[++i] : throw new ArgumentException($"Missing value for {args[i]}");

    switch (args[i])
    {
        case "--mqtt": mqtt = Next(); break;
        case "--http": http = Next(); break;
        case "--modbus": modbusPort = Int32.Parse(Next()); break;
        case "--interval": interval = Int32.Parse(Next()); break;
        case "--topic-root": topicRoot = Next(); break;
        case "--tls": tls = true; break;
        case "--device": mqttDevices.Add(Parse(Next())); break;
        case "--http-device": httpDevices.Add(Parse(Next())); break;
        case "--demo":
            foreach (var code in new[] { "th-001", "th-002", "th-003", "th-004", "th-005" }) mqttDevices.Add(new("env-sensor", code, $"{code}-secret", "sensor"));
            foreach (var code in new[] { "light-001", "light-002", "light-003" }) mqttDevices.Add(new("smart-light", code, $"{code}-secret", "light"));
            break;
        case "-h" or "--help":
            PrintUsage();
            return;
        default:
            Console.Error.WriteLine($"Unknown argument {args[i]}");
            PrintUsage();
            return;
    }
}

// 默认模拟演示数据中的备用传感器 th-005（平台内置模拟器不会连接它）
if (mqttDevices.Count == 0 && httpDevices.Count == 0 && modbusPort == 0)
    mqttDevices.Add(new DeviceProfile("env-sensor", "th-005", "th-005-secret", "sensor"));

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

ModbusSlaveSimulator? modbus = null;
if (modbusPort > 0)
{
    modbus = new ModbusSlaveSimulator(modbusPort, XTrace.Log);
    modbus.Start();
    XTrace.WriteLine("Modbus TCP slave listening on 0.0.0.0:{0} (holding registers 0-{1})", modbusPort, ModbusSlaveSimulator.RegisterCount - 1);
}

await using var mqttSim = mqttDevices.Count > 0 ? new MqttDeviceSimulator(mqtt, mqttDevices, interval, XTrace.Log, topicRoot, tls) : null;
mqttSim?.Start();
if (mqttSim != null) XTrace.WriteLine("Simulating {0} MQTT device(s) against {1}: {2}", mqttDevices.Count, mqtt, String.Join(", ", mqttDevices.Select(e => e.DeviceCode)));

await using var httpSim = httpDevices.Count > 0 && http != null ? new HttpDeviceSimulator(http, httpDevices, Math.Max(interval, 2000), XTrace.Log) : null;
httpSim?.Start();
if (httpSim != null) XTrace.WriteLine("Simulating {0} HTTP device(s) against {1}", httpDevices.Count, http);

XTrace.WriteLine("Press Ctrl+C to stop");
try { await Task.Delay(Timeout.Infinite, cts.Token); } catch (OperationCanceledException) { }

modbus?.Dispose();

static DeviceProfile Parse(String text)
{
    var parts = text.Split(':');
    if (parts.Length < 3) throw new ArgumentException($"Device must be productKey:deviceCode:secret[:kind], got '{text}'");

    return new DeviceProfile(parts[0], parts[1], parts[2], parts.Length > 3 ? parts[3] : parts[0].Contains("light") ? "light" : "sensor");
}

static void PrintUsage() => Console.WriteLine("""
    NewLife IoT device simulator

    Usage: dotnet run --project tools/IoTPlatform.DeviceSimulator -- [options]

      --mqtt <uri>              MQTT broker (default tcp://127.0.0.1:1883); can be the platform's
                                built-in broker or the external broker it is configured to use
      --topic-root <root>       Topic root (default iot, must match IoT:Mqtt:TopicRoot)
      --tls                     Use TLS for the MQTT connection
      --device pk:code:secret[:kind]
                                Simulate an MQTT device; kind = sensor | light (repeatable)
      --http <url>              Platform base URL for HTTP devices, e.g. http://127.0.0.1:5288
      --http-device pk:code:secret[:kind]
                                Simulate an HTTP device (repeatable, requires --http)
      --modbus <port>           Start a Modbus TCP slave (power meter registers 0-6)
      --interval <ms>           Report interval (default 3000)
      --demo                    Simulate all seeded demo MQTT devices (disable the platform's
                                built-in simulator first: IoT__Demo__EnableSimulator=false)

    With no options the spare demo sensor th-005 is simulated.
    """);
