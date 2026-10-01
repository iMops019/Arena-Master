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

    // Earth Alignment's own.
    RumblingGround,
    BruteStrength,
    DeepCracks,
    SturdyStance,
    TotemCarving,
    WideningRift,
}

/// <summary>One upgrade's name, how many times it stacks, and what the next level of it does.</summary>
internal sealed record ShamanUpgradeInfo(ShamanUpgrade Upgrade, string Name, int MaxLevel, string Description);

/// <summary>A choice on the level-up screen: an upgrade and the level it would reach, or (with <see cref="Upgrade"/> null) a heal once everything is maxed.</summary>
internal sealed record ShamanChoice(ShamanUpgrade? Upgrade, string Name, string Description, int NewLevel, int MaxLevel);

/// <summary>
/// The Shaman's level-up pool: the upgrades, their numbers (kept in <see cref="ShamanStats"/>), and rolling three to choose from. Shaman-only, like everything under
/// Shaman/. What the Shaman throws follows the active tree, and so does the pool: the cards about the throw and the body come up with either tree (named for the
/// ball or the stone, see <see cref="Named"/>), the ones about the lightning's forks, zaps and rods only with Lightning Alignment, and Earth Alignment's own only
/// with it. Rod Mastery only turns up once the tree has Lightning Rod; Deep Cracks, Sturdy Stance, Totem Carving and Widening Rift once it has the major they
/// improve.
/// </summary>
internal static class ShamanUpgrades
{
    /// <summary>What the level-up screen offers once every upgrade is maxed.</summary>
    public const float SecondWindHeal = 30f;

    /// <summary>Every card, as Lightning Alignment names it.</summary>
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
        new ShamanUpgradeInfo(ShamanUpgrade.Overcharge, "Overcharge", 4, "+20% increased critical chance, +15% critical damage"),
        new ShamanUpgradeInfo(ShamanUpgrade.StormBolt, "Storm Bolt", 3, "Balls last 1 s longer"),
        new ShamanUpgradeInfo(ShamanUpgrade.Conductive, "Conductive", 4, "+20% fork damage"),
        new ShamanUpgradeInfo(ShamanUpgrade.HeavySphere, "Heavy Sphere", 3, "+20% ball size"),
        new ShamanUpgradeInfo(ShamanUpgrade.Insulation, "Insulation", 3, "Take 6% less damage"),
        new ShamanUpgradeInfo(ShamanUpgrade.RodMastery, "Rod Mastery", 3, "Lightning rods stay charged 1.5 s longer and zap 20% harder"),
        new ShamanUpgradeInfo(ShamanUpgrade.RumblingGround, "Rumbling Ground", 4, "+25% quake damage, +15% quake size"),
        new ShamanUpgradeInfo(ShamanUpgrade.BruteStrength, "Brute Strength", 3, "Stones knock enemies back 25% further"),
        new ShamanUpgradeInfo(ShamanUpgrade.DeepCracks, "Deep Cracks", 3, "Aftershock cracks burst 30% harder and 15% wider"),
        new ShamanUpgradeInfo(ShamanUpgrade.SturdyStance, "Sturdy Stance", 3, "Stoneskin builds 25% faster and takes up to 5% more off"),
        new ShamanUpgradeInfo(ShamanUpgrade.TotemCarving, "Totem Carving", 3, "Totems stand 1 s longer and draw enemies from 1 m further"),
        new ShamanUpgradeInfo(ShamanUpgrade.WideningRift, "Widening Rift", 3, "Rifts stay open 1 s longer and hurt 20% more"),
    };

    /// <summary>The cards that are about the lightning itself (its forks, zaps and rods): offered only with Lightning Alignment active.</summary>
    private static readonly HashSet<ShamanUpgrade> LightningOnly = new()
    {
        ShamanUpgrade.BranchingBolts, ShamanUpgrade.LongReach, ShamanUpgrade.StaticField, ShamanUpgrade.Conductive, ShamanUpgrade.RodMastery,
    };

    /// <summary>Earth Alignment's own cards: offered only with it active.</summary>
    private static readonly HashSet<ShamanUpgrade> EarthOnly = new()
    {
        ShamanUpgrade.RumblingGround, ShamanUpgrade.BruteStrength, ShamanUpgrade.DeepCracks, ShamanUpgrade.SturdyStance, ShamanUpgrade.TotemCarving,
        ShamanUpgrade.WideningRift,
    };

    /// <summary>What the cards about the throw are called with Earth Alignment active, so they speak of the stone.</summary>
    private static readonly Dictionary<ShamanUpgrade, (string Name, string Description)> StoneNames = new()
    {
        [ShamanUpgrade.ChargedCore] = ("Hardened Stone", "+20% stone damage"),
        [ShamanUpgrade.TwinSpheres] = ("Twin Stones", "+1 stone per cast"),
        [ShamanUpgrade.Resonance] = ("Skimming", "The stone bounces 1 more time"),
        [ShamanUpgrade.Stormstride] = ("Stonestride", "+8% move speed"),
        [ShamanUpgrade.Overcharge] = ("Keen Edge", "+20% increased critical chance, +15% critical damage"),
        [ShamanUpgrade.StormBolt] = ("Long Roll", "Stones last 1 s longer"),
        [ShamanUpgrade.HeavySphere] = ("Great Stone", "+20% stone size"),
        [ShamanUpgrade.Insulation] = ("Thick Hide", "Take 6% less damage"),
    };

    /// <summary>
    /// Whether <paramref name="upgrade"/> can come up at all for this build: the lightning's own cards only with Lightning Alignment active (Rod Mastery with
    /// Lightning Rod), Earth Alignment's only with it (the ones that improve a major, with that major).
    /// </summary>
    public static bool Offered(ShamanUpgrade upgrade, ShamanStats stats)
    {
        if (LightningOnly.Contains(upgrade))
        {
            return !stats.Stone && (upgrade != ShamanUpgrade.RodMastery || stats.Tree.LightningRod);
        }

        if (EarthOnly.Contains(upgrade))
        {
            return stats.Stone && upgrade switch
            {
                ShamanUpgrade.DeepCracks => stats.Earth.Aftershock,
                ShamanUpgrade.SturdyStance => stats.HasStoneskin,
                ShamanUpgrade.TotemCarving => stats.Earth.EarthenTotem,
                ShamanUpgrade.WideningRift => stats.Earth.TectonicRift,
                _ => true,
            };
        }

        return true;
    }

    public static ShamanUpgradeInfo Info(ShamanUpgrade upgrade) => All.First(u => u.Upgrade == upgrade);

    /// <summary>A card's name and text as the Shaman throwing what it throws now would read them (the stone's words with Earth Alignment active).</summary>
    public static (string Name, string Description) Named(ShamanUpgrade upgrade, ShamanStats stats)
    {
        var info = Info(upgrade);
        return stats.Stone && StoneNames.TryGetValue(upgrade, out var stone) ? stone : (info.Name, info.Description);
    }

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
            .Select(u =>
            {
                var (name, description) = Named(u.Upgrade, stats);
                return new ShamanChoice(u.Upgrade, name, description, stats.LevelOf(u.Upgrade) + 1, u.MaxLevel);
            })
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
