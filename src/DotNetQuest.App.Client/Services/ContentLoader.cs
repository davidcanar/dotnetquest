using System.Net.Http.Json;
using DotNetQuest.Shared.Content;

namespace DotNetQuest.App.Client.Services;

/// <summary>Fetches and caches the world map index and world content packs.</summary>
public class ContentLoader(HttpClient http)
{
    private List<WorldIndexItem>? _index;
    private readonly Dictionary<string, WorldContent> _worlds = [];

    public async Task<List<WorldIndexItem>> GetWorldIndexAsync()
    {
        if (_index is not null) return _index;
        try
        {
            _index = await http.GetFromJsonAsync<List<WorldIndexItem>>("content/worlds/index.json",
                new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)) ?? [];
        }
        catch (Exception)
        {
            _index = [];
        }
        return _index;
    }

    public async Task<WorldContent?> GetWorldAsync(string worldId)
    {
        if (_worlds.TryGetValue(worldId, out var cached)) return cached;
        try
        {
            var world = await http.GetFromJsonAsync<WorldContent>($"content/worlds/{worldId}.json",
                new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
            if (world is not null) _worlds[worldId] = world;
            return world;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
