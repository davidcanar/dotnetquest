using System.Net;
using System.Net.Http.Json;
using DotNetQuest.Shared.Api;
using DotNetQuest.Shared.Runner;

namespace DotNetQuest.App.Client.Services;

/// <summary>Typed HTTP access to the game API. All endpoints are same-origin (cookies flow automatically).</summary>
public class ApiClient(HttpClient http)
{
    public async Task<MeResponse> GetMeAsync()
    {
        try
        {
            return await http.GetFromJsonAsync<MeResponse>("/api/me", JsonOpts) ?? new MeResponse(null, false, false);
        }
        catch (HttpRequestException)
        {
            return new MeResponse(null, false, false);
        }
    }

    public async Task<PlayerDto?> DevLoginAsync()
    {
        try
        {
            var response = await http.PostAsync("/api/dev-login", null);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<PlayerDto>(JsonOpts);
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    public async Task<NicknameCheckResponse> CheckNicknameAsync(string nickname)
    {
        var url = $"/api/onboarding/check-nickname?nickname={Uri.EscapeDataString(nickname)}";
        try
        {
            return await http.GetFromJsonAsync<NicknameCheckResponse>(url, JsonOpts) ?? new NicknameCheckResponse(false, "error");
        }
        catch (HttpRequestException)
        {
            return new NicknameCheckResponse(false, "error");
        }
    }

    public async Task<PlayerDto?> CompleteOnboardingAsync(OnboardingRequest request)
    {
        var response = await http.PostAsJsonAsync("/api/onboarding", request, JsonOpts);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<PlayerDto>(JsonOpts);
    }

    public async Task<PlayerDto?> UpdateProfileAsync(ProfileUpdateRequest request)
    {
        var response = await http.PutAsJsonAsync("/api/profile", request, JsonOpts);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<PlayerDto>(JsonOpts);
    }

    public async Task SignOutAsync()
    {
        await http.PostAsync("/logout", null);
    }

    public async Task<List<ProgressRowDto>> GetProgressAsync()
    {
        try
        {
            return await http.GetFromJsonAsync<List<ProgressRowDto>>("/api/progress", JsonOpts) ?? [];
        }
        catch (HttpRequestException)
        {
            return [];
        }
    }

    public async Task<LessonCompleteResponse?> CompleteLessonAsync(LessonCompleteRequest request)
    {
        var response = await http.PostAsJsonAsync("/api/progress/lesson-complete", request, JsonOpts);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<LessonCompleteResponse>(JsonOpts);
    }

    public async Task<HeartsDto?> RecordAttemptAsync(ExerciseAttemptRequest request)
    {
        try
        {
            var response = await http.PostAsJsonAsync("/api/progress/exercise-attempt", request, JsonOpts);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<HeartsDto>(JsonOpts);
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    public async Task<LeaderboardResponse?> GetLeaderboardAsync()
    {
        try
        {
            return await http.GetFromJsonAsync<LeaderboardResponse>("/api/leaderboard", JsonOpts);
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    public async Task<CodeRunResultDto?> RunCodeAsync(string code, List<RunTestRequest>? tests = null, string? stdin = null, string? exerciseId = null, bool xunit = false)
    {
        var response = await http.PostAsJsonAsync("/api/code/run", new CodeRunRequest(code, exerciseId, tests, stdin, xunit), JsonOpts);
        if (!response.IsSuccessStatusCode)
            return new CodeRunResultDto(false, ["Request failed"], string.Empty, [], false, 0, $"HTTP {(int)response.StatusCode}");
        return await response.Content.ReadFromJsonAsync<CodeRunResultDto>(JsonOpts);
    }

    public async Task<TutorChatResponse> AskTutorAsync(TutorChatRequest request)
    {
        try
        {
            var response = await http.PostAsJsonAsync("/api/tutor/chat", request, JsonOpts);
            if (!response.IsSuccessStatusCode)
                return new TutorChatResponse(string.Empty, Fallback: true);
            return await response.Content.ReadFromJsonAsync<TutorChatResponse>(JsonOpts) ?? new TutorChatResponse(string.Empty, Fallback: true);
        }
        catch (HttpRequestException)
        {
            return new TutorChatResponse(string.Empty, Fallback: true);
        }
    }

    // ---------- Phase 2: gamification ----------

    public async Task<DailyStatusDto?> GetDailyAsync()
    {
        try
        {
            return await http.GetFromJsonAsync<DailyStatusDto>("/api/daily", JsonOpts);
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    public async Task<ClaimQuestResponse?> ClaimQuestAsync(string questId)
    {
        var response = await http.PostAsync($"/api/quests/claim?questId={Uri.EscapeDataString(questId)}", null);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<ClaimQuestResponse>(JsonOpts);
    }

    public async Task<ClaimLoginBonusResponse?> ClaimLoginBonusAsync()
    {
        var response = await http.PostAsync("/api/login-bonus/claim", null);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<ClaimLoginBonusResponse>(JsonOpts);
    }

    public async Task<ShopBuyResponse?> BuyAsync(string itemId)
    {
        var response = await http.PostAsJsonAsync("/api/shop/buy", new ShopBuyRequest(itemId), JsonOpts);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<ShopBuyResponse>(JsonOpts)
            : null;
    }

    public async Task<List<AchievementStateDto>?> GetAchievementsAsync()
    {
        try
        {
            return await http.GetFromJsonAsync<List<AchievementStateDto>>("/api/achievements", JsonOpts);
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    public async Task<SessionCompleteResponse?> CompleteLightningAsync(int correct, int total)
    {
        var response = await http.PostAsJsonAsync("/api/sessions/lightning", new SessionCompleteRequest(correct, total), JsonOpts);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<SessionCompleteResponse>(JsonOpts)
            : null;
    }

    public async Task<List<PracticeExerciseDto>?> GetPracticeExercisesAsync()
    {
        try
        {
            return await http.GetFromJsonAsync<List<PracticeExerciseDto>>("/api/practice/exercises", JsonOpts);
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    public async Task<SessionCompleteResponse?> CompletePracticeAsync(int correct, int total)
    {
        var response = await http.PostAsJsonAsync("/api/sessions/practice", new SessionCompleteRequest(correct, total), JsonOpts);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<SessionCompleteResponse>(JsonOpts)
            : null;
    }

    public async Task<ChallengeCompleteResponse?> CompleteDailyChallengeAsync()
    {
        var response = await http.PostAsync("/api/daily-challenge/complete", null);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<ChallengeCompleteResponse>(JsonOpts);
    }

    public async Task<PlayerDto?> ConsumeHintAsync()
    {
        var response = await http.PostAsync("/api/tutor/hint-consume", null);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<PlayerDto>(JsonOpts)
            : null;
    }

    public async Task<LeaderboardResponse?> GetLeaderboardAsync(string range = "all")
    {
        try
        {
            return await http.GetFromJsonAsync<LeaderboardResponse>($"/api/leaderboard?range={range}", JsonOpts);
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    public async Task<List<CertificateStatusDto>?> GetCertificatesAsync()
    {
        try
        {
            return await http.GetFromJsonAsync<List<CertificateStatusDto>>("/api/certificates", JsonOpts);
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    public async Task<TutorStatusDto?> GetTutorStatusAsync()
    {
        try
        {
            return await http.GetFromJsonAsync<TutorStatusDto>("/api/tutor/status", JsonOpts);
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    public async Task<bool> ImportProgressAsync(string exportJson)
    {
        try
        {
            var content = new System.Net.Http.StringContent(exportJson, System.Text.Encoding.UTF8, "application/json");
            var response = await http.PostAsync("/api/profile/import", content);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }

    /// <summary>Stream Bit's reply via the SSE endpoint. Returns (fullText, ok, reason) — ok=false means fall back to hints; reason explains why.</summary>
    public async Task<(string Text, bool Ok, string? Reason)> StreamTutorAsync(TutorChatRequest request, Action<string>? onDelta, CancellationToken ct = default)
    {
        try
        {
            using var response = await http.PostAsJsonAsync("/api/tutor/chat/stream", request, JsonOpts, ct);
            if (!response.IsSuccessStatusCode)
            {
                // 429 rate-limit and other failures carry {"error": "..."} — surface it
                // so Bit can show a friendly, localized message instead of hinting blind.
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                var errorToken = errorBody;
                try
                {
                    var err = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(errorBody, JsonOpts);
                    if (err is not null && err.TryGetValue("error", out var e))
                        errorToken = e;
                }
                catch { /* not JSON — pass the raw body through */ }
                return (string.Empty, false, errorToken);
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var reader = new System.IO.StreamReader(stream);
            var full = new System.Text.StringBuilder();

            while (!reader.EndOfStream && !ct.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(ct) ?? string.Empty;
                if (!line.StartsWith("data:", StringComparison.Ordinal)) continue;
                var payload = line["data:".Length..].Trim();
                if (payload == "[DONE]") break;
                if (payload.StartsWith("FALLBACK::", StringComparison.Ordinal))
                    return (string.Empty, false, payload["FALLBACK::".Length..].Trim());
                var delta = System.Text.Json.JsonSerializer.Deserialize<string>(payload, JsonOpts);
                if (string.IsNullOrEmpty(delta))
                    return (full.ToString(), false, null); // server signaled fallback
                full.Append(delta);
                onDelta?.Invoke(delta);
            }
            return (full.ToString(), full.Length > 0, null);
        }
        catch (Exception)
        {
            return (string.Empty, false, null);
        }
    }

    private static readonly System.Text.Json.JsonSerializerOptions JsonOpts = new(System.Text.Json.JsonSerializerDefaults.Web);
}
