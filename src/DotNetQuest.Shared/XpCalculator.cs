using DotNetQuest.Shared.Content;

namespace DotNetQuest.Shared;

/// <summary>
/// XP economy rules. Both client (preview) and server (authoritative) use this,
/// so the numbers always agree.
/// </summary>
public static class XpCalculator
{
    public const int LessonCompleteBonus = 20;
    public const int PerfectLessonBonus = 15;
    public const int QuizCompleteBonus = 25;
    public const int BossCompleteBonus = 50;

    /// <summary>XP for one exercise, based on the exercise's configured reward and lesson kind.</summary>
    public static int ExerciseXp(ExerciseContent exercise, string lessonKind) =>
        lessonKind switch
        {
            LessonKinds.Quiz => exercise.XpReward + 2,
            LessonKinds.Boss => exercise.XpReward * 2,
            _ => exercise.XpReward
        };

    /// <summary>Total XP earned for finishing a lesson.</summary>
    public static int LessonXp(LessonContent lesson, int mistakes)
    {
        var exerciseXp = lesson.Exercises.Sum(e => ExerciseXp(e, lesson.Kind));
        return LessonXpFromParts(exerciseXp, lesson.Kind, mistakes);
    }

    /// <summary>
    /// Server-authoritative XP from client-reported parts. The client (which owns
    /// the content) reports the exercise XP sum; bonuses are applied here.
    /// </summary>
    public static int LessonXpFromParts(int exerciseXp, string kind, int mistakes)
    {
        var total = Math.Clamp(exerciseXp, 0, 5_000);
        total += kind switch
        {
            LessonKinds.Quiz => QuizCompleteBonus,
            LessonKinds.Boss => BossCompleteBonus,
            _ => LessonCompleteBonus
        };

        if (mistakes == 0 && kind == LessonKinds.Lesson)
            total += PerfectLessonBonus;

        return total;
    }

    /// <summary>Stars: 3 = flawless, 2 = a couple of mistakes, 1 = completed.</summary>
    public static int Stars(int mistakes) => mistakes switch
    {
        0 => 3,
        <= 2 => 2,
        _ => 1
    };

    /// <summary>Bonus XP for an in-lesson answer combo (starts paying at 3 in a row, caps at 10).</summary>
    public static int ComboBonus(int maxCombo) => Math.Clamp(maxCombo - 2, 0, 10);

    /// <summary>Double XP runs automatically every Saturday and Sunday (UTC).</summary>
    public static bool IsDoubleXpDay(DateOnly day) =>
        day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

    public const int WeekendMultiplier = 2;

    /// <summary>Level curve: each level costs a bit more than the last (75 * level^2 cumulative).</summary>
    public static int LevelForXp(int totalXp) => totalXp <= 0 ? 1 : (int)Math.Floor(Math.Sqrt(totalXp / 75.0)) + 1;

    /// <summary>XP needed to reach the given level.</summary>
    public static int XpForLevel(int level) => (level - 1) * (level - 1) * 75;

    /// <summary>Progress (0..1) through the current level.</summary>
    public static double LevelProgress(int totalXp)
    {
        var level = LevelForXp(totalXp);
        var currentFloor = XpForLevel(level);
        var nextFloor = XpForLevel(level + 1);
        return nextFloor == currentFloor ? 0 : Math.Clamp((totalXp - currentFloor) / (double)(nextFloor - currentFloor), 0, 1);
    }

    /// <summary>XP remaining until the next level.</summary>
    public static int XpToNextLevel(int totalXp) => XpForLevel(LevelForXp(totalXp) + 1) - totalXp;
}
