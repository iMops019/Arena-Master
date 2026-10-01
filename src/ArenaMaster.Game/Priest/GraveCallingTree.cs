using ArenaMaster.Game.Progression;

namespace ArenaMaster.Game.Priest;

/// <summary>
/// The Priest's second passive tree: necromancy, the dead getting back up to fight. With it active the skulls carry no Plague and hit harder instead (see
/// <see cref="PriestStats.GraveCalling"/>). Three lanes - Servants (the raised dead: how many, how tough, how hard they claw, and what they do when they fall),
/// Soul (the souls kills leave: the charges they give the next cast, and what gathering them does) and Bone (bone spears, and the bone that guards the Priest:
/// health, blocking and the Bone Cage) - over seven tiers opening at tree levels 1, 3, 6, 10, 15, 21 and 28. Two starting nodes; twelve majors, a capstone per
/// lane.
/// </summary>
internal static class GraveCallingTree
{
    public const string TreeId = "gravecalling";

    // Stat names, used in node descriptions as {Name} and summed by GraveCallingBonuses.
    public const string SkullDamage = "Skull damage";
    public const string ServantDamage = "Servant damage";
    public const string ServantHealth = "Servant health";
    public const string ServantDuration = "Servant duration";
    public const string ServantSpeed = "Servant attack speed";
    public const string CastSpeed = "Cast speed";
    public const string EliteDamage = "Damage to elites and bosses";
    public const string SoulReach = "Soul reach";
    public const string SoulDuration = "Soul duration";
    public const string SoulHeal = "Soul heal";
    public const string ChargeDamage = "Charge damage";
    public const string SpearDamage = "Bone spear damage";
    public const string CageTime = "Cage time";
    public const string MaxHealth = "Max health";
    public const string Regeneration = "Health per second";
    public const string BlockChance = "Block chance";
    public const string DamageTakenCut = "Damage taken cut";

    // The majors, by id.
    public const string RaiseDead = "raisedead";
    public const string SoulSiphon = "soulsiphon";
    public const string BoneSpear = "bonespear";
    public const string CorpseBurst = "corpseburst";
    public const string VengefulSpirits = "vengeful";
    public const string BoneCage = "bonecage";
    public const string SoulWell = "soulwell";
    public const string DeathsCommand = "deathscommand";
    public const string Impaler = "impaler";
    public const string ArmyOfTheDead = "armyofthedead";
    public const string LichForm = "lichform";
    public const string BoneColossus = "bonecolossus";

    private static TreeNode Minor(string id, string name, int tier, float x, string lane, int max, string[] parents, string text, params (string Stat, float PerRank)[] stats) =>
        new(id, name, tier, x, lane, max, text, parents, stats.ToDictionary(s => s.Stat, s => s.PerRank));

    private static TreeNode Major(string id, string name, int tier, float x, string lane, string[] parents, string text) =>
        new(id, name, tier, x, lane, 1, text, parents, new Dictionary<string, float>(), Major: true);

    private const string S = "Servants", O = "Soul", B = "Bone";

    public static readonly TreeDefinition Tree = new(TreeId, "Grave Calling", new[]
    {
        // Tier 1 (level 1): the two starting choices.
        Minor("gravepact", "Grave Pact", 1, 260, S, 5, Array.Empty<string>(), "+{Skull damage}% skull damage and +{Servant damage}% servant damage.", (SkullDamage, 8), (ServantDamage, 10)),
        Minor("bonecraft", "Bone Craft", 1, 700, B, 5, Array.Empty<string>(), "+{Skull damage}% skull damage and +{Max health} max health.", (SkullDamage, 8), (MaxHealth, 8)),

        // Tier 2 (level 3)
        Major(RaiseDead, "Raise Dead", 2, 110, S, new[] { "gravepact" },
            "Every 8th kill rises again as a skeleton that fights beside you (up to 4 at once). It claws the nearest enemy near you for 12 damage, wears down while enemies touch it, and crumbles after 30 s."),
        Minor("gravechant", "Grave Chant", 2, 240, S, 5, new[] { "gravepact" }, "+{Cast speed}% cast speed.", (CastSpeed, 8)),
        Major(SoulSiphon, "Soul Siphon", 2, 400, O, new[] { "gravepact", "bonecraft" },
            "Every kill leaves a soul for 8 s. Walk over one to heal 1% of your max health and gain a charge (up to 10): your next cast deals 10% more for each charge, and spends them."),
        Minor("soulfire", "Soul Fire", 2, 520, O, 5, new[] { "gravepact", "bonecraft" }, "+{Skull damage}% skull damage.", (SkullDamage, 12)),
        Minor("marrowchant", "Marrow Chant", 2, 640, B, 5, new[] { "bonecraft" }, "+{Cast speed}% cast speed.", (CastSpeed, 8)),
        Minor("ossified", "Ossified", 2, 820, B, 5, new[] { "bonecraft" }, "+{Max health} max health and +{Block chance}% block chance.", (MaxHealth, 12), (BlockChance, 1.5f)),

        // Tier 3 (level 6)
        Minor("gravebond", "Grave Bond", 3, 90, S, 5, new[] { RaiseDead }, "Your servants have +{Servant health}% health.", (ServantHealth, 15)),
        Minor("graspingdead", "Grasping Dead", 3, 220, S, 5, new[] { RaiseDead, "gravechant" }, "+{Servant damage}% servant damage.", (ServantDamage, 12)),
        Minor("soulcall", "Soul Call", 3, 380, O, 3, new[] { SoulSiphon }, "Souls are gathered from {Soul reach} m further, and last {Soul duration} s longer.", (SoulReach, 0.5f), (SoulDuration, 1)),
        Minor("reaperstithe", "Reaper's Tithe", 3, 510, O, 5, new[] { "soulfire" }, "+{Skull damage}% skull damage and +{Damage to elites and bosses}% damage to elites and bosses.", (SkullDamage, 8), (EliteDamage, 6)),
        Major(BoneSpear, "Bone Spear", 3, 660, B, new[] { "marrowchant" },
            "Every 4th cast is a bone spear instead: it flies straight for 25 m through everything in its line, for 300% of a skull's damage."),
        Minor("ribguard", "Rib Guard", 3, 830, B, 5, new[] { "ossified" }, "+{Block chance}% block chance.", (BlockChance, 2)),

        // Tier 4 (level 10)
        Major(CorpseBurst, "Corpse Burst", 4, 110, S, new[] { "gravebond", "graspingdead" },
            "When a servant dies or crumbles, it bursts: 200% of a skull's damage to everything within 3 m."),
        Minor("longwatch", "Long Watch", 4, 240, S, 3, new[] { "graspingdead" }, "Your servants last {Servant duration} s longer and claw {Servant attack speed}% faster.", (ServantDuration, 3), (ServantSpeed, 8)),
        Major(VengefulSpirits, "Vengeful Spirits", 4, 400, O, new[] { "soulcall" },
            "Every soul you gather flies out as a spirit at the nearest enemy within 10 m, for 60% of a skull's damage."),
        Minor("soulhunger", "Soul Hunger", 4, 530, O, 3, new[] { "reaperstithe" }, "Each charge adds {Charge damage}% more to your next cast.", (ChargeDamage, 2)),
        Minor("sharpenedmarrow", "Sharpened Marrow", 4, 670, B, 5, new[] { BoneSpear }, "+{Bone spear damage}% bone spear damage.", (SpearDamage, 15)),
        Major(BoneCage, "Bone Cage", 4, 830, B, new[] { "ribguard" }, "Every block wraps the attacker in a cage of bones, holding it for 1 s (not bosses)."),

        // Tier 5 (level 15)
        Minor("gravelord", "Grave Lord", 5, 90, S, 3, new[] { CorpseBurst }, "Your servants have +{Servant health}% health and deal +{Servant damage}% damage.", (ServantHealth, 10), (ServantDamage, 10)),
        Minor("carrionfeast", "Carrion Feast", 5, 230, S, 5, new[] { "longwatch" }, "+{Servant damage}% servant damage and +{Skull damage}% skull damage.", (ServantDamage, 10), (SkullDamage, 6)),
        Major(SoulWell, "Soul Well", 5, 390, O, new[] { VengefulSpirits, "soulhunger" }, "A cast spends at most 3 charges, so the rest build up."),
        Minor("soultide", "Soul Tide", 5, 530, O, 3, new[] { "soulhunger" }, "+{Cast speed}% cast speed, and souls are gathered from {Soul reach} m further.", (CastSpeed, 8), (SoulReach, 0.5f)),
        Minor("boneplate", "Bone Plate", 5, 680, B, 3, new[] { "sharpenedmarrow", BoneCage }, "+{Max health} max health and {Damage taken cut}% less damage taken.", (MaxHealth, 12), (DamageTakenCut, 3)),
        Minor("tightbars", "Tight Bars", 5, 830, B, 3, new[] { BoneCage }, "Bone Cages hold {Cage time} s longer, and +{Block chance}% block chance.", (CageTime, 0.2f), (BlockChance, 1.5f)),

        // Tier 6 (level 21)
        Major(DeathsCommand, "Death's Command", 6, 110, S, new[] { "gravelord", "carrionfeast" }, "Your servants claw 40% faster and last 10 s longer."),
        Minor("unendingdead", "Unending Dead", 6, 240, S, 3, new[] { "carrionfeast" }, "Your servants last {Servant duration} s longer and have +{Servant health}% health.", (ServantDuration, 4), (ServantHealth, 10)),
        Minor("soulrend", "Soul Rend", 6, 390, O, 3, new[] { SoulWell }, "+{Skull damage}% skull damage, and each charge adds {Charge damage}% more to your next cast.", (SkullDamage, 10), (ChargeDamage, 2)),
        Minor("spiritwell", "Spirit Well", 6, 520, O, 3, new[] { SoulWell, "soultide" }, "Each soul heals {Soul heal}% more of your max health.", (SoulHeal, 0.2f)),
        Major(Impaler, "Impaler", 6, 680, B, new[] { "boneplate" }, "Every 3rd cast is a bone spear, not every 4th, and bone spears fly 50% further."),
        Minor("marrowward", "Marrow Ward", 6, 840, B, 3, new[] { "boneplate", "tightbars" }, "+{Health per second} health per second and +{Block chance}% block chance.", (Regeneration, 0.4f), (BlockChance, 2)),

        // Tier 7 (level 28): the capstones.
        Major(ArmyOfTheDead, "Army of the Dead", 7, 175, S, new[] { DeathsCommand, "unendingdead" },
            "Raise Dead keeps up to 8 servants, and an enemy a servant kills rises as another at once."),
        Major(LichForm, "Lich Form", 7, 455, O, new[] { "soulrend", "spiritwell" },
            "Your casts no longer spend charges: they are saved up. At 10, you become a Lich for 8 s: every cast fires three skulls instead of one. Then the charges start again from nothing."),
        Major(BoneColossus, "Bone Colossus", 7, 760, B, new[] { Impaler, "marrowward" },
            "Needs Raise Dead. Once you have as many servants as you can raise, the next raise merges them all into a Bone Colossus for 20 s: three times the size, five times the health, four times the damage, and its blows hit everything within 2.5 m."),
    }, new[] { 1, 3, 6, 10, 15, 21, 28 },
        new[] { (S, 170f), (O, 460f), (B, 760f) },
        new HashSet<string> { MaxHealth, Regeneration, ServantDuration, SoulReach, SoulDuration, CageTime });
}

/// <summary>What the ranks spent in the Grave Calling tree add up to, in the terms <see cref="PriestStats"/> uses (fractions for percentages).</summary>
internal sealed class GraveCallingBonuses
{
    public float SkullDamage;
    public float ServantDamage;
    public float ServantHealth;
    public float ServantDuration;
    public float ServantSpeed;
    public float CastSpeed;
    public float EliteDamage;
    public float SoulReach;
    public float SoulDuration;
    public float SoulHeal;
    public float ChargeDamage;
    public float SpearDamage;
    public float CageTime;
    public float MaxHealth;
    public float Regeneration;
    public float BlockChance;

    /// <summary>What damage taken is multiplied by: each rank of a cut takes a share off what is left.</summary>
    public float DamageTaken = 1f;

    public bool RaiseDead;
    public bool SoulSiphon;
    public bool BoneSpear;
    public bool CorpseBurst;
    public bool VengefulSpirits;
    public bool BoneCage;
    public bool SoulWell;
    public bool DeathsCommand;
    public bool Impaler;
    public bool ArmyOfTheDead;
    public bool LichForm;
    public bool BoneColossus;

    public static GraveCallingBonuses From(IReadOnlyDictionary<string, int> ranks)
    {
        var b = new GraveCallingBonuses();
        foreach (var node in GraveCallingTree.Tree.Nodes)
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
                    case GraveCallingTree.SkullDamage: b.SkullDamage += v / 100f; break;
                    case GraveCallingTree.ServantDamage: b.ServantDamage += v / 100f; break;
                    case GraveCallingTree.ServantHealth: b.ServantHealth += v / 100f; break;
                    case GraveCallingTree.ServantDuration: b.ServantDuration += v; break;
                    case GraveCallingTree.ServantSpeed: b.ServantSpeed += v / 100f; break;
                    case GraveCallingTree.CastSpeed: b.CastSpeed += v / 100f; break;
                    case GraveCallingTree.EliteDamage: b.EliteDamage += v / 100f; break;
                    case GraveCallingTree.SoulReach: b.SoulReach += v; break;
                    case GraveCallingTree.SoulDuration: b.SoulDuration += v; break;
                    case GraveCallingTree.SoulHeal: b.SoulHeal += v / 100f; break;
                    case GraveCallingTree.ChargeDamage: b.ChargeDamage += v / 100f; break;
                    case GraveCallingTree.SpearDamage: b.SpearDamage += v / 100f; break;
                    case GraveCallingTree.CageTime: b.CageTime += v; break;
                    case GraveCallingTree.MaxHealth: b.MaxHealth += v; break;
                    case GraveCallingTree.Regeneration: b.Regeneration += v; break;
                    case GraveCallingTree.BlockChance: b.BlockChance += v / 100f; break;
                    case GraveCallingTree.DamageTakenCut: b.DamageTaken *= MathF.Pow(1f - perRank / 100f, r); break;
                }
            }
        }

        bool Has(string id) => ranks.GetValueOrDefault(id) > 0;
        b.RaiseDead = Has(GraveCallingTree.RaiseDead);
        b.SoulSiphon = Has(GraveCallingTree.SoulSiphon);
        b.BoneSpear = Has(GraveCallingTree.BoneSpear);
        b.CorpseBurst = Has(GraveCallingTree.CorpseBurst);
        b.VengefulSpirits = Has(GraveCallingTree.VengefulSpirits);
        b.BoneCage = Has(GraveCallingTree.BoneCage);
        b.SoulWell = Has(GraveCallingTree.SoulWell);
        b.DeathsCommand = Has(GraveCallingTree.DeathsCommand);
        b.Impaler = Has(GraveCallingTree.Impaler);
        b.ArmyOfTheDead = Has(GraveCallingTree.ArmyOfTheDead);
        b.LichForm = Has(GraveCallingTree.LichForm);
        b.BoneColossus = Has(GraveCallingTree.BoneColossus);
        return b;
    }
}
