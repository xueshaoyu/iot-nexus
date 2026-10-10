using IoTPlatform.Services;
using Microsoft.Extensions.Options;
using NewLife.Log;

namespace IoTPlatform.Web.Infrastructure;

/// <summary>在线监控：每 10 秒扫描心跳超时的设备</summary>
public class OnlineMonitorWorker(DeviceOnlineService onlineService) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            try
            {
                onlineService.CheckTimeout();
            }
            catch (Exception ex)
            {
                XTrace.WriteException(ex);
            }
        }
    }
}

/// <summary>统计与维护：每分钟汇总统计、刷新分组计数、标记超时指令，每小时清理过期数据</summary>
public class StatWorker(StatService statService, DeviceGroupService groupService, DeviceCommandService commandService, AccessLogService accessLogs, IOptions<IoTOptions> options) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var lastCleanup = DateTime.MinValue;

        // 启动后先落一次，界面立即有统计数据
        try { await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken); } catch (OperationCanceledException) { return; }

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        do
        {
            try
            {
                statService.Flush();
                groupService.RefreshCounts();
                commandService.ExpireTimeouts(TimeSpan.FromMinutes(5));

                if (DateTime.Now - lastCleanup > TimeSpan.FromHours(1))
                {
                    statService.Cleanup(options.Value.DataRetentionDays);
                    accessLogs.Cleanup(options.Value.AccessLog.RetentionDays);
                    lastCleanup = DateTime.Now;
                }
            }
            catch (Exception ex)
            {
                XTrace.WriteException(ex);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        try { statService.Flush(); } catch (Exception ex) { XTrace.WriteException(ex); }
    }
}
