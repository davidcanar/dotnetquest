using System.Net.Http.Json;
using Microsoft.JSInterop;

namespace DotNetQuest.App.Client.Services;

/// <summary>
/// Lightweight EN/ES localization. UI strings come from wwwroot/i18n/{lang}.json
/// (flat "dot.separated.key" style); lesson content carries its own en/es pairs.
/// </summary>
public sealed class I18nService(HttpClient http, IJSRuntime js)
{
    public const string DefaultLanguage = "en";

    private Dictionary<string, string>? _current;
    private Dictionary<string, string>? _english;

    public string Language { get; private set; } = DefaultLanguage;

    public event Action? LanguageChanged;

    /// <summary>Load dictionaries for the stored/browser language. Safe to call repeatedly.</summary>
    public async Task EnsureLoadedAsync()
    {
        var stored = await GetStoredLanguageAsync();
        await LoadAsync(stored ?? Language);
    }

    public async Task SetLanguageAsync(string language, bool persist = true)
    {
        var target = Normalize(language) ?? DefaultLanguage;
        if (persist)
        {
            try { await js.InvokeVoidAsync("localStorage.setItem", "dq.lang", target); } catch (JSException) { }
        }
        if (target != Language || _current is null)
            await LoadAsync(target);
    }

    /// <summary>Translate a UI key; falls back to English, then the key itself.</summary>
    public string T(string key)
    {
        if (_current is not null && _current.TryGetValue(key, out var value)) return value;
        if (_english is not null && _english.TryGetValue(key, out var en)) return en;
        return key;
    }

    /// <summary>Translate with {0},{1}... format arguments.</summary>
    public string T(string key, params object[] args) => string.Format(T(key), args);

    /// <summary>Pick the right variant of a localized content string.</summary>
    public string Pick(string? en, string? es)
    {
        var chosen = Language == "es"
            ? (string.IsNullOrWhiteSpace(es) ? en : es)
            : (string.IsNullOrWhiteSpace(en) ? es : en);
        return chosen ?? string.Empty;
    }

    private async Task<string?> GetStoredLanguageAsync()
    {
        try
        {
            return await js.InvokeAsync<string?>("localStorage.getItem", "dq.lang");
        }
        catch (JSException)
        {
            return null;
        }
    }

    private async Task LoadAsync(string target)
    {
        _english ??= await FetchDictionaryAsync("en");
        _current = target == "en" ? _english : await FetchDictionaryAsync(target);
        if (Language != target)
        {
            Language = target;
            LanguageChanged?.Invoke();
        }
    }

    private async Task<Dictionary<string, string>> FetchDictionaryAsync(string lang)
    {
        try
        {
            return await http.GetFromJsonAsync<Dictionary<string, string>>($"i18n/{lang}.json") ?? [];
        }
        catch (Exception)
        {
            return [];
        }
    }

    private static string Normalize(string? language)
    {
        if (string.IsNullOrWhiteSpace(language)) return DefaultLanguage;
        return language.StartsWith("es", StringComparison.OrdinalIgnoreCase) ? "es" : "en";
    }
}
