using ArenaMaster.Game.Progression;

namespace ArenaMaster.Game.Shaman;

/// <summary>
/// The Shaman's first passive tree, Lightning Alignment. Three lanes - Tempest (the rolling ball itself: damage, cast speed, bounces, size), Conduction (the forks:
/// how many, how far, how hard, and forking from trees and the ground) and Attunement (the Shaman: staying alive, moving, the surge, and lightning that answers
/// being hit) - over seven tiers opening at tree levels 1, 3, 6, 10, 15, 21 and 28. Two starting nodes; twelve majors, a capstone per lane.
/// </summary>
internal static class AlignmentTree
{
    public const string ClassId = "shaman";
    public const string TreeId = "alignment";

    // Stat names, used in node descriptions as {Name} and summed by AlignmentBonuses.
    public const string LightningDamage = "Lightning damage";
    public const string CastSpeed = "Cast speed";
    public const string Bounces = "Bounces";
    public const string BallSize = "Ball size";
    public const string ZapDamage = "Bounce zap damage";
    public const string Lifetime = "Ball lifetime";
    public const string Forks = "Forks";
    public const string ForkDamage = "Fork damage";
    public const string ForkRange = "Fork range";
    public const string RodDamage = "Lightning rod damage";
    public const string CritChance = "Crit chance";
    public const string CritDamage = "Crit damage";
    public const string MaxHealth = "Max health";
    public const string Regeneration = "Health per second";
    public const string DamageTakenCut = "Damage taken cut";
    public const string MoveSpeed = "Move speed";
    public const string SurgeRecharge = "Surge recharge";
    public const string ShockOnStruck = "Shock when struck";
    public const string HealOnKill = "Heal on lightning kill";
    public const string EliteDamage = "Damage to elites and bosses";

    // The majors, by id.
    public const string Paralysis = "paralysis";
    public const string Thunderclap = "thunderclap";
    public const string ChainReaction = "chainreaction";
    public const string SurgeStrike = "surgestrike";
    public const string Supercell = "supercell";
    public const string LightningRod = "rod";
    public const string GroundCurrent = "groundcurrent";
    public const string EyeOfTheStorm = "eye";
    public const string LightningReflexes = "reflexes";
    public const string ThunderGod = "thundergod";
    public const string LivingCurrent = "livingcurrent";
    public const string CallLightning = "calllightning";

    private static TreeNode Minor(string id, string name, int tier, float x, string lane, int max, string[] parents, string text, params (string Stat, float PerRank)[] stats) =>
        new(id, name, tier, x, lane, max, text, parents, stats.ToDictionary(s => s.Stat, s => s.PerRank));

    private static TreeNode Major(string id, string name, int tier, float x, string lane, string[] parents, string text) =>
        new(id, name, tier, x, lane, 1, text, parents, new Dictionary<string, float>(), Major: true);

    private const string T = "Tempest", C = "Conduction", A = "Attunement";

    public static readonly TreeDefinition Tree = new(TreeId, "Lightning Alignment", new[]
    {
        // Tier 1 (level 1): the two starting choices.
        Minor("charged", "Charged Air", 1, 330, T, 5, Array.Empty<string>(), "+{Lightning damage}% lightning damage and +{Cast speed}% cast speed.", (LightningDamage, 10), (CastSpeed, 5)),
        Minor("livewire", "Live Wire", 1, 620, C, 5, Array.Empty<string>(), "+{Fork damage}% fork damage and +{Fork range}% fork range.", (ForkDamage, 10), (ForkRange, 8)),

        // Tier 2 (level 3)
        Minor("voltage", "High Voltage", 2, 120, T, 5, new[] { "charged" }, "+{Lightning damage}% lightning damage.", (LightningDamage, 12)),
        Minor("rebound", "Rebound", 2, 240, T, 2, new[] { "charged" }, "The ball bounces {Bounces} more times before it fades.", (Bounces, 1)),
        Minor("swiftcast", "Swift Cast", 2, 370, T, 5, new[] { "charged", "livewire" }, "+{Cast speed}% cast speed.", (CastSpeed, 8)),
        Minor("branching", "Branching", 2, 500, C, 2, new[] { "livewire" }, "Every fork reaches {Forks} more enemies.", (Forks, 1)),
        Major(Paralysis, "Paralysis", 2, 620, C, new[] { "livewire" }, "Enemies a fork strikes are paralysed for 0.5 s (half that for elites). Bosses shrug it off."),
        Minor("grounding", "Grounding", 2, 760, A, 5, new[] { "livewire" }, "+{Max health} max health and +{Health per second} health per second.", (MaxHealth, 10), (Regeneration, 0.2f)),
        Minor("stormstep", "Storm Step", 2, 880, A, 3, new[] { "livewire" }, "The surge recharges {Surge recharge}% faster and +{Move speed}% move speed.", (SurgeRecharge, 10), (MoveSpeed, 3)),

        // Tier 3 (level 6)
        Minor("heavycore", "Heavy Core", 3, 130, T, 3, new[] { "voltage" }, "+{Ball size}% ball size and +{Bounce zap damage}% bounce zap damage.", (BallSize, 15), (ZapDamage, 15)),
        Major(Thunderclap, "Thunderclap", 3, 260, T, new[] { "rebound", "voltage" }, "Every bounce's zap reaches 60% further and hits 50% harder."),
        Minor("longarc", "Long Arc", 3, 400, C, 3, new[] { "branching", "swiftcast" }, "+{Fork range}% fork range.", (ForkRange, 15)),
        Major(ChainReaction, "Chain Reaction", 3, 530, C, new[] { "branching" }, "Every fork leaps on once more, from the enemy it struck to one it hasn't."),
        Minor("capacitor", "Capacitor", 3, 650, C, 3, new[] { Paralysis }, "+{Fork damage}% fork damage.", (ForkDamage, 15)),
        Major(SurgeStrike, "Surge Strike", 3, 780, A, new[] { "grounding", "stormstep" }, "Every surge drops a ball of lightning at your feet, rolling the way you went."),
        Minor("insulated", "Insulated", 3, 900, A, 3, new[] { "grounding" }, "{Damage taken cut}% less damage taken.", (DamageTakenCut, 5)),

        // Tier 4 (level 10)
        Minor("overload", "Overload", 4, 120, T, 5, new[] { "heavycore" }, "+{Crit chance}% critical chance and +{Crit damage}% critical damage.", (CritChance, 4), (CritDamage, 10)),
        Major(Supercell, "Supercell", 4, 250, T, new[] { Thunderclap, "heavycore" }, "Every 5th cast lobs a supercell: twice the size, twice the damage, and three more bounces."),
        Major(LightningRod, "Lightning Rod", 4, 400, C, new[] { "longarc" }, "A tree or rock the ball glances off stays charged for 4 s, zapping every enemy within 4 m of it twice a second."),
        Minor("conductivity", "Conductivity", 4, 530, C, 2, new[] { ChainReaction, "longarc" }, "Every fork reaches {Forks} more enemies.", (Forks, 1)),
        Minor("staticskin", "Static Skin", 4, 780, A, 3, new[] { SurgeStrike }, "An enemy that strikes you takes {Shock when struck} lightning damage.", (ShockOnStruck, 15)),
        Minor("quickwind", "Quickening Winds", 4, 900, A, 3, new[] { "insulated", "stormstep" }, "+{Move speed}% move speed.", (MoveSpeed, 5)),

        // Tier 5 (level 15)
        Minor("fury", "Tempest's Fury", 5, 130, T, 3, new[] { Supercell, "overload" }, "+{Lightning damage}% lightning damage and +{Cast speed}% cast speed.", (LightningDamage, 15), (CastSpeed, 5)),
        Minor("persistence", "Persistence", 5, 260, T, 2, new[] { Supercell }, "Balls last {Ball lifetime} s longer and bounce {Bounces} more times.", (Lifetime, 1), (Bounces, 1)),
        Minor("arcweaver", "Arc Weaver", 5, 400, C, 3, new[] { LightningRod }, "Lightning rods zap {Lightning rod damage}% harder.", (RodDamage, 25)),
        Major(GroundCurrent, "Ground Current", 5, 530, C, new[] { "conductivity" }, "Every bounce also forks, from where the ball struck the ground."),
        Major(EyeOfTheStorm, "Eye of the Storm", 5, 700, A, new[] { "staticskin", "quickwind" }, "While you move, a crackling ring shocks every enemy within 3 m of you twice a second."),
        Minor("stormhide", "Storm Hide", 5, 860, A, 3, new[] { "quickwind" }, "+{Max health} max health and +{Health per second} health per second.", (MaxHealth, 15), (Regeneration, 0.3f)),

        // Tier 6 (level 21)
        Minor("megavolt", "Megavolt", 6, 150, T, 3, new[] { "fury", "persistence" }, "+{Lightning damage}% lightning damage and +{Damage to elites and bosses}% damage to elites and bosses.", (LightningDamage, 15), (EliteDamage, 10)),
        Minor("arcmaster", "Arc Master", 6, 420, C, 2, new[] { "arcweaver", GroundCurrent }, "Every fork reaches {Forks} more enemies, and +{Fork range}% fork range.", (Forks, 1), (ForkRange, 10)),
        Minor("stormsurge", "Stormsurge", 6, 560, C, 3, new[] { GroundCurrent }, "+{Fork damage}% fork damage.", (ForkDamage, 20)),
        Minor("galvanic", "Galvanic Recovery", 6, 720, A, 3, new[] { EyeOfTheStorm }, "Every enemy your lightning kills heals you {Heal on lightning kill} health.", (HealOnKill, 0.5f)),
        Major(LightningReflexes, "Lightning Reflexes", 6, 860, A, new[] { "stormhide" }, "A blow that lands on you recharges your surge at once (at most every 5 s)."),

        // Tier 7 (level 28): the capstones.
        Major(ThunderGod, "Wrath of the Thunder God", 7, 200, T, new[] { "megavolt" }, "A ball that fades bursts in a 5 m thunderclap for 300% damage."),
        Major(LivingCurrent, "Living Current", 7, 490, C, new[] { "arcmaster", "stormsurge" }, "Every enemy your lightning kills forks to two more."),
        Major(CallLightning, "Call Lightning", 7, 790, A, new[] { "galvanic", LightningReflexes }, "Every 8 s, lightning strikes the 5 nearest enemies within 15 m from the sky, for 400% damage."),
    }, new[] { 1, 3, 6, 10, 15, 21, 28 },
        new[] { (T, 190f), (C, 480f), (A, 800f) },
        new HashSet<string> { Bounces, Lifetime, Forks, MaxHealth, Regeneration, ShockOnStruck, HealOnKill });
}

/// <summary>What the ranks spent in Lightning Alignment add up to, in the terms <see cref="ShamanStats"/> uses (fractions for percentages).</summary>
internal sealed class AlignmentBonuses
{
    public float LightningDamage;
    public float CastSpeed;
    public int Bounces;
    public float BallSize;
    public float ZapDamage;
    public float Lifetime;
    public int Forks;
    public float ForkDamage;
    public float ForkRange;
    public float RodDamage;
    public float CritChance;
    public float CritDamage;
    public float MaxHealth;
    public float Regeneration;
    public float MoveSpeed;
    public float SurgeRecharge;
    public float ShockOnStruck;
    public float HealOnKill;
    public float EliteDamage;

    /// <summary>What damage taken is multiplied by: each rank of Insulated takes a share off what is left.</summary>
    public float DamageTaken = 1f;

    public bool Paralysis;
    public bool Thunderclap;
    public bool ChainReaction;
    public bool SurgeStrike;
    public bool Supercell;
    public bool LightningRod;
    public bool GroundCurrent;
    public bool EyeOfTheStorm;
    public bool LightningReflexes;
    public bool ThunderGod;
    public bool LivingCurrent;
    public bool CallLightning;

    public static AlignmentBonuses From(IReadOnlyDictionary<string, int> ranks)
    {
        var b = new AlignmentBonuses();
        foreach (var node in AlignmentTree.Tree.Nodes)
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
                    case AlignmentTree.LightningDamage: b.LightningDamage += v / 100f; break;
                    case AlignmentTree.CastSpeed: b.CastSpeed += v / 100f; break;
                    case AlignmentTree.Bounces: b.Bounces += (int)v; break;
                    case AlignmentTree.BallSize: b.BallSize += v / 100f; break;
                    case AlignmentTree.ZapDamage: b.ZapDamage += v / 100f; break;
                    case AlignmentTree.Lifetime: b.Lifetime += v; break;
                    case AlignmentTree.Forks: b.Forks += (int)v; break;
                    case AlignmentTree.ForkDamage: b.ForkDamage += v / 100f; break;
                    case AlignmentTree.ForkRange: b.ForkRange += v / 100f; break;
                    case AlignmentTree.RodDamage: b.RodDamage += v / 100f; break;
                    case AlignmentTree.CritChance: b.CritChance += v / 100f; break;
                    case AlignmentTree.CritDamage: b.CritDamage += v / 100f; break;
                    case AlignmentTree.MaxHealth: b.MaxHealth += v; break;
                    case AlignmentTree.Regeneration: b.Regeneration += v; break;
                    case AlignmentTree.MoveSpeed: b.MoveSpeed += v / 100f; break;
                    case AlignmentTree.SurgeRecharge: b.SurgeRecharge += v / 100f; break;
                    case AlignmentTree.ShockOnStruck: b.ShockOnStruck += v; break;
                    case AlignmentTree.HealOnKill: b.HealOnKill += v; break;
                    case AlignmentTree.EliteDamage: b.EliteDamage += v / 100f; break;
                    case AlignmentTree.DamageTakenCut: b.DamageTaken *= MathF.Pow(1f - perRank / 100f, r); break;
                }
            }
        }

        bool Has(string id) => ranks.GetValueOrDefault(id) > 0;
        b.Paralysis = Has(AlignmentTree.Paralysis);
        b.Thunderclap = Has(AlignmentTree.Thunderclap);
        b.ChainReaction = Has(AlignmentTree.ChainReaction);
        b.SurgeStrike = Has(AlignmentTree.SurgeStrike);
        b.Supercell = Has(AlignmentTree.Supercell);
        b.LightningRod = Has(AlignmentTree.LightningRod);
        b.GroundCurrent = Has(AlignmentTree.GroundCurrent);
        b.EyeOfTheStorm = Has(AlignmentTree.EyeOfTheStorm);
        b.LightningReflexes = Has(AlignmentTree.LightningReflexes);
        b.ThunderGod = Has(AlignmentTree.ThunderGod);
        b.LivingCurrent = Has(AlignmentTree.LivingCurrent);
        b.CallLightning = Has(AlignmentTree.CallLightning);
        return b;
    }
}
