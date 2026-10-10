using System.Collections.Concurrent;

namespace IoTPlatform.Services;

/// <summary>消息计数器。内存累加，由统计任务定期落库</summary>
public class MessageCounter
{
    private sealed class Counter
    {
        public Int64 Uplink;
        public Int64 Downlink;
        public Int64 Alarms;
    }

    private readonly ConcurrentDictionary<Int32, Counter> _pending = new();
    private readonly Int64[] _buckets = new Int64[60];
    private readonly Int64[] _stamps = new Int64[60];
    private Int64 _totalUplink;
    private Int64 _totalDownlink;

    /// <summary>进程启动以来的上行消息总数</summary>
    public Int64 TotalUplink => Interlocked.Read(ref _totalUplink);

    /// <summary>进程启动以来的下行消息总数</summary>
    public Int64 TotalDownlink => Interlocked.Read(ref _totalDownlink);

    public void Uplink(Int32 productId, Int32 count = 1)
    {
        Interlocked.Add(ref Get(productId).Uplink, count);
        Interlocked.Add(ref _totalUplink, count);

        var sec = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var slot = (Int32)(sec % 60);
        if (Interlocked.Exchange(ref _stamps[slot], sec) != sec) Interlocked.Exchange(ref _buckets[slot], 0);
        Interlocked.Add(ref _buckets[slot], count);
    }

    public void Downlink(Int32 productId, Int32 count = 1)
    {
        Interlocked.Add(ref Get(productId).Downlink, count);
        Interlocked.Add(ref _totalDownlink, count);
    }

    public void Alarm(Int32 productId) => Interlocked.Increment(ref Get(productId).Alarms);

    /// <summary>最近60秒的上行消息数</summary>
    public Int64 UplinkLastMinute()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        Int64 sum = 0;
        for (var i = 0; i < 60; i++)
        {
            if (now - Interlocked.Read(ref _stamps[i]) < 60) sum += Interlocked.Read(ref _buckets[i]);
        }

        return sum;
    }

    /// <summary>尚未落库的上行消息数，productId=0 表示全部</summary>
    public Int64 PendingUplink(Int32 productId = 0) =>
        productId == 0 ? _pending.Values.Sum(e => Interlocked.Read(ref e.Uplink)) : _pending.TryGetValue(productId, out var c) ? Interlocked.Read(ref c.Uplink) : 0;

    /// <summary>取出并清零所有产品的计数</summary>
    public IDictionary<Int32, (Int64 Uplink, Int64 Downlink, Int64 Alarms)> Drain()
    {
        var result = new Dictionary<Int32, (Int64, Int64, Int64)>();
        foreach (var (productId, c) in _pending)
        {
            var up = Interlocked.Exchange(ref c.Uplink, 0);
            var down = Interlocked.Exchange(ref c.Downlink, 0);
            var alarms = Interlocked.Exchange(ref c.Alarms, 0);
            if (up + down + alarms > 0) result[productId] = (up, down, alarms);
        }

        return result;
    }

    private Counter Get(Int32 productId) => _pending.GetOrAdd(productId, _ => new Counter());
}
