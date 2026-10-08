using DotNetQuest.Shared;
using DotNetQuest.Shared.Content;

namespace DotNetQuest.Tests;

public class GraderTests
{
    private static ExerciseContent Mcq(int answerIndex = 1) => new(
        "e1", ExerciseTypes.Mcq,
        new Loc("p", "p"), null,
        [new Loc("a", "a"), new Loc("b", "b")],
        answerIndex, null, null, false, null, null,
        new Loc("exp", "exp"), [new Loc("h", "h")], 10);

    [Fact]
    public void Mcq_correct_index_passes()
    {
        var result = Grader.Check(Mcq(1), new AnswerSubmission(OptionIndex: 1));
        Assert.True(result.Correct);
    }

    [Fact]
    public void Mcq_wrong_index_fails_and_returns_explanation()
    {
        var result = Grader.Check(Mcq(1), new AnswerSubmission(OptionIndex: 0));
        Assert.False(result.Correct);
        Assert.NotNull(result.Explanation);
    }

    [Fact]
    public void TrueFalse_uses_option_index()
    {
        var ex = Mcq(0) with { Type = ExerciseTypes.TrueFalse };
        Assert.True(Grader.Check(ex, new AnswerSubmission(OptionIndex: 0)).Correct);
        Assert.False(Grader.Check(ex, new AnswerSubmission(OptionIndex: 1)).Correct);
    }

    [Fact]
    public void FillBlank_case_sensitive_by_default()
    {
        var ex = Mcq(0) with
        {
            Type = ExerciseTypes.FillBlank,
            Code = "___ x = 5;",
            Blanks = [new Blank("int", CaseSensitive: true)]
        };
        Assert.True(Grader.Check(ex, new AnswerSubmission(Blanks: ["int"])).Correct);
        Assert.False(Grader.Check(ex, new AnswerSubmission(Blanks: ["INT"])).Correct);
    }

    [Fact]
    public void FillBlank_case_insensitive_when_flag_off()
    {
        var ex = Mcq(0) with
        {
            Type = ExerciseTypes.FillBlank,
            Code = "bool b = ___;",
            Blanks = [new Blank("false", CaseSensitive: false)]
        };
        Assert.True(Grader.Check(ex, new AnswerSubmission(Blanks: ["FALSE"])).Correct);
    }

    [Fact]
    public void FillBlank_trims_whitespace()
    {
        var ex = Mcq(0) with
        {
            Type = ExerciseTypes.FillBlank,
            Blanks = [new Blank(">=", true)]
        };
        Assert.True(Grader.Check(ex, new AnswerSubmission(Blanks: [" >= "])).Correct);
    }

    [Fact]
    public void TypeIn_accepts_any_accepted_value_case_insensitive()
    {
        var ex = Mcq(0) with
        {
            Type = ExerciseTypes.TypeIn,
            Accepted = ["WriteLine", "writeline"],
            CaseSensitive = false
        };
        Assert.True(Grader.Check(ex, new AnswerSubmission(Text: "  writeline ")).Correct);
        Assert.False(Grader.Check(ex, new AnswerSubmission(Text: "Write")).Correct);
    }

    [Fact]
    public void FillBlank_wrong_count_fails()
    {
        var ex = Mcq(0) with
        {
            Type = ExerciseTypes.FillBlank,
            Blanks = [new Blank("a"), new Blank("b")]
        };
        Assert.False(Grader.Check(ex, new AnswerSubmission(Blanks: ["a"])).Correct);
    }
}
