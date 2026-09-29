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
/// it slows, and goes off in a ring of green. Attacks show their telegraphs on the ground, as crowds too (so dozens at once cost what one does): a red lane for
/// a lunge, a red circle filling up for a leap slam's landing, a ring spreading out for a shockwave; a red wedge filling up for a swing or a cleave, a strip of squares down a rift (the eruption eating it up as it runs out), a ring round a whirlwind's reach, a circle filling round a stomp, a thin line down a sniper's lane (bright once it locks). A shot can have
/// its own landing mark and burst (the Fiend's bolts from the sky). A Magic,
/// Rare or Legendary enemy is drawn a little bigger, over a slowly turning ring in its rarity's colour. A boss roaring into a rage (the Marauder) has red motes
/// swirling up round it and a fire ring pulsing under it; enraged, fewer, for the rest of the fight.
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

    /// <summary>A 10-degree slice of a wedge of radius 1 along +Z (dark), and the same slice bright, filling it; a 1 m square; the rage's motes and ring.</summary>
    public const string SliceModel = "telegraph_slice.glb";
    public const string SliceFillModel = "telegraph_slice_fill.glb";
    public const string SquareModel = "telegraph_square.glb";
    public const string RageMoteModel = "rage_mote.glb";
    public const string RageRingModel = "rage_ring.glb";

    /// <summary>How wide one slice is: a wedge is laid out in as many as it takes.</summary>
    private const float SliceAngle = 10f * MathF.PI / 180f;

    private readonly Dictionary<string, List<CrowdInstance>> _telegraphs = new()   // telegraph model -> this frame's
    {
        [RingModel] = new(), [DiscModel] = new(), [LaneModel] = new(), [ShockwaveModel] = new(),
        [SliceModel] = new(), [SliceFillModel] = new(), [SquareModel] = new(), [RageMoteModel] = new(), [RageRingModel] = new(),
    };
    private readonly Dictionary<string, List<CrowdInstance>> _crowds = new();       // model -> this frame's copies (props)
    private readonly Dictionary<string, List<SkinnedCrowdInstance>> _animated = new();   // model -> this frame's animated copies
    private readonly Dictionary<string, IReadOnlyDictionary<string, float>> _clips = new();   // model -> its clips' lengths
    private readonly EnemyMotion _motion = new();
    private readonly Dictionary<string, List<CrowdInstance>> _shots = new();   // shot model -> this frame's copies
    private readonly Dictionary<string, List<CrowdInstance>> _landings = new() { [LandingModel] = new() };   // landing mark model -> this frame's
    private readonly List<CrowdInstance> _bombMarks = new();
    private readonly Dictionary<string, List<CrowdInstance>> _rings = new()   // rarity ring model -> this frame's
    {
        [RarityTraits.Magic.RingModel!] = new(), [RarityTraits.Rare.RingModel!] = new(), [RarityTraits.Legendary.RingModel!] = new(),
    };
    private readonly Dictionary<string, List<CrowdInstance>> _blasts = new() { [BlastModel] = new(), [BombBurstModel] = new() };   // burst model -> this frame's
    private float _time;

    public void Sync(EngineWindow window, EnemyField field, IEnumerable<Enemy> gone, float deltaSeconds, Func<float, float, float?> groundAt)
    {
        _time += deltaSeconds;

        foreach (var enemy in gone)
        {
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

        foreach (var list in _rings.Values)
        {
            list.Clear();
        }

        foreach (var list in _telegraphs.Values)
        {
            list.Clear();
        }

        foreach (var enemy in field.Enemies)
        {
            if (enemy.IsAlive && enemy.Rarity.RingModel is { } ring)
            {
                float size = enemy.Kind.Radius * enemy.Rarity.Size * 1.5f + 0.25f;
                float spin = (enemy.Rarity.Rarity == MonsterRarity.Legendary ? 1.2f : 0.6f) * _time + enemy.Phase;
                Copies(_rings, ring).Add(new CrowdInstance(Lift(enemy.Position, 0.06f), spin, size, Flash: 0.25f + 0.2f * MathF.Sin(_time * 4f + enemy.Phase)));
            }

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
                AddTelegraphs(enemy, groundAt);
                AddRage(enemy, groundAt);
            }
        }

        foreach (var (model, copies) in _telegraphs)
        {
            window.SetCrowd(model, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(copies));
        }

        foreach (var (model, copies) in _rings)
        {
            window.SetCrowd(model, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(copies));
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

        foreach (var list in _landings.Values)
        {
            list.Clear();
        }

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
                Copies(_landings, bolt.MarkModel).Add(new CrowdInstance(Lift(bolt.Target, 0.07f), _time * 1.5f, bolt.Splash,
                    Flash: 0.3f + 0.3f * MathF.Abs(MathF.Sin(_time * 12f))));
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

        foreach (var (model, landings) in _landings)
        {
            window.SetCrowd(model, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(landings));
        }

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
        if (!enemy.IsAlive || enemy.Attack is not { Type: AttackType.Shoot or AttackType.Lob or AttackType.Snipe })
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
        float scale = (1f + 0.08f * enemy.HitFlash * (fodder ? 1f : 0.3f)) * enemy.Rarity.Size;
        float pitch = -0.2f * enemy.HitFlash * (fodder ? 1f : 0.2f);
        float flash = enemy.IsFrozen ? MathF.Max(0.55f, enemy.HitFlash) : enemy.HitFlash;
        if (!enemy.IsAlive)
        {
            float t = Math.Clamp(enemy.DeadFor / EnemyField.DeathDuration, 0f, 1f);
            position.Y -= MathF.Max(0f, t - 0.5f) * 2f * enemy.Kind.Height * 0.35f;
            flash = 0f;
        }
        else if (enemy.IsRoaring)
        {
            // Shaking with the roar, and glowing with it in pulses.
            position.X += 0.05f * MathF.Sin(enemy.RoarLeft * 55f);
            position.Z += 0.05f * MathF.Cos(enemy.RoarLeft * 47f);
            flash = MathF.Max(flash, 0.12f + 0.18f * MathF.Abs(MathF.Sin(enemy.RoarLeft * 10f)));
        }

        return new SkinnedCrowdInstance(position, enemy.Yaw, motion.Clip, motion.Time, scale, pitch, flash, motion.From, motion.FromTime, motion.Fade);
    }

    private CrowdInstance Pose(Enemy enemy)
    {
        var position = enemy.Position;
        float scale = (1f + 0.12f * enemy.HitFlash * (enemy.Kind.Tier == EnemyTier.Fodder ? 1f : 0.3f)) * enemy.Rarity.Size;   // a quick swell on a hit; big ones barely
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
                case (AttackPhase.WindUp, AttackType.LineSlam):
                    pitch -= 0.25f * windUp;                                                          // rearing back with the axe up
                    break;
                case (AttackPhase.Active, AttackType.LineSlam):
                    pitch += 0.3f;                                                                    // bent over the blow
                    break;
                case (AttackPhase.Recover, AttackType.Whirlwind):
                    pitch += 0.3f;                                                                    // dizzy
                    position.X += 0.06f * MathF.Sin(_time * 9f);
                    break;
                case (AttackPhase.Recover, _):
                    pitch += 0.2f;                                                                    // winded, open to punishment
                    break;
            }
        }
        else if (enemy.IsRoaring)
        {
            pitch -= 0.12f;                                                                            // chest out, roaring, and shaking with it
            position.X += 0.05f * MathF.Sin(_time * 55f);
            position.Z += 0.05f * MathF.Cos(_time * 47f);
        }
        else
        {
            position.Y += MathF.Abs(MathF.Sin(_time * 7f + enemy.Phase)) * 0.06f;
            pitch += 0.08f * MathF.Sin(_time * 3.5f + enemy.Phase);
        }

        return new CrowdInstance(position, enemy.Yaw, scale, pitch, Flash: enemy.HitFlash);
    }

    /// <summary>What an enemy's current attack shows on the ground, added to this frame's telegraph crowds.</summary>
    private void AddTelegraphs(Enemy enemy, Func<float, float, float?> groundAt)
    {
        if (enemy.Attack is not { } attack)
        {
            return;
        }

        switch (attack.Type)
        {
            case AttackType.Lunge when enemy.AttackPhase == AttackPhase.WindUp:
            {
                var direction = enemy.AttackTarget - enemy.AttackOrigin;
                float yaw = MathF.Atan2(direction.X, direction.Z);
                float length = attack.StopAtPlayer ? direction.Length : attack.Reach;   // a charge that stops at the player: as far as it goes
                _telegraphs[LaneModel].Add(new CrowdInstance(Lift(enemy.AttackOrigin, 0.06f), yaw, MathF.Max(0.3f, length) / LaneLength));   // a longer charge, a longer (and wider) lane
                break;
            }

            case AttackType.LeapSlam when enemy.AttackPhase != AttackPhase.Recover:
            {
                // The ring shows the whole landing zone at once; the disc inside fills it up as the landing nears.
                float progress = enemy.AttackPhase == AttackPhase.WindUp
                    ? enemy.PhaseTime / (attack.WindUp + attack.Active)
                    : (attack.WindUp + enemy.PhaseTime) / (attack.WindUp + attack.Active);
                var at = enemy.AttackTarget;
                _telegraphs[RingModel].Add(new CrowdInstance(Lift(at, 0.08f), 0f, attack.Reach));
                _telegraphs[DiscModel].Add(new CrowdInstance(Lift(at, 0.05f), 0f, MathF.Max(0.05f, attack.Reach * Math.Clamp(progress, 0f, 1f))));
                break;
            }

            case AttackType.Shockwave when enemy.AttackPhase == AttackPhase.Active:
            {
                var centre = enemy.AttackOrigin;
                float ground = groundAt(centre.X, centre.Z) ?? centre.Y;
                _telegraphs[ShockwaveModel].Add(new CrowdInstance(new Vector3D<float>(centre.X, ground + 0.15f, centre.Z), 0f, enemy.ShockwaveRadius));
                break;
            }

            case AttackType.Swing or AttackType.Cleave when enemy.AttackPhase == AttackPhase.WindUp:
            {
                // The wedge, in slices, and a brighter wedge filling it out from the attacker as the blow nears.
                var aim = enemy.AttackTarget - enemy.AttackOrigin;
                float yaw = MathF.Atan2(aim.X, aim.Z);
                int slices = Math.Max(1, (int)MathF.Ceiling(2f * attack.HitWidth / SliceAngle));
                float step = 2f * attack.HitWidth / slices;
                float fill = MathF.Max(0.05f, attack.Reach * enemy.WindUpProgress);
                var at = Ground(enemy.Position, groundAt);
                for (int i = 0; i < slices; i++)
                {
                    float sliceYaw = yaw - attack.HitWidth + (i + 0.5f) * step;
                    _telegraphs[SliceModel].Add(new CrowdInstance(Lift(at, 0.05f), sliceYaw, attack.Reach));
                    _telegraphs[SliceFillModel].Add(new CrowdInstance(Lift(at, 0.07f), sliceYaw, fill));
                }

                break;
            }

            case AttackType.LineSlam when enemy.AttackPhase != AttackPhase.Recover:
            {
                // Each lane in squares as wide as it is; once the blow comes, the eruptions run out along them and the squares behind their fronts go.
                float side = 2f * attack.HitWidth;
                int squares = Math.Max(1, (int)MathF.Ceiling(attack.Reach / side));
                float front = enemy.AttackPhase == AttackPhase.Active ? attack.Reach * Math.Clamp(enemy.PhaseTime / attack.Active, 0f, 1f) : 0f;
                foreach (var way in EnemyField.RiftLanes(enemy, attack))
                {
                    float yaw = MathF.Atan2(way.X, way.Z);
                    for (int k = 0; k < squares; k++)
                    {
                        float along = side * (k + 0.5f);
                        if (along < front)
                        {
                            continue;
                        }

                        var centre = enemy.AttackOrigin + way * along;
                        _telegraphs[SquareModel].Add(new CrowdInstance(Lift(Ground(centre, groundAt), 0.06f), yaw, side,
                            Flash: 0.2f + 0.5f * enemy.WindUpProgress));
                    }

                    if (front > 0f)
                    {
                        var burst = enemy.AttackOrigin + way * front;
                        _telegraphs[ShockwaveModel].Add(new CrowdInstance(Lift(Ground(burst, groundAt), 0.2f), 0f, attack.HitWidth * 1.3f));
                    }
                }

                break;
            }

            case AttackType.Stomp when enemy.AttackPhase == AttackPhase.WindUp:
            {
                // The ring round it, filling as the slam comes.
                var at = Ground(enemy.Position, groundAt);
                _telegraphs[RingModel].Add(new CrowdInstance(Lift(at, 0.08f), 0f, attack.Reach));
                _telegraphs[DiscModel].Add(new CrowdInstance(Lift(at, 0.05f), 0f, MathF.Max(0.05f, attack.Reach * enemy.WindUpProgress)));
                break;
            }

            case AttackType.Snipe when enemy.AttackPhase == AttackPhase.WindUp:
            {
                // A thin line of squares down the lane: faint while it follows the player, bright and flickering once it locks.
                var way = Geometry.FlatDirection(enemy.AttackOrigin, enemy.AttackTarget, out _);
                float yaw = MathF.Atan2(way.X, way.Z);
                float side = 2f * attack.HitWidth;
                bool locked = enemy.PhaseTime >= attack.Tracking;
                float flash = locked ? 0.55f + 0.45f * MathF.Abs(MathF.Sin(_time * 18f)) : 0.1f + 0.2f * enemy.WindUpProgress;
                for (float along = enemy.Kind.Radius + side * 0.5f; along < attack.Reach; along += side)
                {
                    var centre = enemy.AttackOrigin + way * along;
                    _telegraphs[SquareModel].Add(new CrowdInstance(Lift(Ground(centre, groundAt), 0.06f), yaw, side, Flash: flash));
                }

                break;
            }

            case AttackType.Whirlwind when enemy.AttackPhase != AttackPhase.Recover:
            {
                // Its reach, round it as it winds up and all the while it spins.
                var at = Ground(enemy.Position, groundAt);
                _telegraphs[RingModel].Add(new CrowdInstance(Lift(at, 0.08f), 0f, attack.Reach));
                if (enemy.AttackPhase == AttackPhase.WindUp)
                {
                    _telegraphs[DiscModel].Add(new CrowdInstance(Lift(at, 0.05f), 0f, MathF.Max(0.05f, attack.Reach * enemy.WindUpProgress)));
                }

                break;
            }
        }
    }

    /// <summary>
    /// A boss's rage: roaring, red motes swirling up round it in waves and a fire ring pulsing under it; enraged after, fewer motes and a steady ring, for the rest
    /// of the fight.
    /// </summary>
    private void AddRage(Enemy enemy, Func<float, float, float?> groundAt)
    {
        if (!enemy.IsRoaring && !enemy.IsEnraged)
        {
            return;
        }

        bool roaring = enemy.IsRoaring;
        int count = roaring ? 40 : 12;
        float swirl = roaring ? 2.4f : 1.2f;
        float climb = roaring ? 1.1f : 0.5f;
        var feet = Ground(enemy.Position, groundAt);
        for (int i = 0; i < count; i++)
        {
            float k = (float)i / count;
            float seed = i * 1.618f;
            float rise = (_time * climb + k * 2.3f) % 1f;
            float angle = _time * swirl + k * MathF.Tau + 0.5f * MathF.Sin(_time * 3f + seed);   // wavering as they go round
            float radius = enemy.Kind.Radius * (0.9f + 0.5f * rise) + 0.3f * MathF.Sin(_time * 4f + seed);
            var at = feet + new Vector3D<float>(MathF.Sin(angle) * radius, rise * enemy.Kind.Height * 1.1f, MathF.Cos(angle) * radius);
            float size = (roaring ? 0.3f : 0.2f) * (1f - 0.6f * rise);
            _telegraphs[RageMoteModel].Add(new CrowdInstance(at, angle, size, _time * 5f + seed, Flash: 0.5f + 0.5f * (1f - rise)));
        }

        float pulse = roaring ? 1.6f + 0.3f * MathF.Sin(_time * 12f) : 1.4f;
        _telegraphs[RageRingModel].Add(new CrowdInstance(Lift(feet, 0.1f), _time * (roaring ? 3f : 0.8f), enemy.Kind.Radius * pulse,
            Flash: roaring ? 0.6f + 0.4f * MathF.Abs(MathF.Sin(_time * 12f)) : 0.3f));
    }

    private static Vector3D<float> Ground(Vector3D<float> p, Func<float, float, float?> groundAt) => new(p.X, groundAt(p.X, p.Z) ?? p.Y, p.Z);

    private static Vector3D<float> Lift(Vector3D<float> p, float by) => new(p.X, p.Y + by, p.Z);
}
