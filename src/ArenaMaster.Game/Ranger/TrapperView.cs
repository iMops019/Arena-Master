using ArenaMaster.Game.Combat;
using CEngine.Core;
using Silk.NET.Maths;

namespace ArenaMaster.Game.Ranger;

/// <summary>
/// Puts the Trapper tree's side of the fight (<see cref="TrapperKit"/>) on screen as engine crowds. Snares lie open on the ground and snap shut on what steps in;
/// a burst throws out a ring, and caltrops lie scattered where it was. Everything poisoned drips green, more for every dose; a toxic cloud is a heap of green puffs
/// swelling and rolling. The hawk circles overhead, flapping and gliding by turns, tucks its wings to dive, and beats back up; a mark hangs over the enemy it
/// marked. The drips are the view's own particles: pure show, no effect on the fight.
/// </summary>
internal sealed class TrapperView
{
    public const string SnareOpenModel = "snare_open.glb";
    public const string SnareShutModel = "snare_shut.glb";
    public const string BurstModel = "snare_burst.glb";
    public const string CaltropsModel = "caltrops.glb";
    public const string DripModel = "venom_drip.glb";
    public const string PuffModel = "toxic_puff.glb";
    public const string HawkUpModel = "hawk_up.glb";
    public const string HawkDownModel = "hawk_down.glb";
    public const string MarkModel = "hawk_mark.glb";

    /// <summary>At most this many drips at once, however much is poisoned.</summary>
    private const int MaxDrips = 700;

    /// <summary>Drips a second from each dose of poison on an enemy.</summary>
    private const float DripsPerDose = 2.5f;

    /// <summary>How big a drip is drawn, against the model's radius of 1.</summary>
    private const float DripSize = 0.06f;

    /// <summary>Puffs in a cloud for each square metre it covers (at least a few), and how big each is against the cloud's radius.</summary>
    private const float PuffsPerSquareMetre = 1.6f;
    private const int MinPuffs = 5;
    private const float PuffSize = 0.55f;

    /// <summary>How long a wingbeat takes (the hawk shows wings up, then wings down), how long it flaps, then how long it glides.</summary>
    private const float BeatTime = 0.14f;
    private const float FlapFor = 1.1f;
    private const float GlideFor = 1.3f;

    /// <summary>How big the hawk is drawn, against the model's 1 m wingspan.</summary>
    private const float HawkSize = 1.1f;

    private sealed class Drip
    {
        public Vector3D<float> Position;
        public Vector3D<float> Velocity;
        public float Age;
        public float Life;
        public float Size;
    }

    private readonly Random _random = new(29);
    private readonly List<Drip> _drips = new();
    private readonly List<CrowdInstance> _open = new();
    private readonly List<CrowdInstance> _shut = new();
    private readonly List<CrowdInstance> _bursts = new();
    private readonly List<CrowdInstance> _caltrops = new();
    private readonly List<CrowdInstance> _dripCopies = new();
    private readonly List<CrowdInstance> _puffs = new();
    private readonly List<CrowdInstance> _hawkUp = new();
    private readonly List<CrowdInstance> _hawkDown = new();
    private readonly List<CrowdInstance> _marks = new();
    private float _time;
    private bool _glowSet;

    /// <summary>Draws this frame of <paramref name="kit"/>, and moves the drips on.</summary>
    public void Sync(EngineWindow window, TrapperKit kit, float deltaSeconds, Func<float, float, float?> groundAt)
    {
        if (!_glowSet)
        {
            window.SetCrowdGlow(DripModel, 0.8f);
            window.SetCrowdGlow(PuffModel, 0.35f);
            window.SetCrowdGlow(BurstModel, 0.9f);
            window.SetCrowdGlow(MarkModel, 1.1f);
            _glowSet = true;
        }

        _time += deltaSeconds;
        DrawSnares(kit, groundAt);
        DrawGround(kit, groundAt);
        DrawPoison(kit, deltaSeconds, groundAt);
        DrawHawk(kit);

        Set(window, SnareOpenModel, _open);
        Set(window, SnareShutModel, _shut);
        Set(window, BurstModel, _bursts);
        Set(window, CaltropsModel, _caltrops);
        Set(window, DripModel, _dripCopies);
        Set(window, PuffModel, _puffs);
        Set(window, HawkUpModel, _hawkUp);
        Set(window, HawkDownModel, _hawkDown);
        Set(window, MarkModel, _marks);
    }

    public void Clear(EngineWindow window)
    {
        _drips.Clear();
        foreach (var model in new[] { SnareOpenModel, SnareShutModel, BurstModel, CaltropsModel, DripModel, PuffModel, HawkUpModel, HawkDownModel, MarkModel })
        {
            window.SetCrowd(model, ReadOnlySpan<CrowdInstance>.Empty);
        }
    }

    /// <summary>Open snares lying armed; shut ones clamped on what they hold (at its feet), or where they lie if it's gone.</summary>
    private void DrawSnares(TrapperKit kit, Func<float, float, float?> groundAt)
    {
        _open.Clear();
        _shut.Clear();
        foreach (var snare in kit.Snares)
        {
            if (!snare.Sprung)
            {
                float glint = 0.12f * MathF.Max(0f, MathF.Sin(_time * 2.2f + snare.Yaw * 3f));
                _open.Add(new CrowdInstance(OnGround(snare.Position, groundAt, 0.02f), snare.Yaw, 1f, Flash: glint));
                continue;
            }

            var at = snare.Holding is { IsAlive: true } held && held.IsFrozen ? held.Position : snare.Position;
            float shake = snare.BurstIn < 0.4f ? 0.05f * MathF.Sin(_time * 70f) : 0f;   // about to go
            _shut.Add(new CrowdInstance(OnGround(at, groundAt, 0.02f), snare.Yaw + shake, 1f, Flash: 0.2f + 0.4f * MathF.Max(0f, 0.4f - snare.BurstIn)));
        }
    }

    /// <summary>The bursts' rings spreading and fading, the caltrops lying, and the clouds' puffs.</summary>
    private void DrawGround(TrapperKit kit, Func<float, float, float?> groundAt)
    {
        _bursts.Clear();
        foreach (var burst in kit.Bursts)
        {
            float t = burst.Age / TrapperKit.BurstShow;
            float spread = 1f - (1f - t) * (1f - t);
            _bursts.Add(new CrowdInstance(OnGround(burst.Centre, groundAt, 0.06f), burst.Centre.X, burst.Radius * (0.3f + 0.7f * spread), Flash: 0.8f * (1f - t)));
        }

        _caltrops.Clear();
        foreach (var patch in kit.Caltrops)
        {
            float fade = MathF.Min(1f, patch.Left / 0.4f);
            _caltrops.Add(new CrowdInstance(OnGround(patch.Centre, groundAt, 0.01f), patch.Centre.Z * 5f, patch.Radius * (0.85f + 0.15f * fade)));
        }

        _puffs.Clear();
        foreach (var cloud in kit.Clouds)
        {
            float age = cloud.Lifetime - cloud.Left;
            float grow = MathF.Min(1f, age / 0.35f) * MathF.Min(1f, cloud.Left / 0.5f);
            int count = Math.Max(MinPuffs, (int)(MathF.PI * cloud.Radius * cloud.Radius * PuffsPerSquareMetre));
            var ground = OnGround(cloud.Centre, groundAt, 0f);
            for (int i = 0; i < count; i++)
            {
                float angle = i * 2.39996f + cloud.Centre.X;   // the golden angle: puffs spread evenly over the cloud
                float distance = cloud.Radius * 0.75f * MathF.Sqrt((i + 0.5f) / count);
                float roll = _time * 0.6f + i;
                var at = ground + new Vector3D<float>(MathF.Sin(angle + 0.15f * MathF.Sin(roll)) * distance, 0.35f + 0.3f * (i % 3) * 0.5f + 0.12f * MathF.Sin(roll * 1.7f),
                    MathF.Cos(angle + 0.15f * MathF.Sin(roll)) * distance);
                float size = cloud.Radius * PuffSize * (0.75f + 0.25f * MathF.Sin(roll * 1.3f)) * grow;
                _puffs.Add(new CrowdInstance(at, roll * 0.4f, size, 0.3f * MathF.Sin(roll), Flash: 0.15f + 0.1f * MathF.Sin(roll * 2f)));
            }
        }
    }

    /// <summary>Everything poisoned drips green, more for every dose, and the drips fall and fade.</summary>
    private void DrawPoison(TrapperKit kit, float deltaSeconds, Func<float, float, float?> groundAt)
    {
        foreach (var (enemy, poisoning) in kit.Poisons)
        {
            if (!enemy.IsAlive)
            {
                continue;
            }

            float count = DripsPerDose * poisoning.Doses.Count * deltaSeconds;
            int whole = (int)count + (_random.NextDouble() < count - (int)count ? 1 : 0);
            for (int i = 0; i < whole && _drips.Count < MaxDrips; i++)
            {
                float height = enemy.Kind.Height * (0.25f + 0.7f * (float)_random.NextDouble());
                var at = enemy.Position + new Vector3D<float>(Signed() * enemy.Kind.Radius, height, Signed() * enemy.Kind.Radius);
                _drips.Add(new Drip
                {
                    Position = at,
                    Velocity = new Vector3D<float>(Signed() * 0.3f, 0.6f * (float)_random.NextDouble(), Signed() * 0.3f),
                    Life = 0.6f + 0.5f * (float)_random.NextDouble(),
                    Size = DripSize * (0.6f + 0.8f * (float)_random.NextDouble()),
                });
            }
        }

        _dripCopies.Clear();
        for (int i = _drips.Count - 1; i >= 0; i--)
        {
            var drip = _drips[i];
            drip.Age += deltaSeconds;
            drip.Velocity.Y -= 6f * deltaSeconds;   // falling, a little slower than a stone: it's thick
            drip.Position += drip.Velocity * deltaSeconds;
            float floor = groundAt(drip.Position.X, drip.Position.Z) ?? float.MinValue;
            if (drip.Age >= drip.Life || drip.Position.Y <= floor)
            {
                _drips.RemoveAt(i);
                continue;
            }

            float t = drip.Age / drip.Life;
            _dripCopies.Add(new CrowdInstance(drip.Position, drip.Age * 3f, drip.Size * (1f - 0.6f * t), Flash: 0.5f * (1f - t)));
        }

        _marks.Clear();
        foreach (var (enemy, left) in kit.Marks)
        {
            if (!enemy.IsAlive)
            {
                continue;
            }

            var above = enemy.Position + new Vector3D<float>(0f, enemy.Kind.Height * enemy.Kind.DrawScale + 0.55f + 0.08f * MathF.Sin(_time * 5f), 0f);
            _marks.Add(new CrowdInstance(above, _time * 2.5f, 1f, Flash: left < 1f ? 0.5f * MathF.Max(0f, MathF.Sin(_time * 20f)) : 0.3f));
        }
    }

    /// <summary>
    /// The hawk: circling, it flaps a while and glides a while; diving, its wings are swept back (the wings-up model, pitched down its stoop); climbing back, it
    /// beats hard.
    /// </summary>
    private void DrawHawk(TrapperKit kit)
    {
        _hawkUp.Clear();
        _hawkDown.Clear();
        if (kit.Hawk is not { } hawk)
        {
            return;
        }

        var (yaw, pitch) = Geometry.YawPitch(hawk.Heading);
        bool wingsUp = hawk.Flight switch
        {
            HawkFlight.Diving => true,
            HawkFlight.Returning => (int)(_time / (BeatTime * 0.7f)) % 2 == 0,
            _ => _time % (FlapFor + GlideFor) < FlapFor && (int)(_time / BeatTime) % 2 == 0,   // gliding on wings held down
        };
        (wingsUp ? _hawkUp : _hawkDown).Add(new CrowdInstance(hawk.Position, yaw, HawkSize, pitch));
    }

    private static void Set(EngineWindow window, string model, List<CrowdInstance> copies) =>
        window.SetCrowd(model, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(copies));

    private static Vector3D<float> OnGround(Vector3D<float> at, Func<float, float, float?> groundAt, float lift) =>
        new(at.X, (groundAt(at.X, at.Z) ?? at.Y) + lift, at.Z);

    private float Signed() => (float)_random.NextDouble() * 2f - 1f;
}
