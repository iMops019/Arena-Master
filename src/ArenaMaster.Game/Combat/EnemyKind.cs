namespace ArenaMaster.Game.Combat;

/// <summary>How an enemy counts in a run: fodder fills the field, elites and bosses are spawned on the director's schedule.</summary>
internal enum EnemyTier
{
    Fodder,
    Elite,
    Boss,
}

internal enum AttackType
{
    /// <summary>Crouches, then charges straight down a marked lane.</summary>
    Lunge,

    /// <summary>Marks a circle, leaps into the air and lands in it: damage, knock-back and a stun to anyone inside.</summary>
    LeapSlam,

    /// <summary>Raises its arms, then sends a ring out along the ground: jump it or take the hit.</summary>
    Shockwave,

    /// <summary>Raises its arms and calls a ring of fodder up around itself.</summary>
    Summon,

    /// <summary>
    /// Stands and aims, its weapon glowing brighter as the shot nears, then looses a shot at where the player is: step out of its path. A shot with a splash
    /// (a fireball) is aimed at the ground under the player instead, and bursts where it lands.
    /// </summary>
    Shoot,
}

/// <summary>
/// One telegraphed attack and its numbers. Every attack has a wind-up (it stands still and shows what is coming), an active part (the charge, the leap, the spreading
/// ring), and a recovery (it stands still, open to punishment). Distances in metres, times in seconds.
/// </summary>
/// <param name="MinRange">It only starts this attack with the player at least this far away...</param>
/// <param name="MaxRange">...and no further than this.</param>
/// <param name="Reach">Lunge: how far the charge goes. LeapSlam: the landing's radius. Shockwave: how far the ring spreads. Summon: how many it calls. Shoot: how far the bolt flies.</param>
/// <param name="HitWidth">Lunge: how close to the charging body counts as hit. Shockwave: half the ring's width. Shoot: the bolt's radius. Unused otherwise.</param>
/// <param name="LeapHeight">LeapSlam: the top of the arc, above the straight line from take-off to landing.</param>
/// <param name="ProjectileSpeed">Shoot: how fast the shot flies - slow enough to sidestep once it is loosed.</param>
/// <param name="Splash">Shoot: 0 for a bolt that has to hit the player; more for a fireball that bursts where it lands, hurting anyone within this far.</param>
/// <param name="ProjectileModel">Shoot: the model the shot is drawn with.</param>
internal sealed record AttackSpec(
    AttackType Type,
    float MinRange,
    float MaxRange,
    float WindUp,
    float Active,
    float Recover,
    float Damage,
    float Reach,
    float HitWidth = 0f,
    float Knockback = 0f,
    float Stun = 0f,
    float LeapHeight = 0f,
    float ProjectileSpeed = 0f,
    float Splash = 0f,
    string? ProjectileModel = null);

/// <summary>
/// What one kind of enemy is: its model and its numbers. Distances are metres, speeds metres per second, times seconds. The body is a standing cylinder of
/// <see cref="Radius"/> and <see cref="Height"/> from its feet - what arrows hit and what keeps it off the player and off other enemies. <see cref="Experience"/> is what its gem is worth.
/// Its <see cref="Attacks"/> are chosen from (among those in range) whenever <see cref="AttackCooldown"/> has passed since the last one ended.
/// </summary>
internal sealed record EnemyKind(
    string Name,
    string Model,
    EnemyTier Tier,
    float MaxHealth,
    float Speed,
    float Radius,
    float Height,
    float ContactDamage,
    float ContactInterval,
    int Experience)
{
    public IReadOnlyList<AttackSpec> Attacks { get; init; } = Array.Empty<AttackSpec>();

    public float AttackCooldown { get; init; }

    /// <summary>
    /// A breakable prop rather than a creature (a crate): it never moves, turns, claws or attacks, isn't counted as a kill or toward the swarm, and drops a pickup
    /// instead of experience when broken. It stands on the field so every class's attacks can break it the way they hit anything else.
    /// </summary>
    public bool IsProp { get; init; }

    /// <summary>A ranged kind stops walking closer once the player is this near (0: it walks right up).</summary>
    public float StandOff { get; init; }

    /// <summary>A second model drawn with the body (a crossbow, a flame), which lights up from faint to bright as a <see cref="AttackType.Shoot"/> winds up. Null for none.</summary>
    public string? HeldModel { get; init; }

    /// <summary>The first enemy: slow, fragile fodder that shambles straight at the player and claws on contact.</summary>
    public static readonly EnemyKind Ghoul = new(
        Name: "Ghoul",
        Model: "ghoul_placeholder.glb",
        Tier: EnemyTier.Fodder,
        MaxHealth: 30f,
        Speed: 3.6f,
        Radius: 0.4f,
        Height: 1.45f,
        ContactDamage: 8f,
        ContactInterval: 0.8f,
        Experience: 1);

    /// <summary>
    /// Ranged fodder: a ghoul with a crossbow. It walks in until the player is in range, stops, and aims - the crossbow glowing brighter through the wind-up - then
    /// looses a bolt at where the player is. A cooldown between shots. Fragile, and it claws like any ghoul if the player comes to it.
    /// </summary>
    public static readonly EnemyKind CrossbowGhoul = new(
        Name: "Crossbow Ghoul",
        Model: "crossbow_ghoul_placeholder.glb",
        Tier: EnemyTier.Fodder,
        MaxHealth: 24f,
        Speed: 3.2f,
        Radius: 0.4f,
        Height: 1.45f,
        ContactDamage: 6f,
        ContactInterval: 0.8f,
        Experience: 2)
    {
        AttackCooldown = 2.8f,
        StandOff = 11f,
        HeldModel = "ghoul_crossbow.glb",
        Attacks = new[]
        {
            new AttackSpec(AttackType.Shoot, MinRange: 3f, MaxRange: 14f, WindUp: 1.1f, Active: 0.1f, Recover: 0.5f,
                Damage: 12f, Reach: 30f, HitWidth: 0.25f, Knockback: 4f, ProjectileSpeed: 20f, ProjectileModel: "ghoul_bolt.glb"),
        },
    };

    /// <summary>
    /// Ranged fodder, rarer and tougher than the crossbow: a robed ghoul that keeps further off and conjures a fireball between its hands - the flame glowing
    /// brighter through a longer wind-up - then hurls it at the ground under the player. It flies slowly, a burning ring marks where it will land, and it bursts
    /// there, hurting anyone within 2 m: a real sidestep, not a lean, gets clear.
    /// </summary>
    public static readonly EnemyKind GhoulMage = new(
        Name: "Ghoul Mage",
        Model: "ghoul_mage_placeholder.glb",
        Tier: EnemyTier.Fodder,
        MaxHealth: 36f,
        Speed: 3f,
        Radius: 0.4f,
        Height: 1.5f,
        ContactDamage: 6f,
        ContactInterval: 0.8f,
        Experience: 3)
    {
        AttackCooldown = 3.5f,
        StandOff = 14f,
        HeldModel = "ghoul_flame.glb",
        Attacks = new[]
        {
            new AttackSpec(AttackType.Shoot, MinRange: 4f, MaxRange: 18f, WindUp: 1.4f, Active: 0.1f, Recover: 0.6f,
                Damage: 16f, Reach: 30f, HitWidth: 0.35f, Knockback: 6f, ProjectileSpeed: 13f, Splash: 2f, ProjectileModel: "ghoul_fireball.glb"),
        },
    };

    /// <summary>A breakable wooden crate that turns up around the player (see <c>World/Crates</c>). A few hits break it; it drops a pickup.</summary>
    public static readonly EnemyKind Crate = new(
        Name: "Crate",
        Model: "crate_placeholder.glb",
        Tier: EnemyTier.Fodder,
        MaxHealth: 20f,
        Speed: 0f,
        Radius: 0.45f,
        Height: 0.9f,
        ContactDamage: 0f,
        ContactInterval: 1f,
        Experience: 0)
    {
        IsProp = true,
    };

    /// <summary>The first elite: a hulking brute that lunges down a lane and leaps to slam a marked circle.</summary>
    public static readonly EnemyKind Brute = new(
        Name: "Ghoul Brute",
        Model: "brute_placeholder.glb",
        Tier: EnemyTier.Elite,
        MaxHealth: 260f,
        Speed: 3.3f,
        Radius: 0.75f,
        Height: 2.4f,
        ContactDamage: 15f,
        ContactInterval: 1f,
        Experience: 12)
    {
        AttackCooldown = 2.5f,
        Attacks = new[]
        {
            new AttackSpec(AttackType.Lunge, MinRange: 3.5f, MaxRange: 9f, WindUp: 0.75f, Active: 0.45f, Recover: 0.7f,
                Damage: 22f, Reach: 7.2f, HitWidth: 0.9f, Knockback: 12f),
            new AttackSpec(AttackType.LeapSlam, MinRange: 5f, MaxRange: 14f, WindUp: 0.7f, Active: 0.75f, Recover: 0.8f,
                Damage: 26f, Reach: 3.5f, Knockback: 10f, Stun: 0.9f, LeapHeight: 3f),
        },
    };

    /// <summary>The boss: a towering ghoul king that slams, sends shockwaves along the ground, and calls up ghouls.</summary>
    public static readonly EnemyKind HollowKing = new(
        Name: "The Hollow King",
        Model: "hollow_king_placeholder.glb",
        Tier: EnemyTier.Boss,
        MaxHealth: 3200f,
        Speed: 2.9f,
        Radius: 1.3f,
        Height: 4.2f,
        ContactDamage: 25f,
        ContactInterval: 1f,
        Experience: 80)
    {
        AttackCooldown = 2.2f,
        Attacks = new[]
        {
            new AttackSpec(AttackType.LeapSlam, MinRange: 6f, MaxRange: 20f, WindUp: 1f, Active: 1f, Recover: 1f,
                Damage: 35f, Reach: 6f, Knockback: 14f, Stun: 1f, LeapHeight: 5f),
            new AttackSpec(AttackType.Shockwave, MinRange: 0f, MaxRange: 16f, WindUp: 1.1f, Active: 1.6f, Recover: 0.8f,
                Damage: 25f, Reach: 18f, HitWidth: 0.7f, Knockback: 9f),
            new AttackSpec(AttackType.Summon, MinRange: 0f, MaxRange: 60f, WindUp: 1f, Active: 0.1f, Recover: 0.6f,
                Damage: 0f, Reach: 8f),
        },
    };
}

/// <summary>How much tougher than its base numbers an enemy spawns - the run director raises these over time.</summary>
internal readonly record struct EnemyScaling(float Health, float Damage, float Speed)
{
    public static readonly EnemyScaling None = new(1f, 1f, 1f);
}
