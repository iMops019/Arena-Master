using CEngine.Core;
using Silk.NET.Maths;

namespace ArenaMaster.Game.Paladin;

/// <summary>
/// Puts <see cref="HolyLight"/> on screen as two engine crowds: each nova's gold ring spreading out over the ground, and the holy circles - a ring with a sigil in
/// it, turning slowly, swelling in as it appears and shrinking away as it fades.
/// </summary>
internal sealed class HolyView
{
    public const string NovaModel = "holy_nova.glb";
    public const string CircleModel = "holy_circle.glb";

    /// <summary>How long a circle takes to swell in, and to shrink away at the end.</summary>
    private const float CircleGrowIn = 0.15f;
    private const float CircleFadeOut = 0.4f;

    private readonly List<CrowdInstance> _novas = new();
    private readonly List<CrowdInstance> _circles = new();

    public void Sync(EngineWindow window, HolyLight light)
    {
        _novas.Clear();
        foreach (var flash in light.Flashes)
        {
            float t = Math.Clamp(flash.Age / HolyLight.FlashDuration, 0f, 1f);
            float spread = 1f - (1f - t) * (1f - t);   // fast out, easing to a stop at the full radius
            _novas.Add(new CrowdInstance(flash.Centre + new Vector3D<float>(0f, 0.2f + 0.3f * t, 0f), 0f, flash.Radius * (0.25f + 0.75f * spread), Flash: 1f - t));
        }

        _circles.Clear();
        foreach (var circle in light.Circles)
        {
            float grow = Math.Clamp(circle.Age / CircleGrowIn, 0f, 1f);
            float fade = Math.Clamp((circle.Lifetime - circle.Age) / CircleFadeOut, 0f, 1f);
            float size = circle.Radius * (0.6f + 0.4f * grow) * fade;
            _circles.Add(new CrowdInstance(circle.Centre + new Vector3D<float>(0f, 0.06f, 0f), circle.Age * 0.4f, MathF.Max(0.01f, size)));
        }

        window.SetCrowd(NovaModel, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_novas));
        window.SetCrowd(CircleModel, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_circles));
    }

    public void Clear(EngineWindow window)
    {
        window.SetCrowd(NovaModel, ReadOnlySpan<CrowdInstance>.Empty);
        window.SetCrowd(CircleModel, ReadOnlySpan<CrowdInstance>.Empty);
    }
}
