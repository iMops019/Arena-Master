namespace ArenaMaster.Game.Delve;

/// <summary>
/// How a stretch of floors looks: the same map every time, lit and weathered differently the deeper it goes - a time of day, low mist pooling on the ground, fog,
/// cloud, rain or snow, and wisps in the dark. The content sets these on the engine for a run and puts camp's own back afterwards.
/// </summary>
/// <param name="TimeOfDay">The engine's hours: noon at 6, the sun low at 11 and gone from 12 to 24.</param>
/// <param name="Mist">Ground mist, 0 to 1.</param>
/// <param name="MistColour">The mist's colour (red, green, blue, 0 to 1).</param>
/// <param name="Wisps">Glowing wisps, 0 to 1.</param>
internal sealed record DelveBand(
    string Name,
    int FromDepth,
    float TimeOfDay,
    float Mist,
    (float R, float G, float B) MistColour,
    float Fog,
    float Cloud,
    float Rain,
    float Snow,
    float Warmth,
    float Wisps);

/// <summary>The bands of floors, shallow to deep. Pure.</summary>
internal static class DelveBands
{
    public static readonly IReadOnlyList<DelveBand> All = new DelveBand[]
    {
        new("The Greenwood", 1, TimeOfDay: 5f, Mist: 0f, MistColour: (0.82f, 0.85f, 0.88f), Fog: 0f, Cloud: 0.1f, Rain: 0f, Snow: 0f, Warmth: 0.3f, Wisps: 0f),
        new("The Amber Hollows", 5, TimeOfDay: 11.2f, Mist: 0.55f, MistColour: (0.95f, 0.8f, 0.6f), Fog: 0.1f, Cloud: 0.2f, Rain: 0f, Snow: 0f, Warmth: 0.4f, Wisps: 0.2f),
        new("The Mistdeep", 10, TimeOfDay: 8f, Mist: 0.9f, MistColour: (0.72f, 0.78f, 0.82f), Fog: 0.55f, Cloud: 0.85f, Rain: 0.35f, Snow: 0f, Warmth: 0.2f, Wisps: 0.3f),
        new("The Night Hollows", 15, TimeOfDay: 17f, Mist: 0.45f, MistColour: (0.55f, 0.62f, 0.9f), Fog: 0.15f, Cloud: 0.2f, Rain: 0f, Snow: 0f, Warmth: 0f, Wisps: 1f),
        new("The Frozen Deep", 20, TimeOfDay: 9.5f, Mist: 0.5f, MistColour: (0.85f, 0.9f, 1f), Fog: 0.35f, Cloud: 0.75f, Rain: 0f, Snow: 0.75f, Warmth: -1f, Wisps: 0.4f),
    };

    /// <summary>The band floor <paramref name="depth"/> belongs to: the deepest one it has reached.</summary>
    public static DelveBand For(int depth) => All.Last(b => b.FromDepth <= Math.Max(1, depth));
}
