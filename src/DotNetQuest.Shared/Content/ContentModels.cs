using System.Text.Json.Serialization;

namespace DotNetQuest.Shared.Content;

/// <summary>A localized string: English + Spanish versions.</summary>
public record Loc(string En, string Es)
{
    public string Get(string language) =>
        string.Equals(language, "es", StringComparison.OrdinalIgnoreCase) ? Es : En;
}

public record WorldIndexItem(
    string Id,
    int Order,
    string Track,
    Loc Title,
    Loc Subtitle,
    string Icon,
    string Color,
    bool Available);

/// <summary>Exercise kinds the player can encounter.</summary>
public static class ExerciseTypes
{
    public const string Mcq = "mcq";
    public const string TrueFalse = "truefalse";
    public const string FillBlank = "fillblank";
    public const string Output = "output";
    public const string TypeIn = "typein";
    public const string Code = "code";
    public const string Parsons = "parsons";
    public const string BugFix = "bugfix";
    public const string Match = "match";

    public static readonly string[] All = { Mcq, TrueFalse, FillBlank, Output, TypeIn, Code, Parsons, BugFix, Match };
}

/// <summary>Lesson kinds. Quiz/boss lessons award bonus XP.</summary>
public static class LessonKinds
{
    public const string Lesson = "lesson";
    public const string Quiz = "quiz";
    public const string Boss = "boss";
}

/// <summary>A fill-in-the-blank answer (matched against ___ placeholders in order).</summary>
public record Blank(string Answer, bool CaseSensitive = true);

/// <summary>A test case for code exercises, executed by the sandboxed runner. ApiPath switches to live HTTP testing.</summary>
public record TestSpec(string Name, string ExpectedOutput, string Match = "exact", string? Stdin = null,
    string? ApiPath = null, string ApiMethod = "GET", int? ExpectedStatus = null);

/// <summary>A term/definition pair for match exercises. The correct pairing is by list index.</summary>
public record MatchPair(Loc Left, Loc Right);

/// <summary>A cheat-sheet section shown from the world page.</summary>
public record CheatSection(Loc Title, List<Loc> Items);

/// <summary>
/// A single exercise. One class with optional fields keeps the content JSON schema
/// simple; the Grader interprets fields according to Type.
/// </summary>
public record ExerciseContent(
    string Id,
    string Type,
    Loc? Prompt,
    string? Code,
    List<Loc>? Options,
    int? AnswerIndex,
    List<Blank>? Blanks,
    List<string>? Accepted,
    bool CaseSensitive,
    string? Starter,
    List<TestSpec>? Tests,
    Loc? Explanation,
    List<Loc>? Hints,
    [property: JsonPropertyName("xp")] int XpReward,
    List<MatchPair>? Pairs = null,
    [property: JsonPropertyName("xunit")] bool Xunit = false);

/// <summary>A lesson: a title, Bit's intro tip, and an ordered exercise list.</summary>
public record LessonContent(
    string Id,
    string Kind,
    Loc Title,
    Loc Tip,
    List<ExerciseContent> Exercises);

public record UnitContent(string Id, Loc Title, List<LessonContent> Lessons);

public record WorldContent(
    string Id,
    int Order,
    string Track,
    Loc Title,
    Loc Subtitle,
    Loc Intro,
    string Icon,
    string Color,
    List<UnitContent> Units,
    List<CheatSection>? CheatSheet = null);

public static class ContentExtensions
{
    public static IEnumerable<LessonContent> AllLessons(this WorldContent world) =>
        world.Units.SelectMany(u => u.Lessons);

    public static LessonContent? FindLesson(this WorldContent world, string lessonId) =>
        world.AllLessons().FirstOrDefault(l => l.Id == lessonId);

    public static int MaxPossibleXp(this LessonContent lesson)
    {
        var baseXp = lesson.Exercises.Sum(e => e.XpReward);
        return lesson.Kind switch
        {
            LessonKinds.Quiz => baseXp + XpCalculator.LessonCompleteBonus + XpCalculator.QuizCompleteBonus,
            LessonKinds.Boss => baseXp + XpCalculator.LessonCompleteBonus + XpCalculator.BossCompleteBonus,
            _ => baseXp + XpCalculator.LessonCompleteBonus + XpCalculator.PerfectLessonBonus
        };
    }
}
