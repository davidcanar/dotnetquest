namespace DotNetQuest.App.Services;

/// <summary>
/// Per-player guard against burning provider credits: a rolling per-minute burst
/// limit plus a per-UTC-day cap on tutor messages. In-memory on purpose — it resets
/// on restart, which is acceptable for a soft abuse brake (the system prompt and the
/// message clamping are the real cost controls).
/// </summary>
public sealed class TutorRateLimiter(Func<DateTimeOffset>? clock = null)
{
    private readonly Func<DateTimeOffset> _clock = clock ?? (() => DateTimeOffset.UtcNow);
    private readonly object _lock = new();
    private readonly Dictionary<string, Queue<DateTimeOffset>> _burst = new();
    private readonly Dictionary<string, (DateTimeOffset Day, int Count)> _daily = new();

    /// <summary>Records a request for <paramref name="playerId"/> and reports whether it is allowed.</summary>
    public bool TryAcquire(string playerId, int perMinute, int perDay)
    {
        var now = _clock();
        var today = now.UtcDateTime.Date;
        lock (_lock)
        {
            if (_daily.TryGetValue(playerId, out var day) && day.Day != today)
            {
                _daily.Remove(playerId);
                day = default;
            }

            var count = day == default ? 0 : day.Count;
            if (count >= perDay)
                return false;

            var window = _burst.TryGetValue(playerId, out var q) ? q : [];
            while (window.Count > 0 && now - window.Peek() > TimeSpan.FromMinutes(1))
                window.Dequeue();
            if (window.Count >= perMinute)
            {
                _burst[playerId] = window;
                return false;
            }

            window.Enqueue(now);
            _burst[playerId] = window;
            _daily[playerId] = (today, count + 1);

            if (_burst.Count > 5000 || _daily.Count > 5000)
                Prune(now, today);
            return true;
        }
    }

    private void Prune(DateTimeOffset now, DateTime today)
    {
        foreach (var (id, q) in _burst.Where(kv => kv.Value.Count == 0 || now - kv.Value.Peek() > TimeSpan.FromHours(1)).ToList())
            _burst.Remove(id);
        foreach (var (id, day) in _daily.Where(kv => kv.Value.Day != today).ToList())
            _daily.Remove(id);
    }
}
