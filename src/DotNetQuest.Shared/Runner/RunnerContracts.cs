using System.Text.Json.Serialization;

namespace DotNetQuest.Shared.Runner;

/// <summary>Request sent to the sandboxed runner process (via stdin JSON).</summary>
public record RunRequest(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("stdin")] string? Stdin,
    [property: JsonPropertyName("tests")] List<RunTest> Tests,
    [property: JsonPropertyName("timeoutMs")] int TimeoutMs = 8000,
    [property: JsonPropertyName("xunit")] bool Xunit = false);

public record RunTest(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("expectedOutput")] string ExpectedOutput,
    [property: JsonPropertyName("match")] string Match = "exact",
    [property: JsonPropertyName("stdin")] string? Stdin = null,
    /// <summary>When set, the test exercises a hosted minimal-API endpoint (GET) instead of console output.
    /// The learner code must expose a public static BuildApp() returning a WebApplication.</summary>
    [property: JsonPropertyName("apiPath")] string? ApiPath = null,
    [property: JsonPropertyName("apiMethod")] string ApiMethod = "GET",
    [property: JsonPropertyName("expectedStatus")] int? ExpectedStatus = null);

public record RunTestResult(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("passed")] bool Passed,
    [property: JsonPropertyName("expected")] string Expected,
    [property: JsonPropertyName("actual")] string Actual);

/// <summary>Result returned by the runner process (via stdout JSON).</summary>
public record RunResult(
    [property: JsonPropertyName("compiled")] bool Compiled,
    [property: JsonPropertyName("diagnostics")] List<string> Diagnostics,
    [property: JsonPropertyName("output")] string Output,
    [property: JsonPropertyName("tests")] List<RunTestResult> Tests,
    [property: JsonPropertyName("timedOut")] bool TimedOut,
    [property: JsonPropertyName("elapsedMs")] int ElapsedMs,
    [property: JsonPropertyName("error")] string? Error)
{
    public static RunResult Fail(string error) =>
        new(false, [], string.Empty, [], false, 0, error);

    public bool AllTestsPassed => Tests.Count > 0 && Tests.All(t => t.Passed);
}
