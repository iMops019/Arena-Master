namespace ArenaMaster.Game.Combat;

/// <summary>How an enemy moves when it isn't attacking.</summary>
internal enum EnemyBehaviour
{
    /// <summary>Straight at the player (stopping at its <see cref="EnemyKind.StandOff"/>, for a shooter), attacking whenever one of its attacks is in range.</summary>
    Chase,

    /// <summary>
    /// A stalker (the Ghoul Beast): it keeps its distance, circling the player and working round behind them, bolts when looked at or hurt, and only attacks
    /// when it has an opening - the player's back turned, or a stun - running off again after. See <c>EnemyField</c>'s stalking.
    /// </summary>
    Stalk,
}

/// <summary>How an enemy counts in a run: fodder fills the field, elites and bosses are spawned on the director's schedule.</summary>
internal enum EnemyTier
{
    Fodder,
    Elite,
    Boss,
}

/// <summary>Where a <see cref="AttackType.Barrage"/>'s fireballs fall.</summary>
internal enum BarragePattern
{
    /// <summary>The first on the player, the rest scattered round them within the attack's reach.</summary>
    Scatter,

    /// <summary>
    /// The Crown of Fire: one on the player and the rest in a ring round them at the attack's reach, with one gap in it - out through the gap before the middle
    /// burns.
    /// </summary>
    Ring,

    /// <summary>The Line of Fire: a row from the caster through the player and on past them, landing one after another outward from the caster - step aside.</summary>
    Line,
}

internal enum AttackType
{
    /// <summary>
    /// Crouches, then charges straight down a marked lane. With <see cref="AttackSpec.Tracking"/> the lane follows the player for the first part of the
    /// wind-up and only then locks.
    /// </summary>
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

    /// <summary>
    /// Raises its arms, then calls fire down from the sky: <see cref="AttackSpec.Count"/> fireballs fall on spots around the player (the first right where they
    /// stand), each spot marked by a burning ring while its fireball falls, bursting on landing. Keep moving.
    /// </summary>
    Barrage,

    /// <summary>
    /// Draws a bomb back, glowing brighter, and lobs it high at the player: it lands short and bounces on toward them (<see cref="AttackSpec.Count"/> times),
    /// rolls to a stop and goes off, catching anyone within its <see cref="AttackSpec.Splash"/> - or goes off at once if it hits the player, a tree or a rock
    /// on the way. A ring on the ground under it shows what its blast will catch.
    /// </summary>
    Lob,

    /// <summary>
    /// A weapon swung round in front: a wedge <see cref="AttackSpec.Reach"/> long and <see cref="AttackSpec.HitWidth"/> radians either side of where it is aimed,
    /// shown on the ground through the wind-up. With <see cref="AttackSpec.Tracking"/> the wedge turns after the player for the first part of it, then locks.
    /// </summary>
    Swing,

    /// <summary>A Swing that is big, wide and quick to come: the one to dash out of. Its own clip and the same wedge.</summary>
    Cleave,

    /// <summary>
    /// The weapon slammed down to send the ground erupting in a line: a lane <see cref="AttackSpec.Reach"/> long and 2 x <see cref="AttackSpec.HitWidth"/> wide,
    /// the eruption running out along it through the blow and hitting whoever it reaches on the ground. Step out of the lane (or jump it).
    /// </summary>
    LineSlam,

    /// <summary>
    /// Spinning with the weapon out, it chases the player round for the whole blow, faster than it walks (its walking speed times
    /// <see cref="AttackSpec.ProjectileSpeed"/>), hitting everything within <see cref="AttackSpec.Reach"/> every <see cref="AttackSpec.Tick"/> seconds; dizzy
    /// after. Keep running.
    /// </summary>
    Whirlwind,
}

/// <summary>
/// One telegraphed attack and its numbers. Every attack has a wind-up (it stands still and shows what is coming), an active part (the charge, the leap, the spreading
/// ring), and a recovery (it stands still, open to punishment). Distances in metres, times in seconds.
/// </summary>
/// <param name="MinRange">It only starts this attack with the player at least this far away...</param>
/// <param name="MaxRange">...and no further than this.</param>
/// <param name="Reach">Lunge: how far the charge goes. LeapSlam: the landing's radius. Shockwave: how far the ring spreads. Summon: how many it calls. Shoot: how far the bolt flies. Barrage: how far from the player the fireballs spread. Lob: unused.</param>
/// <param name="HitWidth">Lunge: how close to the charging body counts as hit. Shockwave: half the ring's width. Shoot, Lob: the shot's radius. Unused otherwise.</param>
/// <param name="LeapHeight">LeapSlam: the top of the arc, above the straight line from take-off to landing.</param>
/// <param name="ProjectileSpeed">Shoot: how fast the shot flies - slow enough to sidestep once it is loosed. Whirlwind: its speed as a share of its walk.</param>
/// <param name="Splash">Shoot: 0 for a bolt that has to hit the player; more for a fireball that bursts where it lands, hurting anyone within this far. Lob: the bomb's blast.</param>
/// <param name="ProjectileModel">Shoot, Barrage, Lob: the model the shot is drawn with.</param>
/// <param name="Count">Barrage: how many fireballs fall. Lob: how many times the bomb bounces before it rolls. LineSlam: how many rifts at once (1 if 0).</param>
/// <param name="Chain">How many times more it goes straight into the same attack once one ends (a double or triple leap, rolling shockwaves), each with half the wind-up.</param>
/// <param name="Tracking">Lunge: for this many seconds of the wind-up the lane keeps turning to follow the player; then it locks, and stepping out of it is the answer.</param>
/// <param name="Pattern">Barrage: where the fireballs fall (see <see cref="BarragePattern"/>).</param>
/// <param name="Tick">Whirlwind: seconds between its hits while it spins.</param>
/// <param name="Spread">LineSlam: the angle between its rifts (radians), fanned either side of the one aimed at the player.</param>
/// <param name="FollowUp">An attack it goes straight into as this one's blow ends (no recovery in between): the Marauder's rifts off his jump slam.</param>
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
    string? ProjectileModel = null,
    int Count = 0,
    int Chain = 0,
    float Tracking = 0f,
    BarragePattern Pattern = BarragePattern.Scatter,
    float Tick = 0f,
    float Spread = 0f,
    AttackSpec? FollowUp = null);

/// <summary>
/// A boss's next stage, once its health falls to <paramref name="Below"/> of its most (0 to 1): a new set of attacks, a shorter breather between them, a faster walk,
/// and a line to announce it.
/// </summary>
internal sealed record BossPhase(float Below, IReadOnlyList<AttackSpec> Attacks, float AttackCooldown, float SpeedMultiplier, string Announcement)
{
    /// <summary>
    /// Seconds it stops for as the stage begins - the attack under way dropped, no attacking, no walking, and nothing hurts it - roaring, before the stage's
    /// boosts take hold. 0 for none.
    /// </summary>
    public float Roar { get; init; }

    /// <summary>What its damage and its attack speed are multiplied by from this stage on (after any roar): the Marauder's rage and frenzy.</summary>
    public float DamageBoost { get; init; } = 1f;

    public float AttackSpeedBoost { get; init; } = 1f;

    /// <summary>The rage it gains, for the HUD to show (the damage it gives is <see cref="DamageBoost"/>).</summary>
    public int Rage { get; init; }
}

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

    /// <summary>How it moves when it isn't attacking: straight at the player, or stalking them.</summary>
    public EnemyBehaviour Behaviour { get; init; } = EnemyBehaviour.Chase;

    /// <summary>A ranged kind stops walking closer once the player is this near (0: it walks right up).</summary>
    public float StandOff { get; init; }

    /// <summary>A boss's later stages, from the first reached to the last (see <see cref="BossPhase"/>). Empty for everything else.</summary>
    public IReadOnlyList<BossPhase> Phases { get; init; } = Array.Empty<BossPhase>();

    /// <summary>
    /// A second model drawn with the body (a crossbow, a flame, a bomb), which lights up from faint to bright as a <see cref="AttackType.Shoot"/> or a
    /// <see cref="AttackType.Lob"/> winds up. Null for none.
    /// </summary>
    public string? HeldModel { get; init; }

    /// <summary>The first enemy: slow, fragile fodder that shambles straight at the player and claws on contact.</summary>
    public static readonly EnemyKind Ghoul = new(
        Name: "Ghoul",
        Model: "ghoul.glb",
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
        Model: "crossbow_ghoul.glb",
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
        Model: "ghoul_mage.glb",
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

    /// <summary>
    /// Fodder that charges: a ghoul riding a fiendish beast, much tougher than a ghoul on foot and quicker. Once the player is in range it stops and winds up
    /// for 1.5 s - the beast crouching and pawing the ground, the rider levelling its spear - over a red lane that follows the player for the first second and
    /// then locks; then it charges 13 m down the lane. Stepping out of the lane once it locks is the answer. Winded after, open to punishment.
    /// </summary>
    public static readonly EnemyKind BeastRider = new(
        Name: "Ghoul Beast Rider",
        Model: "beast_rider.glb",
        Tier: EnemyTier.Fodder,
        MaxHealth: 100f,
        Speed: 4.2f,
        Radius: 0.65f,
        Height: 2.3f,
        ContactDamage: 10f,
        ContactInterval: 0.8f,
        Experience: 4)
    {
        AttackCooldown = 3.5f,
        Attacks = new[]
        {
            new AttackSpec(AttackType.Lunge, MinRange: 4f, MaxRange: 11f, WindUp: 1.5f, Active: 0.8f, Recover: 0.9f,
                Damage: 16f, Reach: 13f, HitWidth: 0.5f, Knockback: 10f, Tracking: 1f),
        },
    };

    /// <summary>
    /// Ranged fodder that lobs bombs: a ghoul officer that keeps its distance, points the player out and lobs a bomb glowing with ghoulish energy high at them.
    /// It lands short and bounces on toward them, then rolls to a stop and goes off (sooner if it hits the player, a tree or a rock). A ring on the ground
    /// under it shows the blast: get out of it before it stops.
    /// </summary>
    public static readonly EnemyKind GhoulTactician = new(
        Name: "Ghoul Tactician",
        Model: "ghoul_tactician.glb",
        Tier: EnemyTier.Fodder,
        MaxHealth: 40f,
        Speed: 3.1f,
        Radius: 0.4f,
        Height: 1.65f,
        ContactDamage: 6f,
        ContactInterval: 0.8f,
        Experience: 3)
    {
        AttackCooldown = 4f,
        StandOff = 12f,
        HeldModel = "ghoul_tactician_bomb.glb",
        Attacks = new[]
        {
            new AttackSpec(AttackType.Lob, MinRange: 5f, MaxRange: 16f, WindUp: 1.2f, Active: 0.15f, Recover: 0.8f,
                Damage: 18f, Reach: 0f, HitWidth: 0.2f, Knockback: 7f, Splash: 2.5f, ProjectileModel: "ghoul_bomb.glb", Count: 2),
        },
    };

    /// <summary>
    /// A stalker: the rider's fiendish hound running wild, tough and quick. It doesn't rush in: it circles at a distance, working round behind the player,
    /// bolting when they turn to face it or when it is hurt, and pounces - a short, quick lunge - only when it has an opening: their back turned, or a stun.
    /// Then it runs off, and starts again. Keep turning to face them.
    /// </summary>
    public static readonly EnemyKind GhoulBeast = new(
        Name: "Ghoul Beast",
        Model: "ghoul_beast.glb",
        Tier: EnemyTier.Fodder,
        MaxHealth: 150f,
        Speed: 6f,
        Radius: 0.6f,
        Height: 1.8f,
        ContactDamage: 8f,
        ContactInterval: 0.8f,
        Experience: 5)
    {
        Behaviour = EnemyBehaviour.Stalk,
        AttackCooldown = 2.5f,
        Attacks = new[]
        {
            new AttackSpec(AttackType.Lunge, MinRange: 2f, MaxRange: 7.5f, WindUp: 0.55f, Active: 0.4f, Recover: 0.5f,
                Damage: 18f, Reach: 7f, HitWidth: 0.45f, Knockback: 9f, Tracking: 0.3f),
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
        Model: "brute.glb",
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
        Model: "hollow_king.glb",
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

    /// <summary>Every creature the player can meet (not the crate), fodder first, then elites and bosses: the stats page's bestiary. After them all, so they are set.</summary>
    public static readonly IReadOnlyList<EnemyKind> Foes = new[]
    {
        Ghoul, CrossbowGhoul, GhoulMage, BeastRider, GhoulTactician, GhoulBeast, Brute, HollowKing, DelveBosses.HollowKingUnbound, DelveBosses.MarauderUnbound,
    };
}

/// <summary>The boss hunts' bosses, each fought alone in the arena: the Hollow King Unbound, and a tier up from him, the Marauder Unbound.</summary>
internal static class DelveBosses
{
    private const string Fireball = "ghoul_fireball.glb";

    private static AttackSpec Leap(int chain) => new(AttackType.LeapSlam, MinRange: 6f, MaxRange: 26f, WindUp: 1f, Active: 1f, Recover: 1.1f,
        Damage: 45f, Reach: 6.5f, Knockback: 14f, Stun: 0.8f, LeapHeight: 6f, Chain: chain);

    private static AttackSpec Wave(int chain) => new(AttackType.Shockwave, MinRange: 0f, MaxRange: 18f, WindUp: 1.1f, Active: 1.8f, Recover: 0.9f,
        Damage: 32f, Reach: 22f, HitWidth: 0.8f, Knockback: 9f, Chain: chain);

    private static AttackSpec Charge(int chain) => new(AttackType.Lunge, MinRange: 5f, MaxRange: 16f, WindUp: 0.9f, Active: 0.7f, Recover: 1.2f,
        Damage: 40f, Reach: 14.4f, HitWidth: 0.2f, Knockback: 16f, Chain: chain);

    private static AttackSpec Rain(int count, float spread) => new(AttackType.Barrage, MinRange: 0f, MaxRange: 40f, WindUp: 1.2f, Active: 0.1f, Recover: 0.9f,
        Damage: 28f, Reach: spread, Knockback: 7f, ProjectileSpeed: 11f, Splash: 2.4f, ProjectileModel: Fireball, Count: count);

    private static AttackSpec Court(int count) => new(AttackType.Summon, MinRange: 0f, MaxRange: 60f, WindUp: 1f, Active: 0.1f, Recover: 0.6f, Damage: 0f, Reach: count);

    /// <summary>The Crown of Fire: a ring of fireballs round the player, one gap in it, and one on the player to make them move.</summary>
    private static AttackSpec Crown(int chain) => new(AttackType.Barrage, MinRange: 0f, MaxRange: 40f, WindUp: 1.3f, Active: 0.1f, Recover: 1f,
        Damage: 34f, Reach: 5f, Knockback: 7f, ProjectileSpeed: 10f, Splash: 2.4f, ProjectileModel: Fireball, Count: 14, Chain: chain, Pattern: BarragePattern.Ring);

    /// <summary>The Line of Fire: a row of fireballs from the King through the player and 10 m past, landing outward from him.</summary>
    private static AttackSpec Line(int chain) => new(AttackType.Barrage, MinRange: 4f, MaxRange: 30f, WindUp: 1f, Active: 0.1f, Recover: 0.8f,
        Damage: 30f, Reach: 10f, Knockback: 8f, ProjectileSpeed: 12f, Splash: 2f, ProjectileModel: Fireball, Chain: chain, Pattern: BarragePattern.Line);

    /// <summary>
    /// The Hollow King Unbound: 5.6 m tall, eight times the run King's health, and in four stages. At first he leaps, sends shockwaves, charges down a long lane,
    /// calls fire from the sky and crowns the player in a ring of it. Below 70% he calls his court too, sends the Line of Fire, and leaps, charges and sends his
    /// rings twice over. Below 40% he is enraged: faster, barely resting, three of everything. Below 15% he makes his last stand: faster still, and hardly a breath
    /// between attacks. (The boss hunt's fight since 2026-09-27; the Crown, the Line and the last stand were added then, the user asking for more to dodge.)
    /// </summary>
    public static readonly EnemyKind HollowKingUnbound = new(
        Name: "The Hollow King Unbound",
        Model: "hollow_king_unbound.glb",
        Tier: EnemyTier.Boss,
        MaxHealth: 26000f,
        Speed: 3.2f,
        Radius: 1.7f,
        Height: 5.6f,
        ContactDamage: 30f,
        ContactInterval: 1f,
        Experience: 200)
    {
        AttackCooldown = 1.8f,
        Attacks = new[] { Leap(0), Wave(0), Charge(0), Rain(8, 8f), Crown(0) },
        Phases = new[]
        {
            new BossPhase(0.7f, new[] { Leap(1), Wave(1), Charge(1), Rain(10, 9f), Court(8), Line(0), Crown(0) }, AttackCooldown: 1.4f, SpeedMultiplier: 1.1f,
                "The Hollow King Unbound calls his court!"),
            new BossPhase(0.4f, new[] { Leap(2), Wave(2), Charge(2), Rain(14, 11f), Court(10), Line(1), Crown(1) }, AttackCooldown: 1.05f, SpeedMultiplier: 1.25f,
                "The Hollow King Unbound is enraged!"),
            new BossPhase(0.15f, new[] { Leap(2), Wave(2), Charge(2), Rain(18, 12f), Line(2), Crown(2) }, AttackCooldown: 0.75f, SpeedMultiplier: 1.35f,
                "The Hollow King Unbound makes his last stand!"),
        },
    };

    // The Marauder's axe work (the user's, 2026-09-28): quicker than the King's in every wind-up, and hitting harder.

    /// <summary>A two-handed swing round in front, following the player a moment before it locks.</summary>
    private static AttackSpec Chop(int chain) => new(AttackType.Swing, MinRange: 0f, MaxRange: 6.5f, WindUp: 0.55f, Active: 0.2f, Recover: 0.45f,
        Damage: 24f, Reach: 5.6f, HitWidth: 0.95f, Knockback: 6f, Chain: chain, Tracking: 0.3f);

    /// <summary>The cleave: a huge, near-flat arc that comes fast and locks almost at once. Time it, and dash out.</summary>
    private static AttackSpec Cleave() => new(AttackType.Cleave, MinRange: 0f, MaxRange: 8.5f, WindUp: 0.42f, Active: 0.18f, Recover: 1f,
        Damage: 40f, Reach: 8f, HitWidth: 1.45f, Knockback: 11f, Tracking: 0.1f);

    /// <summary>
    /// The jump slam: he leaps high and brings the axe down where the player stood when the circle appeared - a 9 m circle (the user's call, 2026-09-28: it was a
    /// 7 m slam where he stood) - and the moment he lands, three rifts at once, fanned at the player (see <see cref="Aftershock"/>). Quicker than at first
    /// (0.7 s wind-up and 0.9 s in the air): the circle alone was too easy to read.
    /// </summary>
    private static AttackSpec Slam() => new(AttackType.LeapSlam, MinRange: 3f, MaxRange: 22f, WindUp: 0.55f, Active: 0.7f, Recover: 0.9f,
        Damage: 42f, Reach: 9f, Knockback: 14f, Stun: 0.7f, LeapHeight: 6f, FollowUp: Aftershock());

    /// <summary>
    /// The jump slam's follow-up (the user's, 2026-09-28): straight off the landing, three rifts at once fanned 20 degrees apart, the middle one at the player, on
    /// a quick 0.35 s wind-up. Stepping aside from the middle one can put you in the next: dash, or read the slam and be clear of it before he lands.
    /// </summary>
    private static AttackSpec Aftershock() => new(AttackType.LineSlam, MinRange: 0f, MaxRange: 40f, WindUp: 0.35f, Active: 0.35f, Recover: 0.9f,
        Damage: 26f, Reach: 16f, HitWidth: 1.1f, Knockback: 9f, Count: 3, Spread: 0.35f);

    /// <summary>The axe slammed down to split the ground in a line across the arena.</summary>
    private static AttackSpec Rift(int chain) => new(AttackType.LineSlam, MinRange: 4f, MaxRange: 24f, WindUp: 0.75f, Active: 0.55f, Recover: 0.9f,
        Damage: 34f, Reach: 20f, HitWidth: 1.3f, Knockback: 10f, Chain: chain, Tracking: 0.25f);

    /// <summary>
    /// The whirlwind: four seconds spinning after the player 35% faster than he walks (the user's call: 8% at first, then more), the axe out, then dizzy.
    /// Enraged, it is near a run: dash.
    /// </summary>
    private static AttackSpec Spin() => new(AttackType.Whirlwind, MinRange: 0f, MaxRange: 26f, WindUp: 0.8f, Active: 4f, Recover: 1.5f,
        Damage: 11f, Reach: 3.6f, Knockback: 4f, ProjectileSpeed: 1.35f, Tick: 0.35f);

    /// <summary>
    /// The Marauder Unbound (the user's, 2026-09-28): a tier up from the Hollow King Unbound, and unlocked by slaying him. A Ghoul Brute grown huge (4 m), with a
    /// great two-handed axe: swings, a cleave that must be dashed out of, a jump slam onto the player, a slam that splits the ground in a line, and from 70% a whirlwind
    /// that chases the player round the arena. At 35% he stops, roars (nothing hurts him) and flies into a Frenzied Rage for the rest of the fight: 20 rage, +20%
    /// damage, attacks 30% quicker, and faster on his feet. Quicker than the King in every wind-up, and harder to avoid: some blows will land.
    /// </summary>
    public static readonly EnemyKind MarauderUnbound = new(
        Name: "The Marauder Unbound",
        Model: "marauder_unbound.glb",
        Tier: EnemyTier.Boss,
        MaxHealth: 30000f,
        Speed: 4f,
        Radius: 1.4f,
        Height: 4f,
        ContactDamage: 30f,
        ContactInterval: 0.9f,
        Experience: 250)
    {
        AttackCooldown = 1.1f,
        Attacks = new[] { Chop(1), Cleave(), Slam(), Rift(0) },
        Phases = new[]
        {
            new BossPhase(0.7f, new[] { Chop(2), Cleave(), Slam(), Rift(1), Spin() }, AttackCooldown: 0.95f, SpeedMultiplier: 1.1f,
                "The Marauder Unbound starts to spin!"),
            new BossPhase(0.35f, new[] { Chop(2), Cleave(), Slam(), Rift(1), Spin() }, AttackCooldown: 0.8f, SpeedMultiplier: 1.25f,
                "The Marauder Unbound roars, and flies into a Frenzied Rage!")
            {
                Roar = 3f,
                DamageBoost = 1.2f,
                AttackSpeedBoost = 1.3f,
                Rage = 20,
            },
        },
    };
}

/// <summary>How much tougher than its base numbers an enemy spawns - the run director raises these over time.</summary>
internal readonly record struct EnemyScaling(float Health, float Damage, float Speed)
{
    public static readonly EnemyScaling None = new(1f, 1f, 1f);
}
