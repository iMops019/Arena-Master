using ArenaMaster.Game.Progression;

namespace ArenaMaster.Game.Ranger;

/// <summary>
/// The Ranger's first passive tree. Three lanes - Precision (crits, big hits, elites), Volley (attack speed, more arrows, staying alive) and Trickshot (pierce,
/// chains, speed) - over seven tiers opening at tree levels 1, 3, 6, 10, 15, 21 and 28. Two starting nodes; nine majors.
/// </summary>
internal static class SharpshooterTree
{
    public const string ClassId = "ranger";
    public const string TreeId = "sharpshooter";

    // Stat names, used in node descriptions as {Name} and summed by SharpshooterBonuses.
    public const string Damage = "Damage";
    public const string AttackSpeed = "Attack speed";
    public const string CritChance = "Crit chance";
    public const string CritDamage = "Crit damage";
    public const string ArrowSpeed = "Arrow speed";
    public const string Range = "Range";
    public const string EliteDamage = "Damage to elites and bosses";
    public const string MaxHealth = "Max health";
    public const string Regeneration = "Health per second";
    public const string Pierce = "Pierce";
    public const string ChainedDamage = "Chained arrow damage";
    public const string DashRecharge = "Dash recharge";
    public const string MoveSpeed = "Move speed";
    public const string HealthyDamage = "Damage to healthy enemies";
    public const string MomentumPerKill = "Attack speed per recent kill";
    public const string ChainRange = "Chain range";
    public const string ExecuteChance = "Execute chance";
    public const string EliteCritHeal = "Heal on elite crit";
    public const string DamagePerExtraArrow = "Damage per extra arrow";
    public const string ExtraChains = "Extra chains";
    public const string DashDistance = "Dash distance";
    public const string DamageTakenCut = "Damage taken cut";
    public const string CascadeDamage = "Damage per chain";
    public const string RainArrows = "Rain arrows";

    // The majors, by id.
    public const string ChainProjectiles = "chain";
    public const string TwinShot = "twin";
    public const string Deadeye = "deadeye";
    public const string RainOfArrows = "rain";
    public const string SnipersFocus = "focus";
    public const string Fork = "fork";
    public const string OneShotOneKill = "oneshot";
    public const string EndlessQuiver = "endless";
    public const string StormOfSplinters = "splinters";

    private static TreeNode Minor(string id, string name, int tier, float x, string lane, int max, string[] parents, string text, params (string Stat, float PerRank)[] stats) =>
        new(id, name, tier, x, lane, max, text, parents, stats.ToDictionary(s => s.Stat, s => s.PerRank));

    private static TreeNode Major(string id, string name, int tier, float x, string lane, string[] parents, string text) =>
        new(id, name, tier, x, lane, 1, text, parents, new Dictionary<string, float>(), Major: true);

    private const string P = "Precision", V = "Volley", T = "Trickshot";

    public static readonly TreeDefinition Tree = new(TreeId, "Sharpshooter", new[]
    {
        // Tier 1 (level 1): the two starting choices.
        Minor("steady", "Steady Hands", 1, 360, P, 5, Array.Empty<string>(), "+{Crit chance}% critical chance and +{Crit damage}% critical damage.", (CritChance, 4), (CritDamage, 10)),
        Minor("honed", "Honed Draw", 1, 580, V, 5, Array.Empty<string>(), "+{Damage}% damage and +{Attack speed}% attack speed.", (Damage, 10), (AttackSpeed, 10)),

        // Tier 2 (level 3)
        Minor("keen", "Keen Eye", 2, 150, P, 5, new[] { "steady" }, "+{Crit chance}% critical chance.", (CritChance, 3)),
        Minor("weak", "Weak Spots", 2, 270, P, 5, new[] { "steady" }, "+{Crit damage}% critical damage.", (CritDamage, 15)),
        Minor("nock", "Rapid Nock", 2, 400, V, 5, new[] { "steady", "honed" }, "+{Attack speed}% attack speed.", (AttackSpeed, 6)),
        Minor("barbed", "Barbed Arrows", 2, 530, T, 5, new[] { "honed" }, "+{Damage}% damage.", (Damage, 8)),
        Major(ChainProjectiles, "Chain Projectiles", 2, 660, T, new[] { "honed" }, "Arrows chain +1 time: when an arrow would stop in an enemy, it jumps to the nearest other enemy within 10 m."),
        Minor("fletcher", "Fletcher's Craft", 2, 790, T, 3, new[] { "honed" }, "+{Arrow speed}% arrow speed and +{Range}% range.", (ArrowSpeed, 10), (Range, 8)),

        // Tier 3 (level 6)
        Minor("mark", "Hunter's Mark", 3, 200, P, 5, new[] { "keen", "weak" }, "+{Damage to elites and bosses}% damage to elites and bosses.", (EliteDamage, 10)),
        Minor("survivalist", "Survivalist", 3, 340, V, 5, new[] { "nock" }, "+{Max health} max health and +{Health per second} health per second.", (MaxHealth, 10), (Regeneration, 0.2f)),
        Major(TwinShot, "Twin Shot", 3, 470, V, new[] { "nock" }, "Every shot fires one extra arrow. All arrows deal 15% less damage."),
        Minor("penetrate", "Penetrating Shot", 3, 600, T, 2, new[] { "barbed", "fletcher" }, "Arrows pierce {Pierce} more enemies before they stop.", (Pierce, 1)),
        Minor("ricochet", "Ricochet", 3, 720, T, 3, new[] { ChainProjectiles }, "Chained arrows deal +{Chained arrow damage}% damage.", (ChainedDamage, 15)),
        Minor("recovery", "Quick Recovery", 3, 840, T, 3, new[] { "fletcher" }, "Dash recharges {Dash recharge}% faster. +{Move speed}% move speed.", (DashRecharge, 8), (MoveSpeed, 4)),

        // Tier 4 (level 10)
        Minor("patient", "Patient Hunter", 4, 140, P, 3, new[] { "mark" }, "+{Damage to healthy enemies}% damage to enemies above 80% health.", (HealthyDamage, 20)),
        Major(Deadeye, "Deadeye", 4, 280, P, new[] { "mark" }, "Critical hits deal triple damage instead of double. -5% critical chance."),
        Minor("weight", "Draw Weight", 4, 420, V, 3, new[] { "survivalist", TwinShot }, "+{Damage}% damage and {Attack speed}% attack speed. A heavier draw, harder hits.", (Damage, 12), (AttackSpeed, -4)),
        Minor("momentum", "Momentum", 4, 550, V, 3, new[] { TwinShot }, "Each kill gives +{Attack speed per recent kill}% attack speed for 3 s, stacking up to 15 times.", (MomentumPerKill, 1)),
        Minor("seeker", "Seeker Fletching", 4, 700, T, 3, new[] { "ricochet", "penetrate" }, "Chained arrows find a new target up to {Chain range}% further away.", (ChainRange, 25)),

        // Tier 5 (level 15)
        Minor("executioner", "Executioner", 5, 160, P, 3, new[] { "patient", Deadeye }, "{Execute chance}% chance for a hit to finish off a non-boss enemy left below 20% health.", (ExecuteChance, 3)),
        Minor("headhunter", "Headhunter", 5, 300, P, 3, new[] { Deadeye }, "Critical hits on elites and bosses heal {Heal on elite crit} health.", (EliteCritHeal, 2)),
        Major(RainOfArrows, "Rain of Arrows", 5, 440, V, new[] { "weight", "momentum" }, "Every 6 s, 12 arrows rain down on the ground under the crosshair."),
        Minor("quiver", "Quiver Mastery", 5, 575, V, 3, new[] { "momentum" }, "+{Damage per extra arrow}% damage for each extra arrow you fire per shot.", (DamagePerExtraArrow, 5)),
        Minor("reach", "Chain Reach", 5, 700, T, 2, new[] { "seeker" }, "Arrows chain {Extra chains} extra times.", (ExtraChains, 1)),
        Minor("windrunner", "Wind Runner", 5, 840, T, 3, new[] { "recovery", "seeker" }, "+{Move speed}% move speed and +{Dash distance}% dash distance.", (MoveSpeed, 5), (DashDistance, 10)),

        // Tier 6 (level 21)
        Minor("lethal", "Lethal Precision", 6, 160, P, 3, new[] { "executioner", "headhunter" }, "+{Crit chance}% critical chance.", (CritChance, 5)),
        Major(SnipersFocus, "Sniper's Focus", 6, 300, P, new[] { "headhunter" }, "Stand still for 1 s and your next arrow deals +150% damage and pierces every enemy in its path."),
        Minor("storm", "Arrowstorm", 6, 440, V, 3, new[] { RainOfArrows }, "Rain of Arrows drops {Rain arrows} more arrows.", (RainArrows, 4)),
        Minor("hardened", "Hardened Leathers", 6, 575, V, 3, new[] { "quiver" }, "+{Max health} max health and {Damage taken cut}% less damage taken.", (MaxHealth, 15), (DamageTakenCut, 5)),
        Minor("cascade", "Cascade", 6, 700, T, 3, new[] { "reach" }, "Each chain after the first deals +{Damage per chain}% more damage than the last.", (CascadeDamage, 10)),
        Major(Fork, "Fork", 6, 840, T, new[] { "reach", "windrunner" }, "The first enemy an arrow hits splits it into two arrows, 20 degrees apart."),

        // Tier 7 (level 28): the capstones.
        Major(OneShotOneKill, "One Shot, One Kill", 7, 230, P, new[] { "lethal", SnipersFocus }, "The first arrow to hit each enemy is always a critical hit."),
        Major(EndlessQuiver, "Endless Quiver", 7, 505, V, new[] { "storm", "hardened" }, "Every 10th shot fires a full ring of 16 arrows around you."),
        Major(StormOfSplinters, "Storm of Splinters", 7, 770, T, new[] { "cascade", Fork }, "An arrow that kills bursts into 4 splinters, each dealing 50% of its damage."),
    }, new[] { 1, 3, 6, 10, 15, 21, 28 },
        new[] { (P, 220f), (V, 480f), (T, 770f) },
        new HashSet<string> { MaxHealth, Regeneration, Pierce, ExtraChains, EliteCritHeal, RainArrows });
}

/// <summary>What the ranks spent in the Sharpshooter tree add up to, in the terms <see cref="RangerStats"/> and the arrows use (fractions for percentages).</summary>
internal sealed class SharpshooterBonuses
{
    public float Damage;
    public float AttackSpeed;
    public float CritChance;
    public float CritDamage;
    public float ArrowSpeed;
    public float Range;
    public float EliteDamage;
    public float MaxHealth;
    public float Regeneration;
    public int Pierce;
    public float ChainedDamage;
    public float DashRecharge;
    public float MoveSpeed;
    public float HealthyDamage;
    public float MomentumPerKill;
    public float ChainRange;
    public float ExecuteChance;
    public float EliteCritHeal;
    public float DamagePerExtraArrow;
    public int ExtraChains;
    public float DashDistance;
    public float CascadeDamage;

    /// <summary>Extra arrows in each Rain of Arrows (Arrowstorm).</summary>
    public int RainArrows;

    /// <summary>What damage taken is multiplied by: each rank of Hardened Leathers takes a share off what is left.</summary>
    public float DamageTaken = 1f;

    public bool ChainProjectiles;
    public bool TwinShot;
    public bool Deadeye;
    public bool RainOfArrows;
    public bool SnipersFocus;
    public bool Fork;
    public bool OneShotOneKill;
    public bool EndlessQuiver;
    public bool StormOfSplinters;

    public static SharpshooterBonuses From(IReadOnlyDictionary<string, int> ranks)
    {
        var b = new SharpshooterBonuses();
        foreach (var node in SharpshooterTree.Tree.Nodes)
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
                    case SharpshooterTree.Damage: b.Damage += v / 100f; break;
                    case SharpshooterTree.AttackSpeed: b.AttackSpeed += v / 100f; break;
                    case SharpshooterTree.CritChance: b.CritChance += v / 100f; break;
                    case SharpshooterTree.CritDamage: b.CritDamage += v / 100f; break;
                    case SharpshooterTree.ArrowSpeed: b.ArrowSpeed += v / 100f; break;
                    case SharpshooterTree.Range: b.Range += v / 100f; break;
                    case SharpshooterTree.EliteDamage: b.EliteDamage += v / 100f; break;
                    case SharpshooterTree.MaxHealth: b.MaxHealth += v; break;
                    case SharpshooterTree.Regeneration: b.Regeneration += v; break;
                    case SharpshooterTree.Pierce: b.Pierce += (int)v; break;
                    case SharpshooterTree.ChainedDamage: b.ChainedDamage += v / 100f; break;
                    case SharpshooterTree.DashRecharge: b.DashRecharge += v / 100f; break;
                    case SharpshooterTree.MoveSpeed: b.MoveSpeed += v / 100f; break;
                    case SharpshooterTree.HealthyDamage: b.HealthyDamage += v / 100f; break;
                    case SharpshooterTree.MomentumPerKill: b.MomentumPerKill += v / 100f; break;
                    case SharpshooterTree.ChainRange: b.ChainRange += v / 100f; break;
                    case SharpshooterTree.ExecuteChance: b.ExecuteChance += v / 100f; break;
                    case SharpshooterTree.EliteCritHeal: b.EliteCritHeal += v; break;
                    case SharpshooterTree.DamagePerExtraArrow: b.DamagePerExtraArrow += v / 100f; break;
                    case SharpshooterTree.ExtraChains: b.ExtraChains += (int)v; break;
                    case SharpshooterTree.DashDistance: b.DashDistance += v / 100f; break;
                    case SharpshooterTree.CascadeDamage: b.CascadeDamage += v / 100f; break;
                    case SharpshooterTree.RainArrows: b.RainArrows += (int)v; break;
                    case SharpshooterTree.DamageTakenCut: b.DamageTaken *= MathF.Pow(1f - perRank / 100f, r); break;
                }
            }
        }

        b.ChainProjectiles = ranks.GetValueOrDefault(SharpshooterTree.ChainProjectiles) > 0;
        b.TwinShot = ranks.GetValueOrDefault(SharpshooterTree.TwinShot) > 0;
        b.Deadeye = ranks.GetValueOrDefault(SharpshooterTree.Deadeye) > 0;
        b.RainOfArrows = ranks.GetValueOrDefault(SharpshooterTree.RainOfArrows) > 0;
        b.SnipersFocus = ranks.GetValueOrDefault(SharpshooterTree.SnipersFocus) > 0;
        b.Fork = ranks.GetValueOrDefault(SharpshooterTree.Fork) > 0;
        b.OneShotOneKill = ranks.GetValueOrDefault(SharpshooterTree.OneShotOneKill) > 0;
        b.EndlessQuiver = ranks.GetValueOrDefault(SharpshooterTree.EndlessQuiver) > 0;
        b.StormOfSplinters = ranks.GetValueOrDefault(SharpshooterTree.StormOfSplinters) > 0;
        return b;
    }
}
