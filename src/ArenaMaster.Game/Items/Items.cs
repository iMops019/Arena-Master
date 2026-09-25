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

        // Epic: multipliers.
        new("rune_of_might", "Rune of Might", ItemRarity.Epic, "x1.2 damage", b => b.DamageMultiplier *= 1.2f),
        new("swiftwind_sigil", "Swiftwind Sigil", ItemRarity.Epic, "x1.15 attack speed", b => b.AttackSpeedMultiplier *= 1.15f),
        new("ironbark_totem", "Ironbark Totem", ItemRarity.Epic, "x0.85 damage taken, +20 max health", b =>
        {
            b.DamageTaken *= 0.85f;
            b.MaxHealth += 20f;
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
    };

    /// <summary>A random item from a source with <paramref name="weights"/>: first the rarity, then an item of that rarity.</summary>
    public static RunItem Roll(Random random, RarityWeights weights)
    {
        var rarity = weights.Roll(random);
        var pool = All.Where(i => i.Rarity == rarity).ToList();
        return pool[random.Next(pool.Count)];
    }
}

/// <summary>The items carried this run, how many of each, in the order they were first found.</summary>
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
