namespace DotNetQuest.Shared.Api;

// ---------- Auth / profile ----------

public record PlayerDto(
    string Id,
    string Nickname,
    string AvatarId,
    string? CertificateName,
    int XpTotal,
    int Gems,
    int StreakCurrent,
    int StreakBest,
    DateOnly? LastPlayedUtc,
    PlayerSettingsDto Settings,
    bool OnboardingComplete,
    int Hearts = 5,
    int HeartsRegenSeconds = 0,
    int StreakFreezeCount = 0,
    int DailyGoalXp = 50,
    int MaxCombo = 0);

public record PlayerSettingsDto(
    string? Language,
    bool AutoSpeak,
    bool Sound,
    bool DarkMode);

public record OnboardingRequest(string Nickname, string AvatarId, string? Language);

public record NicknameCheckResponse(bool Available, string? Error);

public record ProfileUpdateRequest(
    string? Nickname,
    string? AvatarId,
    string? CertificateName,
    PlayerSettingsDto? Settings,
    int? DailyGoalXp = null);

public record MeResponse(PlayerDto? Player, bool TutorConfigured, bool GoogleConfigured);

// ---------- Progress ----------

public record ProgressRowDto(
    string WorldId,
    string LessonId,
    int Stars,
    int Attempts,
    string? CompletedAtUtc);

public record LessonCompleteRequest(
    string WorldId,
    string LessonId,
    string Kind,
    int Correct,
    int Mistakes,
    /// <summary>Sum of ExerciseXp over the exercises answered correctly (computed from content by the client).</summary>
    int ExerciseXp,
    int MaxCombo = 0);

public record LessonCompleteResponse(
    PlayerDto Player,
    ProgressRowDto Progress,
    int XpEarned,
    bool StreakMilestone,
    int NewLevel,
    bool DoubleXp,
    int ComboBonus,
    int PerfectBonus);

public record ExerciseAttemptRequest(string WorldId, string LessonId, string ExerciseId, bool Correct);

public record HeartsDto(int Hearts, int RegenSeconds);

// ---------- Leaderboard ----------

public record LeaderboardEntryDto(int Rank, string PlayerId, string Nickname, string AvatarId, int XpTotal, int StreakCurrent, int Level, bool IsMe);

public record LeaderboardResponse(List<LeaderboardEntryDto> Top, LeaderboardEntryDto? Me);

// ---------- Daily loop: quests, goal, login bonus, challenge ----------

public record QuestDto(string QuestId, int Progress, int Target, int XpReward, int GemReward, bool Claimed);

public record LoginBonusDto(int Day, int Gems, int Xp);

public record DailyStatusDto(
    List<QuestDto> Quests,
    LoginBonusDto? LoginBonus,
    bool ChallengeDone,
    int XpToday,
    int DailyGoalXp,
    bool GoalReached,
    bool WeekendDoubleXp);

public record ClaimQuestResponse(PlayerDto Player, QuestDto Quest, int XpEarned);

public record ClaimLoginBonusResponse(PlayerDto Player, int Day, int Gems, int Xp);

public record ChallengeCompleteResponse(PlayerDto Player, int XpEarned, int GemsEarned);

// ---------- Certificates ----------

public record CertificateStatusDto(string WorldId, bool Eligible, bool Issued, string? IssuedAtUtc);

// ---------- Achievements ----------

public record AchievementStateDto(string Id, bool Unlocked);

// ---------- Shop ----------

public record ShopBuyRequest(string ItemId);

public record ShopBuyResponse(PlayerDto Player, int Hearts, int StreakFreezeCount);

// ---------- Practice / Lightning ----------

public record PracticeExerciseDto(string WorldId, string LessonId, string ExerciseId);

public record SessionCompleteRequest(int Correct, int Total);

public record SessionCompleteResponse(PlayerDto Player, int XpEarned, bool DoubleXp, bool CapReached);

// ---------- Code runner ----------

public record RunTestRequest(string Name, string ExpectedOutput, string Match = "exact", string? Stdin = null, string? ApiPath = null, string ApiMethod = "GET", int? ExpectedStatus = null);

public record CodeRunRequest(string Code, string? ExerciseId = null, List<RunTestRequest>? Tests = null, string? Stdin = null, bool Xunit = false);

public record RunTestResultDto(string Name, bool Passed, string Expected, string Actual);

public record CodeRunResultDto(
    bool Compiled,
    List<string> Diagnostics,
    string Output,
    List<RunTestResultDto> Tests,
    bool TimedOut,
    int ElapsedMs,
    string? Error);

// ---------- AI tutor ----------

public record TutorChatMessage(string Role, string Content);

public record TutorContext(
    string? LessonTitle,
    string? ExercisePrompt,
    string? ExerciseType,
    string? StarterCode,
    string? UserCode,
    string? LastOutput,
    string? LastDiagnostics);

public record TutorChatRequest(string Language, TutorContext? Context, List<TutorChatMessage> Messages);

public record TutorChatResponse(string Reply, bool Fallback, string? Reason = null);

public record TutorStatusDto(bool Configured, string? Reason);
