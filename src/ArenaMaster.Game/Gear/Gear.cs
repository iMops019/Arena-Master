using ArenaMaster.Game.Items;
using ArenaMaster.Game.Progression;

namespace ArenaMaster.Game.Gear;

/// <summary>Where a piece of gear is worn. One piece per slot.</summary>
internal enum GearSlot
{
    BodyArmour,
    Weapon,
    Trinket,
}

/// <summary>How a gear stat's number is written: a whole number (armour, health), a percentage (move speed), or a multiplier with two decimals (x1.20).</summary>
internal enum GearUnit
{
    Flat,
    Percent,
    Multiplier,
}

/// <summary>
/// One line of a piece of gear: its words with <c>{0}</c> where the number goes, and the range the number rolls in when the piece drops (<paramref name="Min"/> the
/// worst roll, <paramref name="Max"/> the best: for a stat where lower is better, Min is the larger number). <paramref name="Decimals"/> for a flat number that
/// isn't whole (health a second). A stat with Min equal to Max doesn't roll. <paramref name="Apply"/> adds the rolled number to a run's bonuses.
/// </summary>
internal sealed record GearStat(string Text, GearUnit Unit, float Min, float Max, Action<ItemBonuses, float> Apply, int Decimals = 0)
{
    public bool Rolls => Min != Max;

    /// <summary>A number in this stat's range written the way the stat writes it: "24", "12", "1.20".</summary>
    public string Write(float value) => Unit switch
    {
        GearUnit.Multiplier => value.ToString("0.00"),
        _ => Decimals > 0 ? value.ToString("0." + new string('0', Decimals)) : value.ToString("0"),
    };

    /// <summary>The line with <paramref name="value"/> in it: "+24 armour".</summary>
    public string Line(float value) => string.Format(Text, Write(value));

    /// <summary>The range as it reads on a card: "10-30", the smaller number first.</summary>
    public string Range => $"{Write(MathF.Min(Min, Max))}-{Write(MathF.Max(Min, Max))}";

    /// <summary>The line with its range where the number goes: "+(10-30) armour", for a piece not yet found.</summary>
    public string RangeLine => Rolls ? string.Format(Text, $"({Range})") : Line(Max);

    /// <summary>Where <paramref name="value"/> sits in the range: 0 the worst roll, 1 the best. A stat that doesn't roll is always 1.</summary>
    public float Quality(float value) => Rolls ? Math.Clamp((value - Min) / (Max - Min), 0f, 1f) : 1f;

    /// <summary>The number at <paramref name="quality"/> (0 to 1) of the way from the worst roll to the best, rounded the way the stat is written.</summary>
    public float At(float quality)
    {
        float value = Min + (Max - Min) * Math.Clamp(quality, 0f, 1f);
        float step = Unit == GearUnit.Multiplier ? 0.01f : Decimals > 0 ? MathF.Pow(10f, -Decimals) : 1f;
        return MathF.Round(value / step) * step;
    }
}

/// <summary>
/// One piece of gear: a unique with a name, a few stats that each roll in a range when it drops (<paramref name="Stats"/>), and a line of flavour. Any class can
/// wear any piece. What a dropped copy actually rolled is a <see cref="GearItem"/>.
/// </summary>
internal sealed record GearPiece(string Id, string Name, GearSlot Slot, IReadOnlyList<GearStat> Stats, string Flavour)
{
    /// <summary>Every stat with its range: what a copy of this piece can roll.</summary>
    public string Effect => string.Join(" ", Stats.Select(s => s.RangeLine + "."));
}

/// <summary>One copy of a piece of gear, as it dropped: its own id, the piece, and the number each of the piece's stats rolled (in the piece's order).</summary>
internal sealed class GearItem
{
    public string Id { get; set; } = "";

    public string Piece { get; set; } = "";

    public List<float> Rolls { get; set; } = new();
}

/// <summary>What the player owns and wears, saved in the <see cref="Profile"/>.</summary>
internal sealed class GearSave
{
    /// <summary>Every copy of gear owned, each with its own rolls.</summary>
    public List<GearItem> Items { get; set; } = new();

    /// <summary>Slot name -> the id of the copy worn there.</summary>
    public Dictionary<string, string> Worn { get; set; } = new();

    /// <summary>A save from before gear rolled: the ids of the pieces owned. Turned into copies by <see cref="GearCatalog.Migrate"/>, and empty after.</summary>
    public List<string> Owned { get; set; } = new();

    /// <summary>A save from before gear rolled: piece id -> how many times it was found. Empty after <see cref="GearCatalog.Migrate"/>.</summary>
    public Dictionary<string, int> Copies { get; set; } = new();
}

/// <summary>Every piece of gear there is, and the rules for rolling, owning, wearing, finding and selling it. Pure.</summary>
internal static class GearCatalog
{
    private static GearStat Armour(float min, float max) => new("+{0} armour", GearUnit.Flat, min, max, (b, v) => b.Armour += v);

    private static GearStat MaxHealth(float min, float max) => new("+{0} max health", GearUnit.Flat, min, max, (b, v) => b.MaxHealth += v);

    private static GearStat Damage(float min, float max) => new("+{0}% damage", GearUnit.Percent, min, max, (b, v) => b.Damage += v / 100f);

    public static readonly IReadOnlyList<GearPiece> All = new GearPiece[]
    {
        // Body armour
        new("great_mages_vestments", "Great Mage's Vestments", GearSlot.BodyArmour, new[]
        {
            Armour(10, 30),
            new GearStat("A {0}-point shield over your health, back 10 seconds after it breaks", GearUnit.Flat, 40, 100, (b, v) =>
            {
                b.GearShield += v;
                b.GearShieldCooldown = 10f;
            }),
        }, "Woven for an archmage who never once learned to dodge."),
        new("ironhide_hauberk", "Ironhide Hauberk", GearSlot.BodyArmour, new[]
        {
            Armour(30, 80),
            new GearStat("Take {0}% less damage", GearUnit.Percent, 5, 15, (b, v) => b.DamageTaken *= 1f - v / 100f),
            MaxHealth(10, 30),
        }, "Every dent in it is a story. None of them end well for the other fellow."),
        new("thornmail_of_the_hollow", "Thornmail of the Hollow", GearSlot.BodyArmour, new[]
        {
            Armour(20, 50),
            new GearStat("{0}% of every blow that reaches you is paid back to the attacker", GearUnit.Percent, 40, 100, (b, v) => b.Retaliation += v / 100f),
        }, "Forged from the King's own briars. It remembers every hand that strikes it."),
        new("wraithskin_coat", "Wraithskin Coat", GearSlot.BodyArmour, new[]
        {
            Armour(8, 20),
            new GearStat("+{0}% move speed", GearUnit.Percent, 5, 15, (b, v) => b.MoveSpeed += v / 100f),
            new GearStat("Your Shift move recharges {0}% faster", GearUnit.Percent, 10, 30, (b, v) => b.DashRecharge += v / 100f),
        }, "Light as a held breath, and twice as hard to catch."),
        new("shroud_of_the_ninth_grave", "Shroud of the Ninth Grave", GearSlot.BodyArmour, new[]
        {
            Armour(15, 40),
            MaxHealth(10, 30),
            new GearStat("Thorns: {0} damage to every enemy touching you every 0.5 s", GearUnit.Flat, 2, 6, (b, v) => b.Thorns += v),
        }, "Stitched from the burial cloths of nine kings. None of them are resting."),
        new("berserkers_hide", "Berserker's Hide", GearSlot.BodyArmour, new[]
        {
            Armour(15, 40),
            MaxHealth(15, 40),
            new GearStat("Up to +{0}% damage as your health drops", GearUnit.Percent, 10, 30, (b, v) => b.LowHealthDamage += v / 100f),
        }, "Tanned from something that didn't know when to stop. Neither will you."),

        // Weapons
        new("kingsbane", "Kingsbane", GearSlot.Weapon, new[]
        {
            new GearStat("x{0} damage to elites and bosses", GearUnit.Multiplier, 1.15f, 1.4f, (b, v) => b.EliteDamage *= v),
        }, "It was made for one neck. It has since developed a taste for others."),
        new("emberbrand", "Emberbrand", GearSlot.Weapon, new[]
        {
            new GearStat("Every 5 seconds, a ring of fire bursts around you for {0} damage", GearUnit.Flat, 25, 60, (b, v) =>
            {
                b.FireNova += v;
                b.FireNovaInterval = 5f;
            }),
        }, "Never sheathed. The scabbard would not survive it."),
        new("tempest_fang", "Tempest Fang", GearSlot.Weapon, new[]
        {
            new GearStat("x{0} attack speed", GearUnit.Multiplier, 1.06f, 1.2f, (b, v) => b.AttackSpeedMultiplier *= v),
        }, "It hums in the hand, impatient for the next swing."),
        new("soulreaver", "Soulreaver", GearSlot.Weapon, new[]
        {
            Damage(4, 10),
            new GearStat("Heal {0} health per kill", GearUnit.Flat, 1, 2, (b, v) => b.HealOnKill += v, Decimals: 1),
        }, "Each life it takes, it shares. A generous blade, in its way."),
        new("ossified_wand", "Ossified Wand", GearSlot.Weapon, new[]
        {
            new GearStat("+{0} projectile", GearUnit.Flat, 1, 1, (b, v) => b.Projectiles += (int)v),
            Damage(4, 10),
        }, "It was a finger once. It still points at whoever is next."),
        new("worldsplitter", "Worldsplitter", GearSlot.Weapon, new[]
        {
            new GearStat("x{0} area", GearUnit.Multiplier, 1.1f, 1.3f, (b, v) => b.AreaMultiplier *= v),
            Damage(4, 10),
        }, "Swung once in anger, it left a valley. The valley is still angry."),

        // Trinkets
        new("reliquary_of_rot", "Reliquary of Rot", GearSlot.Trinket, new[]
        {
            new GearStat("x{0} damage to enemies below half health", GearUnit.Multiplier, 1.06f, 1.2f, (b, v) => b.WoundedDamage *= v),
            new GearStat("+{0}% area", GearUnit.Percent, 4, 10, (b, v) => b.Area += v / 100f),
        }, "What is inside is not a saint. It is, however, still hungry."),
        new("lantern_of_the_deep", "Lantern of the Deep", GearSlot.Trinket, new[]
        {
            new GearStat("+{0}% pickup range", GearUnit.Percent, 20, 50, (b, v) => b.Pickup += v / 100f),
            new GearStat("+{0}% experience", GearUnit.Percent, 5, 15, (b, v) => b.ExperienceGain += v / 100f),
        }, "Its light finds what the dark would rather keep."),
        new("delvers_compass", "Delver's Compass", GearSlot.Trinket, new[]
        {
            new GearStat("+{0}% silver from Delve caches", GearUnit.Percent, 20, 50, (b, v) => b.CacheSilver += v / 100f),
            new GearStat("+{0}% silver from every run", GearUnit.Percent, 5, 15, (b, v) => b.SilverGain += v / 100f),
        }, "The needle never points north. It points down, and toward treasure."),
        new("heart_of_the_mountain", "Heart of the Mountain", GearSlot.Trinket, new[]
        {
            MaxHealth(25, 60),
            new GearStat("+{0} health per second", GearUnit.Flat, 0.4f, 1f, (b, v) => b.Regeneration += v, Decimals: 1),
        }, "Still warm, and still beating, slow as stone."),
        new("gamblers_die", "Gambler's Die", GearSlot.Trinket, new[]
        {
            new GearStat("+{0}% increased critical chance", GearUnit.Percent, 15, 40, (b, v) => b.CritChance += v / 100f),
            new GearStat("+{0} level-up reroll per run", GearUnit.Flat, 1, 1, (b, v) => b.Rerolls += (int)v),
        }, "Six sides, all of them lucky. Just not always for you."),
        new("blood_chalice", "Blood Chalice", GearSlot.Trinket, new[]
        {
            new GearStat("Heal 1 health for every {0} damage you deal", GearUnit.Flat, 80, 40, (b, v) => b.LifePerDamage += 1f / v),
        }, "It is never empty. Best not to ask what fills it."),
    };

    /// <summary>What a copy sells for at the quartermaster: from <see cref="SellFloor"/> for the worst rolls up to <see cref="SellCeiling"/> for perfect ones.</summary>
    public const long SellFloor = 100;
    public const long SellCeiling = 400;

    public static GearPiece? Find(string id) => All.FirstOrDefault(p => p.Id == id);

    public static GearPiece PieceOf(GearItem item) => Find(item.Piece) ?? All[0];

    public static string SlotName(GearSlot slot) => slot switch
    {
        GearSlot.BodyArmour => "Body Armour",
        GearSlot.Weapon => "Weapon",
        _ => "Trinket",
    };

    /// <summary>The copies owned that are of a piece still in the catalogue.</summary>
    public static IEnumerable<GearItem> Owned(Profile profile) => profile.Gear.Items.Where(item => Find(item.Piece) is not null);

    public static bool Owns(Profile profile, GearPiece piece) => profile.Gear.Items.Any(item => item.Piece == piece.Id);

    /// <summary>How many different pieces the player owns at least one copy of.</summary>
    public static int PiecesFound(Profile profile) => All.Count(piece => Owns(profile, piece));

    /// <summary>How many copies of <paramref name="piece"/> the player owns.</summary>
    public static int CopiesOf(Profile profile, GearPiece piece) => profile.Gear.Items.Count(item => item.Piece == piece.Id);

    /// <summary>The number <paramref name="item"/> rolled for its piece's stat <paramref name="index"/> (the best roll if the save is missing it).</summary>
    public static float Roll(GearItem item, int index)
    {
        var stat = PieceOf(item).Stats[index];
        return index < item.Rolls.Count ? item.Rolls[index] : stat.Max;
    }

    /// <summary>How good <paramref name="item"/>'s rolls are over all its stats that roll: 0 every stat at its worst, 1 every stat at its best.</summary>
    public static float Quality(GearItem item)
    {
        var stats = PieceOf(item).Stats;
        var rolling = Enumerable.Range(0, stats.Count).Where(i => stats[i].Rolls).ToList();
        return rolling.Count == 0 ? 1f : rolling.Average(i => stats[i].Quality(Roll(item, i)));
    }

    /// <summary>Adds what <paramref name="item"/> rolled to a run's bonuses.</summary>
    public static void Apply(GearItem item, ItemBonuses bonuses)
    {
        var stats = PieceOf(item).Stats;
        for (int i = 0; i < stats.Count; i++)
        {
            stats[i].Apply(bonuses, Roll(item, i));
        }
    }

    /// <summary>A new copy of <paramref name="piece"/>, every stat rolled at random within its range.</summary>
    public static GearItem RollCopy(GearPiece piece, Random random) => new()
    {
        Id = Guid.NewGuid().ToString("N"),
        Piece = piece.Id,
        Rolls = piece.Stats.Select(stat => stat.At((float)random.NextDouble())).ToList(),
    };

    /// <summary>A copy of <paramref name="piece"/> with every stat at its best.</summary>
    public static GearItem Perfect(GearPiece piece) => new()
    {
        Id = Guid.NewGuid().ToString("N"),
        Piece = piece.Id,
        Rolls = piece.Stats.Select(stat => stat.Max).ToList(),
    };

    /// <summary>The copy worn in <paramref name="slot"/>, or null.</summary>
    public static GearItem? WornIn(Profile profile, GearSlot slot) =>
        profile.Gear.Worn.TryGetValue(slot.ToString(), out var id) && profile.Gear.Items.FirstOrDefault(item => item.Id == id) is { } item
        && Find(item.Piece) is { } piece && piece.Slot == slot ? item : null;

    /// <summary>Every copy worn, one per slot at most.</summary>
    public static IEnumerable<GearItem> Worn(Profile profile) =>
        Enum.GetValues<GearSlot>().Select(slot => WornIn(profile, slot)).OfType<GearItem>();

    public static bool IsWorn(Profile profile, GearItem item) => Worn(profile).Any(worn => worn.Id == item.Id);

    /// <summary>Wears <paramref name="item"/> in its piece's slot, taking off whatever was there. False if it isn't owned.</summary>
    public static bool Wear(Profile profile, GearItem item)
    {
        if (!profile.Gear.Items.Contains(item) || Find(item.Piece) is not { } piece)
        {
            return false;
        }

        profile.Gear.Worn[piece.Slot.ToString()] = item.Id;
        return true;
    }

    public static void TakeOff(Profile profile, GearSlot slot) => profile.Gear.Worn.Remove(slot.ToString());

    /// <summary>
    /// A piece found (a boss's gear roll that came up): any of the pieces at random, owned already or not, every stat rolled within its range - so the piece wanted,
    /// and a good roll of it, stay a hunt. It goes into what the player owns, and on if its slot is empty.
    /// </summary>
    public static GearItem Grant(Profile profile, Random random)
    {
        var piece = All[random.Next(All.Count)];
        var item = RollCopy(piece, random);
        profile.Gear.Items.Add(item);
        if (WornIn(profile, piece.Slot) is null)
        {
            Wear(profile, item);
        }

        return item;
    }

    /// <summary>What the quartermaster pays for <paramref name="item"/>: more the better it rolled.</summary>
    public static long SellPrice(GearItem item) => SellFloor + (long)MathF.Round((SellCeiling - SellFloor) * Quality(item) / 5f) * 5;

    /// <summary>Sells <paramref name="item"/> to the quartermaster for its <see cref="SellPrice"/>. Not while it is worn: take it off first.</summary>
    public static bool Sell(Profile profile, GearItem item)
    {
        if (IsWorn(profile, item) || !profile.Gear.Items.Remove(item))
        {
            return false;
        }

        profile.Silver += SellPrice(item);
        return true;
    }

    /// <summary>
    /// Brings a save from before gear rolled up to date: every piece it owned (and every extra copy it had found) becomes a copy with perfect rolls - the numbers it
    /// had then are the top of each range now - and what was worn stays worn. Does nothing to a save already up to date.
    /// </summary>
    public static void Migrate(Profile profile)
    {
        var save = profile.Gear;
        if (save.Owned.Count == 0)
        {
            save.Copies.Clear();
            return;
        }

        var wornPieces = save.Worn.ToDictionary(kv => kv.Key, kv => kv.Value);
        save.Worn.Clear();
        foreach (string id in save.Owned.Distinct())
        {
            if (Find(id) is not { } piece)
            {
                continue;
            }

            int copies = Math.Max(1, save.Copies.GetValueOrDefault(id));
            for (int i = 0; i < copies; i++)
            {
                var item = Perfect(piece);
                save.Items.Add(item);
                if (i == 0 && wornPieces.GetValueOrDefault(piece.Slot.ToString()) == id)
                {
                    save.Worn[piece.Slot.ToString()] = item.Id;
                }
            }
        }

        save.Owned.Clear();
        save.Copies.Clear();
    }
}
