using ArenaMaster.Game.Progression;

namespace ArenaMaster.Game.Mage;

/// <summary>
/// The Mage's first passive tree. Three lanes - Winter (cold damage, chill, freezing), Barrage (the Frost Barrage's bolts: more of them, faster, piercing,
/// exploding) and Ward (the Frost Shield and staying alive) - over seven tiers opening at tree levels 1, 3, 6, 10, 15, 21 and 28. Two starting nodes; ten majors,
/// a capstone per lane.
/// </summary>
internal static class FrostTree
{
    public const string ClassId = "mage";
    public const string TreeId = "frost";

    // Stat names, used in node descriptions as {Name} and summed by FrostBonuses.
    public const string ColdDamage = "Cold damage";
    public const string CastSpeed = "Cast speed";
    public const string Projectiles = "Projectiles";
    public const string ProjectileSpeed = "Projectile speed";
    public const string Range = "Range";
    public const string Chill = "Chill";
    public const string ChillDuration = "Chill duration";
    public const string ChilledDamage = "Damage to chilled enemies";
    public const string CritChance = "Crit chance";
    public const string CritDamage = "Crit damage";
    public const string BlastRadius = "Frost Blast radius";
    public const string BlastDamage = "Frost Blast damage";
    public const string Pierce = "Pierce";
    public const string FreezeChance = "Freeze chance";
    public const string FreezeDuration = "Freeze duration";
    public const string EliteDamage = "Damage to elites and bosses";
    public const string MaxHealth = "Max health";
    public const string Regeneration = "Health per second";
    public const string DamageTakenCut = "Damage taken cut";
    public const string ShieldStrength = "Frost Shield strength";
    public const string ShieldRecharge = "Frost Shield recharge";
    public const string ShieldDuration = "Frost Shield duration";

    // The majors, by id.
    public const string FrostShield = "frostshield";
    public const string FrostBlast = "frostblast";
    public const string DeepFreeze = "deepfreeze";
    public const string ShatteringWard = "shatteringward";
    public const string Shatter = "shatter";
    public const string SplittingIce = "splitting";
    public const string IceBlock = "iceblock";
    public const string Blizzard = "blizzard";
    public const string Comet = "comet";
    public const string GlacialFortress = "fortress";

    private static TreeNode Minor(string id, string name, int tier, float x, string lane, int max, string[] parents, string text, params (string Stat, float PerRank)[] stats) =>
        new(id, name, tier, x, lane, max, text, parents, stats.ToDictionary(s => s.Stat, s => s.PerRank));

    private static TreeNode Major(string id, string name, int tier, float x, string lane, string[] parents, string text) =>
        new(id, name, tier, x, lane, 1, text, parents, new Dictionary<string, float>(), Major: true);

    private const string W = "Winter", B = "Barrage", D = "Ward";

    public static readonly TreeDefinition Tree = new(TreeId, "Frost", new[]
    {
        // Tier 1 (level 1): the two starting choices.
        Minor("coldhands", "Cold Hands", 1, 300, W, 5, Array.Empty<string>(), "+{Cold damage}% cold damage and +{Chill}% chill.", (ColdDamage, 10), (Chill, 3)),
        Minor("quickmind", "Quickened Mind", 1, 600, B, 5, Array.Empty<string>(), "+{Cast speed}% cast speed and +{Projectile speed}% projectile speed.", (CastSpeed, 8), (ProjectileSpeed, 8)),

        // Tier 2 (level 3)
        Minor("bitter", "Bitter Cold", 2, 120, W, 5, new[] { "coldhands" }, "+{Cold damage}% cold damage.", (ColdDamage, 12)),
        Minor("numbing", "Numbing Frost", 2, 240, W, 4, new[] { "coldhands" }, "+{Chill}% chill: chilled enemies walk that much slower.", (Chill, 5)),
        Minor("quickcast", "Quick Casting", 2, 390, B, 5, new[] { "coldhands", "quickmind" }, "+{Cast speed}% cast speed.", (CastSpeed, 8)),
        Minor("extrashard", "Extra Shard", 2, 510, B, 2, new[] { "quickmind" }, "Frost Barrage fires {Projectiles} more projectiles.", (Projectiles, 1)),
        Major(FrostShield, "Frost Shield", 2, 680, D, new[] { "quickmind" }, "Every 10 s a shield of ice forms around you on its own: for 4 s it takes damage for you, 25 plus 20% of your max health of it."),
        Minor("hardy", "Hardy", 2, 820, D, 5, new[] { "quickmind" }, "+{Max health} max health.", (MaxHealth, 10)),

        // Tier 3 (level 6)
        Minor("brittle", "Brittle", 3, 130, W, 3, new[] { "bitter" }, "+{Damage to chilled enemies}% damage to chilled enemies.", (ChilledDamage, 15)),
        Minor("lingering", "Lingering Chill", 3, 260, W, 2, new[] { "numbing" }, "Chill lasts {Chill duration} s longer.", (ChillDuration, 0.5f)),
        Major(FrostBlast, "Frost Blast", 3, 400, B, new[] { "quickcast", "extrashard" }, "A Frost Barrage bolt that hits an enemy explodes: 50% of its damage to every other enemy within 2 m, chilling them."),
        Minor("swift", "Swift Shards", 3, 530, B, 3, new[] { "extrashard" }, "+{Projectile speed}% projectile speed and +{Range}% range.", (ProjectileSpeed, 15), (Range, 10)),
        Minor("thickice", "Thick Ice", 3, 680, D, 3, new[] { FrostShield }, "+{Frost Shield strength}% Frost Shield strength.", (ShieldStrength, 20)),
        Minor("hoarfrost", "Hoarfrost", 3, 820, D, 5, new[] { "hardy" }, "+{Health per second} health per second.", (Regeneration, 0.3f)),

        // Tier 4 (level 10)
        Major(DeepFreeze, "Deep Freeze", 4, 140, W, new[] { "brittle", "lingering" }, "Frost hits on chilled enemies have a 10% chance to freeze them solid for 1.2 s (half as long for elites). Bosses can't be frozen."),
        Minor("keen", "Keen Frost", 4, 270, W, 5, new[] { "lingering" }, "+{Crit chance}% critical chance and +{Crit damage}% critical damage.", (CritChance, 4), (CritDamage, 10)),
        Minor("blastwave", "Blast Wave", 4, 400, B, 3, new[] { FrostBlast }, "+{Frost Blast radius}% Frost Blast radius and +{Frost Blast damage}% Frost Blast damage.", (BlastRadius, 20), (BlastDamage, 15)),
        Minor("volley", "Volley of Ice", 4, 530, B, 2, new[] { "swift", FrostBlast }, "Frost Barrage fires {Projectiles} more projectiles.", (Projectiles, 1)),
        Major(ShatteringWard, "Shattering Ward", 4, 680, D, new[] { "thickice" }, "When your Frost Shield breaks, it bursts: 300% of a bolt's damage to every enemy within 4 m, chilling them."),
        Minor("reforming", "Rapid Reforming", 4, 820, D, 3, new[] { "thickice", "hoarfrost" }, "The Frost Shield forms {Frost Shield recharge}% faster.", (ShieldRecharge, 10)),

        // Tier 5 (level 15)
        Minor("permafrost", "Permafrost", 5, 110, W, 3, new[] { DeepFreeze }, "+{Freeze chance}% freeze chance and freezes last {Freeze duration} s longer.", (FreezeChance, 3), (FreezeDuration, 0.2f)),
        Major(Shatter, "Shatter", 5, 240, W, new[] { DeepFreeze, "keen" }, "Frozen enemies take 60% more damage from your frost."),
        Minor("piercing", "Piercing Ice", 5, 390, B, 2, new[] { "blastwave", "volley" }, "Bolts pierce {Pierce} more enemies before they stop.", (Pierce, 1)),
        Major(SplittingIce, "Splitting Ice", 5, 520, B, new[] { "volley" }, "A bolt that kills splits into two smaller bolts, at 50% damage, that seek out the nearest other enemies."),
        Minor("bulwark", "Glacial Bulwark", 5, 680, D, 3, new[] { ShatteringWard, "reforming" }, "+{Max health} max health and {Damage taken cut}% less damage taken.", (MaxHealth, 15), (DamageTakenCut, 4)),
        Minor("lastingward", "Lasting Ward", 5, 820, D, 2, new[] { "reforming" }, "The Frost Shield lasts {Frost Shield duration} s longer.", (ShieldDuration, 1)),

        // Tier 6 (level 21)
        Minor("wintersbite", "Winter's Bite", 6, 130, W, 3, new[] { "permafrost", Shatter }, "+{Cold damage}% cold damage and +{Chill}% chill.", (ColdDamage, 15), (Chill, 5)),
        Minor("frozenhunter", "Frozen Hunter", 6, 260, W, 5, new[] { Shatter }, "+{Damage to elites and bosses}% damage to elites and bosses.", (EliteDamage, 15)),
        Minor("stormcaller", "Storm Caller", 6, 450, B, 3, new[] { "piercing", SplittingIce }, "+{Cast speed}% cast speed and +{Cold damage}% cold damage.", (CastSpeed, 10), (ColdDamage, 10)),
        Major(IceBlock, "Ice Block", 6, 680, D, new[] { "bulwark", "lastingward" }, "Once per run, a blow that would kill you leaves you on 1 health, encased in ice: you heal 25% and a shield of half your max health forms."),
        Minor("frostblooded", "Frost Blooded", 6, 820, D, 3, new[] { "lastingward" }, "+{Health per second} health per second and +{Max health} max health.", (Regeneration, 0.5f), (MaxHealth, 10)),

        // Tier 7 (level 28): the capstones.
        Major(Blizzard, "Blizzard", 7, 200, W, new[] { "wintersbite", "frozenhunter" }, "A blizzard swirls around you: every enemy within 5 m takes a bolt's damage each second and is chilled."),
        Major(Comet, "Comet", 7, 470, B, new[] { "stormcaller" }, "Every 4th Frost Barrage ends with a comet at the toughest enemy in range: 500% damage, and a blast of half that within 4 m."),
        Major(GlacialFortress, "Glacial Fortress", 7, 750, D, new[] { IceBlock, "frostblooded" }, "The Frost Shield holds until it breaks, and forms again 50% faster."),
    }, new[] { 1, 3, 6, 10, 15, 21, 28 },
        new[] { (W, 210f), (B, 470f), (D, 750f) },
        new HashSet<string> { Projectiles, ChillDuration, Pierce, FreezeDuration, MaxHealth, Regeneration, ShieldDuration });
}

/// <summary>What the ranks spent in the Frost tree add up to, in the terms <see cref="MageStats"/> uses (fractions for percentages).</summary>
internal sealed class FrostBonuses
{
    public float ColdDamage;
    public float CastSpeed;
    public int Projectiles;
    public float ProjectileSpeed;
    public float Range;
    public float Chill;
    public float ChillDuration;
    public float ChilledDamage;
    public float CritChance;
    public float CritDamage;
    public float BlastRadius;
    public float BlastDamage;
    public int Pierce;
    public float FreezeChance;
    public float FreezeDuration;
    public float EliteDamage;
    public float MaxHealth;
    public float Regeneration;
    public float ShieldStrength;
    public float ShieldRecharge;
    public float ShieldDuration;

    /// <summary>What damage taken is multiplied by: each rank of Glacial Bulwark takes a share off what is left.</summary>
    public float DamageTaken = 1f;

    public bool FrostShield;
    public bool FrostBlast;
    public bool DeepFreeze;
    public bool ShatteringWard;
    public bool Shatter;
    public bool SplittingIce;
    public bool IceBlock;
    public bool Blizzard;
    public bool Comet;
    public bool GlacialFortress;

    public static FrostBonuses From(IReadOnlyDictionary<string, int> ranks)
    {
        var b = new FrostBonuses();
        foreach (var node in FrostTree.Tree.Nodes)
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
                    case FrostTree.ColdDamage: b.ColdDamage += v / 100f; break;
                    case FrostTree.CastSpeed: b.CastSpeed += v / 100f; break;
                    case FrostTree.Projectiles: b.Projectiles += (int)v; break;
                    case FrostTree.ProjectileSpeed: b.ProjectileSpeed += v / 100f; break;
                    case FrostTree.Range: b.Range += v / 100f; break;
                    case FrostTree.Chill: b.Chill += v / 100f; break;
                    case FrostTree.ChillDuration: b.ChillDuration += v; break;
                    case FrostTree.ChilledDamage: b.ChilledDamage += v / 100f; break;
                    case FrostTree.CritChance: b.CritChance += v / 100f; break;
                    case FrostTree.CritDamage: b.CritDamage += v / 100f; break;
                    case FrostTree.BlastRadius: b.BlastRadius += v / 100f; break;
                    case FrostTree.BlastDamage: b.BlastDamage += v / 100f; break;
                    case FrostTree.Pierce: b.Pierce += (int)v; break;
                    case FrostTree.FreezeChance: b.FreezeChance += v / 100f; break;
                    case FrostTree.FreezeDuration: b.FreezeDuration += v; break;
                    case FrostTree.EliteDamage: b.EliteDamage += v / 100f; break;
                    case FrostTree.MaxHealth: b.MaxHealth += v; break;
                    case FrostTree.Regeneration: b.Regeneration += v; break;
                    case FrostTree.ShieldStrength: b.ShieldStrength += v / 100f; break;
                    case FrostTree.ShieldRecharge: b.ShieldRecharge += v / 100f; break;
                    case FrostTree.ShieldDuration: b.ShieldDuration += v; break;
                    case FrostTree.DamageTakenCut: b.DamageTaken *= MathF.Pow(1f - perRank / 100f, r); break;
                }
            }
        }

        bool Has(string id) => ranks.GetValueOrDefault(id) > 0;
        b.FrostShield = Has(FrostTree.FrostShield);
        b.FrostBlast = Has(FrostTree.FrostBlast);
        b.DeepFreeze = Has(FrostTree.DeepFreeze);
        b.ShatteringWard = Has(FrostTree.ShatteringWard);
        b.Shatter = Has(FrostTree.Shatter);
        b.SplittingIce = Has(FrostTree.SplittingIce);
        b.IceBlock = Has(FrostTree.IceBlock);
        b.Blizzard = Has(FrostTree.Blizzard);
        b.Comet = Has(FrostTree.Comet);
        b.GlacialFortress = Has(FrostTree.GlacialFortress);
        return b;
    }
}
