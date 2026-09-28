using ArenaMaster.Game.Progression;

namespace ArenaMaster.Game.Priest;

/// <summary>
/// The Priest's first passive tree: death and decay. Four lanes - Grave Ward (health, and blocking with the Skull Shield: Death and Decay grows from it), Plague
/// (the damage over time the skulls leave: how hard, how long, how many stacks, how it spreads), Skulls (the skull itself: damage, cast speed, speed, how long it
/// lives, how many it pierces) and Rot (the rot left on the ground: its damage, how long it lasts, how wide, and Rotting Step) - over seven tiers opening at tree
/// levels 1, 3, 6, 10, 15, 21 and 28. Two starting nodes; fourteen majors, a capstone per lane.
/// </summary>
internal static class UnholyTree
{
    public const string ClassId = "priest";
    public const string TreeId = "unholy";

    // Stat names, used in node descriptions as {Name} and summed by UnholyBonuses.
    public const string SkullDamage = "Skull damage";
    public const string PlagueDamage = "Plague damage";
    public const string DecayDamage = "Rot damage";
    public const string CastSpeed = "Cast speed";
    public const string ProjectileSpeed = "Skull speed";
    public const string SkullLife = "Skull duration";
    public const string Pierce = "Pierce";
    public const string SeekRange = "Seek range";
    public const string PlagueDuration = "Plague duration";
    public const string PlagueStacks = "Plague stacks";
    public const string CritChance = "Crit chance";
    public const string CritDamage = "Crit damage";
    public const string EliteDamage = "Damage to elites and bosses";
    public const string DecayDuration = "Rot duration";
    public const string DecayArea = "Rot area";
    public const string StepRecharge = "Rotting Step recharge";
    public const string DecayChance = "Death and Decay chance";
    public const string MaxHealth = "Max health";
    public const string Regeneration = "Health per second";
    public const string BlockChance = "Block chance";
    public const string BlockHeal = "Heal on block";
    public const string DamageTakenCut = "Damage taken cut";

    // The majors, by id.
    public const string VirulentStrain = "virulent";
    public const string DeathAndDecay = "deathanddecay";
    public const string TwinSkulls = "twinskulls";
    public const string Pestilence = "pestilence";
    public const string GraveSoil = "gravesoil";
    public const string BoneArmour = "bonearmour";
    public const string GnashingSkulls = "gnashing";
    public const string Epidemic = "epidemic";
    public const string SoulHarvest = "soulharvest";
    public const string AuraOfDecay = "aura";
    public const string MouthOfTheGrave = "mouthofthegrave";
    public const string BlackDeath = "blackdeath";
    public const string Legion = "legion";
    public const string Necropolis = "necropolis";

    private static TreeNode Minor(string id, string name, int tier, float x, string lane, int max, string[] parents, string text, params (string Stat, float PerRank)[] stats) =>
        new(id, name, tier, x, lane, max, text, parents, stats.ToDictionary(s => s.Stat, s => s.PerRank));

    private static TreeNode Major(string id, string name, int tier, float x, string lane, string[] parents, string text) =>
        new(id, name, tier, x, lane, 1, text, parents, new Dictionary<string, float>(), Major: true);

    private const string G = "Grave Ward", P = "Plague", S = "Skulls", R = "Rot";

    public static readonly TreeDefinition Tree = new(TreeId, "Unholy", new[]
    {
        // Tier 1 (level 1): the two starting choices.
        Minor("bonewall", "Bone Ward", 1, 200, G, 5, Array.Empty<string>(), "+{Max health} max health and +{Block chance}% block chance.", (MaxHealth, 8), (BlockChance, 1.5f)),
        Minor("unholymight", "Unholy Might", 1, 620, S, 5, Array.Empty<string>(), "+{Skull damage}% skull damage and +{Plague damage}% Plague damage.", (SkullDamage, 10), (PlagueDamage, 10)),

        // Tier 2 (level 3)
        Minor("marrow", "Marrow", 2, 100, G, 5, new[] { "bonewall" }, "+{Max health} max health.", (MaxHealth, 15)),
        Minor("skullguard", "Skull Guard", 2, 225, G, 5, new[] { "bonewall" }, "Raise the Skull Shield higher: +{Block chance}% block chance.", (BlockChance, 2)),
        Major(VirulentStrain, "Virulent Strain", 2, 380, P, new[] { "bonewall", "unholymight" }, "Plague stacks up to 5 times on an enemy, not 3."),
        Minor("festering", "Festering", 2, 500, P, 5, new[] { "unholymight" }, "+{Plague damage}% Plague damage.", (PlagueDamage, 12)),
        Minor("grinning", "Grinning Skull", 2, 620, S, 5, new[] { "unholymight" }, "+{Skull damage}% skull damage.", (SkullDamage, 12)),
        Minor("hollowchant", "Hollow Chant", 2, 740, S, 5, new[] { "unholymight" }, "+{Cast speed}% cast speed.", (CastSpeed, 8)),
        Minor("rotwalker", "Rotwalker", 2, 880, R, 3, new[] { "unholymight" }, "Rotting Step recharges {Rotting Step recharge}% faster, and its rot is {Rot area}% wider.", (StepRecharge, 10), (DecayArea, 10)),

        // Tier 3 (level 6)
        Minor("gravehold", "Grave Hold", 3, 100, G, 3, new[] { "marrow" }, "+{Health per second} health per second.", (Regeneration, 0.4f)),
        Major(DeathAndDecay, "Death and Decay", 3, 225, G, new[] { "skullguard" },
            "Every block has a 25% chance to spew Death and Decay from the Skull Shield: a wide cone of rot in front of you that lies on the ground for 3 s, and everything in it rots (15 damage a second)."),
        Minor("lingering", "Lingering Sickness", 3, 400, P, 3, new[] { VirulentStrain, "festering" }, "Plague lasts {Plague duration} s longer.", (PlagueDuration, 0.4f)),
        Minor("fever", "Fever", 3, 510, P, 5, new[] { "festering" }, "+{Plague damage}% Plague damage and +{Crit chance}% increased critical chance.", (PlagueDamage, 10), (CritChance, 8)),
        Major(TwinSkulls, "Twin Skulls", 3, 640, S, new[] { "grinning", "hollowchant" }, "Every cast fires a second skull a moment after the first."),
        Minor("splinters", "Bone Splinters", 3, 760, S, 3, new[] { "grinning", "hollowchant" }, "Skulls pierce {Pierce} more enemies.", (Pierce, 1)),
        Minor("festerground", "Festering Ground", 3, 880, R, 5, new[] { "rotwalker" }, "+{Rot damage}% rot damage.", (DecayDamage, 12)),

        // Tier 4 (level 10)
        Minor("gravecloth", "Gravecloth", 4, 100, G, 3, new[] { "gravehold" }, "+{Max health} max health and {Damage taken cut}% less damage taken.", (MaxHealth, 10), (DamageTakenCut, 4)),
        Minor("spreadingrot", "Spreading Rot", 4, 225, G, 3, new[] { DeathAndDecay }, "+{Death and Decay chance}% Death and Decay chance, and a block heals {Heal on block} health.", (DecayChance, 5), (BlockHeal, 1.5f)),
        Major(Pestilence, "Pestilence", 4, 400, P, new[] { "lingering", "fever" },
            "Plague lasts an additional 1.3 seconds. 25% increased damage over time."),
        Minor("swiftbones", "Swift Bones", 4, 580, S, 3, new[] { TwinSkulls, "splinters" }, "+{Skull speed}% skull speed, and skulls last {Skull duration} s longer.", (ProjectileSpeed, 12), (SkullLife, 0.3f)),
        Minor("hungry", "Hungry Skull", 4, 700, S, 3, new[] { "splinters" }, "Skulls sense enemies {Seek range} m further off.", (SeekRange, 2)),
        Major(GraveSoil, "Grave Soil", 4, 860, R, new[] { "festerground" }, "Rot on the ground clings: enemies in it are slowed by 30%."),

        // Tier 5 (level 15)
        Major(BoneArmour, "Bone Armour", 5, 110, G, new[] { "gravecloth", "spreadingrot" }, "Every block raises a bone barrier worth 8% of your max health (up to 25% at once)."),
        Minor("shieldofskulls", "Shield of Skulls", 5, 235, G, 3, new[] { "spreadingrot" }, "+{Block chance}% block chance and +{Death and Decay chance}% Death and Decay chance.", (BlockChance, 3), (DecayChance, 4)),
        Minor("putrid", "Putrid", 5, 395, P, 5, new[] { Pestilence }, "+{Plague damage}% Plague damage and +{Damage to elites and bosses}% damage to elites and bosses.", (PlagueDamage, 12), (EliteDamage, 8)),
        Major(GnashingSkulls, "Gnashing Skulls", 5, 560, S, new[] { "swiftbones", "hungry" }, "A skull that kills gains 1 more pierce and 10% more damage, for every kill."),
        Minor("marrowdrinker", "Marrow Drinker", 5, 690, S, 3, new[] { "hungry" }, "+{Skull damage}% skull damage and +{Crit damage}% critical damage.", (SkullDamage, 12), (CritDamage, 12)),
        Minor("sprawl", "Sprawling Rot", 5, 860, R, 3, new[] { GraveSoil }, "Rot on the ground lasts {Rot duration} s longer and spreads {Rot area}% wider.", (DecayDuration, 0.5f), (DecayArea, 10)),

        // Tier 6 (level 21)
        Major(SoulHarvest, "Soul Harvest", 6, 125, G, new[] { BoneArmour, "shieldofskulls" }, "Every Plagued enemy that dies heals you for 1% of your max health."),
        Minor("deathless", "Deathless", 6, 250, G, 3, new[] { "shieldofskulls" }, "+{Health per second} health per second and {Damage taken cut}% less damage taken.", (Regeneration, 0.5f), (DamageTakenCut, 3)),
        Major(Epidemic, "Epidemic", 6, 400, P, new[] { "putrid" }, "Every skull hit also Plagues the enemies within 2.5 m of the one it struck."),
        Minor("plaguelord", "Plague Lord", 6, 540, P, 3, new[] { "putrid" }, "+{Plague damage}% Plague damage, and Plague lasts {Plague duration} s longer.", (PlagueDamage, 15), (PlagueDuration, 0.3f)),
        Minor("ossuary", "Ossuary", 6, 700, S, 3, new[] { GnashingSkulls, "marrowdrinker" }, "+{Cast speed}% cast speed and skulls pierce {Pierce} more.", (CastSpeed, 10), (Pierce, 1)),
        Major(AuraOfDecay, "Aura of Decay", 6, 860, R, new[] { "sprawl" }, "Everything within 3.5 m of you rots: 8 damage a second."),

        // Tier 7 (level 28): the capstones.
        Major(MouthOfTheGrave, "Mouth of the Grave", 7, 185, G, new[] { SoulHarvest, "deathless" },
            "+8% block chance. Every block spews Death and Decay (if you have it), and its cone reaches 50% further."),
        Major(BlackDeath, "Black Death", 7, 450, P, new[] { Epidemic, "plaguelord" }, "Plague's ticks can be critical hits, and Plague lasts 1 s longer."),
        Major(Legion, "Legion", 7, 640, S, new[] { "ossuary" }, "Every cast fires one more skull, and skulls last 1 s longer."),
        Major(Necropolis, "Necropolis", 7, 860, R, new[] { AuraOfDecay }, "Rot on the ground lasts twice as long, and every enemy it touches catches a stack of Plague each second."),
    }, new[] { 1, 3, 6, 10, 15, 21, 28 },
        new[] { (G, 165f), (P, 430f), (S, 650f), (R, 865f) },
        new HashSet<string> { MaxHealth, Regeneration, BlockHeal, Pierce, SeekRange, PlagueDuration, SkullLife, DecayDuration, PlagueStacks });
}

/// <summary>What the ranks spent in the Unholy tree add up to, in the terms <see cref="PriestStats"/> uses (fractions for percentages).</summary>
internal sealed class UnholyBonuses
{
    public float SkullDamage;
    public float PlagueDamage;
    public float DecayDamage;
    public float CastSpeed;
    public float ProjectileSpeed;
    public float SkullLife;
    public float Pierce;
    public float SeekRange;
    public float PlagueDuration;
    public float PlagueStacks;
    public float CritChance;
    public float CritDamage;
    public float EliteDamage;
    public float DecayDuration;
    public float DecayArea;
    public float StepRecharge;
    public float DecayChance;
    public float MaxHealth;
    public float Regeneration;
    public float BlockChance;
    public float BlockHeal;

    /// <summary>What damage taken is multiplied by: each rank of a cut takes a share off what is left.</summary>
    public float DamageTaken = 1f;

    public bool VirulentStrain;
    public bool DeathAndDecay;
    public bool TwinSkulls;
    public bool Pestilence;
    public bool GraveSoil;
    public bool BoneArmour;
    public bool GnashingSkulls;
    public bool Epidemic;
    public bool SoulHarvest;
    public bool AuraOfDecay;
    public bool MouthOfTheGrave;
    public bool BlackDeath;
    public bool Legion;
    public bool Necropolis;

    public static UnholyBonuses From(IReadOnlyDictionary<string, int> ranks)
    {
        var b = new UnholyBonuses();
        foreach (var node in UnholyTree.Tree.Nodes)
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
                    case UnholyTree.SkullDamage: b.SkullDamage += v / 100f; break;
                    case UnholyTree.PlagueDamage: b.PlagueDamage += v / 100f; break;
                    case UnholyTree.DecayDamage: b.DecayDamage += v / 100f; break;
                    case UnholyTree.CastSpeed: b.CastSpeed += v / 100f; break;
                    case UnholyTree.ProjectileSpeed: b.ProjectileSpeed += v / 100f; break;
                    case UnholyTree.SkullLife: b.SkullLife += v; break;
                    case UnholyTree.Pierce: b.Pierce += v; break;
                    case UnholyTree.SeekRange: b.SeekRange += v; break;
                    case UnholyTree.PlagueDuration: b.PlagueDuration += v; break;
                    case UnholyTree.PlagueStacks: b.PlagueStacks += v; break;
                    case UnholyTree.CritChance: b.CritChance += v / 100f; break;
                    case UnholyTree.CritDamage: b.CritDamage += v / 100f; break;
                    case UnholyTree.EliteDamage: b.EliteDamage += v / 100f; break;
                    case UnholyTree.DecayDuration: b.DecayDuration += v; break;
                    case UnholyTree.DecayArea: b.DecayArea += v / 100f; break;
                    case UnholyTree.StepRecharge: b.StepRecharge += v / 100f; break;
                    case UnholyTree.DecayChance: b.DecayChance += v / 100f; break;
                    case UnholyTree.MaxHealth: b.MaxHealth += v; break;
                    case UnholyTree.Regeneration: b.Regeneration += v; break;
                    case UnholyTree.BlockChance: b.BlockChance += v / 100f; break;
                    case UnholyTree.BlockHeal: b.BlockHeal += v; break;
                    case UnholyTree.DamageTakenCut: b.DamageTaken *= MathF.Pow(1f - perRank / 100f, r); break;
                }
            }
        }

        bool Has(string id) => ranks.GetValueOrDefault(id) > 0;
        b.VirulentStrain = Has(UnholyTree.VirulentStrain);
        b.DeathAndDecay = Has(UnholyTree.DeathAndDecay);
        b.TwinSkulls = Has(UnholyTree.TwinSkulls);
        b.Pestilence = Has(UnholyTree.Pestilence);
        b.GraveSoil = Has(UnholyTree.GraveSoil);
        b.BoneArmour = Has(UnholyTree.BoneArmour);
        b.GnashingSkulls = Has(UnholyTree.GnashingSkulls);
        b.Epidemic = Has(UnholyTree.Epidemic);
        b.SoulHarvest = Has(UnholyTree.SoulHarvest);
        b.AuraOfDecay = Has(UnholyTree.AuraOfDecay);
        b.MouthOfTheGrave = Has(UnholyTree.MouthOfTheGrave);
        b.BlackDeath = Has(UnholyTree.BlackDeath);
        b.Legion = Has(UnholyTree.Legion);
        b.Necropolis = Has(UnholyTree.Necropolis);
        return b;
    }
}
