using System.Net.Http.Json;
using DotNetQuest.Shared;
using DotNetQuest.Shared.Api;
using DotNetQuest.Shared.Content;

namespace DotNetQuest.App.Client.Services;

/// <summary>
/// Central client-side game state: the signed-in player, lesson progress and
/// content. Pages subscribe to StateChanged to re-render on updates.
/// </summary>
public class GameState(ApiClient api, ContentLoader content, I18nService i18n)
{
    public PlayerDto? Player { get; private set; }
    public bool TutorConfigured { get; private set; }
    public bool GoogleConfigured { get; private set; }
    public bool Initialized { get; private set; }
    public bool Initializing { get; private set; }

    private List<ProgressRowDto> _progress = [];
    private List<WorldIndexItem>? _worldIndex;
    private DailyStatusDto? _daily;

    public event Action? StateChanged;

    public IReadOnlyList<ProgressRowDto> Progress => _progress;

    public DailyStatusDto? Daily => _daily;

    public async Task<DailyStatusDto?> RefreshDailyAsync()
    {
        _daily = await api.GetDailyAsync();
        StateChanged?.Invoke();
        return _daily;
    }

    public void UpdatePlayer(PlayerDto player)
    {
        Player = player;
        StateChanged?.Invoke();
    }

    /// <summary>Call at app start (layout OnInitializedAsync).</summary>
    public async Task InitializeAsync()
    {
        if (Initialized || Initializing) return;
        Initializing = true;
        try
        {
            var me = await api.GetMeAsync();
            Player = me.Player;
            TutorConfigured = me.TutorConfigured;
            GoogleConfigured = me.GoogleConfigured;
            if (Player is not null)
                _progress = await api.GetProgressAsync();

            await i18n.EnsureLoadedAsync();
            var preferred = Player?.Settings.Language;
            if (!string.IsNullOrWhiteSpace(preferred) && preferred != i18n.Language)
                await i18n.SetLanguageAsync(preferred, persist: false);

            Initialized = true;
        }
        finally
        {
            Initializing = false;
            StateChanged?.Invoke();
        }
    }

    public async Task RefreshPlayerAsync()
    {
        var me = await api.GetMeAsync();
        Player = me.Player;
        TutorConfigured = me.TutorConfigured;
        GoogleConfigured = me.GoogleConfigured;
        if (Player is not null)
            _progress = await api.GetProgressAsync();
        StateChanged?.Invoke();
    }

    public void NotifyChanged() => StateChanged?.Invoke();

    // ---------- content ----------

    public async Task<List<WorldIndexItem>> GetWorldIndexAsync() =>
        _worldIndex ??= await content.GetWorldIndexAsync();

    public async Task<WorldContent?> GetWorldAsync(string worldId) =>
        await content.GetWorldAsync(worldId);

    // ---------- progress queries ----------

    public ProgressRowDto? GetProgress(string worldId, string lessonId) =>
        _progress.FirstOrDefault(p => p.WorldId == worldId && p.LessonId == lessonId);

    public bool IsLessonCompleted(string worldId, string lessonId) =>
        GetProgress(worldId, lessonId) is not null;

    /// <summary>Index of the first incomplete lesson within a world (the "current" node).</summary>
    public int GetCurrentLessonIndex(WorldContent world)
    {
        var lessons = world.AllLessons().ToList();
        for (var i = 0; i < lessons.Count; i++)
            if (GetProgress(world.Id, lessons[i].Id) is null)
                return i;
        return lessons.Count - 1;
    }

    public bool IsWorldUnlocked(WorldContent world, List<WorldIndexItem>? index = null)
    {
        index ??= _worldIndex ?? [];
        var item = index.FirstOrDefault(w => w.Id == world.Id);
        if (item is { Available: true }) return true;
        // Worlds before the current one unlock as soon as their predecessor has any progress
        // (Phase 1 only ships world 1 anyway).
        return item is { Available: false } ? false : true;
    }

    public (int completed, int total) GetWorldProgress(WorldContent world)
    {
        var lessons = world.AllLessons().ToList();
        return (lessons.Count(l => GetProgress(world.Id, l.Id) is not null), lessons.Count);
    }

    // ---------- mutations ----------

    public async Task<LessonCompleteResponse?> CompleteLessonAsync(WorldContent world, LessonContent lesson, int mistakes, int maxCombo = 0)
    {
        var correct = lesson.Exercises.Count - Math.Min(mistakes, lesson.Exercises.Count);
        var exerciseXp = 0;
        // The client replays the XP math it previews: exercises answered correctly
        // (all of them by the time a lesson completes) minus per-mistake forfeits are
        // handled server-side; simplest honest model: base exercise XP from content.
        foreach (var ex in lesson.Exercises)
            exerciseXp += XpCalculator.ExerciseXp(ex, lesson.Kind);

        var request = new LessonCompleteRequest(world.Id, lesson.Id, lesson.Kind, correct, mistakes, exerciseXp, maxCombo);
        var response = await api.CompleteLessonAsync(request);
        if (response is not null)
        {
            Player = response.Player;
            var row = response.Progress;
            _progress.RemoveAll(p => p.WorldId == row.WorldId && p.LessonId == row.LessonId);
            _progress.Add(row);
            _ = RefreshDailyAsync();
            StateChanged?.Invoke();
        }
        return response;
    }

    public async Task<HeartsDto?> RecordAttemptAsync(WorldContent world, LessonContent lesson, ExerciseContent exercise, bool correct)
    {
        var hearts = await api.RecordAttemptAsync(new ExerciseAttemptRequest(world.Id, lesson.Id, exercise.Id, correct));
        if (hearts is not null && Player is not null)
        {
            Player = Player with { Hearts = hearts.Hearts, HeartsRegenSeconds = hearts.RegenSeconds };
            StateChanged?.Invoke();
        }
        return hearts;
    }

    public async Task<PlayerDto?> UpdateProfileAsync(ProfileUpdateRequest request)
    {
        var updated = await api.UpdateProfileAsync(request);
        if (updated is not null)
        {
            Player = updated;
            StateChanged?.Invoke();
        }
        return updated;
    }

    public async Task CompleteOnboardingAsync(string nickname, string avatarId, string? language)
    {
        var player = await api.CompleteOnboardingAsync(new OnboardingRequest(nickname, avatarId, language));
        if (player is not null)
        {
            Player = player;
            _progress = await api.GetProgressAsync();
            StateChanged?.Invoke();
        }
    }

    public Task<NicknameCheckResponse> CheckNicknamePublic(string nickname) =>
        api.CheckNicknameAsync(nickname);

    /// <summary>Development-only login (POST /api/dev-login). The endpoint 404s in Production.</summary>
    public async Task<bool> DevLoginAsync()
    {
        var player = await api.DevLoginAsync();
        if (player is null) return false;
        Player = player;
        StateChanged?.Invoke();
        return true;
    }

    public async Task SignOutAsync()
    {
        await api.SignOutAsync();
        Player = null;
        _progress = [];
        StateChanged?.Invoke();
    }

    public int Level => Player is null ? 1 : XpCalculator.LevelForXp(Player.XpTotal);
}
