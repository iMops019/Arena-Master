using ArenaMaster.Game.Items;

namespace ArenaMaster.Game.Paladin;

/// <summary>
/// The Paladin's numbers for the current run: the base values, the run's upgrades (see <see cref="PaladinUpgrades"/>), the items carried (<see cref="Items"/>) and
/// the Defiance tree (<see cref="Tree"/>). Upgrade, item and tree bonuses add together; item multipliers then multiply the lot. Items speak in general terms, and
/// here "damage" and "attack speed" mean the Holy Nova's (and the circles' and thorns' damage too), "critical" means a nova's crit.
/// </summary>
internal sealed class PaladinStats
{
    public const float BaseMaxHealth = 130f;
    public const float BaseMoveSpeed = 6.4f;
    public const float BasePickupRadius = 3f;

    /// <summary>The Holy Nova: damage to everything it reaches, seconds between bursts, and how far it reaches from the Paladin.</summary>
    public const float BaseNovaDamage = 30f;
    public const float BaseNovaInterval = 1.25f;
    public const float BaseNovaRadius = 4.5f;

    /// <summary>The holy circle a nova leaves: its size, how long it burns, damage per second to each enemy in it, and health per second to the Paladin standing in it.</summary>
    public const float BaseCircleRadius = 3f;
    public const float BaseCircleDuration = 4f;
    public const float BaseCircleDps = 10f;
    public const float BaseCircleHealing = 2f;

    public const float BaseCritChance = 0.05f;
    public const float BaseCritMultiplier = 2f;

    /// <summary>The big shield: the chance to block with nothing else, and the most block chance can reach.</summary>
    public const float BaseBlockChance = 0.1f;
    public const float MaxBlockChance = 0.6f;

    /// <summary>Thorns (once Crown of Thorns unlocks them): damage per strike, seconds between strikes, and how far past touching they reach.</summary>
    public const float BaseThorns = 6f;
    public const float BaseThornsInterval = 0.5f;
    public const float ThornsReach = 0.4f;

    /// <summary>Crown of Briars: thorns reach this far from the Paladin's feet, and hit this much harder.</summary>
    public const float BriarsReach = 3f;
    public const float BriarsBonus = 0.5f;

    /// <summary>The shield rush on Shift: how long, how fast, how often.</summary>
    public const float RushDuration = 0.2f;
    public const float BaseRushSpeed = 17f;
    public const float BaseRushCooldown = 1.6f;

    /// <summary>Echoing Nova: how long after the first burst the echo comes, and its share of the damage.</summary>
    public const float EchoDelay = 0.35f;
    public const float EchoDamage = 0.6f;

    /// <summary>Wrath of the Many: extra nova damage per enemy hit, and the most it can add.</summary>
    public const float WrathPerEnemy = 0.02f;
    public const float WrathCap = 0.6f;

    /// <summary>Radiant Avatar: every this-many-th nova is a Great Nova, this much bigger and harder, its circle this much bigger.</summary>
    public const int AvatarEvery = 8;
    public const float AvatarRadius = 2f;
    public const float AvatarDamage = 3f;
    public const float AvatarCircle = 1.6f;

    /// <summary>Resonance: a circle's own burst, as a share of the nova's damage.</summary>
    public const float ResonanceDamage = 0.5f;

    /// <summary>Holy Bastion: a block's nova, as a share of the nova's damage.</summary>
    public const float BastionDamage = 0.75f;

    /// <summary>Retribution: what an attacker takes back, as a share of its blow.</summary>
    public const float RetributionShare = 2f;

    /// <summary>Shield of Faith: seconds for the shield to ready itself again after it turns a blow aside.</summary>
    public const float FaithInterval = 12f;

    /// <summary>Unbroken Vow: the share of max health it heals once it has saved the Paladin.</summary>
    public const float VowHeal = 0.4f;

    /// <summary>Desperate Prayer works below this share of max health.</summary>
    public const float LowHealth = 0.4f;

    /// <summary>Sanctuary, while standing in a holy circle: damage taken multiplied by this, and block chance added.</summary>
    public const float SanctuaryDamageTaken = 0.8f;
    public const float SanctuaryBlock = 0.1f;

    private readonly Dictionary<PaladinUpgrade, int> _levels = new();

    /// <summary>What the items carried this run add up to.</summary>
    public ItemBonuses Items { get; set; } = new();

    /// <summary>What the Defiance tree's ranks add up to.</summary>
    public DefianceBonuses Tree { get; set; } = new();

    public int LevelOf(PaladinUpgrade upgrade) => _levels.GetValueOrDefault(upgrade);

    /// <summary>Takes one more level of <paramref name="upgrade"/> (no further than its maximum).</summary>
    public void Increase(PaladinUpgrade upgrade)
    {
        int level = LevelOf(upgrade);
        if (level < PaladinUpgrades.Info(upgrade).MaxLevel)
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

    public float NovaDamage =>
        BaseNovaDamage * (1f + 0.20f * LevelOf(PaladinUpgrade.HolyWrath) + Items.Damage + Tree.NovaDamage + ItemBonuses.ProjectileFallback * Items.Projectiles)
        * Items.DamageMultiplier;

    public float NovaInterval =>
        BaseNovaInterval
        / (MathF.Max(0.2f, 1f + 0.12f * LevelOf(PaladinUpgrade.QuickenedPrayer) + Items.AttackSpeedNow + Tree.NovaFrequency) * Items.AttackSpeedMultiplier);

    public float NovaRadius => BaseNovaRadius * (1f + 0.10f * LevelOf(PaladinUpgrade.Radiance) + Tree.NovaRadius + Items.Area);

    public float CircleRadius => BaseCircleRadius * (1f + 0.10f * LevelOf(PaladinUpgrade.Radiance) + Tree.CircleRadius + Items.Area);

    public float CircleDuration => BaseCircleDuration + 0.5f * LevelOf(PaladinUpgrade.Consecration) + Tree.CircleDuration + Items.Duration;

    /// <summary>A circle's damage per second to each enemy in it.</summary>
    public float CircleDps =>
        BaseCircleDps * (1f + 0.25f * LevelOf(PaladinUpgrade.Consecration) + Items.Damage + Tree.CircleDamage) * Items.DamageMultiplier;

    /// <summary>Health per second from standing in a circle (from each circle, with Consecrated Ground).</summary>
    public float CircleHealing => BaseCircleHealing * (1f + Tree.CircleHealing);

    public float CritChance => BaseCritChance + Items.CritChance + Tree.CritChance;

    /// <summary>How many times normal damage a critical nova hit does.</summary>
    public float CritMultiplier => BaseCritMultiplier + Items.CritDamage + Tree.CritDamage;

    /// <summary>What every Paladin hit on an elite or a boss is multiplied by.</summary>
    public float EliteMultiplier => 1f + Tree.EliteDamage;

    /// <summary>The chance to block a blow, capped at <see cref="MaxBlockChance"/>. Braced Stance adds more standing still, Sanctuary more in a holy circle.</summary>
    public float BlockChance(bool standingStill, bool inCircle) => MathF.Min(MaxBlockChance,
        (BaseBlockChance + 0.04f * LevelOf(PaladinUpgrade.ShieldTraining) + Tree.BlockChance + Items.BlockChance
         + (standingStill ? Tree.StillBlock : 0f)
         + (inCircle && Tree.Sanctuary ? SanctuaryBlock : 0f))
        * Items.BlockMultiplier);

    /// <summary>Seconds for the shield rush to recharge.</summary>
    public float RushCooldown => BaseRushCooldown / (1f + Items.DashRecharge);

    public bool HasThorns => Tree.CrownOfThorns;

    /// <summary>Damage each thorns strike does, or 0 without thorns.</summary>
    public float Thorns => !HasThorns ? 0f
        : (BaseThorns + Tree.Thorns + Tree.ThornsFromHealth * MaxHealth)
          * (1f + 0.40f * LevelOf(PaladinUpgrade.BarbedPlating) + Items.Damage + Tree.ThornsBonus + (Tree.CrownOfBriars ? BriarsBonus : 0f))
          * Items.DamageMultiplier;

    public float ThornsInterval => BaseThornsInterval / (1f + Tree.ThornsSpeed);

    public float MaxHealth => BaseMaxHealth + 20f * LevelOf(PaladinUpgrade.HeavyPlate) + Items.MaxHealth + Tree.MaxHealth;

    /// <summary>Health back per second, always on - more while low, with Desperate Prayer.</summary>
    public float Regeneration(bool low) =>
        (0.5f * LevelOf(PaladinUpgrade.PrayerOfMending) + Items.Regeneration + Tree.Regeneration) * (low ? 1f + Tree.LowRegen : 1f);

    public float DamageTaken(bool inCircle) => Items.DamageTaken * Tree.DamageTaken * (inCircle && Tree.Sanctuary ? SanctuaryDamageTaken : 1f);

    public float MoveSpeed => BaseMoveSpeed * (1f + 0.08f * LevelOf(PaladinUpgrade.PilgrimsStride) + Items.MoveSpeed);

    public float PickupRadius => BasePickupRadius * (1f + 0.35f * LevelOf(PaladinUpgrade.Gleaner) + Items.Pickup);
}
