using CEngine.Core;
using Silk.NET.Maths;

namespace ArenaMaster.Game.Combat;

/// <summary>
/// Puts the <see cref="EnemyField"/> on screen: every enemy of a kind drawn as one engine crowd (one instanced draw however many there are), with a shambling bob
/// while it walks, a flinch and a flash when it is hit, a crouch or a rearing-up as it winds up an attack, standing still and pale while frozen, and a sink into
/// the ground as it dies. Attacks show their
/// telegraphs on the ground as placed props: a red lane for a lunge, a red circle filling up for a leap slam's landing, a ring spreading out for a shockwave. There
/// are no animations yet; the stand-in models are rigid.
/// </summary>
internal sealed class EnemyView
{
    public const string RingModel = "telegraph_ring.glb";
    public const string DiscModel = "telegraph_disc.glb";
    public const string LaneModel = "telegraph_lane.glb";
    public const string ShockwaveModel = "shockwave_ring.glb";

    private enum Marker
    {
        Ring,
        Disc,
        Lane,
        Shockwave,
    }

    private readonly Dictionary<(int Enemy, Marker Marker), int> _markers = new();   // telegraph -> placed prop id
    private readonly Dictionary<string, List<CrowdInstance>> _crowds = new();       // model -> this frame's copies
    private float _time;

    public void Sync(EngineWindow window, EnemyField field, IEnumerable<Enemy> gone, float deltaSeconds, Func<float, float, float?> groundAt)
    {
        _time += deltaSeconds;

        foreach (var enemy in gone)
        {
            Remove(window, enemy);
        }

        foreach (var list in _crowds.Values)
        {
            list.Clear();
        }

        var shown = new HashSet<(int, Marker)>();
        foreach (var enemy in field.Enemies)
        {
            if (!_crowds.TryGetValue(enemy.Kind.Model, out var copies))
            {
                copies = new List<CrowdInstance>();
                _crowds[enemy.Kind.Model] = copies;
            }

            copies.Add(Pose(enemy));
            if (enemy.IsAlive)
            {
                foreach (var (marker, placement) in Telegraphs(enemy, groundAt))
                {
                    Place(window, _markers, (enemy.Id, marker), placement);
                    shown.Add((enemy.Id, marker));
                }
            }
        }

        foreach (var key in _markers.Keys.Where(k => !shown.Contains(k)).ToList())
        {
            window.RemovePlacedProp(_markers[key]);
            _markers.Remove(key);
        }

        foreach (var (model, copies) in _crowds)
        {
            window.SetCrowd(model, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(copies));   // an empty list clears a kind that is gone
        }
    }

    /// <summary>Takes away an enemy's ground telegraphs (its body goes with the next <see cref="Sync"/>).</summary>
    public void Remove(EngineWindow window, Enemy enemy)
    {
        foreach (var key in _markers.Keys.Where(k => k.Enemy == enemy.Id).ToList())
        {
            window.RemovePlacedProp(_markers[key]);
            _markers.Remove(key);
        }
    }

    private static void Place<TKey>(EngineWindow window, Dictionary<TKey, int> props, TKey key, PropPlacement placement)
        where TKey : notnull
    {
        if (props.TryGetValue(key, out int id))
        {
            window.SetPlacedProp(id, placement);
        }
        else
        {
            props[key] = window.PlaceProp(placement);
        }
    }

    private CrowdInstance Pose(Enemy enemy)
    {
        var position = enemy.Position;
        float scale = 1f + 0.12f * enemy.HitFlash * (enemy.Kind.Tier == EnemyTier.Fodder ? 1f : 0.3f);   // a quick swell on a hit; big ones barely
        float pitch = -0.25f * enemy.HitFlash * (enemy.Kind.Tier == EnemyTier.Fodder ? 1f : 0.2f);         // and a flinch back

        if (!enemy.IsAlive)
        {
            float t = Math.Clamp(enemy.DeadFor / EnemyField.DeathDuration, 0f, 1f);
            position.Y -= t * enemy.Kind.Height * 0.8f;   // sinks into the ground
            return new CrowdInstance(position, enemy.Yaw, scale * (1f - 0.4f * t), pitch + 0.9f * t);
        }

        if (enemy.IsFrozen)
        {
            return new CrowdInstance(position, enemy.Yaw, scale, pitch, Flash: MathF.Max(0.55f, enemy.HitFlash));   // still, and pale with frost
        }

        if (enemy.Attack is { } attack)
        {
            float windUp = enemy.WindUpProgress;
            switch (enemy.AttackPhase, attack.Type)
            {
                case (AttackPhase.WindUp, AttackType.Lunge):
                    pitch += 0.35f * windUp;                                                          // crouching to spring
                    position.X += 0.05f * MathF.Sin(_time * 60f) * windUp;                           // and shaking with it
                    break;
                case (AttackPhase.WindUp, AttackType.LeapSlam):
                    position.Y -= 0.25f * windUp;                                                     // squatting to jump
                    break;
                case (AttackPhase.WindUp, AttackType.Shockwave or AttackType.Summon):
                    pitch -= 0.3f * windUp;                                                           // rearing up
                    break;
                case (AttackPhase.Active, AttackType.Lunge):
                    pitch += 0.4f;                                                                    // head down, charging
                    break;
                case (AttackPhase.Active, AttackType.LeapSlam):
                    pitch += 0.5f * (enemy.PhaseTime / attack.Active);                               // tipping forward to come down
                    break;
                case (AttackPhase.Recover, _):
                    pitch += 0.2f;                                                                    // winded, open to punishment
                    break;
            }
        }
        else
        {
            position.Y += MathF.Abs(MathF.Sin(_time * 7f + enemy.Phase)) * 0.06f;
            pitch += 0.08f * MathF.Sin(_time * 3.5f + enemy.Phase);
        }

        return new CrowdInstance(position, enemy.Yaw, scale, pitch, Flash: enemy.HitFlash);
    }

    /// <summary>What an enemy's current attack shows on the ground.</summary>
    private IEnumerable<(Marker, PropPlacement)> Telegraphs(Enemy enemy, Func<float, float, float?> groundAt)
    {
        if (enemy.Attack is not { } attack)
        {
            yield break;
        }

        switch (attack.Type)
        {
            case AttackType.Lunge when enemy.AttackPhase == AttackPhase.WindUp:
            {
                var direction = enemy.AttackTarget - enemy.AttackOrigin;
                float yaw = MathF.Atan2(direction.X, direction.Z);
                yield return (Marker.Lane, new PropPlacement(LaneModel, Lift(enemy.AttackOrigin, 0.06f), yaw, 1f));
                break;
            }

            case AttackType.LeapSlam when enemy.AttackPhase != AttackPhase.Recover:
            {
                // The ring shows the whole landing zone at once; the disc inside fills it up as the landing nears.
                float progress = enemy.AttackPhase == AttackPhase.WindUp
                    ? enemy.PhaseTime / (attack.WindUp + attack.Active)
                    : (attack.WindUp + enemy.PhaseTime) / (attack.WindUp + attack.Active);
                var at = enemy.AttackTarget;
                yield return (Marker.Ring, new PropPlacement(RingModel, Lift(at, 0.08f), 0f, attack.Reach));
                yield return (Marker.Disc, new PropPlacement(DiscModel, Lift(at, 0.05f), 0f, MathF.Max(0.05f, attack.Reach * Math.Clamp(progress, 0f, 1f))));
                break;
            }

            case AttackType.Shockwave when enemy.AttackPhase == AttackPhase.Active:
            {
                var centre = enemy.AttackOrigin;
                float ground = groundAt(centre.X, centre.Z) ?? centre.Y;
                yield return (Marker.Shockwave, new PropPlacement(ShockwaveModel, new Vector3D<float>(centre.X, ground + 0.15f, centre.Z), 0f, enemy.ShockwaveRadius));
                break;
            }
        }
    }

    private static Vector3D<float> Lift(Vector3D<float> p, float by) => new(p.X, p.Y + by, p.Z);
}
