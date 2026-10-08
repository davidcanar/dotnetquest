namespace DotNetQuest.App.Client.Components;

/// <summary>
/// Bit's looks. Every avatar is the same robot with a different body color
/// and an optional accessory — so the mascot and the avatars share one SVG.
/// </summary>
public static class AvatarRegistry
{
    public record AvatarDef(string Id, string BodyColor, string BodyShade, string FaceColor, string Accessory);

    public static readonly AvatarDef[] All =
    {
        new("bit-classic", "#2dd4bf", "#14b8a6", "#0f3d3a", "none"),
        new("bit-sunny",   "#fbbf24", "#f59e0b", "#4a3305", "none"),
        new("bit-cherry",  "#fb7185", "#f43f5e", "#4c0d18", "none"),
        new("bit-grape",   "#a78bfa", "#8b5cf6", "#291b52", "none"),
        new("bit-forest",  "#4ade80", "#22c55e", "#053b21", "none"),
        new("bit-scholar", "#2dd4bf", "#14b8a6", "#0f3d3a", "glasses"),
        new("bit-dj",      "#a78bfa", "#8b5cf6", "#291b52", "headset"),
        new("bit-captain", "#fbbf24", "#f59e0b", "#4a3305", "star")
    };

    public static AvatarDef Get(string? id) =>
        All.FirstOrDefault(a => a.Id == id) ?? All[0];
}

/// <summary>Bit's poses / moods.</summary>
public static class BitPoses
{
    public const string Happy = "happy";
    public const string Wave = "wave";
    public const string Think = "think";
    public const string Cheer = "cheer";
    public const string Sad = "sad";
    public const string Sleep = "sleep";
    public const string Talk = "talk";
}
