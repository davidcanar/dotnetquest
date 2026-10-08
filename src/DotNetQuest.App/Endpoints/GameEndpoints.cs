using System.Security.Claims;
using System.Text.Json;
using DotNetQuest.App.Data;
using DotNetQuest.App.Services;
using DotNetQuest.Shared;
using DotNetQuest.Shared.Api;
using DotNetQuest.Shared.Content;
using DotNetQuest.Shared.Runner;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DotNetQuest.App.Endpoints;

public static class GameEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private const string SettingsPlaceholder = "{}";

    public static void MapGameEndpoints(this WebApplication app)
    {
        // ---------- Auth ----------
        app.MapGet("/login-google", (string? returnUrl) =>
        {
            var props = new AuthenticationProperties { RedirectUri = string.IsNullOrWhiteSpace(returnUrl) ? "/" : returnUrl };
            return Results.Challenge(props, [GoogleDefaults.AuthenticationScheme]);
        }).AllowAnonymous();

        app.MapPost("/logout", async (HttpContext ctx) =>
        {
            await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.Redirect("/");
        });

        // Development-only login so the game is testable before Google credentials exist.
        app.MapPost("/api/dev-login", async (GameDb db, HttpContext ctx, IHostEnvironment env) =>
        {
            if (!env.IsDevelopment())
                return Results.NotFound();

            var subject = "dev|" + Guid.NewGuid().ToString("N");
            var player = new Player
            {
                ExternalSubject = subject,
                Email = "dev@localhost",
                Nickname = "dev-" + subject[4..10],
                SettingsJson = SettingsPlaceholder
            };
            db.Players.Add(player);
            await db.SaveChangesAsync();
            await SignInPlayerAsync(ctx, player);
            return Results.Ok(ToDto(player));
        }).AllowAnonymous();

        // ---------- Me / onboarding / profile ----------
        app.MapGet("/api/me", async (GameDb db, HttpContext ctx, AiTutorService tutor, IConfiguration cfg) =>
        {
            var player = await CurrentPlayerAsync(db, ctx);
            int hearts = 5, regen = 0;
            if (player is not null)
            {
                (hearts, regen) = GamificationCore.ResolveHearts(player);
                await db.SaveChangesAsync();
            }
            return Results.Ok(new MeResponse(
                player is null ? null : ToDto(player, hearts, regen),
                tutor.IsConfigured,
                GoogleConfigured(cfg)));
        }).AllowAnonymous();

        app.MapGet("/api/onboarding/check-nickname", async (GameDb db, string nickname, HttpContext ctx) =>
        {
            var normalized = nickname.Trim();
            if (normalized.Length is < 2 or > 24)
                return Results.Ok(new NicknameCheckResponse(false, "length"));

            if (!IsValidNickname(normalized))
                return Results.Ok(new NicknameCheckResponse(false, "characters"));

            var player = await CurrentPlayerAsync(db, ctx);
            var taken = await db.Players.AnyAsync(p => p.Nickname.ToLower() == normalized.ToLower() && (player == null || p.Id != player.Id));
            return Results.Ok(new NicknameCheckResponse(!taken, taken ? "taken" : null));
        }).RequireAuthorization();

        app.MapPost("/api/onboarding", async (GameDb db, HttpContext ctx, OnboardingRequest request) =>
        {
            var player = await CurrentPlayerAsync(db, ctx);
            if (player is null) return Results.Unauthorized();

            var nickname = request.Nickname.Trim();
            if (nickname.Length is < 2 or > 24 || !IsValidNickname(nickname))
                return Results.BadRequest(new { error = "invalid-nickname" });

            var taken = await db.Players.AnyAsync(p => p.Nickname.ToLower() == nickname.ToLower() && p.Id != player.Id);
            if (taken) return Results.Conflict(new { error = "nickname-taken" });

            player.Nickname = nickname;
            player.AvatarId = string.IsNullOrWhiteSpace(request.AvatarId) ? "bit-classic" : request.AvatarId.Trim();
            player.OnboardingComplete = true;
            if (!string.IsNullOrWhiteSpace(request.Language))
                player.SettingsJson = JsonSerializer.Serialize(
                    new PlayerSettingsDto(request.Language, AutoSpeak: true, Sound: true, DarkMode: false), JsonOptions);

            await db.SaveChangesAsync();
            return Results.Ok(ToDto(player));
        }).RequireAuthorization();

        app.MapPut("/api/profile", async (GameDb db, HttpContext ctx, ProfileUpdateRequest request) =>
        {
            var player = await CurrentPlayerAsync(db, ctx);
            if (player is null) return Results.Unauthorized();

            if (!string.IsNullOrWhiteSpace(request.Nickname))
            {
                var nickname = request.Nickname.Trim();
                if (nickname.Length is < 2 or > 24 || !IsValidNickname(nickname))
                    return Results.BadRequest(new { error = "invalid-nickname" });
                var taken = await db.Players.AnyAsync(p => p.Nickname.ToLower() == nickname.ToLower() && p.Id != player.Id);
                if (taken) return Results.Conflict(new { error = "nickname-taken" });
                player.Nickname = nickname;
            }

            if (!string.IsNullOrWhiteSpace(request.AvatarId))
                player.AvatarId = request.AvatarId.Trim();

            if (request.CertificateName is not null)
                player.CertificateName = request.CertificateName.Trim() is { Length: > 0 } certName ? certName : null;

            if (request.Settings is not null)
                player.SettingsJson = JsonSerializer.Serialize(request.Settings, JsonOptions);

            if (request.DailyGoalXp is { } goal)
                player.DailyGoalXp = Math.Clamp(goal, 20, 200);

            await db.SaveChangesAsync();
            return Results.Ok(ToDto(player));
        }).RequireAuthorization();

        // ---------- Progress & XP ----------
        app.MapGet("/api/progress", async (GameDb db, HttpContext ctx) =>
        {
            var player = await CurrentPlayerAsync(db, ctx);
            if (player is null) return Results.Unauthorized();

            var rows = await db.LessonProgress
                .Where(p => p.PlayerId == player.Id)
                .Select(p => new ProgressRowDto(p.WorldId, p.LessonId, p.Stars, p.Attempts,
                    p.CompletedAtUtc.HasValue ? p.CompletedAtUtc.Value.ToString("O") : null))
                .ToListAsync();
            return Results.Ok(rows);
        }).RequireAuthorization();

        app.MapPost("/api/progress/lesson-complete", async (GameDb db, HttpContext ctx, LessonCompleteRequest request) =>
        {
            var player = await CurrentPlayerAsync(db, ctx);
            if (player is null) return Results.Unauthorized();

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var daily = GamificationCore.EnsureDailyActivity(db, player, today);

            // ----- XP: base + combo bonus, doubled on weekends -----
            var baseXp = XpCalculator.LessonXpFromParts(request.ExerciseXp, request.Kind, request.Mistakes);
            var comboBonus = XpCalculator.ComboBonus(request.MaxCombo);
            if (request.MaxCombo > player.MaxCombo)
                player.MaxCombo = request.MaxCombo;
            var doubleXp = XpCalculator.IsDoubleXpDay(today);
            var xpEarned = (baseXp + comboBonus) * (doubleXp ? XpCalculator.WeekendMultiplier : 1);

            // ----- Streak (auto-consume a streak freeze if one bridges the gap) -----
            var streakBefore = player.StreakCurrent;
            var freezeAvailable = player.StreakFreezeCount > 0;
            player.StreakCurrent = StreakCalculator.NextStreak(player.LastPlayedUtc, today, player.StreakCurrent, freezeAvailable);
            if (freezeAvailable && player.LastPlayedUtc == today.AddDays(-2) && player.StreakCurrent > streakBefore)
                player.StreakFreezeCount--;
            player.StreakBest = Math.Max(player.StreakBest, player.StreakCurrent);
            player.LastPlayedUtc = today;

            // ----- Progress row -----
            var row = await db.LessonProgress
                .FirstOrDefaultAsync(p => p.PlayerId == player.Id && p.WorldId == request.WorldId && p.LessonId == request.LessonId);
            var isNewCompletion = row is null;
            if (row is null)
            {
                row = new LessonProgress { PlayerId = player.Id, WorldId = request.WorldId, LessonId = request.LessonId };
                db.LessonProgress.Add(row);
            }

            row.Attempts += 1;
            row.Mistakes = request.Mistakes;
            row.Stars = XpCalculator.Stars(request.Mistakes);
            row.XpEarned = Math.Max(row.XpEarned, xpEarned);
            row.CompletedAtUtc = DateTime.UtcNow;

            // ----- Counters -----
            if (isNewCompletion)
            {
                player.LessonsCompleted++;
                daily.LessonsCompleted += 1;
            }
            var stars = row.Stars;
            var perfect = stars == 3;
            if (perfect)
            {
                player.PerfectLessons++;
                daily.PerfectLessons++;
            }
            if (request.Kind == LessonKinds.Boss) player.BossesDefeated++;
            if (request.Kind == LessonKinds.Quiz) player.QuizzesPassed++;

            // ----- Apply XP (also handles the daily goal bonus), then achievements -----
            GamificationCore.GrantXp(player, daily, xpEarned);
            GamificationCore.EvaluateAchievements(db, player, daily);

            var newLevel = XpCalculator.LevelForXp(player.XpTotal);
            await db.SaveChangesAsync();

            return Results.Ok(new LessonCompleteResponse(
                ToDto(player),
                new ProgressRowDto(row.WorldId, row.LessonId, row.Stars, row.Attempts, row.CompletedAtUtc!.Value.ToString("O")),
                xpEarned,
                StreakCalculator.IsMilestone(player.StreakCurrent),
                newLevel,
                doubleXp,
                comboBonus,
                perfect ? XpCalculator.PerfectLessonBonus : 0));
        }).RequireAuthorization();

        app.MapPost("/api/progress/exercise-attempt", async (GameDb db, HttpContext ctx, ExerciseAttemptRequest request) =>
        {
            var player = await CurrentPlayerAsync(db, ctx);
            if (player is null) return Results.Unauthorized();

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var daily = GamificationCore.EnsureDailyActivity(db, player, today);
            db.ExerciseAttempts.Add(new ExerciseAttempt
            {
                PlayerId = player.Id,
                WorldId = request.WorldId,
                LessonId = request.LessonId,
                ExerciseId = request.ExerciseId,
                Correct = request.Correct
            });

            HeartsDto hearts;
            if (request.Correct)
            {
                daily.CorrectAnswers += 1;
                hearts = new HeartsDto(player.Hearts, 0);
            }
            else
            {
                var (h, regen) = GamificationCore.LoseHeart(player);
                hearts = new HeartsDto(h, regen);
            }

            await db.SaveChangesAsync();
            return Results.Ok(hearts);
        }).RequireAuthorization();

        // ---------- Leaderboard (all-time + weekly) ----------
        app.MapGet("/api/leaderboard", async (GameDb db, HttpContext ctx, string? range) =>
        {
            var player = await CurrentPlayerAsync(db, ctx);
            var weekly = string.Equals(range, "week", StringComparison.OrdinalIgnoreCase);

            List<Player> top;
            if (weekly)
            {
                var monday = DateOnly.FromDateTime(DateTime.UtcNow);
                var daysBack = ((int)monday.DayOfWeek + 6) % 7; // Monday-based week
                monday = monday.AddDays(-daysBack);
                var weekXp = await db.DailyActivity
                    .Where(d => d.DateUtc >= monday)
                    .GroupBy(d => d.PlayerId)
                    .Select(g => new { PlayerId = g.Key, Xp = g.Sum(x => x.XpEarned) })
                    .ToListAsync();
                var playerIds = await db.Players.Where(p => p.OnboardingComplete).Select(p => new { p.Id, p.XpTotal }).ToDictionaryAsync(p => p.Id, p => p.XpTotal);
                var ranked = weekXp
                    .Where(w => playerIds.ContainsKey(w.PlayerId))
                    .Select(w => new { w.PlayerId, Xp = w.Xp + playerIds[w.PlayerId] })
                    .OrderByDescending(x => x.Xp)
                    .Take(10)
                    .ToList();
                var topPlayers = await db.Players
                    .Where(p => ranked.Select(r => r.PlayerId).Contains(p.Id))
                    .ToDictionaryAsync(p => p.Id);
                top = ranked
                    .Where(r => topPlayers.ContainsKey(r.PlayerId))
                    .Select(r =>
                    {
                        var p = topPlayers[r.PlayerId];
                        p.XpTotal = r.Xp; // projection only, not saved
                        return p;
                    })
                    .ToList();
            }
            else
            {
                top = await db.Players
                    .Where(p => p.OnboardingComplete)
                    .OrderByDescending(p => p.XpTotal)
                    .ThenBy(p => p.CreatedAtUtc)
                    .Take(10)
                    .ToListAsync();
            }

            var entries = top.Select((p, i) => new LeaderboardEntryDto(
                i + 1, p.Id.ToString(), p.Nickname, p.AvatarId, p.XpTotal, p.StreakCurrent,
                XpCalculator.LevelForXp(p.XpTotal), player != null && p.Id == player.Id)).ToList();

            LeaderboardEntryDto? me = null;
            if (player is { OnboardingComplete: true } && top.All(p => p.Id != player.Id))
            {
                int myXp = player.XpTotal;
                int rank;
                if (weekly)
                {
                    var monday = DateOnly.FromDateTime(DateTime.UtcNow);
                    var daysBack = ((int)monday.DayOfWeek + 6) % 7;
                    monday = monday.AddDays(-daysBack);
                    var myWeekXp = await db.DailyActivity
                        .Where(d => d.PlayerId == player.Id && d.DateUtc >= monday)
                        .SumAsync(x => (int?)x.XpEarned) ?? 0;
                    myXp += myWeekXp;
                    var others = await db.Players
                        .Where(p => p.OnboardingComplete && p.Id != player.Id)
                        .Select(p => new { p.Id, p.XpTotal })
                        .ToListAsync();
                    var weekSums = await db.DailyActivity
                        .Where(d => d.DateUtc >= monday)
                        .GroupBy(d => d.PlayerId)
                        .Select(g => new { g.Key, Xp = g.Sum(x => x.XpEarned) })
                        .ToListAsync();
                    var sumById = weekSums.ToDictionary(x => x.Key, x => x.Xp);
                    var better = others.Count(o => o.XpTotal + sumById.GetValueOrDefault(o.Id) > myXp);
                    rank = better + 1;
                }
                else
                {
                    rank = await db.Players.CountAsync(p => p.OnboardingComplete && p.XpTotal > player.XpTotal) + 1;
                }
                me = new LeaderboardEntryDto(rank, player.Id.ToString(), player.Nickname, player.AvatarId,
                    myXp, player.StreakCurrent, XpCalculator.LevelForXp(player.XpTotal), true);
            }

            return Results.Ok(new LeaderboardResponse(entries, me));
        }).AllowAnonymous();

        // ---------- Code runner ----------
        app.MapPost("/api/code/run", async (CodeRunnerService runner, GameDb db, HttpContext ctx, CodeRunRequest request) =>
        {
            var player = await CurrentPlayerAsync(db, ctx);
            if (player is null) return Results.Unauthorized();

            if (string.IsNullOrWhiteSpace(request.Code) || request.Code.Length > 20_000)
                return Results.BadRequest(new { error = "The program is empty or too long." });

            var tests = (request.Tests ?? [])
                .Select(t => new RunTest(t.Name, t.ExpectedOutput, t.Match, t.Stdin, t.ApiPath, t.ApiMethod, t.ExpectedStatus))
                .ToList();
            var result = await runner.RunAsync(request.Code, tests, request.Stdin, request.Xunit);

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var daily = GamificationCore.EnsureDailyActivity(db, player, today);
            var passed = result.Compiled && result.AllTestsPassed;

            if (request.ExerciseId is { Length: > 0 })
            {
                db.CodeSubmissions.Add(new CodeSubmission
                {
                    PlayerId = player.Id,
                    ExerciseId = request.ExerciseId,
                    Passed = passed
                });
                if (passed)
                {
                    player.CodeRunsPassed++;
                    daily.CodeRuns++;
                }
            }
            else
            {
                // Free playground run.
                player.PlaygroundRuns++;
                GamificationCore.EvaluateAchievements(db, player, daily);
            }

            await db.SaveChangesAsync();
            return Results.Ok(ToDto(result));
        }).RequireAuthorization();

        // ---------- AI tutor ----------
        // Both endpoints are rate-limited per player so Bit can't be used as a free,
        // unlimited general chatbot (costs real provider credits).
        static IResult? CheckTutorRate(TutorRateLimiter limiter, IOptionsMonitor<AiTutorOptions> opts, Player? player) =>
            player is not null && !limiter.TryAcquire(player.Id.ToString(),
                opts.CurrentValue.RateLimitPerMinute, opts.CurrentValue.RateLimitPerDay)
                ? Results.Json(new { error = "tutor-rate-limited" }, statusCode: 429)
                : null;

        app.MapPost("/api/tutor/chat", async (AiTutorService tutor, TutorRateLimiter limiter,
            IOptionsMonitor<AiTutorOptions> tutorOpts, GameDb db, TutorChatRequest request, HttpContext ctx) =>
        {
            if (ctx.User.Identity?.IsAuthenticated != true)
                return Results.Unauthorized();
            var player = await CurrentPlayerAsync(db, ctx);
            if (player is null) return Results.Unauthorized();
            if (CheckTutorRate(limiter, tutorOpts, player) is { } limited)
                return limited;
            return Results.Ok(await tutor.ChatAsync(request));
        }).RequireAuthorization();

        app.MapPost("/api/tutor/chat/stream", async (AiTutorService tutor, TutorRateLimiter limiter,
            IOptionsMonitor<AiTutorOptions> tutorOpts, GameDb db, TutorChatRequest request, HttpContext ctx, CancellationToken ct) =>
        {
            if (ctx.User.Identity?.IsAuthenticated != true)
                return Results.Unauthorized();
            var player = await CurrentPlayerAsync(db, ctx);
            if (player is null) return Results.Unauthorized();
            if (CheckTutorRate(limiter, tutorOpts, player) is { } limited)
                return limited;

            ctx.Response.Headers.ContentType = "text/event-stream";
            ctx.Response.Headers.CacheControl = "no-cache";

            try
            {
                await foreach (var delta in tutor.ChatStreamAsync(request, ct))
                {
                    await ctx.Response.WriteAsync($"data:{JsonSerializer.Serialize(delta, JsonOptions)}\n\n", ct);
                    await ctx.Response.Body.FlushAsync(ct);
                }
                await ctx.Response.WriteAsync("data:[DONE]\n\n", ct);
                await ctx.Response.Body.FlushAsync(ct);
            }
            catch (OperationCanceledException)
            {
                // Client disconnected — stop streaming.
            }
            return Results.Empty;
        }).RequireAuthorization();

        // Tutor diagnostics: is the provider configured, and why did it last fail?
        app.MapGet("/api/tutor/status", (AiTutorService tutor) =>
            Results.Ok(new TutorStatusDto(tutor.IsConfigured, tutor.IsConfigured ? tutor.LastError : "not-configured")))
            .RequireAuthorization();

        // Hint economy: the first 3 hints per lesson are free (tracked client-side);
        // extra ones cost gems.
        app.MapPost("/api/tutor/hint-consume", async (GameDb db, HttpContext ctx) =>
        {
            var player = await CurrentPlayerAsync(db, ctx);
            if (player is null) return Results.Unauthorized();

            if (player.Gems < Shop.HintCost)
                return Results.Conflict(new { error = "not-enough-gems" });

            player.Gems -= Shop.HintCost;
            await db.SaveChangesAsync();
            return Results.Ok(ToDto(player));
        }).RequireAuthorization();
    }

    private static CodeRunResultDto ToDto(RunResult r) => new(
        r.Compiled,
        r.Diagnostics,
        r.Output,
        r.Tests.Select(t => new RunTestResultDto(t.Name, t.Passed, t.Expected, t.Actual)).ToList(),
        r.TimedOut,
        r.ElapsedMs,
        r.Error);

    public static PlayerDto ToDto(Player p, int? hearts = null, int regenSeconds = 0)
    {
        PlayerSettingsDto settings;
        try
        {
            settings = JsonSerializer.Deserialize<PlayerSettingsDto>(p.SettingsJson, JsonOptions)
                       ?? new PlayerSettingsDto(null, false, true, false);
        }
        catch (JsonException)
        {
            settings = new PlayerSettingsDto(null, false, true, false);
        }

        var (heartsValue, regen) = (hearts, regenSeconds) switch
        {
            (int h, int r) => (h, r),
            _ => GamificationCore.ResolveHearts(p)
        };

        return new PlayerDto(
            p.Id.ToString(),
            p.Nickname,
            p.AvatarId,
            p.CertificateName,
            p.XpTotal,
            p.Gems,
            p.StreakCurrent,
            p.StreakBest,
            p.LastPlayedUtc,
            settings,
            p.OnboardingComplete,
            heartsValue,
            regen,
            p.StreakFreezeCount,
            p.DailyGoalXp,
            p.MaxCombo);
    }

    private static async Task<Player?> CurrentPlayerAsync(GameDb db, HttpContext ctx)
    {
        var subject = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(subject))
            return null;

        var player = await db.Players.FirstOrDefaultAsync(p => p.ExternalSubject == subject);
        if (player is not null)
            return player;

        // First time seeing this authenticated identity — create the account row.
        // Onboarding (nickname + avatar) is still required before the game opens up.
        player = new Player
        {
            ExternalSubject = subject,
            Email = ctx.User.FindFirstValue(ClaimTypes.Email) ?? string.Empty,
            Nickname = string.Empty,
            SettingsJson = SettingsPlaceholder
        };
        db.Players.Add(player);
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Lost a create race (parallel first requests); the row exists now.
            db.Entry(player).State = EntityState.Detached;
            player = await db.Players.FirstOrDefaultAsync(p => p.ExternalSubject == subject);
        }
        return player;
    }

    private static async Task SignInPlayerAsync(HttpContext ctx, Player player)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, player.ExternalSubject),
            new(ClaimTypes.Name, player.Nickname)
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = true });
    }

    private static bool GoogleConfigured(IConfiguration cfg)
    {
        var id = cfg["Authentication:Google:ClientId"];
        var secret = cfg["Authentication:Google:ClientSecret"];
        return !string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(secret)
            && id is not ("<YOU-FILL>" or "YOUR_CLIENT_ID")
            && secret is not ("<YOU-FILL>" or "YOUR_CLIENT_SECRET");
    }

    private static bool IsValidNickname(string nickname) =>
        nickname.All(c => char.IsLetterOrDigit(c) || c is '_' or '-' or '.' or ' ');
}
