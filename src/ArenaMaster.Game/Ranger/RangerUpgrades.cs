namespace ArenaMaster.Game.Ranger;

/// <summary>The upgrades a Ranger can be offered on levelling up. Each stacks up to its own maximum.</summary>
internal enum RangerUpgrade
{
    SharpenedTips,
    QuickDraw,
    SplitShot,
    PiercingArrows,
    Deadeye,
    Fletching,
    FleetFoot,
    Vitality,
    Scavenger,

    // The Trapper tree's, offered only with it active.
    TrapSetter,
    HeavySnares,
    StrongVenom,
    SlowPoison,
    SharpTalons,
    HawkTraining,
}

/// <summary>One upgrade's name, how many times it stacks, and what the next level of it does.</summary>
internal sealed record RangerUpgradeInfo(RangerUpgrade Upgrade, string Name, int MaxLevel, string Description);

/// <summary>A choice on the level-up screen: an upgrade and the level it would reach, or (with <see cref="Upgrade"/> null) a heal once everything is maxed.</summary>
internal sealed record UpgradeChoice(RangerUpgrade? Upgrade, string Name, string Description, int NewLevel, int MaxLevel);

/// <summary>
/// The Ranger's level-up pool: the upgrades, their numbers (kept in <see cref="RangerStats"/>), and rolling three to choose from. Ranger-only, like everything
/// under Ranger/ - another class gets a pool of its own. The first nine are about the bow and the body, and are offered whichever tree is active; the rest are the
/// Trapper's, offered only with it active, and most only once the major they build on is taken.
/// </summary>
internal static class RangerUpgrades
{
    /// <summary>What the level-up screen offers once every upgrade is maxed.</summary>
    public const float SecondWindHeal = 30f;

    public static readonly IReadOnlyList<RangerUpgradeInfo> All = new[]
    {
        new RangerUpgradeInfo(RangerUpgrade.SharpenedTips, "Sharpened Tips", 5, "+20% arrow damage"),
        new RangerUpgradeInfo(RangerUpgrade.QuickDraw, "Quick Draw", 5, "+15% attack speed"),
        new RangerUpgradeInfo(RangerUpgrade.SplitShot, "Split Shot", 4, "+1 arrow per shot, fanned out"),
        new RangerUpgradeInfo(RangerUpgrade.PiercingArrows, "Piercing Arrows", 3, "Arrows pass through 1 more enemy"),
        new RangerUpgradeInfo(RangerUpgrade.Deadeye, "Deadeye", 5, "+20% increased critical chance (crits deal double)"),
        new RangerUpgradeInfo(RangerUpgrade.Fletching, "Fletching", 3, "+20% arrow speed, +15% range"),
        new RangerUpgradeInfo(RangerUpgrade.FleetFoot, "Fleet Foot", 5, "+8% move speed"),
        new RangerUpgradeInfo(RangerUpgrade.Vitality, "Vitality", 5, "+20 max health, and heal 20"),
        new RangerUpgradeInfo(RangerUpgrade.Scavenger, "Scavenger", 4, "+35% pickup range"),
        new RangerUpgradeInfo(RangerUpgrade.TrapSetter, "Trap Setter", 3, "Your dash recharges 10% faster"),
        new RangerUpgradeInfo(RangerUpgrade.HeavySnares, "Heavy Snares", 4, "+25% trap damage"),
        new RangerUpgradeInfo(RangerUpgrade.StrongVenom, "Strong Venom", 5, "+20% poison damage"),
        new RangerUpgradeInfo(RangerUpgrade.SlowPoison, "Slow Poison", 3, "Poison lasts 0.5 s longer"),
        new RangerUpgradeInfo(RangerUpgrade.SharpTalons, "Sharp Talons", 4, "+20% hawk damage"),
        new RangerUpgradeInfo(RangerUpgrade.HawkTraining, "Hawk Training", 3, "The hawk dives 10% more often"),
    };

    public static RangerUpgradeInfo Info(RangerUpgrade upgrade) => All.First(u => u.Upgrade == upgrade);

    /// <summary>
    /// Whether <paramref name="upgrade"/> can be offered now: the bow's and the body's always; the Trapper's only with that tree active, and those that build on a
    /// major only once it is taken.
    /// </summary>
    public static bool Offered(RangerUpgrade upgrade, RangerStats stats) => upgrade switch
    {
        RangerUpgrade.TrapSetter => stats.TrapperActive,
        RangerUpgrade.HeavySnares => stats.TrapperActive && stats.Trapper.SnareLine,
        RangerUpgrade.StrongVenom or RangerUpgrade.SlowPoison => stats.TrapperActive && stats.Trapper.VenomTips,
        RangerUpgrade.SharpTalons or RangerUpgrade.HawkTraining => stats.TrapperActive && stats.Trapper.HawkCompanion,
        _ => true,
    };

    /// <summary>
    /// Up to <paramref name="count"/> different upgrades that can be offered (<see cref="Offered"/>), aren't maxed yet and aren't in <paramref name="excluded"/>
    /// (banished this run), picked at random - or a heal, if none is left.
    /// </summary>
    public static List<UpgradeChoice> Roll(RangerStats stats, Random random, int count = 3, IReadOnlySet<RangerUpgrade>? excluded = null)
    {
        var open = All.Where(u => Offered(u.Upgrade, stats) && stats.LevelOf(u.Upgrade) < u.MaxLevel && excluded?.Contains(u.Upgrade) != true).ToList();
        if (open.Count == 0)
        {
            return new List<UpgradeChoice> { new(null, "Second Wind", $"Heal {SecondWindHeal:0} health", 0, 0) };
        }

        return open
            .OrderBy(_ => random.Next())
            .Take(count)
            .Select(u => new UpgradeChoice(u.Upgrade, u.Name, u.Description, stats.LevelOf(u.Upgrade) + 1, u.MaxLevel))
            .ToList();
    }

    /// <summary>
    /// The choices with the one at <paramref name="index"/> struck (a banish) and, if the pool has one to spare, a fresh upgrade in its place - never one already offered
    /// or in <paramref name="excluded"/>.
    /// </summary>
    public static List<UpgradeChoice> Replace(IReadOnlyList<UpgradeChoice> choices, int index, RangerStats stats, Random random, IReadOnlySet<RangerUpgrade> excluded)
    {
        var keep = choices.Where((_, i) => i != index).ToList();
        var skip = new HashSet<RangerUpgrade>(excluded);
        skip.UnionWith(keep.Where(c => c.Upgrade is not null).Select(c => c.Upgrade!.Value));
        var fresh = Roll(stats, random, 1, skip).Where(c => c.Upgrade is not null).ToList();
        keep.InsertRange(Math.Min(index, keep.Count), fresh);
        return keep.Count > 0 ? keep : Roll(stats, random, 1, skip);   // nothing left at all: the heal
    }
}
