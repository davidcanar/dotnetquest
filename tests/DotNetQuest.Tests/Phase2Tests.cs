using DotNetQuest.Shared;
using DotNetQuest.Shared.Content;

namespace DotNetQuest.Tests;

public class GraderNewTypesTests
{
    private static ExerciseContent Ex(ExerciseContent ex) => ex with { Explanation = new Loc("e", "e") };

    [Fact]
    public void Parsons_correct_order_passes()
    {
        var ex = Ex(new ExerciseContent(
            "p1", ExerciseTypes.Parsons, null,
            "A();\nvoid A()\n{\n    Console.WriteLine(1);\n}",
            null, null, null, null, false, null, null, null, null, 10));

        Assert.True(Grader.Check(ex, new AnswerSubmission(Lines: ["A();", "void A()", "{", "Console.WriteLine(1);", "}"])).Correct);
        Assert.False(Grader.Check(ex, new AnswerSubmission(Lines: ["void A()", "A();", "{", "Console.WriteLine(1);", "}"])).Correct);
    }

    [Fact]
    public void Parsons_ignores_empty_lines_and_whitespace()
    {
        var ex = Ex(new ExerciseContent(
            "p2", ExerciseTypes.Parsons, null, "x = 1;\ny = x + 1;",
            null, null, null, null, false, null, null, null, null, 10));

        Assert.True(Grader.Check(ex, new AnswerSubmission(Lines: [" x = 1; ", "", "y = x + 1;"])).Correct);
    }

    [Fact]
    public void Match_identity_selection_passes()
    {
        var pairs = new List<MatchPair>
        {
            new(new Loc("L1", "L1"), new Loc("R1", "R1")),
            new(new Loc("L2", "L2"), new Loc("R2", "R2")),
            new(new Loc("L3", "L3"), new Loc("R3", "R3"))
        };
        var ex = Ex(new ExerciseContent("m1", ExerciseTypes.Match, null, null, null, null, null, null, false, null, null, null, null, 10, pairs));

        Assert.True(Grader.Check(ex, new AnswerSubmission(MatchSelection: [0, 1, 2])).Correct);
        Assert.False(Grader.Check(ex, new AnswerSubmission(MatchSelection: [1, 0, 2])).Correct);
        Assert.False(Grader.Check(ex, new AnswerSubmission(MatchSelection: [0, 1])).Correct);
    }
}

public class GamificationTests
{
    [Fact]
    public void ComboBonus_starts_at_three_and_caps_at_ten()
    {
        Assert.Equal(0, XpCalculator.ComboBonus(1));
        Assert.Equal(0, XpCalculator.ComboBonus(2));
        Assert.Equal(1, XpCalculator.ComboBonus(3));
        Assert.Equal(8, XpCalculator.ComboBonus(10));
        Assert.Equal(10, XpCalculator.ComboBonus(25));
    }

    [Fact]
    public void Weekend_is_saturday_or_sunday()
    {
        Assert.True(XpCalculator.IsDoubleXpDay(new DateOnly(2026, 10, 3)));  // Saturday
        Assert.True(XpCalculator.IsDoubleXpDay(new DateOnly(2026, 10, 4)));  // Sunday
        Assert.False(XpCalculator.IsDoubleXpDay(new DateOnly(2026, 10, 5))); // Monday
    }

    [Fact]
    public void Achievements_definitions_are_unique_and_rewarded()
    {
        var ids = Achievements.All.Select(a => a.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
        Assert.True(Achievements.All.Length >= 30, "launch requires 30+ achievements");
        Assert.All(Achievements.All, a =>
        {
            Assert.False(string.IsNullOrWhiteSpace(a.Title.En));
            Assert.False(string.IsNullOrWhiteSpace(a.Title.Es));
            Assert.True(a.XpReward > 0 || a.GemReward > 0);
        });
    }

    [Fact]
    public void Achievement_evaluation_unlocks_only_new()
    {
        var stats = new PlayerStats(1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 50, 1, 0, 0);
        var first = Achievements.Evaluate(stats, []);
        Assert.Contains(first, a => a.Id == "first-lesson");

        // Not yet: xp-100 needs 100 XP
        Assert.DoesNotContain(first, a => a.Id == "xp-100");

        // Already unlocked does not reappear
        var second = Achievements.Evaluate(stats, ["first-lesson"]);
        Assert.DoesNotContain(second, a => a.Id == "first-lesson");
    }

    [Fact]
    public void DailyQuests_pick_three_distinct_and_deterministic()
    {
        var id = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var day = new DateOnly(2026, 10, 5);
        var pick1 = DailyQuests.PickForDay(id, day);
        var pick2 = DailyQuests.PickForDay(id, day);

        Assert.Equal(3, pick1.Count);
        Assert.Equal(pick1.Count, pick1.Select(q => q.Id).Distinct().Count());
        Assert.Equal(pick1.Select(q => q.Id), pick2.Select(q => q.Id)); // deterministic
    }

    [Fact]
    public void Shop_login_bonus_has_seven_days()
    {
        Assert.Equal(7, Shop.LoginBonusDays.Length);
        Assert.True(Shop.LoginBonusDays[^1].Gems > Shop.LoginBonusDays[0].Gems, "day 7 beats day 1");
    }
}
