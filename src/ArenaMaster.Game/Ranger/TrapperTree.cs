using ArenaMaster.Game.Progression;

namespace ArenaMaster.Game.Ranger;

/// <summary>
/// The Ranger's second passive tree: getting ready before the fight. Three lanes - Snares (traps dropped with every dash), Venom (poisoned arrows, clouds of it,
/// and the Ranger's first damage over time) and Wilds (a hawk that hunts the toughest enemy, and staying alive) - over seven tiers opening at tree levels 1, 3, 6,
/// 10, 15, 21 and 28. Two starting nodes; eleven majors. The bow itself doesn't change: same shots, same aim.
/// </summary>
internal static class TrapperTree
{
    public const string TreeId = "trapper";

    // Stat names, used in node descriptions as {Name} and summed by TrapperBonuses.
    public const string TrapDamage = "Trap damage";
    public const string DashRecharge = "Dash recharge";
    public const string PoisonDamage = "Poison damage";
    public const string Damage = "Damage";
    public const string MoveSpeed = "Move speed";
    public const string HoldTime = "Hold time";
    public const string BurstRadius = "Burst size";
    public const string Snares = "Snares";
    public const string SnareTime = "Snare time";
    public const string PoisonTime = "Poison time";
    public const string AttackSpeed = "Attack speed";
    public const string HawkDamage = "Hawk damage";
    public const string PoisonStacks = "Poison stacks";
    public const string CloudSize = "Cloud size";
    public const string CloudTime = "Cloud time";
    public const string MaxHealth = "Max health";
    public const string Regeneration = "Health per second";
    public const string PoisonedDamage = "Damage to poisoned enemies";
    public const string HawkRate = "Dive rate";
    public const string HawkRange = "Hunting range";
    public const string Pickup = "Pickup range";
    public const string HeldDamage = "Damage to held enemies";
    public const string DashDistance = "Dash distance";
    public const string DamageTakenCut = "Damage taken cut";

    // The majors, by id.
    public const string SnareLine = "snareline";
    public const string VenomTips = "venomtips";
    public const string HawkCompanion = "hawk";
    public const string ToxicCloud = "toxiccloud";
    public const string HawksMark = "hawksmark";
    public const string Caltrops = "caltrops";
    public const string CripplingVenom = "crippling";
    public const string KeenTalons = "talons";
    public const string KillingField = "killingfield";
    public const string ApexPredator = "apex";
    public const string BlightArrows = "blight";

    private static TreeNode Minor(string id, string name, int tier, float x, string lane, int max, string[] parents, string text, params (string Stat, float PerRank)[] stats) =>
        new(id, name, tier, x, lane, max, text, parents, stats.ToDictionary(s => s.Stat, s => s.PerRank));

    private static TreeNode Major(string id, string name, int tier, float x, string lane, string[] parents, string text) =>
        new(id, name, tier, x, lane, 1, text, parents, new Dictionary<string, float>(), Major: true);

    private const string S = "Snares", V = "Venom", W = "Wilds";

    public static readonly TreeDefinition Tree = new(TreeId, "Trapper", new[]
    {
        // Tier 1 (level 1): the two starting choices.
        Minor("trapcraft", "Trap Craft", 1, 360, S, 5, Array.Empty<string>(), "+{Trap damage}% trap damage, and your dash recharges {Dash recharge}% faster.", (TrapDamage, 12), (DashRecharge, 6)),
        Minor("tipped", "Tipped Arrows", 1, 580, V, 5, Array.Empty<string>(), "+{Poison damage}% poison damage and +{Damage}% damage.", (PoisonDamage, 10), (Damage, 8)),

        // Tier 2 (level 3): each lane's first major.
        Minor("ironjaws", "Iron Jaws", 2, 150, S, 5, new[] { "trapcraft" }, "+{Trap damage}% trap damage.", (TrapDamage, 15)),
        Major(SnareLine, "Snare Line", 2, 270, S, new[] { "trapcraft" },
            "Every dash drops a snare where it began. The first enemy to step on it is held for 1.5 s (elites half that; bosses aren't held), then it bursts, hurting everything within 2.5 m for 250% of an arrow's damage. Snares last 12 s; 6 at most."),
        Minor("lightfeet", "Light Feet", 2, 400, S, 5, new[] { "trapcraft", "tipped" }, "+{Move speed}% move speed.", (MoveSpeed, 4)),
        Major(VenomTips, "Venom Tips", 2, 530, V, new[] { "tipped" },
            "Every arrow poisons what it hits: 40% of the hit's damage over 3 s. Up to 5 poisons at once on an enemy, each its own."),
        Minor("nightshade", "Nightshade", 2, 660, V, 5, new[] { "tipped" }, "+{Poison damage}% poison damage.", (PoisonDamage, 12)),
        Major(HawkCompanion, "Hawk Companion", 2, 790, W, new[] { "tipped" },
            "A hawk circles above you. Every 4 s it dives at the toughest enemy within 20 m (bosses, then elites, then the most health) for 250% of an arrow's damage."),

        // Tier 3 (level 6)
        Minor("tripwire", "Tripwire", 3, 140, S, 3, new[] { "ironjaws", SnareLine }, "Snares hold {Hold time}% longer and burst {Burst size}% wider.", (HoldTime, 15), (BurstRadius, 10)),
        Minor("spares", "Spare Snares", 3, 260, S, 2, new[] { SnareLine }, "+{Snares} to the snares you can have out at once, and snares last {Snare time}% longer.", (Snares, 1), (SnareTime, 20)),
        Minor("lingering", "Lingering Venom", 3, 390, V, 3, new[] { VenomTips, "lightfeet" }, "Poison lasts {Poison time}% longer, hurting for longer.", (PoisonTime, 15)),
        Major(ToxicCloud, "Toxic Cloud", 3, 520, V, new[] { VenomTips },
            "A poisoned enemy that dies leaves a cloud 2.5 m across for 3 s. Whatever walks through it is poisoned, once a second."),
        Minor("fletching", "Swift Fletching", 3, 650, V, 5, new[] { "nightshade" }, "+{Attack speed}% attack speed.", (AttackSpeed, 6)),
        Minor("falconer", "Falconer's Glove", 3, 790, W, 5, new[] { HawkCompanion }, "The hawk deals +{Hawk damage}% damage.", (HawkDamage, 15)),

        // Tier 4 (level 10)
        Minor("serrated", "Serrated Jaws", 4, 150, S, 3, new[] { "tripwire" }, "+{Trap damage}% trap damage.", (TrapDamage, 20)),
        Minor("stride", "Trapper's Stride", 4, 280, S, 3, new[] { "spares", "tripwire" }, "Your dash recharges {Dash recharge}% faster and goes {Dash distance}% further.", (DashRecharge, 8), (DashDistance, 10)),
        Minor("potent", "Potent Venom", 4, 420, V, 2, new[] { "lingering", ToxicCloud }, "Enemies can carry {Poison stacks} more poison at once.", (PoisonStacks, 1)),
        Minor("fumes", "Choking Fumes", 4, 550, V, 3, new[] { ToxicCloud }, "Toxic clouds are {Cloud size}% wider and last {Cloud time}% longer.", (CloudSize, 20), (CloudTime, 20)),
        Major(HawksMark, "Hawk's Mark", 4, 680, W, new[] { "falconer" }, "The enemy the hawk dives at takes 25% more damage from you for 4 s."),
        Minor("wildheart", "Wild Heart", 4, 820, W, 5, new[] { "falconer" }, "+{Max health} max health and +{Health per second} health per second.", (MaxHealth, 10), (Regeneration, 0.2f)),

        // Tier 5 (level 15)
        Major(Caltrops, "Caltrops", 5, 160, S, new[] { "serrated", "stride" },
            "A bursting snare leaves caltrops where it was for 4 s: everything in them is slowed by 40% and takes 20% of the burst's damage each second."),
        Minor("deadfall", "Deadfall", 5, 300, S, 3, new[] { "stride" }, "+{Trap damage}% trap damage and snares burst {Burst size}% wider.", (TrapDamage, 15), (BurstRadius, 10)),
        Minor("toxin", "Concentrated Toxin", 5, 440, V, 3, new[] { "potent" }, "+{Poison damage}% poison damage.", (PoisonDamage, 20)),
        Minor("festering", "Festering Wounds", 5, 570, V, 3, new[] { "fumes" }, "Poisoned enemies take +{Damage to poisoned enemies}% damage from you.", (PoisonedDamage, 8)),
        Minor("swiftwings", "Swift Wings", 5, 700, W, 3, new[] { HawksMark }, "The hawk dives {Dive rate}% more often.", (HawkRate, 12)),
        Minor("keensight", "Keen Sight", 5, 830, W, 3, new[] { "wildheart", HawksMark }, "The hawk hunts {Hunting range}% further away, and +{Pickup range}% pickup range.", (HawkRange, 15), (Pickup, 10)),

        // Tier 6 (level 21)
        Minor("beartraps", "Bear Traps", 6, 150, S, 3, new[] { Caltrops, "deadfall" }, "Snares hold {Hold time}% longer and deal +{Trap damage}% trap damage.", (HoldTime, 15), (TrapDamage, 10)),
        Minor("ambush", "Ambush", 6, 290, S, 3, new[] { "deadfall" }, "Enemies a snare is holding take +{Damage to held enemies}% damage from you.", (HeldDamage, 15)),
        Major(CripplingVenom, "Crippling Venom", 6, 430, V, new[] { "toxin" }, "An enemy carrying as much poison as it can is slowed by 35%."),
        Minor("deepvenom", "Deep Venom", 6, 560, V, 3, new[] { "toxin", "festering" }, "+{Poison damage}% poison damage, and poison lasts {Poison time}% longer.", (PoisonDamage, 15), (PoisonTime, 10)),
        Major(KeenTalons, "Keen Talons", 6, 700, W, new[] { "swiftwings" }, "The hawk's dive is always a critical hit."),
        Minor("instinct", "Survivor's Instinct", 6, 840, W, 3, new[] { "keensight" }, "+{Max health} max health and {Damage taken cut}% less damage taken.", (MaxHealth, 15), (DamageTakenCut, 5)),

        // Tier 7 (level 28): the capstones.
        Major(KillingField, "Killing Field", 7, 220, S, new[] { "beartraps", "ambush" }, "A snare whose burst kills something sets itself again, once."),
        Major(BlightArrows, "Blight Arrows", 7, 500, V, new[] { CripplingVenom, "deepvenom" }, "Arrows pass through poisoned enemies without using up any pierce."),
        Major(ApexPredator, "Apex Predator", 7, 770, W, new[] { KeenTalons, "instinct" }, "The hawk dives twice as often, and its dive poisons its prey as much as it can carry."),
    }, new[] { 1, 3, 6, 10, 15, 21, 28 },
        new[] { (S, 220f), (V, 490f), (W, 770f) },
        new HashSet<string> { Snares, PoisonStacks, MaxHealth, Regeneration });
}

/// <summary>What the ranks spent in the Trapper tree add up to, in the terms <see cref="RangerStats"/> and <see cref="TrapperKit"/> use (fractions for percentages).</summary>
internal sealed class TrapperBonuses
{
    public float TrapDamage;
    public float DashRecharge;
    public float PoisonDamage;
    public float Damage;
    public float MoveSpeed;
    public float HoldTime;
    public float BurstRadius;
    public int Snares;
    public float SnareTime;
    public float PoisonTime;
    public float AttackSpeed;
    public float HawkDamage;
    public int PoisonStacks;
    public float CloudSize;
    public float CloudTime;
    public float MaxHealth;
    public float Regeneration;
    public float PoisonedDamage;
    public float HawkRate;
    public float HawkRange;
    public float Pickup;
    public float HeldDamage;
    public float DashDistance;

    /// <summary>What damage taken is multiplied by: each rank of Survivor's Instinct takes a share off what is left.</summary>
    public float DamageTaken = 1f;

    public bool SnareLine;
    public bool VenomTips;
    public bool HawkCompanion;
    public bool ToxicCloud;
    public bool HawksMark;
    public bool Caltrops;
    public bool CripplingVenom;
    public bool KeenTalons;
    public bool KillingField;
    public bool ApexPredator;
    public bool BlightArrows;

    public static TrapperBonuses From(IReadOnlyDictionary<string, int> ranks)
    {
        var b = new TrapperBonuses();
        foreach (var node in TrapperTree.Tree.Nodes)
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
                    case TrapperTree.TrapDamage: b.TrapDamage += v / 100f; break;
                    case TrapperTree.DashRecharge: b.DashRecharge += v / 100f; break;
                    case TrapperTree.PoisonDamage: b.PoisonDamage += v / 100f; break;
                    case TrapperTree.Damage: b.Damage += v / 100f; break;
                    case TrapperTree.MoveSpeed: b.MoveSpeed += v / 100f; break;
                    case TrapperTree.HoldTime: b.HoldTime += v / 100f; break;
                    case TrapperTree.BurstRadius: b.BurstRadius += v / 100f; break;
                    case TrapperTree.Snares: b.Snares += (int)v; break;
                    case TrapperTree.SnareTime: b.SnareTime += v / 100f; break;
                    case TrapperTree.PoisonTime: b.PoisonTime += v / 100f; break;
                    case TrapperTree.AttackSpeed: b.AttackSpeed += v / 100f; break;
                    case TrapperTree.HawkDamage: b.HawkDamage += v / 100f; break;
                    case TrapperTree.PoisonStacks: b.PoisonStacks += (int)v; break;
                    case TrapperTree.CloudSize: b.CloudSize += v / 100f; break;
                    case TrapperTree.CloudTime: b.CloudTime += v / 100f; break;
                    case TrapperTree.MaxHealth: b.MaxHealth += v; break;
                    case TrapperTree.Regeneration: b.Regeneration += v; break;
                    case TrapperTree.PoisonedDamage: b.PoisonedDamage += v / 100f; break;
                    case TrapperTree.HawkRate: b.HawkRate += v / 100f; break;
                    case TrapperTree.HawkRange: b.HawkRange += v / 100f; break;
                    case TrapperTree.Pickup: b.Pickup += v / 100f; break;
                    case TrapperTree.HeldDamage: b.HeldDamage += v / 100f; break;
                    case TrapperTree.DashDistance: b.DashDistance += v / 100f; break;
                    case TrapperTree.DamageTakenCut: b.DamageTaken *= MathF.Pow(1f - perRank / 100f, r); break;
                }
            }
        }

        b.SnareLine = ranks.GetValueOrDefault(TrapperTree.SnareLine) > 0;
        b.VenomTips = ranks.GetValueOrDefault(TrapperTree.VenomTips) > 0;
        b.HawkCompanion = ranks.GetValueOrDefault(TrapperTree.HawkCompanion) > 0;
        b.ToxicCloud = ranks.GetValueOrDefault(TrapperTree.ToxicCloud) > 0;
        b.HawksMark = ranks.GetValueOrDefault(TrapperTree.HawksMark) > 0;
        b.Caltrops = ranks.GetValueOrDefault(TrapperTree.Caltrops) > 0;
        b.CripplingVenom = ranks.GetValueOrDefault(TrapperTree.CripplingVenom) > 0;
        b.KeenTalons = ranks.GetValueOrDefault(TrapperTree.KeenTalons) > 0;
        b.KillingField = ranks.GetValueOrDefault(TrapperTree.KillingField) > 0;
        b.ApexPredator = ranks.GetValueOrDefault(TrapperTree.ApexPredator) > 0;
        b.BlightArrows = ranks.GetValueOrDefault(TrapperTree.BlightArrows) > 0;
        return b;
    }
}
