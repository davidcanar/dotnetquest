using DotNetQuest.Shared;
using DotNetQuest.Shared.Content;

namespace DotNetQuest.Tests;

public class XpCalculatorTests
{
    [Fact]
    public void Level1_starts_at_zero_xp()
    {
        Assert.Equal(1, XpCalculator.LevelForXp(0));
        Assert.Equal(1, XpCalculator.LevelForXp(74));
    }

    [Fact]
    public void Level_increases_with_xp()
    {
        Assert.Equal(2, XpCalculator.LevelForXp(75));
        Assert.Equal(3, XpCalculator.LevelForXp(XpCalculator.XpForLevel(3)));
    }

    [Fact]
    public void XpForLevel_is_consistent_with_LevelForXp()
    {
        foreach (var level in new[] { 1, 2, 3, 5, 10 })
        {
            Assert.Equal(level, XpCalculator.LevelForXp(XpCalculator.XpForLevel(level)));
        }
    }

    [Fact]
    public void Boss_exercise_xp_is_doubled()
    {
        var ex = new ExerciseContent("e", ExerciseTypes.Code, null, null, null, null, null, null, false, null, null, null, null, 15);
        Assert.Equal(30, XpCalculator.ExerciseXp(ex, LessonKinds.Boss));
        Assert.Equal(17, XpCalculator.ExerciseXp(ex with { XpReward = 15 }, LessonKinds.Quiz));
        Assert.Equal(15, XpCalculator.ExerciseXp(ex, LessonKinds.Lesson));
    }

    [Fact]
    public void LessonXp_from_parts_applies_bonuses()
    {
        // 4 exercises at 10 XP = 40 + lesson bonus 20 = 60
        Assert.Equal(60, XpCalculator.LessonXpFromParts(40, LessonKinds.Lesson, mistakes: 1));
        // Perfect lesson adds 15 more
        Assert.Equal(75, XpCalculator.LessonXpFromParts(40, LessonKinds.Lesson, mistakes: 0));
        // Quiz bonus is 25
        Assert.Equal(65, XpCalculator.LessonXpFromParts(40, LessonKinds.Quiz, mistakes: 0));
        // Boss bonus is 50
        Assert.Equal(90, XpCalculator.LessonXpFromParts(40, LessonKinds.Boss, mistakes: 0));
    }

    [Fact]
    public void LessonXpFromParts_clamps_unreasonable_values()
    {
        Assert.Equal(5000 + 20, XpCalculator.LessonXpFromParts(999999, LessonKinds.Lesson, 1));
        Assert.Equal(0 + 20, XpCalculator.LessonXpFromParts(-50, LessonKinds.Lesson, 1));
    }

    [Fact]
    public void Stars_map_to_mistakes()
    {
        Assert.Equal(3, XpCalculator.Stars(0));
        Assert.Equal(2, XpCalculator.Stars(1));
        Assert.Equal(2, XpCalculator.Stars(2));
        Assert.Equal(1, XpCalculator.Stars(3));
        Assert.Equal(1, XpCalculator.Stars(10));
    }
}

public class StreakCalculatorTests
{
    private static readonly DateOnly Today = new(2026, 10, 5);

    [Fact]
    public void Same_day_keeps_streak()
    {
        Assert.Equal(4, StreakCalculator.NextStreak(Today, Today, 4));
    }

    [Fact]
    public void Consecutive_day_increments()
    {
        Assert.Equal(5, StreakCalculator.NextStreak(Today.AddDays(-1), Today, 4));
    }

    [Fact]
    public void Gap_resets_to_one()
    {
        Assert.Equal(1, StreakCalculator.NextStreak(Today.AddDays(-3), Today, 4));
    }

    [Fact]
    public void First_ever_play_starts_at_one()
    {
        Assert.Equal(1, StreakCalculator.NextStreak(null, Today, 0));
    }

    [Fact]
    public void Milestones_are_detected()
    {
        Assert.True(StreakCalculator.IsMilestone(7));
        Assert.False(StreakCalculator.IsMilestone(8));
    }
}
