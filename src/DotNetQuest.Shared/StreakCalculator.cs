namespace DotNetQuest.Shared;

/// <summary>
/// Daily-streak rules, computed in UTC. A streak continues when the player
/// earns XP on consecutive days; missing a day resets it to 1.
/// </summary>
public static class StreakCalculator
{
    /// <summary>
    /// Returns the new streak value after playing on <paramref name="todayUtc"/>.
    /// </summary>
    /// <param name="lastPlayedUtc">The last day the player earned XP (null = never).</param>
    /// <param name="currentStreak">The player's current streak.</param>
    /// <param name="streakFreezeActive">Reserved for Phase 2 (streak freeze item).</param>
    public static int NextStreak(DateOnly? lastPlayedUtc, DateOnly todayUtc, int currentStreak, bool streakFreezeActive = false)
    {
        if (lastPlayedUtc == todayUtc)
            return Math.Max(currentStreak, 1);
        if (lastPlayedUtc == todayUtc.AddDays(-1))
            return currentStreak + 1;
        if (streakFreezeActive && (lastPlayedUtc == todayUtc.AddDays(-2)))
            return currentStreak + 1;
        return 1;
    }

    public static readonly int[] Milestones = { 7, 30, 100, 365 };

    public static bool IsMilestone(int streak) => Milestones.Contains(streak);
}
