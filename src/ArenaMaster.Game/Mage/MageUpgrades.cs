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
    GlacialSpikes,
    IceArmor,
    Shatterpoint,
    DeepChill,
    BlastPower,
    FanTheFlames,
    LastingEmbers,
    CoolingBreath,
    BlastingAsh,
    RekindledWard,
}

/// <summary>
/// One upgrade's name, how many times it stacks, and what the next level of it does; and, for a card offered with either tree, its name and text with the Fire
/// Barrage (null where they read the same).
/// </summary>
internal sealed record MageUpgradeInfo(MageUpgrade Upgrade, string Name, int MaxLevel, string Description, string? FireName = null, string? FireDescription = null)
{
    /// <summary>The name and text as the card shows them, for the barrage's element.</summary>
    public (string Name, string Description) Shown(MageStats stats) =>
        stats.Fire ? (FireName ?? Name, FireDescription ?? Description) : (Name, Description);
}

/// <summary>A choice on the level-up screen: an upgrade and the level it would reach, or (with <see cref="Upgrade"/> null) a heal once everything is maxed.</summary>
internal sealed record MageChoice(MageUpgrade? Upgrade, string Name, string Description, int NewLevel, int MaxLevel);

/// <summary>
/// The Mage's level-up pool: the upgrades, their numbers (kept in <see cref="MageStats"/>), and rolling three to choose from. Mage-only, like everything under
/// Mage/. The cards about the barrage and the body come up with either tree, named for its element (Ice Shards with Frost, Searing Bolts with Pyromancy). Numbing
/// Cold and Glacial Spikes only come up with Frost active, and Glacial Ward, Concussive Frost, Blast Power and Deep Chill once the Frost tree has the Frost Shield,
/// Frost Blast or Deep Freeze they improve. Fan the Flames and Lasting Embers only come up with Pyromancy active, and Cooling Breath, Blasting Ash and Rekindled
/// Ward once the Pyromancy tree has the Heat, Combustion or Flame Ward they improve.
/// </summary>
internal static class MageUpgrades
{
    /// <summary>What the level-up screen offers once every upgrade is maxed.</summary>
    public const float SecondWindHeal = 30f;

    public static readonly IReadOnlyList<MageUpgradeInfo> All = new[]
    {
        new MageUpgradeInfo(MageUpgrade.IceShards, "Ice Shards", 5, "+20% cold damage", "Searing Bolts", "+20% fire damage"),
        new MageUpgradeInfo(MageUpgrade.QuickenedCasting, "Quickened Casting", 5, "+12% cast speed"),
        new MageUpgradeInfo(MageUpgrade.SplinterBolt, "Splinter Bolt", 3, "+1 Frost Barrage projectile", "Split Flame", "+1 Fire Barrage projectile"),
        new MageUpgradeInfo(MageUpgrade.WinterWind, "Winter Wind", 3, "+20% projectile speed, +15% range", "Hot Wind"),
        new MageUpgradeInfo(MageUpgrade.NumbingCold, "Numbing Cold", 4, "+8% chill, and chill lasts 0.3 s longer"),
        new MageUpgradeInfo(MageUpgrade.PiercingIce, "Piercing Ice", 2, "Bolts pierce 1 more enemy", "Piercing Flame"),
        new MageUpgradeInfo(MageUpgrade.GlacialWard, "Glacial Ward", 3, "+30% Frost Shield strength"),
        new MageUpgradeInfo(MageUpgrade.ConcussiveFrost, "Concussive Frost", 3, "+25% Frost Blast radius"),
        new MageUpgradeInfo(MageUpgrade.ArcaneVigor, "Arcane Vigor", 5, "+15 max health, and heal 15"),
        new MageUpgradeInfo(MageUpgrade.FleetStep, "Fleet Step", 5, "+8% move speed"),
        new MageUpgradeInfo(MageUpgrade.Attunement, "Attunement", 4, "+35% pickup range"),
        new MageUpgradeInfo(MageUpgrade.FrozenPrecision, "Frozen Precision", 4, "+20% increased critical chance, +15% critical damage", "Searing Eye"),
        new MageUpgradeInfo(MageUpgrade.GlacialSpikes, "Glacial Spikes", 4, "+20% damage to chilled and frozen enemies"),
        new MageUpgradeInfo(MageUpgrade.IceArmor, "Ice Armor", 3, "Take 6% less damage", "Ash Armor"),
        new MageUpgradeInfo(MageUpgrade.Shatterpoint, "Shatterpoint", 4, "+15% damage to elites and bosses", "Slayer's Flame"),
        new MageUpgradeInfo(MageUpgrade.DeepChill, "Deep Chill", 3, "+3% freeze chance, and freezes last 0.3 s longer"),
        new MageUpgradeInfo(MageUpgrade.BlastPower, "Blast Power", 3, "Frost Blast deals 25% more damage"),
        new MageUpgradeInfo(MageUpgrade.FanTheFlames, "Fan the Flames", 5, "+20% burn damage"),
        new MageUpgradeInfo(MageUpgrade.LastingEmbers, "Lasting Embers", 3, "Burns last 0.5 s longer"),
        new MageUpgradeInfo(MageUpgrade.CoolingBreath, "Cooling Breath", 3, "Heat cools 1.5 a second faster"),
        new MageUpgradeInfo(MageUpgrade.BlastingAsh, "Blasting Ash", 3, "+25% Combustion damage, +10% Combustion radius"),
        new MageUpgradeInfo(MageUpgrade.RekindledWard, "Rekindled Ward", 3, "Flame Ward comes back 1.5 s sooner"),
    };

    public static MageUpgradeInfo Info(MageUpgrade upgrade) => All.First(u => u.Upgrade == upgrade);

    /// <summary>
    /// Whether <paramref name="upgrade"/> can come up at all for this build: a card about the cold only with Frost active, one about burning only with Pyromancy,
    /// and one that improves a major only with that major.
    /// </summary>
    public static bool Offered(MageUpgrade upgrade, MageStats stats) => upgrade switch
    {
        MageUpgrade.NumbingCold or MageUpgrade.GlacialSpikes => !stats.Fire,
        MageUpgrade.GlacialWard => !stats.Fire && stats.Tree.FrostShield,
        MageUpgrade.ConcussiveFrost or MageUpgrade.BlastPower => !stats.Fire && stats.Tree.FrostBlast,
        MageUpgrade.DeepChill => !stats.Fire && stats.Tree.DeepFreeze,
        MageUpgrade.FanTheFlames or MageUpgrade.LastingEmbers => stats.Fire,
        MageUpgrade.CoolingBreath => stats.Fire && stats.Pyro.Heat,
        MageUpgrade.BlastingAsh => stats.Fire && stats.Pyro.Combustion,
        MageUpgrade.RekindledWard => stats.Fire && stats.Pyro.FlameWard,
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
            .Select(u => (Info: u, Shown: u.Shown(stats)))
            .Select(u => new MageChoice(u.Info.Upgrade, u.Shown.Name, u.Shown.Description, stats.LevelOf(u.Info.Upgrade) + 1, u.Info.MaxLevel))
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
