using ArenaMaster.Game.Combat;
using CEngine.Core;
using Silk.NET.Maths;

namespace ArenaMaster.Game.Mage;

/// <summary>
/// Puts the Mage's fire on screen as engine crowds, all of them glowing: the Fire Barrage's bolts trailing embers, each burst's ring of flame spreading over the
/// ground, flames licking round every burning enemy, the fire on the ground (Fire Walk's line and a meteor's crater), the meteors falling onto the marks where
/// they will land, the Inferno's ring of fire, and embers rising off the Mage as the heat builds. With the Frost Barrage it draws nothing.
/// </summary>
internal sealed class FireView
{
    public const string BoltModel = "pyro_bolt.glb";
    public const string BurstModel = "pyro_burst.glb";
    public const string BurnModel = "pyro_burn.glb";
    public const string FireModel = "pyro_fire.glb";
    public const string MeteorModel = "pyro_meteor.glb";
    public const string MarkModel = "pyro_meteor_mark.glb";
    public const string InfernoModel = "pyro_inferno.glb";
    public const string EmberModel = "pyro_ember.glb";

    private static readonly string[] Models = { BoltModel, BurstModel, BurnModel, FireModel, MeteorModel, MarkModel, InfernoModel, EmberModel };

    /// <summary>How much each model glows of its own.</summary>
    private static readonly (string Model, float Glow)[] Glows =
    {
        (BoltModel, 1.3f), (BurstModel, 1.1f), (BurnModel, 1.0f), (FireModel, 0.9f), (MeteorModel, 0.7f), (MarkModel, 0.8f), (InfernoModel, 1.1f), (EmberModel, 1.5f),
    };

    /// <summary>Embers trailing each bolt, how far apart, and how big.</summary>
    private const int TrailEmbers = 3;
    private const float TrailSpacing = 0.22f;
    private const float EmberSize = 0.05f;

    /// <summary>How far apart the flames stand along a line of fire, and how many stand in a crater of radius 1 m (more for bigger ones).</summary>
    private const float LineSpacing = 0.8f;
    private const int CraterFlames = 5;

    /// <summary>How high a meteor starts, and how far back along its slant.</summary>
    private const float MeteorHeight = 22f;
    private const float MeteorSlant = 9f;

    /// <summary>The most embers rising off the Mage, at full heat.</summary>
    private const int HeatEmbers = 12;

    private readonly Dictionary<string, List<CrowdInstance>> _crowds = Models.ToDictionary(m => m, _ => new List<CrowdInstance>());
    private bool _glowing;
    private float _time;

    public void Sync(EngineWindow window, FrostBarrage barrage, Vector3D<float> feet, bool fire, float heat, float infernoRadius, float deltaSeconds)
    {
        _time += deltaSeconds;
        if (!_glowing)
        {
            foreach (var (model, glow) in Glows)
            {
                window.SetCrowdGlow(model, glow);
            }

            _glowing = true;
        }

        foreach (var list in _crowds.Values)
        {
            list.Clear();
        }

        if (fire)
        {
            var flames = barrage.Flames;
            Bolts(barrage);
            Blasts(flames);
            Burning(flames);
            Ground(flames);
            Meteors(flames);
            Heat(flames, feet, heat, infernoRadius);
        }

        foreach (var (model, list) in _crowds)
        {
            window.SetCrowd(model, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(list));
        }
    }

    public void Clear(EngineWindow window)
    {
        foreach (var model in Models)
        {
            window.SetCrowd(model, ReadOnlySpan<CrowdInstance>.Empty);
        }
    }

    /// <summary>The fire bolts, each with a few embers trailing behind it.</summary>
    private void Bolts(FrostBarrage barrage)
    {
        foreach (var bolt in barrage.Bolts)
        {
            var (yaw, pitch) = Geometry.YawPitch(bolt.Heading);
            float flicker = 1f + 0.12f * MathF.Sin(_time * 31f + bolt.Age * 17f);
            _crowds[BoltModel].Add(new CrowdInstance(bolt.Position, yaw, bolt.Scale * flicker, pitch));
            for (int i = 1; i <= TrailEmbers; i++)
            {
                var at = bolt.Position - bolt.Heading * (TrailSpacing * i * bolt.Scale)
                    + new Vector3D<float>(0.04f * MathF.Sin(_time * 23f + i * 2.1f), 0.05f * i, 0.04f * MathF.Cos(_time * 19f + i * 1.7f));
                _crowds[EmberModel].Add(new CrowdInstance(at, 0f, EmberSize * bolt.Scale * (1f - 0.22f * i)));
            }
        }
    }

    /// <summary>Each burst: a ring of flame spreading out and sinking as it fades.</summary>
    private void Blasts(Flames flames)
    {
        foreach (var blast in flames.Blasts)
        {
            float t = Math.Clamp(blast.Age / Flames.BlastDuration, 0f, 1f);
            float spread = 1f - (1f - t) * (1f - t);
            _crowds[BurstModel].Add(new CrowdInstance(blast.Centre + new Vector3D<float>(0f, 0.05f, 0f), t * 1.3f, blast.Radius * (0.25f + 0.75f * spread),
                Flash: 1f - t));
        }
    }

    /// <summary>Flames licking round every burning enemy, sized to it, dying down in a burn's last half second.</summary>
    private void Burning(Flames flames)
    {
        foreach (var (enemy, burn) in flames.Burns)
        {
            if (!enemy.IsAlive)
            {
                continue;
            }

            float size = enemy.Kind.Height / 1.8f * enemy.Rarity.Size * enemy.Kind.DrawScale;
            float fade = MathF.Min(1f, burn.Left / 0.5f);
            float flicker = 1f + 0.15f * MathF.Sin(_time * 18f + enemy.Id * 1.3f);
            _crowds[BurnModel].Add(new CrowdInstance(enemy.Position, _time * 2.5f + enemy.Id, size * flicker * (0.4f + 0.6f * fade)));
        }
    }

    /// <summary>The fire on the ground: flames in a row along a line of fire, scattered over a crater; smaller as it burns out.</summary>
    private void Ground(Flames flames)
    {
        foreach (var fire in flames.Ground)
        {
            float fade = Math.Clamp(fire.Left / 0.6f, 0f, 1f) * Math.Clamp((fire.Seconds - fire.Left) / 0.15f, 0.3f, 1f);
            if (fire.IsLine)
            {
                Geometry.FlatDirection(fire.From, fire.To, out float length);
                int count = Math.Max(1, (int)MathF.Ceiling(length / LineSpacing) + 1);
                for (int i = 0; i < count; i++)
                {
                    float along = count == 1 ? 0f : i / (float)(count - 1);
                    var at = fire.From + (fire.To - fire.From) * along;
                    float flicker = 1f + 0.18f * MathF.Sin(_time * 14f + i * 2.3f);
                    _crowds[FireModel].Add(new CrowdInstance(at, i * 1.9f, fire.Width * flicker * fade));
                }

                continue;
            }

            // A crater: one flame in the middle, and rings of them out to its edge.
            int rings = Math.Max(1, (int)MathF.Round(fire.Width / 1.6f));
            _crowds[FireModel].Add(new CrowdInstance(fire.From, _time, 1.1f * fade));
            for (int ring = 1; ring <= rings; ring++)
            {
                float radius = fire.Width * ring / (rings + 0.3f);
                int count = CraterFlames * ring;
                for (int i = 0; i < count; i++)
                {
                    float angle = MathF.Tau * i / count + ring * 0.7f;
                    var at = fire.From + new Vector3D<float>(MathF.Sin(angle) * radius, 0f, MathF.Cos(angle) * radius);
                    float flicker = 1f + 0.2f * MathF.Sin(_time * 12f + i * 1.7f + ring);
                    _crowds[FireModel].Add(new CrowdInstance(at, angle, 0.9f * flicker * fade));
                }
            }
        }
    }

    /// <summary>The meteors: each one's mark on the ground, pulsing, and the meteor coming down onto it on a slant.</summary>
    private void Meteors(Flames flames)
    {
        foreach (var meteor in flames.Meteors)
        {
            float t = 1f - Math.Clamp(meteor.FallLeft / MageStats.MeteorFall, 0f, 1f);
            _crowds[MarkModel].Add(new CrowdInstance(meteor.Target + new Vector3D<float>(0f, 0.04f, 0f), _time * 0.8f, meteor.Radius,
                Flash: 0.5f + 0.5f * MathF.Sin(_time * 14f)));
            var from = meteor.Target + new Vector3D<float>(-MeteorSlant, MeteorHeight, -MeteorSlant * 0.5f);
            var at = from + (meteor.Target - from) * (t * t);
            var (yaw, pitch) = Geometry.YawPitch(Vector3D.Normalize(meteor.Target - from));
            _crowds[MeteorModel].Add(new CrowdInstance(at, yaw, 1f + 0.08f * MathF.Sin(_time * 25f), pitch));
        }
    }

    /// <summary>Embers rising off the Mage, more the hotter it is; and while the Inferno rages, its ring of fire turning round the Mage.</summary>
    private void Heat(Flames flames, Vector3D<float> feet, float heat, float infernoRadius)
    {
        int embers = (int)MathF.Round(HeatEmbers * Math.Clamp(heat / MageStats.MaxHeat, 0f, 1f));
        for (int i = 0; i < embers; i++)
        {
            float rise = (_time * 0.7f + i * 0.37f) % 1f;
            float angle = i * 2.4f + _time * 0.9f;
            var at = feet + new Vector3D<float>(MathF.Sin(angle) * 0.45f, 0.4f + 1.8f * rise, MathF.Cos(angle) * 0.45f);
            _crowds[EmberModel].Add(new CrowdInstance(at, 0f, EmberSize * 1.2f * (1f - rise)));
        }

        if (flames.InInferno)
        {
            float swell = Math.Clamp((MageStats.OverheatSeconds - flames.OverheatLeft) / 0.2f, 0f, 1f);
            _crowds[InfernoModel].Add(new CrowdInstance(feet + new Vector3D<float>(0f, 0.05f, 0f), _time * 1.6f, infernoRadius * swell,
                Flash: 0.3f + 0.2f * MathF.Sin(_time * 20f)));
            _crowds[InfernoModel].Add(new CrowdInstance(feet + new Vector3D<float>(0f, 0.05f, 0f), -_time * 2.3f, infernoRadius * 0.6f * swell));
        }
    }
}
