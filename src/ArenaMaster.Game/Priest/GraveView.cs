using ArenaMaster.Game.Combat;
using CEngine.Core;
using Silk.NET.Maths;

namespace ArenaMaster.Game.Priest;

/// <summary>
/// Puts the Grave Calling tree on screen as engine crowds: the raised dead as an animated crowd of skeletons (climbing out of the ground, walking, clawing,
/// falling apart; the Bone Colossus the same skeleton three times the size), the souls lying on the ground as glowing orbs bobbing over the spot, the spirits
/// streaking at their enemies, the bone spears in flight, a cage of bones over every enemy a block has caged, a ghostly aura round the Priest in Lich Form, and a
/// ring of bone shards for each Corpse Burst and of churned earth for each rising. Pure show: nothing here changes the fight.
/// </summary>
internal sealed class GraveView
{
    public const string ServantModel = "grave_servant.glb";
    public const string SoulModel = "grave_soul.glb";
    public const string SpearModel = "bone_spear.glb";
    public const string CageModel = "bone_cage.glb";
    public const string LichModel = "lich_aura.glb";
    public const string BurstModel = "corpse_burst.glb";
    public const string RiseModel = "grave_rise.glb";

    public const string IdleClip = "Idle";
    public const string WalkClip = "Walk";
    public const string ClawClip = "Claw";
    public const string DieClip = "Die";

    /// <summary>The clips' lengths, as tools/gravecalling_models.py makes them (used until the engine says otherwise): the walk's stride is as many metres.</summary>
    private const float IdleSeconds = 3f;
    private const float WalkSeconds = 1.25f;

    /// <summary>How long a servant takes to climb out of the ground, and how deep it starts; how long a change of clip fades.</summary>
    private const float ClimbSeconds = 0.5f;
    private const float ClimbDepth = 1.3f;
    private const float FadeSeconds = 0.15f;

    /// <summary>How big a soul is drawn, how high over the ground it floats, and how long it shrinks away at the end of its time.</summary>
    private const float SoulSize = 0.28f;
    private const float SoulHover = 0.7f;
    private const float SoulFadeOut = 1f;

    /// <summary>How long a cage takes to close round its enemy, and how tall the model's cage is (a ghoul's height).</summary>
    private const float CageClose = 0.15f;
    private const float CageHeight = 1.45f;

    /// <summary>How long the flash of a gathered soul lasts.</summary>
    private const float GatherFlash = 0.3f;

    private sealed class Motion
    {
        public Vector3D<float> Last;
        public float Walk;
        public float Idle;
        public string Clip = IdleClip;
        public float Time;
        public string? From;
        public float FromTime;
        public float Fade;
    }

    private readonly Dictionary<Servant, Motion> _motions = new();
    private readonly Dictionary<SoulSpirit, List<Vector3D<float>>> _trails = new();
    private readonly Dictionary<Enemy, float> _cagedFor = new();
    private readonly List<(Vector3D<float> At, float Age)> _flashes = new();
    private readonly List<SkinnedCrowdInstance> _servants = new();
    private readonly List<CrowdInstance> _souls = new();
    private readonly List<CrowdInstance> _spears = new();
    private readonly List<CrowdInstance> _cages = new();
    private readonly List<CrowdInstance> _lich = new();
    private readonly List<CrowdInstance> _bursts = new();
    private readonly List<CrowdInstance> _risings = new();
    private float _time;

    /// <summary>Draws this frame. <paramref name="feet"/> is where the Priest stands (the Lich's aura is round it).</summary>
    public void Sync(EngineWindow window, RaisedDead dead, GatheredSouls souls, PlagueSkulls skulls, IReadOnlySet<Enemy> caged, float deltaSeconds,
        Func<float, float, float?> groundAt, Vector3D<float> feet)
    {
        _time += deltaSeconds;
        var clips = window.SkinnedCrowdClips(ServantModel);
        DrawServants(dead, deltaSeconds, clips);
        DrawSouls(souls, deltaSeconds, feet);
        DrawSpears(skulls);
        DrawCages(caged, deltaSeconds);
        DrawRings(dead, groundAt);

        _lich.Clear();
        if (souls.IsLich)
        {
            float pulse = 1f + 0.08f * MathF.Sin(_time * 6f);
            _lich.Add(new CrowdInstance(feet + new Vector3D<float>(0f, 0.05f, 0f), _time * 1.5f, 1.2f * pulse, Flash: 0.4f));
            _lich.Add(new CrowdInstance(feet + new Vector3D<float>(0f, 0.6f, 0f), -_time * 2.1f, 0.9f / pulse, Flash: 0.3f));
        }

        window.SetSkinnedCrowd(ServantModel, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_servants));
        window.SetCrowd(SoulModel, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_souls));
        window.SetCrowd(SpearModel, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_spears));
        window.SetCrowd(CageModel, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_cages));
        window.SetCrowd(LichModel, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_lich));
        window.SetCrowd(BurstModel, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_bursts));
        window.SetCrowd(RiseModel, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_risings));
        window.SetCrowdGlow(SoulModel, 0.9f);
        window.SetCrowdGlow(LichModel, 0.7f);
    }

    public void Clear(EngineWindow window)
    {
        _motions.Clear();
        _trails.Clear();
        _cagedFor.Clear();
        _flashes.Clear();
        window.SetSkinnedCrowd(ServantModel, ReadOnlySpan<SkinnedCrowdInstance>.Empty);
        foreach (var model in new[] { SoulModel, SpearModel, CageModel, LichModel, BurstModel, RiseModel })
        {
            window.SetCrowd(model, ReadOnlySpan<CrowdInstance>.Empty);
        }
    }

    /// <summary>
    /// Every servant at its clip: climbing out of the ground as it rises, walking (the stride moved on by the ground it covers), clawing in time with its blows
    /// (the clip's middle is the moment one lands), standing, or falling apart and sinking.
    /// </summary>
    private void DrawServants(RaisedDead dead, float deltaSeconds, IReadOnlyDictionary<string, float>? clips)
    {
        _servants.Clear();
        float idleLength = clips?.GetValueOrDefault(IdleClip) is > 0f and var idle ? idle : IdleSeconds;
        float walkLength = clips?.GetValueOrDefault(WalkClip) is > 0f and var walk ? walk : WalkSeconds;
        foreach (var servant in dead.Servants)
        {
            if (!_motions.TryGetValue(servant, out var motion))
            {
                motion = new Motion { Last = servant.Position, Idle = servant.Slot };
                _motions[servant] = motion;
            }

            float size = servant.IsColossus ? PriestStats.ColossusSize : 1f;
            var flat = new Vector2D<float>(servant.Position.X - motion.Last.X, servant.Position.Z - motion.Last.Z);
            float moved = flat.Length < 3f * size ? flat.Length : 0f;
            motion.Last = servant.Position;
            motion.Idle = (motion.Idle + deltaSeconds) % idleLength;

            string clip;
            float time;
            var position = servant.Position;
            float flash = servant.IsColossus ? 0.12f : 0.06f;
            if (servant.DeadFor is { } deadFor)
            {
                clip = DieClip;
                time = deadFor;
                position.Y -= MathF.Max(0f, deadFor - 0.5f) * 2f * 0.6f * size;
                flash = 0f;
            }
            else if (servant.Clawing)
            {
                clip = ClawClip;
                time = ((1.5f - servant.ClawIn / MathF.Max(0.05f, servant.ClawInterval)) % 1f + 1f) % 1f;   // the blow lands at the clip's middle
            }
            else if (moved > 0.2f * deltaSeconds * PriestStats.ServantSpeed)
            {
                motion.Walk = (motion.Walk + moved / size) % walkLength;
                clip = WalkClip;
                time = motion.Walk;
            }
            else
            {
                clip = IdleClip;
                time = motion.Idle;
            }

            if (servant.Age < ClimbSeconds)
            {
                position.Y -= (1f - servant.Age / ClimbSeconds) * ClimbDepth * size;   // climbing out of its grave
            }

            if (clip != motion.Clip)
            {
                motion.From = motion.Clip;
                motion.FromTime = motion.Time;
                motion.Fade = 1f;
                motion.Clip = clip;
            }

            motion.Time = time;
            motion.Fade = MathF.Max(0f, motion.Fade - deltaSeconds / FadeSeconds);
            _servants.Add(new SkinnedCrowdInstance(position, servant.Yaw, clip, time, size, 0f, flash,
                motion.Fade > 0f ? motion.From : null, motion.FromTime, motion.Fade));
        }

        foreach (var gone in _motions.Keys.Where(s => !dead.Servants.Contains(s)).ToList())
        {
            _motions.Remove(gone);
        }
    }

    /// <summary>The souls lying on the ground, bobbing and pulsing, shrinking away at the end; the spirits streaking with a tail; a flash where each was gathered.</summary>
    private void DrawSouls(GatheredSouls souls, float deltaSeconds, Vector3D<float> feet)
    {
        _souls.Clear();
        foreach (var soul in souls.Souls)
        {
            float bob = 0.1f * MathF.Sin(_time * 3f + soul.Position.X * 1.7f + soul.Position.Z);
            float grow = MathF.Min(1f, soul.Age / 0.25f) * MathF.Min(1f, (soul.Life - soul.Age) / SoulFadeOut);
            float pulse = 1f + 0.12f * MathF.Sin(_time * 7f + soul.Position.Z);
            _souls.Add(new CrowdInstance(soul.Position + new Vector3D<float>(0f, SoulHover + bob, 0f), _time * 2f + soul.Position.X, SoulSize * grow * pulse,
                Flash: 0.3f));
        }

        foreach (var spirit in souls.Spirits)
        {
            if (!_trails.TryGetValue(spirit, out var trail))
            {
                trail = new List<Vector3D<float>>();
                _trails[spirit] = trail;
            }

            trail.Insert(0, spirit.Position);
            if (trail.Count > 6)
            {
                trail.RemoveAt(trail.Count - 1);
            }

            for (int i = 0; i < trail.Count; i++)
            {
                _souls.Add(new CrowdInstance(trail[i], _time * 5f + i, SoulSize * (1.1f - 0.15f * i), Flash: 0.5f - 0.07f * i));
            }
        }

        foreach (var gone in _trails.Keys.Where(s => !souls.Spirits.Contains(s)).ToList())
        {
            _trails.Remove(gone);
        }

        foreach (var at in souls.Gathered)
        {
            _flashes.Add((at, 0f));
        }

        for (int i = _flashes.Count - 1; i >= 0; i--)
        {
            var (at, age) = _flashes[i];
            age += deltaSeconds;
            if (age >= GatherFlash)
            {
                _flashes.RemoveAt(i);
                continue;
            }

            _flashes[i] = (at, age);
            float t = age / GatherFlash;
            var rising = Vector3D.Lerp(at + new Vector3D<float>(0f, SoulHover, 0f), feet + new Vector3D<float>(0f, 1.1f, 0f), t);   // drawn into the Priest
            _souls.Add(new CrowdInstance(rising, 0f, SoulSize * (1f + 1.5f * t) * (1f - t), Flash: 0.8f));
        }
    }

    /// <summary>The bone spears, each along its way.</summary>
    private void DrawSpears(PlagueSkulls skulls)
    {
        _spears.Clear();
        foreach (var spear in skulls.Spears)
        {
            float yaw = MathF.Atan2(spear.Heading.X, spear.Heading.Z);
            _spears.Add(new CrowdInstance(spear.Position, yaw, 1f, 0f, Flash: 0.15f));
        }
    }

    /// <summary>A cage of bones over every caged enemy, closing up out of the ground round it, sized to it.</summary>
    private void DrawCages(IReadOnlySet<Enemy> caged, float deltaSeconds)
    {
        _cages.Clear();
        foreach (var enemy in caged)
        {
            float held = _cagedFor.GetValueOrDefault(enemy) + deltaSeconds;
            _cagedFor[enemy] = held;
            float size = MathF.Max(enemy.Kind.Height / CageHeight, enemy.Kind.Radius / 0.45f) * enemy.Kind.DrawScale * enemy.Rarity.Size;
            float close = MathF.Min(1f, held / CageClose);
            var at = enemy.Position - new Vector3D<float>(0f, (1f - close) * CageHeight * size, 0f);
            _cages.Add(new CrowdInstance(at, enemy.Yaw, size, Flash: 0.1f + 0.3f * (1f - close)));
        }

        foreach (var gone in _cagedFor.Keys.Where(e => !caged.Contains(e)).ToList())
        {
            _cagedFor.Remove(gone);
        }
    }

    /// <summary>A ring of bone shards flung out for each Corpse Burst, and a ring of churned earth where each servant climbs out.</summary>
    private void DrawRings(RaisedDead dead, Func<float, float, float?> groundAt)
    {
        _bursts.Clear();
        foreach (var burst in dead.Bursts)
        {
            float t = burst.Age / RaisedDead.BurstSeconds;
            var at = new Vector3D<float>(burst.Centre.X, (groundAt(burst.Centre.X, burst.Centre.Z) ?? burst.Centre.Y) + 0.1f, burst.Centre.Z);
            _bursts.Add(new CrowdInstance(at, burst.Age * 3f, burst.Radius * (0.25f + 0.75f * t), Flash: 0.5f * (1f - t)));
        }

        _risings.Clear();
        foreach (var rising in dead.Risings)
        {
            float t = rising.Age / RaisedDead.RiseSeconds;
            var at = new Vector3D<float>(rising.Centre.X, (groundAt(rising.Centre.X, rising.Centre.Z) ?? rising.Centre.Y) + 0.04f, rising.Centre.Z);
            _risings.Add(new CrowdInstance(at, rising.Centre.X, rising.Size * (0.7f + 0.5f * t) * (t < 0.8f ? 1f : (1f - t) / 0.2f), Flash: 0.4f * (1f - t)));
        }
    }
}
