namespace ArenaMaster.Game.Paladin;

/// <summary>The upgrades a Paladin can be offered on levelling up. Each stacks up to its own maximum.</summary>
internal enum PaladinUpgrade
{
    HolyWrath,
    QuickenedPrayer,
    Radiance,
    Consecration,
    ShieldTraining,
    HeavyPlate,
    PrayerOfMending,
    BarbedPlating,
    PilgrimsStride,
    Gleaner,
    SanctifiedGround,
    ShieldSlam,
    ZealotsEye,
    Steadfast,
    BrambleMail,
    Fervour,
    Hammerfall,
    BloodTithe,
    Onslaught,
    BatteringCharge,
}

/// <summary>One upgrade's name, how many times it stacks, and what the next level of it does.</summary>
internal sealed record PaladinUpgradeInfo(PaladinUpgrade Upgrade, string Name, int MaxLevel, string Description);

/// <summary>A choice on the level-up screen: an upgrade and the level it would reach, or (with <see cref="Upgrade"/> null) a heal once everything is maxed.</summary>
internal sealed record PaladinChoice(PaladinUpgrade? Upgrade, string Name, string Description, int NewLevel, int MaxLevel);

/// <summary>
/// The Paladin's level-up pool: the upgrades, their numbers (kept in <see cref="PaladinStats"/>), and rolling three to choose from. Paladin-only, like everything
/// under Paladin/. The nova's, the circles' and the body's cards come up whichever tree is active. Shield Slam and Steadfast (blocking, standing your ground) come
/// up only with Defiance, and Barbed Plating and Bramble Mail only once Defiance has unlocked thorns. Onslaught comes up only with the Crusade, and the other
/// Crusade cards only once the major they build on is taken: Fervour with Zeal (or Endless Crusade), Hammerfall with the hammers, Blood Tithe with Blood Oath,
/// Battering Charge with Crusader's Rush.
/// </summary>
internal static class PaladinUpgrades
{
    /// <summary>What the level-up screen offers once every upgrade is maxed.</summary>
    public const float SecondWindHeal = 30f;

    public static readonly IReadOnlyList<PaladinUpgradeInfo> All = new[]
    {
        new PaladinUpgradeInfo(PaladinUpgrade.HolyWrath, "Holy Wrath", 5, "+20% Holy Nova damage"),
        new PaladinUpgradeInfo(PaladinUpgrade.QuickenedPrayer, "Quickened Prayer", 5, "+12% nova frequency"),
        new PaladinUpgradeInfo(PaladinUpgrade.Radiance, "Radiance", 4, "+10% Holy Nova radius and holy circle size"),
        new PaladinUpgradeInfo(PaladinUpgrade.Consecration, "Consecration", 4, "+25% holy circle damage, and circles last 0.5 s longer"),
        new PaladinUpgradeInfo(PaladinUpgrade.ShieldTraining, "Shield Training", 5, "+4% block chance"),
        new PaladinUpgradeInfo(PaladinUpgrade.HeavyPlate, "Heavy Plate", 5, "+20 max health, and heal 20"),
        new PaladinUpgradeInfo(PaladinUpgrade.PrayerOfMending, "Prayer of Mending", 5, "+0.5 health per second"),
        new PaladinUpgradeInfo(PaladinUpgrade.BarbedPlating, "Barbed Plating", 4, "+40% thorns damage"),
        new PaladinUpgradeInfo(PaladinUpgrade.PilgrimsStride, "Pilgrim's Stride", 5, "+8% move speed"),
        new PaladinUpgradeInfo(PaladinUpgrade.Gleaner, "Gleaner", 4, "+35% pickup range"),
        new PaladinUpgradeInfo(PaladinUpgrade.SanctifiedGround, "Sanctified Ground", 4, "Holy circles heal 25% more"),
        new PaladinUpgradeInfo(PaladinUpgrade.ShieldSlam, "Shield Slam", 3, "Blocking a blow deals 15 damage to the attacker"),
        new PaladinUpgradeInfo(PaladinUpgrade.ZealotsEye, "Zealot's Eye", 4, "+20% increased critical chance, +15% critical damage"),
        new PaladinUpgradeInfo(PaladinUpgrade.Steadfast, "Steadfast", 3, "+5% block chance while standing still"),
        new PaladinUpgradeInfo(PaladinUpgrade.BrambleMail, "Bramble Mail", 3, "Thorns strike 20% more often"),
        new PaladinUpgradeInfo(PaladinUpgrade.Fervour, "Fervour", 3, "Zeal builds 25% faster and drains 25% slower"),
        new PaladinUpgradeInfo(PaladinUpgrade.Hammerfall, "Hammerfall", 4, "+30% hammer damage"),
        new PaladinUpgradeInfo(PaladinUpgrade.BloodTithe, "Blood Tithe", 3, "Every kill heals 0.5 more health"),
        new PaladinUpgradeInfo(PaladinUpgrade.Onslaught, "Onslaught", 3, "The shield rush recharges 15% faster"),
        new PaladinUpgradeInfo(PaladinUpgrade.BatteringCharge, "Battering Charge", 3, "+30% shield rush damage"),
    };

    public static PaladinUpgradeInfo Info(PaladinUpgrade upgrade) => All.First(u => u.Upgrade == upgrade);

    /// <summary>Whether <paramref name="upgrade"/> can come up at all for this build: the active tree's own cards, and those that build on a major only once it is taken.</summary>
    public static bool Offered(PaladinUpgrade upgrade, PaladinStats stats) => upgrade switch
    {
        PaladinUpgrade.BarbedPlating or PaladinUpgrade.BrambleMail => !stats.CrusadeActive && stats.Tree.CrownOfThorns,
        PaladinUpgrade.ShieldSlam or PaladinUpgrade.Steadfast => !stats.CrusadeActive,
        PaladinUpgrade.Onslaught => stats.CrusadeActive,
        PaladinUpgrade.Fervour => stats.CrusadeActive && stats.BuildsZeal,
        PaladinUpgrade.Hammerfall => stats.CrusadeActive && stats.DropsHammers,
        PaladinUpgrade.BloodTithe => stats.CrusadeActive && stats.Crusade.BloodOath,
        PaladinUpgrade.BatteringCharge => stats.CrusadeActive && stats.Crusade.CrusadersRush,
        _ => true,
    };

    /// <summary>
    /// Up to <paramref name="count"/> different upgrades that can be offered, aren't maxed and aren't in <paramref name="excluded"/> (banished this run), picked at
    /// random - or a heal, if none is left.
    /// </summary>
    public static List<PaladinChoice> Roll(PaladinStats stats, Random random, int count = 3, IReadOnlySet<PaladinUpgrade>? excluded = null)
    {
        var open = All.Where(u => Offered(u.Upgrade, stats) && stats.LevelOf(u.Upgrade) < u.MaxLevel && excluded?.Contains(u.Upgrade) != true).ToList();
        if (open.Count == 0)
        {
            return new List<PaladinChoice> { new(null, "Second Wind", $"Heal {SecondWindHeal:0} health", 0, 0) };
        }

        return open
            .OrderBy(_ => random.Next())
            .Take(count)
            .Select(u => new PaladinChoice(u.Upgrade, u.Name, u.Description, stats.LevelOf(u.Upgrade) + 1, u.MaxLevel))
            .ToList();
    }

    /// <summary>
    /// The choices with the one at <paramref name="index"/> struck (a banish) and, if the pool has one to spare, a fresh upgrade in its place - never one already offered
    /// or in <paramref name="excluded"/>.
    /// </summary>
    public static List<PaladinChoice> Replace(IReadOnlyList<PaladinChoice> choices, int index, PaladinStats stats, Random random, IReadOnlySet<PaladinUpgrade> excluded)
    {
        var keep = choices.Where((_, i) => i != index).ToList();
        var skip = new HashSet<PaladinUpgrade>(excluded);
        skip.UnionWith(keep.Where(c => c.Upgrade is not null).Select(c => c.Upgrade!.Value));
        var fresh = Roll(stats, random, 1, skip).Where(c => c.Upgrade is not null).ToList();
        keep.InsertRange(Math.Min(index, keep.Count), fresh);
        return keep.Count > 0 ? keep : Roll(stats, random, 1, skip);   // nothing left at all: the heal
    }
}
