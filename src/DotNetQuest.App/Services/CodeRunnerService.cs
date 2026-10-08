using System.Diagnostics;
using System.Text;
using System.Text.Json;
using DotNetQuest.Shared.Runner;

namespace DotNetQuest.App.Services;

/// <summary>
/// Runs learner C# by delegating to the DotNetQuest.Runner child process.
/// Process isolation means runaway code is killed by the timeout without
/// affecting the game server.
/// </summary>
public sealed class CodeRunnerService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ILogger<CodeRunnerService> _logger;
    private readonly string _runnerPath;
    private readonly SemaphoreSlim _gate = new(2, 2);

    public CodeRunnerService(ILogger<CodeRunnerService> logger, IHostEnvironment env)
    {
        _logger = logger;
        _runnerPath = Path.Combine(AppContext.BaseDirectory, "runner", "DotNetQuest.Runner.dll");
        if (!File.Exists(_runnerPath))
            _logger.LogWarning("Runner executable not found at {Path}. Code exercises will fail until the build copies it.", _runnerPath);
    }

    public bool IsAvailable => File.Exists(_runnerPath);

    public async Task<RunResult> RunAsync(string code, List<RunTest>? tests = null, string? stdin = null, bool xunit = false, CancellationToken ct = default)
    {
        if (!IsAvailable)
            return RunResult.Fail("The code runner is not installed on the server. Rebuild the solution and try again.");

        var request = new RunRequest(code, stdin, tests ?? [], TimeoutMs: 8000, Xunit: xunit);
        await _gate.WaitAsync(ct);
        try
        {
            return await ExecuteInProcessAsync(request, ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<RunResult> ExecuteInProcessAsync(RunRequest request, CancellationToken ct)
    {
        // Fresh working directory per run: learner File I/O and SQLite databases
        // stay isolated and vanish with the run.
        var workDir = Path.Combine(Path.GetTempPath(), "dqrun_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDir);

        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = workDir,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        psi.ArgumentList.Add(_runnerPath);

        using var process = new Process { StartInfo = psi };
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(request.TimeoutMs / 1000.0 + 20));

        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start the runner process");
            TryDeleteDir(workDir);
            return RunResult.Fail("Could not start the code runner. Is the .NET runtime available on the server?");
        }

        // Write the request up front (it is small), then read everything back.
        await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(request, JsonOptions));
        process.StandardInput.Dispose();

        var stdoutTask = process.StandardOutput.ReadToEndAsync(timeoutCts.Token);
        var stderrTask = process.StandardError.ReadToEndAsync(timeoutCts.Token);

        RunResult result;
        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
            result = await CollectResultAsync(process, await stdoutTask, await stderrTask);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // Our own timeout fired — kill the runaway process tree.
            TryKill(process);
            result = new RunResult(false, [], string.Empty, [], true, request.TimeoutMs,
                "Your code took too long to finish. Check for infinite loops!");
        }
        finally
        {
            TryDeleteDir(workDir);
        }

        if (ct.IsCancellationRequested)
        {
            TryKill(process);
            return RunResult.Fail("The request was cancelled.");
        }
        return result;
    }

    private async Task<RunResult> CollectResultAsync(Process process, string stdout, string stderr)
    {
        if (process.ExitCode == 124)
            return new RunResult(false, [], string.Empty, [], true, 0,
                "Your code took too long to finish. Check for infinite loops!");

        // The runner prints exactly one line of JSON on stdout.
        var jsonLine = stdout
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .LastOrDefault(line => line.StartsWith('{'));
        if (jsonLine is null)
        {
            _logger.LogError("Runner produced no result JSON. Exit code: {Code}. Stderr: {Stderr}", process.ExitCode, stderr);
            return RunResult.Fail("The code runner crashed while executing this program.");
        }

        try
        {
            var result = JsonSerializer.Deserialize<RunResult>(jsonLine, JsonOptions);
            return result ?? RunResult.Fail("The code runner returned an unreadable result.");
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Runner JSON was malformed: {Json}", jsonLine[..Math.Min(500, jsonLine.Length)]);
            return RunResult.Fail("The code runner returned an unreadable result.");
        }
    }

    private static void TryDeleteDir(string path)
    {
        try { Directory.Delete(path, recursive: true); } catch { /* temp cleanup best effort */ }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch
        {
            // Already gone.
        }
    }
}
