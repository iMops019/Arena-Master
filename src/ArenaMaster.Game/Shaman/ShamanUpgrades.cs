namespace ArenaMaster.Game.Shaman;

/// <summary>The upgrades a Shaman can be offered on levelling up. Each stacks up to its own maximum.</summary>
internal enum ShamanUpgrade
{
    ChargedCore,
    SwiftCasting,
    TwinSpheres,
    Resonance,
    BranchingBolts,
    LongReach,
    StaticField,
    EarthenHide,
    Stormstride,
    Magnetism,
    Overcharge,
    StormBolt,
    Conductive,
    HeavySphere,
    Insulation,
    RodMastery,
}

/// <summary>One upgrade's name, how many times it stacks, and what the next level of it does.</summary>
internal sealed record ShamanUpgradeInfo(ShamanUpgrade Upgrade, string Name, int MaxLevel, string Description);

/// <summary>A choice on the level-up screen: an upgrade and the level it would reach, or (with <see cref="Upgrade"/> null) a heal once everything is maxed.</summary>
internal sealed record ShamanChoice(ShamanUpgrade? Upgrade, string Name, string Description, int NewLevel, int MaxLevel);

/// <summary>
/// The Shaman's level-up pool: the upgrades, their numbers (kept in <see cref="ShamanStats"/>), and rolling three to choose from. Shaman-only, like everything under
/// Shaman/. Rod Mastery only turns up once the tree has Lightning Rod.
/// </summary>
internal static class ShamanUpgrades
{
    /// <summary>What the level-up screen offers once every upgrade is maxed.</summary>
    public const float SecondWindHeal = 30f;

    public static readonly IReadOnlyList<ShamanUpgradeInfo> All = new[]
    {
        new ShamanUpgradeInfo(ShamanUpgrade.ChargedCore, "Charged Core", 5, "+20% lightning damage"),
        new ShamanUpgradeInfo(ShamanUpgrade.SwiftCasting, "Swift Casting", 5, "+12% cast speed"),
        new ShamanUpgradeInfo(ShamanUpgrade.TwinSpheres, "Twin Spheres", 3, "+1 ball of lightning per cast"),
        new ShamanUpgradeInfo(ShamanUpgrade.Resonance, "Resonance", 3, "The ball bounces 1 more time"),
        new ShamanUpgradeInfo(ShamanUpgrade.BranchingBolts, "Branching Bolts", 3, "Every fork reaches 1 more enemy"),
        new ShamanUpgradeInfo(ShamanUpgrade.LongReach, "Long Reach", 3, "+20% fork range"),
        new ShamanUpgradeInfo(ShamanUpgrade.StaticField, "Static Field", 4, "+25% bounce zap damage, +15% zap size"),
        new ShamanUpgradeInfo(ShamanUpgrade.EarthenHide, "Earthen Hide", 5, "+20 max health, and heal 20"),
        new ShamanUpgradeInfo(ShamanUpgrade.Stormstride, "Stormstride", 5, "+8% move speed"),
        new ShamanUpgradeInfo(ShamanUpgrade.Magnetism, "Magnetism", 4, "+35% pickup range"),
        new ShamanUpgradeInfo(ShamanUpgrade.Overcharge, "Overcharge", 4, "+6% critical chance, +15% critical damage"),
        new ShamanUpgradeInfo(ShamanUpgrade.StormBolt, "Storm Bolt", 3, "Balls last 1 s longer"),
        new ShamanUpgradeInfo(ShamanUpgrade.Conductive, "Conductive", 4, "+20% fork damage"),
        new ShamanUpgradeInfo(ShamanUpgrade.HeavySphere, "Heavy Sphere", 3, "+20% ball size"),
        new ShamanUpgradeInfo(ShamanUpgrade.Insulation, "Insulation", 3, "Take 6% less damage"),
        new ShamanUpgradeInfo(ShamanUpgrade.RodMastery, "Rod Mastery", 3, "Lightning rods stay charged 1.5 s longer and zap 20% harder"),
    };

    /// <summary>Whether <paramref name="upgrade"/> can come up at all for this build (Rod Mastery needs Lightning Rod).</summary>
    public static bool Offered(ShamanUpgrade upgrade, ShamanStats stats) => upgrade != ShamanUpgrade.RodMastery || stats.Tree.LightningRod;

    public static ShamanUpgradeInfo Info(ShamanUpgrade upgrade) => All.First(u => u.Upgrade == upgrade);

    /// <summary>Up to <paramref name="count"/> different upgrades that aren't maxed and aren't in <paramref name="excluded"/> (banished), picked at random - or a heal, if none is left.</summary>
    public static List<ShamanChoice> Roll(ShamanStats stats, Random random, int count = 3, IReadOnlySet<ShamanUpgrade>? excluded = null)
    {
        var open = All.Where(u => Offered(u.Upgrade, stats) && stats.LevelOf(u.Upgrade) < u.MaxLevel && excluded?.Contains(u.Upgrade) != true).ToList();
        if (open.Count == 0)
        {
            return new List<ShamanChoice> { new(null, "Second Wind", $"Heal {SecondWindHeal:0} health", 0, 0) };
        }

        return open
            .OrderBy(_ => random.Next())
            .Take(count)
            .Select(u => new ShamanChoice(u.Upgrade, u.Name, u.Description, stats.LevelOf(u.Upgrade) + 1, u.MaxLevel))
            .ToList();
    }

    /// <summary>The choices with the one at <paramref name="index"/> struck (a banish) and a fresh one in its place if the pool has one to spare.</summary>
    public static List<ShamanChoice> Replace(IReadOnlyList<ShamanChoice> choices, int index, ShamanStats stats, Random random, IReadOnlySet<ShamanUpgrade> excluded)
    {
        var keep = choices.Where((_, i) => i != index).ToList();
        var skip = new HashSet<ShamanUpgrade>(excluded);
        skip.UnionWith(keep.Where(c => c.Upgrade is not null).Select(c => c.Upgrade!.Value));
        var fresh = Roll(stats, random, 1, skip).Where(c => c.Upgrade is not null).ToList();
        keep.InsertRange(Math.Min(index, keep.Count), fresh);
        return keep.Count > 0 ? keep : Roll(stats, random, 1, skip);   // nothing left at all: the heal
    }
}
