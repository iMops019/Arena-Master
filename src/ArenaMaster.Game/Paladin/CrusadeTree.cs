using ArenaMaster.Game.Progression;

namespace ArenaMaster.Game.Paladin;

/// <summary>
/// The Paladin's second passive tree: the crusader who keeps moving forward and trades health for damage. Three lanes - Zeal (moving: Zeal built by walking,
/// the shield rush as a weapon, holy ground left behind), Judgement (hammers of light from the sky on the toughest enemies) and Sacrifice (paying health for
/// damage, and what saves you when it runs low) - over seven tiers opening at tree levels 1, 3, 6, 10, 15, 21 and 28. Two starting nodes; twelve majors, one
/// capstone per lane. The Holy Nova and its circles are the same whichever tree is active.
/// </summary>
internal static class CrusadeTree
{
    public const string TreeId = "crusade";

    // Stat names, used in node descriptions as {Name} and summed by CrusadeBonuses.
    public const string MoveSpeed = "Move speed";
    public const string NovaDamage = "Nova damage";
    public const string NovaFrequency = "Nova frequency";
    public const string CritChance = "Crit chance";
    public const string CritDamage = "Crit damage";
    public const string RushRecharge = "Shield rush recharge";
    public const string RushDamage = "Rush damage";
    public const string ZealGain = "Zeal gain";
    public const string ZealKept = "Zeal kept";
    public const string HammerDamage = "Hammer damage";
    public const string HammerRadius = "Hammer size";
    public const string EliteDamage = "Damage to elites and bosses";
    public const string LowHealthDamage = "Nova damage below half health";
    public const string MaxHealth = "Max health";
    public const string Regeneration = "Health per second";
    public const string HealOnKill = "Heal on kill";
    public const string DamageTakenCut = "Damage taken cut";

    // The majors, by id.
    public const string Zeal = "zeal";
    public const string CrusadersRush = "rush";
    public const string BlessedTrail = "trail";
    public const string EndlessCrusade = "endless";
    public const string HammerOfJudgement = "hammer";
    public const string Condemn = "condemn";
    public const string HallowedImpact = "hallowed";
    public const string FinalJudgement = "final";
    public const string BloodOath = "oath";
    public const string Martyrdom = "martyrdom";
    public const string Penance = "penance";
    public const string AvengingWings = "wings";

    private static TreeNode Minor(string id, string name, int tier, float x, string lane, int max, string[] parents, string text, params (string Stat, float PerRank)[] stats) =>
        new(id, name, tier, x, lane, max, text, parents, stats.ToDictionary(s => s.Stat, s => s.PerRank));

    private static TreeNode Major(string id, string name, int tier, float x, string lane, string[] parents, string text) =>
        new(id, name, tier, x, lane, 1, text, parents, new Dictionary<string, float>(), Major: true);

    private const string Z = "Zeal", J = "Judgement", S = "Sacrifice";

    public static readonly TreeDefinition Tree = new(TreeId, "Crusade", new[]
    {
        // Tier 1 (level 1): the two starting choices.
        Minor("march", "Forward March", 1, 230, Z, 5, Array.Empty<string>(), "+{Move speed}% move speed and +{Nova damage}% Holy Nova damage.", (MoveSpeed, 2), (NovaDamage, 8)),
        Minor("righteous", "Righteous Strike", 1, 620, J, 5, Array.Empty<string>(), "+{Nova damage}% Holy Nova damage and +{Crit chance}% increased critical chance.", (NovaDamage, 8), (CritChance, 8)),

        // Tier 2 (level 3)
        Minor("road", "Long Road", 2, 80, Z, 5, new[] { "march" }, "+{Move speed}% move speed.", (MoveSpeed, 3)),
        Major(Zeal, "Zeal", 2, 210, Z, new[] { "march" }, "Every second you keep moving builds 1 Zeal, up to 20. Each Zeal adds 2% Holy Nova damage. Standing still drains it, 4 a second."),
        Minor("ardour", "Ardour", 2, 340, Z, 5, new[] { "march" }, "+{Nova frequency}% nova frequency.", (NovaFrequency, 6)),
        Minor("verdict", "Stern Verdict", 2, 480, J, 5, new[] { "righteous" }, "+{Nova damage}% Holy Nova damage.", (NovaDamage, 10)),
        Minor("keen", "Keen Eye", 2, 600, J, 3, new[] { "righteous" }, "+{Crit chance}% increased critical chance and +{Crit damage}% critical damage.", (CritChance, 10), (CritDamage, 10)),
        Major(BloodOath, "Blood Oath", 2, 740, S, new[] { "righteous" }, "Each Holy Nova costs 1% of your max health (never taking you below 1) and deals 40% more damage. Every kill heals you 1 health."),
        Minor("scarred", "Scarred Flesh", 2, 880, S, 5, new[] { "righteous" }, "+{Max health} max health.", (MaxHealth, 10)),

        // Tier 3 (level 6)
        Minor("charge", "Relentless Charge", 3, 90, Z, 3, new[] { "road" }, "The shield rush recharges {Shield rush recharge}% faster.", (RushRecharge, 15)),
        Minor("kindling", "Kindled Zeal", 3, 210, Z, 3, new[] { Zeal }, "Zeal builds {Zeal gain}% faster.", (ZealGain, 20)),
        Minor("unwavering", "Unwavering", 3, 330, Z, 3, new[] { Zeal }, "Zeal drains {Zeal kept}% slower while you stand still.", (ZealKept, 20)),
        Major(HammerOfJudgement, "Hammer of Judgement", 3, 460, J, new[] { "ardour", "verdict" },
            "Every 5th Holy Nova drops a hammer of light on the toughest enemy within 15 m (bosses first, then elites, then the most health): 400% of the nova's damage within 2 m."),
        Minor("sentence", "Harsh Sentence", 3, 590, J, 5, new[] { "keen" }, "+{Damage to elites and bosses}% damage to elites and bosses.", (EliteDamage, 12)),
        Minor("price", "Blood Price", 3, 720, S, 3, new[] { BloodOath }, "+{Nova damage below half health}% Holy Nova damage while you are below half health.", (LowHealthDamage, 15)),
        Minor("grit", "Grim Resolve", 3, 860, S, 3, new[] { "scarred" }, "+{Health per second} health per second.", (Regeneration, 0.4f)),

        // Tier 4 (level 10)
        Major(CrusadersRush, "Crusader's Rush", 4, 110, Z, new[] { "charge", "kindling" },
            "Your shield rush hits every enemy it passes for a Holy Nova's damage, and a holy circle forms where the rush ends."),
        Minor("fervent", "Fervent March", 4, 250, Z, 3, new[] { "kindling", "unwavering" }, "+{Move speed}% move speed and +{Nova frequency}% nova frequency.", (MoveSpeed, 3), (NovaFrequency, 4)),
        Minor("heavy", "Heavy Hammers", 4, 380, J, 5, new[] { HammerOfJudgement }, "+{Hammer damage}% hammer damage.", (HammerDamage, 25)),
        Major(Condemn, "Condemn", 4, 490, J, new[] { HammerOfJudgement }, "Enemies a hammer strikes are condemned for 5 s: they take 30% more damage from you."),
        Minor("swift", "Swift Justice", 4, 600, J, 3, new[] { "sentence" }, "+{Crit chance}% increased critical chance and +{Crit damage}% critical damage.", (CritChance, 10), (CritDamage, 15)),
        Major(Martyrdom, "Martyrdom", 4, 710, S, new[] { "price" }, "The less health you have, the harder your novas hit: up to 60% more damage at 10% health or less (none at full health)."),
        Minor("scars", "Old Scars", 4, 840, S, 3, new[] { "price", "grit" }, "+{Max health} max health and {Damage taken cut}% less damage taken.", (MaxHealth, 8), (DamageTakenCut, 3)),

        // Tier 5 (level 15)
        Major(BlessedTrail, "Blessed Trail", 5, 90, Z, new[] { CrusadersRush }, "While you keep moving, you leave holy ground behind you: a smaller holy circle every 3 m you walk."),
        Minor("battering", "Battering Rush", 5, 210, Z, 3, new[] { CrusadersRush, "fervent" }, "+{Rush damage}% shield rush damage.", (RushDamage, 30)),
        Minor("zealfire", "Zealous Fire", 5, 330, Z, 3, new[] { "fervent" }, "+{Nova damage}% Holy Nova damage, and Zeal builds {Zeal gain}% faster.", (NovaDamage, 8), (ZealGain, 10)),
        Minor("broad", "Broad Hammers", 5, 450, J, 3, new[] { "heavy" }, "+{Hammer size}% hammer size.", (HammerRadius, 15)),
        Major(HallowedImpact, "Hallowed Impact", 5, 570, J, new[] { "heavy", Condemn }, "A holy circle forms where each hammer lands."),
        Major(Penance, "Penance", 5, 690, S, new[] { Martyrdom }, "The health that blows take off you is paid back: your next Holy Nova deals twice as much extra damage to every enemy it hits."),
        Minor("lifeblood", "Lifeblood", 5, 820, S, 3, new[] { Martyrdom, "scars" }, "Every kill heals you {Heal on kill} health.", (HealOnKill, 0.5f)),

        // Tier 6 (level 21)
        Minor("tireless", "Tireless", 6, 120, Z, 3, new[] { BlessedTrail, "battering" }, "+{Move speed}% move speed, and the shield rush recharges {Shield rush recharge}% faster.", (MoveSpeed, 4), (RushRecharge, 10)),
        Minor("fanatic", "Fanatic", 6, 300, Z, 3, new[] { "zealfire" }, "+{Nova damage}% Holy Nova damage and +{Nova frequency}% nova frequency.", (NovaDamage, 12), (NovaFrequency, 5)),
        Minor("hand", "Hand of Judgement", 6, 440, J, 3, new[] { "broad", HallowedImpact }, "+{Hammer damage}% hammer damage and +{Damage to elites and bosses}% damage to elites and bosses.", (HammerDamage, 20), (EliteDamage, 10)),
        Minor("exalted", "Exalted Verdict", 6, 570, J, 3, new[] { HallowedImpact, "swift" }, "+{Nova damage}% Holy Nova damage and +{Damage to elites and bosses}% damage to elites and bosses.", (NovaDamage, 10), (EliteDamage, 10)),
        Minor("fury", "Blood Fury", 6, 700, S, 3, new[] { Penance }, "+{Nova damage below half health}% Holy Nova damage while you are below half health.", (LowHealthDamage, 20)),
        Minor("will", "Iron Will", 6, 850, S, 3, new[] { "lifeblood" }, "+{Max health} max health and +{Health per second} health per second.", (MaxHealth, 12), (Regeneration, 0.4f)),

        // Tier 7 (level 28): the capstones.
        Major(EndlessCrusade, "Endless Crusade", 7, 210, Z, new[] { "tireless", "fanatic" },
            "Zeal builds as you keep moving, up to 20. At full Zeal, every Holy Nova bursts again 0.35 s later at 60% damage."),
        Major(FinalJudgement, "Final Judgement", 7, 500, J, new[] { "hand", "exalted" },
            "Every 5th Holy Nova drops hammers of light on every elite and boss within 25 m, as well as on the toughest enemy near you."),
        Major(AvengingWings, "Avenging Wings", 7, 780, S, new[] { "fury", "will" },
            "When you drop below 30% health, you take flight for 6 s: novas come 50% more often and you take no damage. Once every 60 s."),
    }, new[] { 1, 3, 6, 10, 15, 21, 28 },
        new[] { (Z, 210f), (J, 510f), (S, 780f) },
        new HashSet<string> { MaxHealth, Regeneration, HealOnKill });
}

/// <summary>What the ranks spent in the Crusade tree add up to, in the terms <see cref="PaladinStats"/> uses (fractions for percentages).</summary>
internal sealed class CrusadeBonuses
{
    public float MoveSpeed;
    public float NovaDamage;
    public float NovaFrequency;
    public float CritChance;
    public float CritDamage;
    public float RushRecharge;
    public float RushDamage;
    public float ZealGain;
    public float ZealKept;
    public float HammerDamage;
    public float HammerRadius;
    public float EliteDamage;
    public float LowHealthDamage;
    public float MaxHealth;
    public float Regeneration;
    public float HealOnKill;

    /// <summary>What damage taken is multiplied by: each rank of Old Scars takes a share off what is left.</summary>
    public float DamageTaken = 1f;

    public bool Zeal;
    public bool CrusadersRush;
    public bool BlessedTrail;
    public bool EndlessCrusade;
    public bool HammerOfJudgement;
    public bool Condemn;
    public bool HallowedImpact;
    public bool FinalJudgement;
    public bool BloodOath;
    public bool Martyrdom;
    public bool Penance;
    public bool AvengingWings;

    public static CrusadeBonuses From(IReadOnlyDictionary<string, int> ranks)
    {
        var b = new CrusadeBonuses();
        foreach (var node in CrusadeTree.Tree.Nodes)
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
                    case CrusadeTree.MoveSpeed: b.MoveSpeed += v / 100f; break;
                    case CrusadeTree.NovaDamage: b.NovaDamage += v / 100f; break;
                    case CrusadeTree.NovaFrequency: b.NovaFrequency += v / 100f; break;
                    case CrusadeTree.CritChance: b.CritChance += v / 100f; break;
                    case CrusadeTree.CritDamage: b.CritDamage += v / 100f; break;
                    case CrusadeTree.RushRecharge: b.RushRecharge += v / 100f; break;
                    case CrusadeTree.RushDamage: b.RushDamage += v / 100f; break;
                    case CrusadeTree.ZealGain: b.ZealGain += v / 100f; break;
                    case CrusadeTree.ZealKept: b.ZealKept += v / 100f; break;
                    case CrusadeTree.HammerDamage: b.HammerDamage += v / 100f; break;
                    case CrusadeTree.HammerRadius: b.HammerRadius += v / 100f; break;
                    case CrusadeTree.EliteDamage: b.EliteDamage += v / 100f; break;
                    case CrusadeTree.LowHealthDamage: b.LowHealthDamage += v / 100f; break;
                    case CrusadeTree.MaxHealth: b.MaxHealth += v; break;
                    case CrusadeTree.Regeneration: b.Regeneration += v; break;
                    case CrusadeTree.HealOnKill: b.HealOnKill += v; break;
                    case CrusadeTree.DamageTakenCut: b.DamageTaken *= MathF.Pow(1f - perRank / 100f, r); break;
                }
            }
        }

        bool Has(string id) => ranks.GetValueOrDefault(id) > 0;
        b.Zeal = Has(CrusadeTree.Zeal);
        b.CrusadersRush = Has(CrusadeTree.CrusadersRush);
        b.BlessedTrail = Has(CrusadeTree.BlessedTrail);
        b.EndlessCrusade = Has(CrusadeTree.EndlessCrusade);
        b.HammerOfJudgement = Has(CrusadeTree.HammerOfJudgement);
        b.Condemn = Has(CrusadeTree.Condemn);
        b.HallowedImpact = Has(CrusadeTree.HallowedImpact);
        b.FinalJudgement = Has(CrusadeTree.FinalJudgement);
        b.BloodOath = Has(CrusadeTree.BloodOath);
        b.Martyrdom = Has(CrusadeTree.Martyrdom);
        b.Penance = Has(CrusadeTree.Penance);
        b.AvengingWings = Has(CrusadeTree.AvengingWings);
        return b;
    }
}
