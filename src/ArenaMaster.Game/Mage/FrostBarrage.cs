using ArenaMaster.Game.Combat;
using Silk.NET.Maths;

namespace ArenaMaster.Game.Mage;

/// <summary>A bolt of ice from the Frost Barrage: it flies out from the staff, curves onto its target, and melts when its flight is spent.</summary>
internal sealed class FrostBolt
{
    public Vector3D<float> Position { get; set; }

    /// <summary>Which way it flies (unit length).</summary>
    public Vector3D<float> Heading { get; set; }

    public float Speed { get; set; }

    public float Age { get; set; }

    /// <summary>Seconds of flight left before it melts.</summary>
    public float FlightLeft { get; set; }

    public float Damage { get; set; }

    public int PierceLeft { get; set; }

    /// <summary>The enemy it is curving toward, or null (then it looks for the nearest one, or flies straight).</summary>
    public Enemy? Target { get; set; }

    /// <summary>The enemies it has already hit, so a piercing bolt never hits one twice.</summary>
    public HashSet<Enemy> AlreadyHit { get; } = new();

    public float Scale { get; set; } = 1f;

    /// <summary>A half of a bolt that killed (Splitting Ice). Halves never split again.</summary>
    public bool IsSplit { get; set; }

    /// <summary>The comet at the end of every 4th barrage (Comet): big, and it always blasts.</summary>
    public bool IsComet { get; set; }
}

/// <summary>A burst of frost spreading over the ground, for the view: a Frost Blast, a comet's blast, a Shattering Ward. Its damage is dealt when it appears.</summary>
internal sealed class FrostBurst
{
    public Vector3D<float> Centre { get; init; }

    public float Radius { get; init; }

    public float Age { get; set; }
}

internal enum FrostSource
{
    Bolt,
    Blast,
    Blizzard,
    Ward,
}

internal readonly record struct FrostHit(Enemy Enemy, Vector3D<float> Position, float Damage, bool Killed, bool Crit, FrostSource Source);

/// <summary>
/// The Mage's attacks: the Frost Barrage - every so often, while an enemy is in range, a volley of bolts leaves the staff one after another, fans out, and curves
/// onto enemies (no more at one than it takes to kill it, then on to the next) - and what the Frost tree adds to it: Frost Blast, Deep Freeze, Shatter, Splitting Ice, the Comet and
/// the Blizzard. Every frost hit chills. Pure simulation - no engine calls - so it can be tested; <see cref="FrostView"/> draws it.
/// </summary>
internal sealed class FrostBarrage
{
    /// <summary>How fat a bolt is for hitting.</summary>
    public const float BoltRadius = 0.18f;

    /// <summary>How hard a bolt turns toward its target: a gentle curve at first, tightening the longer it flies so it never circles its target.</summary>
    public const float TurnRate = 4f;
    public const float TurnGrowth = 30f;

    /// <summary>Within this distance of its target, a bolt stops curving and flies straight at it.</summary>
    public const float CloseIn = 3f;

    /// <summary>The bolts of a barrage leave in a fan this wide (degrees either side of the way to their target), and a little upward.</summary>
    public const float FanDegrees = 55f;
    public const float Lift = 0.35f;

    /// <summary>How far a bolt whose target died looks for another.</summary>
    public const float RetargetRange = 12f;

    /// <summary>How long a burst takes to spread out.</summary>
    public const float BurstDuration = 0.3f;

    /// <summary>The Blizzard hurts what is in it this often.</summary>
    public const float BlizzardTick = 0.5f;

    /// <summary>The first barrage of a run can go this soon.</summary>
    public const float FirstBarrage = 0.4f;

    private readonly Random _random;
    private readonly List<FrostBolt> _bolts = new();
    private readonly List<FrostBurst> _bursts = new();
    private readonly List<(float Delay, int Index, int Count, bool Comet)> _launches = new();
    private float _blizzardIn;

    public FrostBarrage(Random random) => _random = random;

    public IReadOnlyList<FrostBolt> Bolts => _bolts;

    public IReadOnlyList<FrostBurst> Bursts => _bursts;

    /// <summary>Seconds until the next barrage may go (it waits, at 0, for an enemy in range).</summary>
    public float BarrageIn { get; private set; } = FirstBarrage;

    /// <summary>Barrages cast this run.</summary>
    public int Barrages { get; private set; }

    /// <summary>Bolts still to leave the staff in the barrage under way.</summary>
    public int Queued => _launches.Count;

    /// <summary>
    /// One frame: a barrage when one is due and an enemy is in range (held, bolts still to leave included, while <paramref name="canCast"/> is false - a stun), bolts
    /// leaving the staff at <paramref name="staff"/> one by one, the bolts in flight, and the Blizzard around <paramref name="feet"/>. <paramref name="aimFlat"/> is
    /// where the camera faces: enemies in front are aimed at first. Every hit dealt goes on <paramref name="hits"/>.
    /// </summary>
    public void Update(float deltaSeconds, Vector3D<float> staff, Vector3D<float> feet, Vector3D<float> aimFlat, MageStats stats, EnemyField enemies,
        Func<float, float, float?> groundAt, bool canCast, List<FrostHit> hits)
    {
        if (canCast)
        {
            BarrageIn -= deltaSeconds;
            if (BarrageIn <= 0f && _launches.Count == 0)
            {
                if (enemies.Within(feet, stats.TargetRange).Count > 0)
                {
                    BeginBarrage(stats);
                }
                else
                {
                    BarrageIn = 0f;   // ready, and waiting for something to aim at
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
                Launch(launch.Index, launch.Count, launch.Comet, staff, aimFlat, stats, enemies);
            }
        }

        MoveBolts(deltaSeconds, stats, enemies, groundAt, hits);

        foreach (var burst in _bursts)
        {
            burst.Age += deltaSeconds;
        }

        _bursts.RemoveAll(b => b.Age >= BurstDuration);
        UpdateBlizzard(deltaSeconds, feet, stats, enemies, hits);
    }

    /// <summary>Lines up a barrage: its bolts a moment apart, and on every 4th (with Comet) the comet last.</summary>
    public void BeginBarrage(MageStats stats)
    {
        Barrages++;
        BarrageIn = stats.BarrageInterval;
        int count = stats.Projectiles;
        for (int i = 0; i < count; i++)
        {
            _launches.Add((i * MageStats.BoltStagger, i, count, false));
        }

        if (stats.Tree.Comet && Barrages % MageStats.CometEvery == 0)
        {
            _launches.Add((count * MageStats.BoltStagger + 0.15f, 0, 1, true));
        }
    }

    /// <summary>
    /// A burst of frost at <paramref name="centre"/>: <paramref name="damage"/> to every enemy within <paramref name="radius"/> but <paramref name="spare"/>, chilling
    /// each. Frost Blast, a comet's blast and a Shattering Ward all go through here.
    /// </summary>
    public void Burst(Vector3D<float> centre, float radius, float damage, MageStats stats, EnemyField enemies, List<FrostHit> hits, FrostSource source,
        Enemy? spare = null)
    {
        _bursts.Add(new FrostBurst { Centre = centre, Radius = radius });
        foreach (var enemy in enemies.Within(centre, radius))
        {
            if (enemy != spare)
            {
                Frost(enemy, damage, crit: false, source, stats, enemies, hits, out _);
            }
        }
    }

    /// <summary>Everything out of the air (a restart), and the clocks back to the start.</summary>
    public void Reset()
    {
        _bolts.Clear();
        _bursts.Clear();
        _launches.Clear();
        BarrageIn = FirstBarrage;
        Barrages = 0;
        _blizzardIn = 0f;
    }

    /// <summary>
    /// Sends bolt <paramref name="index"/> of <paramref name="count"/> off the staff: aimed at the best target in range (in front first, nearest first) that the bolts
    /// already flying won't kill, leaving on its own slant of the fan so the volley spreads before it closes in. The comet goes for the toughest.
    /// </summary>
    private void Launch(int index, int count, bool comet, Vector3D<float> staff, Vector3D<float> aimFlat, MageStats stats, EnemyField enemies)
    {
        var inRange = enemies.Within(staff, stats.TargetRange);
        Enemy? target;
        if (comet)
        {
            target = inRange.OrderByDescending(e => e.Health).FirstOrDefault();
        }
        else
        {
            // The best target not already doomed by the bolts on their way to it - so a volley kills what it hits and then spreads, rather than grazing every
            // enemy and killing none. Once every enemy in range is spoken for, the bolts go round them again.
            var ranked = inRange.OrderBy(e => Rank(e, staff, aimFlat)).ToList();
            target = ranked.FirstOrDefault(e => e.Health > Incoming(e)) ?? (ranked.Count > 0 ? ranked[index % ranked.Count] : null);
        }

        var toward = target is null ? aimFlat : Geometry.FlatDirection(staff, target.Position, out _);
        if (toward == Vector3D<float>.Zero)
        {
            toward = aimFlat;
        }

        float slant = count > 1 ? (index / (float)(count - 1) * 2f - 1f) * FanDegrees * MathF.PI / 180f : 0f;
        float cos = MathF.Cos(slant), sin = MathF.Sin(slant);
        var fanned = new Vector3D<float>(toward.X * cos + toward.Z * sin, 0f, -toward.X * sin + toward.Z * cos);
        var heading = Vector3D.Normalize(fanned + new Vector3D<float>(0f, comet ? 0.8f : Lift, 0f));

        float speed = stats.BoltSpeed * (comet ? 0.8f : 1f);
        _bolts.Add(new FrostBolt
        {
            Position = staff,
            Heading = heading,
            Speed = speed,
            FlightLeft = stats.Range / speed,
            Damage = stats.BoltDamage * (comet ? MageStats.CometDamage : 1f),
            PierceLeft = comet ? 0 : stats.Pierce,
            Target = target,
            Scale = comet ? 2.5f : 1f,
            IsComet = comet,
        });
    }

    /// <summary>The damage the bolts in flight toward <paramref name="enemy"/> will do when they land (before crits and chill).</summary>
    private float Incoming(Enemy enemy)
    {
        float total = 0f;
        foreach (var bolt in _bolts)
        {
            if (bolt.Target == enemy)
            {
                total += bolt.Damage;
            }
        }

        return total;
    }

    /// <summary>How good a target an enemy is: nearer is better, and one behind the Mage counts as half again as far.</summary>
    private static float Rank(Enemy enemy, Vector3D<float> staff, Vector3D<float> aimFlat)
    {
        var toward = Geometry.FlatDirection(staff, enemy.Position, out float distance);
        return Vector3D.Dot(toward, aimFlat) >= 0f ? distance : distance * 1.5f;
    }

    private void MoveBolts(float deltaSeconds, MageStats stats, EnemyField enemies, Func<float, float, float?> groundAt, List<FrostHit> hits)
    {
        var spawned = new List<FrostBolt>();
        for (int i = _bolts.Count - 1; i >= 0; i--)
        {
            var bolt = _bolts[i];
            bolt.Age += deltaSeconds;
            bolt.FlightLeft -= deltaSeconds;
            if (bolt.FlightLeft <= 0f)
            {
                _bolts.RemoveAt(i);
                continue;
            }

            if (bolt.Target is not { IsAlive: true } || bolt.AlreadyHit.Contains(bolt.Target))
            {
                bolt.Target = Nearest(enemies, bolt.Position, RetargetRange, bolt.AlreadyHit);
            }

            if (bolt.Target is { } target)
            {
                var aimAt = target.Position + new Vector3D<float>(0f, MathF.Min(target.Kind.Height * 0.5f, 1.2f), 0f);
                var want = aimAt - bolt.Position;
                if (want.LengthSquared > 1e-6f)
                {
                    // Close in, it goes straight at the target: a bolt that only turned would circle a target it came at from the side.
                    float turn = want.Length < CloseIn ? 1f : MathF.Min(1f, (TurnRate + TurnGrowth * bolt.Age) * deltaSeconds);
                    var blended = bolt.Heading + (Vector3D.Normalize(want) - bolt.Heading) * turn;
                    if (blended.LengthSquared > 1e-6f)
                    {
                        bolt.Heading = Vector3D.Normalize(blended);
                    }
                }
            }

            var from = bolt.Position;
            var to = from + bolt.Heading * bolt.Speed * deltaSeconds;
            bool gone = false;
            while (enemies.FirstHit(from, to, BoltRadius * bolt.Scale, out float along, bolt.AlreadyHit) is { } enemy)
            {
                var at = from + (to - from) * along;
                bolt.AlreadyHit.Add(enemy);
                Strike(bolt, enemy, at, stats, enemies, hits, spawned);
                if (bolt.PierceLeft <= 0)
                {
                    gone = true;
                    break;
                }

                bolt.PierceLeft--;
                bolt.Target = null;
            }

            if (!gone && groundAt(to.X, to.Z) is { } ground && to.Y < ground)
            {
                gone = true;   // into the ground: it shatters there
            }

            if (gone)
            {
                _bolts.RemoveAt(i);
            }
            else
            {
                bolt.Position = to;
            }
        }

        _bolts.AddRange(spawned);
    }

    /// <summary>
    /// A bolt's hit: its damage (more on a chilled enemy, more again on a frozen one with Shatter, a crit roll), the chill and a chance to freeze, the Frost Blast or
    /// the comet's blast around it, and - if it killed - Splitting Ice's two halves.
    /// </summary>
    private void Strike(FrostBolt bolt, Enemy enemy, Vector3D<float> at, MageStats stats, EnemyField enemies, List<FrostHit> hits, List<FrostBolt> spawned)
    {
        bool frozen = enemy.IsFrozen;
        float damage = bolt.Damage
            * (enemy.IsChilled || frozen ? stats.ChilledMultiplier : 1f)
            * (frozen && stats.Tree.Shatter ? 1f + MageStats.ShatterBonus : 1f);
        bool crit = _random.NextDouble() < stats.CritChance;
        bool killed = Frost(enemy, crit ? damage * stats.CritMultiplier : damage, crit, FrostSource.Bolt, stats, enemies, hits, out float dealt);

        if (bolt.IsComet)
        {
            Burst(enemy.Position, MageStats.CometRadius * stats.AreaScale, dealt * MageStats.CometBlast, stats, enemies, hits, FrostSource.Blast, spare: enemy);
        }
        else if (stats.Tree.FrostBlast)
        {
            Burst(enemy.Position, stats.BlastRadius, dealt * stats.BlastShare, stats, enemies, hits, FrostSource.Blast, spare: enemy);
        }

        if (killed && stats.Tree.SplittingIce && !bolt.IsSplit && !bolt.IsComet)
        {
            var skip = new HashSet<Enemy>(bolt.AlreadyHit);
            var side = Vector3D.Cross(bolt.Heading, Vector3D<float>.UnitY);
            side = side.LengthSquared > 1e-6f ? Vector3D.Normalize(side) : Vector3D<float>.UnitX;
            for (int i = 0; i < MageStats.SplitCount; i++)
            {
                var next = Nearest(enemies, at, MageStats.SplitRange, skip);
                if (next is not null)
                {
                    skip.Add(next);
                }

                var half = new FrostBolt
                {
                    Position = at,
                    Heading = Vector3D.Normalize(bolt.Heading + side * (i % 2 == 0 ? 0.8f : -0.8f) + new Vector3D<float>(0f, 0.4f, 0f)),
                    Speed = bolt.Speed,
                    FlightLeft = MageStats.SplitRange * 1.5f / bolt.Speed,
                    Damage = bolt.Damage * MageStats.SplitDamage,
                    Target = next,
                    Scale = 0.65f,
                    IsSplit = true,
                };
                half.AlreadyHit.UnionWith(bolt.AlreadyHit);
                spawned.Add(half);
            }
        }
    }

    /// <summary>
    /// Frost damage to one enemy (harder on an elite or a boss): it chills, and with Deep Freeze a hit on an enemy already chilled may freeze it (never a boss, half as
    /// long for an elite). True if it killed; <paramref name="dealt"/> is the damage done. Nothing happens to one already dead.
    /// </summary>
    private bool Frost(Enemy enemy, float amount, bool crit, FrostSource source, MageStats stats, EnemyField enemies, List<FrostHit> hits, out float dealt)
    {
        dealt = 0f;
        if (!enemy.IsAlive || amount <= 0f)
        {
            return false;
        }

        if (enemy.Kind.Tier != EnemyTier.Fodder)
        {
            amount *= stats.EliteMultiplier;
        }

        dealt = amount;

        bool wasChilled = enemy.IsChilled;
        bool killed = enemies.Damage(enemy, amount);
        hits.Add(new FrostHit(enemy, enemy.Position + new Vector3D<float>(0f, enemy.Kind.Height * 0.7f, 0f), amount, killed, crit, source));
        if (killed)
        {
            return true;
        }

        enemy.Chill(stats.ChillDuration, stats.Chill);
        if (wasChilled && enemy.Kind.Tier != EnemyTier.Boss && stats.FreezeChance > 0f && _random.NextDouble() < stats.FreezeChance)
        {
            enemy.Freeze(stats.FreezeDuration * (enemy.Kind.Tier == EnemyTier.Elite ? MageStats.EliteFreezeShare : 1f));
        }

        return false;
    }

    /// <summary>The Blizzard, once taken: every so often, frost to every enemy within its reach of the Mage.</summary>
    private void UpdateBlizzard(float deltaSeconds, Vector3D<float> feet, MageStats stats, EnemyField enemies, List<FrostHit> hits)
    {
        if (!stats.Tree.Blizzard)
        {
            _blizzardIn = 0f;
            return;
        }

        _blizzardIn -= deltaSeconds;
        if (_blizzardIn > 0f)
        {
            return;
        }

        _blizzardIn = MathF.Max(0f, _blizzardIn + BlizzardTick);
        float damage = stats.BoltDamage * MageStats.BlizzardShare * BlizzardTick;
        foreach (var enemy in enemies.Within(feet, MageStats.BlizzardRadius * stats.AreaScale))
        {
            Frost(enemy, damage, crit: false, FrostSource.Blizzard, stats, enemies, hits, out _);
        }
    }

    /// <summary>The live enemy nearest <paramref name="point"/> within <paramref name="range"/>, not in <paramref name="skip"/>, or null.</summary>
    private static Enemy? Nearest(EnemyField enemies, Vector3D<float> point, float range, IReadOnlySet<Enemy> skip)
    {
        Enemy? best = null;
        float bestDistance = float.MaxValue;
        foreach (var enemy in enemies.Within(point, range))
        {
            if (skip.Contains(enemy))
            {
                continue;
            }

            Geometry.FlatDirection(point, enemy.Position, out float distance);
            if (distance < bestDistance)
            {
                best = enemy;
                bestDistance = distance;
            }
        }

        return best;
    }
}
