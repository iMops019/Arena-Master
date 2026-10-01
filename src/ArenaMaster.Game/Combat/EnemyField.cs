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
    public Enemy(int id, EnemyKind kind, Vector3D<float> position, EnemyScaling scaling, RarityTraits? rarity = null)
    {
        Id = id;
        Kind = kind;
        Rarity = rarity ?? RarityTraits.Normal;
        Position = position;
        MaxHealth = kind.MaxHealth * scaling.Health * Rarity.Health;
        Health = MaxHealth;
        SeenHealth = MaxHealth;
        Speed = kind.Speed * scaling.Speed * Rarity.Speed;
        DamageScale = scaling.Damage * Rarity.Damage;
    }

    private float _attackSpeedBoost = 1f;

    public int Id { get; }

    public EnemyKind Kind { get; }

    /// <summary>How rare it spawned (Magic, Rare, Legendary), and what that does to it.</summary>
    public RarityTraits Rarity { get; }

    /// <summary>Its name with its rarity: "Legendary Ghoul".</summary>
    public string Name => Rarity.Prefix + Kind.Name;

    /// <summary>What its gem is worth: its kind's experience, raised by its rarity.</summary>
    public int Experience => (int)MathF.Round(Kind.Experience * Rarity.Experience);

    /// <summary>How much faster than its kind it attacks: its wind-ups, blows, recoveries, breathers and claws all run this much quicker.</summary>
    public float AttackSpeed => Rarity.AttackSpeed * _attackSpeedBoost;

    public Vector3D<float> Position { get; set; }

    public float Yaw { get; set; }

    public float MaxHealth { get; }

    public float Health { get; set; }

    public float Speed { get; }

    /// <summary>What its contact and attack damage are multiplied by.</summary>
    public float DamageScale { get; private set; }

    /// <summary>Seconds left of a stage's roar (see <see cref="BossPhase.Roar"/>): standing, roaring, and nothing hurts it. And how long the roar was.</summary>
    public float RoarLeft { get; set; }

    public float RoarSeconds { get; set; }

    public bool IsRoaring => RoarLeft > 0f;

    /// <summary>The rage it has gained (the Marauder's frenzy), for the HUD; 0 for none.</summary>
    public int Rage { get; private set; }

    public bool IsEnraged => Rage > 0;

    /// <summary>A stage's boosts take hold: its damage and attack speed raised, and its rage.</summary>
    public void Enrage(BossPhase phase)
    {
        DamageScale *= phase.DamageBoost;
        _attackSpeedBoost *= phase.AttackSpeedBoost;
        Rage += phase.Rage;
    }

    /// <summary>Seconds until a lasting attack (a whirlwind) may hit again.</summary>
    public float TickLeft { get; set; }

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

    /// <summary>
    /// Seconds left reeling from a blow (a quake): no step, no claw, an attack under way held - like a freeze, but it isn't frozen, so nothing that works on
    /// frozen enemies counts.
    /// </summary>
    public float StaggeredFor { get; set; }

    public bool IsStaggered => StaggeredFor > 0f;

    /// <summary>Staggers it for <paramref name="seconds"/> (or keeps a longer stagger already on it).</summary>
    public void Stagger(float seconds) => StaggeredFor = MathF.Max(StaggeredFor, seconds);

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

    /// <summary>Where it stood when it chose its attack (not a chained one or a follow-up): where a <see cref="AttackType.Retreat"/> takes it back to.</summary>
    public Vector3D<float> Home { get; set; }

    /// <summary>A stalker's mood: stalking (circling, waiting for an opening) or fleeing, and how long a flight lasts yet.</summary>
    public StalkMood Mood { get; set; }

    public float MoodLeft { get; set; }

    /// <summary>A stalker's health when it last looked, so a hit (from anything) spooks it.</summary>
    public float SeenHealth { get; set; }

    /// <summary>Whether the attack has already hit (each attack hits at most once).</summary>
    public bool AttackLanded { get; set; }

    /// <summary>0 to 1 through the wind-up - how far along a telegraph is.</summary>
    public float WindUpProgress => Attack is { } a && AttackPhase == AttackPhase.WindUp ? Math.Clamp(PhaseTime / a.WindUp, 0f, 1f) : 0f;

    /// <summary>How far a shockwave has spread from its centre right now, or 0 if none is spreading.</summary>
    public float ShockwaveRadius => Attack is { Type: AttackType.Shockwave } a && AttackPhase == AttackPhase.Active
        ? Kind.Radius + (a.Reach - Kind.Radius) * Math.Clamp(PhaseTime / a.Active, 0f, 1f)
        : 0f;
}

/// <summary>A stalker's mood (see <see cref="EnemyBehaviour.Stalk"/>).</summary>
internal enum StalkMood
{
    Stalk,
    Flee,
}

/// <summary>
/// Where the player is and what enemies can do to them this frame. <paramref name="BlockChance"/> (0 to 1) is the chance a blow is turned aside completely: no
/// damage, no shove, no stun. It's 0 for a class without a shield. <paramref name="Facing"/> is the flat way the player is looking (zero if not known): a
/// stalker watches it for its opening.
/// </summary>
internal readonly record struct PlayerTarget(Vector3D<float> Feet, bool Grounded, PlayerHealth Health, PlayerCondition Condition, float BlockChance = 0f,
    Vector3D<float> Facing = default);

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

    /// <summary>What marks a splash shot's landing spot while it flies, and what it bursts in.</summary>
    public string MarkModel { get; init; } = EnemyView.LandingModel;

    public string BurstModel { get; init; } = EnemyView.BlastModel;

    public Vector3D<float> Position { get; set; }

    public Vector3D<float> Velocity { get; init; }

    /// <summary>Seconds of flight left.</summary>
    public float FlightLeft { get; set; }

    public float Damage { get; init; }

    public float Radius { get; init; }

    public float Knockback { get; init; }
}

/// <summary>
/// A Ghoul Tactician's bomb: lobbed high, falling, bouncing along the ground toward where the player stood (<see cref="BouncesLeft"/> more times), then
/// rolling to a stop, when it goes off. It goes off at once on touching the player, a tree or a rock, or running into a slope too steep to roll up.
/// </summary>
internal sealed class EnemyBomb
{
    public required Enemy Thrower { get; init; }

    /// <summary>The model it is drawn with.</summary>
    public required string Model { get; init; }

    /// <summary>Its middle.</summary>
    public Vector3D<float> Position { get; set; }

    public Vector3D<float> Velocity { get; set; }

    public float Radius { get; init; }

    /// <summary>How far its blast reaches.</summary>
    public float Splash { get; init; }

    public float Damage { get; init; }

    public float Knockback { get; init; }

    /// <summary>How many more times it bounces before it rolls.</summary>
    public int BouncesLeft { get; set; }

    /// <summary>Whether it has bounced its last and is rolling along the ground to a stop.</summary>
    public bool Rolling { get; set; }

    /// <summary>Seconds since it was thrown.</summary>
    public float Age { get; set; }
}

/// <summary>A spot that draws the enemies near it to it for a while (a totem): see <see cref="EnemyField.AddLure"/>.</summary>
internal sealed class EnemyLure
{
    public Vector3D<float> Spot { get; init; }

    /// <summary>How near it an enemy must be to be drawn to it.</summary>
    public float Reach { get; init; }

    /// <summary>Seconds until it stops drawing them.</summary>
    public float Left { get; set; }
}

/// <summary>A splash shot bursting (a fireball, a bomb), for the view: where, how wide, how long ago, and the model it is drawn with.</summary>
internal sealed class EnemyBlast
{
    public Vector3D<float> Centre { get; init; }

    public string Model { get; init; } = EnemyView.BlastModel;

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
    float FreezeSeconds = 0f,
    float ExecuteBelow = 0f,
    float WoundedMultiplier = 1f)
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
    private readonly List<EnemyBomb> _bombs = new();
    private readonly List<(Enemy Boss, BossPhase Phase)> _phaseChanges = new();
    private readonly List<EnemyLure> _lures = new();
    private readonly List<Enemy> _legendaries = new();
    private readonly Dictionary<AttackType, int> _fodderAttacking = new();
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
    /// The kinds some of the fodder spawns as instead of <see cref="Kind"/> (the Crossbow Ghoul, the Ghoul Mage, the Beast Rider, the Tactician), each with the share that does (0 to 1, and
    /// together at most 1).
    /// </summary>
    public IReadOnlyList<(EnemyKind Kind, float Share)> Mix { get; set; } = Array.Empty<(EnemyKind, float)>();

    /// <summary>
    /// The chance each spawning enemy (fodder and elites, never a boss or a crate) is Magic, Rare or Legendary. None unless set (the directors set it each
    /// frame of a run).
    /// </summary>
    public RarityOdds RarityOdds { get; set; } = RarityOdds.None;

    /// <summary>The Legendary enemies that have turned up since this was last asked, for an announcement.</summary>
    public List<Enemy> TakeLegendarySpawns()
    {
        var spawned = _legendaries.ToList();
        _legendaries.Clear();
        return spawned;
    }

    /// <summary>The enemies' shots in flight.</summary>
    public IReadOnlyList<EnemyBolt> Bolts => _bolts;

    /// <summary>The Ghoul Tacticians' bombs, flying, bouncing or rolling.</summary>
    public IReadOnlyList<EnemyBomb> Bombs => _bombs;

    /// <summary>
    /// Whether a ball of the given radius at a point touches a solid obstacle (a tree, a rock): a bomb that does goes off. In the game this is the engine's
    /// <c>EngineWindow.TouchesObstacle</c>; null for none.
    /// </summary>
    public Func<Vector3D<float>, float, bool>? Obstacles { get; set; }

    /// <summary>
    /// The ground a leap away from the player must land on (the boss arena inside its walls: a flat centre and radius), or null for anywhere. Set by a boss
    /// hunt.
    /// </summary>
    public (Vector2D<float> Centre, float Radius)? Arena { get; set; }

    /// <summary>
    /// The way an enemy walks to the player when it can't go straight (a wall between them): given where it is and where the player is, the flat way to walk, or
    /// null to walk straight at them. An enemy steered round a wall doesn't attack until it has a clear line again. Null for no walls (straight, always). In the
    /// game it is the cave's way through its tunnels (<c>World/CaveFlow</c>).
    /// </summary>
    public Func<Vector3D<float>, Vector3D<float>, Vector3D<float>?>? Steer { get; set; }

    /// <summary>Whether an enemy may be put at a spot (x, z) as it spawns or is brought back: in the cave, only where there is a way to the player. Null for anywhere.</summary>
    public Func<float, float, bool>? SpawnFilter { get; set; }

    /// <summary>How long a burst lasts, for the view.</summary>
    public const float BlastSeconds = 0.35f;

    /// <summary>An enemy drawn to a lure stops this far from its spot.</summary>
    public const float LureStop = 1f;

    /// <summary>The lures drawing enemies now.</summary>
    public IReadOnlyList<EnemyLure> Lures => _lures;

    /// <summary>
    /// Puts a lure at <paramref name="spot"/> for <paramref name="seconds"/>: every enemy within <paramref name="reach"/> of it (but a boss, which pays it no mind)
    /// walks to it instead of the player, and starts no attack while it is drawn there - though it still claws a player standing in its way.
    /// </summary>
    public EnemyLure AddLure(Vector3D<float> spot, float reach, float seconds)
    {
        var lure = new EnemyLure { Spot = spot, Reach = reach, Left = seconds };
        _lures.Add(lure);
        return lure;
    }

    /// <summary>The nearest lure <paramref name="enemy"/> is within reach of, or null for none (and always for a boss).</summary>
    private EnemyLure? LureFor(Enemy enemy)
    {
        if (_lures.Count == 0 || enemy.Kind.Tier == EnemyTier.Boss)
        {
            return null;
        }

        EnemyLure? best = null;
        float bestDistance = float.MaxValue;
        foreach (var lure in _lures)
        {
            Geometry.FlatDirection(enemy.Position, lure.Spot, out float distance);
            if (distance <= lure.Reach && distance < bestDistance)
            {
                best = lure;
                bestDistance = distance;
            }
        }

        return best;
    }

    /// <summary>Splash shots bursting right now.</summary>
    public IReadOnlyList<EnemyBlast> Blasts => _blasts;

    /// <summary>What every hit the player lands does besides its damage (see <see cref="HitEffects"/>). Set at the start of a run.</summary>
    public HitEffects HitEffects { get; set; } = HitEffects.None;

    /// <summary>What every hit the player lands is multiplied by this frame, from items that build up during a run (Bloodfury, Berserker's Hide). Kept up to date by <see cref="Items.ItemEffects"/>.</summary>
    public float DamageBoost { get; set; } = 1f;

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

    /// <summary>
    /// Puts an enemy (of <see cref="Kind"/> unless <paramref name="kind"/> says otherwise, at the current <see cref="Scaling"/>) at <paramref name="position"/>, its
    /// feet. Its rarity is <paramref name="rarity"/> if given, otherwise rolled from <see cref="RarityOdds"/> (a boss or a crate is always normal).
    /// </summary>
    public Enemy Spawn(Vector3D<float> position, EnemyKind? kind = null, MonsterRarity? rarity = null)
    {
        kind ??= Kind;
        var scaling = HealthBonus != 0f ? Scaling with { Health = Scaling.Health * (1f + HealthBonus) } : Scaling;
        var rolled = rarity ?? (kind.Tier == EnemyTier.Boss || kind.IsProp ? MonsterRarity.Normal : RarityOdds.Pick(_random.NextDouble()));
        var enemy = new Enemy(_nextId++, kind, position, scaling, RarityTraits.Of(rolled)) { Phase = (float)_random.NextDouble() * MathF.Tau };
        if (rolled == MonsterRarity.Legendary)
        {
            _legendaries.Add(enemy);
        }

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
        foreach (var lure in _lures)
        {
            lure.Left -= deltaSeconds;
        }

        _lures.RemoveAll(l => l.Left <= 0f);
        SpawnTowardTarget(deltaSeconds, player.Feet, groundAt);
        RefreshGrid();
        CountFodderAttacks();

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
        MoveBombs(deltaSeconds, player, groundAt);
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
        if (!enemy.IsAlive || enemy.IsRoaring)
        {
            return false;   // (a roaring boss shrugs everything off)
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

        if (enemy.Health < enemy.MaxHealth * 0.5f)
        {
            amount *= effects.WoundedMultiplier;
        }

        amount *= DamageBoost;
        if (effects.ExecuteBelow > 0f && enemy.Kind.Tier != EnemyTier.Boss && !enemy.Kind.IsProp && enemy.Health <= enemy.MaxHealth * effects.ExecuteBelow)
        {
            amount = MathF.Max(amount, enemy.Health);   // finished off
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

    /// <summary>
    /// The nearest live enemy whose body reaches into the flat circle of <paramref name="radius"/> around <paramref name="centre"/> - a crate only when no enemy
    /// does - or null for none: what a hero turns to face.
    /// </summary>
    public Enemy? Nearest(Vector3D<float> centre, float radius)
    {
        Enemy? best = null;
        float bestDistance = float.MaxValue;
        foreach (var enemy in Within(centre, radius))
        {
            Geometry.FlatDirection(centre, enemy.Position, out float distance);
            bool better = best is null
                || (best.Kind.IsProp && !enemy.Kind.IsProp)
                || (best.Kind.IsProp == enemy.Kind.IsProp && distance < bestDistance);
            if (better)
            {
                best = enemy;
                bestDistance = distance;
            }
        }

        return best;
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
        _legendaries.Clear();
        _strikes.Clear();
        _bolts.Clear();
        _bombs.Clear();
        _blasts.Clear();
        _lures.Clear();
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
        for (int attempt = 0; attempt < 12; attempt++)
        {
            float angle = (float)_random.NextDouble() * MathF.Tau;
            float distance = SpawnMinDistance + (float)_random.NextDouble() * (SpawnMaxDistance - SpawnMinDistance);
            float x = playerFeet.X + MathF.Sin(angle) * distance;
            float z = playerFeet.Z + MathF.Cos(angle) * distance;
            if (groundAt(x, z) is { } ground && SpawnFilter?.Invoke(x, z) != false)
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
        if (enemy.IsRoaring)
        {
            enemy.RoarLeft = MathF.Max(0f, enemy.RoarLeft - deltaSeconds);
            var toward = Geometry.FlatDirection(enemy.Position, player.Feet, out float away);
            if (away > 1e-3f)
            {
                enemy.Yaw = MathF.Atan2(toward.X, toward.Z);
            }

            if (!enemy.IsRoaring && enemy.CurrentPhase is { } roared)
            {
                enemy.Enrage(roared);   // the roar is over: the frenzy takes hold
                enemy.AttackCooldown = 0.3f;
            }

            return;   // no step, no claw, no attack while it roars
        }

        enemy.ChilledFor = MathF.Max(0f, enemy.ChilledFor - deltaSeconds);
        if (enemy.FrozenFor > 0f)
        {
            enemy.FrozenFor = MathF.Max(0f, enemy.FrozenFor - deltaSeconds);
            return;   // frozen solid: no step, no claw, and an attack under way waits
        }

        if (enemy.StaggeredFor > 0f)
        {
            enemy.StaggeredFor = MathF.Max(0f, enemy.StaggeredFor - deltaSeconds);
            return;   // reeling: the same, but not frozen
        }

        enemy.ContactCooldown = MathF.Max(0f, enemy.ContactCooldown - deltaSeconds * enemy.AttackSpeed);
        enemy.AttackCooldown = MathF.Max(0f, enemy.AttackCooldown - deltaSeconds * enemy.AttackSpeed);

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

        Vector3D<float> step;
        if (LureFor(enemy) is { } lure)
        {
            // Drawn to a lure: over to it, and no attack started.
            var toLure = Geometry.FlatDirection(enemy.Position, lure.Spot, out float fromLure);
            step = fromLure > LureStop ? toLure * enemy.WalkSpeed : Vector3D<float>.Zero;
            if (fromLure > 1e-3f)
            {
                enemy.Yaw = MathF.Atan2(toLure.X, toLure.Z);
            }
        }
        else if (kind.Behaviour == EnemyBehaviour.Stalk)
        {
            if (Stalk(enemy, deltaSeconds, toPlayer, distance, player, groundAt, out step))
            {
                return;   // it pounced
            }
        }
        else if (Steer?.Invoke(enemy.Position, player.Feet) is { } way)
        {
            // A wall between it and the player: round by the way through, and no attacking until it has a clear line again.
            enemy.Yaw = MathF.Atan2(way.X, way.Z);
            step = way * enemy.WalkSpeed;
        }
        else
        {
            if (distance > 1e-3f)
            {
                enemy.Yaw = MathF.Atan2(toPlayer.X, toPlayer.Z);
            }

            if (enemy.AttackCooldown <= 0f && TryStartAttack(enemy, distance, player, groundAt))
            {
                return;
            }

            // Walk at the player (a ranged kind stops once it is close enough to shoot).
            step = kind.StandOff > 0f && distance <= kind.StandOff ? Vector3D<float>.Zero : toPlayer * enemy.WalkSpeed;
        }

        // Eased off by any other enemy close enough to crowd it, so a pack spreads around the player instead of stacking into one. The bigger of two enemies
        // gives way less.
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

    // A stalker's ways (the Ghoul Beast's; see EnemyBehaviour.Stalk). Distances in metres, times in seconds, speeds as shares of its walking speed.

    /// <summary>It circles this far from the player, and creeps in to this once it is behind them.</summary>
    public const float StalkRadius = 10f;
    public const float CreepRadius = 5.5f;

    /// <summary>Further off than this it just comes on at full speed.</summary>
    public const float StalkApproach = 16f;

    /// <summary>The player looking at it (their facing within about 30 degrees of it) from nearer than this spooks it.</summary>
    public const float SpookDistance = 9f;
    public const float SpookFacing = 0.85f;

    /// <summary>It has an opening when the player's facing points this far away from it (about 110 degrees and more): their back is turned.</summary>
    public const float BackTurned = -0.35f;

    /// <summary>How long it runs when spooked (between the two), and after each pounce.</summary>
    public const float FleeMin = 1.1f;
    public const float FleeMax = 1.9f;
    public const float StalkAfterPounce = 1.6f;

    /// <summary>Its pace prowling round the player, bolting away, and rushing in on an opening.</summary>
    public const float ProwlPace = 0.7f;
    public const float FleePace = 1.3f;
    public const float RushPace = 1.2f;

    /// <summary>
    /// A stalker's move for this frame (into <paramref name="step"/>), or its pounce: true if it pounced. Spooked (looked at from close by, or hurt), it bolts
    /// for a moment. With an opening - the player's back turned, or a stun - it rushes in and pounces once in range. Otherwise it circles, working round toward
    /// the player's back and creeping closer once it is there. It faces the way it goes.
    /// </summary>
    private bool Stalk(Enemy enemy, float deltaSeconds, Vector3D<float> toPlayer, float distance, PlayerTarget player, Func<float, float, float?> groundAt,
        out Vector3D<float> step)
    {
        var fromPlayer = -toPlayer;
        var facing = new Vector3D<float>(player.Facing.X, 0f, player.Facing.Z);
        bool knowsFacing = facing.LengthSquared > 1e-6f;
        if (knowsFacing)
        {
            facing = Vector3D.Normalize(facing);
        }

        float watched = knowsFacing ? Vector3D.Dot(facing, fromPlayer) : 0f;   // 1: the player looks right at it; -1: its back is to it
        bool opening = player.Condition.IsStunned || (knowsFacing && watched < BackTurned);
        bool hurt = enemy.Health < enemy.SeenHealth;
        enemy.SeenHealth = enemy.Health;
        enemy.MoodLeft -= deltaSeconds;

        if (enemy.Mood == StalkMood.Flee && enemy.MoodLeft <= 0f)
        {
            enemy.Mood = StalkMood.Stalk;
        }

        if (enemy.Mood == StalkMood.Stalk && !opening && (hurt || (watched > SpookFacing && distance < SpookDistance)))
        {
            Spook(enemy, FleeMin + (FleeMax - FleeMin) * (float)_random.NextDouble());
        }

        if (enemy.Mood == StalkMood.Flee)
        {
            // Away from the player, veering off to its circling side rather than straight back.
            step = Vector3D.Normalize(fromPlayer + Side(fromPlayer, enemy.Phase < MathF.PI) * 0.35f) * enemy.WalkSpeed * FleePace;
        }
        else if (opening)
        {
            var pounce = enemy.AttacksNow.FirstOrDefault(a => distance >= a.MinRange && distance <= a.MaxRange);
            if (pounce is not null && enemy.AttackCooldown <= 0f && MayStart(enemy, pounce))
            {
                enemy.Yaw = MathF.Atan2(toPlayer.X, toPlayer.Z);
                enemy.Home = enemy.Position;
                StartAttack(enemy, pounce, player, groundAt);
                Started(enemy, pounce);
                enemy.ChainLeft = pounce.Chain;
                step = Vector3D<float>.Zero;
                return true;
            }

            step = toPlayer * enemy.WalkSpeed * RushPace;   // closing in while it can
        }
        else if (distance > StalkApproach)
        {
            step = toPlayer * enemy.WalkSpeed;
        }
        else
        {
            // Round the player toward their back (or its own way, not knowing where they look), holding its distance - closer once it is behind them.
            var around = knowsFacing
                ? Side(fromPlayer, Vector3D.Dot(Side(fromPlayer, true), -facing) >= 0f)
                : Side(fromPlayer, enemy.Phase < MathF.PI);
            float behind = knowsFacing ? -watched : 0f;
            float want = behind > 0.5f ? CreepRadius : StalkRadius;
            float along = knowsFacing && behind > 0.9f ? 0.2f : 1f;   // nearly there: mostly closing in
            var way = around * along + fromPlayer * Math.Clamp((want - distance) * 0.5f, -1f, 1f);
            step = way.LengthSquared > 1e-6f ? Vector3D.Normalize(way) * enemy.WalkSpeed * ProwlPace : Vector3D<float>.Zero;
        }

        var heading = step.LengthSquared > 0.09f ? step : toPlayer;
        if (heading.LengthSquared > 1e-6f)
        {
            enemy.Yaw = MathF.Atan2(heading.X, heading.Z);
        }

        return false;
    }

    /// <summary>A stalker bolts for <paramref name="seconds"/>.</summary>
    private static void Spook(Enemy enemy, float seconds)
    {
        enemy.Mood = StalkMood.Flee;
        enemy.MoodLeft = seconds;
    }

    /// <summary>The flat way a quarter turn round from <paramref name="from"/>, one way or the other.</summary>
    private static Vector3D<float> Side(Vector3D<float> from, bool left) =>
        left ? new Vector3D<float>(from.Z, 0f, -from.X) : new Vector3D<float>(-from.Z, 0f, from.X);

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
            var phase = phases[reached];
            if (phase.Roar > 0f)
            {
                // Every attack stops: it roars first, and the stage's boosts come when the roar is done.
                enemy.Attack = null;
                enemy.ChainLeft = 0;
                enemy.RoarLeft = phase.Roar;
                enemy.RoarSeconds = phase.Roar;
            }
            else if (phase.DamageBoost != 1f || phase.AttackSpeedBoost != 1f || phase.Rage > 0)
            {
                enemy.Enrage(phase);
            }
        }
    }

    /// <summary>
    /// At most this many fodder at once may be winding up or in the middle of each kind of attack: past it, the rest wait their turn. It keeps a swarm deep in a
    /// run or a Delve from filling the air with shots and the ground with charge lanes all at once. Elites and bosses are never held back.
    /// </summary>
    public static int FodderAttackCap(AttackType type) => type switch
    {
        AttackType.Shoot => 12,
        AttackType.Lob => 6,
        AttackType.Lunge => 8,
        _ => int.MaxValue,
    };

    /// <summary>How many fodder are in the middle of an attack of <paramref name="type"/> right now.</summary>
    public int FodderAttacking(AttackType type) => _fodderAttacking.GetValueOrDefault(type);

    private void CountFodderAttacks()
    {
        _fodderAttacking.Clear();
        foreach (var enemy in _enemies)
        {
            if (enemy.IsAlive && enemy.Attack is { } attack && enemy.Kind.Tier == EnemyTier.Fodder)
            {
                _fodderAttacking[attack.Type] = _fodderAttacking.GetValueOrDefault(attack.Type) + 1;
            }
        }
    }

    /// <summary>Whether <paramref name="enemy"/> may start <paramref name="attack"/> now: always, unless it is fodder and too many are at that kind of attack already.</summary>
    private bool MayStart(Enemy enemy, AttackSpec attack) =>
        enemy.Kind.Tier != EnemyTier.Fodder || _fodderAttacking.GetValueOrDefault(attack.Type) < FodderAttackCap(attack.Type);

    /// <summary>Counts an attack just started toward the cap.</summary>
    private void Started(Enemy enemy, AttackSpec attack)
    {
        if (enemy.Kind.Tier == EnemyTier.Fodder)
        {
            _fodderAttacking[attack.Type] = _fodderAttacking.GetValueOrDefault(attack.Type) + 1;
        }
    }

    /// <summary>Starts one of the enemy's attacks that suits how far away the player is (and isn't held back by the cap), picked at random. False if none does.</summary>
    private bool TryStartAttack(Enemy enemy, float distance, PlayerTarget player, Func<float, float, float?> groundAt)
    {
        var usable = enemy.AttacksNow.Where(a => distance >= a.MinRange && distance <= a.MaxRange && MayStart(enemy, a)).ToList();
        if (usable.Count == 0)
        {
            return false;
        }

        var attack = usable[_random.Next(usable.Count)];
        enemy.Home = enemy.Position;
        StartAttack(enemy, attack, player, groundAt);
        Started(enemy, attack);
        enemy.ChainLeft = attack.Chain;
        return true;
    }

    /// <summary>Begins <paramref name="attack"/>'s wind-up, fixing where it is aimed.</summary>
    private void StartAttack(Enemy enemy, AttackSpec attack, PlayerTarget player, Func<float, float, float?> groundAt)
    {
        enemy.Attack = attack;
        enemy.AttackPhase = AttackPhase.WindUp;
        enemy.PhaseTime = 0f;
        enemy.AttackLanded = false;
        enemy.TickLeft = 0f;
        enemy.AttackOrigin = enemy.Position;
        enemy.AttackTarget = attack.Type switch
        {
            // The lane is fixed the moment it is shown: the charge goes where the lane points, not where the player has moved to. A swing's wedge, a rift's
            // lane and a sniper's line likewise (after any tracking).
            _ when Aimed(attack.Type) => LaneEnd(enemy, attack, player.Feet),

            // The landing spot is where the player stood when the circle appeared - so the circle is the warning, and leaving it is the answer.
            AttackType.LeapSlam => new Vector3D<float>(player.Feet.X, groundAt(player.Feet.X, player.Feet.Z) ?? player.Feet.Y, player.Feet.Z),
            AttackType.Retreat when attack.Reach > 0f => LeapAway(enemy, attack.Reach, player.Feet),
            AttackType.Retreat => enemy.Home,
            _ => enemy.Position,
        };
    }

    /// <summary>
    /// Where a leap <paramref name="reach"/> away from the player lands: straight away from them if that stays in the <see cref="Arena"/>, otherwise the way
    /// (turned up to 100 degrees either side) that lands furthest from them inside it; if none does, the arena's edge straight away from them.
    /// </summary>
    public Vector3D<float> LeapAway(Enemy enemy, float reach, Vector3D<float> feet)
    {
        var away = Geometry.FlatDirection(feet, enemy.Position, out _);
        if (away == Vector3D<float>.Zero)
        {
            away = new Vector3D<float>(-MathF.Sin(enemy.Yaw), 0f, -MathF.Cos(enemy.Yaw));   // right on top of it: back the way it faces
        }

        if (Arena is not { } arena)
        {
            return enemy.Position + away * reach;
        }

        bool Inside(Vector3D<float> p) => Vector2D.Distance(new Vector2D<float>(p.X, p.Z), arena.Centre) <= arena.Radius;
        Vector3D<float>? best = null;
        float furthest = -1f;
        for (int step = 0; step <= 8; step++)
        {
            float turn = (step - 4) * 25f * MathF.PI / 180f;
            var landing = enemy.Position + Turned(away, turn) * reach;
            Geometry.FlatDirection(feet, landing, out float from);
            if (Inside(landing) && from > furthest)
            {
                best = landing;
                furthest = from;
            }
        }

        if (best is { } found)
        {
            return found;
        }

        // Cornered against the wall: as far as it can go toward the way away, on the arena's edge.
        var wanted = enemy.Position + away * reach;
        var outward = Vector2D.Normalize(new Vector2D<float>(wanted.X, wanted.Z) - arena.Centre) * arena.Radius + arena.Centre;
        return new Vector3D<float>(outward.X, enemy.Position.Y, outward.Y);
    }

    /// <summary>
    /// Where an aimed attack's lane (or wedge) ends, aimed from where the attack began at <paramref name="feet"/>: its whole reach, or for a charge that stops at
    /// the player, only as far as their edge.
    /// </summary>
    private static Vector3D<float> LaneEnd(Enemy enemy, AttackSpec attack, Vector3D<float> feet)
    {
        var way = Geometry.FlatDirection(enemy.AttackOrigin, feet, out float distance);
        float length = attack.StopAtPlayer ? Math.Clamp(distance - enemy.Kind.Radius - PlayerRadius, 0f, attack.Reach) : attack.Reach;
        return enemy.AttackOrigin + way * length;
    }

    private void RunAttack(Enemy enemy, float deltaSeconds, PlayerTarget player, Func<float, float, float?> groundAt)
    {
        var attack = enemy.Attack!;
        LastDelta = deltaSeconds;
        enemy.PhaseTime += deltaSeconds * enemy.AttackSpeed;   // a quicker enemy runs through its whole attack quicker

        switch (enemy.AttackPhase)
        {
            case AttackPhase.WindUp:
                if (Aimed(attack.Type) && enemy.PhaseTime < attack.Tracking)
                {
                    // The lane (or wedge) still follows the player; it locks once the tracking is over.
                    enemy.AttackTarget = LaneEnd(enemy, attack, player.Feet);
                }

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
                    else if (attack.Type == AttackType.Lob)
                    {
                        Lob(enemy, attack, player, groundAt);
                    }
                    else if (attack.Type == AttackType.Snipe)
                    {
                        Snipe(enemy, attack, player);
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

                    if (attack.FollowUp is { } next)
                    {
                        // Straight into its follow-up, aimed at the player where they are now, with no recovery between (the rifts off a jump slam).
                        StartAttack(enemy, next, player, groundAt);
                        enemy.ChainLeft = next.Chain;
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
                    if (enemy.Kind.Behaviour == EnemyBehaviour.Stalk)
                    {
                        Spook(enemy, StalkAfterPounce);   // hit and run
                    }
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
                if (groundAt(flat.X, flat.Z) is { } lungeGround)
                {
                    enemy.Position = new Vector3D<float>(flat.X, lungeGround, flat.Z);   // (a charge into rock stops at it)
                }

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

            case AttackType.Swing or AttackType.Cleave when !enemy.AttackLanded:
            {
                enemy.AttackLanded = true;   // the blade passes once, at the start of the blow
                if (InWedge(enemy, attack, player.Feet) && MathF.Abs(player.Feet.Y - enemy.Position.Y) < enemy.Kind.Height)
                {
                    Hit(enemy, attack, player, Geometry.FlatDirection(enemy.Position, player.Feet, out _));
                }

                break;
            }

            case AttackType.LineSlam when !enemy.AttackLanded:
            {
                // The eruptions run out along their lanes through the blow; one hits the player on the ground in its lane once its front reaches them (one hit
                // however many lanes).
                var offset = player.Feet - enemy.AttackOrigin;
                foreach (var way in RiftLanes(enemy, attack))
                {
                    float along = offset.X * way.X + offset.Z * way.Z;
                    float side = MathF.Abs(offset.X * way.Z - offset.Z * way.X);
                    if (player.Grounded && along >= 0f && along <= attack.Reach * t && along <= attack.Reach + PlayerRadius && side <= attack.HitWidth + PlayerRadius)
                    {
                        enemy.AttackLanded = true;
                        Hit(enemy, attack, player, way);
                        break;
                    }
                }

                break;
            }

            case AttackType.Stomp when !enemy.AttackLanded:
            {
                enemy.AttackLanded = true;   // the slam comes down at the start of the blow
                var away = Geometry.FlatDirection(enemy.Position, player.Feet, out float gap);
                if (gap <= attack.Reach + PlayerRadius && MathF.Abs(player.Feet.Y - enemy.Position.Y) < enemy.Kind.Height)
                {
                    Hit(enemy, attack, player, away == Vector3D<float>.Zero ? new Vector3D<float>(MathF.Sin(enemy.Yaw), 0f, MathF.Cos(enemy.Yaw)) : away);
                }

                break;
            }

            case AttackType.Retreat:
            {
                // An arcing leap back to where it stood.
                var flat = enemy.AttackOrigin + (enemy.AttackTarget - enemy.AttackOrigin) * t;
                float from = enemy.AttackOrigin.Y, to = groundAt(enemy.AttackTarget.X, enemy.AttackTarget.Z) ?? enemy.AttackTarget.Y;
                enemy.Position = new Vector3D<float>(flat.X, from + (to - from) * t + 4f * attack.LeapHeight * t * (1f - t), flat.Z);
                break;
            }

            case AttackType.Whirlwind:
            {
                // Spinning after the player, faster than it walks, hitting whatever is in reach every tick.
                var toward = Geometry.FlatDirection(enemy.Position, player.Feet, out float distance);
                float step = MathF.Min(distance, enemy.WalkSpeed * attack.ProjectileSpeed * LastDelta);
                var flat = enemy.Position + toward * step;
                if (groundAt(flat.X, flat.Z) is { } spinGround)
                {
                    enemy.Position = new Vector3D<float>(flat.X, spinGround, flat.Z);
                }

                enemy.Yaw += WhirlwindTurn * LastDelta;
                enemy.TickLeft -= LastDelta * enemy.AttackSpeed;
                if (enemy.TickLeft <= 0f && distance <= attack.Reach + PlayerRadius && MathF.Abs(player.Feet.Y - enemy.Position.Y) < enemy.Kind.Height)
                {
                    enemy.TickLeft = attack.Tick;
                    Hit(enemy, attack, player, toward);
                }

                break;
            }
        }
    }

    /// <summary>The flat directions of a line slam's rifts: the one it is aimed along, and its others fanned <see cref="AttackSpec.Spread"/> apart either side.</summary>
    public static List<Vector3D<float>> RiftLanes(Enemy enemy, AttackSpec attack)
    {
        var aim = Geometry.FlatDirection(enemy.AttackOrigin, enemy.AttackTarget, out _);
        float yaw = MathF.Atan2(aim.X, aim.Z);
        int count = Math.Max(1, attack.Count);
        var lanes = new List<Vector3D<float>>(count);
        for (int i = 0; i < count; i++)
        {
            float turn = yaw + (i - (count - 1) / 2f) * attack.Spread;
            lanes.Add(new Vector3D<float>(MathF.Sin(turn), 0f, MathF.Cos(turn)));
        }

        return lanes;
    }

    /// <summary>How fast a whirlwind spins, in radians a second (about two turns).</summary>
    public const float WhirlwindTurn = 13f;

    /// <summary>This frame's step, for the attacks that move by it while they last (a whirlwind).</summary>
    private float LastDelta { get; set; }

    /// <summary>Whether <paramref name="at"/> is within <paramref name="attack"/>'s wedge: its reach from the attacker, and its half-width of where it is aimed.</summary>
    public static bool InWedge(Enemy enemy, AttackSpec attack, Vector3D<float> at)
    {
        var aim = Geometry.FlatDirection(enemy.AttackOrigin, enemy.AttackTarget, out _);
        var to = Geometry.FlatDirection(enemy.Position, at, out float distance);
        if (distance > attack.Reach + PlayerRadius)
        {
            return false;
        }

        if (distance < enemy.Kind.Radius + PlayerRadius)
        {
            return true;   // right up against it: the haft if not the blade
        }

        float angle = MathF.Acos(Math.Clamp(aim.X * to.X + aim.Z * to.Z, -1f, 1f));
        return angle <= attack.HitWidth;
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

    /// <summary>Whether an attack of <paramref name="type"/> is aimed along a locked line: a lunge's lane, a swing's or cleave's wedge, a rift, a sniper's line.</summary>
    public static bool Aimed(AttackType type) => type is AttackType.Lunge or AttackType.Swing or AttackType.Cleave or AttackType.LineSlam or AttackType.Snipe;

    /// <summary>Turns toward what the attack is aimed at: the lane, the wedge or the landing spot while they are shown, otherwise the player.</summary>
    private static void FaceAttack(Enemy enemy, PlayerTarget player)
    {
        var at = Aimed(enemy.Attack!.Type) || enemy.Attack.Type == AttackType.LeapSlam ? enemy.AttackTarget : player.Feet;
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
    /// after the glow peaks is how to dodge it. A salvo looses its other bolts at the same time, fanned out either side of the first.
    /// </summary>
    private void Shoot(Enemy shooter, AttackSpec attack, PlayerTarget player, Func<float, float, float?> groundAt)
    {
        var facing = new Vector3D<float>(MathF.Sin(shooter.Yaw), 0f, MathF.Cos(shooter.Yaw));
        var from = Muzzle(shooter, facing);
        var at = attack.Splash > 0f
            ? new Vector3D<float>(player.Feet.X, groundAt(player.Feet.X, player.Feet.Z) ?? player.Feet.Y, player.Feet.Z)
            : player.Feet + new Vector3D<float>(0f, PlayerHeight * 0.5f, 0f);
        var toward = at - from;
        var direction = toward.LengthSquared > 1e-6f ? Vector3D.Normalize(toward) : facing;
        int salvo = Math.Max(1, attack.Salvo);
        for (int i = 0; i < salvo; i++)
        {
            float turn = (i - (salvo - 1) / 2f) * attack.Spread;
            Loose(shooter, attack, from, Turned(direction, turn), at, attack.Reach / attack.ProjectileSpeed);
        }
    }

    /// <summary>
    /// The Snipe's shot, as its wind-up ends: straight down the locked lane (whatever the player has done since), dipping only to meet the player's middle where
    /// they are along it.
    /// </summary>
    private void Snipe(Enemy shooter, AttackSpec attack, PlayerTarget player)
    {
        var way = Geometry.FlatDirection(shooter.AttackOrigin, shooter.AttackTarget, out _);
        if (way == Vector3D<float>.Zero)
        {
            way = new Vector3D<float>(MathF.Sin(shooter.Yaw), 0f, MathF.Cos(shooter.Yaw));
        }

        var from = Muzzle(shooter, way);
        float along = MathF.Max(5f, (player.Feet.X - from.X) * way.X + (player.Feet.Z - from.Z) * way.Z);
        var at = new Vector3D<float>(from.X + way.X * along, player.Feet.Y + PlayerHeight * 0.5f, from.Z + way.Z * along);
        Loose(shooter, attack, from, Vector3D.Normalize(at - from), at, attack.Reach / attack.ProjectileSpeed);
    }

    /// <summary>Where a shooter's shots leave its weapon, facing <paramref name="facing"/>.</summary>
    private static Vector3D<float> Muzzle(Enemy shooter, Vector3D<float> facing) =>
        shooter.Position + new Vector3D<float>(0f, shooter.Kind.ShotHeight, 0f) + facing * shooter.Kind.ShotForward;

    /// <summary><paramref name="direction"/> turned <paramref name="angle"/> radians round the upright.</summary>
    private static Vector3D<float> Turned(Vector3D<float> direction, float angle)
    {
        if (angle == 0f)
        {
            return direction;
        }

        float c = MathF.Cos(angle), s = MathF.Sin(angle);
        return new Vector3D<float>(direction.X * c + direction.Z * s, direction.Y, direction.Z * c - direction.X * s);
    }

    /// <summary>One of <paramref name="attack"/>'s shots, flying from <paramref name="from"/> along <paramref name="direction"/> for <paramref name="flight"/> seconds.</summary>
    private void Loose(Enemy shooter, AttackSpec attack, Vector3D<float> from, Vector3D<float> direction, Vector3D<float> target, float flight) =>
        _bolts.Add(new EnemyBolt
        {
            Shooter = shooter,
            Model = attack.ProjectileModel ?? "ghoul_bolt.glb",
            Target = target,
            Splash = attack.Splash,
            MarkModel = attack.MarkModel ?? EnemyView.LandingModel,
            BurstModel = attack.BurstModel ?? EnemyView.BlastModel,
            Position = from,
            Velocity = direction * attack.ProjectileSpeed,
            FlightLeft = flight,
            Damage = attack.Damage * shooter.DamageScale * RangedDamageTaken,
            Radius = attack.HitWidth,
            Knockback = attack.Knockback,
        });

    /// <summary>How high above its landing spot a barrage's fireball starts to fall.</summary>
    public const float BarrageHeight = 16f;

    /// <summary>How far apart the Line of Fire's fireballs land, and how much later each lands than the one before it.</summary>
    public const float LineSpacing = 2.4f;
    public const float LineStagger = 0.12f;

    /// <summary>How many of the Crown of Fire's ring places are left empty, side by side: the way out.</summary>
    public const int CrownGap = 2;

    /// <summary>
    /// Where <paramref name="attack"/>'s fireballs land (flat x, z) and how much later than the attack's own fall each one lands, by its pattern: scattered round the
    /// player (the first on them), a ring round them with a gap, or a row from the caster through them.
    /// </summary>
    public List<(float X, float Z, float Delay)> BarrageSpots(Vector3D<float> caster, AttackSpec attack, Vector3D<float> playerFeet)
    {
        var spots = new List<(float, float, float)>();
        switch (attack.Pattern)
        {
            case BarragePattern.Ring:
            {
                spots.Add((playerFeet.X, playerFeet.Z, 0f));
                int places = Math.Max(CrownGap + 3, attack.Count - 1 + CrownGap);
                int gap = _random.Next(places);
                for (int i = 0; i < places; i++)
                {
                    if ((i - gap + places) % places < CrownGap)
                    {
                        continue;
                    }

                    float angle = MathF.Tau * i / places;
                    spots.Add((playerFeet.X + MathF.Sin(angle) * attack.Reach, playerFeet.Z + MathF.Cos(angle) * attack.Reach, 0f));
                }

                break;
            }

            case BarragePattern.Line:
            {
                var way = Geometry.FlatDirection(caster, playerFeet, out float toPlayer);
                float length = toPlayer + attack.Reach;
                int n = 0;
                for (float d = 2.5f; d <= length; d += LineSpacing, n++)
                {
                    spots.Add((caster.X + way.X * d, caster.Z + way.Z * d, n * LineStagger));
                }

                break;
            }

            default:
                for (int i = 0; i < Math.Max(1, attack.Count); i++)
                {
                    float angle = (float)_random.NextDouble() * MathF.Tau;
                    float distance = i == 0 ? 0f : attack.Reach * MathF.Sqrt((float)_random.NextDouble());
                    float late = (float)_random.NextDouble() * 0.3f;   // not all landing at once
                    spots.Add((playerFeet.X + MathF.Sin(angle) * distance, playerFeet.Z + MathF.Cos(angle) * distance, late));
                }

                break;
        }

        return spots;
    }

    /// <summary>
    /// Calls <paramref name="attack"/>'s fireballs down from the sky on its pattern's spots (see <see cref="BarrageSpots"/>). Each falls from high above its spot, a
    /// little aslant, at about the attack's speed - its landing marked the whole way down.
    /// </summary>
    private void Barrage(Enemy caster, AttackSpec attack, PlayerTarget player, Func<float, float, float?> groundAt)
    {
        foreach (var (x, z, delay) in BarrageSpots(caster.Position, attack, player.Feet))
        {
            if (groundAt(x, z) is not { } ground)
            {
                continue;
            }

            float angle = (float)_random.NextDouble() * MathF.Tau;
            var target = new Vector3D<float>(x, ground, z);
            var from = target + new Vector3D<float>(MathF.Sin(angle) * 3f, BarrageHeight, MathF.Cos(angle) * 3f);
            var fall = target - from;
            float speed = fall.Length / (fall.Length / attack.ProjectileSpeed + delay);
            _bolts.Add(new EnemyBolt
            {
                Shooter = caster,
                Model = attack.ProjectileModel ?? "ghoul_fireball.glb",
                Target = target,
                Splash = attack.Splash,
                MarkModel = attack.MarkModel ?? EnemyView.LandingModel,
                BurstModel = attack.BurstModel ?? EnemyView.BlastModel,
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
                    Burst(bolt.Shooter, from + (to - from) * along, bolt.Splash, bolt.Damage, bolt.Knockback, bolt.Velocity, bolt.BurstModel, player, bottom, top);
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
                    Burst(bolt.Shooter, new Vector3D<float>(to.X, ground, to.Z), bolt.Splash, bolt.Damage, bolt.Knockback, bolt.Velocity, bolt.BurstModel, player, bottom, top);
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

    /// <summary>
    /// A splash shot or a bomb bursting at <paramref name="centre"/>: it strikes the player if any of them is within <paramref name="splash"/>, shoving them
    /// away from it (along <paramref name="heading"/> if they are right on it).
    /// </summary>
    private void Burst(Enemy shooter, Vector3D<float> centre, float splash, float damage, float knockback, Vector3D<float> heading, string model,
        PlayerTarget player, Vector3D<float> bottom, Vector3D<float> top)
    {
        _blasts.Add(new EnemyBlast { Centre = centre, Radius = splash, Model = model });
        if (Geometry.SegmentDistance(centre, centre, bottom, top, out _) <= splash + PlayerRadius
            && Strike(shooter, damage, player, out bool blocked) && !blocked)
        {
            var away = Geometry.FlatDirection(centre, player.Feet, out _);
            player.Condition.Knock(away == Vector3D<float>.Zero ? heading : away, knockback);
        }
    }

    /// <summary>How hard a bomb falls (metres a second, each second).</summary>
    public const float BombGravity = 18f;

    /// <summary>Where a lobbed bomb leaves the thrower's hand: this high above its feet, this far in front.</summary>
    public const float LobHeight = 1.6f;
    public const float LobForward = 0.4f;

    /// <summary>A lobbed bomb first lands this share of the way to where the player stood, and bounces on toward them.</summary>
    public const float LobShort = 0.6f;

    /// <summary>What a bounce keeps of a bomb's upward speed and of its speed along the ground.</summary>
    public const float BounceUp = 0.5f;
    public const float BounceAlong = 0.75f;

    /// <summary>How fast a rolling bomb slows (metres a second, each second), and the speed it counts as stopped - and goes off - below.</summary>
    public const float RollDrag = 6f;
    public const float RollStop = 0.3f;

    /// <summary>A rolling bomb that meets ground rising steeper than this (rise over run) has run into the hillside, and goes off.</summary>
    public const float SteepSlope = 1f;

    /// <summary>A bomb goes off after this long whatever it is doing.</summary>
    public const float BombFuse = 5f;

    /// <summary>
    /// Lobs a bomb from <paramref name="thrower"/>'s raised hand in a high arc that first lands <see cref="LobShort"/> of the way to where the player stands
    /// now, so it bounces on toward them. A salvo lobs its other bombs at the same time at spots round the player, evenly spaced <see cref="AttackSpec.Reach"/>
    /// from them.
    /// </summary>
    private void Lob(Enemy thrower, AttackSpec attack, PlayerTarget player, Func<float, float, float?> groundAt)
    {
        int salvo = Math.Max(1, attack.Salvo);
        float turn = salvo > 1 ? (float)_random.NextDouble() * MathF.Tau : 0f;
        for (int i = 0; i < salvo; i++)
        {
            // The rest round the player, evenly, each between most of the reach and all of it so they don't sit in a neat ring.
            float angle = turn + MathF.Tau * i / MathF.Max(1, salvo - 1);
            float distance = attack.Reach * (0.7f + 0.3f * (float)_random.NextDouble());
            var aim = i == 0 ? player.Feet : player.Feet + new Vector3D<float>(MathF.Sin(angle), 0f, MathF.Cos(angle)) * distance;
            LobAt(thrower, attack, aim, player.Feet.Y, groundAt, salvo > 1 ? SalvoShort : LobShort);
        }
    }

    /// <summary>
    /// A salvo's bombs first land this share of the way to their spots, nearer than a lone bomb does, so they come down spread out round the player rather than
    /// bunched up short of them (the user's call, 2026-09-28).
    /// </summary>
    public const float SalvoShort = 0.85f;

    /// <summary>One bomb lobbed at <paramref name="aim"/>, landing <paramref name="landShare"/> of the way there and bouncing on.</summary>
    private void LobAt(Enemy thrower, AttackSpec attack, Vector3D<float> aim, float fallbackY, Func<float, float, float?> groundAt, float landShare)
    {
        var facing = new Vector3D<float>(MathF.Sin(thrower.Yaw), 0f, MathF.Cos(thrower.Yaw));
        var from = thrower.Position + new Vector3D<float>(0f, thrower.Kind.LobHeight, 0f) + facing * LobForward;
        var toward = Geometry.FlatDirection(from, aim, out float distance);
        if (toward == Vector3D<float>.Zero)
        {
            toward = facing;
        }

        float along = distance * landShare;
        var land = from + toward * along;
        float landY = (groundAt(land.X, land.Z) ?? fallbackY) + attack.HitWidth;
        float flight = 0.75f + 0.02f * distance;
        float up = (landY - from.Y + 0.5f * BombGravity * flight * flight) / flight;
        _bombs.Add(new EnemyBomb
        {
            Thrower = thrower,
            Model = attack.ProjectileModel ?? "ghoul_bomb.glb",
            Position = from,
            Velocity = toward * (along / flight) + new Vector3D<float>(0f, up, 0f),
            Radius = attack.HitWidth,
            Splash = attack.Splash,
            Damage = attack.Damage * thrower.DamageScale * RangedDamageTaken,
            Knockback = attack.Knockback,
            BouncesLeft = attack.Count,
        });
    }

    /// <summary>
    /// Moves the bombs: falling, bouncing off the ground, rolling to a stop. One goes off where it touches the player, a tree or a rock, or a slope too steep
    /// to roll up; when it stops rolling; when its fuse runs out; or off the map.
    /// </summary>
    private void MoveBombs(float deltaSeconds, PlayerTarget player, Func<float, float, float?> groundAt)
    {
        var bottom = player.Feet + new Vector3D<float>(0f, PlayerRadius, 0f);
        var top = player.Feet + new Vector3D<float>(0f, PlayerHeight - PlayerRadius, 0f);
        for (int i = _bombs.Count - 1; i >= 0; i--)
        {
            var bomb = _bombs[i];
            bomb.Age += deltaSeconds;
            var velocity = bomb.Velocity;
            if (bomb.Rolling)
            {
                var flat = new Vector3D<float>(velocity.X, 0f, velocity.Z);
                float speed = MathF.Max(0f, flat.Length - RollDrag * deltaSeconds);
                velocity = flat.Length > 1e-4f ? Vector3D.Normalize(flat) * speed : Vector3D<float>.Zero;
            }
            else
            {
                velocity.Y -= BombGravity * deltaSeconds;
            }

            var from = bomb.Position;
            var to = from + velocity * deltaSeconds;
            if (Geometry.SegmentDistance(from, to, bottom, top, out float along) <= PlayerRadius + bomb.Radius)
            {
                GoOff(i, from + (to - from) * along, player, bottom, top);
                continue;
            }

            if (Obstacles?.Invoke(to, bomb.Radius) == true || groundAt(to.X, to.Z) is not { } ground || bomb.Age >= BombFuse)
            {
                GoOff(i, from, player, bottom, top);
                continue;
            }

            if (to.Y - bomb.Radius <= ground)
            {
                // Meeting ground that rises steeply (or rising above it while it still climbs) is running into the hillside: it goes off there.
                float run = new Vector2D<float>(to.X - from.X, to.Z - from.Z).Length;
                float rise = ground - (groundAt(from.X, from.Z) ?? ground);
                if ((run > 1e-4f && rise > SteepSlope * run) || (!bomb.Rolling && velocity.Y >= 0f))
                {
                    GoOff(i, from, player, bottom, top);
                    continue;
                }
            }

            if (bomb.Rolling)
            {
                to.Y = ground + bomb.Radius;   // it keeps to the ground, up or down
                if (velocity.Length <= RollStop)
                {
                    GoOff(i, to, player, bottom, top);   // come to rest: it goes off
                    continue;
                }
            }
            else if (to.Y - bomb.Radius <= ground)
            {
                to.Y = ground + bomb.Radius;
                float bounce = -velocity.Y * BounceUp;
                velocity = new Vector3D<float>(velocity.X * BounceAlong, 0f, velocity.Z * BounceAlong);
                if (bomb.BouncesLeft > 0)
                {
                    bomb.BouncesLeft--;
                    velocity.Y = bounce;
                }
                else
                {
                    bomb.Rolling = true;
                }
            }

            bomb.Position = to;
            bomb.Velocity = velocity;
        }
    }

    /// <summary>The bomb at <paramref name="index"/> goes off at <paramref name="centre"/>.</summary>
    private void GoOff(int index, Vector3D<float> centre, PlayerTarget player, Vector3D<float> bottom, Vector3D<float> top)
    {
        var bomb = _bombs[index];
        _bombs.RemoveAt(index);
        Burst(bomb.Thrower, centre, bomb.Splash, bomb.Damage, bomb.Knockback, bomb.Velocity, EnemyView.BombBurstModel, player, bottom, top);
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
