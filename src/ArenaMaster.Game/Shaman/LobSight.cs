using ArenaMaster.Game.Combat;
using Silk.NET.Maths;

namespace ArenaMaster.Game.Shaman;

/// <summary>
/// What the Shaman throws its lightning at, on its own: the nearest enemy within throwing range (a crate only when there is no enemy), kept until it dies, leaves
/// range, or another comes much nearer - and where to throw so the ball comes down on it: where it will be once the ball has flown, from how it has been moving.
/// No line of sight is needed, as the ball goes over. The Shaman's own, like everything under Shaman/. Pure, so it can be tested.
/// </summary>
internal sealed class LobSight
{
    /// <summary>Another enemy takes over only when it is nearer than this share of the target's distance.</summary>
    public const float Stickiness = 0.75f;

    /// <summary>How quickly the target's measured speed settles (fraction per second, roughly).</summary>
    private const float LeadSmoothing = 6f;

    /// <summary>The fastest the throw leads for (metres per second): past this it's a leap or a charge, and aiming far ahead of it only misses.</summary>
    private const float MaxLeadSpeed = 9f;

    private Vector3D<float> _lastSeen;

    /// <summary>What the Shaman is throwing at, or null with nothing in range.</summary>
    public Enemy? Target { get; private set; }

    /// <summary>The target's flat speed as measured over the last frames.</summary>
    public Vector3D<float> TargetVelocity { get; private set; }

    /// <summary>Picks the target for this frame: the nearest of <paramref name="enemies"/> within <paramref name="range"/> of <paramref name="feet"/>.</summary>
    public void Update(EnemyField enemies, Vector3D<float> feet, float range, float deltaSeconds)
    {
        var nearest = enemies.Nearest(feet, range);
        var picked = nearest;
        if (Target is { IsAlive: true } current && current != nearest && nearest is not null && current.Kind.IsProp == nearest.Kind.IsProp)
        {
            Geometry.FlatDirection(feet, current.Position, out float kept);
            Geometry.FlatDirection(feet, nearest.Position, out float closer);
            if (kept <= range + current.Kind.Radius && closer >= kept * Stickiness)
            {
                picked = current;   // no flicking between two at much the same distance
            }
        }

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

    /// <summary>Where to lob from <paramref name="hand"/> so the ball comes down on the target, moved on by its speed for the ball's flight. Null with no target.</summary>
    public Vector3D<float>? AimPoint(Vector3D<float> hand, ShamanStats stats)
    {
        if (Target is not { } target)
        {
            return null;
        }

        var point = target.Position;
        for (int i = 0; i < 2; i++)   // the flight to where it will be, not where it is
        {
            point = target.Position + TargetVelocity * RollingLightning.FlightTime(hand, point, stats);
        }

        return point;
    }
}
