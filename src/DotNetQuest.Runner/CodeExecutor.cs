using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using DotNetQuest.Shared.Runner;
using Microsoft.AspNetCore.Builder;

namespace DotNetQuest.Runner;

/// <summary>
/// Compiles and executes untrusted learner code inside this throwaway process.
/// The parent API kills this process on timeout, so runaway code (infinite
/// loops) can never wedge the game server.
/// </summary>
public static class CodeExecutor
{
    private const int MaxOutputChars = 64_000;

    /// <summary>
    /// Assembly-name prefixes learner code may reference. Broad by design (the
    /// whole platform: EF Core, ASP.NET Core, SQLite natives) — sandboxing comes
    /// from process isolation and timeouts, not from this list.
    /// </summary>
    private static readonly string[] AllowedAssemblyPrefixes =
    {
        "System.",
        "Microsoft.",
        "SQLitePCLRaw",
        "DotNetQuest.",
        "netstandard"
    };

    public static RunResult Run(RunRequest request) => Run(request, hardExitOnTimeout: true);

    /// <summary>
    /// Compiles and executes the learner code. With hardExitOnTimeout (runner
    /// process mode) a timeout kills the process; in library mode (tests) it
    /// simply reports TimedOut and abandons the runaway worker thread.
    /// </summary>
    public static RunResult Run(RunRequest request, bool hardExitOnTimeout)
    {
        var timeoutMs = Math.Clamp(request.TimeoutMs, 1000, 15_000);

        RunResult? final = null;
        using var done = new ManualResetEventSlim(false);
        var worker = new Thread(() =>
        {
            try
            {
                final = RunCore(request);
            }
            catch (Exception ex)
            {
                final = RunResult.Fail(Describe(ex));
            }
            finally
            {
                done.Set();
            }
        })
        { IsBackground = true };
        worker.Start();

        var budgetMs = hardExitOnTimeout
            ? timeoutMs * Math.Max(request.Tests.Count, 1) + 4_000
            : timeoutMs + 250;

        if (done.Wait(TimeSpan.FromMilliseconds(budgetMs)) && final is not null)
            return final;

        if (hardExitOnTimeout)
            Environment.Exit(124);

        return new RunResult(false, [], string.Empty, [], true, timeoutMs,
            "Your code took too long to finish. Check for infinite loops!");
    }

    private static RunResult RunCore(RunRequest request)
    {
        var isApiMode = request.Tests.Any(t => t.ApiPath is not null);

        var syntaxTree = CSharpSyntaxTree.ParseText(request.Code,
            new CSharpParseOptions(languageVersion: LanguageVersion.Latest));

        // Mirror the SDK's implicit usings so beginner code (Console, Math, List...)
        // compiles without explicit using directives.
        var implicitUsings = CSharpSyntaxTree.ParseText(
            """
            global using System;
            global using System.Collections.Generic;
            global using System.IO;
            global using System.Linq;
            global using System.Net.Http;
            global using System.Threading;
            global using System.Threading.Tasks;
            global using DotNetQuest.Runner.Testing;
            """,
            new CSharpParseOptions(languageVersion: LanguageVersion.Latest));

        var compilation = CSharpCompilation.Create(
            "LearnerCode",
            [implicitUsings, syntaxTree],
            LoadReferences(),
            new CSharpCompilationOptions(
                // API exercises define a BuildApp() method instead of an entry point.
                isApiMode || request.Xunit
                    ? OutputKind.DynamicallyLinkedLibrary
                    : OutputKind.ConsoleApplication,
                allowUnsafe: false,
                optimizationLevel: OptimizationLevel.Release,
                warningLevel: 1));

        using var peStream = new MemoryStream();
        var emit = compilation.Emit(peStream);

        if (!emit.Success)
        {
            var diagnostics = emit.Diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Take(10)
                .Select(d => FormatDiagnostic(d))
                .ToList();
            return new RunResult(false, diagnostics, string.Empty, [], false, 0, null);
        }

        var loaded = Assembly.Load(peStream.ToArray());
        if (request.Xunit)
            return RunXunitFacts(loaded);
        return isApiMode
            ? RunApiTests(loaded, request)
            : RunConsoleTests(loaded, request);
    }

    /// <summary>
    /// xUnit mode: the learner writes real [Fact] / [Theory] tests. We discover
    /// them by reflection, run each, and report per-test pass/fail.
    /// </summary>
    private static RunResult RunXunitFacts(Assembly assembly)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var results = new List<RunTestResult>();
        var found = 0;

        foreach (var type in assembly.GetTypes().Where(t => t.IsPublic))
        {
            object? instance = null;
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                var attrs = method.GetCustomAttributes(false);
                var isFact = attrs.Any(a => a.GetType().Name == "FactAttribute");
                var theories = attrs.OfType<object>()
                    .Where(a => a.GetType().Name == "InlineDataAttribute")
                    .ToList();

                if (!isFact && theories.Count == 0)
                    continue;

                if (!method.IsStatic && instance is null)
                    instance = Activator.CreateInstance(type);

                if (isFact)
                {
                    found++;
                    results.Add(RunOneTest($"{type.Name}.{method.Name}", () => method.Invoke(instance, [])));
                }

                foreach (var row in theories)
                {
                    found++;
                    var data = (object[]?)row.GetType().GetProperty("DataValues")?.GetValue(row) ?? [];
                    var args = string.Join(", ", data.Select(d => d?.ToString() ?? "null"));
                    var localData = data;
                    results.Add(RunOneTest($"{type.Name}.{method.Name}({args})", () => method.Invoke(instance, localData)));
                }
            }
        }

        sw.Stop();
        if (found == 0)
            return new RunResult(true, [], string.Empty,
            [
                new RunTestResult("tests", false, "at least one [Fact] or [Theory] test",
                    "none found — write [Fact] public void MyTest() { ... }")
            ], false, (int)sw.ElapsedMilliseconds, null);

        return new RunResult(true, [], $"Ran {found} test(s).", results, false, (int)sw.ElapsedMilliseconds, null);
    }

    private static RunTestResult RunOneTest(string name, Action invoke)
    {
        try
        {
            invoke();
            return new RunTestResult(name, true, "no exception", "passed");
        }
        catch (TargetInvocationException tie) when (tie.InnerException is not null)
        {
            return new RunTestResult(name, false, "no exception", Describe(tie.InnerException));
        }
        catch (Exception ex)
        {
            return new RunTestResult(name, false, "no exception", Describe(ex));
        }
    }

    /// <summary>
    /// API mode: the learner exposes public static BuildApp() returning a
    /// WebApplication. We host it on a random local port and hit it with real
    /// HTTP requests, one per test case.
    /// </summary>
    private static RunResult RunApiTests(Assembly assembly, RunRequest request)
    {
        MethodInfo? buildApp = null;
        foreach (var type in assembly.GetTypes())
        {
            buildApp = type.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(m => m.Name == "BuildApp" && m.GetParameters().Length == 0);
            if (buildApp is not null) break;
        }

        if (buildApp is null)
            return new RunResult(true, [], string.Empty,
            [
                new RunTestResult("BuildApp", false,
                    "a public static BuildApp() method returning WebApplication",
                    "not found — define: public static WebApplication BuildApp() { ... }")
            ], false, 0, null);

        WebApplication? app = null;
        try
        {
            app = (WebApplication?)buildApp.Invoke(null, []);
            if (app is null)
                return RunResult.Fail("BuildApp() returned null — it must return the configured WebApplication.");

            app.Urls.Add("http://127.0.0.1:0");
            app.StartAsync().GetAwaiter().GetResult();
            var baseUrl = app.Urls.First();

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var results = new List<RunTestResult>();
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

            foreach (var test in request.Tests)
            {
                string body;
                int status;
                string? error = null;
                try
                {
                    using var response = http.GetAsync(baseUrl.TrimEnd('/') + test.ApiPath).GetAwaiter().GetResult();
                    status = (int)response.StatusCode;
                    body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    body = string.Empty;
                    status = 0;
                    error = Describe(ex);
                }

                var statusOk = test.ExpectedStatus is null || test.ExpectedStatus == status;
                var bodyOk = error is null && Matches(body, test.ExpectedOutput, test.Match);
                var actual = error ?? $"[{status}] {body}";
                results.Add(new RunTestResult(test.Name, statusOk && bodyOk, test.ExpectedOutput, actual));
            }

            sw.Stop();
            return new RunResult(true, [], string.Empty, results, false, (int)sw.ElapsedMilliseconds, null);
        }
        catch (TargetInvocationException tie) when (tie.InnerException is not null)
        {
            return new RunResult(true, [], string.Empty,
            [
                new RunTestResult("BuildApp", false, "a working app", Describe(tie.InnerException))
            ], false, 0, null);
        }
        catch (Exception ex)
        {
            return new RunResult(true, [], string.Empty,
            [
                new RunTestResult("BuildApp", false, "a working app", Describe(ex))
            ], false, 0, null);
        }
        finally
        {
            try { app?.StopAsync().GetAwaiter().GetResult(); } catch { /* best effort */ }
            try { app?.DisposeAsync().AsTask().GetAwaiter().GetResult(); } catch { /* best effort */ }
        }
    }

    private static RunResult RunConsoleTests(Assembly assembly, RunRequest request)
    {
        var entry = assembly.EntryPoint
            ?? throw new InvalidOperationException("No entry point found. Write your statements at the top level (or a Main method).");

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var results = new List<RunTestResult>();
        var combinedOutput = new StringBuilder();
        string? runtimeError = null;

        // Run the program once per test case (fresh output each time). With no
        // test cases (free playground), still execute once so the output is shown.
        var runs = request.Tests.Count > 0
            ? request.Tests
            : [new RunTest("run", string.Empty, "contains", request.Stdin)];

        foreach (var test in runs)
        {
            var outputBuffer = new CappedWriter();
            string? error = null;
            try
            {
                var originalOut = Console.Out;
                var originalIn = Console.In;
                try
                {
                    Console.SetOut(outputBuffer);
                    Console.SetIn(new StringReader(test.Stdin ?? request.Stdin ?? string.Empty));
                    var entryResult = entry.Invoke(null, [Array.Empty<string>()]);
                    // Async top-level programs (await in Main) return a Task —
                    // wait for it INSIDE the redirection window so output is captured.
                    if (entryResult is Task task)
                        task.GetAwaiter().GetResult();
                }
                finally
                {
                    Console.SetOut(originalOut);
                    Console.SetIn(originalIn);
                }
            }
            catch (TargetInvocationException tie) when (tie.InnerException is not null)
            {
                error = Describe(tie.InnerException);
            }
            catch (Exception ex)
            {
                error = Describe(ex);
            }

            var actual = outputBuffer.ToString();
            if (error is not null)
                actual = (actual.Length > 0 ? actual + "\n" : string.Empty) + error;

            combinedOutput.AppendLine(actual.TrimEnd());

            if (request.Tests.Count > 0)
            {
                var passed = string.IsNullOrEmpty(error) && Matches(actual, test.ExpectedOutput, test.Match);
                results.Add(new RunTestResult(test.Name, passed, test.ExpectedOutput, actual));
            }
        }

        sw.Stop();
        return new RunResult(true, [], combinedOutput.ToString(), results, false, (int)sw.ElapsedMilliseconds, runtimeError);
    }

    private static bool Matches(string actual, string expected, string match)
    {
        var a = Normalize(actual);
        var e = Normalize(expected);
        return match switch
        {
            "contains" => a.Contains(e, StringComparison.Ordinal),
            "regex" => System.Text.RegularExpressions.Regex.IsMatch(a, expected),
            _ => string.Equals(a, e, StringComparison.Ordinal)
        };
    }

    private static string Normalize(string s) =>
        string.Join("\n", s.Replace("\r\n", "\n").Split('\n').Select(line => line.TrimEnd()))
            .Trim();

    private static string Describe(Exception ex)
    {
        var message = ex.Message;
        var stack = ex.StackTrace?.Split('\n').Take(3) ?? [];
        return $"{ex.GetType().Name}: {message}\n{string.Join("\n", stack)}".TrimEnd();
    }

    private static string FormatDiagnostic(Diagnostic d)
    {
        var line = d.Location.GetLineSpan().StartLinePosition;
        return $"Line {line.Line + 1}, Col {line.Character + 1}: {d.Id}: {d.GetMessage()}";
    }

    private static MetadataReference[] LoadReferences()
    {
        var references = new List<MetadataReference>();
        var tpa = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? string.Empty;

        foreach (var path in tpa.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var name = Path.GetFileNameWithoutExtension(path);
            if (!AllowedAssemblyPrefixes.Any(p => name.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
                continue;
            try
            {
                references.Add(MetadataReference.CreateFromFile(path));
            }
            catch
            {
                // Skip anything unreadable; the core reference below is the essential one.
            }
        }

        // Guarantee the core library is present even if the TPA scan missed it.
        var core = typeof(object).Assembly.Location;
        if (core.Length > 0 && references.All(r => r.Display != core))
            references.Add(MetadataReference.CreateFromFile(core));

        return [.. references];
    }

    /// <summary>A TextWriter that captures learner console output and stops after the cap.</summary>
    private sealed class CappedWriter : TextWriter
    {
        private readonly StringBuilder _buffer = new();
        private bool _capped;

        public override Encoding Encoding => Encoding.UTF8;

        public override void Write(char value)
        {
            if (_capped) return;
            if (_buffer.Length >= MaxOutputChars)
            {
                _buffer.Append("\n[output truncated — too much text]");
                _capped = true;
                return;
            }
            _buffer.Append(value);
        }

        public override void Write(string? value)
        {
            if (value is null) return;
            foreach (var ch in value)
                Write(ch);
        }

        public override void WriteLine(string? value)
        {
            Write(value);
            Write('\n');
        }

        public override string ToString() => _buffer.ToString();
    }
}
