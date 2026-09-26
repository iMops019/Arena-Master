using ArenaMaster.Game.Progression;

namespace ArenaMaster.Game.Warrior;

/// <summary>
/// The Warrior's first passive tree: fury and the swing. Four lanes - Iron Hide (health, and blocking with the two axes), Rage (Berserking, the tree's heart, and
/// everything that feeds and spends rage), Bloodlust (damage, attack speed, crits, the kill) and Carnage (the cleave's size: its reach and its arc) - over seven tiers
/// opening at tree levels 1, 3, 6, 10, 15, 21 and 28. Two starting nodes; thirteen majors, one capstone per lane.
/// </summary>
internal static class BerserkerTree
{
    public const string ClassId = "warrior";
    public const string TreeId = "berserker";

    // Stat names, used in node descriptions as {Name} and summed by BerserkerBonuses.
    public const string CleaveDamage = "Cleave damage";
    public const string AttackSpeed = "Attack speed";
    public const string CritChance = "Crit chance";
    public const string CritDamage = "Crit damage";
    public const string EliteDamage = "Damage to elites and bosses";
    public const string LifeLeech = "Life leech";
    public const string CleaveSize = "Cleave size";
    public const string CleaveReach = "Cleave reach";
    public const string CleaveArc = "Cleave arc";
    public const string MaxRage = "Max rage";
    public const string RageDuration = "Rage duration";
    public const string RageEffect = "Rage effect";
    public const string MaxHealth = "Max health";
    public const string Regeneration = "Health per second";
    public const string BlockChance = "Block chance";
    public const string BlockHeal = "Heal on block";
    public const string DamageTakenCut = "Damage taken cut";

    // The majors, by id.
    public const string Berserking = "berserking";
    public const string FuelledByPain = "pain";
    public const string GreatSweep = "greatsweep";
    public const string Riposte = "riposte";
    public const string Execute = "execute";
    public const string BloodRage = "bloodrage";
    public const string EchoingCleave = "echo";
    public const string LastStand = "laststand";
    public const string BloodFrenzy = "frenzy";
    public const string BladeWall = "bladewall";
    public const string AvatarOfRage = "avatar";
    public const string TwinFury = "twinfury";
    public const string Whirlwind = "whirlwind";

    private static TreeNode Minor(string id, string name, int tier, float x, string lane, int max, string[] parents, string text, params (string Stat, float PerRank)[] stats) =>
        new(id, name, tier, x, lane, max, text, parents, stats.ToDictionary(s => s.Stat, s => s.PerRank));

    private static TreeNode Major(string id, string name, int tier, float x, string lane, string[] parents, string text) =>
        new(id, name, tier, x, lane, 1, text, parents, new Dictionary<string, float>(), Major: true);

    private const string I = "Iron Hide", R = "Rage", B = "Bloodlust", C = "Carnage";

    public static readonly TreeDefinition Tree = new(TreeId, "Berserker", new[]
    {
        // Tier 1 (level 1): the two starting choices.
        Minor("thickskin", "Thick Skin", 1, 200, I, 5, Array.Empty<string>(), "+{Max health} max health and +{Block chance}% block chance.", (MaxHealth, 8), (BlockChance, 1.5f)),
        Minor("mastery", "Axe Mastery", 1, 650, B, 5, Array.Empty<string>(), "+{Cleave damage}% Cleave damage and +{Attack speed}% attack speed.", (CleaveDamage, 10), (AttackSpeed, 5)),

        // Tier 2 (level 3)
        Minor("ironwill", "Iron Will", 2, 100, I, 5, new[] { "thickskin" }, "+{Max health} max health.", (MaxHealth, 15)),
        Minor("crossed", "Crossed Axes", 2, 225, I, 5, new[] { "thickskin" }, "Parry with both axes: +{Block chance}% block chance.", (BlockChance, 3)),
        Major(Berserking, "Berserking", 2, 380, R, new[] { "thickskin", "mastery" },
            "Hits grant rage: +1 for every enemy a Cleave hits, up to 20. Each rage gives +1% attack damage and attack speed. Rage lasts 5 s; keep hitting to keep it up."),
        Minor("sharpened", "Sharpened Axes", 2, 535, B, 5, new[] { "mastery" }, "+{Cleave damage}% Cleave damage.", (CleaveDamage, 12)),
        Minor("frenzied", "Frenzied Swings", 2, 650, B, 5, new[] { "mastery" }, "+{Attack speed}% attack speed.", (AttackSpeed, 8)),
        Minor("broad", "Broad Swings", 2, 775, C, 5, new[] { "mastery" }, "+{Cleave size}% Cleave size: it reaches further and sweeps wider.", (CleaveSize, 8)),
        Minor("widearc", "Wide Arc", 2, 890, C, 3, new[] { "mastery" }, "The Cleave sweeps {Cleave arc} degrees wider.", (CleaveArc, 15)),

        // Tier 3 (level 6)
        Minor("hardened", "Hardened", 3, 100, I, 3, new[] { "ironwill" }, "+{Health per second} health per second.", (Regeneration, 0.4f)),
        Minor("parry", "Parry", 3, 225, I, 3, new[] { "crossed" }, "+{Block chance}% block chance, and a block heals {Heal on block} health.", (BlockChance, 3), (BlockHeal, 1.5f)),
        Minor("hotblood", "Hot Blood", 3, 330, R, 3, new[] { Berserking }, "+{Max rage} max rage.", (MaxRage, 3)),
        Major(FuelledByPain, "Fuelled by Pain", 3, 440, R, new[] { Berserking }, "Every blow that reaches you, landed or blocked, grants 4 rage."),
        Minor("butcher", "Butcher", 3, 590, B, 5, new[] { "sharpened", "frenzied" }, "+{Crit chance}% increased critical chance and +{Crit damage}% critical damage.", (CritChance, 10), (CritDamage, 10)),
        Major(GreatSweep, "Great Sweep", 3, 730, C, new[] { "broad", "widearc" }, "The Cleave sweeps 60 degrees wider."),
        Minor("longreach", "Long Reach", 3, 865, C, 3, new[] { "broad", "widearc" }, "+{Cleave reach}% Cleave reach.", (CleaveReach, 10)),

        // Tier 4 (level 10)
        Minor("scarred", "Scarred", 4, 100, I, 3, new[] { "hardened" }, "+{Max health} max health and {Damage taken cut}% less damage taken.", (MaxHealth, 10), (DamageTakenCut, 4)),
        Major(Riposte, "Riposte", 4, 225, I, new[] { "parry" }, "Every blow you block is answered: the attacker takes 300% of a Cleave hit."),
        Minor("simmer", "Simmering", 4, 340, R, 3, new[] { "hotblood", FuelledByPain }, "Rage lasts {Rage duration} s longer.", (RageDuration, 1)),
        Minor("seething", "Seething", 4, 450, R, 3, new[] { "hotblood", FuelledByPain }, "Each rage counts {Rage effect}% more.", (RageEffect, 25)),
        Major(Execute, "Execute", 4, 560, B, new[] { "butcher" }, "A Cleave hit on an enemy below 20% health kills it outright (not bosses)."),
        Minor("bloodthirst", "Bloodthirst", 4, 670, B, 3, new[] { "butcher" }, "Heal {Life leech}% of the Cleave damage you deal.", (LifeLeech, 0.3f)),
        Minor("massive", "Massive Swings", 4, 800, C, 5, new[] { GreatSweep, "longreach" }, "+{Cleave size}% Cleave size.", (CleaveSize, 10)),

        // Tier 5 (level 15)
        Minor("ironhide", "Iron Hide", 5, 110, I, 3, new[] { "scarred", Riposte }, "+{Max health} max health and +{Health per second} health per second.", (MaxHealth, 20), (Regeneration, 0.3f)),
        Minor("bladeguard", "Blade Guard", 5, 235, I, 3, new[] { Riposte }, "+{Block chance}% block chance, and a block heals {Heal on block} health.", (BlockChance, 3), (BlockHeal, 1)),
        Major(BloodRage, "Blood Rage", 5, 395, R, new[] { "simmer", "seething" }, "Each rage also cuts the damage you take by 0.5%."),
        Minor("slayer", "Giant Slayer", 5, 545, B, 5, new[] { Execute }, "+{Damage to elites and bosses}% damage to elites and bosses.", (EliteDamage, 15)),
        Minor("ferocity", "Ferocity", 5, 660, B, 3, new[] { Execute, "bloodthirst" }, "+{Cleave damage}% Cleave damage and +{Attack speed}% attack speed.", (CleaveDamage, 10), (AttackSpeed, 10)),
        Major(EchoingCleave, "Echoing Cleave", 5, 775, C, new[] { "massive" }, "Every Cleave is followed 0.2 s later by a second wave, at 50% damage."),
        Minor("vastarc", "Vast Arc", 5, 885, C, 3, new[] { "massive" }, "The Cleave sweeps {Cleave arc} degrees wider and reaches {Cleave reach}% further.", (CleaveArc, 20), (CleaveReach, 8)),

        // Tier 6 (level 21)
        Major(LastStand, "Last Stand", 6, 125, I, new[] { "ironhide", "bladeguard" }, "Once per run, a blow that would kill you leaves you on 1 health instead, and heals you for 40% of your max health."),
        Minor("tenacity", "Tenacity", 6, 250, I, 3, new[] { "bladeguard" }, "+{Health per second} health per second and {Damage taken cut}% less damage taken.", (Regeneration, 0.5f), (DamageTakenCut, 3)),
        Minor("wrath", "Unbridled Wrath", 6, 395, R, 3, new[] { BloodRage }, "+{Max rage} max rage, and each rage counts {Rage effect}% more.", (MaxRage, 3), (RageEffect, 10)),
        Major(BloodFrenzy, "Blood Frenzy", 6, 560, B, new[] { "slayer", "ferocity" }, "Every kill gives +1% attack speed for 4 s, up to +30%."),
        Minor("reaver", "Reaver", 6, 675, B, 3, new[] { "ferocity" }, "+{Cleave damage}% Cleave damage and +{Crit damage}% critical damage.", (CleaveDamage, 15), (CritDamage, 15)),
        Minor("titan", "Titan's Reach", 6, 830, C, 3, new[] { EchoingCleave, "vastarc" }, "+{Cleave size}% Cleave size.", (CleaveSize, 12)),

        // Tier 7 (level 28): the capstones.
        Major(BladeWall, "Blade Wall", 7, 185, I, new[] { LastStand, "tenacity" }, "+10% block chance. Every block grants 5 rage and heals 2% of your max health."),
        Major(AvatarOfRage, "Avatar of Rage", 7, 395, R, new[] { "wrath" }, "Max rage is doubled, and each rage also makes the Cleave 1% bigger."),
        Major(TwinFury, "Twin Fury", 7, 615, B, new[] { BloodFrenzy, "reaver" }, "Both axes swing at once: every Cleave sends a second wave, angled the other way, at 70% damage."),
        Major(Whirlwind, "Whirlwind", 7, 830, C, new[] { "titan" }, "Every 4th swing is a Whirlwind: a Cleave all the way round you, reaching 50% further."),
    }, new[] { 1, 3, 6, 10, 15, 21, 28 },
        new[] { (I, 165f), (R, 395f), (B, 620f), (C, 830f) },
        new HashSet<string> { MaxHealth, Regeneration, BlockHeal, MaxRage, RageDuration, CleaveArc });
}

/// <summary>What the ranks spent in the Berserker tree add up to, in the terms <see cref="WarriorStats"/> uses (fractions for percentages).</summary>
internal sealed class BerserkerBonuses
{
    public float CleaveDamage;
    public float AttackSpeed;
    public float CritChance;
    public float CritDamage;
    public float EliteDamage;
    public float LifeLeech;
    public float CleaveSize;
    public float CleaveReach;

    /// <summary>Degrees added to the Cleave's sweep.</summary>
    public float CleaveArc;

    public float MaxRage;
    public float RageDuration;
    public float RageEffect;
    public float MaxHealth;
    public float Regeneration;
    public float BlockChance;
    public float BlockHeal;

    /// <summary>What damage taken is multiplied by: each rank of a cut takes a share off what is left.</summary>
    public float DamageTaken = 1f;

    public bool Berserking;
    public bool FuelledByPain;
    public bool GreatSweep;
    public bool Riposte;
    public bool Execute;
    public bool BloodRage;
    public bool EchoingCleave;
    public bool LastStand;
    public bool BloodFrenzy;
    public bool BladeWall;
    public bool AvatarOfRage;
    public bool TwinFury;
    public bool Whirlwind;

    public static BerserkerBonuses From(IReadOnlyDictionary<string, int> ranks)
    {
        var b = new BerserkerBonuses();
        foreach (var node in BerserkerTree.Tree.Nodes)
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
                    case BerserkerTree.CleaveDamage: b.CleaveDamage += v / 100f; break;
                    case BerserkerTree.AttackSpeed: b.AttackSpeed += v / 100f; break;
                    case BerserkerTree.CritChance: b.CritChance += v / 100f; break;
                    case BerserkerTree.CritDamage: b.CritDamage += v / 100f; break;
                    case BerserkerTree.EliteDamage: b.EliteDamage += v / 100f; break;
                    case BerserkerTree.LifeLeech: b.LifeLeech += v / 100f; break;
                    case BerserkerTree.CleaveSize: b.CleaveSize += v / 100f; break;
                    case BerserkerTree.CleaveReach: b.CleaveReach += v / 100f; break;
                    case BerserkerTree.CleaveArc: b.CleaveArc += v; break;
                    case BerserkerTree.MaxRage: b.MaxRage += v; break;
                    case BerserkerTree.RageDuration: b.RageDuration += v; break;
                    case BerserkerTree.RageEffect: b.RageEffect += v / 100f; break;
                    case BerserkerTree.MaxHealth: b.MaxHealth += v; break;
                    case BerserkerTree.Regeneration: b.Regeneration += v; break;
                    case BerserkerTree.BlockChance: b.BlockChance += v / 100f; break;
                    case BerserkerTree.BlockHeal: b.BlockHeal += v; break;
                    case BerserkerTree.DamageTakenCut: b.DamageTaken *= MathF.Pow(1f - perRank / 100f, r); break;
                }
            }
        }

        bool Has(string id) => ranks.GetValueOrDefault(id) > 0;
        b.Berserking = Has(BerserkerTree.Berserking);
        b.FuelledByPain = Has(BerserkerTree.FuelledByPain);
        b.GreatSweep = Has(BerserkerTree.GreatSweep);
        b.Riposte = Has(BerserkerTree.Riposte);
        b.Execute = Has(BerserkerTree.Execute);
        b.BloodRage = Has(BerserkerTree.BloodRage);
        b.EchoingCleave = Has(BerserkerTree.EchoingCleave);
        b.LastStand = Has(BerserkerTree.LastStand);
        b.BloodFrenzy = Has(BerserkerTree.BloodFrenzy);
        b.BladeWall = Has(BerserkerTree.BladeWall);
        b.AvatarOfRage = Has(BerserkerTree.AvatarOfRage);
        b.TwinFury = Has(BerserkerTree.TwinFury);
        b.Whirlwind = Has(BerserkerTree.Whirlwind);
        return b;
    }
}
