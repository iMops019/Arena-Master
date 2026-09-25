using Silk.NET.Maths;

namespace ArenaMaster.Game.Combat;

/// <summary>One enemy in the world. <see cref="Position"/> is its feet.</summary>
internal sealed class Enemy
{
    public Enemy(int id, EnemyKind kind, Vector3D<float> position)
    {
        Id = id;
        Kind = kind;
        Position = position;
        Health = kind.MaxHealth;
    }

    public int Id { get; }

    public EnemyKind Kind { get; }

    public Vector3D<float> Position { get; set; }

    public float Yaw { get; set; }

    public float Health { get; set; }

    public bool IsAlive => Health > 0f;

    /// <summary>1 the moment it is hit, fading to 0 - the view makes it flinch.</summary>
    public float HitFlash { get; set; }

    /// <summary>Seconds since it died (only meaningful once it has).</summary>
    public float DeadFor { get; set; }

    public float ContactCooldown { get; set; }

    /// <summary>A per-enemy offset so a crowd doesn't bob in step.</summary>
    public float Phase { get; set; }
}

/// <summary>
/// Every enemy on the map: spawning them around the player, moving them (straight at the player, kept apart from each other and off the player), their contact damage,
/// taking hits and dying. Pure simulation - no engine calls - so it can be tested; <see cref="EnemyView"/> puts it on screen.
/// </summary>
internal sealed class EnemyField
{
    /// <summary>How long a dead enemy stays (sinking) before it is gone.</summary>
    public const float DeathDuration = 0.4f;

    /// <summary>The walking player's radius (the engine's PlayerController), for contact.</summary>
    public const float PlayerRadius = 0.35f;

    /// <summary>Enemies further than this from the player are moved back into the spawn ring rather than left stranded.</summary>
    public const float LeashDistance = 75f;

    private readonly List<Enemy> _enemies = new();
    private readonly Random _random;
    private int _nextId = 1;
    private float _spawnTimer;

    public EnemyField(Random random) => _random = random;

    public IReadOnlyList<Enemy> Enemies => _enemies;

    public EnemyKind Kind { get; set; } = EnemyKind.Ghoul;

    /// <summary>How many live enemies the field keeps topped up to.</summary>
    public int TargetCount { get; set; } = 14;

    /// <summary>Seconds between spawns while below <see cref="TargetCount"/>.</summary>
    public float SpawnInterval { get; set; } = 0.5f;

    public float SpawnMinDistance { get; set; } = 24f;

    public float SpawnMaxDistance { get; set; } = 38f;

    public int Kills { get; private set; }

    public int AliveCount => _enemies.Count(e => e.IsAlive);

    /// <summary>Puts an enemy of <see cref="Kind"/> at <paramref name="position"/> (its feet).</summary>
    public Enemy Spawn(Vector3D<float> position)
    {
        var enemy = new Enemy(_nextId++, Kind, position) { Phase = (float)_random.NextDouble() * MathF.Tau };
        _enemies.Add(enemy);
        return enemy;
    }

    /// <summary>
    /// Advances every enemy one frame. <paramref name="groundAt"/> gives the ground height at a point, or null off the map. Enemies touching the player hurt
    /// <paramref name="player"/>. Returns the enemies that finished dying this frame and are now gone (so their view can be removed).
    /// </summary>
    public List<Enemy> Update(float deltaSeconds, Vector3D<float> playerFeet, Func<float, float, float?> groundAt, PlayerHealth player)
    {
        SpawnTowardTarget(deltaSeconds, playerFeet, groundAt);

        foreach (var enemy in _enemies)
        {
            enemy.HitFlash = MathF.Max(0f, enemy.HitFlash - deltaSeconds * 5f);
            if (enemy.IsAlive)
            {
                Move(enemy, deltaSeconds, playerFeet, groundAt, player);
            }
            else
            {
                enemy.DeadFor += deltaSeconds;
            }
        }

        var gone = _enemies.Where(e => !e.IsAlive && e.DeadFor >= DeathDuration).ToList();
        _enemies.RemoveAll(e => !e.IsAlive && e.DeadFor >= DeathDuration);
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
        return true;
    }

    /// <summary>
    /// The first live enemy the moving sphere (<paramref name="from"/> to <paramref name="to"/>, radius <paramref name="radius"/>) touches, and how far along the move (0 to 1) it did.
    /// Each enemy is a capsule around its standing axis.
    /// </summary>
    public Enemy? FirstHit(Vector3D<float> from, Vector3D<float> to, float radius, out float along)
    {
        Enemy? best = null;
        along = float.MaxValue;

        foreach (var enemy in _enemies)
        {
            if (!enemy.IsAlive)
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

    /// <summary>Takes every enemy away at once (a restart). Returns them so their view can be cleared.</summary>
    public List<Enemy> Clear()
    {
        var all = _enemies.ToList();
        _enemies.Clear();
        _spawnTimer = 0f;
        return all;
    }

    public void ResetKills() => Kills = 0;

    private void SpawnTowardTarget(float deltaSeconds, Vector3D<float> playerFeet, Func<float, float, float?> groundAt)
    {
        _spawnTimer -= deltaSeconds;
        if (_spawnTimer > 0f || AliveCount >= TargetCount)
        {
            return;
        }

        _spawnTimer = SpawnInterval;
        if (TryPickSpawnPoint(playerFeet, groundAt, out var point))
        {
            Spawn(point);
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

    private void Move(Enemy enemy, float deltaSeconds, Vector3D<float> playerFeet, Func<float, float, float?> groundAt, PlayerHealth player)
    {
        var kind = enemy.Kind;
        var toPlayer = Geometry.FlatDirection(enemy.Position, playerFeet, out float distance);

        if (distance > LeashDistance && TryPickSpawnPoint(playerFeet, groundAt, out var nearer))
        {
            enemy.Position = nearer;   // lost far behind: bring it back into the fight
            return;
        }

        // Walk at the player, eased off by any other enemy close enough to crowd it, so a pack spreads around the player instead of stacking into one.
        var step = toPlayer * kind.Speed;
        foreach (var other in _enemies)
        {
            if (other == enemy || !other.IsAlive)
            {
                continue;
            }

            float spacing = kind.Radius + other.Kind.Radius + 0.15f;
            var away = Geometry.FlatDirection(other.Position, enemy.Position, out float apart);
            if (apart < spacing)
            {
                step += (apart > 1e-4f ? away : new Vector3D<float>(MathF.Sin(enemy.Phase), 0f, MathF.Cos(enemy.Phase))) * kind.Speed * (1f - apart / spacing) * 1.5f;
            }
        }

        var position = enemy.Position + step * deltaSeconds;

        // Stop at the player's edge rather than walk into them, and claw while touching.
        float reach = kind.Radius + PlayerRadius;
        var fromPlayer = Geometry.FlatDirection(playerFeet, position, out float gap);
        if (gap < reach)
        {
            var push = gap > 1e-4f ? fromPlayer : -toPlayer;
            position = new Vector3D<float>(playerFeet.X + push.X * reach, position.Y, playerFeet.Z + push.Z * reach);
        }

        bool touching = gap <= reach + 0.1f && MathF.Abs(position.Y - playerFeet.Y) < kind.Height;
        enemy.ContactCooldown = MathF.Max(0f, enemy.ContactCooldown - deltaSeconds);
        if (touching && enemy.ContactCooldown <= 0f && player.TakeDamage(kind.ContactDamage))
        {
            enemy.ContactCooldown = kind.ContactInterval;
        }

        if (groundAt(position.X, position.Z) is { } ground)
        {
            enemy.Position = new Vector3D<float>(position.X, ground, position.Z);
        }

        if (distance > 1e-3f)
        {
            enemy.Yaw = MathF.Atan2(toPlayer.X, toPlayer.Z);
        }
    }
}
