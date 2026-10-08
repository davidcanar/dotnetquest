using System.Security.Claims;
using DotNetQuest.App.Data;
using DotNetQuest.Shared;
using DotNetQuest.Shared.Api;
using Microsoft.EntityFrameworkCore;

namespace DotNetQuest.App.Endpoints;

/// <summary>
/// Phase 2 gamification: the daily loop (quests, goal, login bonus, daily
/// challenge), hearts refill, the gem shop, achievements, and practice /
/// lightning sessions.
/// </summary>
public static class GamificationEndpoints
{
    public static void MapGamificationEndpoints(this WebApplication app)
    {
        // ---------- Daily status ----------
        app.MapGet("/api/daily", async (GameDb db, HttpContext ctx) =>
        {
            var player = await CurrentPlayerAsync(db, ctx);
            if (player is null) return Results.Unauthorized();

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var daily = GamificationCore.EnsureDailyActivity(db, player, today);
            var quests = GamificationCore.EnsureAndGetQuests(db, player, daily, today);

            LoginBonusDto? loginBonus = null;
            if (player.LastLoginBonusDateUtc != today)
            {
                var day = player.LastLoginBonusDateUtc == today.AddDays(-1)
                    ? player.LoginBonusDay % Shop.LoginBonusDays.Length + 1
                    : 1;
                var (gems, xp) = Shop.LoginBonusDays[day - 1];
                loginBonus = new LoginBonusDto(day, gems, xp);
            }

            await db.SaveChangesAsync();
            return Results.Ok(new DailyStatusDto(
                quests,
                loginBonus,
                daily.ChallengeDone,
                daily.XpEarned,
                player.DailyGoalXp,
                daily.GoalReached,
                XpCalculator.IsDoubleXpDay(today)));
        }).RequireAuthorization();

        // ---------- Quests ----------
        app.MapPost("/api/quests/claim", async (GameDb db, HttpContext ctx, string questId) =>
        {
            var player = await CurrentPlayerAsync(db, ctx);
            if (player is null) return Results.Unauthorized();

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var daily = GamificationCore.EnsureDailyActivity(db, player, today);
            var quests = GamificationCore.EnsureAndGetQuests(db, player, daily, today);
            var quest = quests.FirstOrDefault(q => q.QuestId == questId);
            if (quest is null)
                return Results.NotFound(new { error = "unknown-quest" });
            if (quest.Claimed)
                return Results.Conflict(new { error = "already-claimed" });
            if (quest.Progress < quest.Target)
                return Results.Conflict(new { error = "not-complete" });

            var row = await db.DailyQuests
                .FirstAsync(q => q.PlayerId == player.Id && q.DateUtc == today && q.QuestId == questId);
            row.Claimed = true;

            GamificationCore.GrantXp(player, daily, quest.XpReward);
            GamificationCore.GrantGems(player, quest.GemReward);
            await db.SaveChangesAsync();

            return Results.Ok(new ClaimQuestResponse(GameEndpoints.ToDto(player), quest, quest.XpReward));
        }).RequireAuthorization();

        // ---------- Login bonus ----------
        app.MapPost("/api/login-bonus/claim", async (GameDb db, HttpContext ctx) =>
        {
            var player = await CurrentPlayerAsync(db, ctx);
            if (player is null) return Results.Unauthorized();

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            if (player.LastLoginBonusDateUtc == today)
                return Results.Conflict(new { error = "already-claimed" });

            var day = player.LastLoginBonusDateUtc == today.AddDays(-1)
                ? player.LoginBonusDay % Shop.LoginBonusDays.Length + 1
                : 1;
            var (gems, xp) = Shop.LoginBonusDays[day - 1];

            player.LoginBonusDay = day;
            player.LastLoginBonusDateUtc = today;

            var daily = GamificationCore.EnsureDailyActivity(db, player, today);
            GamificationCore.GrantXp(player, daily, xp);
            GamificationCore.GrantGems(player, gems);
            GamificationCore.EvaluateAchievements(db, player, daily);
            await db.SaveChangesAsync();

            return Results.Ok(new ClaimLoginBonusResponse(GameEndpoints.ToDto(player), day, gems, xp));
        }).RequireAuthorization();

        // ---------- Hearts & shop ----------
        app.MapPost("/api/shop/buy", async (GameDb db, HttpContext ctx, ShopBuyRequest request) =>
        {
            var player = await CurrentPlayerAsync(db, ctx);
            if (player is null) return Results.Unauthorized();

            switch (request.ItemId)
            {
                case "heart-refill":
                    if (player.Gems < Shop.HeartRefillCost)
                        return Conflict("not-enough-gems");
                    player.Gems -= Shop.HeartRefillCost;
                    player.Hearts = GamificationCore.MaxHearts;
                    player.HeartsRegenAtUtc = null;
                    break;

                case "streak-freeze":
                    if (player.StreakFreezeCount >= Shop.MaxStreakFreezes)
                        return Conflict("max-freezes");
                    if (player.Gems < Shop.StreakFreezeCost)
                        return Conflict("not-enough-gems");
                    player.Gems -= Shop.StreakFreezeCost;
                    player.StreakFreezeCount++;
                    break;

                default:
                    return Results.BadRequest(new { error = "unknown-item" });
            }

            await db.SaveChangesAsync();
            return Results.Ok(new ShopBuyResponse(GameEndpoints.ToDto(player), player.Hearts, player.StreakFreezeCount));
        }).RequireAuthorization();

        // ---------- Achievements ----------
        app.MapGet("/api/achievements", async (GameDb db, HttpContext ctx) =>
        {
            var player = await CurrentPlayerAsync(db, ctx);
            if (player is null) return Results.Unauthorized();

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var daily = GamificationCore.EnsureDailyActivity(db, player, today);
            // Checking in also unlocks anything already earned but not yet persisted.
            GamificationCore.EvaluateAchievements(db, player, daily);
            await db.SaveChangesAsync();

            var unlocked = await db.Achievements
                .Where(a => a.PlayerId == player.Id)
                .Select(a => a.AchievementId)
                .ToListAsync();
            var set = unlocked.ToHashSet();
            return Results.Ok(Achievements.All.Select(a => new AchievementStateDto(a.Id, set.Contains(a.Id))).ToList());
        }).RequireAuthorization();

        // ---------- Lightning mode (60s sprint) ----------
        app.MapPost("/api/sessions/lightning", async (GameDb db, HttpContext ctx, SessionCompleteRequest request) =>
        {
            var player = await CurrentPlayerAsync(db, ctx);
            if (player is null) return Results.Unauthorized();

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var daily = GamificationCore.EnsureDailyActivity(db, player, today);
            if (daily.LightningRuns >= 3)
                return Results.Ok(new SessionCompleteResponse(GameEndpoints.ToDto(player), 0, false, CapReached: true));

            var correct = Math.Clamp(request.Correct, 0, 100);
            var doubleXp = XpCalculator.IsDoubleXpDay(today);
            var xp = Math.Min(correct * 2, 40) * (doubleXp ? XpCalculator.WeekendMultiplier : 1);

            player.LightningRuns++;
            daily.LightningRuns++;
            GamificationCore.GrantXp(player, daily, xp);
            GamificationCore.EvaluateAchievements(db, player, daily);
            await db.SaveChangesAsync();

            return Results.Ok(new SessionCompleteResponse(GameEndpoints.ToDto(player), xp, doubleXp, daily.LightningRuns >= 3));
        }).RequireAuthorization();

        // ---------- Practice (spaced repetition) ----------
        app.MapGet("/api/practice/exercises", async (GameDb db, HttpContext ctx) =>
        {
            var player = await CurrentPlayerAsync(db, ctx);
            if (player is null) return Results.Unauthorized();

            var missed = await db.ExerciseAttempts
                .Where(a => a.PlayerId == player.Id && !a.Correct)
                .OrderByDescending(a => a.Id)
                .Take(120)
                .Select(a => new { a.WorldId, a.LessonId, a.ExerciseId })
                .ToListAsync();

            var result = missed
                .DistinctBy(a => new { a.WorldId, a.ExerciseId })
                .Take(15)
                .Select(a => new PracticeExerciseDto(a.WorldId, a.LessonId, a.ExerciseId))
                .ToList();
            return Results.Ok(result);
        }).RequireAuthorization();

        app.MapPost("/api/sessions/practice", async (GameDb db, HttpContext ctx, SessionCompleteRequest request) =>
        {
            var player = await CurrentPlayerAsync(db, ctx);
            if (player is null) return Results.Unauthorized();

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var daily = GamificationCore.EnsureDailyActivity(db, player, today);
            if (daily.PracticeRuns >= 3)
                return Results.Ok(new SessionCompleteResponse(GameEndpoints.ToDto(player), 0, false, CapReached: true));

            var correct = Math.Clamp(request.Correct, 0, 100);
            var doubleXp = XpCalculator.IsDoubleXpDay(today);
            var xp = Math.Min(correct, 25) * (doubleXp ? XpCalculator.WeekendMultiplier : 1);

            player.PracticeRuns++;
            daily.PracticeRuns++;
            GamificationCore.GrantXp(player, daily, xp);
            GamificationCore.EvaluateAchievements(db, player, daily);
            await db.SaveChangesAsync();

            return Results.Ok(new SessionCompleteResponse(GameEndpoints.ToDto(player), xp, doubleXp, daily.PracticeRuns >= 3));
        }).RequireAuthorization();

        // ---------- Bit's Daily Challenge ----------
        app.MapPost("/api/daily-challenge/complete", async (GameDb db, HttpContext ctx) =>
        {
            var player = await CurrentPlayerAsync(db, ctx);
            if (player is null) return Results.Unauthorized();

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var daily = GamificationCore.EnsureDailyActivity(db, player, today);
            if (daily.ChallengeDone)
                return Results.Ok(new ChallengeCompleteResponse(GameEndpoints.ToDto(player), 0, 0));

            var doubleXp = XpCalculator.IsDoubleXpDay(today);
            var xp = Shop.DailyChallengeXp * (doubleXp ? XpCalculator.WeekendMultiplier : 1);
            daily.ChallengeDone = true;
            player.DailyChallengesDone++;
            GamificationCore.GrantXp(player, daily, xp);
            GamificationCore.GrantGems(player, Shop.DailyChallengeGems);
            GamificationCore.EvaluateAchievements(db, player, daily);
            await db.SaveChangesAsync();

            return Results.Ok(new ChallengeCompleteResponse(GameEndpoints.ToDto(player), xp, Shop.DailyChallengeGems));
        }).RequireAuthorization();

        // ---------- Practice exercise pool ----------
    }

    private static IResult Conflict(string error) => Results.Conflict(new { error });

    private static async Task<Player?> CurrentPlayerAsync(GameDb db, HttpContext ctx)
    {
        var subject = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(subject))
            return null;
        return await db.Players.FirstOrDefaultAsync(p => p.ExternalSubject == subject);
    }
}
