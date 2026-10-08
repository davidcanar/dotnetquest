using DotNetQuest.Shared.Runner;
using DotNetQuest.Runner;

namespace DotNetQuest.Tests;

/// <summary>Integration tests for the sandboxed C# code executor.</summary>
[Collection("CodeExecutor")]
public class CodeExecutorTests
{
    private static RunResult Run(string code, List<RunTest>? tests = null, string? stdin = null) =>
        CodeExecutor.Run(new RunRequest(code, stdin, tests ?? [new RunTest("output", "", "contains")], TimeoutMs: 8000));

    [Fact]
    public void Runs_a_simple_program()
    {
        var result = Run("Console.WriteLine(\"Hello from the sandbox\");");
        Assert.True(result.Compiled, string.Join("\n", result.Diagnostics));
        Assert.Equal("Hello from the sandbox", result.Output.Trim());
        Assert.True(result.Tests[0].Passed);
    }

    [Fact]
    public void Test_exact_match_fails_on_wrong_output()
    {
        var result = Run("Console.WriteLine(2 + 2);", [new RunTest("math", "5", "exact")]);
        Assert.True(result.Compiled);
        Assert.False(result.Tests[0].Passed);
        Assert.Equal("4", result.Tests[0].Actual.Trim());
    }

    [Fact]
    public void Test_multiple_lines_are_normalized()
    {
        var result = Run("for (int i = 1; i <= 3; i++) Console.WriteLine(i);",
            [new RunTest("count", "1\r\n2\r\n3", "exact")]);
        Assert.True(result.Compiled);
        Assert.True(result.Tests[0].Passed);
    }

    [Fact]
    public void Stdin_is_available_to_the_program()
    {
        var result = Run(
            "string name = Console.ReadLine();\nConsole.WriteLine($\"Hello, {name}!\");",
            [new RunTest("greet", "Hello, Bit!", "exact", Stdin: "Bit")]);
        Assert.True(result.Compiled);
        Assert.True(result.Tests[0].Passed, result.Tests[0].Actual);
    }

    [Fact]
    public void Compile_errors_are_reported_as_diagnostics()
    {
        var result = Run("Console.Writeline(\"oops\");");
        Assert.False(result.Compiled);
        Assert.NotEmpty(result.Diagnostics);
        Assert.Contains("CS", result.Diagnostics[0]);
    }

    [Fact]
    public void Runtime_exceptions_surface_in_output()
    {
        var result = Run("int x = int.Parse(\"not a number\");\nConsole.WriteLine(x);",
            [new RunTest("parse", "42", "exact")]);
        Assert.True(result.Compiled);
        Assert.False(result.Tests[0].Passed);
        Assert.Contains("FormatException", result.Tests[0].Actual);
    }

    [Fact]
    public void Infinite_loop_is_reported_as_timeout()
    {
        // Short timeout so the test stays fast; the executor clamps to >= 1s.
        var request = new RunRequest("while (true) { }", null, [new RunTest("never", "x", "exact")], TimeoutMs: 1200);
        var result = CodeExecutor.Run(request, hardExitOnTimeout: false);
        Assert.True(result.TimedOut);
    }

    [Fact]
    public void Linq_is_available()
    {
        var result = Run(
            "var nums = new[] { 1, 2, 3, 4 };\nConsole.WriteLine(nums.Where(n => n > 2).Sum());",
            [new RunTest("sum", "7", "exact")]);
        Assert.True(result.Compiled, string.Join("\n", result.Diagnostics));
        Assert.True(result.Tests[0].Passed);
    }

    [Fact]
    public void Unsafe_code_is_rejected()
    {
        var result = Run("unsafe { }");
        Assert.False(result.Compiled);
    }
}
