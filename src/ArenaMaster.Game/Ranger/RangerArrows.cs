using ArenaMaster.Game.Combat;
using Silk.NET.Maths;

namespace ArenaMaster.Game.Ranger;

/// <summary>One of the Ranger's arrows: flying, or stuck in the ground for a moment after a miss.</summary>
internal sealed class Arrow
{
    public Vector3D<float> Position { get; set; }

    public Vector3D<float> Velocity { get; set; }

    /// <summary>Seconds of flight left before it drops out of range.</summary>
    public float FlightLeft { get; set; }

    public bool Stuck { get; set; }

    public float StuckFor { get; set; }

    public float Damage { get; set; }

    /// <summary>How many more enemies it can pass through before it stops.</summary>
    public int PierceLeft { get; set; }

    public float CritChance { get; set; }

    /// <summary>How many times normal damage a critical hit does.</summary>
    public float CritMultiplier { get; set; } = RangerStats.BaseCritMultiplier;

    /// <summary>How many more times it can jump on to another enemy when it would otherwise stop.</summary>
    public int ChainsLeft { get; set; }

    /// <summary>How far it looks for the next enemy when it chains.</summary>
    public float ChainRange { get; set; }

    /// <summary>How many times it has chained so far (0 for a fresh arrow).</summary>
    public int ChainIndex { get; set; }

    public HitRules Rules { get; set; }

    /// <summary>The enemies it has already hit, so a piercing or chaining arrow never hits the same one twice.</summary>
    public HashSet<Enemy> AlreadyHit { get; } = new();

    /// <summary>Which way it points - kept when it sticks, so it stays at the angle it landed.</summary>
    public Vector3D<float> Heading { get; set; }

    /// <summary>How big it is drawn (splinters are small).</summary>
    public float Scale { get; set; } = 1f;

    /// <summary>Whether it has already split (Fork) - or is itself one of the halves, which never split again.</summary>
    public bool Forked { get; set; }

    /// <summary>A splinter from a kill (Storm of Splinters). Splinters never burst into more splinters.</summary>
    public bool IsSplinter { get; set; }
}

/// <summary>A hit an arrow landed this frame.</summary>
internal readonly record struct ArrowHit(Enemy Enemy, Vector3D<float> Position, float Damage, bool Killed, bool Crit);

/// <summary>
/// The Ranger's arrows in flight: they fly straight (no drop, for now), hit enemies in their path (passing through as many as their pierce allows, then jumping on
/// to another enemy as many times as they can chain), and stick in the ground briefly when they miss. Pure simulation - <see cref="RangerBow"/> fires them and draws them.
/// </summary>
internal sealed class RangerArrows
{
    /// <summary>How fat an arrow is for hitting, metres - a little generous so near misses still count.</summary>
    public const float HitRadius = 0.12f;

    /// <summary>How long a missed arrow stays stuck in the ground.</summary>
    public const float StuckLifetime = 1.5f;

    /// <summary>Below this share of its health, a non-boss enemy can be executed (see <see cref="HitRules.ExecuteChance"/>).</summary>
    public const float ExecuteBelow = 0.2f;

    /// <summary>Above this share of its health, an enemy counts as healthy (see <see cref="HitRules.HealthyDamage"/>).</summary>
    public const float HealthyAbove = 0.8f;

    /// <summary>Fork: the two halves fly this many degrees either side of the arrow's heading (20 apart).</summary>
    public const float ForkHalfAngleDegrees = 10f;

    /// <summary>Storm of Splinters: how many, how hard (a share of the killing arrow's damage), how fast (a share of its speed), how far, how big.</summary>
    public const int SplinterCount = 4;
    public const float SplinterDamage = 0.5f;
    public const float SplinterSpeed = 0.8f;
    public const float SplinterRange = 12f;
    public const float SplinterScale = 0.6f;

    private enum Flight
    {
        Flying,
        Stopped,
        Chained,
    }

    private readonly List<Arrow> _arrows = new();
    private readonly List<Arrow> _spawned = new();
    private readonly Random _random;

    public RangerArrows(Random random) => _random = random;

    public IReadOnlyList<Arrow> Arrows => _arrows;

    public Arrow Fire(Vector3D<float> origin, Vector3D<float> direction, float speed, float range, float damage, int pierce = 0, float critChance = 0f,
        float critMultiplier = RangerStats.BaseCritMultiplier, int chains = 0, float chainRange = RangerStats.BaseChainRange, HitRules rules = default)
    {
        var heading = Vector3D.Normalize(direction);
        var arrow = new Arrow
        {
            Position = origin,
            Velocity = heading * speed,
            FlightLeft = range / speed,
            Damage = damage,
            PierceLeft = pierce,
            CritChance = critChance,
            CritMultiplier = critMultiplier,
            ChainsLeft = chains,
            ChainRange = chainRange,
            Rules = rules,
            Heading = heading,
        };
        _arrows.Add(arrow);
        return arrow;
    }

    /// <summary>
    /// Moves every arrow one frame, hitting enemies in <paramref name="enemies"/> (and hurting them) and sticking in the ground. Returns the hits this frame and, in
    /// <paramref name="gone"/>, the arrows that are finished (stopped in an enemy, flew out of range, or have been stuck long enough).
    /// </summary>
    public List<ArrowHit> Update(float deltaSeconds, EnemyField enemies, Func<float, float, float?> groundAt, List<Arrow> gone)
    {
        var hits = new List<ArrowHit>();

        foreach (var arrow in _arrows)
        {
            if (arrow.Stuck)
            {
                arrow.StuckFor += deltaSeconds;
                if (arrow.StuckFor >= StuckLifetime)
                {
                    gone.Add(arrow);
                }

                continue;
            }

            var from = arrow.Position;
            var to = from + arrow.Velocity * deltaSeconds;

            switch (HitAlong(arrow, from, to, enemies, hits))
            {
                case Flight.Stopped:
                    gone.Add(arrow);   // stopped in the last enemy it could reach
                    continue;
                case Flight.Chained:
                    continue;          // already turned toward its next target, from where it hit
            }

            arrow.Position = to;
            arrow.FlightLeft -= deltaSeconds;

            if (groundAt(to.X, to.Z) is { } ground && to.Y <= ground)
            {
                arrow.Stuck = true;
                arrow.Position = new Vector3D<float>(to.X, ground, to.Z) - arrow.Heading * 0.15f;   // tip in the ground, most of the shaft showing
            }
            else if (arrow.FlightLeft <= 0f || groundAt(to.X, to.Z) is null)
            {
                gone.Add(arrow);
            }
        }

        _arrows.RemoveAll(gone.Contains);
        _arrows.AddRange(_spawned);   // forks and splinters born this frame start flying next frame
        _spawned.Clear();
        return hits;
    }

    public List<Arrow> Clear()
    {
        var all = _arrows.ToList();
        _arrows.Clear();
        _spawned.Clear();
        return all;
    }

    /// <summary>What one arrow's hit on <paramref name="enemy"/> is worth before a crit: more on elites, on healthy enemies, and after each chain.</summary>
    public static float DamageAgainst(Arrow arrow, Enemy enemy)
    {
        var rules = arrow.Rules;
        float damage = arrow.Damage;
        if (enemy.Kind.Tier != EnemyTier.Fodder)
        {
            damage *= 1f + rules.EliteDamage;
        }

        if (enemy.Health > enemy.MaxHealth * HealthyAbove)
        {
            damage *= 1f + rules.HealthyDamage;
        }

        if (arrow.ChainIndex > 0)
        {
            damage *= (1f + rules.ChainedDamage) * MathF.Pow(1f + rules.CascadeDamage, arrow.ChainIndex - 1);
        }

        return damage;
    }

    /// <summary>Hits every enemy on this frame's stretch of flight, in order, until the arrow runs out of pierce - then chains on if it can.</summary>
    private Flight HitAlong(Arrow arrow, Vector3D<float> from, Vector3D<float> to, EnemyField enemies, List<ArrowHit> hits)
    {
        while (enemies.FirstHit(from, to, HitRadius, out float along, arrow.AlreadyHit) is { } enemy)
        {
            var at = from + (to - from) * along;
            arrow.AlreadyHit.Add(enemy);
            Strike(arrow, enemy, at, enemies, hits);

            if (arrow.Rules.Fork && !arrow.Forked)
            {
                arrow.Forked = true;
                SpawnForks(arrow, at);
            }

            if (arrow.PierceLeft > 0)
            {
                arrow.PierceLeft--;
                continue;
            }

            return arrow.ChainsLeft > 0 && TryChain(arrow, at, enemies) ? Flight.Chained : Flight.Stopped;
        }

        return Flight.Flying;
    }

    private void Strike(Arrow arrow, Enemy enemy, Vector3D<float> at, EnemyField enemies, List<ArrowHit> hits)
    {
        bool untouched = enemy.Health >= enemy.MaxHealth;
        bool crit = (arrow.Rules.FirstHitCrits && untouched) || (arrow.CritChance > 0f && _random.NextDouble() < arrow.CritChance);
        float damage = DamageAgainst(arrow, enemy) * (crit ? arrow.CritMultiplier : 1f);
        bool killed = enemies.Damage(enemy, damage);

        if (!killed && enemy.Kind.Tier != EnemyTier.Boss && enemy.Health < enemy.MaxHealth * ExecuteBelow
            && arrow.Rules.ExecuteChance > 0f && _random.NextDouble() < arrow.Rules.ExecuteChance)
        {
            damage += enemy.Health;
            killed = enemies.Damage(enemy, enemy.Health);
        }

        hits.Add(new ArrowHit(enemy, at, damage, killed, crit));

        if (killed && arrow.Rules.Splinters && !arrow.IsSplinter)
        {
            SpawnSplinters(arrow, at);
        }
    }

    /// <summary>Fork: two halves of the arrow fly on from where it hit, either side of its heading. They carry what the arrow had left and never split again.</summary>
    private void SpawnForks(Arrow arrow, Vector3D<float> at)
    {
        float speed = arrow.Velocity.Length;
        foreach (float degrees in new[] { -ForkHalfAngleDegrees, ForkHalfAngleDegrees })
        {
            var heading = Turn(arrow.Heading, degrees * MathF.PI / 180f);
            var half = Copy(arrow, at, heading, speed);
            half.FlightLeft = arrow.FlightLeft;
            _spawned.Add(half);
        }
    }

    /// <summary>Storm of Splinters: small arrows burst out flat and evenly spread from a kill, each with half the killing arrow's damage. They don't pierce, chain, fork or splinter.</summary>
    private void SpawnSplinters(Arrow arrow, Vector3D<float> at)
    {
        float speed = arrow.Velocity.Length * SplinterSpeed;
        float start = (float)_random.NextDouble() * MathF.Tau;
        for (int i = 0; i < SplinterCount; i++)
        {
            float angle = start + MathF.Tau * i / SplinterCount;
            var splinter = Copy(arrow, at, new Vector3D<float>(MathF.Sin(angle), 0f, MathF.Cos(angle)), speed);
            splinter.Damage = arrow.Damage * SplinterDamage;
            splinter.FlightLeft = SplinterRange / speed;
            splinter.PierceLeft = 0;
            splinter.ChainsLeft = 0;
            splinter.IsSplinter = true;
            splinter.Scale = SplinterScale;
            _spawned.Add(splinter);
        }
    }

    /// <summary>A new arrow like <paramref name="arrow"/> (its damage, crits, what it has left, what it has hit), leaving <paramref name="at"/> along <paramref name="heading"/>.</summary>
    private static Arrow Copy(Arrow arrow, Vector3D<float> at, Vector3D<float> heading, float speed)
    {
        var copy = new Arrow
        {
            Position = at,
            Heading = heading,
            Velocity = heading * speed,
            Damage = arrow.Damage,
            PierceLeft = arrow.PierceLeft,
            CritChance = arrow.CritChance,
            CritMultiplier = arrow.CritMultiplier,
            ChainsLeft = arrow.ChainsLeft,
            ChainRange = arrow.ChainRange,
            ChainIndex = arrow.ChainIndex,
            Rules = arrow.Rules,
            Forked = true,
            IsSplinter = arrow.IsSplinter,
            Scale = arrow.Scale,
        };
        copy.AlreadyHit.UnionWith(arrow.AlreadyHit);
        return copy;
    }

    /// <summary><paramref name="heading"/> turned about the vertical by <paramref name="radians"/> (the same turn a model's yaw makes).</summary>
    public static Vector3D<float> Turn(Vector3D<float> heading, float radians)
    {
        float cos = MathF.Cos(radians), sin = MathF.Sin(radians);
        return Vector3D.Normalize(new Vector3D<float>(heading.X * cos + heading.Z * sin, heading.Y, -heading.X * sin + heading.Z * cos));
    }

    /// <summary>Turns the arrow, from where it hit, toward the nearest live enemy it hasn't hit yet within its chain range. False if there is none.</summary>
    private static bool TryChain(Arrow arrow, Vector3D<float> at, EnemyField enemies)
    {
        Enemy? next = null;
        float best = arrow.ChainRange;
        foreach (var enemy in enemies.Enemies)
        {
            if (!enemy.IsAlive || arrow.AlreadyHit.Contains(enemy))
            {
                continue;
            }

            float distance = Vector3D.Distance(at, Chest(enemy));
            if (distance < best)
            {
                best = distance;
                next = enemy;
            }
        }

        if (next is null)
        {
            return false;
        }

        var heading = Vector3D.Normalize(Chest(next) - at);
        float speed = arrow.Velocity.Length;
        arrow.Position = at;
        arrow.Heading = heading;
        arrow.Velocity = heading * speed;
        arrow.FlightLeft = MathF.Max(arrow.FlightLeft, (arrow.ChainRange + 2f) / speed);
        arrow.ChainsLeft--;
        arrow.ChainIndex++;
        return true;
    }

    private static Vector3D<float> Chest(Enemy enemy) => enemy.Position + new Vector3D<float>(0f, enemy.Kind.Height * 0.55f, 0f);
}
