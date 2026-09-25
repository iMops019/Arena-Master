using ArenaMaster.Game.Combat;
using CEngine.Core;
using Silk.NET.Maths;

namespace ArenaMaster.Game.Ranger;

/// <summary>
/// The Ranger's basic attack: a bow that fires on its own, over and over, at whatever the crosshair is on (Megabonk-style - the player aims with the camera, never clicks).
/// Arrows leave from the Ranger's bow and converge on the crosshair's target, so what is under the crosshair is what gets hit.
/// </summary>
internal sealed class RangerBow
{
    public const string ArrowModel = "arrow_placeholder.glb";

    /// <summary>Where on the Ranger the arrow leaves from: this high above the feet, this far ahead.</summary>
    private const float ReleaseHeight = 1.3f;
    private const float ReleaseForward = 0.5f;

    private readonly RangerArrows _arrows;
    private readonly Dictionary<Arrow, int> _props = new();
    private float _cooldown;

    public RangerBow(Random random) => _arrows = new RangerArrows(random);

    public void Update(EngineWindow window, float deltaSeconds, RangerStats stats, EnemyField enemies, DamageNumbers numbers, Func<float, float, float?> groundAt)
    {
        _cooldown -= deltaSeconds;
        if (_cooldown <= 0f && window.Camera is { } camera && window.Terrain is { } terrain)
        {
            _cooldown += stats.FireInterval;
            if (_cooldown < 0f)
            {
                _cooldown = 0f;   // after a pause, don't fire a burst to catch up
            }

            var aimFlat = Flat(camera.Front);
            var origin = window.PlayerFeet + new Vector3D<float>(0f, ReleaseHeight, 0f) + aimFlat * ReleaseForward;
            var aim = AimDirection(origin, CrosshairTarget(camera, terrain, enemies, stats.Range), camera.Front, aimFlat);
            foreach (var direction in Fan(aim, stats.ArrowsPerShot, RangerStats.SplitSpreadDegrees))
            {
                _arrows.Fire(origin, direction, stats.ArrowSpeed, stats.Range, stats.Damage, stats.Pierce, stats.CritChance);
            }
        }

        var gone = new List<Arrow>();
        foreach (var hit in _arrows.Update(deltaSeconds, enemies, groundAt, gone))
        {
            numbers.Add(hit.Position, hit.Damage, hit.Killed, hit.Crit);
        }

        foreach (var arrow in gone)
        {
            RemoveProp(window, arrow);
        }

        foreach (var arrow in _arrows.Arrows)
        {
            var (yaw, pitch) = Geometry.YawPitch(arrow.Heading);
            var placement = new PropPlacement(ArrowModel, arrow.Position, yaw, 1f, pitch);
            if (_props.TryGetValue(arrow, out int id))
            {
                window.SetPlacedProp(id, placement);
            }
            else
            {
                _props[arrow] = window.PlaceProp(placement);
            }
        }
    }

    /// <summary>Takes every arrow out of the world (a restart).</summary>
    public void Clear(EngineWindow window)
    {
        foreach (var arrow in _arrows.Clear())
        {
            RemoveProp(window, arrow);
        }
    }

    /// <summary>
    /// The way to shoot from <paramref name="origin"/> so the arrow reaches <paramref name="target"/> (what the crosshair is on). Falls back to the camera's own direction when the
    /// target is too close, or not in front of the Ranger (the crosshair on the ground at the Ranger's feet, say), where aiming at it would shoot sideways or backwards.
    /// </summary>
    public static Vector3D<float> AimDirection(Vector3D<float> origin, Vector3D<float> target, Vector3D<float> cameraFront, Vector3D<float> aimFlat)
    {
        var toTarget = target - origin;
        float distance = toTarget.Length;
        if (distance < 2f)
        {
            return cameraFront;
        }

        var direction = toTarget / distance;
        return Vector3D.Dot(Flat(direction), aimFlat) < 0.5f ? cameraFront : direction;
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

    /// <summary>What the crosshair is on: the nearest enemy or ground along the camera's line of sight, or a point far along it if there is neither.</summary>
    private static Vector3D<float> CrosshairTarget(Camera camera, Terrain terrain, EnemyField enemies, float range)
    {
        float reach = range + 10f;
        var far = camera.Position + camera.Front * reach;
        var target = far;
        float best = reach;

        if (terrain.TryRaycast(camera.Position, camera.Front, reach, out var ground))
        {
            best = Vector3D.Distance(camera.Position, ground);
            target = ground;
        }

        if (enemies.FirstHit(camera.Position, far, 0.05f, out float along) is not null && along * reach < best)
        {
            target = camera.Position + camera.Front * (along * reach);
        }

        return target;
    }

    private void RemoveProp(EngineWindow window, Arrow arrow)
    {
        if (_props.Remove(arrow, out int id))
        {
            window.RemovePlacedProp(id);
        }
    }

    private static Vector3D<float> Flat(Vector3D<float> v)
    {
        var flat = new Vector3D<float>(v.X, 0f, v.Z);
        return flat.LengthSquared > 1e-6f ? Vector3D.Normalize(flat) : Vector3D<float>.UnitZ;
    }
}
