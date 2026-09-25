using ArenaMaster.Game.Progression;

namespace ArenaMaster.Game.Paladin;

/// <summary>
/// The Paladin's first passive tree: standing your ground. Four lanes - Bulwark (block chance, what a block does, health regeneration), Retribution (unlocking
/// thorns, then making them hurt), Radiance (the Holy Nova itself) and Consecration (area: the nova's reach and the holy circles it leaves) - over seven tiers
/// opening at tree levels 1, 3, 6, 10, 15, 21 and 28. Two starting nodes; thirteen majors, one capstone per lane.
/// </summary>
internal static class DefianceTree
{
    public const string ClassId = "paladin";
    public const string TreeId = "defiance";

    // Stat names, used in node descriptions as {Name} and summed by DefianceBonuses.
    public const string BlockChance = "Block chance";
    public const string StillBlock = "Block chance standing still";
    public const string MaxHealth = "Max health";
    public const string Regeneration = "Health per second";
    public const string LowRegen = "Regeneration below 40% health";
    public const string BashDamage = "Shield bash damage";
    public const string BlockHeal = "Heal on block";
    public const string DamageTakenCut = "Damage taken cut";
    public const string Thorns = "Thorns damage";
    public const string ThornsBonus = "Thorns damage bonus";
    public const string ThornsSpeed = "Thorns speed";
    public const string ThornsFromHealth = "Thorns from max health";
    public const string ThornsKillHeal = "Heal on thorns kill";
    public const string NovaDamage = "Nova damage";
    public const string NovaFrequency = "Nova frequency";
    public const string NovaRadius = "Nova radius";
    public const string CritChance = "Crit chance";
    public const string CritDamage = "Crit damage";
    public const string EliteDamage = "Damage to elites and bosses";
    public const string CircleDamage = "Holy circle damage";
    public const string CircleDuration = "Holy circle duration";
    public const string CircleRadius = "Holy circle size";
    public const string CircleHealing = "Holy circle healing";

    // The majors, by id.
    public const string CrownOfThorns = "thorns";
    public const string EchoingNova = "echo";
    public const string ShieldOfFaith = "faith";
    public const string Retribution = "retribution";
    public const string ConsecratedGround = "consecrated";
    public const string WrathOfTheMany = "wrath";
    public const string ExpandingLight = "expanding";
    public const string UnbrokenVow = "vow";
    public const string Sanctuary = "sanctuary";
    public const string HolyBastion = "bastion";
    public const string CrownOfBriars = "briars";
    public const string RadiantAvatar = "avatar";
    public const string Resonance = "resonance";

    private static TreeNode Minor(string id, string name, int tier, float x, string lane, int max, string[] parents, string text, params (string Stat, float PerRank)[] stats) =>
        new(id, name, tier, x, lane, max, text, parents, stats.ToDictionary(s => s.Stat, s => s.PerRank));

    private static TreeNode Major(string id, string name, int tier, float x, string lane, string[] parents, string text) =>
        new(id, name, tier, x, lane, 1, text, parents, new Dictionary<string, float>(), Major: true);

    private const string B = "Bulwark", R = "Retribution", H = "Radiance", C = "Consecration";

    public static readonly TreeDefinition Tree = new(TreeId, "Defiance", new[]
    {
        // Tier 1 (level 1): the two starting choices.
        Minor("shieldwall", "Shield Wall", 1, 270, B, 5, Array.Empty<string>(), "+{Block chance}% block chance and +{Max health} max health.", (BlockChance, 2), (MaxHealth, 8)),
        Minor("zeal", "Zealous Light", 1, 670, H, 5, Array.Empty<string>(), "+{Nova damage}% Holy Nova damage and +{Nova frequency}% nova frequency.", (NovaDamage, 10), (NovaFrequency, 5)),

        // Tier 2 (level 3)
        Minor("stalwart", "Stalwart", 2, 110, B, 5, new[] { "shieldwall" }, "+{Block chance}% block chance.", (BlockChance, 3)),
        Minor("mending", "Mending Faith", 2, 230, B, 5, new[] { "shieldwall" }, "+{Health per second} health per second.", (Regeneration, 0.3f)),
        Major(CrownOfThorns, "Crown of Thorns", 2, 370, R, new[] { "shieldwall" }, "Unlocks Thorns: every 0.5 s, each enemy touching you takes 6 damage."),
        Minor("fervor", "Fervor", 2, 520, H, 5, new[] { "zeal" }, "+{Nova frequency}% nova frequency.", (NovaFrequency, 8)),
        Minor("holyfire", "Holy Fire", 2, 640, H, 5, new[] { "zeal" }, "+{Nova damage}% Holy Nova damage.", (NovaDamage, 12)),
        Minor("widening", "Widening Light", 2, 770, C, 3, new[] { "zeal" }, "+{Nova radius}% Holy Nova radius.", (NovaRadius, 8)),
        Minor("hallowed", "Hallowed Ground", 2, 890, C, 5, new[] { "zeal" }, "+{Holy circle damage}% holy circle damage.", (CircleDamage, 15)),

        // Tier 3 (level 6)
        Minor("braced", "Braced Stance", 3, 110, B, 3, new[] { "stalwart" }, "+{Block chance standing still}% block chance while you stand still.", (StillBlock, 5)),
        Minor("bash", "Shield Bash", 3, 230, B, 3, new[] { "stalwart", "mending" }, "Blocking a blow deals {Shield bash damage} damage to the attacker.", (BashDamage, 12)),
        Minor("barbed", "Barbed Plate", 3, 350, R, 5, new[] { CrownOfThorns }, "+{Thorns damage} thorns damage.", (Thorns, 4)),
        Minor("quicken", "Quickening Thorns", 3, 460, R, 3, new[] { CrownOfThorns }, "Thorns strike {Thorns speed}% more often.", (ThornsSpeed, 15)),
        Major(EchoingNova, "Echoing Nova", 3, 580, H, new[] { "fervor", "holyfire" }, "Every Holy Nova bursts a second time 0.35 s later, at 60% damage."),
        Minor("lingering", "Lingering Light", 3, 720, C, 3, new[] { "widening", "hallowed" }, "Holy circles last {Holy circle duration} s longer.", (CircleDuration, 1)),
        Minor("sanctum", "Broad Sanctum", 3, 860, C, 3, new[] { "hallowed" }, "+{Holy circle size}% holy circle size.", (CircleRadius, 10)),

        // Tier 4 (level 10)
        Major(ShieldOfFaith, "Shield of Faith", 4, 130, B, new[] { "braced", "bash" }, "Every 12 s a holy shield readies itself and turns aside the next blow, for certain."),
        Minor("guard", "Mending Guard", 4, 250, B, 3, new[] { "bash" }, "Blocking a blow heals {Heal on block} health.", (BlockHeal, 1.5f)),
        Minor("spite", "Spiteful Thorns", 4, 370, R, 3, new[] { "barbed" }, "+{Thorns damage bonus}% thorns damage.", (ThornsBonus, 25)),
        Major(Retribution, "Retribution", 4, 470, R, new[] { "barbed", "quicken" }, "Every blow that reaches you, landed or blocked, is paid back: the attacker takes 200% of it."),
        Minor("judgement", "Judgement", 4, 590, H, 5, new[] { EchoingNova }, "+{Crit chance}% critical chance and +{Crit damage}% critical damage.", (CritChance, 4), (CritDamage, 10)),
        Major(ConsecratedGround, "Consecrated Ground", 4, 730, C, new[] { "lingering" }, "Holy circles' healing stacks: you heal for every circle you stand in."),
        Minor("soothing", "Soothing Light", 4, 860, C, 3, new[] { "lingering", "sanctum" }, "+{Holy circle healing}% holy circle healing.", (CircleHealing, 25)),

        // Tier 5 (level 15)
        Minor("vigil", "Vigil", 5, 110, B, 3, new[] { ShieldOfFaith, "guard" }, "+{Health per second} health per second and +{Max health} max health.", (Regeneration, 0.4f), (MaxHealth, 10)),
        Minor("tower", "Tower Shield", 5, 240, B, 3, new[] { "guard" }, "+{Block chance}% block chance and {Damage taken cut}% less damage taken.", (BlockChance, 3), (DamageTakenCut, 4)),
        Minor("ironbriars", "Iron Briars", 5, 370, R, 3, new[] { "spite", Retribution }, "Thorns deal extra damage equal to {Thorns from max health}% of your max health.", (ThornsFromHealth, 2)),
        Major(WrathOfTheMany, "Wrath of the Many", 5, 520, H, new[] { "judgement" }, "Each Holy Nova deals 2% more damage for every enemy it hits, up to +60%."),
        Minor("smiter", "Smiter", 5, 640, H, 5, new[] { "judgement" }, "+{Damage to elites and bosses}% damage to elites and bosses.", (EliteDamage, 15)),
        Major(ExpandingLight, "Expanding Light", 5, 760, C, new[] { ConsecratedGround, "soothing" }, "Holy circles grow as they burn, to twice their size by the end."),
        Minor("dawnreach", "Dawn's Reach", 5, 880, C, 3, new[] { "soothing" }, "+{Nova radius}% Holy Nova radius and +{Holy circle size}% holy circle size.", (NovaRadius, 10), (CircleRadius, 10)),

        // Tier 6 (level 21)
        Major(UnbrokenVow, "Unbroken Vow", 6, 130, B, new[] { "vigil", "tower" }, "Once per run, a blow that would kill you leaves you on 1 health instead, and heals you for 40% of your max health."),
        Minor("desperate", "Desperate Prayer", 6, 250, B, 3, new[] { "vigil" }, "+{Regeneration below 40% health}% health regeneration while below 40% health.", (LowRegen, 50)),
        Minor("bloodthorns", "Bloodthorns", 6, 380, R, 3, new[] { "ironbriars" }, "Each enemy your thorns kill heals you {Heal on thorns kill} health.", (ThornsKillHeal, 1)),
        Minor("fury", "Righteous Fury", 6, 540, H, 3, new[] { WrathOfTheMany, "smiter" }, "+{Nova damage}% Holy Nova damage and +{Nova frequency}% nova frequency.", (NovaDamage, 15), (NovaFrequency, 5)),
        Minor("sacred", "Sacred Fire", 6, 720, C, 3, new[] { ExpandingLight }, "+{Holy circle damage}% holy circle damage.", (CircleDamage, 20)),
        Major(Sanctuary, "Sanctuary", 6, 860, C, new[] { ExpandingLight, "dawnreach" }, "While you stand in a holy circle, you take 20% less damage and have +10% block chance."),

        // Tier 7 (level 28): the capstones.
        Major(HolyBastion, "Holy Bastion", 7, 180, B, new[] { UnbrokenVow, "desperate" }, "Every blow you block releases a Holy Nova at 75% damage."),
        Major(CrownOfBriars, "Crown of Briars", 7, 380, R, new[] { "bloodthorns" }, "Thorns strike every enemy within 3 m of you, not only those touching you, and deal 50% more damage."),
        Major(RadiantAvatar, "Radiant Avatar", 7, 580, H, new[] { "fury" }, "Every 8th Holy Nova is a Great Nova: twice the radius and triple the damage, and its circle is larger."),
        Major(Resonance, "Resonance", 7, 790, C, new[] { "sacred", Sanctuary }, "Every Holy Nova also bursts from each of your holy circles, at 50% damage."),
    }, new[] { 1, 3, 6, 10, 15, 21, 28 },
        new[] { (B, 170f), (R, 405f), (H, 590f), (C, 810f) },
        new HashSet<string> { MaxHealth, Regeneration, BashDamage, BlockHeal, Thorns, ThornsKillHeal, CircleDuration });
}

/// <summary>What the ranks spent in the Defiance tree add up to, in the terms <see cref="PaladinStats"/> uses (fractions for percentages).</summary>
internal sealed class DefianceBonuses
{
    public float BlockChance;
    public float StillBlock;
    public float MaxHealth;
    public float Regeneration;
    public float LowRegen;
    public float BashDamage;
    public float BlockHeal;
    public float Thorns;
    public float ThornsBonus;
    public float ThornsSpeed;
    public float ThornsFromHealth;
    public float ThornsKillHeal;
    public float NovaDamage;
    public float NovaFrequency;
    public float NovaRadius;
    public float CritChance;
    public float CritDamage;
    public float EliteDamage;
    public float CircleDamage;
    public float CircleDuration;
    public float CircleRadius;
    public float CircleHealing;

    /// <summary>What damage taken is multiplied by: each rank of Tower Shield takes a share off what is left.</summary>
    public float DamageTaken = 1f;

    public bool CrownOfThorns;
    public bool EchoingNova;
    public bool ShieldOfFaith;
    public bool Retribution;
    public bool ConsecratedGround;
    public bool WrathOfTheMany;
    public bool ExpandingLight;
    public bool UnbrokenVow;
    public bool Sanctuary;
    public bool HolyBastion;
    public bool CrownOfBriars;
    public bool RadiantAvatar;
    public bool Resonance;

    public static DefianceBonuses From(IReadOnlyDictionary<string, int> ranks)
    {
        var b = new DefianceBonuses();
        foreach (var node in DefianceTree.Tree.Nodes)
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
                    case DefianceTree.BlockChance: b.BlockChance += v / 100f; break;
                    case DefianceTree.StillBlock: b.StillBlock += v / 100f; break;
                    case DefianceTree.MaxHealth: b.MaxHealth += v; break;
                    case DefianceTree.Regeneration: b.Regeneration += v; break;
                    case DefianceTree.LowRegen: b.LowRegen += v / 100f; break;
                    case DefianceTree.BashDamage: b.BashDamage += v; break;
                    case DefianceTree.BlockHeal: b.BlockHeal += v; break;
                    case DefianceTree.Thorns: b.Thorns += v; break;
                    case DefianceTree.ThornsBonus: b.ThornsBonus += v / 100f; break;
                    case DefianceTree.ThornsSpeed: b.ThornsSpeed += v / 100f; break;
                    case DefianceTree.ThornsFromHealth: b.ThornsFromHealth += v / 100f; break;
                    case DefianceTree.ThornsKillHeal: b.ThornsKillHeal += v; break;
                    case DefianceTree.NovaDamage: b.NovaDamage += v / 100f; break;
                    case DefianceTree.NovaFrequency: b.NovaFrequency += v / 100f; break;
                    case DefianceTree.NovaRadius: b.NovaRadius += v / 100f; break;
                    case DefianceTree.CritChance: b.CritChance += v / 100f; break;
                    case DefianceTree.CritDamage: b.CritDamage += v / 100f; break;
                    case DefianceTree.EliteDamage: b.EliteDamage += v / 100f; break;
                    case DefianceTree.CircleDamage: b.CircleDamage += v / 100f; break;
                    case DefianceTree.CircleDuration: b.CircleDuration += v; break;
                    case DefianceTree.CircleRadius: b.CircleRadius += v / 100f; break;
                    case DefianceTree.CircleHealing: b.CircleHealing += v / 100f; break;
                    case DefianceTree.DamageTakenCut: b.DamageTaken *= MathF.Pow(1f - perRank / 100f, r); break;
                }
            }
        }

        bool Has(string id) => ranks.GetValueOrDefault(id) > 0;
        b.CrownOfThorns = Has(DefianceTree.CrownOfThorns);
        b.EchoingNova = Has(DefianceTree.EchoingNova);
        b.ShieldOfFaith = Has(DefianceTree.ShieldOfFaith);
        b.Retribution = Has(DefianceTree.Retribution);
        b.ConsecratedGround = Has(DefianceTree.ConsecratedGround);
        b.WrathOfTheMany = Has(DefianceTree.WrathOfTheMany);
        b.ExpandingLight = Has(DefianceTree.ExpandingLight);
        b.UnbrokenVow = Has(DefianceTree.UnbrokenVow);
        b.Sanctuary = Has(DefianceTree.Sanctuary);
        b.HolyBastion = Has(DefianceTree.HolyBastion);
        b.CrownOfBriars = Has(DefianceTree.CrownOfBriars);
        b.RadiantAvatar = Has(DefianceTree.RadiantAvatar);
        b.Resonance = Has(DefianceTree.Resonance);
        return b;
    }
}
