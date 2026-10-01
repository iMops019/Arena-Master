using ArenaMaster.Game.Progression;

namespace ArenaMaster.Game.Shaman;

/// <summary>
/// The Shaman's second passive tree, Earth Alignment: slow, heavy and tough where Lightning Alignment is quick. With it active the Shaman throws a stone in
/// place of the ball of lightning (<see cref="RollingStone"/>). Three lanes - Stone (the thrown stone: damage, size, cast speed, bounces, how hard it knocks
/// enemies back), Quake (the ground it strikes: quakes, cracks and rifts) and Mountain (the Shaman: health, toughness, the surge and its totem) - over seven
/// tiers opening at tree levels 1, 3, 6, 10, 15, 21 and 28. Two starting nodes; twelve majors, a capstone per lane.
/// </summary>
internal static class EarthTree
{
    public const string TreeId = "earth";

    // Stat names, used in node descriptions as {Name} and summed by EarthBonuses.
    public const string StoneDamage = "Stone damage";
    public const string CastSpeed = "Cast speed";
    public const string StoneSize = "Stone size";
    public const string Bounces = "Bounces";
    public const string Lifetime = "Stone lifetime";
    public const string Knockback = "Knock-back";
    public const string QuakeDamage = "Quake damage";
    public const string QuakeSize = "Quake size";
    public const string CrackDamage = "Aftershock damage";
    public const string CritChance = "Crit chance";
    public const string CritDamage = "Crit damage";
    public const string EliteDamage = "Damage to elites and bosses";
    public const string MaxHealth = "Max health";
    public const string Regeneration = "Health per second";
    public const string DamageTakenCut = "Damage taken cut";
    public const string MoveSpeed = "Move speed";
    public const string SurgeRecharge = "Surge recharge";
    public const string BlockChance = "Block chance";
    public const string TotemTime = "Totem time";

    // The majors, by id.
    public const string Boulder = "boulder";
    public const string Aftershock = "aftershock";
    public const string Stoneskin = "stoneskin";
    public const string Tremor = "tremor";
    public const string GatheringWeight = "gatheringweight";
    public const string RumblingEarth = "rumblingearth";
    public const string EarthenTotem = "totem";
    public const string Shatter = "shatter";
    public const string Upheaval = "upheaval";
    public const string Avalanche = "avalanche";
    public const string TectonicRift = "rift";
    public const string WalkingMountain = "walkingmountain";

    private static TreeNode Minor(string id, string name, int tier, float x, string lane, int max, string[] parents, string text, params (string Stat, float PerRank)[] stats) =>
        new(id, name, tier, x, lane, max, text, parents, stats.ToDictionary(s => s.Stat, s => s.PerRank));

    private static TreeNode Major(string id, string name, int tier, float x, string lane, string[] parents, string text) =>
        new(id, name, tier, x, lane, 1, text, parents, new Dictionary<string, float>(), Major: true);

    private const string S = "Stone", Q = "Quake", M = "Mountain";

    public static readonly TreeDefinition Tree = new(TreeId, "Earth Alignment", new[]
    {
        // Tier 1 (level 1): the two starting choices.
        Minor("heavystone", "Heavy Stone", 1, 330, S, 5, Array.Empty<string>(), "+{Stone damage}% stone damage and +{Stone size}% stone size.", (StoneDamage, 10), (StoneSize, 6)),
        Minor("deeproots", "Deep Roots", 1, 620, M, 5, Array.Empty<string>(), "+{Max health} max health and {Damage taken cut}% less damage taken.", (MaxHealth, 8), (DamageTakenCut, 2)),

        // Tier 2 (level 3)
        Minor("granite", "Granite", 2, 110, S, 5, new[] { "heavystone" }, "+{Stone damage}% stone damage.", (StoneDamage, 12)),
        Minor("strongarm", "Strong Arm", 2, 240, S, 5, new[] { "heavystone" }, "+{Cast speed}% cast speed.", (CastSpeed, 8)),
        Minor("rumble", "Rumble", 2, 380, Q, 5, new[] { "heavystone", "deeproots" }, "+{Quake damage}% quake damage and +{Quake size}% quake size.", (QuakeDamage, 12), (QuakeSize, 6)),
        Major(Aftershock, "Aftershock", 2, 510, Q, new[] { "heavystone", "deeproots" }, "Every bounce leaves a crack that bursts 1 s later, for 100% of the stone's damage within 2 m."),
        Major(Stoneskin, "Stoneskin", 2, 640, M, new[] { "deeproots" }, "Every second you stand still takes 5% off the damage you take, up to 30%. Moving wears it away, 10% a second."),
        Minor("bedrock", "Bedrock", 2, 760, M, 5, new[] { "deeproots" }, "+{Max health} max health and +{Health per second} health per second.", (MaxHealth, 10), (Regeneration, 0.2f)),
        Minor("surefoot", "Sure Footing", 2, 880, M, 3, new[] { "deeproots" }, "The surge recharges {Surge recharge}% faster and +{Move speed}% move speed.", (SurgeRecharge, 10), (MoveSpeed, 3)),

        // Tier 3 (level 6)
        Minor("weighty", "Weighty", 3, 110, S, 3, new[] { "granite" }, "+{Stone size}% stone size and +{Knock-back}% knock-back.", (StoneSize, 15), (Knockback, 20)),
        Major(Boulder, "Boulder", 3, 240, S, new[] { "granite", "strongarm" }, "Stones crush through enemies instead of bouncing off: each one in the way is hit once and knocked aside, and the stone rolls on."),
        Minor("skipping", "Skipping Stone", 3, 370, S, 2, new[] { "strongarm", "rumble" }, "The stone bounces {Bounces} more times before it stops.", (Bounces, 1)),
        Major(Tremor, "Tremor", 3, 500, Q, new[] { "rumble", Aftershock }, "Enemies a quake hits stagger and can't attack for 0.5 s (half that for elites). Bosses shrug it off."),
        Minor("deepcracks", "Deep Cracks", 3, 630, Q, 3, new[] { Aftershock }, "+{Aftershock damage}% damage from the cracks' bursts.", (CrackDamage, 25)),
        Minor("hardened", "Hardened", 3, 760, M, 3, new[] { Stoneskin, "bedrock" }, "{Damage taken cut}% less damage taken.", (DamageTakenCut, 5)),
        Minor("mossback", "Mossback", 3, 890, M, 3, new[] { "bedrock", "surefoot" }, "+{Health per second} health per second.", (Regeneration, 0.3f)),

        // Tier 4 (level 10)
        Minor("flint", "Sharp Flint", 4, 110, S, 5, new[] { "weighty" }, "+{Crit chance}% increased critical chance and +{Crit damage}% critical damage.", (CritChance, 10), (CritDamage, 10)),
        Major(GatheringWeight, "Gathering Weight", 4, 250, S, new[] { Boulder, "weighty" }, "Every bounce makes the stone 20% heavier: it hits harder, and so do its quakes, all the way to its last."),
        Major(RumblingEarth, "Rumbling Earth", 4, 420, Q, new[] { Tremor, "skipping" }, "Every quake reaches 50% further and hits 50% harder."),
        Minor("seismic", "Seismic Reach", 4, 560, Q, 3, new[] { Tremor, "deepcracks" }, "+{Quake size}% quake size.", (QuakeSize, 15)),
        Major(EarthenTotem, "Earthen Totem", 4, 700, M, new[] { "hardened" }, "Every surge plants a stone totem where you surged from. For 4 s, enemies within 6 m of it go for the totem instead of you."),
        Minor("stoneblood", "Stone Blood", 4, 850, M, 3, new[] { "hardened", "mossback" }, "+{Block chance}% chance to turn a blow aside.", (BlockChance, 4)),

        // Tier 5 (level 15)
        Minor("crushing", "Crushing Force", 5, 120, S, 3, new[] { GatheringWeight, "flint" }, "+{Stone damage}% stone damage and +{Cast speed}% cast speed.", (StoneDamage, 15), (CastSpeed, 5)),
        Major(Shatter, "Shatter", 5, 260, S, new[] { GatheringWeight }, "A stone that comes to rest shatters, flinging rubble all round: 150% of its damage within 2.5 m."),
        Minor("quaking", "Quaking Ground", 5, 420, Q, 3, new[] { RumblingEarth, "seismic" }, "+{Quake damage}% quake damage.", (QuakeDamage, 15)),
        Minor("shiftingplates", "Shifting Plates", 5, 560, Q, 3, new[] { "seismic" }, "+{Aftershock damage}% damage from the cracks' bursts and +{Quake size}% quake size.", (CrackDamage, 20), (QuakeSize, 8)),
        Major(Upheaval, "Upheaval", 5, 700, M, new[] { EarthenTotem, "stoneblood" }, "A blow that lands on you makes the ground heave: 150% of the stone's damage within 3 m, throwing enemies back (at most once a second)."),
        Minor("granitehide", "Granite Hide", 5, 860, M, 3, new[] { "stoneblood" }, "+{Max health} max health and +{Health per second} health per second.", (MaxHealth, 15), (Regeneration, 0.3f)),

        // Tier 6 (level 21)
        Minor("mountainweight", "Weight of the Mountain", 6, 150, S, 3, new[] { "crushing", Shatter }, "+{Stone damage}% stone damage and +{Damage to elites and bosses}% damage to elites and bosses.", (StoneDamage, 15), (EliteDamage, 10)),
        Minor("enduring", "Enduring Stone", 6, 290, S, 2, new[] { Shatter }, "Stones last {Stone lifetime} s longer and bounce {Bounces} more times.", (Lifetime, 1), (Bounces, 1)),
        Minor("faultline", "Fault Line", 6, 450, Q, 3, new[] { "quaking", "shiftingplates" }, "+{Quake damage}% quake damage and +{Aftershock damage}% damage from the cracks' bursts.", (QuakeDamage, 15), (CrackDamage, 15)),
        Minor("carver", "Totem Carver", 6, 600, M, 2, new[] { EarthenTotem }, "Totems stand {Totem time} s longer.", (TotemTime, 1)),
        Minor("patience", "Patience of Stone", 6, 740, M, 3, new[] { Upheaval }, "{Damage taken cut}% less damage taken and +{Health per second} health per second.", (DamageTakenCut, 4), (Regeneration, 0.3f)),
        Minor("unyielding", "Unyielding", 6, 880, M, 3, new[] { "granitehide" }, "+{Max health} max health and {Damage taken cut}% less damage taken.", (MaxHealth, 15), (DamageTakenCut, 3)),

        // Tier 7 (level 28): the capstones.
        Major(Avalanche, "Avalanche", 7, 200, S, new[] { "mountainweight", "enduring" }, "Every 6th cast is a rockslide: five stones fanned out."),
        Major(TectonicRift, "Tectonic Rift", 7, 470, Q, new[] { "faultline" }, "A stone splits the ground behind it as it goes: the rift lasts 3 s and hurts what stands in it for 50% of the stone's damage a second."),
        Major(WalkingMountain, "Walking Mountain", 7, 800, M, new[] { "patience", "unyielding", "carver" }, "You have Stoneskin, and it builds twice as fast and never wears away while you move."),
    }, new[] { 1, 3, 6, 10, 15, 21, 28 },
        new[] { (S, 190f), (Q, 480f), (M, 800f) },
        new HashSet<string> { Bounces, Lifetime, MaxHealth, Regeneration, TotemTime });
}

/// <summary>What the ranks spent in Earth Alignment add up to, in the terms <see cref="ShamanStats"/> uses (fractions for percentages).</summary>
internal sealed class EarthBonuses
{
    public float StoneDamage;
    public float CastSpeed;
    public float StoneSize;
    public int Bounces;
    public float Lifetime;
    public float Knockback;
    public float QuakeDamage;
    public float QuakeSize;
    public float CrackDamage;
    public float CritChance;
    public float CritDamage;
    public float EliteDamage;
    public float MaxHealth;
    public float Regeneration;
    public float MoveSpeed;
    public float SurgeRecharge;
    public float BlockChance;
    public float TotemTime;

    /// <summary>What damage taken is multiplied by: each rank of a node that cuts it takes a share off what is left.</summary>
    public float DamageTaken = 1f;

    public bool Boulder;
    public bool Aftershock;
    public bool Stoneskin;
    public bool Tremor;
    public bool GatheringWeight;
    public bool RumblingEarth;
    public bool EarthenTotem;
    public bool Shatter;
    public bool Upheaval;
    public bool Avalanche;
    public bool TectonicRift;
    public bool WalkingMountain;

    public static EarthBonuses From(IReadOnlyDictionary<string, int> ranks)
    {
        var b = new EarthBonuses();
        foreach (var node in EarthTree.Tree.Nodes)
        {
            int r = ranks.GetValueOrDefault(node.Id);
            if (r == 0)
            {
                continue;
            }

            foreach (var (stat, perRank) in node.Stats)
            {
                float v = perRank * r;
                switch (stat)
                {
                    case EarthTree.StoneDamage: b.StoneDamage += v / 100f; break;
                    case EarthTree.CastSpeed: b.CastSpeed += v / 100f; break;
                    case EarthTree.StoneSize: b.StoneSize += v / 100f; break;
                    case EarthTree.Bounces: b.Bounces += (int)v; break;
                    case EarthTree.Lifetime: b.Lifetime += v; break;
                    case EarthTree.Knockback: b.Knockback += v / 100f; break;
                    case EarthTree.QuakeDamage: b.QuakeDamage += v / 100f; break;
                    case EarthTree.QuakeSize: b.QuakeSize += v / 100f; break;
                    case EarthTree.CrackDamage: b.CrackDamage += v / 100f; break;
                    case EarthTree.CritChance: b.CritChance += v / 100f; break;
                    case EarthTree.CritDamage: b.CritDamage += v / 100f; break;
                    case EarthTree.EliteDamage: b.EliteDamage += v / 100f; break;
                    case EarthTree.MaxHealth: b.MaxHealth += v; break;
                    case EarthTree.Regeneration: b.Regeneration += v; break;
                    case EarthTree.MoveSpeed: b.MoveSpeed += v / 100f; break;
                    case EarthTree.SurgeRecharge: b.SurgeRecharge += v / 100f; break;
                    case EarthTree.BlockChance: b.BlockChance += v / 100f; break;
                    case EarthTree.TotemTime: b.TotemTime += v; break;
                    case EarthTree.DamageTakenCut: b.DamageTaken *= MathF.Pow(1f - perRank / 100f, r); break;
                }
            }
        }

        bool Has(string id) => ranks.GetValueOrDefault(id) > 0;
        b.Boulder = Has(EarthTree.Boulder);
        b.Aftershock = Has(EarthTree.Aftershock);
        b.Stoneskin = Has(EarthTree.Stoneskin);
        b.Tremor = Has(EarthTree.Tremor);
        b.GatheringWeight = Has(EarthTree.GatheringWeight);
        b.RumblingEarth = Has(EarthTree.RumblingEarth);
        b.EarthenTotem = Has(EarthTree.EarthenTotem);
        b.Shatter = Has(EarthTree.Shatter);
        b.Upheaval = Has(EarthTree.Upheaval);
        b.Avalanche = Has(EarthTree.Avalanche);
        b.TectonicRift = Has(EarthTree.TectonicRift);
        b.WalkingMountain = Has(EarthTree.WalkingMountain);
        return b;
    }
}
