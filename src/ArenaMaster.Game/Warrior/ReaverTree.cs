using ArenaMaster.Game.Progression;

namespace ArenaMaster.Game.Warrior;

/// <summary>
/// The Warrior's second passive tree: the axes thrown, and the wounds they leave. With it active the swings become throws (<see cref="ThrownAxes"/>): each axe spins
/// out at the nearest enemy and back to the hand. Three lanes - Throw (the axes: damage, speed, how far they fly, crits), Bleed (Hemorrhage, the tree's heart, and
/// everything that makes wounds bleed longer and harder) and Hunt (moving and charging in: speed, the battle charge, health) - over seven tiers opening at tree
/// levels 1, 3, 6, 10, 15, 21 and 28. Two starting nodes; twelve majors, one capstone per lane.
/// </summary>
internal static class ReaverTree
{
    public const string TreeId = "reaver";

    // Stat names, used in node descriptions as {Name} and summed by ReaverBonuses.
    public const string ThrowDamage = "Throw damage";
    public const string ThrowRange = "Throw range";
    public const string AxeSpeed = "Axe speed";
    public const string AttackSpeed = "Attack speed";
    public const string CritChance = "Crit chance";
    public const string CritDamage = "Crit damage";
    public const string BleedDamage = "Bleed damage";
    public const string BleedDuration = "Bleed duration";
    public const string EliteDamage = "Damage to elites and bosses";
    public const string LifeLeech = "Life leech";
    public const string MoveSpeed = "Move speed";
    public const string ChargeRecharge = "Charge recharge";
    public const string MaxHealth = "Max health";
    public const string Regeneration = "Health per second";
    public const string DamageTakenCut = "Damage taken cut";
    public const string Axes = "Axes";
    public const string Chains = "Bounces";

    // The majors, by id.
    public const string Catch = "catch";
    public const string Hemorrhage = "hemorrhage";
    public const string RunThemDown = "runthemdown";
    public const string RicochetAxes = "ricochet";
    public const string DeepWounds = "deepwounds";
    public const string ClosingIn = "closingin";
    public const string ReturningEdge = "returningedge";
    public const string Bloodbath = "bloodbath";
    public const string BloodScent = "bloodscent";
    public const string AxeStorm = "axestorm";
    public const string CrimsonTide = "crimsontide";
    public const string Harvester = "harvester";

    private static TreeNode Minor(string id, string name, int tier, float x, string lane, int max, string[] parents, string text, params (string Stat, float PerRank)[] stats) =>
        new(id, name, tier, x, lane, max, text, parents, stats.ToDictionary(s => s.Stat, s => s.PerRank));

    private static TreeNode Major(string id, string name, int tier, float x, string lane, string[] parents, string text) =>
        new(id, name, tier, x, lane, 1, text, parents, new Dictionary<string, float>(), Major: true);

    private const string T = "Throw", B = "Bleed", H = "Hunt";

    public static readonly TreeDefinition Tree = new(TreeId, "Reaver", new[]
    {
        // Tier 1 (level 1): the two starting choices.
        Minor("balanced", "Balanced Axes", 1, 250, T, 5, Array.Empty<string>(), "+{Throw damage}% throw damage, and the axes fly {Throw range}% further.", (ThrowDamage, 10), (ThrowRange, 4)),
        Minor("serrated", "Serrated Edge", 1, 640, B, 5, Array.Empty<string>(), "+{Bleed damage}% bleed damage and +{Crit damage}% critical damage.", (BleedDamage, 12), (CritDamage, 8)),

        // Tier 2 (level 3)
        Minor("keen", "Keen Edges", 2, 90, T, 5, new[] { "balanced" }, "+{Throw damage}% throw damage.", (ThrowDamage, 12)),
        Minor("quick", "Quick Release", 2, 210, T, 5, new[] { "balanced" }, "+{Attack speed}% attack speed.", (AttackSpeed, 8)),
        Major(Catch, "Catch", 2, 330, T, new[] { "balanced" }, "Catching a returning axe makes your next throw 50% stronger."),
        Major(Hemorrhage, "Hemorrhage", 2, 480, B, new[] { "balanced", "serrated" },
            "Hits make enemies bleed: 30% of the hit's damage over 4 s. Bleeding stacks up to 5 times."),
        Minor("fleet", "Fleet of Foot", 2, 680, H, 5, new[] { "serrated" }, "+{Move speed}% move speed.", (MoveSpeed, 4)),
        Minor("stride", "Hunter's Stride", 2, 820, H, 3, new[] { "serrated" }, "The battle charge recharges {Charge recharge}% faster.", (ChargeRecharge, 10)),

        // Tier 3 (level 6)
        Minor("skipping", "Skipping Axes", 3, 80, T, 2, new[] { "keen" }, "Your axes bounce to {Bounces} more enemies before they turn back.", (Chains, 1)),
        Minor("spare", "Spare Axes", 3, 190, T, 2, new[] { "quick", Catch }, "Every throw sends {Axes} more axes, fanned out.", (Axes, 1)),
        Minor("marksman", "Eye for the Throat", 3, 300, T, 5, new[] { "quick", Catch }, "+{Crit chance}% increased critical chance and +{Crit damage}% critical damage.",
            (CritChance, 10), (CritDamage, 10)),
        Minor("gash", "Gash", 3, 420, B, 5, new[] { Hemorrhage }, "+{Bleed damage}% bleed damage.", (BleedDamage, 15)),
        Minor("veins", "Open Veins", 3, 530, B, 3, new[] { Hemorrhage }, "Bleeding lasts {Bleed duration} s longer.", (BleedDuration, 0.5f)),
        Major(RunThemDown, "Run Them Down", 3, 660, H, new[] { "fleet", "stride" },
            "The battle charge hits everything it passes for a throw's damage, and leaves it bleeding at full stacks (even without Hemorrhage)."),
        Minor("hide", "Hunter's Hide", 3, 800, H, 3, new[] { "stride" }, "+{Max health} max health and {Damage taken cut}% less damage taken.", (MaxHealth, 10), (DamageTakenCut, 3)),

        // Tier 4 (level 10)
        Major(RicochetAxes, "Ricochet Axes", 4, 110, T, new[] { "skipping", "spare" },
            "Your axes bounce to 2 more enemies, and find the next one up to 8 m away, not 5."),
        Minor("heavy", "Heavy Heads", 4, 250, T, 3, new[] { "spare", "marksman" }, "+{Throw damage}% throw damage and +{Crit damage}% critical damage.", (ThrowDamage, 10), (CritDamage, 10)),
        Minor("bloodletter", "Bloodletter", 4, 400, B, 3, new[] { "gash" }, "+{Bleed damage}% bleed damage.", (BleedDamage, 15)),
        Major(DeepWounds, "Deep Wounds", 4, 530, B, new[] { "gash", "veins" }, "Bleeding stacks up to 8 times, not 5."),
        Major(ClosingIn, "Closing In", 4, 670, H, new[] { RunThemDown }, "Your axes hit enemies within 4 m of you 30% harder."),
        Minor("pursuit", "Pursuit", 4, 810, H, 3, new[] { RunThemDown, "hide" }, "+{Move speed}% move speed, and the battle charge recharges {Charge recharge}% faster.",
            (MoveSpeed, 4), (ChargeRecharge, 8)),

        // Tier 5 (level 15)
        Minor("whirling", "Whirling Axes", 5, 90, T, 3, new[] { RicochetAxes }, "+{Attack speed}% attack speed, and the axes fly {Axe speed}% faster.", (AttackSpeed, 8), (AxeSpeed, 10)),
        Major(ReturningEdge, "Returning Edge", 5, 230, T, new[] { RicochetAxes, "heavy" }, "An axe hits 50% harder on its way back."),
        Major(Bloodbath, "Bloodbath", 5, 390, B, new[] { "bloodletter", DeepWounds },
            "A bleeding enemy that dies leaves a pool of blood (2 m, 4 s). Standing in one heals you 2% of your max health a second."),
        Minor("sanguine", "Weeping Wounds", 5, 520, B, 3, new[] { DeepWounds }, "+{Bleed damage}% bleed damage, and bleeding lasts {Bleed duration} s longer.", (BleedDamage, 15), (BleedDuration, 0.5f)),
        Major(BloodScent, "Blood Scent", 5, 660, H, new[] { ClosingIn, "pursuit" },
            "For every bleeding enemy within 12 m: +3% move speed, and the battle charge recharges 3% faster, up to +30%."),
        Minor("biggame", "Big Game", 5, 810, H, 5, new[] { "pursuit" }, "+{Damage to elites and bosses}% damage to elites and bosses.", (EliteDamage, 12)),

        // Tier 6 (level 21)
        Minor("axemaster", "Axe Master", 6, 110, T, 3, new[] { "whirling", ReturningEdge }, "+{Throw damage}% throw damage and +{Crit damage}% critical damage.", (ThrowDamage, 12), (CritDamage, 12)),
        Minor("armful", "Armful of Axes", 6, 250, T, 2, new[] { ReturningEdge }, "Every throw sends {Axes} more axes, and the axes fly {Throw range}% further.", (Axes, 1), (ThrowRange, 8)),
        Minor("exsanguinate", "Bleed Them Dry", 6, 400, B, 3, new[] { Bloodbath, "sanguine" }, "+{Bleed damage}% bleed damage.", (BleedDamage, 20)),
        Minor("drinker", "Blood Drinker", 6, 530, B, 3, new[] { "sanguine" }, "Heal {Life leech}% of the damage your axes deal.", (LifeLeech, 0.3f)),
        Minor("predator", "Predator", 6, 670, H, 3, new[] { BloodScent }, "+{Move speed}% move speed and +{Throw damage}% throw damage.", (MoveSpeed, 5), (ThrowDamage, 10)),
        Minor("tireless", "Tireless", 6, 810, H, 3, new[] { BloodScent, "biggame" }, "+{Max health} max health and +{Health per second} health per second.", (MaxHealth, 15), (Regeneration, 0.4f)),

        // Tier 7 (level 28): the capstones.
        Major(AxeStorm, "Axe Storm", 7, 180, T, new[] { "axemaster", "armful" },
            "Every 12 s both axes circle you for 4 s, 3 m out, hitting everything they pass twice a second for 150% of a throw's damage (no throws meanwhile)."),
        Major(CrimsonTide, "Crimson Tide", 7, 465, B, new[] { "exsanguinate", "drinker" }, "Bleeding enemies take 20% more damage from your axes."),
        Major(Harvester, "Harvester", 7, 740, H, new[] { "predator", "tireless" }, "Every 5th kill throws a free axe at the nearest enemy."),
    }, new[] { 1, 3, 6, 10, 15, 21, 28 },
        new[] { (T, 200f), (B, 475f), (H, 745f) },
        new HashSet<string> { MaxHealth, Regeneration, BleedDuration, Axes, Chains });
}

/// <summary>What the ranks spent in the Reaver tree add up to, in the terms <see cref="WarriorStats"/> uses (fractions for percentages).</summary>
internal sealed class ReaverBonuses
{
    public float ThrowDamage;
    public float ThrowRange;
    public float AxeSpeed;
    public float AttackSpeed;
    public float CritChance;
    public float CritDamage;
    public float BleedDamage;

    /// <summary>Seconds longer that bleeding lasts.</summary>
    public float BleedDuration;

    /// <summary>More axes a throw, and more bounces an axe.</summary>
    public int Axes;
    public int Chains;

    public float EliteDamage;
    public float LifeLeech;
    public float MoveSpeed;
    public float ChargeRecharge;
    public float MaxHealth;
    public float Regeneration;

    /// <summary>What damage taken is multiplied by: each rank of a cut takes a share off what is left.</summary>
    public float DamageTaken = 1f;

    public bool Catch;
    public bool Hemorrhage;
    public bool RunThemDown;
    public bool RicochetAxes;
    public bool DeepWounds;
    public bool ClosingIn;
    public bool ReturningEdge;
    public bool Bloodbath;
    public bool BloodScent;
    public bool AxeStorm;
    public bool CrimsonTide;
    public bool Harvester;

    /// <summary>Whether anything makes enemies bleed (Hemorrhage's hits or Run Them Down's charge).</summary>
    public bool Bleeds => Hemorrhage || RunThemDown;

    public static ReaverBonuses From(IReadOnlyDictionary<string, int> ranks)
    {
        var b = new ReaverBonuses();
        foreach (var node in ReaverTree.Tree.Nodes)
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
                    case ReaverTree.ThrowDamage: b.ThrowDamage += v / 100f; break;
                    case ReaverTree.ThrowRange: b.ThrowRange += v / 100f; break;
                    case ReaverTree.AxeSpeed: b.AxeSpeed += v / 100f; break;
                    case ReaverTree.AttackSpeed: b.AttackSpeed += v / 100f; break;
                    case ReaverTree.CritChance: b.CritChance += v / 100f; break;
                    case ReaverTree.CritDamage: b.CritDamage += v / 100f; break;
                    case ReaverTree.BleedDamage: b.BleedDamage += v / 100f; break;
                    case ReaverTree.BleedDuration: b.BleedDuration += v; break;
                    case ReaverTree.EliteDamage: b.EliteDamage += v / 100f; break;
                    case ReaverTree.LifeLeech: b.LifeLeech += v / 100f; break;
                    case ReaverTree.MoveSpeed: b.MoveSpeed += v / 100f; break;
                    case ReaverTree.ChargeRecharge: b.ChargeRecharge += v / 100f; break;
                    case ReaverTree.MaxHealth: b.MaxHealth += v; break;
                    case ReaverTree.Regeneration: b.Regeneration += v; break;
                    case ReaverTree.Axes: b.Axes += (int)v; break;
                    case ReaverTree.Chains: b.Chains += (int)v; break;
                    case ReaverTree.DamageTakenCut: b.DamageTaken *= MathF.Pow(1f - perRank / 100f, r); break;
                }
            }
        }

        bool Has(string id) => ranks.GetValueOrDefault(id) > 0;
        b.Catch = Has(ReaverTree.Catch);
        b.Hemorrhage = Has(ReaverTree.Hemorrhage);
        b.RunThemDown = Has(ReaverTree.RunThemDown);
        b.RicochetAxes = Has(ReaverTree.RicochetAxes);
        b.DeepWounds = Has(ReaverTree.DeepWounds);
        b.ClosingIn = Has(ReaverTree.ClosingIn);
        b.ReturningEdge = Has(ReaverTree.ReturningEdge);
        b.Bloodbath = Has(ReaverTree.Bloodbath);
        b.BloodScent = Has(ReaverTree.BloodScent);
        b.AxeStorm = Has(ReaverTree.AxeStorm);
        b.CrimsonTide = Has(ReaverTree.CrimsonTide);
        b.Harvester = Has(ReaverTree.Harvester);
        return b;
    }
}
