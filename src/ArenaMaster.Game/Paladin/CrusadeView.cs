using CEngine.Core;
using Silk.NET.Maths;

namespace ArenaMaster.Game.Paladin;

/// <summary>
/// Puts <see cref="CrusadeLight"/> on screen as engine crowds, all glowing: each hammer of light falling from the sky (spinning as it drops) with a ring on the
/// ground where it will land, the ring of light spreading from where one landed, the golden wings behind the Paladin while Avenging Wings last (two frames, in turn,
/// as they beat), and a ring of gold round the Paladin's feet that turns and brightens as Zeal builds.
/// </summary>
internal sealed class CrusadeView
{
    public const string HammerModel = "crusade_hammer.glb";
    public const string MarkModel = "crusade_hammer_mark.glb";
    public const string ImpactModel = "crusade_impact.glb";
    public const string WingsModel = "crusade_wings.glb";
    public const string WingsRaisedModel = "crusade_wings_raised.glb";
    public const string ZealModel = "crusade_zeal.glb";

    /// <summary>How high a hammer starts its fall.</summary>
    private const float DropHeight = 14f;

    /// <summary>How long each of the wings' two frames shows as they beat.</summary>
    private const float WingBeat = 0.22f;

    /// <summary>Where the wings sit: up from the feet, and back from the body's middle.</summary>
    private const float WingsHeight = 1.2f;
    private const float WingsBack = 0.28f;

    private readonly List<CrowdInstance> _hammers = new();
    private readonly List<CrowdInstance> _marks = new();
    private readonly List<CrowdInstance> _impacts = new();
    private readonly List<CrowdInstance> _wings = new();
    private readonly List<CrowdInstance> _raised = new();
    private readonly List<CrowdInstance> _zeal = new();
    private float _clock;
    private bool _glowing;

    /// <summary>Draws this frame's hammers, rings, wings and Zeal. <paramref name="feet"/> is the Paladin's feet and <paramref name="yaw"/> the way its body faces.</summary>
    public void Sync(EngineWindow window, CrusadeLight crusade, Vector3D<float> feet, float yaw, float deltaSeconds)
    {
        if (!_glowing)
        {
            _glowing = true;
            window.SetCrowdGlow(HammerModel, 1.3f);
            window.SetCrowdGlow(MarkModel, 0.8f);
            window.SetCrowdGlow(ImpactModel, 1.3f);
            window.SetCrowdGlow(WingsModel, 0.9f);
            window.SetCrowdGlow(WingsRaisedModel, 0.9f);
            window.SetCrowdGlow(ZealModel, 1f);
        }

        _clock += deltaSeconds;

        _hammers.Clear();
        _marks.Clear();
        foreach (var hammer in crusade.Hammers)
        {
            float t = Math.Clamp(hammer.Age / CrusadeLight.FallTime, 0f, 1f);
            float height = DropHeight * (1f - t * t);   // falling faster and faster
            _hammers.Add(new CrowdInstance(hammer.Position + new Vector3D<float>(0f, height, 0f), hammer.Age * 7f, 1f, Flash: t));
            _marks.Add(new CrowdInstance(hammer.Position + new Vector3D<float>(0f, 0.07f, 0f), -hammer.Age * 2f, hammer.Radius * (1.3f - 0.3f * t), Flash: t));
        }

        _impacts.Clear();
        foreach (var impact in crusade.Impacts)
        {
            float t = Math.Clamp(impact.Age / CrusadeLight.ImpactDuration, 0f, 1f);
            float spread = 1f - (1f - t) * (1f - t);
            _impacts.Add(new CrowdInstance(impact.Centre + new Vector3D<float>(0f, 0.15f + 0.3f * t, 0f), 0f, impact.Radius * (0.3f + 0.9f * spread), Flash: 1f - t));
        }

        _wings.Clear();
        _raised.Clear();
        if (crusade.Winged)
        {
            var back = new Vector3D<float>(MathF.Sin(yaw), 0f, MathF.Cos(yaw)) * -WingsBack;
            float blink = crusade.WingsLeft < 1f ? 0.5f + 0.5f * MathF.Sin(crusade.WingsLeft * 30f) : 0.4f;   // flickering as they fade
            var wing = new CrowdInstance(feet + back + new Vector3D<float>(0f, WingsHeight + 0.06f * MathF.Sin(_clock * 9f), 0f), yaw, 1f, Flash: blink);
            ((int)(_clock / WingBeat) % 2 == 0 ? _wings : _raised).Add(wing);
        }

        _zeal.Clear();
        float fill = crusade.Zeal / PaladinStats.MaxZeal;
        if (fill > 0.02f)
        {
            _zeal.Add(new CrowdInstance(feet + new Vector3D<float>(0f, 0.08f, 0f), _clock * (0.8f + 2f * fill), 0.7f + 0.5f * fill, Flash: fill));
        }

        window.SetCrowd(HammerModel, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_hammers));
        window.SetCrowd(MarkModel, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_marks));
        window.SetCrowd(ImpactModel, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_impacts));
        window.SetCrowd(WingsModel, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_wings));
        window.SetCrowd(WingsRaisedModel, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_raised));
        window.SetCrowd(ZealModel, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_zeal));
    }

    public void Clear(EngineWindow window)
    {
        foreach (var model in new[] { HammerModel, MarkModel, ImpactModel, WingsModel, WingsRaisedModel, ZealModel })
        {
            window.SetCrowd(model, ReadOnlySpan<CrowdInstance>.Empty);
        }
    }
}
