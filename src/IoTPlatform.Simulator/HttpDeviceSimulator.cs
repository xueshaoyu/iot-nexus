using System.Net.Http.Json;
using System.Text.Json;
using NewLife.Log;

namespace IoTPlatform.Simulator;

/// <summary>HTTP 设备模拟器。调用平台设备接入接口上报数据，并轮询待执行指令</summary>
public sealed class HttpDeviceSimulator(String baseUrl, IEnumerable<DeviceProfile> profiles, Int32 interval = 5000, ILog? log = null) : IAsyncDisposable
{
    private readonly List<DeviceProfile> _profiles = profiles.ToList();
    private readonly CancellationTokenSource _cts = new();
    private readonly HttpClient _http = new() { BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/"), Timeout = TimeSpan.FromSeconds(10) };
    private readonly List<Task> _tasks = [];

    public void Start()
    {
        foreach (var p in _profiles) _tasks.Add(Task.Run(() => RunAsync(p, _cts.Token)));
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        try { await Task.WhenAll(_tasks); } catch { }
        _http.Dispose();
        _cts.Dispose();
    }

    private async Task RunAsync(DeviceProfile profile, CancellationToken token)
    {
        var device = SimulatedDevice.Create(profile);
        var path = $"api/device-access/{profile.DeviceCode}/";

        try { await Task.Delay(Random.Shared.Next(1000, 3000), token); } catch (OperationCanceledException) { return; }

        while (!token.IsCancellationRequested)
        {
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Post, path + "properties") { Content = JsonContent.Create(device.NextTelemetry()) };
                req.Headers.Add("X-Device-Secret", profile.Secret);
                using var res = await _http.SendAsync(req, token);

                using var poll = new HttpRequestMessage(HttpMethod.Get, path + "commands");
                poll.Headers.Add("X-Device-Secret", profile.Secret);
                using var cmds = await _http.SendAsync(poll, token);
                if (cmds.IsSuccessStatusCode)
                {
                    var list = await cmds.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: token);
                    if (list.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var cmd in list.EnumerateArray())
                        {
                            var id = cmd.GetProperty("id").GetInt64();
                            using var reply = new HttpRequestMessage(HttpMethod.Post, path + "reply")
                            {
                                Content = JsonContent.Create(new { id, status = 2, data = "ok" }),
                            };
                            reply.Headers.Add("X-Device-Secret", profile.Secret);
                            using var _ = await _http.SendAsync(reply, token);
                        }
                    }
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                log?.Info("[Simulator] HTTP {0} error {1}", profile.DeviceCode, ex.Message);
            }

            try { await Task.Delay(interval, token); } catch (OperationCanceledException) { break; }
        }
    }
}
