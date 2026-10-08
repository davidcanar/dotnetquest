using System.Text.Json;
using DotNetQuest.Shared.Runner;
using DotNetQuest.Runner;

try
{
    using var stdin = Console.OpenStandardInput();
    using var ms = new MemoryStream();
    stdin.CopyTo(ms);
    var request = JsonSerializer.Deserialize<RunRequest>(ms.ToArray())!;
    var result = CodeExecutor.Run(request);
    Console.Out.WriteLine(JsonSerializer.Serialize(result));
}
catch (Exception ex)
{
    // Emit a well-formed failure so the parent never has to guess.
    var result = RunResult.Fail($"Runner failed to process the request: {ex.Message}");
    try
    {
        Console.Out.WriteLine(JsonSerializer.Serialize(result));
    }
    catch
    {
        Environment.Exit(1);
    }
}
