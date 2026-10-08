using System.Security.Claims;
using DotNetQuest.Shared.Api;
using System.Text.Json;
using DotNetQuest.App.Data;
using Microsoft.EntityFrameworkCore;

namespace DotNetQuest.App.Endpoints;

public record ExportedData(
    string Version,
    string Nickname,
    string AvatarId,
    int XpTotal,
    int Gems,
    int StreakCurrent,
    int StreakBest,
    List<ProgressRowDto> Progress,
    List<string> Achievements);

/// <summary>Export/import of player progress as JSON (cloud-save style backup).</summary>
public static class ExportEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static void MapExportEndpoints(this WebApplication app)
    {
        app.MapGet("/api/profile/export", async (GameDb db, HttpContext ctx) =>
        {
            var player = await CurrentPlayerAsync(db, ctx);
            if (player is null) return Results.Unauthorized();

            var progress = await db.LessonProgress
                .Where(p => p.PlayerId == player.Id)
                .Select(p => new ProgressRowDto(p.WorldId, p.LessonId, p.Stars, p.Attempts, null))
                .ToListAsync();
            var achievements = await db.Achievements
                .Where(a => a.PlayerId == player.Id)
                .Select(a => a.AchievementId)
                .ToListAsync();

            var export = new ExportedData(
                "1",
                player.Nickname,
                player.AvatarId,
                player.XpTotal,
                player.Gems,
                player.StreakCurrent,
                player.StreakBest,
                progress,
                achievements);

            return Results.Text(JsonSerializer.Serialize(export, JsonOptions), "application/json");
        }).RequireAuthorization();

        app.MapPost("/api/profile/import", async (GameDb db, HttpContext ctx, JsonElement body) =>
        {
            var player = await CurrentPlayerAsync(db, ctx);
            if (player is null) return Results.Unauthorized();

            ExportedData? data;
            try
            {
                data = JsonSerializer.Deserialize<ExportedData>(body.GetRawText(), JsonOptions);
            }
            catch (JsonException)
            {
                return Results.BadRequest(new { error = "invalid-json" });
            }
            if (data is null || string.IsNullOrWhiteSpace(data.Nickname) || data.Version != "1")
                return Results.BadRequest(new { error = "invalid-format" });

            // Replace progress + achievements with the imported snapshot.
            var oldProgress = db.LessonProgress.Where(p => p.PlayerId == player.Id);
            db.LessonProgress.RemoveRange(oldProgress);
            foreach (var row in data.Progress.Take(2000))
            {
                db.LessonProgress.Add(new LessonProgress
                {
                    PlayerId = player.Id,
                    WorldId = row.WorldId,
                    LessonId = row.LessonId,
                    Stars = Math.Clamp(row.Stars, 0, 3),
                    Attempts = Math.Clamp(row.Attempts, 0, 999)
                });
            }

            var oldAch = db.Achievements.Where(a => a.PlayerId == player.Id);
            db.Achievements.RemoveRange(oldAch);
            foreach (var achId in data.Achievements.Take(500))
                db.Achievements.Add(new AchievementRow { PlayerId = player.Id, AchievementId = achId });

            player.XpTotal = Math.Clamp(data.XpTotal, 0, 10_000_000);
            player.Gems = Math.Clamp(data.Gems, 0, 1_000_000);
            player.StreakCurrent = Math.Clamp(data.StreakCurrent, 0, 100_000);
            player.StreakBest = Math.Max(player.StreakBest, player.StreakCurrent);
            player.GemsEarnedTotal = Math.Max(player.GemsEarnedTotal, player.Gems);

            await db.SaveChangesAsync();
            return Results.Ok(new { imported = data.Progress.Count, achievements = data.Achievements.Count });
        }).RequireAuthorization();
    }

    private static async Task<Player?> CurrentPlayerAsync(GameDb db, HttpContext ctx)
    {
        var subject = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(subject))
            return null;
        return await db.Players.FirstOrDefaultAsync(p => p.ExternalSubject == subject);
    }
}
