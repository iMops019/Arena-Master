namespace ArenaMaster.Game.Combat;

/// <summary>
/// The cave's mini bosses (the user's call, 2026-09-29): what the Stirring (<c>World/Stirring</c>) wakes. Custom, not the boss hunts' bosses, and far lighter than
/// them - a few thousand health at their base, raised like any enemy by how far the run has gone - but tougher than an elite, each with its own few attacks. A kill
/// pays what <see cref="Reward"/> says. They count as elites (a chest's odds, the elite tally), and always spawn Normal.
/// </summary>
internal static class MiniBosses
{
    /// <summary>What a mini boss's kill pays: silver for the run, passive tree experience, a chance of a Delve Mark and a chance of a chest (an elite's odds). No gear.</summary>
    public sealed record MiniBossReward(int Silver, int TreeExperience, float MarkChance, float ItemChance);

    public static readonly MiniBossReward Reward = new(Silver: 75, TreeExperience: 100, MarkChance: 0.12f, ItemChance: 0.35f);

    /// <summary>
    /// The Gorewatcher: a bloated butcher of a ghoul, 3.5 m tall, with a great cleaver. It charges (twice in a row), slams down onto the player, and drives the cleaver
    /// into the ground to send a ring out along it.
    /// </summary>
    public static readonly EnemyKind Gorewatcher = new(
        Name: "The Gorewatcher",
        Model: "gorewatcher.glb",
        Tier: EnemyTier.Elite,
        MaxHealth: 2600f,
        Speed: 3.6f,
        Radius: 1.1f,
        Height: 3.5f,
        ContactDamage: 22f,
        ContactInterval: 1f,
        Experience: 60)
    {
        DrawScale = 1.45f,
        AttackCooldown = 1.8f,
        Attacks = new[]
        {
            new AttackSpec(AttackType.Lunge, MinRange: 4f, MaxRange: 12f, WindUp: 0.8f, Active: 0.5f, Recover: 0.9f,
                Damage: 28f, Reach: 10f, HitWidth: 0.9f, Knockback: 12f, Chain: 1, Tracking: 0.3f),
            new AttackSpec(AttackType.LeapSlam, MinRange: 5f, MaxRange: 16f, WindUp: 0.8f, Active: 0.8f, Recover: 0.9f,
                Damage: 32f, Reach: 4.5f, Knockback: 12f, Stun: 0.6f, LeapHeight: 4f),
            new AttackSpec(AttackType.Shockwave, MinRange: 0f, MaxRange: 10f, WindUp: 0.9f, Active: 1.3f, Recover: 0.8f,
                Damage: 24f, Reach: 13f, HitWidth: 0.7f, Knockback: 9f),
        },
    };

    /// <summary>
    /// The Bone Weaver: a ghoul caster 2.4 m tall in a robe of bones and a crown of antlers, keeping its distance. It hurls violet fire three times in a row, calls fire
    /// down round the player, and calls ghouls up round itself.
    /// </summary>
    public static readonly EnemyKind BoneWeaver = new(
        Name: "The Bone Weaver",
        Model: "bone_weaver.glb",
        Tier: EnemyTier.Elite,
        MaxHealth: 1800f,
        Speed: 3.2f,
        Radius: 0.64f,
        Height: 2.4f,
        ContactDamage: 14f,
        ContactInterval: 0.9f,
        Experience: 60)
    {
        DrawScale = 1.6f,
        AttackCooldown = 2f,
        StandOff = 12f,
        HeldModel = "bone_weaver_flame.glb",
        ShotHeight = 1.7f,
        ShotForward = 0.9f,
        Attacks = new[]
        {
            new AttackSpec(AttackType.Shoot, MinRange: 3f, MaxRange: 22f, WindUp: 0.9f, Active: 0.1f, Recover: 0.7f,
                Damage: 20f, Reach: 30f, HitWidth: 0.35f, Knockback: 6f, ProjectileSpeed: 15f, Splash: 2.2f, ProjectileModel: "ghoul_fireball.glb", Chain: 2),
            new AttackSpec(AttackType.Barrage, MinRange: 0f, MaxRange: 30f, WindUp: 1.1f, Active: 0.2f, Recover: 0.8f,
                Damage: 22f, Reach: 7f, Knockback: 6f, ProjectileSpeed: 12f, Splash: 2.2f, ProjectileModel: "ghoul_fireball.glb", Count: 8),
            new AttackSpec(AttackType.Summon, MinRange: 0f, MaxRange: 40f, WindUp: 1f, Active: 0.1f, Recover: 0.6f, Damage: 0f, Reach: 5f),
        },
    };

    /// <summary>
    /// The Gutter Hound: a huge mangy ghoul hound, 3 m at the shoulder, in a spiked iron collar. Quick; it charges at the player down a lane (twice in a row) and
    /// leaps onto them.
    /// </summary>
    public static readonly EnemyKind GutterHound = new(
        Name: "The Gutter Hound",
        Model: "gutter_hound.glb",
        Tier: EnemyTier.Elite,
        MaxHealth: 2200f,
        Speed: 5.2f,
        Radius: 1f,
        Height: 3.1f,
        ContactDamage: 20f,
        ContactInterval: 0.8f,
        Experience: 60)
    {
        DrawScale = 1.7f,
        AttackCooldown = 1.6f,
        Attacks = new[]
        {
            new AttackSpec(AttackType.Lunge, MinRange: 3f, MaxRange: 13f, WindUp: 0.65f, Active: 0.45f, Recover: 0.8f,
                Damage: 26f, Reach: 11f, HitWidth: 0.8f, Knockback: 11f, Chain: 1, Tracking: 0.4f),
            new AttackSpec(AttackType.LeapSlam, MinRange: 4f, MaxRange: 14f, WindUp: 0.6f, Active: 0.7f, Recover: 0.8f,
                Damage: 28f, Reach: 3.5f, Knockback: 10f, Stun: 0.5f, LeapHeight: 3.5f),
        },
    };

    public static readonly IReadOnlyList<EnemyKind> All = new[] { Gorewatcher, BoneWeaver, GutterHound };

    public static bool Is(EnemyKind kind) => All.Contains(kind);
}
