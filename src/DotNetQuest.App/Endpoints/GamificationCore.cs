using DotNetQuest.App.Data;
using DotNetQuest.Shared;
using DotNetQuest.Shared.Api;
using Microsoft.EntityFrameworkCore;

namespace DotNetQuest.App.Endpoints;

/// <summary>Shared gamification logic: hearts, XP application, achievements, daily rows.</summary>
public static class GamificationCore
{
    public const int MaxHearts = 5;
    public static readonly TimeSpan HeartRegenTime = TimeSpan.FromMinutes(30);

    /// <summary>Apply heart regen based on elapsed time. Mutates the player; caller must SaveChanges.</summary>
    public static (int Hearts, int RegenSeconds) ResolveHearts(Player p)
    {
        if (p.Hearts >= MaxHearts)
        {
            p.Hearts = MaxHearts;
            p.HeartsRegenAtUtc = null;
            return (MaxHearts, 0);
        }

        var now = DateTime.UtcNow;
        var next = p.HeartsRegenAtUtc ?? now.Add(HeartRegenTime);
        if (next > now)
            return (p.Hearts, (int)(next - now).TotalSeconds + 1);

        p.Hearts = Math.Min(MaxHearts, p.Hearts + 1);
        p.HeartsRegenAtUtc = p.Hearts >= MaxHearts ? null : now.Add(HeartRegenTime);
        return (p.Hearts, p.Hearts >= MaxHearts ? 0 : (int)HeartRegenTime.TotalSeconds);
    }

    /// <summary>Lose one heart (wrong answer). Returns the new hearts state.</summary>
    public static (int Hearts, int RegenSeconds) LoseHeart(Player p)
    {
        if (p.Hearts > 0)
        {
            p.Hearts--;
            if (p.Hearts < MaxHearts)
                p.HeartsRegenAtUtc = DateTime.UtcNow.Add(HeartRegenTime);
        }
        return (p.Hearts, (int)HeartRegenTime.TotalSeconds);
    }

    public static DailyActivity EnsureDailyActivity(GameDb db, Player player, DateOnly today)
    {
        var daily = db.DailyActivity.FirstOrDefault(d => d.PlayerId == player.Id && d.DateUtc == today);
        if (daily is null)
        {
            daily = new DailyActivity { PlayerId = player.Id, DateUtc = today };
            db.DailyActivity.Add(daily);
        }
        return daily;
    }

    public static void GrantXp(Player player, DailyActivity daily, int xp)
    {
        player.XpTotal += xp;
        daily.XpEarned += xp;

        // Daily goal: one-time bonus the first day it is reached.
        if (!daily.GoalReached && daily.XpEarned >= player.DailyGoalXp)
        {
            daily.GoalReached = true;
            GrantXpRaw(player, daily, 20);
            GrantGems(player, 10);
        }
    }

    private static void GrantXpRaw(Player player, DailyActivity daily, int xp)
    {
        player.XpTotal += xp;
        daily.XpEarned += xp;
    }

    public static void GrantGems(Player player, int gems)
    {
        player.Gems += gems;
        player.GemsEarnedTotal += gems;
    }

    public static PlayerStats BuildStats(Player p) => new(
        p.LessonsCompleted,
        p.PerfectLessons,
        p.BossesDefeated,
        p.QuizzesPassed,
        p.MaxCombo,
        p.CodeRunsPassed,
        p.PlaygroundRuns,
        p.DailyChallengesDone,
        p.LightningRuns,
        p.PracticeRuns,
        p.StreakBest,
        p.XpTotal,
        XpCalculator.LevelForXp(p.XpTotal),
        p.StreakFreezeCount,
        p.GemsEarnedTotal);

    /// <summary>
    /// Evaluate achievements, persist newly unlocked rows and grant their rewards.
    /// Returns the newly unlocked definitions (XP rewards already applied).
    /// </summary>
    public static List<AchievementDef> EvaluateAchievements(GameDb db, Player player, DailyActivity daily)
    {
        var unlockedIds = db.Achievements
            .Where(a => a.PlayerId == player.Id)
            .Select(a => a.AchievementId)
            .ToHashSet();

        var newly = Achievements.Evaluate(BuildStats(player), unlockedIds);
        foreach (var def in newly)
        {
            db.Achievements.Add(new AchievementRow
            {
                PlayerId = player.Id,
                AchievementId = def.Id,
                UnlockedAtUtc = DateTime.UtcNow
            });
            GrantXp(player, daily, def.XpReward);
            GrantGems(player, def.GemReward);
        }
        return newly;
    }

    /// <summary>Ensure today's 3 quests exist, then map them to DTOs with live progress.</summary>
    public static List<QuestDto> EnsureAndGetQuests(GameDb db, Player player, DailyActivity daily, DateOnly today)
    {
        var defs = DailyQuests.PickForDay(player.Id, today);
        var rows = db.DailyQuests
            .Where(q => q.PlayerId == player.Id && q.DateUtc == today)
            .ToList();

        var result = new List<QuestDto>();
        foreach (var def in defs)
        {
            var row = rows.FirstOrDefault(r => r.QuestId == def.Id);
            if (row is null)
            {
                row = new DailyQuestRow { PlayerId = player.Id, DateUtc = today, QuestId = def.Id };
                db.DailyQuests.Add(row);
                rows.Add(row);
            }

            row.Progress = def.Metric switch
            {
                QuestMetric.XpToday => daily.XpEarned,
                QuestMetric.LessonsToday => daily.LessonsCompleted,
                QuestMetric.CorrectToday => daily.CorrectAnswers,
                QuestMetric.PerfectToday => daily.PerfectLessons,
                QuestMetric.CodeRunsToday => daily.CodeRuns,
                _ => row.Progress
            };

            result.Add(new QuestDto(def.Id, Math.Min(row.Progress, def.Target), def.Target, def.XpReward, def.GemReward, row.Claimed));
        }
        return result;
    }
}
