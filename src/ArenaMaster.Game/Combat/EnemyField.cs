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

    /// <summary>How many live fodder enemies the field keeps topped up to. Elites and bosses don't count.</summary>
    public int TargetCount { get; set; } = 14;

    /// <summary>Seconds between spawns while below <see cref="TargetCount"/>.</summary>
    public float SpawnInterval { get; set; } = 0.5f;

    public float SpawnMinDistance { get; set; } = 24f;

    public float SpawnMaxDistance { get; set; } = 38f;

    public int Kills { get; private set; }

    public int AliveCount => _enemies.Count(e => e.IsAlive && e.Kind.Tier == EnemyTier.Fodder);

    /// <summary>The blows that reached the player during the last <see cref="Update"/>, landed or blocked, for anything that answers them (thorns, say).</summary>
    public IReadOnlyList<Strike> Strikes => _strikes;

    /// <summary>The first living boss, if one is on the field.</summary>
    public Enemy? Boss => _enemies.FirstOrDefault(e => e.IsAlive && e.Kind.Tier == EnemyTier.Boss);

    /// <summary>Puts an enemy (of <see cref="Kind"/> unless <paramref name="kind"/> says otherwise, at the current <see cref="Scaling"/>) at <paramref name="position"/>, its feet.</summary>
    public Enemy Spawn(Vector3D<float> position, EnemyKind? kind = null)
    {
        var enemy = new Enemy(_nextId++, kind ?? Kind, position, Scaling) { Phase = (float)_random.NextDouble() * MathF.Tau };
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

    /// <summary>Hurts <paramref name="enemy"/>. True if this was the killing blow.</summary>
    public bool Damage(Enemy enemy, float amount)
    {
        if (!enemy.IsAlive)
        {
            return false;
        }

        enemy.Health -= amount;
        enemy.HitFlash = 1f;
        if (enemy.IsAlive)
        {
            return false;
        }

        enemy.Health = 0f;
        enemy.DeadFor = 0f;
        Kills++;
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

    /// <summary>Takes every enemy away at once (a restart). Returns them so their view can be cleared.</summary>
    public List<Enemy> Clear()
    {
        var all = _enemies.ToList();
        _enemies.Clear();
        _newlyKilled.Clear();
        _summoned.Clear();
        _strikes.Clear();
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

    public void ResetKills() => Kills = 0;

    private void SpawnTowardTarget(float deltaSeconds, Vector3D<float> playerFeet, Func<float, float, float?> groundAt)
    {
        _spawnTimer -= deltaSeconds;
        int alive = AliveCount;
        for (int spawned = 0; _spawnTimer <= 0f && alive < TargetCount && spawned < MaxSpawnsPerFrame; spawned++)
        {
            _spawnTimer += SpawnInterval;
            if (TryPickSpawnPoint(playerFeet, groundAt, out var point))
            {
                Spawn(point);
                alive++;
            }
        }

        if (alive >= TargetCount)
        {
            _spawnTimer = MathF.Max(_spawnTimer, 0f);   // no stored-up burst once the field is full
        }
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

        // Walk at the player, eased off by any other enemy close enough to crowd it, so a pack spreads around the player instead of stacking into one. The bigger of two
        // enemies gives way less.
        var step = toPlayer * enemy.Speed;
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
                step += push * enemy.Speed * (1f - apart / spacing) * 1.5f * giveWay;
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

    /// <summary>Starts one of the enemy's attacks that suits how far away the player is, picked at random. False if none does.</summary>
    private bool TryStartAttack(Enemy enemy, float distance, PlayerTarget player, Func<float, float, float?> groundAt)
    {
        var usable = enemy.Kind.Attacks.Where(a => distance >= a.MinRange && distance <= a.MaxRange).ToList();
        if (usable.Count == 0)
        {
            return false;
        }

        var attack = usable[_random.Next(usable.Count)];
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

        return true;
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
                }

                break;

            case AttackPhase.Active:
                float t = Math.Clamp(enemy.PhaseTime / attack.Active, 0f, 1f);
                RunActive(enemy, attack, t, player, groundAt);
                if (enemy.PhaseTime >= attack.Active)
                {
                    enemy.PhaseTime -= attack.Active;
                    enemy.AttackPhase = AttackPhase.Recover;
                }

                break;

            case AttackPhase.Recover:
                if (enemy.PhaseTime >= attack.Recover)
                {
                    enemy.Attack = null;
                    enemy.AttackCooldown = enemy.Kind.AttackCooldown;
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
