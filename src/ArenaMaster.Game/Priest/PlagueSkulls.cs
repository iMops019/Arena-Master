using ArenaMaster.Game.Combat;
using Silk.NET.Maths;

namespace ArenaMaster.Game.Priest;

/// <summary>
/// A Plague Skull: loosed from the wand, it flies at about chest height over the ground, turning onto the nearest enemy it hasn't struck yet, striking and
/// piercing on through, until its pierces or its life run out.
/// </summary>
internal sealed class PlagueSkull
{
    public Vector3D<float> Position { get; set; }

    /// <summary>Which way it flies, flat (unit length).</summary>
    public Vector3D<float> Heading { get; set; }

    public float Speed { get; init; }

    public float Age { get; set; }

    public float Life { get; init; }

    public float Damage { get; set; }

    /// <summary>How many more enemies it goes on through after the next it strikes.</summary>
    public int PierceLeft { get; set; }

    /// <summary>The enemy it is turning onto, or null (then it looks for the nearest, or flies straight on).</summary>
    public Enemy? Target { get; set; }

    /// <summary>The enemies it has struck: it never strikes one twice.</summary>
    public HashSet<Enemy> AlreadyHit { get; } = new();
}

/// <summary>The Plague on one enemy: a stack for each infection (each with its own time left, up to the most it can carry), and when it next ticks.</summary>
internal sealed class Infection
{
    public List<float> Stacks { get; } = new();

    public float TickIn { get; set; }
}

/// <summary>
/// Rot lying on the ground: a circle (Rotting Step's) or a wedge of one (Death and Decay's cone, <see cref="HalfArc"/> either side of <see cref="Yaw"/>). Everything
/// in it rots every <see cref="PlagueSkulls.RotTick"/> seconds.
/// </summary>
internal sealed class RotPatch
{
    public Vector3D<float> Centre { get; init; }

    public float Radius { get; init; }

    /// <summary>Which way a wedge opens (0 is +Z), and how far either side of it; <see cref="MathF.PI"/> for a whole circle.</summary>
    public float Yaw { get; init; }

    public float HalfArc { get; init; } = MathF.PI;

    public float Lifetime { get; init; }

    public float Left { get; set; }

    public float DamagePerSecond { get; init; }

    public float TickIn { get; set; }

    /// <summary>How many times it has ticked.</summary>
    public int Ticks { get; set; }

    /// <summary>Whether a point on the ground is in it.</summary>
    public bool Covers(Vector3D<float> point, float give = 0f)
    {
        var toward = Geometry.FlatDirection(Centre, point, out float distance);
        if (distance > Radius + give)
        {
            return false;
        }

        if (HalfArc >= MathF.PI || distance < 0.5f)
        {
            return true;
        }

        float angle = MathF.Atan2(toward.X, toward.Z);
        return MathF.Abs(MathF.IEEERemainder(angle - Yaw, MathF.Tau)) <= HalfArc + give / MathF.Max(distance, 0.5f);
    }
}

/// <summary>Death and Decay spewing from the Skull Shield, for the view: where from, which way, how wide and far, and how long ago.</summary>
internal sealed class RotSpray
{
    public Vector3D<float> Origin { get; init; }

    public float Yaw { get; init; }

    public float HalfArc { get; init; }

    public float Reach { get; init; }

    public float Age { get; set; }
}

/// <summary>Plague leaping from a dead enemy to another (Pestilence), for the view.</summary>
internal sealed class PlagueLeap
{
    public Vector3D<float> From { get; init; }

    public Vector3D<float> To { get; init; }

    public float Age { get; set; }
}

internal enum PriestSource
{
    Skull,
    Plague,
    Rot,
    Aura,
}

internal readonly record struct PriestHit(Enemy Enemy, Vector3D<float> Position, float Damage, bool Killed, bool Crit, PriestSource Source);

/// <summary>
/// The Priest's attack, the Plague Skull, and everything of death and decay around it: every so often, with an enemy in range, a skull leaves the wand and
/// hunts through the crowd, piercing one enemy after another and Plaguing each (a stack of damage over time; stacks pile up to a cap). Rot lies on the ground
/// (Death and Decay's cone, Rotting Step's patch) and rots what stands in it. And the Unholy tree's majors: Virulent Strain, Twin Skulls, Pestilence, Grave Soil,
/// Gnashing Skulls, Epidemic, Aura of Decay, Black Death, Legion and Necropolis. Pure - no engine calls; <see cref="PriestView"/> draws it.
/// </summary>
internal sealed class PlagueSkulls
{
    /// <summary>How fat a skull is for hitting, and how high over the ground it flies.</summary>
    public const float SkullRadius = 0.3f;
    public const float Hover = 1.05f;

    /// <summary>How hard a skull turns onto its target: gently at first, harder the longer it flies; and within this distance, straight at it.</summary>
    public const float TurnRate = 5f;
    public const float TurnGrowth = 12f;
    public const float CloseIn = 2.5f;

    /// <summary>The skulls of one cast leave this far apart in time, each turned this many degrees further off the first.</summary>
    public const float SkullStagger = 0.12f;
    public const float SkullSpread = 16f;

    /// <summary>Plague ticks this often; rot on the ground and the Aura, this often.</summary>
    public const float PlagueTick = 0.25f;
    public const float RotTick = 0.5f;

    /// <summary>How long a spray and a leap show.</summary>
    public const float SprayTime = 0.4f;
    public const float LeapTime = 0.3f;

    /// <summary>The first cast of a run can go this soon.</summary>
    public const float FirstCast = 0.4f;

    private readonly Random _random;
    private readonly List<PlagueSkull> _skulls = new();
    private readonly Dictionary<Enemy, Infection> _infections = new();
    private readonly List<RotPatch> _patches = new();
    private readonly List<RotSpray> _sprays = new();
    private readonly List<PlagueLeap> _leaps = new();
    private readonly List<(float Delay, int Index, int Count)> _launches = new();
    private float _auraIn = RotTick;

    public PlagueSkulls(Random random) => _random = random;

    public IReadOnlyList<PlagueSkull> Skulls => _skulls;

    public IReadOnlyDictionary<Enemy, Infection> Infections => _infections;

    public IReadOnlyList<RotPatch> Patches => _patches;

    public IReadOnlyList<RotSpray> Sprays => _sprays;

    public IReadOnlyList<PlagueLeap> Leaps => _leaps;

    /// <summary>Seconds until the next cast may go (it waits, at 0, for an enemy in range).</summary>
    public float CastIn { get; private set; } = FirstCast;

    public int Casts { get; private set; }

    /// <summary>How many Plagued enemies died during the last <see cref="Update"/> (Soul Harvest heals for each).</summary>
    public int PlaguedDeaths { get; private set; }

    /// <summary>How many stacks of Plague <paramref name="enemy"/> carries now.</summary>
    public int StacksOn(Enemy enemy) => _infections.TryGetValue(enemy, out var infection) ? infection.Stacks.Count : 0;

    /// <summary>
    /// One frame: a cast from <paramref name="hand"/> when one is due and an enemy is in range (held, skulls still to leave included, while
    /// <paramref name="canCast"/> is false - a stun), the skulls in flight, the Plague on every enemy, the rot on the ground, and the Aura of Decay around
    /// <paramref name="feet"/>. <paramref name="aimFlat"/> is where the camera faces: enemies in front are aimed at first. Every hit goes on <paramref name="hits"/>.
    /// </summary>
    public void Update(float deltaSeconds, Vector3D<float> hand, Vector3D<float> feet, Vector3D<float> aimFlat, PriestStats stats, EnemyField enemies,
        Func<float, float, float?> groundAt, bool canCast, List<PriestHit> hits)
    {
        PlaguedDeaths = 0;
        if (canCast)
        {
            CastIn -= deltaSeconds;
            if (CastIn <= 0f && _launches.Count == 0)
            {
                if (enemies.Within(feet, PriestStats.CastRange).Any(e => !e.Kind.IsProp))
                {
                    Cast(stats);
                }
                else
                {
                    CastIn = 0f;   // ready, and waiting for something to aim at
                }
            }

            for (int i = 0; i < _launches.Count; i++)
            {
                var launch = _launches[i];
                launch.Delay -= deltaSeconds;
                _launches[i] = launch;
            }

            foreach (var launch in _launches.Where(l => l.Delay <= 0f).ToList())
            {
                _launches.Remove(launch);
                Launch(launch.Index, launch.Count, hand, aimFlat, stats, enemies);
            }
        }

        MoveSkulls(deltaSeconds, stats, enemies, groundAt, hits);
        TickPlague(deltaSeconds, stats, enemies, hits);
        TickRot(deltaSeconds, stats, enemies, hits);
        TickAura(deltaSeconds, feet, stats, enemies, hits);

        foreach (var spray in _sprays)
        {
            spray.Age += deltaSeconds;
        }

        _sprays.RemoveAll(s => s.Age >= SprayTime);
        foreach (var leap in _leaps)
        {
            leap.Age += deltaSeconds;
        }

        _leaps.RemoveAll(l => l.Age >= LeapTime);
    }

    /// <summary>A cast: its skulls lined up a moment apart.</summary>
    public void Cast(PriestStats stats)
    {
        Casts++;
        CastIn = stats.CastInterval;
        int count = stats.Skulls;
        for (int i = 0; i < count; i++)
        {
            _launches.Add((i * SkullStagger, i, count));
        }
    }

    /// <summary>
    /// Plagues <paramref name="enemy"/>: a fresh stack, or - at the most it can carry - its oldest stack made fresh again. Nothing happens to one dead, or to a
    /// crate.
    /// </summary>
    public void Infect(Enemy enemy, PriestStats stats)
    {
        if (!enemy.IsAlive || enemy.Kind.IsProp)
        {
            return;
        }

        if (!_infections.TryGetValue(enemy, out var infection))
        {
            infection = new Infection { TickIn = PlagueTick };
            _infections[enemy] = infection;
        }

        float duration = stats.PlagueDuration;
        if (infection.Stacks.Count < stats.PlagueStacks)
        {
            infection.Stacks.Add(duration);
        }
        else
        {
            int oldest = 0;
            for (int i = 1; i < infection.Stacks.Count; i++)
            {
                if (infection.Stacks[i] < infection.Stacks[oldest])
                {
                    oldest = i;
                }
            }

            infection.Stacks[oldest] = duration;
        }
    }

    /// <summary>
    /// Death and Decay: a wide cone of rot spewed from the Skull Shield at <paramref name="feet"/> toward <paramref name="yaw"/>, lying on the ground for its
    /// time.
    /// </summary>
    public void SpewDecay(Vector3D<float> feet, float yaw, PriestStats stats)
    {
        float halfArc = PriestStats.DecayConeDegrees * 0.5f * MathF.PI / 180f;
        float reach = stats.DecayConeLength;
        AddPatch(feet, reach, yaw, halfArc, stats.DecayTime(PriestStats.DecayConeSeconds), PriestStats.DecayConeDamage * stats.DecayScale);
        _sprays.Add(new RotSpray { Origin = feet, Yaw = yaw, HalfArc = halfArc, Reach = reach });
    }

    /// <summary>The rot Rotting Step leaves where the Priest vanished.</summary>
    public void RotAt(Vector3D<float> point, PriestStats stats) =>
        AddPatch(point, stats.StepRotRadiusNow, 0f, MathF.PI, stats.DecayTime(PriestStats.StepRotSeconds), PriestStats.StepRotDamage * stats.DecayScale);

    /// <summary>
    /// Damage from the Priest to one enemy (harder on an elite or a boss). True if it killed. Nothing happens to one already dead.
    /// </summary>
    public bool Hurt(Enemy enemy, float amount, bool crit, PriestSource source, PriestStats stats, EnemyField enemies, List<PriestHit> hits)
    {
        if (!enemy.IsAlive || amount <= 0f)
        {
            return false;
        }

        if (enemy.Kind.Tier != EnemyTier.Fodder)
        {
            amount *= stats.EliteMultiplier;
        }

        bool killed = enemies.Damage(enemy, amount);
        hits.Add(new PriestHit(enemy, enemy.Position + new Vector3D<float>(0f, enemy.Kind.Height * 0.7f, 0f), amount, killed, crit, source));
        return killed;
    }

    /// <summary>Everything out of the world (a restart), and the clocks back to the start.</summary>
    public void Reset()
    {
        _skulls.Clear();
        _infections.Clear();
        _patches.Clear();
        _sprays.Clear();
        _leaps.Clear();
        _launches.Clear();
        CastIn = FirstCast;
        Casts = 0;
        PlaguedDeaths = 0;
        _auraIn = RotTick;
    }

    private void AddPatch(Vector3D<float> centre, float radius, float yaw, float halfArc, float seconds, float damagePerSecond) =>
        _patches.Add(new RotPatch
        {
            Centre = centre, Radius = radius, Yaw = yaw, HalfArc = halfArc, Lifetime = seconds, Left = seconds, DamagePerSecond = damagePerSecond, TickIn = 0f,
        });

    /// <summary>Skull <paramref name="index"/> of <paramref name="count"/> leaves the wand: toward the best enemy in range (in front first, nearest first), each after the first turned a little further off it.</summary>
    private void Launch(int index, int count, Vector3D<float> hand, Vector3D<float> aimFlat, PriestStats stats, EnemyField enemies)
    {
        var target = enemies.Within(hand, PriestStats.CastRange).Where(e => !e.Kind.IsProp).OrderBy(e => Rank(e, hand, aimFlat)).FirstOrDefault();
        var toward = target is null ? aimFlat : Geometry.FlatDirection(hand, target.Position, out _);
        if (toward == Vector3D<float>.Zero)
        {
            toward = aimFlat == Vector3D<float>.Zero ? Vector3D<float>.UnitZ : aimFlat;
        }

        float turn = (index + 1) / 2 * SkullSpread * (index % 2 == 1 ? 1f : -1f) * MathF.PI / 180f;
        float cos = MathF.Cos(turn), sin = MathF.Sin(turn);
        _skulls.Add(new PlagueSkull
        {
            Position = hand,
            Heading = new Vector3D<float>(toward.X * cos + toward.Z * sin, 0f, -toward.X * sin + toward.Z * cos),
            Speed = stats.SkullSpeed,
            Life = stats.SkullLife,
            Damage = stats.SkullDamage,
            PierceLeft = stats.Pierce,
            Target = index == 0 ? target : null,
        });
    }

    /// <summary>How good a target an enemy is: nearer is better, and one behind the Priest counts as half again as far.</summary>
    private static float Rank(Enemy enemy, Vector3D<float> from, Vector3D<float> aimFlat)
    {
        var toward = Geometry.FlatDirection(from, enemy.Position, out float distance);
        return Vector3D.Dot(toward, aimFlat) >= 0f ? distance : distance * 1.5f;
    }

    /// <summary>The nearest live enemy (not a crate) within <paramref name="range"/> of <paramref name="from"/> that isn't in <paramref name="skip"/>.</summary>
    private static Enemy? Nearest(EnemyField enemies, Vector3D<float> from, float range, IReadOnlySet<Enemy> skip)
    {
        Enemy? best = null;
        float bestDistance = float.MaxValue;
        foreach (var enemy in enemies.Within(from, range))
        {
            if (enemy.Kind.IsProp || skip.Contains(enemy))
            {
                continue;
            }

            float distance = Vector3D.DistanceSquared(enemy.Position, from);
            if (distance < bestDistance)
            {
                best = enemy;
                bestDistance = distance;
            }
        }

        return best;
    }

    private void MoveSkulls(float deltaSeconds, PriestStats stats, EnemyField enemies, Func<float, float, float?> groundAt, List<PriestHit> hits)
    {
        for (int i = _skulls.Count - 1; i >= 0; i--)
        {
            var skull = _skulls[i];
            skull.Age += deltaSeconds;
            if (skull.Age >= skull.Life)
            {
                _skulls.RemoveAt(i);
                continue;
            }

            if (skull.Target is not { IsAlive: true } || skull.AlreadyHit.Contains(skull.Target))
            {
                skull.Target = Nearest(enemies, skull.Position, stats.SeekRange, skull.AlreadyHit);
            }

            if (skull.Target is { } target)
            {
                var want = Geometry.FlatDirection(skull.Position, target.Position, out float distance);
                if (want != Vector3D<float>.Zero)
                {
                    // Close in, it goes straight at the target: one that only turned would circle a target it came at from the side.
                    float turn = distance < CloseIn ? 1f : MathF.Min(1f, (TurnRate + TurnGrowth * skull.Age) * deltaSeconds);
                    var blended = skull.Heading + (want - skull.Heading) * turn;
                    if (blended.LengthSquared > 1e-6f)
                    {
                        skull.Heading = Vector3D.Normalize(blended);
                    }
                }
            }

            var from = skull.Position;
            var to = from + skull.Heading * skull.Speed * deltaSeconds;
            float ground = groundAt(to.X, to.Z) ?? to.Y - Hover;
            to.Y += (ground + Hover - to.Y) * MathF.Min(1f, 10f * deltaSeconds);   // gliding over the ground at chest height

            bool gone = false;
            while (enemies.FirstHit(from, to, SkullRadius, out float along, skull.AlreadyHit) is { } enemy)
            {
                skull.AlreadyHit.Add(enemy);
                Strike(skull, enemy, stats, enemies, hits);
                if (skull.PierceLeft <= 0)
                {
                    gone = true;
                    break;
                }

                skull.PierceLeft--;
                skull.Target = null;
            }

            if (gone)
            {
                _skulls.RemoveAt(i);
            }
            else
            {
                skull.Position = to;
            }
        }
    }

    /// <summary>
    /// A skull's hit: its damage (a crit roll), the Plague it leaves (and with Epidemic, on those around too), and with Gnashing Skulls a kill feeds it: one more
    /// pierce and more damage.
    /// </summary>
    private void Strike(PlagueSkull skull, Enemy enemy, PriestStats stats, EnemyField enemies, List<PriestHit> hits)
    {
        bool crit = _random.NextDouble() < stats.CritChance;
        bool killed = Hurt(enemy, crit ? skull.Damage * stats.CritMultiplier : skull.Damage, crit, PriestSource.Skull, stats, enemies, hits);
        Infect(enemy, stats);
        if (stats.Tree.Epidemic)
        {
            foreach (var near in enemies.Within(enemy.Position, PriestStats.EpidemicRadius))
            {
                if (near != enemy)
                {
                    Infect(near, stats);
                }
            }
        }

        if (killed && stats.Tree.GnashingSkulls && !enemy.Kind.IsProp)
        {
            skull.PierceLeft++;
            skull.Damage *= 1f + PriestStats.GnashingDamage;
        }
    }

    /// <summary>
    /// The Plague on every enemy: each stack running down, and every tick the enemy takes its share of every stack's damage (able to crit, with Black Death). A
    /// Plagued enemy that died (from anything) is counted for Soul Harvest and, with Pestilence, its Plague leaps to the nearest others.
    /// </summary>
    private void TickPlague(float deltaSeconds, PriestStats stats, EnemyField enemies, List<PriestHit> hits)
    {
        if (_infections.Count == 0)
        {
            return;
        }

        float perStackTick = stats.PlagueDamage * PlagueTick / MathF.Max(0.1f, stats.PlagueDuration);
        foreach (var (enemy, infection) in _infections.ToList())
        {
            if (!enemy.IsAlive)
            {
                _infections.Remove(enemy);
                Died(enemy, infection.Stacks.Count, stats, enemies);
                continue;
            }

            for (int i = infection.Stacks.Count - 1; i >= 0; i--)
            {
                infection.Stacks[i] -= deltaSeconds;
            }

            infection.TickIn -= deltaSeconds;
            while (infection.TickIn <= 0f && infection.Stacks.Count > 0 && enemy.IsAlive)
            {
                infection.TickIn += PlagueTick;
                bool crit = stats.Tree.BlackDeath && _random.NextDouble() < stats.CritChance;
                float damage = perStackTick * infection.Stacks.Count * (crit ? stats.CritMultiplier : 1f);
                Hurt(enemy, damage, crit, PriestSource.Plague, stats, enemies, hits);
            }

            infection.Stacks.RemoveAll(left => left <= 0f);
            if (!enemy.IsAlive)
            {
                _infections.Remove(enemy);
                Died(enemy, Math.Max(1, infection.Stacks.Count), stats, enemies);
            }
            else if (infection.Stacks.Count == 0)
            {
                _infections.Remove(enemy);
            }
        }
    }

    /// <summary>A Plagued enemy died: counted, and with Pestilence, its <paramref name="stacks"/> leap to the nearest enemies around it.</summary>
    private void Died(Enemy enemy, int stacks, PriestStats stats, EnemyField enemies)
    {
        PlaguedDeaths++;
        if (!stats.Tree.Pestilence || stacks <= 0)
        {
            return;
        }

        var next = enemies.Within(enemy.Position, PriestStats.PestilenceRange)
            .Where(e => e != enemy && !e.Kind.IsProp)
            .OrderBy(e => Vector3D.DistanceSquared(e.Position, enemy.Position))
            .Take(PriestStats.PestilenceCount)
            .ToList();
        var from = enemy.Position + new Vector3D<float>(0f, enemy.Kind.Height * 0.6f, 0f);
        foreach (var other in next)
        {
            for (int i = 0; i < stacks; i++)
            {
                Infect(other, stats);
            }

            _leaps.Add(new PlagueLeap { From = from, To = other.Position + new Vector3D<float>(0f, other.Kind.Height * 0.6f, 0f) });
        }
    }

    /// <summary>The rot on the ground: running down, and every tick hurting (slowing, with Grave Soil; Plaguing, with Necropolis) everything in it.</summary>
    private void TickRot(float deltaSeconds, PriestStats stats, EnemyField enemies, List<PriestHit> hits)
    {
        for (int i = _patches.Count - 1; i >= 0; i--)
        {
            var patch = _patches[i];
            patch.Left -= deltaSeconds;
            if (patch.Left <= 1e-3f)   // (a hair's slack, so time added up frame by frame doesn't sneak in a tick past its end)
            {
                _patches.RemoveAt(i);
                continue;
            }

            patch.TickIn -= deltaSeconds;
            if (patch.TickIn > 0f)
            {
                continue;
            }

            patch.TickIn += RotTick;
            bool plague = stats.Tree.Necropolis && patch.Ticks++ % 2 == 0;   // a stack each second: every other tick, from the first
            foreach (var enemy in enemies.Within(patch.Centre, patch.Radius))
            {
                if (enemy.Kind.IsProp || !patch.Covers(enemy.Position, enemy.Kind.Radius))
                {
                    continue;
                }

                Hurt(enemy, patch.DamagePerSecond * RotTick, crit: false, PriestSource.Rot, stats, enemies, hits);
                if (stats.Tree.GraveSoil && enemy.IsAlive)
                {
                    enemy.Chill(RotTick + 0.2f, PriestStats.GraveSoilSlow);
                }

                if (plague)
                {
                    Infect(enemy, stats);
                }
            }
        }
    }

    /// <summary>The Aura of Decay: every tick, everything close around the Priest rots.</summary>
    private void TickAura(float deltaSeconds, Vector3D<float> feet, PriestStats stats, EnemyField enemies, List<PriestHit> hits)
    {
        if (!stats.Tree.AuraOfDecay)
        {
            return;
        }

        _auraIn -= deltaSeconds;
        if (_auraIn > 0f)
        {
            return;
        }

        _auraIn += RotTick;
        foreach (var enemy in enemies.Within(feet, PriestStats.AuraRadius * stats.DecayArea))
        {
            if (!enemy.Kind.IsProp)
            {
                Hurt(enemy, PriestStats.AuraDamage * stats.DecayScale * RotTick, crit: false, PriestSource.Aura, stats, enemies, hits);
            }
        }
    }
}
