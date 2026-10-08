using DotNetQuest.App.Data;
using Microsoft.EntityFrameworkCore;

namespace DotNetQuest.App.Data;

public class GameDb(DbContextOptions<GameDb> options) : DbContext(options)
{
    public DbSet<Player> Players => Set<Player>();
    public DbSet<LessonProgress> LessonProgress => Set<LessonProgress>();
    public DbSet<ExerciseAttempt> ExerciseAttempts => Set<ExerciseAttempt>();
    public DbSet<DailyActivity> DailyActivity => Set<DailyActivity>();
    public DbSet<DailyQuestRow> DailyQuests => Set<DailyQuestRow>();
    public DbSet<CodeSubmission> CodeSubmissions => Set<CodeSubmission>();
    public DbSet<AchievementRow> Achievements => Set<AchievementRow>();
    public DbSet<CertificateRow> Certificates => Set<CertificateRow>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<Player>(e =>
        {
            e.HasIndex(p => p.ExternalSubject).IsUnique();
            e.HasIndex(p => p.Nickname).IsUnique();
        });

        model.Entity<LessonProgress>(e =>
        {
            e.HasIndex(p => new { p.PlayerId, p.WorldId, p.LessonId }).IsUnique();
        });

        model.Entity<ExerciseAttempt>(e =>
        {
            e.HasIndex(p => new { p.PlayerId, p.CreatedAtUtc });
        });

        model.Entity<DailyActivity>(e =>
        {
            e.HasIndex(p => new { p.PlayerId, p.DateUtc }).IsUnique();
        });

        model.Entity<DailyQuestRow>(e =>
        {
            e.HasIndex(p => new { p.PlayerId, p.DateUtc, p.QuestId }).IsUnique();
        });

        model.Entity<AchievementRow>(e =>
        {
            e.HasIndex(p => new { p.PlayerId, p.AchievementId }).IsUnique();
        });

        model.Entity<CertificateRow>(e =>
        {
            e.HasIndex(p => new { p.PlayerId, p.WorldId }).IsUnique();
        });
    }
}
