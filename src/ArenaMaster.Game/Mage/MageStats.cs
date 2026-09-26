using ArenaMaster.Game.Items;

namespace ArenaMaster.Game.Mage;

/// <summary>
/// The Mage's numbers for the current run: the base values, the run's upgrades (see <see cref="MageUpgrades"/>), the items carried (<see cref="Items"/>) and the
/// Frost tree (<see cref="Tree"/>). Upgrade, item and tree bonuses add together; item multipliers then multiply the lot. Items speak in general terms, and here
/// "damage" means cold damage, "attack speed" cast speed, and "critical" a bolt's crit.
/// </summary>
internal sealed class MageStats
{
    public const float BaseMaxHealth = 90f;
    public const float BaseMoveSpeed = 7f;
    public const float BasePickupRadius = 3f;

    /// <summary>
    /// The Frost Barrage: how many bolts, the moment between one leaving the staff and the next, seconds from one barrage to the next, each bolt's damage and speed,
    /// how far a bolt flies before it melts, and how far away the barrage looks for enemies to aim at.
    /// </summary>
    public const int BaseProjectiles = 7;
    public const float BoltStagger = 0.07f;
    public const float BaseBarrageInterval = 1.8f;
    public const float BaseBoltDamage = 15f;
    public const float BaseBoltSpeed = 24f;
    public const float BaseRange = 30f;
    public const float BaseTargetRange = 22f;

    public const float BaseCritChance = 0.05f;
    public const float BaseCritMultiplier = 2f;

    /// <summary>Every frost hit chills: this long, slowing the walk this much, and never more than the cap.</summary>
    public const float BaseChillDuration = 1.5f;
    public const float BaseChill = 0.2f;
    public const float MaxChill = 0.7f;

    /// <summary>Deep Freeze: the chance for a hit on a chilled enemy to freeze it, for how long, and the share of that an elite gets.</summary>
    public const float BaseFreezeChance = 0.1f;
    public const float BaseFreezeDuration = 1.2f;
    public const float EliteFreezeShare = 0.5f;

    /// <summary>Shatter: extra damage to a frozen enemy.</summary>
    public const float ShatterBonus = 0.6f;

    /// <summary>Frost Blast: its radius, and its share of the bolt's hit.</summary>
    public const float BaseBlastRadius = 2f;
    public const float BaseBlastDamage = 0.5f;

    /// <summary>Splitting Ice: how many bolts a kill splits into, their share of the damage, and how far they look for a new enemy.</summary>
    public const int SplitCount = 2;
    public const float SplitDamage = 0.5f;
    public const float SplitRange = 10f;

    /// <summary>Comet: every this-many-th barrage, this many bolts' damage, its blast's radius and share.</summary>
    public const int CometEvery = 4;
    public const float CometDamage = 5f;
    public const float CometRadius = 4f;
    public const float CometBlast = 0.5f;

    /// <summary>Blizzard: how far it reaches, and the bolt damages per second it does to each enemy in it.</summary>
    public const float BlizzardRadius = 5f;
    public const float BlizzardShare = 1f;

    /// <summary>Frost Shield: seconds between shields, how long one lasts, and how much it holds (a flat part and a share of max health).</summary>
    public const float BaseShieldInterval = 10f;
    public const float BaseShieldDuration = 4f;
    public const float ShieldFlat = 25f;
    public const float ShieldShare = 0.2f;

    /// <summary>Shattering Ward: its burst's reach, and the bolt damages it does.</summary>
    public const float WardBurstRadius = 4f;
    public const float WardBurstShare = 3f;

    /// <summary>Glacial Fortress: how much faster the shield forms again.</summary>
    public const float FortressRecharge = 0.5f;

    /// <summary>Ice Block: the heal, and the shield (a share of max health) it leaves.</summary>
    public const float IceBlockHeal = 0.25f;
    public const float IceBlockShield = 0.5f;

    /// <summary>The blink on Shift: a very short, very fast push.</summary>
    public const float BlinkDuration = 0.12f;
    public const float BlinkSpeed = 38f;
    public const float BlinkCooldown = 2.2f;

    private readonly Dictionary<MageUpgrade, int> _levels = new();

    /// <summary>What the items carried this run add up to.</summary>
    public ItemBonuses Items { get; set; } = new();

    /// <summary>What the Frost tree's ranks add up to.</summary>
    public FrostBonuses Tree { get; set; } = new();

    public int LevelOf(MageUpgrade upgrade) => _levels.GetValueOrDefault(upgrade);

    /// <summary>Takes one more level of <paramref name="upgrade"/> (no further than its maximum).</summary>
    public void Increase(MageUpgrade upgrade)
    {
        int level = LevelOf(upgrade);
        if (level < MageUpgrades.Info(upgrade).MaxLevel)
        {
            _levels[upgrade] = level + 1;
        }
    }

    /// <summary>A new run: no upgrades, no items. The tree is left as it is.</summary>
    public void Reset()
    {
        _levels.Clear();
        Items = new ItemBonuses();
    }

    public int Projectiles => BaseProjectiles + LevelOf(MageUpgrade.SplinterBolt) + Tree.Projectiles;

    public float BoltDamage =>
        BaseBoltDamage * (1f + 0.20f * LevelOf(MageUpgrade.IceShards) + Items.Damage + Tree.ColdDamage) * Items.DamageMultiplier;

    public float BarrageInterval =>
        BaseBarrageInterval
        / (MathF.Max(0.2f, 1f + 0.12f * LevelOf(MageUpgrade.QuickenedCasting) + Items.AttackSpeed + Tree.CastSpeed) * Items.AttackSpeedMultiplier);

    public float BoltSpeed => BaseBoltSpeed * (1f + 0.20f * LevelOf(MageUpgrade.WinterWind) + Tree.ProjectileSpeed);

    public float Range => BaseRange * (1f + 0.15f * LevelOf(MageUpgrade.WinterWind) + Tree.Range);

    /// <summary>How far away the barrage finds enemies to aim at: grows with range.</summary>
    public float TargetRange => BaseTargetRange * (1f + 0.15f * LevelOf(MageUpgrade.WinterWind) + Tree.Range);

    public int Pierce => LevelOf(MageUpgrade.PiercingIce) + Tree.Pierce;

    public float CritChance => BaseCritChance + 0.06f * LevelOf(MageUpgrade.FrozenPrecision) + Items.CritChance + Tree.CritChance;

    /// <summary>How many times normal damage a critical bolt does.</summary>
    public float CritMultiplier => BaseCritMultiplier + 0.15f * LevelOf(MageUpgrade.FrozenPrecision) + Items.CritDamage + Tree.CritDamage;

    /// <summary>How much a chill slows an enemy's walk (0 to 1), capped at <see cref="MaxChill"/>.</summary>
    public float Chill => MathF.Min(MaxChill, BaseChill + 0.08f * LevelOf(MageUpgrade.NumbingCold) + Tree.Chill);

    public float ChillDuration => BaseChillDuration + 0.3f * LevelOf(MageUpgrade.NumbingCold) + Tree.ChillDuration;

    /// <summary>What a hit on a chilled (or frozen) enemy is multiplied by.</summary>
    public float ChilledMultiplier => 1f + Tree.ChilledDamage;

    public float FreezeChance => Tree.DeepFreeze ? BaseFreezeChance + Tree.FreezeChance : 0f;

    public float FreezeDuration => BaseFreezeDuration + Tree.FreezeDuration;

    public float EliteMultiplier => 1f + Tree.EliteDamage;

    public float BlastRadius => BaseBlastRadius * (1f + 0.25f * LevelOf(MageUpgrade.ConcussiveFrost) + Tree.BlastRadius);

    public float BlastShare => BaseBlastDamage * (1f + Tree.BlastDamage);

    public float ShieldAmount => (ShieldFlat + ShieldShare * MaxHealth) * (1f + 0.30f * LevelOf(MageUpgrade.GlacialWard) + Tree.ShieldStrength);

    public float ShieldDuration => BaseShieldDuration + Tree.ShieldDuration;

    /// <summary>Seconds for the Frost Shield to form again after the last one ended.</summary>
    public float ShieldInterval => BaseShieldInterval / (1f + Tree.ShieldRecharge + (Tree.GlacialFortress ? FortressRecharge : 0f));

    public float MaxHealth => BaseMaxHealth + 15f * LevelOf(MageUpgrade.ArcaneVigor) + Items.MaxHealth + Tree.MaxHealth;

    public float Regeneration => Items.Regeneration + Tree.Regeneration;

    public float DamageTaken => Items.DamageTaken * Tree.DamageTaken;

    public float MoveSpeed => BaseMoveSpeed * (1f + 0.08f * LevelOf(MageUpgrade.FleetStep) + Items.MoveSpeed);

    public float PickupRadius => BasePickupRadius * (1f + 0.35f * LevelOf(MageUpgrade.Attunement) + Items.Pickup);
}
