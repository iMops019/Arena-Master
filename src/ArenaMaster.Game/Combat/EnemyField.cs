using Silk.NET.Maths;

namespace ArenaMaster.Game.Combat;

internal enum AttackPhase
{
    WindUp,
    Active,
    Recover,
}

/// <summary>One enemy in the world. <see cref="Position"/> is its feet.</summary>
internal sealed class Enemy
{
    public Enemy(int id, EnemyKind kind, Vector3D<float> position, EnemyScaling scaling)
    {
        Id = id;
        Kind = kind;
        Position = position;
        MaxHealth = kind.MaxHealth * scaling.Health;
        Health = MaxHealth;
        Speed = kind.Speed * scaling.Speed;
        DamageScale = scaling.Damage;
    }

    public int Id { get; }

    public EnemyKind Kind { get; }

    public Vector3D<float> Position { get; set; }

    public float Yaw { get; set; }

    public float MaxHealth { get; }

    public float Health { get; set; }

    public float Speed { get; }

    /// <summary>What its contact and attack damage are multiplied by.</summary>
    public float DamageScale { get; }

    public bool IsAlive => Health > 0f;

    /// <summary>1 the moment it is hit, fading to 0 - the view makes it flinch.</summary>
    public float HitFlash { get; set; }

    /// <summary>Seconds since it died (only meaningful once it has).</summary>
    public float DeadFor { get; set; }

    /// <summary>Seconds of chill left, and how much it slows the walk while it lasts (0 to 1).</summary>
    public float ChilledFor { get; set; }

    public float ChillSlow { get; set; }

    /// <summary>Seconds left frozen solid: no walking, no clawing, an attack under way held.</summary>
    public float FrozenFor { get; set; }

    public bool IsChilled => ChilledFor > 0f;

    public bool IsFrozen => FrozenFor > 0f;

    /// <summary>How fast it walks right now: its speed (quicker in a boss's later stages), less any chill.</summary>
    public float WalkSpeed => (IsChilled ? Speed * (1f - ChillSlow) : Speed) * PhaseSpeed;

    /// <summary>Which of its kind's <see cref="EnemyKind.Phases"/> it is in: -1 for none yet (its first stage).</summary>
    public int PhaseIndex { get; set; } = -1;

    public BossPhase? CurrentPhase => PhaseIndex >= 0 ? Kind.Phases[PhaseIndex] : null;

    /// <summary>The attacks it chooses from now: its stage's, or its kind's.</summary>
    public IReadOnlyList<AttackSpec> AttacksNow => CurrentPhase?.Attacks ?? Kind.Attacks;

    /// <summary>The breather after each attack now.</summary>
    public float AttackCooldownNow => CurrentPhase?.AttackCooldown ?? Kind.AttackCooldown;

    public float PhaseSpeed => CurrentPhase?.SpeedMultiplier ?? 1f;

    /// <summary>How many more times the attack under way goes again straight after (its <see cref="AttackSpec.Chain"/>).</summary>
    public int ChainLeft { get; set; }

    /// <summary>
    /// Chills it for <paramref name="seconds"/>, slowing its walk by <paramref name="slow"/> (0 to 1). A chill already on it keeps the stronger slow and the longer time.
    /// </summary>
    public void Chill(float seconds, float slow)
    {
        ChillSlow = IsChilled ? MathF.Max(ChillSlow, slow) : slow;
        ChilledFor = MathF.Max(ChilledFor, seconds);
    }

    /// <summary>Freezes it solid for <paramref name="seconds"/> (or keeps a longer freeze already on it).</summary>
    public void Freeze(float seconds) => FrozenFor = MathF.Max(FrozenFor, seconds);

    public float ContactCooldown { get; set; }

    /// <summary>A per-enemy offset so a crowd doesn't bob in step.</summary>
    public float Phase { get; set; }

    /// <summary>The attack under way, or null while it is just chasing.</summary>
    public AttackSpec? Attack { get; set; }

    public AttackPhase AttackPhase { get; set; }

    /// <summary>Seconds into the current <see cref="AttackPhase"/>.</summary>
    public float PhaseTime { get; set; }

    /// <summary>Seconds until it may start another attack.</summary>
    public float AttackCooldown { get; set; }

    /// <summary>Where the attack started from: the lunge's start, the leap's take-off, the shockwave's centre.</summary>
    public Vector3D<float> AttackOrigin { get; set; }

    /// <summary>Where the attack is aimed: the lunge's end, the leap's landing spot.</summary>
    public Vector3D<float> AttackTarget { get; set; }

    /// <summary>Whether the attack has already hit (each attack hits at most once).</summary>
    public bool AttackLanded { get; set; }

    /// <summary>0 to 1 through the wind-up - how far along a telegraph is.</summary>
    public float WindUpProgress => Attack is { } a && AttackPhase == AttackPhase.WindUp ? Math.Clamp(PhaseTime / a.WindUp, 0f, 1f) : 0f;

    /// <summary>How far a shockwave has spread from its centre right now, or 0 if none is spreading.</summary>
    public float ShockwaveRadius => Attack is { Type: AttackType.Shockwave } a && AttackPhase == AttackPhase.Active
        ? Kind.Radius + (a.Reach - Kind.Radius) * Math.Clamp(PhaseTime / a.Active, 0f, 1f)
        : 0f;
}

/// <summary>
/// Where the player is and what enemies can do to them this frame. <paramref name="BlockChance"/> (0 to 1) is the chance a blow is turned aside completely: no
/// damage, no shove, no stun. It's 0 for a class without a shield.
/// </summary>
internal readonly record struct PlayerTarget(Vector3D<float> Feet, bool Grounded, PlayerHealth Health, PlayerCondition Condition, float BlockChance = 0f);

/// <summary>A blow that reached the player this frame, landed or blocked: who struck, and how hard (before any cut to damage taken).</summary>
internal readonly record struct Strike(Enemy Attacker, float Damage, bool Blocked);

/// <summary>
/// An enemy's shot in flight - a Crossbow Ghoul's bolt or a Ghoul Mage's fireball: it flies straight, and the first thing it meets, the player or the ground,
/// stops it. A shot with a <see cref="Splash"/> bursts there, hurting the player if they are within it.
/// </summary>
internal sealed class EnemyBolt
{
    public required Enemy Shooter { get; init; }

    /// <summary>The model it is drawn with.</summary>
    public required string Model { get; init; }

    /// <summary>Where it was aimed: for a splash shot, the spot on the ground where it will land (the view marks it).</summary>
    public Vector3D<float> Target { get; init; }

    public float Splash { get; init; }

    public Vector3D<float> Position { get; set; }

    public Vector3D<float> Velocity { get; init; }

    /// <summary>Seconds of flight left.</summary>
    public float FlightLeft { get; set; }

    public float Damage { get; init; }

    public float Radius { get; init; }

    public float Knockback { get; init; }
}

/// <summary>A splash shot bursting (a fireball), for the view: where, how wide, and how long ago.</summary>
internal sealed class EnemyBlast
{
    public Vector3D<float> Centre { get; init; }

    public float Radius { get; init; }

    public float Age { get; set; }
}

/// <summary>
/// What every hit the player lands does besides its damage, whichever class lands it (from the items carried): multipliers on chilled, frozen and elite enemies,
/// a chill, and a chance to freeze a non-boss enemy.
/// </summary>
internal sealed record HitEffects(
    float ChilledMultiplier = 1f,
    float FrozenMultiplier = 1f,
    float EliteMultiplier = 1f,
    float ChillSlow = 0f,
    float ChillSeconds = 0f,
    float FreezeChance = 0f,
    float FreezeSeconds = 0f)
{
    public static readonly HitEffects None = new();
}

/// <summary>
/// Every enemy on the map: keeping the field topped up with fodder around the player, moving them (straight at the player, kept apart from each other and off the
/// player), contact damage, the elites' and bosses' telegraphed attacks, taking hits and dying. Pure simulation - no engine calls - so it can be tested;
/// <see cref="EnemyView"/> puts it on screen.
/// </summary>
internal sealed class EnemyField
{
    /// <summary>How long a dead enemy stays (sinking) before it is gone.</summary>
    public const float DeathDuration = 0.4f;

    /// <summary>The walking player's radius (the engine's PlayerController), for contact.</summary>
    public const float PlayerRadius = 0.35f;

    /// <summary>Enemies further than this from the player are moved back into the spawn ring rather than left stranded.</summary>
    public const float LeashDistance = 75f;

    /// <summary>The widest enemy's radius (the boss's), so grid lookups reach far enough to find any body that could touch.</summary>
    private const float MaxEnemyRadius = 1.5f;

    /// <summary>At most this many fodder spawn in one frame, however far below the target count the field is.</summary>
    private const int MaxSpawnsPerFrame = 6;

    private readonly List<Enemy> _enemies = new();
    private readonly List<Enemy> _newlyKilled = new();
    private readonly List<(EnemyKind Kind, Vector3D<float> Position)> _summoned = new();
    private readonly List<Strike> _strikes = new();
    private readonly List<EnemyBolt> _bolts = new();
    private readonly List<EnemyBlast> _blasts = new();
    private readonly List<(Enemy Boss, BossPhase Phase)> _phaseChanges = new();
    private readonly EnemyGrid _grid = new();
    private readonly Random _random;
    private bool _gridStale = true;
    private int _nextId = 1;
    private float _spawnTimer;

    public EnemyField(Random random) => _random = random;

    public IReadOnlyList<Enemy> Enemies => _enemies;

    /// <summary>The fodder the field keeps topped up with.</summary>
    public EnemyKind Kind { get; set; } = EnemyKind.Ghoul;

    /// <summary>How much tougher than base the next spawns are (the run director raises it over time).</summary>
    public EnemyScaling Scaling { get; set; } = EnemyScaling.None;

    /// <summary>
    /// The kinds some of the fodder spawns as instead of <see cref="Kind"/> (the Crossbow Ghoul, the Ghoul Mage), each with the share that does (0 to 1, and
    /// together at most 1).
    /// </summary>
    public IReadOnlyList<(EnemyKind Kind, float Share)> Mix { get; set; } = Array.Empty<(EnemyKind, float)>();

    /// <summary>The enemies' shots in flight.</summary>
    public IReadOnlyList<EnemyBolt> Bolts => _bolts;

    /// <summary>How long a burst lasts, for the view.</summary>
    public const float BlastSeconds = 0.35f;

    /// <summary>Splash shots bursting right now.</summary>
    public IReadOnlyList<EnemyBlast> Blasts => _blasts;

    /// <summary>What every hit the player lands does besides its damage (see <see cref="HitEffects"/>). Set at the start of a run.</summary>
    public HitEffects HitEffects { get; set; } = HitEffects.None;

    /// <summary>What the damage of the enemies' shots (bolts, fireballs) is multiplied by, from the items carried.</summary>
    public float RangedDamageTaken { get; set; } = 1f;

    /// <summary>Enemies spawn with this much more health than <see cref="Scaling"/> alone gives them (a cursed item).</summary>
    public float HealthBonus { get; set; }

    /// <summary>All the damage the player has dealt since <see cref="ResetKills"/>, after every multiplier.</summary>
    public float DamageDealt { get; private set; }

    /// <summary>How many live fodder enemies the field keeps topped up to. Elites and bosses don't count.</summary>
    public int TargetCount { get; set; } = 14;

    /// <summary>Seconds between spawns while below <see cref="TargetCount"/>.</summary>
    public float SpawnInterval { get; set; } = 0.5f;

    public float SpawnMinDistance { get; set; } = 24f;

    public float SpawnMaxDistance { get; set; } = 38f;

    public int Kills { get; private set; }

    public int AliveCount => _enemies.Count(e => e.IsAlive && e.Kind.Tier == EnemyTier.Fodder && !e.Kind.IsProp);

    /// <summary>The blows that reached the player during the last <see cref="Update"/>, landed or blocked, for anything that answers them (thorns, say).</summary>
    public IReadOnlyList<Strike> Strikes => _strikes;

    /// <summary>The bosses that went into a new stage since this was last asked, and the stage each went into.</summary>
    public List<(Enemy Boss, BossPhase Phase)> TakePhaseChanges()
    {
        var changes = _phaseChanges.ToList();
        _phaseChanges.Clear();
        return changes;
    }

    /// <summary>The first living boss, if one is on the field.</summary>
    public Enemy? Boss => _enemies.FirstOrDefault(e => e.IsAlive && e.Kind.Tier == EnemyTier.Boss);

    /// <summary>Puts an enemy (of <see cref="Kind"/> unless <paramref name="kind"/> says otherwise, at the current <see cref="Scaling"/>) at <paramref name="position"/>, its feet.</summary>
    public Enemy Spawn(Vector3D<float> position, EnemyKind? kind = null)
    {
        var scaling = HealthBonus != 0f ? Scaling with { Health = Scaling.Health * (1f + HealthBonus) } : Scaling;
        var enemy = new Enemy(_nextId++, kind ?? Kind, position, scaling) { Phase = (float)_random.NextDouble() * MathF.Tau };
        enemy.AttackCooldown = enemy.Kind.AttackCooldown * 0.5f;   // a short breather after arriving
        _enemies.Add(enemy);
        _gridStale = true;
        return enemy;
    }

    /// <summary>Spawns a <paramref name="kind"/> in the ring around the player. Null if no spot on the map could be found.</summary>
    public Enemy? SpawnAround(EnemyKind kind, Vector3D<float> playerFeet, Func<float, float, float?> groundAt) =>
        TryPickSpawnPoint(playerFeet, groundAt, out var point) ? Spawn(point, kind) : null;

    /// <summary>
    /// Advances every enemy one frame. <paramref name="groundAt"/> gives the ground height at a point, or null off the map. Enemies hurt, shove and stun
    /// <paramref name="player"/>. Returns the enemies that finished dying this frame and are now gone (so their view can be removed).
    /// </summary>
    public List<Enemy> Update(float deltaSeconds, PlayerTarget player, Func<float, float, float?> groundAt)
    {
        _strikes.Clear();
        SpawnTowardTarget(deltaSeconds, player.Feet, groundAt);
        RefreshGrid();

        foreach (var enemy in _enemies)
        {
            enemy.HitFlash = MathF.Max(0f, enemy.HitFlash - deltaSeconds * 5f);
            if (enemy.IsAlive)
            {
                Move(enemy, deltaSeconds, player, groundAt);
            }
            else
            {
                enemy.DeadFor += deltaSeconds;
            }
        }

        MoveBolts(deltaSeconds, player, groundAt);
        foreach (var blast in _blasts)
        {
            blast.Age += deltaSeconds;
        }

        _blasts.RemoveAll(b => b.Age >= BlastSeconds);

        foreach (var (kind, position) in _summoned)
        {
            Spawn(position, kind);
        }

        _summoned.Clear();

        var gone = _enemies.Where(e => !e.IsAlive && e.DeadFor >= DeathDuration).ToList();
        _enemies.RemoveAll(e => !e.IsAlive && e.DeadFor >= DeathDuration);
        _gridStale = true;   // everyone has moved
        return gone;
    }

    /// <summary>
    /// Hurts <paramref name="enemy"/>: <paramref name="amount"/>, raised by <see cref="HitEffects"/> for a chilled, frozen or elite enemy; then, if it lives, the
    /// hit's chill and chance to freeze. True if this was the killing blow.
    /// </summary>
    public bool Damage(Enemy enemy, float amount)
    {
        if (!enemy.IsAlive)
        {
            return false;
        }

        var effects = HitEffects;
        if (enemy.IsChilled || enemy.IsFrozen)
        {
            amount *= effects.ChilledMultiplier;
        }

        if (enemy.IsFrozen)
        {
            amount *= effects.FrozenMultiplier;
        }

        if (enemy.Kind.Tier != EnemyTier.Fodder)
        {
            amount *= effects.EliteMultiplier;
        }

        enemy.Health -= amount;
        enemy.HitFlash = 1f;
        DamageDealt += MathF.Max(0f, amount + MathF.Min(0f, enemy.Health));   // no credit for overkill
        if (enemy.IsAlive)
        {
            if (effects.ChillSlow > 0f)
            {
                enemy.Chill(effects.ChillSeconds, effects.ChillSlow);
            }

            if (effects.FreezeChance > 0f && enemy.Kind.Tier != EnemyTier.Boss && _random.NextDouble() < effects.FreezeChance)
            {
                enemy.Freeze(effects.FreezeSeconds);
            }

            return false;
        }

        enemy.Health = 0f;
        enemy.DeadFor = 0f;
        if (!enemy.Kind.IsProp)
        {
            Kills++;   // a broken crate is not a kill
        }

        _newlyKilled.Add(enemy);
        return true;
    }

    /// <summary>The enemies killed since the last call, whatever killed them - for drops.</summary>
    public List<Enemy> TakeNewlyKilled()
    {
        var killed = _newlyKilled.ToList();
        _newlyKilled.Clear();
        return killed;
    }

    /// <summary>
    /// The first live enemy the moving sphere (<paramref name="from"/> to <paramref name="to"/>, radius <paramref name="radius"/>) touches, and how far along the move (0 to 1) it did.
    /// Each enemy is a capsule around its standing axis. Enemies in <paramref name="skip"/> are passed over (ones a piercing arrow has already gone through).
    /// </summary>
    public Enemy? FirstHit(Vector3D<float> from, Vector3D<float> to, float radius, out float along, IReadOnlySet<Enemy>? skip = null)
    {
        Enemy? best = null;
        along = float.MaxValue;

        RefreshGrid();
        foreach (var enemy in _grid.AlongSegment(from, to, radius + MaxEnemyRadius))
        {
            if (!enemy.IsAlive || skip?.Contains(enemy) == true)
            {
                continue;
            }

            float r = enemy.Kind.Radius;
            var bottom = enemy.Position + new Vector3D<float>(0f, r, 0f);
            var top = enemy.Position + new Vector3D<float>(0f, MathF.Max(r, enemy.Kind.Height - r), 0f);
            if (Geometry.SegmentDistance(from, to, bottom, top, out float s) <= r + radius && s < along)
            {
                best = enemy;
                along = s;
            }
        }

        return best;
    }

    /// <summary>
    /// The live enemies whose bodies reach into the flat circle of <paramref name="radius"/> around <paramref name="centre"/>: everything an area attack there touches,
    /// big ones from further off. A list, so the caller can hurt them as it goes.
    /// </summary>
    public List<Enemy> Within(Vector3D<float> centre, float radius)
    {
        RefreshGrid();
        var found = new List<Enemy>();
        foreach (var enemy in _grid.Near(centre.X, centre.Z, radius + MaxEnemyRadius))
        {
            if (enemy.IsAlive)
            {
                Geometry.FlatDirection(centre, enemy.Position, out float distance);
                if (distance <= radius + enemy.Kind.Radius)
                {
                    found.Add(enemy);
                }
            }
        }

        return found;
    }

    /// <summary>Takes one away quietly - no kill, nothing dropped (a crate left far behind). It is gone after the next <see cref="Update"/>.</summary>
    public void Vanish(Enemy enemy)
    {
        enemy.Health = 0f;
        enemy.DeadFor = DeathDuration;
    }

    /// <summary>Takes every enemy away at once (a restart). Returns them so their view can be cleared.</summary>
    public List<Enemy> Clear()
    {
        var all = _enemies.ToList();
        _enemies.Clear();
        _newlyKilled.Clear();
        _summoned.Clear();
        _strikes.Clear();
        _bolts.Clear();
        _blasts.Clear();
        _spawnTimer = 0f;
        _gridStale = true;
        return all;
    }

    /// <summary>Files the enemies in the grid again if any have moved, arrived or gone since it was last done.</summary>
    private void RefreshGrid()
    {
        if (_gridStale)
        {
            _grid.Rebuild(_enemies);
            _gridStale = false;
        }
    }

    public void ResetKills()
    {
        Kills = 0;
        DamageDealt = 0f;
    }

    private void SpawnTowardTarget(float deltaSeconds, Vector3D<float> playerFeet, Func<float, float, float?> groundAt)
    {
        _spawnTimer -= deltaSeconds;
        int alive = AliveCount;
        for (int spawned = 0; _spawnTimer <= 0f && alive < TargetCount && spawned < MaxSpawnsPerFrame; spawned++)
        {
            _spawnTimer += SpawnInterval;
            if (TryPickSpawnPoint(playerFeet, groundAt, out var point))
            {
                Spawn(point, PickFromMix());
                alive++;
            }
        }

        if (alive >= TargetCount)
        {
            _spawnTimer = MathF.Max(_spawnTimer, 0f);   // no stored-up burst once the field is full
        }
    }

    /// <summary>A kind from <see cref="Mix"/> by its shares, or null (the plain <see cref="Kind"/>) for the rest.</summary>
    private EnemyKind? PickFromMix()
    {
        if (Mix.Count == 0)
        {
            return null;
        }

        double roll = _random.NextDouble();
        foreach (var (kind, share) in Mix)
        {
            if ((roll -= share) < 0.0)
            {
                return kind;
            }
        }

        return null;
    }

    private bool TryPickSpawnPoint(Vector3D<float> playerFeet, Func<float, float, float?> groundAt, out Vector3D<float> point)
    {
        for (int attempt = 0; attempt < 8; attempt++)
        {
            float angle = (float)_random.NextDouble() * MathF.Tau;
            float distance = SpawnMinDistance + (float)_random.NextDouble() * (SpawnMaxDistance - SpawnMinDistance);
            float x = playerFeet.X + MathF.Sin(angle) * distance;
            float z = playerFeet.Z + MathF.Cos(angle) * distance;
            if (groundAt(x, z) is { } ground)
            {
                point = new Vector3D<float>(x, ground, z);
                return true;
            }
        }

        point = default;
        return false;
    }

    private void Move(Enemy enemy, float deltaSeconds, PlayerTarget player, Func<float, float, float?> groundAt)
    {
        var kind = enemy.Kind;
        if (kind.IsProp)
        {
            return;   // a crate just stands there
        }

        UpdatePhase(enemy);
        enemy.ChilledFor = MathF.Max(0f, enemy.ChilledFor - deltaSeconds);
        if (enemy.FrozenFor > 0f)
        {
            enemy.FrozenFor = MathF.Max(0f, enemy.FrozenFor - deltaSeconds);
            return;   // frozen solid: no step, no claw, and an attack under way waits
        }

        enemy.ContactCooldown = MathF.Max(0f, enemy.ContactCooldown - deltaSeconds);
        enemy.AttackCooldown = MathF.Max(0f, enemy.AttackCooldown - deltaSeconds);

        if (enemy.Attack is not null)
        {
            RunAttack(enemy, deltaSeconds, player, groundAt);
            return;
        }

        var toPlayer = Geometry.FlatDirection(enemy.Position, player.Feet, out float distance);

        if (distance > LeashDistance && TryPickSpawnPoint(player.Feet, groundAt, out var nearer))
        {
            enemy.Position = nearer;   // lost far behind: bring it back into the fight
            return;
        }

        if (distance > 1e-3f)
        {
            enemy.Yaw = MathF.Atan2(toPlayer.X, toPlayer.Z);
        }

        if (enemy.AttackCooldown <= 0f && TryStartAttack(enemy, distance, player, groundAt))
        {
            return;
        }

        // Walk at the player (a ranged kind stops once it is close enough to shoot), eased off by any other enemy close enough to crowd it, so a pack spreads around
        // the player instead of stacking into one. The bigger of two enemies gives way less.
        var step = kind.StandOff > 0f && distance <= kind.StandOff ? Vector3D<float>.Zero : toPlayer * enemy.WalkSpeed;
        foreach (var other in _grid.Near(enemy.Position.X, enemy.Position.Z, kind.Radius + MaxEnemyRadius + 0.15f))
        {
            if (other == enemy || !other.IsAlive)
            {
                continue;
            }

            float spacing = kind.Radius + other.Kind.Radius + 0.15f;
            var away = Geometry.FlatDirection(other.Position, enemy.Position, out float apart);
            if (apart < spacing)
            {
                float giveWay = 2f * other.Kind.Radius / (kind.Radius + other.Kind.Radius);
                var push = apart > 1e-4f ? away : new Vector3D<float>(MathF.Sin(enemy.Phase), 0f, MathF.Cos(enemy.Phase));
                step += push * enemy.WalkSpeed * (1f - apart / spacing) * 1.5f * giveWay;
            }
        }

        var position = enemy.Position + step * deltaSeconds;

        // Stop at the player's edge rather than walk into them, and claw while touching.
        float reach = kind.Radius + PlayerRadius;
        var fromPlayer = Geometry.FlatDirection(player.Feet, position, out float gap);
        if (gap < reach)
        {
            var push = gap > 1e-4f ? fromPlayer : -toPlayer;
            position = new Vector3D<float>(player.Feet.X + push.X * reach, position.Y, player.Feet.Z + push.Z * reach);
        }

        bool touching = gap <= reach + 0.1f && MathF.Abs(position.Y - player.Feet.Y) < kind.Height;
        if (touching && enemy.ContactCooldown <= 0f && Strike(enemy, kind.ContactDamage * enemy.DamageScale, player, out _))
        {
            enemy.ContactCooldown = kind.ContactInterval;
        }

        if (groundAt(position.X, position.Z) is { } ground)
        {
            enemy.Position = new Vector3D<float>(position.X, ground, position.Z);
        }
    }

    /// <summary>A boss whose health has fallen past its next stage's mark goes into it (and on past any it skipped), and the change is noted.</summary>
    private void UpdatePhase(Enemy enemy)
    {
        var phases = enemy.Kind.Phases;
        if (phases.Count == 0)
        {
            return;
        }

        float share = enemy.Health / enemy.MaxHealth;
        int reached = enemy.PhaseIndex;
        while (reached + 1 < phases.Count && share <= phases[reached + 1].Below)
        {
            reached++;
        }

        if (reached != enemy.PhaseIndex)
        {
            enemy.PhaseIndex = reached;
            enemy.AttackCooldown = MathF.Min(enemy.AttackCooldown, 0.5f);
            _phaseChanges.Add((enemy, phases[reached]));
        }
    }

    /// <summary>Starts one of the enemy's attacks that suits how far away the player is, picked at random. False if none does.</summary>
    private bool TryStartAttack(Enemy enemy, float distance, PlayerTarget player, Func<float, float, float?> groundAt)
    {
        var usable = enemy.AttacksNow.Where(a => distance >= a.MinRange && distance <= a.MaxRange).ToList();
        if (usable.Count == 0)
        {
            return false;
        }

        var attack = usable[_random.Next(usable.Count)];
        StartAttack(enemy, attack, player, groundAt);
        enemy.ChainLeft = attack.Chain;
        return true;
    }

    /// <summary>Begins <paramref name="attack"/>'s wind-up, fixing where it is aimed.</summary>
    private static void StartAttack(Enemy enemy, AttackSpec attack, PlayerTarget player, Func<float, float, float?> groundAt)
    {
        var toPlayer = Geometry.FlatDirection(enemy.Position, player.Feet, out _);

        enemy.Attack = attack;
        enemy.AttackPhase = AttackPhase.WindUp;
        enemy.PhaseTime = 0f;
        enemy.AttackLanded = false;
        enemy.AttackOrigin = enemy.Position;
        enemy.AttackTarget = attack.Type switch
        {
            // The lane is fixed the moment it is shown: the charge goes where the lane points, not where the player has moved to.
            AttackType.Lunge => enemy.Position + toPlayer * attack.Reach,

            // The landing spot is where the player stood when the circle appeared - so the circle is the warning, and leaving it is the answer.
            AttackType.LeapSlam => new Vector3D<float>(player.Feet.X, groundAt(player.Feet.X, player.Feet.Z) ?? player.Feet.Y, player.Feet.Z),
            _ => enemy.Position,
        };
    }

    private void RunAttack(Enemy enemy, float deltaSeconds, PlayerTarget player, Func<float, float, float?> groundAt)
    {
        var attack = enemy.Attack!;
        enemy.PhaseTime += deltaSeconds;

        switch (enemy.AttackPhase)
        {
            case AttackPhase.WindUp:
                FaceAttack(enemy, player);
                if (enemy.PhaseTime >= attack.WindUp)
                {
                    enemy.PhaseTime -= attack.WindUp;
                    enemy.AttackPhase = AttackPhase.Active;
                    if (attack.Type == AttackType.Summon)
                    {
                        Summon(enemy, (int)attack.Reach, groundAt);
                    }
                    else if (attack.Type == AttackType.Shoot)
                    {
                        Shoot(enemy, attack, player, groundAt);
                    }
                    else if (attack.Type == AttackType.Barrage)
                    {
                        Barrage(enemy, attack, player, groundAt);
                    }
                }

                break;

            case AttackPhase.Active:
                float t = Math.Clamp(enemy.PhaseTime / attack.Active, 0f, 1f);
                RunActive(enemy, attack, t, player, groundAt);
                if (enemy.PhaseTime >= attack.Active)
                {
                    if (enemy.ChainLeft > 0)
                    {
                        // Straight into it again, aimed afresh, with half the wind-up: a second leap, the next ring.
                        enemy.ChainLeft--;
                        StartAttack(enemy, attack, player, groundAt);
                        enemy.PhaseTime = attack.WindUp * 0.5f;
                        break;
                    }

                    enemy.PhaseTime -= attack.Active;
                    enemy.AttackPhase = AttackPhase.Recover;
                }

                break;

            case AttackPhase.Recover:
                if (enemy.PhaseTime >= attack.Recover)
                {
                    enemy.Attack = null;
                    enemy.AttackCooldown = enemy.AttackCooldownNow;
                }

                break;
        }
    }

    private void RunActive(Enemy enemy, AttackSpec attack, float t, PlayerTarget player, Func<float, float, float?> groundAt)
    {
        switch (attack.Type)
        {
            case AttackType.Lunge:
            {
                var flat = enemy.AttackOrigin + (enemy.AttackTarget - enemy.AttackOrigin) * t;
                enemy.Position = new Vector3D<float>(flat.X, groundAt(flat.X, flat.Z) ?? enemy.Position.Y, flat.Z);
                Geometry.FlatDirection(enemy.Position, player.Feet, out float gap);
                if (!enemy.AttackLanded && gap <= enemy.Kind.Radius + attack.HitWidth + PlayerRadius && MathF.Abs(enemy.Position.Y - player.Feet.Y) < enemy.Kind.Height)
                {
                    enemy.AttackLanded = true;
                    Hit(enemy, attack, player, enemy.AttackTarget - enemy.AttackOrigin);
                }

                break;
            }

            case AttackType.LeapSlam:
            {
                var flat = enemy.AttackOrigin + (enemy.AttackTarget - enemy.AttackOrigin) * t;
                float groundY = enemy.AttackOrigin.Y + (enemy.AttackTarget.Y - enemy.AttackOrigin.Y) * t;
                enemy.Position = new Vector3D<float>(flat.X, groundY + 4f * attack.LeapHeight * t * (1f - t), flat.Z);
                if (t >= 1f && !enemy.AttackLanded)
                {
                    enemy.AttackLanded = true;
                    enemy.Position = enemy.AttackTarget;
                    var away = Geometry.FlatDirection(enemy.AttackTarget, player.Feet, out float gap);
                    if (gap <= attack.Reach + PlayerRadius)
                    {
                        Hit(enemy, attack, player, away == Vector3D<float>.Zero ? new Vector3D<float>(MathF.Sin(enemy.Yaw), 0f, MathF.Cos(enemy.Yaw)) : away);
                    }
                }

                break;
            }

            case AttackType.Shockwave:
            {
                var away = Geometry.FlatDirection(enemy.AttackOrigin, player.Feet, out float distance);
                if (!enemy.AttackLanded && player.Grounded && MathF.Abs(distance - enemy.ShockwaveRadius) <= attack.HitWidth + PlayerRadius)
                {
                    enemy.AttackLanded = true;   // one hit per wave
                    Hit(enemy, attack, player, away);
                }

                break;
            }
        }
    }

    /// <summary>An attack's hit on the player: its damage, then its shove and stun. A blocked blow stops all three.</summary>
    private void Hit(Enemy enemy, AttackSpec attack, PlayerTarget player, Vector3D<float> shove)
    {
        if (Strike(enemy, attack.Damage * enemy.DamageScale, player, out bool blocked) && !blocked)
        {
            player.Condition.Knock(shove, attack.Knockback);
            if (attack.Stun > 0f)
            {
                player.Condition.Stun(attack.Stun);
            }
        }
    }

    /// <summary>
    /// A blow reaching the player: turned aside by a block (a roll against <see cref="PlayerTarget.BlockChance"/>) or landing. Either way it connects and goes on
    /// <see cref="Strikes"/>. False if the player can't be hurt just now (dead, or in the grace after the last hit); then nothing happens.
    /// </summary>
    private bool Strike(Enemy enemy, float damage, PlayerTarget player, out bool blocked)
    {
        blocked = false;
        if (!player.Health.CanBeHurt)
        {
            return false;
        }

        if (player.BlockChance > 0f && _random.NextDouble() < player.BlockChance)
        {
            blocked = true;
            player.Health.Deflect();
        }
        else if (!player.Health.TakeDamage(damage))
        {
            return false;
        }

        _strikes.Add(new Strike(enemy, damage, blocked));
        return true;
    }

    /// <summary>Turns toward what the attack is aimed at: the lane or the landing spot while they are shown, otherwise the player.</summary>
    private static void FaceAttack(Enemy enemy, PlayerTarget player)
    {
        var at = enemy.Attack!.Type is AttackType.Lunge or AttackType.LeapSlam ? enemy.AttackTarget : player.Feet;
        var toward = Geometry.FlatDirection(enemy.Position, at, out float distance);
        if (distance > 1e-3f)
        {
            enemy.Yaw = MathF.Atan2(toward.X, toward.Z);
        }
    }

    /// <summary>Where a shooter's bolt leaves from: its weapon, this high above its feet and this far in front.</summary>
    public const float ShotHeight = 1.05f;
    public const float ShotForward = 0.85f;

    /// <summary>How tall the player is, for a bolt hitting them: a capsule from the feet up this far.</summary>
    public const float PlayerHeight = 1.8f;

    /// <summary>
    /// Looses a shot from <paramref name="shooter"/>'s weapon where the player stands now - a bolt at their middle, a splash shot at the ground under them. Moving
    /// after the glow peaks is how to dodge it.
    /// </summary>
    private void Shoot(Enemy shooter, AttackSpec attack, PlayerTarget player, Func<float, float, float?> groundAt)
    {
        var facing = new Vector3D<float>(MathF.Sin(shooter.Yaw), 0f, MathF.Cos(shooter.Yaw));
        var from = shooter.Position + new Vector3D<float>(0f, ShotHeight, 0f) + facing * ShotForward;
        var at = attack.Splash > 0f
            ? new Vector3D<float>(player.Feet.X, groundAt(player.Feet.X, player.Feet.Z) ?? player.Feet.Y, player.Feet.Z)
            : player.Feet + new Vector3D<float>(0f, PlayerHeight * 0.5f, 0f);
        var toward = at - from;
        var direction = toward.LengthSquared > 1e-6f ? Vector3D.Normalize(toward) : facing;
        _bolts.Add(new EnemyBolt
        {
            Shooter = shooter,
            Model = attack.ProjectileModel ?? "ghoul_bolt.glb",
            Target = at,
            Splash = attack.Splash,
            Position = from,
            Velocity = direction * attack.ProjectileSpeed,
            FlightLeft = attack.Reach / attack.ProjectileSpeed,
            Damage = attack.Damage * shooter.DamageScale * RangedDamageTaken,
            Radius = attack.HitWidth,
            Knockback = attack.Knockback,
        });
    }

    /// <summary>How high above its landing spot a barrage's fireball starts to fall.</summary>
    public const float BarrageHeight = 16f;

    /// <summary>
    /// Calls <paramref name="attack"/>'s fireballs down from the sky: the first on the spot where the player stands, the rest scattered within its reach around it.
    /// Each falls from high above its spot, a little aslant, at about the attack's speed - its landing marked the whole way down.
    /// </summary>
    private void Barrage(Enemy caster, AttackSpec attack, PlayerTarget player, Func<float, float, float?> groundAt)
    {
        for (int i = 0; i < Math.Max(1, attack.Count); i++)
        {
            float angle = (float)_random.NextDouble() * MathF.Tau;
            float distance = i == 0 ? 0f : attack.Reach * MathF.Sqrt((float)_random.NextDouble());
            float x = player.Feet.X + MathF.Sin(angle) * distance;
            float z = player.Feet.Z + MathF.Cos(angle) * distance;
            if (groundAt(x, z) is not { } ground)
            {
                continue;
            }

            var target = new Vector3D<float>(x, ground, z);
            var from = target + new Vector3D<float>(MathF.Sin(angle + 1f) * 3f, BarrageHeight, MathF.Cos(angle + 1f) * 3f);
            var fall = target - from;
            float speed = attack.ProjectileSpeed * (0.85f + 0.3f * (float)_random.NextDouble());   // not all landing at once
            _bolts.Add(new EnemyBolt
            {
                Shooter = caster,
                Model = attack.ProjectileModel ?? "ghoul_fireball.glb",
                Target = target,
                Splash = attack.Splash,
                Position = from,
                Velocity = Vector3D.Normalize(fall) * speed,
                FlightLeft = fall.Length / speed + 1f,
                Damage = attack.Damage * caster.DamageScale * RangedDamageTaken,
                Radius = 0.4f,
                Knockback = attack.Knockback,
            });
        }
    }

    /// <summary>
    /// Moves the shots in flight. A bolt that reaches the player strikes them (a shield can block it, a blow in the grace after the last hit glances off) and is
    /// spent; one that meets the ground or runs out of flight is gone. A splash shot bursts on the player or on the ground, striking the player if they are within
    /// its splash.
    /// </summary>
    private void MoveBolts(float deltaSeconds, PlayerTarget player, Func<float, float, float?> groundAt)
    {
        var bottom = player.Feet + new Vector3D<float>(0f, PlayerRadius, 0f);
        var top = player.Feet + new Vector3D<float>(0f, PlayerHeight - PlayerRadius, 0f);
        for (int i = _bolts.Count - 1; i >= 0; i--)
        {
            var bolt = _bolts[i];
            var from = bolt.Position;
            var to = from + bolt.Velocity * deltaSeconds;
            bolt.FlightLeft -= deltaSeconds;

            if (Geometry.SegmentDistance(from, to, bottom, top, out float along) <= PlayerRadius + bolt.Radius)
            {
                if (bolt.Splash > 0f)
                {
                    Burst(bolt, from + (to - from) * along, player, bottom, top);
                }
                else if (Strike(bolt.Shooter, bolt.Damage, player, out bool blocked) && !blocked)
                {
                    player.Condition.Knock(bolt.Velocity, bolt.Knockback);
                }

                _bolts.RemoveAt(i);
                continue;
            }

            if (groundAt(to.X, to.Z) is { } ground && to.Y < ground)
            {
                if (bolt.Splash > 0f)
                {
                    Burst(bolt, new Vector3D<float>(to.X, ground, to.Z), player, bottom, top);
                }

                _bolts.RemoveAt(i);
                continue;
            }

            if (bolt.FlightLeft <= 0f)
            {
                _bolts.RemoveAt(i);
                continue;
            }

            bolt.Position = to;
        }
    }

    /// <summary>A splash shot bursting at <paramref name="centre"/>: it strikes the player if any of them is within its splash, shoving them away from it.</summary>
    private void Burst(EnemyBolt bolt, Vector3D<float> centre, PlayerTarget player, Vector3D<float> bottom, Vector3D<float> top)
    {
        _blasts.Add(new EnemyBlast { Centre = centre, Radius = bolt.Splash });
        if (Geometry.SegmentDistance(centre, centre, bottom, top, out _) <= bolt.Splash + PlayerRadius
            && Strike(bolt.Shooter, bolt.Damage, player, out bool blocked) && !blocked)
        {
            var away = Geometry.FlatDirection(centre, player.Feet, out _);
            player.Condition.Knock(away == Vector3D<float>.Zero ? bolt.Velocity : away, bolt.Knockback);
        }
    }

    /// <summary>Calls <paramref name="count"/> fodder up in a ring around <paramref name="caller"/> (added after this frame's moves).</summary>
    private void Summon(Enemy caller, int count, Func<float, float, float?> groundAt)
    {
        float ring = caller.Kind.Radius + 3f;
        for (int i = 0; i < count; i++)
        {
            float angle = MathF.Tau * i / count + caller.Phase;
            float x = caller.Position.X + MathF.Sin(angle) * ring;
            float z = caller.Position.Z + MathF.Cos(angle) * ring;
            if (groundAt(x, z) is { } ground)
            {
                _summoned.Add((Kind, new Vector3D<float>(x, ground, z)));
            }
        }
    }
}
