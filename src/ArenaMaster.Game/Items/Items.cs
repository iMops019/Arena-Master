using ArenaMaster.Game.Combat;

namespace ArenaMaster.Game.Items;

internal enum ItemRarity
{
    Common,
    Rare,
    Epic,
    Legendary,
}

/// <summary>
/// Everything the items carried this run add up to. Items are shared loot - any class can carry any item - so they speak in general terms (damage, attack
/// speed, ...) and each class's own stats decide what those mean for it (see <c>RangerStats</c>).
///
/// Two kinds of number: the <b>bonuses</b> (percentages as fractions, 0.08 = +8%, and flat amounts) add together with each other and with a class's own
/// upgrades; the <b>multipliers</b> (from epics and legendaries) multiply on top of everything, so they stack hard.
///
/// Who reads what: a class's stats read the ones that change its own attacks (damage, attack speed, area, duration, projectiles, block, dash recharge...);
/// <see cref="EnemyField"/>'s hit effects apply the ones that change every hit whoever lands it (chill, freeze, damage to chilled, frozen and elite enemies); and
/// <see cref="ItemEffects"/> runs the ones that act on their own during a run (thorns, the ward, life from damage, paying blows back, the Aegis burst, the Phoenix).
/// </summary>
internal sealed class ItemBonuses
{
    public float Damage;
    public float AttackSpeed;
    public float MoveSpeed;
    public float Pickup;
    public float ExperienceGain;
    public float CritChance;

    /// <summary>Added to how many times normal damage a critical hit does.</summary>
    public float CritDamage;

    public float MaxHealth;

    /// <summary>Health back per second.</summary>
    public float Regeneration;

    /// <summary>Health back per kill.</summary>
    public float HealOnKill;

    public float DamageMultiplier = 1f;
    public float AttackSpeedMultiplier = 1f;

    /// <summary>What damage taken is multiplied by (below 1 is less damage).</summary>
    public float DamageTaken = 1f;

    /// <summary>Block chance added (any class can block with it), and what the whole block chance is multiplied by.</summary>
    public float BlockChance;
    public float BlockMultiplier = 1f;

    /// <summary>Thorns: damage to each enemy touching the player, every half second.</summary>
    public float Thorns;

    /// <summary>Bigger areas: the Paladin's nova and circles, the Mage's blasts and blizzard, the Ranger's Rain of Arrows.</summary>
    public float Area;

    /// <summary>Seconds longer for lingering effects: holy circles, chill, the Frost Shield, Momentum.</summary>
    public float Duration;

    /// <summary>Every hit chills (this much slower, for <see cref="ItemEffects.ChillSeconds"/>); the Mage's own chill gets this much stronger too.</summary>
    public float ChillOnHit;

    /// <summary>Extra projectiles (arrows, bolts); a class without them turns each into <see cref="ProjectileFallback"/> more damage. And what each projectile's damage is multiplied by.</summary>
    public int Projectiles;
    public float ProjectileDamage = 1f;

    /// <summary>What each extra projectile is worth to a class that has none (the Paladin): this much more damage.</summary>
    public const float ProjectileFallback = 0.12f;

    /// <summary>
    /// Extra chains: one more jump for whatever a class's attack jumps with - a Shaman fork's enemies, a Ranger's arrow chaining on, a Mage's bolt piercing through.
    /// A class with nothing that jumps (the Paladin) turns each into <see cref="ChainFallback"/> more damage.
    /// </summary>
    public int Chains;

    public const float ChainFallback = 0.10f;

    /// <summary>What every area (the nova, the circles, blasts, zaps, forks' reach, the rain) is multiplied by, on top of <see cref="Area"/>.</summary>
    public float AreaMultiplier = 1f;

    /// <summary>What the damage of enemy shots (bolts, fireballs) is multiplied by.</summary>
    public float RangedDamageTaken = 1f;

    /// <summary>Thunderstone: the damage of the lightning that strikes the nearest enemy every so often (see <see cref="ItemEffects"/>). 0 for none.</summary>
    public float SkyStrike;

    public float ProjectileSpeed;
    public float Range;

    /// <summary>A ward: a barrier of this many points every so often (see <see cref="ItemEffects"/>). A Mage with the Frost Shield gets its shield stronger by <see cref="WardShield"/> instead.</summary>
    public float Ward;
    public float WardShield;

    /// <summary>Damage to elites and bosses: a multiplier on every hit on them.</summary>
    public float EliteDamage = 1f;

    /// <summary>What every hit on a chilled (or frozen) enemy is multiplied by, and on a frozen one on top of that.</summary>
    public float ChilledDamage = 1f;
    public float FrozenDamage = 1f;

    /// <summary>The chance for any hit to freeze a non-boss enemy.</summary>
    public float FreezeChance;

    /// <summary>The Shift move recharges this much faster.</summary>
    public float DashRecharge;

    /// <summary>Health back for each point of damage dealt.</summary>
    public float LifePerDamage;

    public float SilverGain;
    public float SilverMultiplier = 1f;
    public float ExperienceMultiplier = 1f;

    /// <summary>Extra level-up rerolls per run.</summary>
    public int Rerolls;

    /// <summary>Attack speed added at no health left, scaling with the share of health missing (Berserker's Band).</summary>
    public float Berserk;

    /// <summary>The share of max health missing right now (0 to 1), kept up to date during a run by <see cref="ItemEffects"/> for <see cref="Berserk"/>.</summary>
    public float MissingHealth;

    /// <summary>Attack speed added for a while by a Frenzy potion from a crate, kept up to date during a run by the content (0 otherwise).</summary>
    public float Frenzy;

    /// <summary>Attack speed as it stands this moment: the flat bonus, Berserk's share and any Frenzy. Classes read this, not <see cref="AttackSpeed"/>.</summary>
    public float AttackSpeedNow => AttackSpeed + Berserk * MissingHealth + Frenzy;

    /// <summary>Enemies spawn with this much more health (Cursed Idol).</summary>
    public float EnemyHealth;

    /// <summary>Times a killing blow brings the player back (Phoenix Feather).</summary>
    public int LastStands;

    /// <summary>The share of every blow that reaches the player paid back to the attacker, and the share of max health healed each time.</summary>
    public float Retaliation;
    public float HealPerBlow;

    /// <summary>Damage of the burst of light every block releases (Aegis of the Dawn).</summary>
    public float BlockBurst;
}

/// <summary>One item: its name, rarity, what one of it does in words, and what one of it does to the bonuses. Stacks apply it once per copy.</summary>
internal sealed record RunItem(string Id, string Name, ItemRarity Rarity, string Description, Action<ItemBonuses> ApplyOne);

/// <summary>How likely each rarity is from one source of loot. Relative weights, not percentages.</summary>
internal readonly record struct RarityWeights(float Common, float Rare, float Epic, float Legendary)
{
    /// <summary>A chest found lying on the map, or an item a fodder enemy dropped.</summary>
    public static readonly RarityWeights World = new(60f, 28f, 10f, 2f);

    /// <summary>The chest an elite leaves: never common.</summary>
    public static readonly RarityWeights Elite = new(0f, 60f, 32f, 8f);

    /// <summary>The chest a boss leaves: epic or better.</summary>
    public static readonly RarityWeights Boss = new(0f, 0f, 75f, 25f);

    public ItemRarity Roll(Random random)
    {
        float total = Common + Rare + Epic + Legendary;
        float pick = (float)random.NextDouble() * total;
        if ((pick -= Common) < 0f)
        {
            return ItemRarity.Common;
        }

        if ((pick -= Rare) < 0f)
        {
            return ItemRarity.Rare;
        }

        return pick - Epic < 0f ? ItemRarity.Epic : ItemRarity.Legendary;
    }
}

/// <summary>Every item there is, and rolling one.</summary>
internal static class ItemCatalog
{
    public static readonly IReadOnlyList<RunItem> All = new RunItem[]
    {
        // Common: small bonuses.
        new("whetstone", "Whetstone", ItemRarity.Common, "+8% damage", b => b.Damage += 0.08f),
        new("feather_charm", "Feather Charm", ItemRarity.Common, "+8% attack speed", b => b.AttackSpeed += 0.08f),
        new("worn_boots", "Worn Boots", ItemRarity.Common, "+6% move speed", b => b.MoveSpeed += 0.06f),
        new("troll_blood", "Troll Blood", ItemRarity.Common, "+0.4 health per second", b => b.Regeneration += 0.4f),
        new("brigandine", "Leather Brigandine", ItemRarity.Common, "Take 6% less damage", b => b.DamageTaken *= 0.94f),
        new("lodestone", "Lodestone", ItemRarity.Common, "+20% pickup range", b => b.Pickup += 0.20f),
        new("old_tome", "Old Tome", ItemRarity.Common, "+8% experience", b => b.ExperienceGain += 0.08f),

        // Rare: bigger bonuses, and a few new effects.
        new("hawk_feather", "Hawk Feather", ItemRarity.Rare, "+6% critical chance", b => b.CritChance += 0.06f),
        new("troll_heart", "Troll Heart", ItemRarity.Rare, "+25 max health", b => b.MaxHealth += 25f),
        new("vampire_fang", "Vampire Fang", ItemRarity.Rare, "Heal 1 health per kill", b => b.HealOnKill += 1f),
        new("serrated_edge", "Serrated Edge", ItemRarity.Rare, "Critical hits deal +30% more", b => b.CritDamage += 0.30f),

        // Paladin-leaning: block, thorns, area, lingering effects. Every class gets something from each.
        new("iron_buckler", "Iron Buckler", ItemRarity.Common, "+4% block chance (any class can block with it)", b => b.BlockChance += 0.04f),
        new("thorned_bracers", "Thorned Bracers", ItemRarity.Common, "Thorns: 4 damage to enemies touching you every 0.5 s", b => b.Thorns += 4f),
        new("pilgrims_censer", "Pilgrim's Censer", ItemRarity.Rare, "+20% area: nova, circles, blasts, blizzard, rain", b => b.Area += 0.20f),
        new("blessed_reliquary", "Blessed Reliquary", ItemRarity.Rare, "Lingering effects last 1 s longer: circles, chill, shields, Momentum", b => b.Duration += 1f),

        // Mage-leaning: projectiles, cold, wards.
        new("rime_charm", "Rime Charm", ItemRarity.Common, "Your hits chill: 10% slower for 1 s (the Mage's chill +10%)", b => b.ChillOnHit += 0.10f),
        new("prism_shard", "Prism Shard", ItemRarity.Rare, "+1 projectile (the Paladin: +12% nova damage)", b => b.Projectiles += 1),
        new("warding_crystal", "Warding Crystal", ItemRarity.Rare, "Every 15 s a 20-point ward holds for 5 s (with Frost Shield: shield +25%)", b =>
        {
            b.Ward += 20f;
            b.WardShield += 0.25f;
        }),
        new("lens_of_clarity", "Lens of Clarity", ItemRarity.Rare, "+20% projectile speed and range, +5% critical chance", b =>
        {
            b.ProjectileSpeed += 0.20f;
            b.Range += 0.20f;
            b.CritChance += 0.05f;
        }),

        // For everyone.
        new("merchants_purse", "Merchant's Purse", ItemRarity.Common, "+15% silver from runs", b => b.SilverGain += 0.15f),
        new("headsmans_axe", "Headsman's Axe", ItemRarity.Rare, "+25% damage to elites and bosses", b => b.EliteDamage *= 1.25f),
        new("windwalker_boots", "Wind-Walker Boots", ItemRarity.Rare, "Dash recharges 25% faster, +5% move speed", b =>
        {
            b.DashRecharge += 0.25f;
            b.MoveSpeed += 0.05f;
        }),
        new("bloodstone", "Bloodstone", ItemRarity.Rare, "Heal 1 health for every 60 damage you deal", b => b.LifePerDamage += 1f / 60f),
        new("hourglass", "Hourglass of Chances", ItemRarity.Rare, "+1 level-up reroll per run, +5% experience", b =>
        {
            b.Rerolls += 1;
            b.ExperienceGain += 0.05f;
        }),

        // Shaman-leaning: chains, area, lightning.
        new("grounding_charm", "Grounding Charm", ItemRarity.Common, "Take 25% less damage from bolts and fireballs", b => b.RangedDamageTaken *= 0.75f),
        new("storm_glass", "Storm Glass", ItemRarity.Common, "+10% area, and lingering effects last 0.5 s longer", b =>
        {
            b.Area += 0.10f;
            b.Duration += 0.5f;
        }),
        new("conductors_coil", "Conductor's Coil", ItemRarity.Rare, "+1 chain: one more fork, arrow chain or bolt pierce (the Paladin: +10% nova damage)", b => b.Chains += 1),
        new("thunderstone", "Thunderstone", ItemRarity.Rare, "Every 6 s lightning strikes the nearest enemy for 40 damage", b => b.SkyStrike += 40f),

        // Epic: multipliers.
        new("rune_of_might", "Rune of Might", ItemRarity.Epic, "x1.2 damage", b => b.DamageMultiplier *= 1.2f),
        new("swiftwind_sigil", "Swiftwind Sigil", ItemRarity.Epic, "x1.15 attack speed", b => b.AttackSpeedMultiplier *= 1.15f),
        new("ironbark_totem", "Ironbark Totem", ItemRarity.Epic, "x0.85 damage taken, +20 max health", b =>
        {
            b.DamageTaken *= 0.85f;
            b.MaxHealth += 20f;
        }),
        new("bulwark_sigil", "Bulwark Sigil", ItemRarity.Epic, "x1.25 block chance, x0.9 damage taken", b =>
        {
            b.BlockMultiplier *= 1.25f;
            b.DamageTaken *= 0.9f;
        }),
        new("heart_of_winter", "Heart of Winter", ItemRarity.Epic, "x1.25 damage to slowed or frozen enemies", b => b.ChilledDamage *= 1.25f),
        new("glass_pendant", "Glass Pendant", ItemRarity.Epic, "x1.35 damage, but x1.2 damage taken", b =>
        {
            b.DamageMultiplier *= 1.35f;
            b.DamageTaken *= 1.2f;
        }),
        new("berserkers_band", "Berserker's Band", ItemRarity.Epic, "Up to +35% attack speed as your health drops", b => b.Berserk += 0.35f),
        new("stormcallers_horn", "Stormcaller's Horn", ItemRarity.Epic, "x1.3 area", b => b.AreaMultiplier *= 1.3f),
        new("cursed_idol", "Cursed Idol", ItemRarity.Epic, "+40% experience and silver, but enemies have +15% health", b =>
        {
            b.ExperienceGain += 0.40f;
            b.SilverGain += 0.40f;
            b.EnemyHealth += 0.15f;
        }),

        // Legendary: big multipliers.
        new("dragon_heart", "Dragon Heart", ItemRarity.Legendary, "+60 max health, +1.5 health per second", b =>
        {
            b.MaxHealth += 60f;
            b.Regeneration += 1.5f;
        }),
        new("hunters_moon", "Hunter's Moon", ItemRarity.Legendary, "x1.35 damage, +10% critical chance", b =>
        {
            b.DamageMultiplier *= 1.35f;
            b.CritChance += 0.10f;
        }),
        new("martyrs_crown", "Martyr's Crown", ItemRarity.Legendary, "Blows that reach you are paid back at 150%, and heal you 1% each", b =>
        {
            b.Retaliation += 1.5f;
            b.HealPerBlow += 0.01f;
        }),
        new("aegis_of_dawn", "Aegis of the Dawn", ItemRarity.Legendary, "+12% block chance; every block bursts with light for 25 damage", b =>
        {
            b.BlockChance += 0.12f;
            b.BlockBurst += 25f;
        }),
        new("staff_of_long_night", "Staff of the Long Night", ItemRarity.Legendary, "Hits have a 5% chance to freeze (not bosses); frozen take x1.4", b =>
        {
            b.FreezeChance += 0.05f;
            b.FrozenDamage *= 1.4f;
        }),
        new("splintered_crown", "Splintered Crown", ItemRarity.Legendary, "+2 projectiles, each 15% weaker (the Paladin: +24% nova damage)", b =>
        {
            b.Projectiles += 2;
            b.ProjectileDamage *= 0.85f;
        }),
        new("phoenix_feather", "Phoenix Feather", ItemRarity.Legendary, "Once per run, a killing blow brings you back at 50% health", b => b.LastStands += 1),
        new("crown_of_storms", "Crown of Storms", ItemRarity.Legendary, "+2 chains, x1.2 attack speed", b =>
        {
            b.Chains += 2;
            b.AttackSpeedMultiplier *= 1.2f;
        }),
        new("crown_of_plenty", "Crown of Plenty", ItemRarity.Legendary, "x1.2 damage, experience and silver", b =>
        {
            b.DamageMultiplier *= 1.2f;
            b.ExperienceMultiplier *= 1.2f;
            b.SilverMultiplier *= 1.2f;
        }),
    };

    /// <summary>
    /// A random item from a source with <paramref name="weights"/>: first the rarity, then an item of that rarity from those <paramref name="available"/> allows (all, if
    /// not given). If no item of the rolled rarity is available yet (still locked), the next rarity down is tried, then up - so a roll always gives something.
    /// </summary>
    public static RunItem Roll(Random random, RarityWeights weights, Func<RunItem, bool>? available = null)
    {
        var rarity = weights.Roll(random);
        var candidates = available is null ? All : All.Where(available).ToList();
        if (candidates.Count == 0)
        {
            candidates = All;   // everything locked: the lock can't leave a chest empty
        }

        for (int step = 0; step <= (int)ItemRarity.Legendary; step++)
        {
            var down = candidates.Where(i => i.Rarity == rarity - step).ToList();
            if (down.Count > 0)
            {
                return down[random.Next(down.Count)];
            }
        }

        var up = candidates.Where(i => i.Rarity > rarity).OrderBy(i => i.Rarity).ToList();
        return up[random.Next(up.Count(i => i.Rarity == up[0].Rarity))];
    }
}

/// <summary>
/// A run's items. What the run starts with - the loadout, every copy owned at the moment of setting out - is fixed for the whole run and is all that gives
/// bonuses. Items found during the run go straight into the chest and are listed as found, but do nothing until a later run brings them: a find never boosts the
/// run it was found in, not even as an extra copy of an item already brought.
/// </summary>
internal sealed class RunItems
{
    private readonly List<RunItem> _found = new();

    /// <summary>What the run set out with, and so what gives its bonuses.</summary>
    public ItemInventory Carried { get; } = new();

    /// <summary>What has been found so far this run, in the order found.</summary>
    public IReadOnlyList<RunItem> Found => _found;

    /// <summary>Starts a run carrying <paramref name="loadout"/> (each copy listed once), with nothing found yet.</summary>
    public void Begin(IEnumerable<RunItem> loadout)
    {
        Carried.Clear();
        foreach (var item in loadout)
        {
            Carried.Add(item);
        }

        _found.Clear();
    }

    /// <summary>An item picked up on the run: into <paramref name="profile"/>'s chest for good, and onto the found list - but not into <see cref="Carried"/>.</summary>
    public void Find(RunItem item, Progression.Profile profile)
    {
        profile.AddToStash(item.Id);
        _found.Add(item);
    }
}

/// <summary>A set of items and how many of each, in the order they were first added.</summary>
internal sealed class ItemInventory
{
    private readonly List<(RunItem Item, int Count)> _items = new();
    private ItemBonuses? _bonuses;

    public IReadOnlyList<(RunItem Item, int Count)> Items => _items;

    public int CountOf(RunItem item) => _items.FirstOrDefault(e => e.Item == item).Count;

    public void Add(RunItem item)
    {
        int index = _items.FindIndex(e => e.Item == item);
        if (index >= 0)
        {
            _items[index] = (item, _items[index].Count + 1);
        }
        else
        {
            _items.Add((item, 1));
        }

        _bonuses = null;
    }

    /// <summary>What everything carried adds up to (worked out again only after the items change).</summary>
    public ItemBonuses Bonuses
    {
        get
        {
            if (_bonuses is null)
            {
                _bonuses = new ItemBonuses();
                foreach (var (item, count) in _items)
                {
                    for (int i = 0; i < count; i++)
                    {
                        item.ApplyOne(_bonuses);
                    }
                }
            }

            return _bonuses;
        }
    }

    public void Clear()
    {
        _items.Clear();
        _bonuses = null;
    }
}
