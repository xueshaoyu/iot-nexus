using IoTPlatform.Data;
using IoTPlatform.Protocols;
using IoTPlatform.Services;
using IoTPlatform.Simulator;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Options;
using NewLife.Log;

namespace IoTPlatform.Web.Infrastructure;

/// <summary>进程内演示模拟器：Modbus 从站 + MQTT 设备 + HTTP 设备，让演示数据在 dotnet run 后立即流动</summary>
/// <remarks>备用设备 th-005 故意不模拟，用于展示离线状态。关闭方式：IoT:Demo:EnableSimulator=false。MQTT 设备按各自解析到的服务器分别连接。</remarks>
public class DemoSimulatorWorker(IOptions<IoTOptions> options, IServer server, IHostApplicationLifetime lifetime, MqttBrokerCatalog catalog) : BackgroundService
{
    private static readonly String[] Skipped = ["th-005"];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var demo = options.Value.Demo;
        if (!demo.EnableSimulator) return;

        // 等待 Web 与 MQTT 服务端就绪
        var started = new TaskCompletionSource();
        using var reg = lifetime.ApplicationStarted.Register(() => started.TrySetResult());
        await Task.WhenAny(started.Task, Task.Delay(Timeout.Infinite, stoppingToken));
        if (stoppingToken.IsCancellationRequested) return;
        await Task.Delay(1000, stoppingToken);

        ModbusSlaveSimulator? modbus = null;
        var mqttSims = new List<MqttDeviceSimulator>();
        HttpDeviceSimulator? http = null;
        try
        {
            try
            {
                modbus = new ModbusSlaveSimulator(demo.ModbusPort);
                modbus.Start();
                XTrace.WriteLine("演示 Modbus TCP 从站已启动 127.0.0.1:{0}", demo.ModbusPort);
            }
            catch (Exception ex)
            {
                XTrace.WriteLine("演示 Modbus 从站启动失败（端口 {0} 被占用？）{1}", demo.ModbusPort, ex.Message);
                modbus = null;
            }

            if (options.Value.Mqtt.Enable)
            {
                foreach (var group in Profiles(DataSeeder.EnvSensor, "sensor").Concat(Profiles(DataSeeder.SmartLight, "light")).GroupBy(e => e.BrokerId, StringComparer.OrdinalIgnoreCase))
                {
                    var brokerId = group.Key;
                    var (serverUrl, root, useTls) = Endpoint(brokerId);
                    if (serverUrl == null)
                    {
                        XTrace.WriteLine("演示 MQTT 设备模拟器跳过 {0}：未找到服务器配置", brokerId);
                        continue;
                    }

                    var sim = new MqttDeviceSimulator(serverUrl, group.Select(e => e.Profile), demo.ReportInterval, XTrace.Log, root, useTls);
                    sim.Start();
                    mqttSims.Add(sim);
                    XTrace.WriteLine("演示 MQTT 设备模拟器已启动，设备数 {0}，服务器 {1}（{2}）", sim.Count, serverUrl, brokerId);
                }
            }

            var httpProfiles = Profiles(DataSeeder.HttpGateway, "sensor").Select(e => e.Profile).ToList();
            var baseUrl = GetLocalUrl();
            if (httpProfiles.Count > 0 && baseUrl != null)
            {
                http = new HttpDeviceSimulator(baseUrl, httpProfiles, Math.Max(demo.ReportInterval, 5000), XTrace.Log);
                http.Start();
                XTrace.WriteLine("演示 HTTP 设备模拟器已启动 {0}", baseUrl);
            }

            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            foreach (var mqtt in mqttSims) await mqtt.DisposeAsync();
            if (http != null) await http.DisposeAsync();
            modbus?.Dispose();
        }
    }

    private (String? Server, String Root, Boolean UseTls) Endpoint(String brokerId)
    {
        var mqtt = options.Value.Mqtt;
        if (catalog.IsEmbedded(brokerId))
            return ($"tcp://127.0.0.1:{mqtt.Port}", catalog.TopicRoot(brokerId), false);

        var profile = mqtt.FindBroker(brokerId);
        if (profile == null) return (null, catalog.TopicRoot(brokerId), false);

        return (profile.Server, catalog.TopicRoot(brokerId), profile.UseTls);
    }

    private IEnumerable<(DeviceProfile Profile, String BrokerId)> Profiles(String productCode, String kind)
    {
        var product = Product.FindByCode(productCode);
        if (product == null) yield break;

        foreach (var d in Device.FindAllByProductId(product.Id).Where(e => e.Enable && !Skipped.Contains(e.Code)))
        {
            if (d.Secret != DataSeeder.DemoSecret(d.Code)) continue;
            yield return (new DeviceProfile(product.Code, d.Code, d.Secret, kind), catalog.Resolve(d, product));
        }
    }

    private String? GetLocalUrl()
    {
        var address = server.Features.Get<IServerAddressesFeature>()?.Addresses.FirstOrDefault(e => e.StartsWith("http://"));
        return address?.Replace("0.0.0.0", "127.0.0.1").Replace("[::]", "127.0.0.1").Replace("+", "127.0.0.1").Replace("*", "127.0.0.1");
    }
}
