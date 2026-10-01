using ArenaMaster.Game.Combat;
using Silk.NET.Maths;

namespace ArenaMaster.Game.Ranger;

/// <summary>
/// The Ranger's auto-aim: which enemy the bow shoots at, and where to aim so the arrow meets it. The nearest enemy in range that the bow can see (a crate only
/// when there is no enemy), kept until it dies, leaves range or goes out of sight, or another comes much nearer - so the aim doesn't flick between two at
/// much the same distance. The shot is led: where the target will be when the arrow gets there, from how it has been moving. Pure, so it can be tested.
/// </summary>
internal sealed class BowSight
{
    /// <summary>Another enemy takes over from the target only when it is nearer than this share of the target's distance.</summary>
    public const float Stickiness = 0.75f;

    /// <summary>How many of the nearest enemies are checked for a clear line before giving up (each check is a ray against the ground).</summary>
    private const int SightChecks = 8;

    /// <summary>How quickly the target's measured speed settles (fraction per second, roughly): high enough to follow a turn, low enough to ride out a stumble.</summary>
    private const float LeadSmoothing = 8f;

    /// <summary>The fastest the shot leads for (metres per second): a leap covers ground faster, but aiming far ahead of one only misses.</summary>
    private const float MaxLeadSpeed = 12f;

    private readonly List<(Enemy Enemy, float Distance)> _candidates = new();
    private Vector3D<float> _lastSeen;

    /// <summary>What the bow is shooting at, or null with nothing in range and sight.</summary>
    public Enemy? Target { get; private set; }

    /// <summary>The target's flat speed as measured over the last frames (zero with no target, or one just picked).</summary>
    public Vector3D<float> TargetVelocity { get; private set; }

    /// <summary>
    /// Picks the target for this frame from <paramref name="enemies"/>, as seen from <paramref name="from"/> (the bow). <paramref name="clear"/> says whether
    /// the line between two points is open (no ground in the way); null takes every line as open.
    /// </summary>
    public void Update(IReadOnlyList<Enemy> enemies, Vector3D<float> from, float range, float deltaSeconds, Func<Vector3D<float>, Vector3D<float>, bool>? clear = null)
    {
        var picked = Pick(enemies, from, range, clear);
        if (picked is null || picked != Target)
        {
            TargetVelocity = Vector3D<float>.Zero;
        }
        else if (deltaSeconds > 0f)
        {
            var moved = picked.Position - _lastSeen;
            var seen = new Vector3D<float>(moved.X, 0f, moved.Z) / deltaSeconds;
            if (seen.Length > MaxLeadSpeed)
            {
                seen = Vector3D.Normalize(seen) * MaxLeadSpeed;
            }

            TargetVelocity += (seen - TargetVelocity) * (1f - MathF.Exp(-LeadSmoothing * deltaSeconds));
        }

        Target = picked;
        if (picked is not null)
        {
            _lastSeen = picked.Position;
        }
    }

    /// <summary>Forgets the target (a run ends).</summary>
    public void Clear()
    {
        Target = null;
        TargetVelocity = Vector3D<float>.Zero;
    }

    /// <summary>
    /// Where to aim from <paramref name="from"/> so an arrow at <paramref name="arrowSpeed"/> meets the target: the middle of its body, moved on by its speed for
    /// as long as the arrow takes to get there. Null with no target.
    /// </summary>
    public Vector3D<float>? AimPoint(Vector3D<float> from, float arrowSpeed)
    {
        if (Target is not { } target)
        {
            return null;
        }

        var point = Middle(target);
        if (arrowSpeed <= 0f)
        {
            return point;
        }

        for (int i = 0; i < 2; i++)   // the flight time from where it will be, not where it is: twice is plenty at these speeds
        {
            point = Middle(target) + TargetVelocity * (Vector3D.Distance(from, point) / arrowSpeed);
        }

        return point;
    }

    /// <summary>The middle of an enemy's body, where the arrows go.</summary>
    public static Vector3D<float> Middle(Enemy enemy) => enemy.Position + new Vector3D<float>(0f, MathF.Max(enemy.Kind.Radius, enemy.Kind.Height * 0.5f), 0f);

    private Enemy? Pick(IReadOnlyList<Enemy> enemies, Vector3D<float> from, float range, Func<Vector3D<float>, Vector3D<float>, bool>? clear)
    {
        _candidates.Clear();
        bool anyFoe = false;
        foreach (var enemy in enemies)
        {
            if (!enemy.IsAlive)
            {
                continue;
            }

            float distance = Vector3D.Distance(from, Middle(enemy));
            if (distance > range)
            {
                continue;
            }

            anyFoe |= !enemy.Kind.IsProp;
            _candidates.Add((enemy, distance));
        }

        if (anyFoe)
        {
            _candidates.RemoveAll(c => c.Enemy.Kind.IsProp);   // a crate only when there is nothing to fight
        }

        _candidates.Sort((a, b) => a.Distance.CompareTo(b.Distance));

        (Enemy Enemy, float Distance)? nearest = null;
        for (int i = 0; i < _candidates.Count && i < SightChecks; i++)
        {
            if (Sees(from, _candidates[i].Enemy, clear))
            {
                nearest = _candidates[i];
                break;
            }
        }

        if (Target is { } current && nearest?.Enemy != current)
        {
            int kept = _candidates.FindIndex(c => c.Enemy == current);
            if (kept >= 0 && (nearest is null || nearest.Value.Distance >= _candidates[kept].Distance * Stickiness) && Sees(from, current, clear))
            {
                return current;
            }
        }

        return nearest?.Enemy;
    }

    private static bool Sees(Vector3D<float> from, Enemy enemy, Func<Vector3D<float>, Vector3D<float>, bool>? clear) => clear?.Invoke(from, Middle(enemy)) ?? true;
}
