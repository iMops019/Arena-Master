namespace ArenaMaster.Game.Combat;

/// <summary>How rare an enemy spawned: most are normal; a few are Magic, Rare or Legendary, tougher and worth more.</summary>
internal enum MonsterRarity
{
    Normal,
    Magic,
    Rare,
    Legendary,
}

/// <summary>
/// What a rarity does to an enemy: its health, damage, attack speed (every attack's wind-up, blow and recovery, the breather between attacks, and how often
/// it claws), walking speed and experience are multiplied; it may leave a chest (<see cref="ChestChance"/>); and it is drawn a little bigger, over a ring in
/// its colour (<see cref="RingModel"/>). Bosses and props (crates) never roll a rarity.
/// </summary>
internal sealed record RarityTraits(
    MonsterRarity Rarity,
    float Health,
    float Damage,
    float AttackSpeed,
    float Speed,
    float Experience,
    float ChestChance,
    float Size,
    string? RingModel)
{
    public static readonly RarityTraits Normal = new(MonsterRarity.Normal, 1f, 1f, 1f, 1f, 1f, 0f, 1f, null);

    /// <summary>Magic: only more health (and more experience for it).</summary>
    public static readonly RarityTraits Magic = new(MonsterRarity.Magic, Health: 2.5f, Damage: 1f, AttackSpeed: 1f, Speed: 1f, Experience: 2f,
        ChestChance: 0f, Size: 1.06f, RingModel: "rarity_magic.glb");

    /// <summary>Rare: somewhat more health, damage, attack speed and walking speed; a small chance of a chest.</summary>
    public static readonly RarityTraits Rare = new(MonsterRarity.Rare, Health: 4f, Damage: 1.2f, AttackSpeed: 1.2f, Speed: 1.12f, Experience: 5f,
        ChestChance: 0.005f, Size: 1.12f, RingModel: "rarity_rare.glb");

    /// <summary>Legendary: far more of all of them; a real chance of a chest, with the elites' odds.</summary>
    public static readonly RarityTraits Legendary = new(MonsterRarity.Legendary, Health: 10f, Damage: 1.6f, AttackSpeed: 1.5f, Speed: 1.3f, Experience: 15f,
        ChestChance: 0.05f, Size: 1.25f, RingModel: "rarity_legendary.glb");

    public static RarityTraits Of(MonsterRarity rarity) => rarity switch
    {
        MonsterRarity.Magic => Magic,
        MonsterRarity.Rare => Rare,
        MonsterRarity.Legendary => Legendary,
        _ => Normal,
    };

    /// <summary>What goes before an enemy's name ("Legendary Ghoul"); empty for a normal one.</summary>
    public string Prefix => Rarity == MonsterRarity.Normal ? "" : Rarity + " ";
}

/// <summary>The chance (0 to 1) that a spawning enemy is Magic, Rare or Legendary; the rest are normal.</summary>
internal readonly record struct RarityOdds(float Magic, float Rare, float Legendary)
{
    public static readonly RarityOdds None = new(0f, 0f, 0f);

    /// <summary>Every run's: 6% Magic, 1.5% Rare, 0.3% Legendary.</summary>
    public static readonly RarityOdds Standard = new(0.06f, 0.015f, 0.003f);

    /// <summary>The rarity a roll of <paramref name="roll"/> (0 to 1) gives: the rarest bands first.</summary>
    public MonsterRarity Pick(double roll)
    {
        if ((roll -= Legendary) < 0.0)
        {
            return MonsterRarity.Legendary;
        }

        if ((roll -= Rare) < 0.0)
        {
            return MonsterRarity.Rare;
        }

        return roll - Magic < 0.0 ? MonsterRarity.Magic : MonsterRarity.Normal;
    }
}
