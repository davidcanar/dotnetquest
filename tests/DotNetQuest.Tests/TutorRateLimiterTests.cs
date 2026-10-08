using DotNetQuest.App.Services;
using Xunit;

namespace DotNetQuest.Tests;

/// <summary>
/// The tutor abuse brake: burst window blocks message floods, the window slides,
/// and the daily cap stops slow-drip all-day usage.
/// </summary>
public class TutorRateLimiterTests
{
    [Fact]
    public void Blocks_requests_past_the_per_minute_limit()
    {
        var limiter = new TutorRateLimiter();

        for (var i = 0; i < 12; i++)
            Assert.True(limiter.TryAcquire("p1", perMinute: 12, perDay: 300));
        Assert.False(limiter.TryAcquire("p1", perMinute: 12, perDay: 300));
    }

    [Fact]
    public void Limits_are_per_player()
    {
        var limiter = new TutorRateLimiter();

        Assert.True(limiter.TryAcquire("p1", perMinute: 1, perDay: 300));
        Assert.False(limiter.TryAcquire("p1", perMinute: 1, perDay: 300));
        Assert.True(limiter.TryAcquire("p2", perMinute: 1, perDay: 300));
    }

    [Fact]
    public void Daily_cap_stops_usage_even_with_a_fresh_window()
    {
        var limiter = new TutorRateLimiter();

        // Each call is minutes apart conceptually — simulate by using a huge
        // per-minute budget so only the daily cap can trip.
        for (var i = 0; i < 5; i++)
            Assert.True(limiter.TryAcquire("p1", perMinute: 1000, perDay: 5));
        Assert.False(limiter.TryAcquire("p1", perMinute: 1000, perDay: 5));
    }

    [Fact]
    public void Window_slides_after_a_minute_of_silence()
    {
        var now = DateTimeOffset.UtcNow;
        var limiter = new TutorRateLimiter(() => now);

        Assert.True(limiter.TryAcquire("p1", perMinute: 1, perDay: 300));
        Assert.False(limiter.TryAcquire("p1", perMinute: 1, perDay: 300));

        now += TimeSpan.FromMinutes(1.1);
        Assert.True(limiter.TryAcquire("p1", perMinute: 1, perDay: 300));
    }

    [Fact]
    public void Daily_cap_resets_the_next_day()
    {
        var now = DateTimeOffset.UtcNow;
        var limiter = new TutorRateLimiter(() => now);

        Assert.True(limiter.TryAcquire("p1", perMinute: 1000, perDay: 1));
        Assert.False(limiter.TryAcquire("p1", perMinute: 1000, perDay: 1));

        now = now.AddDays(1);
        Assert.True(limiter.TryAcquire("p1", perMinute: 1000, perDay: 1));
    }
}
