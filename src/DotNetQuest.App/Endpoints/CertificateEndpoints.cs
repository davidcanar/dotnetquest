using System.Security.Claims;
using DotNetQuest.App.Data;
using DotNetQuest.App.Services;
using DotNetQuest.Shared.Api;
using Microsoft.EntityFrameworkCore;

namespace DotNetQuest.App.Endpoints;

public record CertificateIssueRequest(
    string WorldTitleEn,
    string WorldTitleEs,
    int LessonsTotal,
    int Stars,
    int XpEarned,
    bool IsGrand);

/// <summary>
/// World-completion certificates: server re-validates progress against the
/// lesson counts reported by the content (the client owns the content packs),
/// then generates a QuestPDF certificate decorated with Bit.
/// </summary>
public static class CertificateEndpoints
{
    public static void MapCertificateEndpoints(this WebApplication app)
    {
        // Which certificates has this player already earned?
        app.MapGet("/api/certificates", async (GameDb db, HttpContext ctx) =>
        {
            var player = await CurrentPlayerAsync(db, ctx);
            if (player is null) return Results.Unauthorized();

            var issued = await db.Certificates
                .Where(c => c.PlayerId == player.Id)
                .Select(c => new CertificateStatusDto(c.WorldId, Eligible: true, Issued: true,
                    c.IssuedAtUtc.ToString("O")))
                .ToListAsync();
            return Results.Ok(issued);
        }).RequireAuthorization();

        // Download (and on first call, issue) a certificate.
        // The client reports the world's lesson count so the server can verify completion.
        app.MapGet("/api/certificates/{worldId}", async (GameDb db, HttpContext ctx,
            string worldId, int lessons, string? titleEn, string? titleEs, bool grand,
            int stars, int xp, string? lang) =>
        {
            var player = await CurrentPlayerAsync(db, ctx);
            if (player is null) return Results.Unauthorized();

            if (lessons is < 1 or > 500)
                return Results.BadRequest(new { error = "invalid-lessons" });

            var completed = await db.LessonProgress
                .Where(p => p.PlayerId == player.Id && p.WorldId == worldId)
                .CountAsync();

            if (completed < lessons)
                return Results.Conflict(new { error = "not-completed", completed, lessons });

            var language = string.Equals(lang, "es", StringComparison.OrdinalIgnoreCase) ? "es" : "en";
            var playerName = string.IsNullOrWhiteSpace(player.CertificateName) ? player.Nickname : player.CertificateName!;

            var row = await db.Certificates
                .FirstOrDefaultAsync(c => c.PlayerId == player.Id && c.WorldId == worldId);
            if (row is null)
            {
                row = new CertificateRow
                {
                    PlayerId = player.Id,
                    WorldId = worldId,
                    IssuedToName = playerName,
                    XpAtIssue = player.XpTotal,
                    IssuedAtUtc = DateTime.UtcNow
                };
                db.Certificates.Add(row);
                await db.SaveChangesAsync();
            }

            var data = new CertificateData(
                row.IssuedToName,
                language == "es" ? (titleEs ?? titleEn ?? worldId) : (titleEn ?? titleEs ?? worldId),
                language,
                Math.Clamp(stars, 0, 100),
                StarsTotal: lessons * 3,
                Math.Clamp(xp, 0, 1_000_000),
                lessons,
                DateOnly.FromDateTime(row.IssuedAtUtc),
                grand);

            var pdf = CertificateService.Generate(data);
            var fileName = $"devquest-{(grand ? "graduation" : worldId)}.pdf";
            return Results.File(pdf, "application/pdf", fileName);
        }).RequireAuthorization();
    }

    private static async Task<Player?> CurrentPlayerAsync(GameDb db, HttpContext ctx)
    {
        var subject = ctx.User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(subject))
            return null;
        return await db.Players.FirstOrDefaultAsync(p => p.ExternalSubject == subject);
    }
}
