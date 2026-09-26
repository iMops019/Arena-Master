using CEngine.Core;
using Silk.NET.Maths;

namespace ArenaMaster.Game.Combat;

/// <summary>
/// Puts the <see cref="EnemyField"/> on screen: every enemy of a kind drawn as one engine animated crowd (one instanced draw however many there are, each copy
/// at its own clip and time, chosen by <see cref="EnemyMotion"/>: walking with its feet planted, winding up and landing its attacks, dying), with a flinch and
/// a flash when it is hit, standing still and pale while frozen, and a sink into the ground as it dies. A kind that holds a weapon (the Crossbow Ghoul's
/// crossbow, the Ghoul Mage's flame, the Ghoul Tactician's bomb) has it drawn as an animated crowd of its own on the same skeleton and clip, lighting up from
/// faint to bright as a shot winds up. A prop (a crate) is a plain crowd. The shots in flight are crowds too; a fireball's landing spot is marked with a
/// burning ring while it flies, and it bursts in a ring of fire; a bomb tumbles, a ghostly ring on the ground under it showing its blast and pulsing faster as
/// it slows, and goes off in a ring of green. Attacks show their telegraphs on the ground as placed props: a red lane for a lunge, a red circle filling up for
/// a leap slam's landing, a ring spreading out for a shockwave.
/// </summary>
internal sealed class EnemyView
{
    public const string RingModel = "telegraph_ring.glb";
    public const string DiscModel = "telegraph_disc.glb";
    public const string LaneModel = "telegraph_lane.glb";

    /// <summary>How long the lane model is: a lunge of another reach scales it.</summary>
    private const float LaneLength = 7.2f;
    public const string ShockwaveModel = "shockwave_ring.glb";
    public const string LandingModel = "fireball_mark.glb";
    public const string BlastModel = "fireball_burst.glb";
    public const string BombMarkModel = "ghoul_bomb_mark.glb";
    public const string BombBurstModel = "ghoul_bomb_burst.glb";

    private enum Marker
    {
        Ring,
        Disc,
        Lane,
        Shockwave,
    }

    private readonly Dictionary<(int Enemy, Marker Marker), int> _markers = new();   // telegraph -> placed prop id
    private readonly Dictionary<string, List<CrowdInstance>> _crowds = new();       // model -> this frame's copies (props)
    private readonly Dictionary<string, List<SkinnedCrowdInstance>> _animated = new();   // model -> this frame's animated copies
    private readonly Dictionary<string, IReadOnlyDictionary<string, float>> _clips = new();   // model -> its clips' lengths
    private readonly EnemyMotion _motion = new();
    private readonly Dictionary<string, List<CrowdInstance>> _shots = new();   // shot model -> this frame's copies
    private readonly List<CrowdInstance> _landings = new();
    private readonly List<CrowdInstance> _bombMarks = new();
    private readonly Dictionary<string, List<CrowdInstance>> _blasts = new() { [BlastModel] = new(), [BombBurstModel] = new() };   // burst model -> this frame's
    private float _time;

    public void Sync(EngineWindow window, EnemyField field, IEnumerable<Enemy> gone, float deltaSeconds, Func<float, float, float?> groundAt)
    {
        _time += deltaSeconds;

        foreach (var enemy in gone)
        {
            Remove(window, enemy);
            _motion.Forget(enemy);
        }

        foreach (var list in _crowds.Values)
        {
            list.Clear();
        }

        foreach (var list in _animated.Values)
        {
            list.Clear();
        }

        var shown = new HashSet<(int, Marker)>();
        foreach (var enemy in field.Enemies)
        {
            if (enemy.Kind.IsProp || Clips(window, enemy.Kind.Model) is not { } clips)
            {
                Copies(_crowds, enemy.Kind.Model).Add(Pose(enemy));
            }
            else
            {
                var body = Animate(enemy, _motion.Update(enemy, deltaSeconds, clips));
                Copies(_animated, enemy.Kind.Model).Add(body);
                if (enemy.Kind.HeldModel is { } held)
                {
                    Copies(_animated, held).Add(body with { Flash = MathF.Max(Glow(enemy), body.Flash * 0.5f) });
                }
            }

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

        foreach (var (model, copies) in _animated)
        {
            window.SetSkinnedCrowd(model, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(copies));
        }

        foreach (var list in _shots.Values)
        {
            list.Clear();
        }

        _landings.Clear();
        foreach (var bolt in field.Bolts)
        {
            if (!_shots.TryGetValue(bolt.Model, out var shots))
            {
                shots = new List<CrowdInstance>();
                _shots[bolt.Model] = shots;
            }

            var (yaw, pitch) = Geometry.YawPitch(bolt.Velocity);
            shots.Add(new CrowdInstance(bolt.Position, yaw, 1f, pitch, Flash: 0.4f));
            if (bolt.Splash > 0f)
            {
                _landings.Add(new CrowdInstance(Lift(bolt.Target, 0.07f), _time * 1.5f, bolt.Splash, Flash: 0.3f + 0.3f * MathF.Abs(MathF.Sin(_time * 12f))));
            }
        }

        _bombMarks.Clear();
        foreach (var bomb in field.Bombs)
        {
            // Tumbling as it goes; the ring under it pulses quicker as it slows to a stop, and brighter the nearer it is to the ground.
            float speed = bomb.Velocity.Length;
            Copies(_shots, bomb.Model).Add(new CrowdInstance(bomb.Position, bomb.Age * 7f, 1f, bomb.Age * 11f, Flash: 0.35f + 0.35f * MathF.Abs(MathF.Sin(bomb.Age * 9f))));
            float ground = groundAt(bomb.Position.X, bomb.Position.Z) ?? bomb.Position.Y - bomb.Radius;
            float pulse = MathF.Abs(MathF.Sin(bomb.Age * (bomb.Rolling ? 20f - 2f * MathF.Min(speed, 6f) : 8f)));
            float near = 1f - Math.Clamp((bomb.Position.Y - bomb.Radius - ground) / 3f, 0f, 0.7f);
            _bombMarks.Add(new CrowdInstance(new Vector3D<float>(bomb.Position.X, ground + 0.07f, bomb.Position.Z), -_time * 2f, bomb.Splash,
                Flash: (0.2f + 0.5f * pulse) * near));
        }

        foreach (var list in _blasts.Values)
        {
            list.Clear();
        }

        foreach (var blast in field.Blasts)
        {
            float t = Math.Clamp(blast.Age / EnemyField.BlastSeconds, 0f, 1f);
            Copies(_blasts, blast.Model).Add(new CrowdInstance(Lift(blast.Centre, 0.15f + 0.4f * t), 0f, blast.Radius * (0.4f + 0.6f * (1f - (1f - t) * (1f - t))), Flash: 1f - t));
        }

        foreach (var (model, shots) in _shots)
        {
            window.SetCrowd(model, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(shots));
        }

        window.SetCrowd(LandingModel, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_landings));
        window.SetCrowd(BombMarkModel, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_bombMarks));
        foreach (var (model, blasts) in _blasts)
        {
            window.SetCrowd(model, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(blasts));
        }
    }

    /// <summary>
    /// How brightly a held weapon glows (0 to 1): faintly as a shot starts to wind up, climbing ever faster to full brightness as it is loosed - the warning to move.
    /// Dark otherwise.
    /// </summary>
    public static float Glow(Enemy enemy)
    {
        if (!enemy.IsAlive || enemy.Attack is not { Type: AttackType.Shoot or AttackType.Lob })
        {
            return 0f;
        }

        return enemy.AttackPhase switch
        {
            AttackPhase.WindUp => 0.15f + 0.85f * MathF.Pow(enemy.WindUpProgress, 1.5f),
            AttackPhase.Active => 1f,
            _ => 0f,
        };
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

    private static List<T> Copies<T>(Dictionary<string, List<T>> crowds, string model)
    {
        if (!crowds.TryGetValue(model, out var copies))
        {
            copies = new List<T>();
            crowds[model] = copies;
        }

        return copies;
    }

    /// <summary>An animated model's clips and their lengths (null if the model isn't there), asked of the engine once per model.</summary>
    private IReadOnlyDictionary<string, float>? Clips(EngineWindow window, string model)
    {
        if (!_clips.TryGetValue(model, out var clips) && window.SkinnedCrowdClips(model) is { } loaded)
        {
            clips = loaded;
            _clips[model] = clips;
        }

        return clips;
    }

    /// <summary>
    /// An animated enemy's copy: its clip from <paramref name="motion"/>, and on top of it a quick swell and flinch back when hit, pale while frozen, and a sink
    /// into the ground over the second half of dying (the Die clip has it on the ground by then).
    /// </summary>
    private static SkinnedCrowdInstance Animate(Enemy enemy, EnemyMotion.Pose motion)
    {
        var position = enemy.Position;
        bool fodder = enemy.Kind.Tier == EnemyTier.Fodder;
        float scale = 1f + 0.08f * enemy.HitFlash * (fodder ? 1f : 0.3f);
        float pitch = -0.2f * enemy.HitFlash * (fodder ? 1f : 0.2f);
        float flash = enemy.IsFrozen ? MathF.Max(0.55f, enemy.HitFlash) : enemy.HitFlash;
        if (!enemy.IsAlive)
        {
            float t = Math.Clamp(enemy.DeadFor / EnemyField.DeathDuration, 0f, 1f);
            position.Y -= MathF.Max(0f, t - 0.5f) * 2f * enemy.Kind.Height * 0.35f;
            flash = 0f;
        }

        return new SkinnedCrowdInstance(position, enemy.Yaw, motion.Clip, motion.Time, scale, pitch, flash, motion.From, motion.FromTime, motion.Fade);
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

        if (enemy.Kind.IsProp)
        {
            return new CrowdInstance(position, enemy.Yaw, scale, 0f, Flash: enemy.HitFlash);   // a crate: no bob, it just jolts when hit
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
                case (AttackPhase.WindUp, AttackType.Shockwave or AttackType.Summon or AttackType.Barrage):
                    pitch -= 0.3f * windUp;                                                           // rearing up
                    break;
                case (AttackPhase.WindUp, AttackType.Shoot or AttackType.Lob):
                    pitch -= 0.12f * windUp;                                                          // straightening up to aim
                    break;
                case (AttackPhase.Active, AttackType.Shoot or AttackType.Lob):
                    pitch -= 0.25f;                                                                   // the kick of the shot
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
                yield return (Marker.Lane, new PropPlacement(LaneModel, Lift(enemy.AttackOrigin, 0.06f), yaw, attack.Reach / LaneLength));   // a longer charge, a longer (and wider) lane
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
