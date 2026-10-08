using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace DotNetQuest.App.Data;

public class Player
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Google account subject ("sub" claim), or "dev|..." for development logins.</summary>
    [MaxLength(128)]
    public string ExternalSubject { get; set; } = string.Empty;

    [MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [MaxLength(32)]
    public string Nickname { get; set; } = string.Empty;

    [MaxLength(64)]
    public string AvatarId { get; set; } = "bit-classic";

    [MaxLength(128)]
    public string? CertificateName { get; set; }

    public int XpTotal { get; set; }

    public int Gems { get; set; }

    public int StreakCurrent { get; set; }

    public int StreakBest { get; set; }

    public DateOnly? LastPlayedUtc { get; set; }

    // ---------- gamification (Phase 2) ----------

    public int Hearts { get; set; } = 5;

    /// <summary>UTC instant when the next heart regenerates (null while full).</summary>
    public DateTime? HeartsRegenAtUtc { get; set; }

    public int StreakFreezeCount { get; set; }

    /// <summary>Current position in the 7-day login-bonus cycle (0 = none claimed yet).</summary>
    public int LoginBonusDay { get; set; }

    public DateOnly? LastLoginBonusDateUtc { get; set; }

    public int DailyGoalXp { get; set; } = 50;

    // Lifetime counters (drive achievements).
    public int LessonsCompleted { get; set; }
    public int PerfectLessons { get; set; }
    public int BossesDefeated { get; set; }
    public int QuizzesPassed { get; set; }
    public int MaxCombo { get; set; }
    public int CodeRunsPassed { get; set; }
    public int PlaygroundRuns { get; set; }
    public int DailyChallengesDone { get; set; }
    public int LightningRuns { get; set; }
    public int PracticeRuns { get; set; }
    public int GemsEarnedTotal { get; set; }

    /// <summary>JSON blob of PlayerSettingsDto.</summary>
    [MaxLength(512)]
    public string SettingsJson { get; set; } = "{}";

    public bool OnboardingComplete { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class LessonProgress
{
    public int Id { get; set; }

    public Guid PlayerId { get; set; }
    public Player? Player { get; set; }

    [MaxLength(64)]
    public string WorldId { get; set; } = string.Empty;

    [MaxLength(64)]
    public string LessonId { get; set; } = string.Empty;

    public int Stars { get; set; }

    public int Attempts { get; set; }

    public int Mistakes { get; set; }

    public int XpEarned { get; set; }

    public DateTime? CompletedAtUtc { get; set; }
}

public class ExerciseAttempt
{
    public long Id { get; set; }

    public Guid PlayerId { get; set; }
    public Player? Player { get; set; }

    [MaxLength(64)]
    public string WorldId { get; set; } = string.Empty;

    [MaxLength(64)]
    public string LessonId { get; set; } = string.Empty;

    [MaxLength(64)]
    public string ExerciseId { get; set; } = string.Empty;

    public bool Correct { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class DailyActivity
{
    public int Id { get; set; }

    public Guid PlayerId { get; set; }
    public Player? Player { get; set; }

    public DateOnly DateUtc { get; set; }

    public int XpEarned { get; set; }

    public int LessonsCompleted { get; set; }

    public int CorrectAnswers { get; set; }

    public int PerfectLessons { get; set; }

    public int CodeRuns { get; set; }

    public bool GoalReached { get; set; }

    public bool ChallengeDone { get; set; }

    public int LightningRuns { get; set; }

    public int PracticeRuns { get; set; }
}

public class DailyQuestRow
{
    public int Id { get; set; }

    public Guid PlayerId { get; set; }
    public Player? Player { get; set; }

    public DateOnly DateUtc { get; set; }

    [MaxLength(64)]
    public string QuestId { get; set; } = string.Empty;

    public int Progress { get; set; }

    public bool Claimed { get; set; }
}

public class CodeSubmission
{
    public long Id { get; set; }

    public Guid PlayerId { get; set; }
    public Player? Player { get; set; }

    [MaxLength(64)]
    public string? ExerciseId { get; set; }

    public bool Passed { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class AchievementRow
{
    public int Id { get; set; }

    public Guid PlayerId { get; set; }
    public Player? Player { get; set; }

    [MaxLength(64)]
    public string AchievementId { get; set; } = string.Empty;

    public DateTime UnlockedAtUtc { get; set; } = DateTime.UtcNow;
}

public class CertificateRow
{
    public int Id { get; set; }

    public Guid PlayerId { get; set; }
    public Player? Player { get; set; }

    [MaxLength(64)]
    public string WorldId { get; set; } = string.Empty;

    [MaxLength(128)]
    public string IssuedToName { get; set; } = string.Empty;

    public int XpAtIssue { get; set; }

    public DateTime IssuedAtUtc { get; set; } = DateTime.UtcNow;
}
