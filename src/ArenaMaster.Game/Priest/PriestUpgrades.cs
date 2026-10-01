namespace ArenaMaster.Game.Priest;

/// <summary>The upgrades a Priest can be offered on levelling up. Each stacks up to its own maximum.</summary>
internal enum PriestUpgrade
{
    WickedSkull,
    HollowChant,
    Virulence,
    LongFever,
    BoneSplinter,
    SwiftBones,
    GraveHardiness,
    UnholyMending,
    SkullWard,
    RotWalker,
    GraveRobber,
    CreepingDeath,
    Deathbringer,
    SpreadingRot,
    DeepDecay,
    DeathKnell,
    RestlessDead,
    HardenedBones,
    SoulLure,
    SharpenedBone,
}

/// <summary>One upgrade's name, how many times it stacks, and what the next level of it does.</summary>
internal sealed record PriestUpgradeInfo(PriestUpgrade Upgrade, string Name, int MaxLevel, string Description);

/// <summary>A choice on the level-up screen: an upgrade and the level it would reach, or (with <see cref="Upgrade"/> null) a heal once everything is maxed.</summary>
internal sealed record PriestChoice(PriestUpgrade? Upgrade, string Name, string Description, int NewLevel, int MaxLevel);

/// <summary>
/// The Priest's level-up pool: the upgrades, their numbers (kept in <see cref="PriestStats"/>), and rolling three to choose from. Priest-only, like everything under
/// Priest/. The skull's, the shield's and the body's come up whichever tree is active; the Plague and rot ones only with Unholy (Spreading Rot and Deep Decay once
/// it has Death and Decay), and the dead's only with Grave Calling (most of them once the major they build on is taken).
/// </summary>
internal static class PriestUpgrades
{
    /// <summary>What the level-up screen offers once every upgrade is maxed.</summary>
    public const float SecondWindHeal = 30f;

    public static readonly IReadOnlyList<PriestUpgradeInfo> All = new[]
    {
        new PriestUpgradeInfo(PriestUpgrade.WickedSkull, "Wicked Skull", 5, "+20% skull damage"),
        new PriestUpgradeInfo(PriestUpgrade.HollowChant, "Hollow Chant", 5, "+12% cast speed"),
        new PriestUpgradeInfo(PriestUpgrade.Virulence, "Virulence", 5, "+20% Plague damage"),
        new PriestUpgradeInfo(PriestUpgrade.LongFever, "Long Fever", 3, "Plague lasts 0.3 s longer"),
        new PriestUpgradeInfo(PriestUpgrade.BoneSplinter, "Bone Splinter", 3, "Skulls pierce 1 more enemy"),
        new PriestUpgradeInfo(PriestUpgrade.SwiftBones, "Swift Bones", 3, "+15% skull speed, and skulls last 0.3 s longer"),
        new PriestUpgradeInfo(PriestUpgrade.GraveHardiness, "Grave Hardiness", 5, "+20 max health, and heal 20"),
        new PriestUpgradeInfo(PriestUpgrade.UnholyMending, "Unholy Mending", 5, "+0.5 health per second"),
        new PriestUpgradeInfo(PriestUpgrade.SkullWard, "Skull Ward", 5, "+4% block chance"),
        new PriestUpgradeInfo(PriestUpgrade.RotWalker, "Rot Walker", 5, "+8% move speed"),
        new PriestUpgradeInfo(PriestUpgrade.GraveRobber, "Grave Robber", 4, "+35% pickup range"),
        new PriestUpgradeInfo(PriestUpgrade.CreepingDeath, "Creeping Death", 4, "+12% increased damage over time: Plague, rot and the Aura"),
        new PriestUpgradeInfo(PriestUpgrade.Deathbringer, "Deathbringer", 4, "+15% damage to elites and bosses"),
        new PriestUpgradeInfo(PriestUpgrade.SpreadingRot, "Spreading Rot", 3, "+5% Death and Decay chance"),
        new PriestUpgradeInfo(PriestUpgrade.DeepDecay, "Deep Decay", 3, "+20% rot damage"),
        new PriestUpgradeInfo(PriestUpgrade.DeathKnell, "Death Knell", 4, "+15% damage to enemies under half health"),
        new PriestUpgradeInfo(PriestUpgrade.RestlessDead, "Restless Dead", 5, "+20% servant damage"),
        new PriestUpgradeInfo(PriestUpgrade.HardenedBones, "Hardened Bones", 3, "+25% servant health, and servants last 3 s longer"),
        new PriestUpgradeInfo(PriestUpgrade.SoulLure, "Soul Lure", 3, "Souls are gathered from 0.75 m further and last 2 s longer"),
        new PriestUpgradeInfo(PriestUpgrade.SharpenedBone, "Sharpened Bone", 4, "+25% bone spear damage"),
    };

    public static PriestUpgradeInfo Info(PriestUpgrade upgrade) => All.First(u => u.Upgrade == upgrade);

    /// <summary>
    /// Whether <paramref name="upgrade"/> can come up at all for this build: the Plague and rot ones only with Unholy active (the rot ones once it has Death and
    /// Decay), the dead's only with Grave Calling (the servants' once it has Raise Dead, the souls' Soul Siphon, the spear's Bone Spear).
    /// </summary>
    public static bool Offered(PriestUpgrade upgrade, PriestStats stats) => upgrade switch
    {
        PriestUpgrade.Virulence or PriestUpgrade.LongFever or PriestUpgrade.CreepingDeath => !stats.GraveCalling,
        PriestUpgrade.SpreadingRot or PriestUpgrade.DeepDecay => !stats.GraveCalling && stats.Tree.DeathAndDecay,
        PriestUpgrade.DeathKnell => stats.GraveCalling,
        PriestUpgrade.RestlessDead or PriestUpgrade.HardenedBones => stats.GraveCalling && stats.Grave.RaiseDead,
        PriestUpgrade.SoulLure => stats.GraveCalling && stats.Grave.SoulSiphon,
        PriestUpgrade.SharpenedBone => stats.GraveCalling && stats.Grave.BoneSpear,
        _ => true,
    };

    /// <summary>
    /// Up to <paramref name="count"/> different upgrades that can be offered, aren't maxed and aren't in <paramref name="excluded"/> (banished this run), picked at
    /// random - or a heal, if none is left.
    /// </summary>
    public static List<PriestChoice> Roll(PriestStats stats, Random random, int count = 3, IReadOnlySet<PriestUpgrade>? excluded = null)
    {
        var open = All.Where(u => Offered(u.Upgrade, stats) && stats.LevelOf(u.Upgrade) < u.MaxLevel && excluded?.Contains(u.Upgrade) != true).ToList();
        if (open.Count == 0)
        {
            return new List<PriestChoice> { new(null, "Second Wind", $"Heal {SecondWindHeal:0} health", 0, 0) };
        }

        return open
            .OrderBy(_ => random.Next())
            .Take(count)
            .Select(u => new PriestChoice(u.Upgrade, u.Name, u.Description, stats.LevelOf(u.Upgrade) + 1, u.MaxLevel))
            .ToList();
    }

    /// <summary>
    /// The choices with the one at <paramref name="index"/> struck (a banish) and, if the pool has one to spare, a fresh upgrade in its place - never one already offered
    /// or in <paramref name="excluded"/>.
    /// </summary>
    public static List<PriestChoice> Replace(IReadOnlyList<PriestChoice> choices, int index, PriestStats stats, Random random, IReadOnlySet<PriestUpgrade> excluded)
    {
        var keep = choices.Where((_, i) => i != index).ToList();
        var skip = new HashSet<PriestUpgrade>(excluded);
        skip.UnionWith(keep.Where(c => c.Upgrade is not null).Select(c => c.Upgrade!.Value));
        var fresh = Roll(stats, random, 1, skip).Where(c => c.Upgrade is not null).ToList();
        keep.InsertRange(Math.Min(index, keep.Count), fresh);
        return keep.Count > 0 ? keep : Roll(stats, random, 1, skip);   // nothing left at all: the heal
    }
}
