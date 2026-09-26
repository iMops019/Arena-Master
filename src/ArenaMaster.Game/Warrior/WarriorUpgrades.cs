namespace ArenaMaster.Game.Warrior;

/// <summary>The upgrades a Warrior can be offered on levelling up. Each stacks up to its own maximum.</summary>
internal enum WarriorUpgrade
{
    BrutalSwings,
    QuickHands,
    WideCleave,
    LongAxes,
    Hardy,
    Recovery,
    ParryingBlades,
    BattleStride,
    Plunderer,
    SavageEye,
    Headsman,
    Bloodletting,
    DeepFury,
    LastingRage,
}

/// <summary>One upgrade's name, how many times it stacks, and what the next level of it does.</summary>
internal sealed record WarriorUpgradeInfo(WarriorUpgrade Upgrade, string Name, int MaxLevel, string Description);

/// <summary>A choice on the level-up screen: an upgrade and the level it would reach, or (with <see cref="Upgrade"/> null) a heal once everything is maxed.</summary>
internal sealed record WarriorChoice(WarriorUpgrade? Upgrade, string Name, string Description, int NewLevel, int MaxLevel);

/// <summary>
/// The Warrior's level-up pool: the upgrades, their numbers (kept in <see cref="WarriorStats"/>), and rolling three to choose from. Warrior-only, like everything
/// under Warrior/. Deep Fury and Lasting Rage only turn up once the tree has Berserking, since they do nothing without rage.
/// </summary>
internal static class WarriorUpgrades
{
    /// <summary>What the level-up screen offers once every upgrade is maxed.</summary>
    public const float SecondWindHeal = 30f;

    public static readonly IReadOnlyList<WarriorUpgradeInfo> All = new[]
    {
        new WarriorUpgradeInfo(WarriorUpgrade.BrutalSwings, "Brutal Swings", 5, "+20% Cleave damage"),
        new WarriorUpgradeInfo(WarriorUpgrade.QuickHands, "Quick Hands", 5, "+12% attack speed"),
        new WarriorUpgradeInfo(WarriorUpgrade.WideCleave, "Wide Cleave", 4, "+10% Cleave size: further and wider"),
        new WarriorUpgradeInfo(WarriorUpgrade.LongAxes, "Long Axes", 3, "+12% Cleave reach"),
        new WarriorUpgradeInfo(WarriorUpgrade.Hardy, "Hardy", 5, "+20 max health, and heal 20"),
        new WarriorUpgradeInfo(WarriorUpgrade.Recovery, "Recovery", 5, "+0.5 health per second"),
        new WarriorUpgradeInfo(WarriorUpgrade.ParryingBlades, "Parrying Blades", 5, "+4% block chance"),
        new WarriorUpgradeInfo(WarriorUpgrade.BattleStride, "Battle Stride", 5, "+8% move speed"),
        new WarriorUpgradeInfo(WarriorUpgrade.Plunderer, "Plunderer", 4, "+35% pickup range"),
        new WarriorUpgradeInfo(WarriorUpgrade.SavageEye, "Savage Eye", 4, "+20% increased critical chance, +15% critical damage"),
        new WarriorUpgradeInfo(WarriorUpgrade.Headsman, "Headsman", 4, "+15% damage to elites and bosses"),
        new WarriorUpgradeInfo(WarriorUpgrade.Bloodletting, "Bloodletting", 3, "Heal 0.3% of the Cleave damage you deal"),
        new WarriorUpgradeInfo(WarriorUpgrade.DeepFury, "Deep Fury", 3, "+4 max rage"),
        new WarriorUpgradeInfo(WarriorUpgrade.LastingRage, "Lasting Rage", 3, "Rage lasts 1 s longer"),
    };

    public static WarriorUpgradeInfo Info(WarriorUpgrade upgrade) => All.First(u => u.Upgrade == upgrade);

    /// <summary>Whether <paramref name="upgrade"/> can come up at all for this build (the rage upgrades need Berserking).</summary>
    public static bool Offered(WarriorUpgrade upgrade, WarriorStats stats) =>
        upgrade is not (WarriorUpgrade.DeepFury or WarriorUpgrade.LastingRage) || stats.Tree.Berserking;

    /// <summary>
    /// Up to <paramref name="count"/> different upgrades that can be offered, aren't maxed and aren't in <paramref name="excluded"/> (banished this run), picked at
    /// random - or a heal, if none is left.
    /// </summary>
    public static List<WarriorChoice> Roll(WarriorStats stats, Random random, int count = 3, IReadOnlySet<WarriorUpgrade>? excluded = null)
    {
        var open = All.Where(u => Offered(u.Upgrade, stats) && stats.LevelOf(u.Upgrade) < u.MaxLevel && excluded?.Contains(u.Upgrade) != true).ToList();
        if (open.Count == 0)
        {
            return new List<WarriorChoice> { new(null, "Second Wind", $"Heal {SecondWindHeal:0} health", 0, 0) };
        }

        return open
            .OrderBy(_ => random.Next())
            .Take(count)
            .Select(u => new WarriorChoice(u.Upgrade, u.Name, u.Description, stats.LevelOf(u.Upgrade) + 1, u.MaxLevel))
            .ToList();
    }

    /// <summary>
    /// The choices with the one at <paramref name="index"/> struck (a banish) and, if the pool has one to spare, a fresh upgrade in its place - never one already offered
    /// or in <paramref name="excluded"/>.
    /// </summary>
    public static List<WarriorChoice> Replace(IReadOnlyList<WarriorChoice> choices, int index, WarriorStats stats, Random random, IReadOnlySet<WarriorUpgrade> excluded)
    {
        var keep = choices.Where((_, i) => i != index).ToList();
        var skip = new HashSet<WarriorUpgrade>(excluded);
        skip.UnionWith(keep.Where(c => c.Upgrade is not null).Select(c => c.Upgrade!.Value));
        var fresh = Roll(stats, random, 1, skip).Where(c => c.Upgrade is not null).ToList();
        keep.InsertRange(Math.Min(index, keep.Count), fresh);
        return keep.Count > 0 ? keep : Roll(stats, random, 1, skip);   // nothing left at all: the heal
    }
}
