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

    /// <summary>Which way it points - kept when it sticks, so it stays at the angle it landed.</summary>
    public Vector3D<float> Heading { get; set; }
}

/// <summary>A hit an arrow landed this frame.</summary>
internal readonly record struct ArrowHit(Enemy Enemy, Vector3D<float> Position, float Damage, bool Killed);

/// <summary>
/// The Ranger's arrows in flight: they fly straight (no drop, for now), hit the first enemy in their path, and stick in the ground briefly when they miss. Pure simulation -
/// <see cref="RangerBow"/> fires them and draws them.
/// </summary>
internal sealed class RangerArrows
{
    /// <summary>How fat an arrow is for hitting, metres - a little generous so near misses still count.</summary>
    public const float HitRadius = 0.12f;

    /// <summary>How long a missed arrow stays stuck in the ground.</summary>
    public const float StuckLifetime = 1.5f;

    private readonly List<Arrow> _arrows = new();

    public IReadOnlyList<Arrow> Arrows => _arrows;

    public Arrow Fire(Vector3D<float> origin, Vector3D<float> direction, float speed, float range, float damage)
    {
        var heading = Vector3D.Normalize(direction);
        var arrow = new Arrow { Position = origin, Velocity = heading * speed, FlightLeft = range / speed, Damage = damage, Heading = heading };
        _arrows.Add(arrow);
        return arrow;
    }

    /// <summary>
    /// Moves every arrow one frame, hitting enemies in <paramref name="enemies"/> (and hurting them) and sticking in the ground. Returns the hits this frame and, in
    /// <paramref name="gone"/>, the arrows that are finished (hit something, flew out of range, or have been stuck long enough).
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

            if (enemies.FirstHit(from, to, HitRadius, out float along) is { } enemy)
            {
                var at = from + (to - from) * along;
                bool killed = enemies.Damage(enemy, arrow.Damage);
                hits.Add(new ArrowHit(enemy, at, arrow.Damage, killed));
                gone.Add(arrow);
                continue;
            }

            arrow.Position = to;
            arrow.FlightLeft -= deltaSeconds;

            if (groundAt(to.X, to.Z) is { } ground && to.Y <= ground)
            {
                arrow.Stuck = true;
                arrow.Position = new Vector3D<float>(to.X, ground, to.Z) - arrow.Heading * 0.15f;   // buried a little, not balanced on its tip
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
}
