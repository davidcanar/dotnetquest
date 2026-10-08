using System.Text.Json;
using DotNetQuest.Shared.Content;

namespace DotNetQuest.Tests;

/// <summary>
/// Content QA: the World 1 pack must parse, be fully bilingual, and every
/// exercise must be answerable (valid answers, non-empty hints, etc.).
/// </summary>
public class ContentValidationTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static string FindContentRoot()
    {
        // Walk up from the test bin folder to the repo root.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "DotNetQuest.slnx")))
            dir = dir.Parent!;
        return Path.Combine(dir!.FullName, "src", "DotNetQuest.App.Client", "wwwroot", "content", "worlds");
    }

    private static WorldContent LoadWorld(string name = "w1-csharp-basics")
    {
        var path = Path.Combine(FindContentRoot(), $"{name}.json");
        Assert.True(File.Exists(path), $"World content not found at {path}");
        var world = JsonSerializer.Deserialize<WorldContent>(File.ReadAllText(path), Json);
        Assert.NotNull(world);
        return world!;
    }

    private static List<WorldContent> LoadAvailableWorlds()
    {
        var indexPath = Path.Combine(FindContentRoot(), "index.json");
        var index = JsonSerializer.Deserialize<List<WorldIndexItem>>(File.ReadAllText(indexPath), Json)!;
        return index.Where(w => w.Available)
            .Select(w => LoadWorld(w.Id))
            .ToList();
    }

    [Fact]
    public void World_index_has_twelve_worlds()
    {
        var index = JsonSerializer.Deserialize<List<WorldIndexItem>>(
            File.ReadAllText(Path.Combine(FindContentRoot(), "index.json")), Json);
        Assert.NotNull(index);
        Assert.Equal(12, index!.Count);
        Assert.True(index.Count(w => w.Available) >= 1, "At least one world must be available");
    }

    [Fact]
    public void All_available_worlds_are_fully_bilingual_with_valid_answers()
    {
        foreach (var world in LoadAvailableWorlds())
        {
            Assert.True(world.AllLessons().Count() >= 15, $"{world.Id} needs at least 15 lessons");
            foreach (var lesson in world.AllLessons())
            {
                Assert.False(string.IsNullOrWhiteSpace(lesson.Title.En));
                Assert.False(string.IsNullOrWhiteSpace(lesson.Title.Es));

                foreach (var ex in lesson.Exercises)
                {
                    var where = $"{world.Id}/{lesson.Id}/{ex.Id}";
                    Assert.True(ex.XpReward > 0, $"{where} missing xp");
                    Assert.NotNull(ex.Prompt);
                    Assert.False(string.IsNullOrWhiteSpace(ex.Prompt!.En), $"{where} missing EN prompt");
                    Assert.False(string.IsNullOrWhiteSpace(ex.Prompt.Es), $"{where} missing ES prompt");
                    Assert.NotNull(ex.Explanation);
                    Assert.False(string.IsNullOrWhiteSpace(ex.Explanation!.En), $"{where} missing EN explanation");
                    Assert.False(string.IsNullOrWhiteSpace(ex.Explanation.Es), $"{where} missing ES explanation");
                    Assert.NotNull(ex.Hints);
                    Assert.True(ex.Hints!.Count >= 1, $"{where} needs at least one hint");

                    // Type-specific structural checks.
                    if (ex.Type is ExerciseTypes.Mcq or ExerciseTypes.Output)
                    {
                        Assert.NotNull(ex.Options);
                        Assert.True(ex.Options!.Count >= 2, $"{where} needs at least 2 options");
                        Assert.InRange(ex.AnswerIndex!.Value, 0, ex.Options.Count - 1);
                    }
                    if (ex.Type == ExerciseTypes.TrueFalse)
                    {
                        Assert.InRange(ex.AnswerIndex!.Value, 0, 1);
                    }
                    if (ex.Type == ExerciseTypes.FillBlank)
                    {
                        Assert.NotNull(ex.Code);
                        Assert.NotNull(ex.Blanks);
                        Assert.Equal(ex.Code!.Split("___").Length - 1, ex.Blanks!.Count);
                    }
                    if (ex.Type is ExerciseTypes.Code or ExerciseTypes.BugFix)
                    {
                        Assert.NotNull(ex.Starter);
                        // xUnit exercises discover their tests from [Fact]s at runtime.
                        if (!ex.Xunit)
                        {
                            Assert.NotNull(ex.Tests);
                            Assert.True(ex.Tests!.Count > 0, $"{where} needs at least one test");
                        }
                    }
                    if (ex.Type == ExerciseTypes.Parsons)
                    {
                        Assert.NotNull(ex.Code);
                        Assert.True(ex.Code!.Contains('\n'), $"{where} parsons needs multiple lines");
                    }
                    if (ex.Type == ExerciseTypes.Match)
                    {
                        Assert.NotNull(ex.Pairs);
                        Assert.True(ex.Pairs!.Count >= 3, $"{where} match needs at least 3 pairs");
                        foreach (var pair in ex.Pairs)
                        {
                            Assert.False(string.IsNullOrWhiteSpace(pair.Left.En));
                            Assert.False(string.IsNullOrWhiteSpace(pair.Right.Es));
                        }
                    }
                }
            }
        }
    }

    [Fact]
    public void Available_worlds_have_cheat_sheets()
    {
        foreach (var world in LoadAvailableWorlds())
        {
            Assert.True(world.CheatSheet is { Count: > 0 }, $"{world.Id} is missing its cheat sheet");
        }
    }

    [Fact]
    public void World1_has_25_lessons()
    {
        var world = LoadWorld();
        Assert.Equal(25, world.AllLessons().Count());
        Assert.EndsWith("boss", world.AllLessons().Last().Id);
    }

    [Fact]
    public void All_lesson_and_exercise_ids_are_unique()
    {
        var world = LoadWorld();
        var lessonIds = world.AllLessons().Select(l => l.Id).ToList();
        Assert.Equal(lessonIds.Count, lessonIds.Distinct().Count());

        var exIds = world.AllLessons().SelectMany(l => l.Exercises).Select(e => e.Id).ToList();
        Assert.Equal(exIds.Count, exIds.Distinct().Count());
    }

    [Fact]
    public void Every_exercise_is_fully_bilingual_with_hints()
    {
        var world = LoadWorld();
        foreach (var lesson in world.AllLessons())
        {
            Assert.False(string.IsNullOrWhiteSpace(lesson.Title.En));
            Assert.False(string.IsNullOrWhiteSpace(lesson.Title.Es));
            Assert.False(string.IsNullOrWhiteSpace(lesson.Tip.En));
            Assert.False(string.IsNullOrWhiteSpace(lesson.Tip.Es));

            foreach (var ex in lesson.Exercises)
            {
                var where = $"{lesson.Id}/{ex.Id}";
                Assert.True(ex.XpReward > 0, $"{where} missing xp");
                Assert.NotNull(ex.Prompt);
                Assert.False(string.IsNullOrWhiteSpace(ex.Prompt!.En), $"{where} missing EN prompt");
                Assert.False(string.IsNullOrWhiteSpace(ex.Prompt.Es), $"{where} missing ES prompt");
                Assert.NotNull(ex.Explanation);
                Assert.False(string.IsNullOrWhiteSpace(ex.Explanation!.En), $"{where} missing EN explanation");
                Assert.False(string.IsNullOrWhiteSpace(ex.Explanation.Es), $"{where} missing ES explanation");
                Assert.NotNull(ex.Hints);
                Assert.True(ex.Hints!.Count >= 1, $"{where} needs at least one hint");
                foreach (var hint in ex.Hints)
                {
                    Assert.False(string.IsNullOrWhiteSpace(hint.En));
                    Assert.False(string.IsNullOrWhiteSpace(hint.Es));
                }
            }
        }
    }

    [Fact]
    public void Choice_exercises_have_valid_answers()
    {
        var world = LoadWorld();
        foreach (var lesson in world.AllLessons())
        {
            foreach (var ex in lesson.Exercises.Where(e => e.Type is ExerciseTypes.Mcq or ExerciseTypes.Output))
            {
                var where = $"{lesson.Id}/{ex.Id}";
                Assert.NotNull(ex.Options);
                Assert.True(ex.Options!.Count >= 2, $"{where} needs at least 2 options");
                Assert.NotNull(ex.AnswerIndex);
                Assert.InRange(ex.AnswerIndex!.Value, 0, ex.Options.Count - 1);
                foreach (var opt in ex.Options)
                {
                    Assert.False(string.IsNullOrWhiteSpace(opt.En));
                    Assert.False(string.IsNullOrWhiteSpace(opt.Es));
                }
            }

            foreach (var ex in lesson.Exercises.Where(e => e.Type == ExerciseTypes.TrueFalse))
            {
                var where = $"{lesson.Id}/{ex.Id}";
                Assert.NotNull(ex.AnswerIndex);
                Assert.InRange(ex.AnswerIndex!.Value, 0, 1);
            }
        }
    }

    [Fact]
    public void Fill_blank_exercises_have_matching_blanks()
    {
        var world = LoadWorld();
        foreach (var lesson in world.AllLessons())
        {
            foreach (var ex in lesson.Exercises.Where(e => e.Type == ExerciseTypes.FillBlank))
            {
                var where = $"{lesson.Id}/{ex.Id}";
                Assert.NotNull(ex.Code);
                Assert.NotNull(ex.Blanks);
                var holes = ex.Code!.Split("___").Length - 1;
                Assert.Equal(holes, ex.Blanks!.Count);
                foreach (var b in ex.Blanks)
                    Assert.False(string.IsNullOrWhiteSpace(b.Answer));
            }
        }
    }

    [Fact]
    public void Code_exercises_have_tests_and_non_empty_starter()
    {
        var world = LoadWorld();
        foreach (var lesson in world.AllLessons())
        {
            foreach (var ex in lesson.Exercises.Where(e => e.Type == ExerciseTypes.Code))
            {
                var where = $"{lesson.Id}/{ex.Id}";
                Assert.NotNull(ex.Starter);
                Assert.NotNull(ex.Tests);
                Assert.True(ex.Tests!.Count > 0, $"{where} needs at least one test");
                foreach (var t in ex.Tests)
                    Assert.False(string.IsNullOrWhiteSpace(t.ExpectedOutput), $"{where} test '{t.Name}' has no expected output");
            }
        }
    }
}
