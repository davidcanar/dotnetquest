using DotNetQuest.Shared.Content;

namespace DotNetQuest.Shared;

/// <summary>The player's answer to an exercise, submitted to the grader.</summary>
public record AnswerSubmission(
    int? OptionIndex = null,
    List<string>? Blanks = null,
    string? Text = null,
    List<string>? Lines = null,
    List<int>? MatchSelection = null);

/// <summary>Result of grading one exercise.</summary>
public record GradeResult(bool Correct, Loc? Explanation);

/// <summary>
/// Pure answer-checking logic for every exercise type. Shared by client and tests.
/// Code exercises are graded by the sandboxed runner instead.
/// </summary>
public static class Grader
{
    public static GradeResult Check(ExerciseContent ex, AnswerSubmission answer)
    {
        var correct = ex.Type switch
        {
            ExerciseTypes.Mcq or ExerciseTypes.Output or ExerciseTypes.TrueFalse => CheckOption(ex, answer),
            ExerciseTypes.FillBlank => CheckBlanks(ex, answer),
            ExerciseTypes.TypeIn => CheckText(ex, answer),
            ExerciseTypes.Parsons => CheckLines(ex, answer),
            ExerciseTypes.Match => CheckMatch(ex, answer),
            _ => false
        };
        return new GradeResult(correct, ex.Explanation);
    }

    private static bool CheckOption(ExerciseContent ex, AnswerSubmission answer) =>
        answer.OptionIndex is int i && i == ex.AnswerIndex;

    private static bool CheckBlanks(ExerciseContent ex, AnswerSubmission answer)
    {
        if (ex.Blanks is null || answer.Blanks is null || answer.Blanks.Count != ex.Blanks.Count)
            return false;

        for (var i = 0; i < ex.Blanks.Count; i++)
        {
            var expected = ex.Blanks[i].Answer;
            var actual = answer.Blanks[i] ?? string.Empty;
            var comparison = ex.Blanks[i].CaseSensitive
                ? StringComparison.Ordinal
                : StringComparison.OrdinalIgnoreCase;
            if (!string.Equals(expected.Trim(), actual.Trim(), comparison))
                return false;
        }
        return true;
    }

    private static bool CheckText(ExerciseContent ex, AnswerSubmission answer)
    {
        if (answer.Text is null || ex.Accepted is null || ex.Accepted.Count == 0)
            return false;

        var comparison = ex.CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        var actual = Normalize(answer.Text);
        return ex.Accepted.Any(a => Normalize(a).Equals(actual, comparison));
    }

    private static bool CheckLines(ExerciseContent ex, AnswerSubmission answer)
    {
        if (ex.Code is null || answer.Lines is null)
            return false;

        var expected = ex.Code.Replace("\r\n", "\n")
            .Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .ToList();
        var actual = answer.Lines
            .Select(l => (l ?? string.Empty).Trim())
            .Where(l => l.Length > 0)
            .ToList();

        if (expected.Count != actual.Count)
            return false;
        for (var i = 0; i < expected.Count; i++)
            if (!string.Equals(expected[i], actual[i], StringComparison.Ordinal))
                return false;
        return true;
    }

    private static bool CheckMatch(ExerciseContent ex, AnswerSubmission answer)
    {
        if (ex.Pairs is null || answer.MatchSelection is null || answer.MatchSelection.Count != ex.Pairs.Count)
            return false;
        for (var i = 0; i < ex.Pairs.Count; i++)
            if (answer.MatchSelection[i] != i)
                return false;
        return true;
    }

    private static string Normalize(string s)
    {
        var trimmed = s.Trim().Replace("\r\n", "\n");
        while (trimmed.Contains("  "))
            trimmed = trimmed.Replace("  ", " ");
        return trimmed;
    }
}
