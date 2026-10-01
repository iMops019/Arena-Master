using ArenaMaster.Game.Progression;

namespace ArenaMaster.Game.Mage;

/// <summary>
/// The Mage's second passive tree: with it active the barrage burns instead of chilling (the Fire Barrage). Three lanes - Ignite (burns: their damage, how long they
/// last, spreading), Combustion (explosions: burning enemies bursting, bolts bursting, the Meteor) and Heat (a gauge that builds with every cast, blinking, and staying
/// alive) - over seven tiers opening at tree levels 1, 3, 6, 10, 15, 21 and 28. Two starting nodes; eleven majors, a capstone per lane.
/// </summary>
internal static class PyromancyTree
{
    public const string TreeId = "pyromancy";

    // Stat names, used in node descriptions as {Name} and summed by PyromancyBonuses.
    public const string FireDamage = "Fire damage";
    public const string BurnDamage = "Burn damage";
    public const string BurnDuration = "Burn duration";
    public const string CastSpeed = "Cast speed";
    public const string Projectiles = "Projectiles";
    public const string ProjectileSpeed = "Projectile speed";
    public const string Range = "Range";
    public const string Pierce = "Pierce";
    public const string CritChance = "Crit chance";
    public const string CritDamage = "Crit damage";
    public const string EliteDamage = "Damage to elites and bosses";
    public const string CombustionDamage = "Combustion damage";
    public const string CombustionRadius = "Combustion radius";
    public const string FireballRadius = "Fireball radius";
    public const string FireballDamage = "Fireball damage";
    public const string HeatDamage = "Damage per heat";
    public const string HeatCooling = "Heat cooling";
    public const string MaxHealth = "Max health";
    public const string Regeneration = "Health per second";
    public const string DamageTakenCut = "Damage taken cut";

    // The majors, by id.
    public const string Heat = "heat";
    public const string Combustion = "combustion";
    public const string Wildfire = "wildfire";
    public const string FireWalk = "firewalk";
    public const string Fireball = "fireball";
    public const string FlameWard = "flameward";
    public const string Scorch = "scorch";
    public const string Cauterise = "cauterise";
    public const string LivingFlame = "livingflame";
    public const string Meteor = "meteor";
    public const string Inferno = "inferno";

    private static TreeNode Minor(string id, string name, int tier, float x, string lane, int max, string[] parents, string text, params (string Stat, float PerRank)[] stats) =>
        new(id, name, tier, x, lane, max, text, parents, stats.ToDictionary(s => s.Stat, s => s.PerRank));

    private static TreeNode Major(string id, string name, int tier, float x, string lane, string[] parents, string text) =>
        new(id, name, tier, x, lane, 1, text, parents, new Dictionary<string, float>(), Major: true);

    private const string I = "Ignite", C = "Combustion", H = "Heat";

    public static readonly TreeDefinition Tree = new(TreeId, "Pyromancy", new[]
    {
        // Tier 1 (level 1): the two starting choices.
        Minor("kindling", "Kindling", 1, 300, I, 5, Array.Empty<string>(), "+{Burn damage}% burn damage, and burns last {Burn duration} s longer.", (BurnDamage, 10), (BurnDuration, 0.2f)),
        Minor("quickflame", "Quick Flame", 1, 600, C, 5, Array.Empty<string>(), "+{Cast speed}% cast speed and +{Projectile speed}% projectile speed.", (CastSpeed, 8), (ProjectileSpeed, 8)),

        // Tier 2 (level 3)
        Minor("scald", "Scald", 2, 120, I, 5, new[] { "kindling" }, "+{Fire damage}% fire damage.", (FireDamage, 12)),
        Minor("embers", "Hot Embers", 2, 240, I, 4, new[] { "kindling" }, "+{Burn damage}% burn damage.", (BurnDamage, 15)),
        Major(Combustion, "Combustion", 2, 390, C, new[] { "kindling", "quickflame" }, "A burning enemy that dies bursts: 100% of a bolt's damage to every enemy within 2.5 m, setting them burning."),
        Minor("twinflame", "Twin Flame", 2, 510, C, 2, new[] { "quickflame" }, "Fire Barrage fires {Projectiles} more projectiles.", (Projectiles, 1)),
        Major(Heat, "Heat", 2, 680, H, new[] { "quickflame" }, "Every cast builds 10 heat (up to 100), and heat cools 5 a second. Each point of heat is +0.5% damage. At 100 you overheat: no casting for 1.5 s, then the heat is gone. Blinking vents it all at once, with no overheat."),
        Minor("warmblood", "Warm Blood", 2, 820, H, 5, new[] { "quickflame" }, "+{Max health} max health.", (MaxHealth, 10)),

        // Tier 3 (level 6)
        Minor("tinder", "Dry Tinder", 3, 130, I, 2, new[] { "scald", "embers" }, "Burns last {Burn duration} s longer.", (BurnDuration, 0.5f)),
        Major(Wildfire, "Wildfire", 3, 260, I, new[] { "embers" }, "Once a second, every burn spreads to the nearest enemy within 4 m that isn't burning."),
        Minor("volatile", "Volatile", 3, 400, C, 3, new[] { Combustion }, "+{Combustion damage}% Combustion damage and +{Combustion radius}% Combustion radius.", (CombustionDamage, 20), (CombustionRadius, 15)),
        Minor("fleetflame", "Fleet Flame", 3, 530, C, 3, new[] { "twinflame" }, "+{Projectile speed}% projectile speed and +{Range}% range.", (ProjectileSpeed, 15), (Range, 10)),
        Major(FireWalk, "Fire Walk", 3, 650, H, new[] { Heat }, "Blinking leaves a line of fire along the way you went. It burns for 3 s: anything standing in it takes a bolt's damage a second and is set burning."),
        Minor("stoked", "Stoked", 3, 760, H, 2, new[] { Heat }, "+{Damage per heat}% more damage for every point of heat.", (HeatDamage, 0.1f)),
        Minor("hearth", "Hearth", 3, 870, H, 5, new[] { "warmblood" }, "+{Health per second} health per second.", (Regeneration, 0.3f)),

        // Tier 4 (level 10)
        Minor("keen", "Keen Flame", 4, 140, I, 5, new[] { "tinder", Wildfire }, "+{Crit chance}% increased critical chance and +{Crit damage}% critical damage.", (CritChance, 10), (CritDamage, 10)),
        Minor("fanned", "Fanned Flames", 4, 270, I, 3, new[] { Wildfire }, "+{Burn damage}% burn damage and +{Fire damage}% fire damage.", (BurnDamage, 12), (FireDamage, 6)),
        Major(Fireball, "Fireball", 4, 400, C, new[] { "volatile", "fleetflame" }, "A Fire Barrage bolt that hits an enemy bursts: 40% of its damage to every other enemy within 2 m, setting them burning."),
        Minor("flamevolley", "Volley of Flame", 4, 530, C, 2, new[] { "fleetflame" }, "Fire Barrage fires {Projectiles} more projectiles.", (Projectiles, 1)),
        Major(FlameWard, "Flame Ward", 4, 660, H, new[] { FireWalk, "hearth" }, "Every 10 s, the next blow that reaches you is burned away: you take no damage, and whoever struck it is set burning."),
        Minor("coolhead", "Cool Head", 4, 790, H, 3, new[] { "stoked" }, "Heat cools {Heat cooling} a second faster.", (HeatCooling, 1)),

        // Tier 5 (level 15)
        Minor("pyre", "Pyre", 5, 120, I, 5, new[] { "keen" }, "+{Damage to elites and bosses}% damage to elites and bosses.", (EliteDamage, 15)),
        Major(Scorch, "Scorch", 5, 250, I, new[] { "keen", "fanned" }, "Your bolts do 30% more damage to burning enemies."),
        Minor("piercingflame", "Piercing Flame", 5, 390, C, 2, new[] { Fireball, "flamevolley" }, "Bolts pierce {Pierce} more enemies before they stop.", (Pierce, 1)),
        Minor("greatfire", "Great Fire", 5, 510, C, 3, new[] { Fireball }, "+{Fireball radius}% Fireball radius and +{Fireball damage}% Fireball damage.", (FireballRadius, 20), (FireballDamage, 15)),
        Minor("ashenskin", "Ashen Skin", 5, 660, H, 3, new[] { FlameWard }, "+{Max health} max health and {Damage taken cut}% less damage taken.", (MaxHealth, 15), (DamageTakenCut, 4)),
        Minor("blazingmind", "Blazing Mind", 5, 790, H, 3, new[] { "coolhead" }, "+{Cast speed}% cast speed and +{Fire damage}% fire damage.", (CastSpeed, 8), (FireDamage, 8)),

        // Tier 6 (level 21)
        Minor("consuming", "Consuming Fire", 6, 150, I, 3, new[] { "pyre", Scorch }, "+{Burn damage}% burn damage and +{Fire damage}% fire damage.", (BurnDamage, 15), (FireDamage, 10)),
        Minor("everburning", "Everburning", 6, 280, I, 2, new[] { Scorch }, "Burns last {Burn duration} s longer.", (BurnDuration, 0.5f)),
        Minor("flashpoint", "Flashpoint", 6, 450, C, 3, new[] { "piercingflame", "greatfire" }, "+{Combustion damage}% Combustion damage and +{Cast speed}% cast speed.", (CombustionDamage, 20), (CastSpeed, 8)),
        Minor("emberheart", "Ember Heart", 6, 660, H, 3, new[] { "ashenskin" }, "+{Health per second} health per second and +{Max health} max health.", (Regeneration, 0.5f), (MaxHealth, 10)),
        Major(Cauterise, "Cauterise", 6, 800, H, new[] { "blazingmind" }, "Venting your heat by blinking heals you: 1 health for every 4 heat it lets out."),

        // Tier 7 (level 28): the capstones.
        Major(LivingFlame, "Living Flame", 7, 200, I, new[] { "consuming", "everburning" }, "A burn grows the longer it lasts: every tick does 25% more for each second it has burned (up to 200% more). A new burn on a burning enemy keeps the old one's age."),
        Major(Meteor, "Meteor", 7, 470, C, new[] { "flashpoint" }, "Every 4th Fire Barrage calls a meteor down on the thickest crowd in range: 500% of a bolt's damage within 4 m, leaving the ground burning for 3 s."),
        Major(Inferno, "Inferno", 7, 760, H, new[] { Cauterise, "blazingmind" }, "Overheating wraps you in a firestorm instead of stopping your casts: for its 1.5 s you keep casting, and everything within 5 m takes a bolt's damage twice a second."),
    }, new[] { 1, 3, 6, 10, 15, 21, 28 },
        new[] { (I, 200f), (C, 460f), (H, 760f) },
        new HashSet<string> { BurnDuration, Projectiles, Pierce, HeatCooling, MaxHealth, Regeneration });
}

/// <summary>What the ranks spent in the Pyromancy tree add up to, in the terms <see cref="MageStats"/> uses (fractions for percentages).</summary>
internal sealed class PyromancyBonuses
{
    public float FireDamage;
    public float BurnDamage;
    public float BurnDuration;
    public float CastSpeed;
    public int Projectiles;
    public float ProjectileSpeed;
    public float Range;
    public int Pierce;
    public float CritChance;
    public float CritDamage;
    public float EliteDamage;
    public float CombustionDamage;
    public float CombustionRadius;
    public float FireballRadius;
    public float FireballDamage;

    /// <summary>Extra damage for each point of heat (a fraction: 0.001 is +0.1% a point).</summary>
    public float HeatDamage;

    /// <summary>Extra heat lost each second.</summary>
    public float HeatCooling;

    public float MaxHealth;
    public float Regeneration;

    /// <summary>What damage taken is multiplied by: each rank of Ashen Skin takes a share off what is left.</summary>
    public float DamageTaken = 1f;

    public bool Heat;
    public bool Combustion;
    public bool Wildfire;
    public bool FireWalk;
    public bool Fireball;
    public bool FlameWard;
    public bool Scorch;
    public bool Cauterise;
    public bool LivingFlame;
    public bool Meteor;
    public bool Inferno;

    public static PyromancyBonuses From(IReadOnlyDictionary<string, int> ranks)
    {
        var b = new PyromancyBonuses();
        foreach (var node in PyromancyTree.Tree.Nodes)
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
                    case PyromancyTree.FireDamage: b.FireDamage += v / 100f; break;
                    case PyromancyTree.BurnDamage: b.BurnDamage += v / 100f; break;
                    case PyromancyTree.BurnDuration: b.BurnDuration += v; break;
                    case PyromancyTree.CastSpeed: b.CastSpeed += v / 100f; break;
                    case PyromancyTree.Projectiles: b.Projectiles += (int)v; break;
                    case PyromancyTree.ProjectileSpeed: b.ProjectileSpeed += v / 100f; break;
                    case PyromancyTree.Range: b.Range += v / 100f; break;
                    case PyromancyTree.Pierce: b.Pierce += (int)v; break;
                    case PyromancyTree.CritChance: b.CritChance += v / 100f; break;
                    case PyromancyTree.CritDamage: b.CritDamage += v / 100f; break;
                    case PyromancyTree.EliteDamage: b.EliteDamage += v / 100f; break;
                    case PyromancyTree.CombustionDamage: b.CombustionDamage += v / 100f; break;
                    case PyromancyTree.CombustionRadius: b.CombustionRadius += v / 100f; break;
                    case PyromancyTree.FireballRadius: b.FireballRadius += v / 100f; break;
                    case PyromancyTree.FireballDamage: b.FireballDamage += v / 100f; break;
                    case PyromancyTree.HeatDamage: b.HeatDamage += v / 100f; break;
                    case PyromancyTree.HeatCooling: b.HeatCooling += v; break;
                    case PyromancyTree.MaxHealth: b.MaxHealth += v; break;
                    case PyromancyTree.Regeneration: b.Regeneration += v; break;
                    case PyromancyTree.DamageTakenCut: b.DamageTaken *= MathF.Pow(1f - perRank / 100f, r); break;
                }
            }
        }

        bool Has(string id) => ranks.GetValueOrDefault(id) > 0;
        b.Heat = Has(PyromancyTree.Heat);
        b.Combustion = Has(PyromancyTree.Combustion);
        b.Wildfire = Has(PyromancyTree.Wildfire);
        b.FireWalk = Has(PyromancyTree.FireWalk);
        b.Fireball = Has(PyromancyTree.Fireball);
        b.FlameWard = Has(PyromancyTree.FlameWard);
        b.Scorch = Has(PyromancyTree.Scorch);
        b.Cauterise = Has(PyromancyTree.Cauterise);
        b.LivingFlame = Has(PyromancyTree.LivingFlame);
        b.Meteor = Has(PyromancyTree.Meteor);
        b.Inferno = Has(PyromancyTree.Inferno);
        return b;
    }
}
