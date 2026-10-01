using ArenaMaster.Game.Combat;
using CEngine.Core;
using Silk.NET.Maths;

namespace ArenaMaster.Game.Ranger;

/// <summary>
/// The Ranger's basic attack: a bow that aims and fires on its own, over and over, at the nearest enemy it can see (<see cref="BowSight"/>) - the player only moves,
/// and looks round with the mouse. Arrows leave from the Ranger's bow and are led to where the target will be. The passive tree's timed majors fire from here too:
/// a focused shot after standing still (Sniper's Focus), a ring of arrows every tenth shot (Endless Quiver), and arrows raining on the target every few seconds
/// (Rain of Arrows).
/// </summary>
internal sealed class RangerBow
{
    public const string ArrowModel = "arrow_placeholder.glb";

    /// <summary>Where on the Ranger the arrow leaves from: this high above the feet, this far ahead.</summary>
    private const float ReleaseHeight = 1.3f;
    private const float ReleaseForward = 0.5f;

    /// <summary>Rain of Arrows: how high above the ground the arrows start, how fast they fall, and over how long a volley's arrows arrive.</summary>
    private const float RainHeight = 16f;
    private const float RainSpeed = 40f;
    private const float RainSpread = 0.6f;

    /// <summary>How big the focused arrow is drawn.</summary>
    private const float FocusScale = 1.4f;

    private readonly RangerArrows _arrows;
    private readonly Random _random;
    private readonly List<CrowdInstance> _copies = new();
    private readonly BowCadence _cadence = new();
    private readonly BowSight _sight = new();
    private readonly List<(float Delay, Vector3D<float> Origin, Vector3D<float> Direction)> _rain = new();
    private float _cooldown;

    /// <summary>A bow whose arrows answer to <paramref name="kit"/> (the Trapper tree's venom and marks) on every hit, if given.</summary>
    public RangerBow(Random random, TrapperKit? kit = null)
    {
        _random = random;
        _arrows = new RangerArrows(random) { Kit = kit };
    }

    /// <summary>How far the bow is through drawing its next shot: 0 as an arrow leaves, 1 as the next is about to (the body's draw keeps time with it).</summary>
    public float DrawProgress(RangerStats stats) => Math.Clamp(1f - _cooldown / stats.FireInterval, 0f, 1f);

    /// <summary>Whether the next shot is focused (Sniper's Focus), for the HUD.</summary>
    public bool FocusReady(RangerStats stats) => _cadence.FocusReady(stats);

    /// <summary>What the bow is shooting at, or null with nothing in range and sight (the body turns to it, the HUD marks it).</summary>
    public Enemy? Target => _sight.Target;

    /// <summary>The flat way to the target, or null with none.</summary>
    public Vector3D<float>? AimFlat(Vector3D<float> feet) => _sight.Target is { } target ? Flat(target.Position - feet) : null;

    /// <summary>
    /// Picks the nearest enemy in sight and fires at it when it's time (unless <paramref name="canFire"/> is false - a stun holds the bow), then moves the arrows
    /// already in the air. With nothing to shoot the bow waits, drawn, and looses the moment something comes in range. <paramref name="standingStill"/> feeds
    /// Sniper's Focus. Returns this frame's hits, for anything that answers to them (healing on a crit, say).
    /// </summary>
    public List<ArrowHit> Update(EngineWindow window, float deltaSeconds, RangerStats stats, EnemyField enemies, DamageNumbers numbers, Func<float, float, float?> groundAt,
        bool canFire = true, bool standingStill = false)
    {
        _cadence.Update(deltaSeconds, standingStill);
        _cooldown = canFire ? _cooldown - deltaSeconds : MathF.Max(_cooldown, 0.15f);
        if (window.Terrain is { } terrain)
        {
            var feet = window.PlayerFeet;
            var bow = feet + new Vector3D<float>(0f, ReleaseHeight, 0f);
            _sight.Update(enemies.Enemies, bow, stats.Range, deltaSeconds, (from, to) => ClearLine(terrain, from, to));

            if (_sight.AimPoint(bow, stats.ArrowSpeed) is { } aimPoint)
            {
                var aimFlat = Flat(aimPoint - feet);
                var origin = bow + aimFlat * ReleaseForward;
                if (_cooldown <= 0f)
                {
                    _cooldown += stats.FireInterval;
                    if (_cooldown < 0f)
                    {
                        _cooldown = 0f;   // after a pause, don't fire a burst to catch up
                    }

                    var toward = aimPoint - origin;
                    Shoot(stats, origin, toward.LengthSquared > 1e-6f ? Vector3D.Normalize(toward) : aimFlat);
                }

                if (canFire && _cadence.RainDue(stats))
                {
                    var centre = _sight.Target!.Position;
                    QueueRain(stats, new Vector3D<float>(centre.X, groundAt(centre.X, centre.Z) ?? centre.Y, centre.Z));
                }
            }
            else
            {
                _cooldown = MathF.Max(_cooldown, 0f);   // nothing to shoot: the bow waits, drawn
                _cadence.HoldRain();
            }
        }

        FallRain(stats, deltaSeconds);

        var gone = new List<Arrow>();
        var hits = _arrows.Update(deltaSeconds, enemies, groundAt, gone);
        foreach (var hit in hits)
        {
            numbers.Add(hit.Position, hit.Damage, hit.Killed, hit.Crit);
        }

        _copies.Clear();
        foreach (var arrow in _arrows.Arrows)
        {
            var (yaw, pitch) = Geometry.YawPitch(arrow.Heading);
            _copies.Add(new CrowdInstance(arrow.Position, yaw, arrow.Scale, pitch));
        }

        window.SetCrowd(ArrowModel, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_copies));
        return hits;
    }

    /// <summary>Takes every arrow out of the world (a restart).</summary>
    public void Clear(EngineWindow window)
    {
        _arrows.Clear();
        window.SetCrowd(ArrowModel, ReadOnlySpan<CrowdInstance>.Empty);
        _rain.Clear();
        _cadence.Reset();
        _sight.Clear();
    }

    /// <summary>
    /// A shot: the fan of arrows toward the aim - the middle one focused if Sniper's Focus is ready (much harder, piercing everything) - and, on every tenth shot with
    /// Endless Quiver, a full ring of arrows around the Ranger as well.
    /// </summary>
    private void Shoot(RangerStats stats, Vector3D<float> origin, Vector3D<float> aim)
    {
        var (focused, ring) = _cadence.Shoot(stats);
        int middle = (stats.ArrowsPerShot - 1) / 2;
        int i = 0;
        foreach (var direction in Fan(aim, stats.ArrowsPerShot, RangerStats.SplitSpreadDegrees))
        {
            var arrow = Fire(stats, origin, direction);
            if (focused && i == middle)
            {
                arrow.Damage *= RangerStats.FocusDamage;
                arrow.PierceLeft = int.MaxValue / 2;
                arrow.Scale = FocusScale;
            }

            i++;
        }

        if (ring)
        {
            foreach (var direction in Ring(RangerStats.QuiverRing))
            {
                Fire(stats, origin, direction);
            }
        }
    }

    private Arrow Fire(RangerStats stats, Vector3D<float> origin, Vector3D<float> direction) =>
        _arrows.Fire(origin, direction, stats.ArrowSpeed, stats.Range, stats.Damage, stats.Pierce, stats.CritChance, stats.CritMultiplier,
            stats.Chains, stats.ChainRange, stats.HitRules);

    /// <summary>Lines up a Rain of Arrows over <paramref name="centre"/> (a spot on the ground): its arrows arrive over a moment rather than all at once.</summary>
    private void QueueRain(RangerStats stats, Vector3D<float> centre)
    {
        foreach (var (origin, direction) in RainDrops(centre, stats.RainArrows, _random, stats.RainPatch))
        {
            _rain.Add(((float)_random.NextDouble() * RainSpread, origin, direction));
        }
    }

    private void FallRain(RangerStats stats, float deltaSeconds)
    {
        for (int i = _rain.Count - 1; i >= 0; i--)
        {
            var drop = _rain[i];
            drop.Delay -= deltaSeconds;
            if (drop.Delay > 0f)
            {
                _rain[i] = drop;
                continue;
            }

            _arrows.Fire(drop.Origin, drop.Direction, RainSpeed, RainHeight * 2f, stats.Damage, 0, stats.CritChance, stats.CritMultiplier, 0, stats.ChainRange, stats.HitRules);
            _rain.RemoveAt(i);
        }
    }

    /// <summary>
    /// Where each of <paramref name="count"/> raining arrows starts and which way it falls: spread over a patch <see cref="RangerStats.RainRadius"/> around
    /// <paramref name="centre"/>, starting high above it, falling steeply with a slight lean.
    /// </summary>
    public static IEnumerable<(Vector3D<float> Origin, Vector3D<float> Direction)> RainDrops(Vector3D<float> centre, int count, Random random,
        float radius = RangerStats.RainRadius)
    {
        for (int i = 0; i < count; i++)
        {
            float angle = (float)random.NextDouble() * MathF.Tau;
            float distance = radius * MathF.Sqrt((float)random.NextDouble());   // even over the patch, not bunched in the middle
            var landing = centre + new Vector3D<float>(MathF.Sin(angle) * distance, 0f, MathF.Cos(angle) * distance);
            var lean = new Vector3D<float>((float)random.NextDouble() - 0.5f, 0f, (float)random.NextDouble() - 0.5f) * 0.3f;
            var direction = Vector3D.Normalize(new Vector3D<float>(0f, -1f, 0f) + lean);
            yield return (landing - direction * (RainHeight / -direction.Y), direction);
        }
    }

    /// <summary><paramref name="count"/> flat directions evenly around the compass (Endless Quiver's ring).</summary>
    public static IEnumerable<Vector3D<float>> Ring(int count)
    {
        for (int i = 0; i < count; i++)
        {
            float angle = MathF.Tau * i / count;
            yield return new Vector3D<float>(MathF.Sin(angle), 0f, MathF.Cos(angle));
        }
    }

    /// <summary>
    /// <paramref name="count"/> directions fanned out evenly around <paramref name="aim"/>, turned about the vertical <paramref name="spreadDegrees"/> apart - the middle one
    /// (for an odd count) straight along the aim.
    /// </summary>
    public static IEnumerable<Vector3D<float>> Fan(Vector3D<float> aim, int count, float spreadDegrees)
    {
        for (int i = 0; i < count; i++)
        {
            float angle = (i - (count - 1) * 0.5f) * spreadDegrees * MathF.PI / 180f;
            float cos = MathF.Cos(angle), sin = MathF.Sin(angle);
            yield return new Vector3D<float>(aim.X * cos + aim.Z * sin, aim.Y, -aim.X * sin + aim.Z * cos);
        }
    }

    /// <summary>Whether the ground leaves the line from <paramref name="from"/> to <paramref name="to"/> open (a hill or a cave's rock would take the arrow).</summary>
    private static bool ClearLine(Terrain terrain, Vector3D<float> from, Vector3D<float> to)
    {
        var line = to - from;
        float length = line.Length;
        return length < 1e-3f || !terrain.TryRaycast(from, line / length, MathF.Max(0f, length - 0.3f), out _);
    }

    private static Vector3D<float> Flat(Vector3D<float> v)
    {
        var flat = new Vector3D<float>(v.X, 0f, v.Z);
        return flat.LengthSquared > 1e-6f ? Vector3D.Normalize(flat) : Vector3D<float>.UnitZ;
    }
}
