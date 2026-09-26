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

/// <summary>
/// One piece of gear: a unique with a name, one simple and strong effect (<paramref name="Effect"/> says it; <paramref name="Apply"/> does it, through the same
/// bonuses items use), and a line of flavour. Any class can wear any piece. Worn gear counts on every run, classic or Delve, until it is taken off.
/// </summary>
internal sealed record GearPiece(string Id, string Name, GearSlot Slot, string Effect, string Flavour, Action<ItemBonuses> Apply);

/// <summary>What the player owns and wears, saved in the <see cref="Profile"/>.</summary>
internal sealed class GearSave
{
    /// <summary>The ids of the pieces owned. A piece is owned once, never twice.</summary>
    public List<string> Owned { get; set; } = new();

    /// <summary>Slot name -> the id of the piece worn there.</summary>
    public Dictionary<string, string> Worn { get; set; } = new();
}

/// <summary>Every piece of gear there is, and the rules for owning, wearing and finding it. Pure.</summary>
internal static class GearCatalog
{
    public static readonly IReadOnlyList<GearPiece> All = new GearPiece[]
    {
        // Body armour
        new("great_mages_vestments", "Great Mage's Vestments", GearSlot.BodyArmour,
            "Gain a 100-point shield over your health. Once it breaks, it returns after 10 seconds.",
            "Woven for an archmage who never once learned to dodge.",
            b =>
            {
                b.GearShield += 100f;
                b.GearShieldCooldown = 10f;
            }),
        new("ironhide_hauberk", "Ironhide Hauberk", GearSlot.BodyArmour,
            "Take 15% less damage. +30 max health.",
            "Every dent in it is a story. None of them end well for the other fellow.",
            b =>
            {
                b.DamageTaken *= 0.85f;
                b.MaxHealth += 30f;
            }),
        new("thornmail_of_the_hollow", "Thornmail of the Hollow", GearSlot.BodyArmour,
            "Every blow that reaches you is paid back to the attacker in full.",
            "Forged from the King's own briars. It remembers every hand that strikes it.",
            b => b.Retaliation += 1f),
        new("wraithskin_coat", "Wraithskin Coat", GearSlot.BodyArmour,
            "+15% move speed. Your Shift move recharges 30% faster.",
            "Light as a held breath, and twice as hard to catch.",
            b =>
            {
                b.MoveSpeed += 0.15f;
                b.DashRecharge += 0.30f;
            }),

        // Weapons
        new("kingsbane", "Kingsbane", GearSlot.Weapon,
            "x1.4 damage to elites and bosses.",
            "It was made for one neck. It has since developed a taste for others.",
            b => b.EliteDamage *= 1.4f),
        new("emberbrand", "Emberbrand", GearSlot.Weapon,
            "Every 5 seconds, a ring of fire bursts around you for 60 damage.",
            "Never sheathed. The scabbard would not survive it.",
            b =>
            {
                b.FireNova += 60f;
                b.FireNovaInterval = 5f;
            }),
        new("tempest_fang", "Tempest Fang", GearSlot.Weapon,
            "x1.2 attack speed.",
            "It hums in the hand, impatient for the next swing.",
            b => b.AttackSpeedMultiplier *= 1.2f),
        new("soulreaver", "Soulreaver", GearSlot.Weapon,
            "+10% damage. Heal 2 health per kill.",
            "Each life it takes, it shares. A generous blade, in its way.",
            b =>
            {
                b.Damage += 0.10f;
                b.HealOnKill += 2f;
            }),

        // Trinkets
        new("lantern_of_the_deep", "Lantern of the Deep", GearSlot.Trinket,
            "+50% pickup range. +15% experience.",
            "Its light finds what the dark would rather keep.",
            b =>
            {
                b.Pickup += 0.5f;
                b.ExperienceGain += 0.15f;
            }),
        new("delvers_compass", "Delver's Compass", GearSlot.Trinket,
            "+50% silver from Delve caches. +15% silver from every run.",
            "The needle never points north. It points down, and toward treasure.",
            b =>
            {
                b.CacheSilver += 0.5f;
                b.SilverGain += 0.15f;
            }),
        new("heart_of_the_mountain", "Heart of the Mountain", GearSlot.Trinket,
            "+60 max health. +1 health per second.",
            "Still warm, and still beating, slow as stone.",
            b =>
            {
                b.MaxHealth += 60f;
                b.Regeneration += 1f;
            }),
        new("gamblers_die", "Gambler's Die", GearSlot.Trinket,
            "+10% critical chance. +1 level-up reroll per run.",
            "Six sides, all of them lucky. Just not always for you.",
            b =>
            {
                b.CritChance += 0.10f;
                b.Rerolls += 1;
            }),
    };

    public static GearPiece? Find(string id) => All.FirstOrDefault(p => p.Id == id);

    public static string SlotName(GearSlot slot) => slot switch
    {
        GearSlot.BodyArmour => "Body Armour",
        GearSlot.Weapon => "Weapon",
        _ => "Trinket",
    };

    public static bool Owns(Profile profile, GearPiece piece) => profile.Gear.Owned.Contains(piece.Id);

    /// <summary>The piece worn in <paramref name="slot"/>, or null.</summary>
    public static GearPiece? WornIn(Profile profile, GearSlot slot) =>
        profile.Gear.Worn.TryGetValue(slot.ToString(), out var id) && Find(id) is { } piece && Owns(profile, piece) ? piece : null;

    /// <summary>Every piece worn, one per slot at most.</summary>
    public static IEnumerable<GearPiece> Worn(Profile profile) =>
        Enum.GetValues<GearSlot>().Select(slot => WornIn(profile, slot)).OfType<GearPiece>();

    /// <summary>Wears <paramref name="piece"/> in its slot, taking off whatever was there. False if it isn't owned.</summary>
    public static bool Wear(Profile profile, GearPiece piece)
    {
        if (!Owns(profile, piece))
        {
            return false;
        }

        profile.Gear.Worn[piece.Slot.ToString()] = piece.Id;
        return true;
    }

    public static void TakeOff(Profile profile, GearSlot slot) => profile.Gear.Worn.Remove(slot.ToString());

    /// <summary>
    /// A piece found (a Delve cache's gear roll that came up): one not yet owned, at random, preferring an empty slot's. It goes straight into what the player owns, and on if its slot is
    /// empty. Null if every piece is owned already.
    /// </summary>
    public static GearPiece? Grant(Profile profile, Random random)
    {
        var unowned = All.Where(p => !Owns(profile, p)).ToList();
        if (unowned.Count == 0)
        {
            return null;
        }

        var forEmpty = unowned.Where(p => WornIn(profile, p.Slot) is null).ToList();
        var pool = forEmpty.Count > 0 ? forEmpty : unowned;
        var piece = pool[random.Next(pool.Count)];
        profile.Gear.Owned.Add(piece.Id);
        if (WornIn(profile, piece.Slot) is null)
        {
            Wear(profile, piece);
        }

        return piece;
    }
}
