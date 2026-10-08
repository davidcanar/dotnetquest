using DotNetQuest.Shared.Runner;
using DotNetQuest.Runner;
using Xunit;

namespace DotNetQuest.Tests;

/// <summary>Phase 3 sandbox capabilities: real SQLite via EF Core and live minimal-API hosting.</summary>
[Collection("CodeExecutor")]
public class SandboxUpgradeTests
{
    [Fact]
    public void EfCore_writes_and_reads_a_real_database()
    {
        const string code = """
            using Microsoft.EntityFrameworkCore;

            using var db = new Shop();
            db.Database.EnsureDeleted();
            db.Database.EnsureCreated();
            db.Players.Add(new Player { Name = "Ada" });
            db.SaveChanges();
            Console.WriteLine(db.Players.Single().Name);

            class Player
            {
                public int Id { get; set; }
                public string Name { get; set; } = "";
            }

            class Shop : DbContext
            {
                public DbSet<Player> Players => Set<Player>();
                protected override void OnConfiguring(DbContextOptionsBuilder o) => o.UseSqlite("Data Source=quest.db");
            }
            """;

        var result = CodeExecutor.Run(
            new RunRequest(code, null, [new RunTest("roundtrip", "Ada", "exact")], TimeoutMs: 10_000),
            hardExitOnTimeout: false);

        Assert.True(result.Compiled, string.Join('\n', result.Diagnostics));
        Assert.True(result.Tests[0].Passed, result.Tests[0].Actual);
    }

    [Fact]
    public void MinimalApi_is_hosted_and_called_over_http()
    {
        const string code = """
            using Microsoft.AspNetCore.Builder;
            using Microsoft.AspNetCore.Http;
            using Microsoft.AspNetCore.Routing;

            public static class Api
            {
                public static WebApplication BuildApp()
                {
                    var builder = WebApplication.CreateBuilder();
                    var app = builder.Build();
                    app.MapGet("/hello", () => "Hello, DevQuest!");
                    app.MapGet("/double/{n}", (int n) => (n * 2).ToString());
                    return app;
                }
            }
            """;

        var result = CodeExecutor.Run(
            new RunRequest(code, null,
            [
                new RunTest("hello", "Hello, DevQuest!", "exact", ApiPath: "/hello"),
                new RunTest("double", "42", "exact", ApiPath: "/double/21")
            ], TimeoutMs: 10_000),
            hardExitOnTimeout: false);

        Assert.True(result.Compiled, string.Join('\n', result.Diagnostics));
        Assert.All(result.Tests, t => Assert.True(t.Passed, t.Actual));
    }

    [Fact]
    public void Async_top_level_programs_await_to_completion()
    {
        const string code = """
            async Task BrewAsync()
            {
                await Task.Delay(20);
                Console.WriteLine("coffee ready");
            }

            Console.WriteLine("start");
            await BrewAsync();
            Console.WriteLine("end");
            """;

        var result = CodeExecutor.Run(
            new RunRequest(code, null, [new RunTest("full run", "start\ncoffee ready\nend", "exact")], TimeoutMs: 8000),
            hardExitOnTimeout: false);

        Assert.True(result.Compiled, string.Join("\n", result.Diagnostics));
        Assert.True(result.Tests[0].Passed, result.Tests[0].Actual);
    }

    [Fact]
    public void Xunit_facts_run_and_report_pass_fail()
    {
        const string code = """
            public class Calculator
            {
                public int Add(int a, int b) => a + b;
            }

            public class CalculatorTests
            {
                [DotNetQuest.Runner.Testing.Fact]
                public void Adds_correctly()
                {
                    DotNetQuest.Runner.Testing.Assert.Equal(4, new Calculator().Add(2, 2));
                }

                [DotNetQuest.Runner.Testing.Fact]
                public void Fails_on_purpose()
                {
                    DotNetQuest.Runner.Testing.Assert.Equal(5, new Calculator().Add(2, 2));
                }
            }
            """;

        var result = CodeExecutor.Run(
            new RunRequest(code, null, [], TimeoutMs: 8000, Xunit: true),
            hardExitOnTimeout: false);

        Assert.True(result.Compiled, string.Join(Environment.NewLine, result.Diagnostics));
        Assert.Equal(2, result.Tests.Count);
        Assert.True(result.Tests[0].Passed, result.Tests[0].Actual);
        Assert.False(result.Tests[1].Passed);
        Assert.Contains("4", result.Tests[1].Actual);
    }

    [Fact]
    public void Missing_BuildApp_is_reported_clearly()
    {
        const string code = "class Nothing { }";

        var result = CodeExecutor.Run(
            new RunRequest(code, null, [new RunTest("x", "", "contains", ApiPath: "/x")], TimeoutMs: 5000),
            hardExitOnTimeout: false);

        Assert.True(result.Compiled);
        Assert.False(result.Tests[0].Passed);
        Assert.Contains("BuildApp", result.Tests[0].Actual);
    }

    [Fact]
    public void ExpectedStatus_is_enforced()
    {
        const string code = """
            using Microsoft.AspNetCore.Builder;
            using Microsoft.AspNetCore.Http;
            using Microsoft.AspNetCore.Routing;

            public static class Api
            {
                public static WebApplication BuildApp()
                {
                    var builder = WebApplication.CreateBuilder();
                    var app = builder.Build();
                    app.MapGet("/missing", () => Results.NotFound());
                    return app;
                }
            }
            """;

        var result = CodeExecutor.Run(
            new RunRequest(code, null,
            [
                new RunTest("not found", "", "contains", ApiPath: "/missing", ExpectedStatus: 404),
                new RunTest("wrong expectation", "", "contains", ApiPath: "/missing", ExpectedStatus: 200)
            ], TimeoutMs: 10_000),
            hardExitOnTimeout: false);

        Assert.True(result.Tests[0].Passed);
        Assert.False(result.Tests[1].Passed);
    }
}
