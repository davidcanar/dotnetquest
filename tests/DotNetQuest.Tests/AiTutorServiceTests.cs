using System.Net;
using System.Text;
using DotNetQuest.App.Services;
using DotNetQuest.Shared.Api;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace DotNetQuest.Tests;

/// <summary>
/// AiTutorService against a stubbed provider: proves streaming deltas parse,
/// reasoning frames are skipped, thinking-disabled is requested, and failures
/// carry a reason.
/// </summary>
public class AiTutorServiceTests
{
    private readonly List<string> _requestBodies = [];

    private AiTutorService Create(string sseResponse, bool disableThinking)
    {
        _requestBodies.Clear();
        var http = new HttpClient(new StubHandler(
            _ => sseResponse,
            (_, body) => _requestBodies.Add(body)));
        var options = new AiTutorOptions
        {
            BaseUrl = "https://provider.test",
            ApiKey = "test-key",
            Model = "deepseek-flash",
            DisableThinking = disableThinking
        };
        return new AiTutorService(http, new StubMonitor(options), NullLogger<AiTutorService>.Instance);
    }

    private static string Sse(params string[] deltas)
    {
        var sb = new StringBuilder();
        foreach (var d in deltas)
            sb.Append("data: ").Append("{\"choices\":[{\"delta\":").Append(d).Append("}]}").Append("\n\n");
        sb.Append("data: [DONE]\n\n");
        return sb.ToString();
    }

    private static async Task<string> Collect(AiTutorService service)
    {
        var sb = new StringBuilder();
        await foreach (var chunk in service.ChatStreamAsync(Req(), CancellationToken.None))
            sb.Append(chunk);
        return sb.ToString();
    }

    [Fact]
    public async Task Stream_parses_delta_frames_and_skips_reasoning_frames()
    {
        var service = Create(
            Sse(
                "{\"reasoning_content\":\"thinking...\"}",
                "{\"content\":\"Hel\"}",
                "{\"content\":\"lo\"}"),
            disableThinking: false);

        var text = await Collect(service);

        Assert.Equal("Hello", text);
        Assert.Null(service.LastError);
    }

    [Fact]
    public async Task Thinking_disabled_is_requested_when_configured()
    {
        _ = await Collect(Create(Sse("{\"content\":\"x\"}"), disableThinking: true));

        Assert.Contains(_requestBodies, b => b.Contains("thinking"));
    }

    [Fact]
    public async Task Thinking_not_requested_when_option_off()
    {
        _ = await Collect(Create(Sse("{\"content\":\"x\"}"), disableThinking: false));

        Assert.DoesNotContain(_requestBodies, b => b.Contains("thinking"));
    }

    [Fact]
    public async Task Invalid_json_from_provider_surfaces_reason()
    {
        var http = new HttpClient(new StubHandler(_ => "boom", null));
        var service = new AiTutorService(http, new StubMonitor(new AiTutorOptions
        {
            BaseUrl = "https://provider.test",
            ApiKey = "k",
            Model = "deepseek-flash"
        }), NullLogger<AiTutorService>.Instance);

        var response = await service.ChatAsync(Req(), CancellationToken.None);

        Assert.True(response.Fallback);
        Assert.Contains("unparseable", response.Reason);
    }

    [Fact]
    public async Task System_prompt_sent_to_provider_demands_staying_on_topic()
    {
        _ = await Collect(Create(Sse("{\"content\":\"ok\"}"), disableThinking: false));

        var body = Assert.Single(_requestBodies);
        Assert.Contains("Stay on topic", body);
        Assert.Contains("decline playfully", body);
    }

    [Fact]
    public async Task Oversized_history_is_clamped_before_reaching_the_provider()
    {
        var messages = new List<TutorChatMessage>();
        for (var i = 0; i < 30; i++)
            messages.Add(new TutorChatMessage("user", new string('x', 6000)));

        var service = Create(Sse("{\"content\":\"ok\"}"), disableThinking: false);
        var sb = new StringBuilder();
        await foreach (var chunk in service.ChatStreamAsync(new TutorChatRequest("en", null, messages), CancellationToken.None))
            sb.Append(chunk);

        var body = Assert.Single(_requestBodies);
        // Each message is capped below 6000 chars, so no full-size payload survives.
        Assert.DoesNotContain(new string('x', 6000), body);
        // 12 kept turns × 4000 chars + system prompt + JSON framing.
        Assert.True(body.Length < 60_000, $"provider payload too large: {body.Length}");
    }

    private static TutorChatRequest Req() => new(
        "en",
        null,
        [new TutorChatMessage("user", "hi")]);

    private sealed class StubHandler(Func<HttpRequestMessage, string> responder,
        Action<HttpRequestMessage, string>? capture) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct);
            capture?.Invoke(request, body);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(responder(request)) };
        }
    }

    private sealed class StubMonitor(AiTutorOptions options) : IOptionsMonitor<AiTutorOptions>
    {
        public AiTutorOptions CurrentValue => options;
        public AiTutorOptions Get(string? name) => options;
        public IDisposable? OnChange(Action<AiTutorOptions> listener) => null;
        public IDisposable? OnChange(Action<AiTutorOptions, string?> listener) => null;
    }
}
