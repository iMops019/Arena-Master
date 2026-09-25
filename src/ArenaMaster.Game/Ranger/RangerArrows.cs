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

    /// <summary>The enemies it has already gone through, so a piercing arrow never hits the same one twice.</summary>
    public HashSet<Enemy> AlreadyHit { get; } = new();

    /// <summary>Which way it points - kept when it sticks, so it stays at the angle it landed.</summary>
    public Vector3D<float> Heading { get; set; }
}

/// <summary>A hit an arrow landed this frame.</summary>
internal readonly record struct ArrowHit(Enemy Enemy, Vector3D<float> Position, float Damage, bool Killed, bool Crit);

/// <summary>
/// The Ranger's arrows in flight: they fly straight (no drop, for now), hit enemies in their path (passing through as many as their pierce allows), and stick in
/// the ground briefly when they miss. Pure simulation - <see cref="RangerBow"/> fires them and draws them.
/// </summary>
internal sealed class RangerArrows
{
    /// <summary>How fat an arrow is for hitting, metres - a little generous so near misses still count.</summary>
    public const float HitRadius = 0.12f;

    /// <summary>How long a missed arrow stays stuck in the ground.</summary>
    public const float StuckLifetime = 1.5f;

    private readonly List<Arrow> _arrows = new();
    private readonly Random _random;

    public RangerArrows(Random random) => _random = random;

    public IReadOnlyList<Arrow> Arrows => _arrows;

    public Arrow Fire(Vector3D<float> origin, Vector3D<float> direction, float speed, float range, float damage, int pierce = 0, float critChance = 0f)
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

            if (HitAlong(arrow, from, to, enemies, hits))
            {
                gone.Add(arrow);   // stopped in the last enemy it could reach
                continue;
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
        return hits;
    }

    public List<Arrow> Clear()
    {
        var all = _arrows.ToList();
        _arrows.Clear();
        return all;
    }

    /// <summary>Hits every enemy on this frame's stretch of flight, in order, until the arrow runs out of pierce. True if it stopped in one.</summary>
    private bool HitAlong(Arrow arrow, Vector3D<float> from, Vector3D<float> to, EnemyField enemies, List<ArrowHit> hits)
    {
        while (enemies.FirstHit(from, to, HitRadius, out float along, arrow.AlreadyHit) is { } enemy)
        {
            arrow.AlreadyHit.Add(enemy);
            bool crit = arrow.CritChance > 0f && _random.NextDouble() < arrow.CritChance;
            float damage = crit ? arrow.Damage * RangerStats.CritMultiplier : arrow.Damage;
            bool killed = enemies.Damage(enemy, damage);
            hits.Add(new ArrowHit(enemy, from + (to - from) * along, damage, killed, crit));

            if (arrow.PierceLeft <= 0)
            {
                return true;
            }

            arrow.PierceLeft--;
        }

        return false;
    }
}
