namespace ArenaMaster.Game.Mage;

/// <summary>The upgrades a Mage can be offered on levelling up. Each stacks up to its own maximum.</summary>
internal enum MageUpgrade
{
    IceShards,
    QuickenedCasting,
    SplinterBolt,
    WinterWind,
    NumbingCold,
    PiercingIce,
    GlacialWard,
    ConcussiveFrost,
    ArcaneVigor,
    FleetStep,
    Attunement,
    FrozenPrecision,
}

/// <summary>One upgrade's name, how many times it stacks, and what the next level of it does.</summary>
internal sealed record MageUpgradeInfo(MageUpgrade Upgrade, string Name, int MaxLevel, string Description);

/// <summary>A choice on the level-up screen: an upgrade and the level it would reach, or (with <see cref="Upgrade"/> null) a heal once everything is maxed.</summary>
internal sealed record MageChoice(MageUpgrade? Upgrade, string Name, string Description, int NewLevel, int MaxLevel);

/// <summary>
/// The Mage's level-up pool: the upgrades, their numbers (kept in <see cref="MageStats"/>), and rolling three to choose from. Mage-only, like everything under
/// Mage/. Glacial Ward and Concussive Frost only turn up once the tree has the Frost Shield or Frost Blast they improve.
/// </summary>
internal static class MageUpgrades
{
    /// <summary>What the level-up screen offers once every upgrade is maxed.</summary>
    public const float SecondWindHeal = 30f;

    public static readonly IReadOnlyList<MageUpgradeInfo> All = new[]
    {
        new MageUpgradeInfo(MageUpgrade.IceShards, "Ice Shards", 5, "+20% cold damage"),
        new MageUpgradeInfo(MageUpgrade.QuickenedCasting, "Quickened Casting", 5, "+12% cast speed"),
        new MageUpgradeInfo(MageUpgrade.SplinterBolt, "Splinter Bolt", 3, "+1 Frost Barrage projectile"),
        new MageUpgradeInfo(MageUpgrade.WinterWind, "Winter Wind", 3, "+20% projectile speed, +15% range"),
        new MageUpgradeInfo(MageUpgrade.NumbingCold, "Numbing Cold", 4, "+8% chill, and chill lasts 0.3 s longer"),
        new MageUpgradeInfo(MageUpgrade.PiercingIce, "Piercing Ice", 2, "Bolts pierce 1 more enemy"),
        new MageUpgradeInfo(MageUpgrade.GlacialWard, "Glacial Ward", 3, "+30% Frost Shield strength"),
        new MageUpgradeInfo(MageUpgrade.ConcussiveFrost, "Concussive Frost", 3, "+25% Frost Blast radius"),
        new MageUpgradeInfo(MageUpgrade.ArcaneVigor, "Arcane Vigor", 5, "+15 max health, and heal 15"),
        new MageUpgradeInfo(MageUpgrade.FleetStep, "Fleet Step", 5, "+8% move speed"),
        new MageUpgradeInfo(MageUpgrade.Attunement, "Attunement", 4, "+35% pickup range"),
        new MageUpgradeInfo(MageUpgrade.FrozenPrecision, "Frozen Precision", 4, "+6% critical chance, +15% critical damage"),
    };

    public static MageUpgradeInfo Info(MageUpgrade upgrade) => All.First(u => u.Upgrade == upgrade);

    /// <summary>Whether <paramref name="upgrade"/> can come up at all for this build (a shield or blast upgrade needs its major).</summary>
    public static bool Offered(MageUpgrade upgrade, MageStats stats) => upgrade switch
    {
        MageUpgrade.GlacialWard => stats.Tree.FrostShield,
        MageUpgrade.ConcussiveFrost => stats.Tree.FrostBlast,
        _ => true,
    };

    /// <summary>
    /// Up to <paramref name="count"/> different upgrades that can be offered, aren't maxed and aren't in <paramref name="excluded"/> (banished this run), picked at
    /// random - or a heal, if none is left.
    /// </summary>
    public static List<MageChoice> Roll(MageStats stats, Random random, int count = 3, IReadOnlySet<MageUpgrade>? excluded = null)
    {
        var open = All.Where(u => Offered(u.Upgrade, stats) && stats.LevelOf(u.Upgrade) < u.MaxLevel && excluded?.Contains(u.Upgrade) != true).ToList();
        if (open.Count == 0)
        {
            return new List<MageChoice> { new(null, "Second Wind", $"Heal {SecondWindHeal:0} health", 0, 0) };
        }

        return open
            .OrderBy(_ => random.Next())
            .Take(count)
            .Select(u => new MageChoice(u.Upgrade, u.Name, u.Description, stats.LevelOf(u.Upgrade) + 1, u.MaxLevel))
            .ToList();
    }

    /// <summary>
    /// The choices with the one at <paramref name="index"/> struck (a banish) and, if the pool has one to spare, a fresh upgrade in its place - never one already offered
    /// or in <paramref name="excluded"/>.
    /// </summary>
    public static List<MageChoice> Replace(IReadOnlyList<MageChoice> choices, int index, MageStats stats, Random random, IReadOnlySet<MageUpgrade> excluded)
    {
        var keep = choices.Where((_, i) => i != index).ToList();
        var skip = new HashSet<MageUpgrade>(excluded);
        skip.UnionWith(keep.Where(c => c.Upgrade is not null).Select(c => c.Upgrade!.Value));
        var fresh = Roll(stats, random, 1, skip).Where(c => c.Upgrade is not null).ToList();
        keep.InsertRange(Math.Min(index, keep.Count), fresh);
        return keep.Count > 0 ? keep : Roll(stats, random, 1, skip);   // nothing left at all: the heal
    }
}
