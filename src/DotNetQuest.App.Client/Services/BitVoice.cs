using Microsoft.JSInterop;

namespace DotNetQuest.App.Client.Services;

/// <summary>
/// Bit's voice: browser speech synthesis via a JS module. Free, offline, and
/// bilingual (en-US / es-ES voices are picked automatically).
/// </summary>
public sealed class BitVoice(IJSRuntime js) : IAsyncDisposable
{
    private Lazy<Task<IJSObjectReference>>? _module;

    public event Action? SpeakingChanged;

    public bool Speaking { get; private set; }
    public bool Enabled { get; set; } = true;

    private Lazy<Task<IJSObjectReference>> Module => _module ??= new(async () =>
        await js.InvokeAsync<IJSObjectReference>("import", "./js/bit-voice.js"));

    public async Task<bool> IsSupportedAsync()
    {
        try
        {
            var module = await Module.Value;
            return await module.InvokeAsync<bool>("isSupported");
        }
        catch (JSException)
        {
            return false;
        }
    }

    public async Task SpeakAsync(string text, string language)
    {
        if (!Enabled || string.IsNullOrWhiteSpace(text)) return;
        try
        {
            var module = await Module.Value;
            var dotnetRef = DotNetObjectReference.Create(new Callback(this));
            await module.InvokeVoidAsync("speak", text, language == "es" ? "es-ES" : "en-US", dotnetRef);
        }
        catch (JSException)
        {
            // Voice is a nicety; silence on failure.
        }
    }

    public async Task StopAsync()
    {
        try
        {
            var module = await Module.Value;
            await module.InvokeVoidAsync("stop");
        }
        catch (JSException) { }
    }

    [JSInvokable]
    public void OnSpeakingStateChanged(bool speaking)
    {
        Speaking = speaking;
        SpeakingChanged?.Invoke();
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is { IsValueCreated: true })
        {
            try
            {
                var module = await _module.Value;
                await module.DisposeAsync();
            }
            catch (JSException) { }
        }
    }

    private sealed class Callback(BitVoice owner)
    {
        [JSInvokable]
        public void SetSpeaking(bool speaking) => owner.OnSpeakingStateChanged(speaking);
    }
}

/// <summary>Canvas confetti bursts for celebrations.</summary>
public sealed class Confetti(IJSRuntime js) : IAsyncDisposable
{
    private Lazy<Task<IJSObjectReference>>? _module;

    private Lazy<Task<IJSObjectReference>> Module => _module ??= new(async () =>
        await js.InvokeAsync<IJSObjectReference>("import", "./js/confetti.js"));

    public async Task BurstAsync()
    {
        try
        {
            var module = await Module.Value;
            await module.InvokeVoidAsync("burst");
        }
        catch (JSException) { }
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is { IsValueCreated: true })
        {
            try
            {
                var module = await _module.Value;
                await module.DisposeAsync();
            }
            catch (JSException) { }
        }
    }
}
