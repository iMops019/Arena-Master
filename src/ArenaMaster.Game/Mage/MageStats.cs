using ArenaMaster.Game.Items;

namespace ArenaMaster.Game.Mage;

/// <summary>What the Mage's barrage is made of: set by the active tree (Frost chills, Pyromancy burns).</summary>
internal enum MageElement
{
    Frost,
    Fire,
}

/// <summary>
/// The Mage's numbers for the current run: the base values, the run's upgrades (see <see cref="MageUpgrades"/>), the items carried (<see cref="Items"/>) and the
/// active tree - the Frost tree (<see cref="Tree"/>) or the Pyromancy tree (<see cref="Pyro"/>), the other one left empty. Upgrade, item and tree bonuses add
/// together; item multipliers then multiply the lot. Items speak in general terms, and here "damage" means the barrage's cold or fire damage, "attack speed" cast
/// speed, and "critical" a bolt's crit.
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
    public const int BaseProjectiles = 3;
    public const float BoltStagger = 0.07f;
    public const float BaseBarrageInterval = 1.8f;
    public const float BaseBoltDamage = 15f;
    public const float BaseBoltSpeed = 24f;
    public const float BaseRange = 30f;
    public const float BaseTargetRange = 22f;

    /// <summary>
    /// The class's own critical chance. Everything else only scales it: the level-up, the tree and items add <i>increased</i> critical chance, so +100% doubles
    /// the base.
    /// </summary>
    public const float BaseCritChance = 0.08f;
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

    /// <summary>Fire Barrage: every hit sets the enemy burning for this share of the hit's damage over the base burn time, ticking this often.</summary>
    public const float BaseBurnShare = 0.3f;
    public const float BaseBurnDuration = 3f;
    public const float BurnTick = 0.25f;

    /// <summary>Heat: the most there is, what each cast builds, how much cools each second, the damage each point gives, and how long an overheat stops the casting.</summary>
    public const float MaxHeat = 100f;
    public const float HeatPerCast = 10f;
    public const float BaseHeatCooling = 5f;
    public const float HeatDamagePerPoint = 0.005f;
    public const float OverheatSeconds = 1.5f;

    /// <summary>Combustion: a burning enemy's death bursts this far, for this many bolts' damage.</summary>
    public const float BaseCombustionRadius = 2.5f;
    public const float CombustionShare = 1f;

    /// <summary>Wildfire: how far a burn spreads, and how often.</summary>
    public const float WildfireRange = 4f;
    public const float WildfireEvery = 1f;

    /// <summary>Fire on the ground (Fire Walk's line, a meteor's crater): bolt damages a second to what stands in it, and how often it bites.</summary>
    public const float GroundFireShare = 1f;
    public const float GroundTick = 0.5f;

    /// <summary>Fire Walk: how long the line burns, and how far either side of it the fire reaches.</summary>
    public const float FireWalkSeconds = 3f;
    public const float FireWalkWidth = 1f;

    /// <summary>Fireball: its radius, and its share of the bolt's hit.</summary>
    public const float BaseFireballRadius = 2f;
    public const float BaseFireballShare = 0.4f;

    /// <summary>Flame Ward: seconds between wards, and the burn its attacker gets, as if from a hit of this many bolts.</summary>
    public const float BaseFlameWardInterval = 10f;
    public const float FlameWardBurn = 2f;

    /// <summary>Scorch: extra bolt damage to a burning enemy.</summary>
    public const float ScorchBonus = 0.3f;

    /// <summary>Cauterise: heat vented for each point of health healed.</summary>
    public const float CauteriseHeatPerHealth = 4f;

    /// <summary>Living Flame: a burn's ticks grow this much for each second it has burned, counting no more than this many seconds.</summary>
    public const float LivingFlameGrowth = 0.25f;
    public const float LivingFlameMaxSeconds = 8f;

    /// <summary>Meteor: every this-many-th barrage, this many bolts' damage within this radius, after this long falling, and the ground left burning this long.</summary>
    public const int MeteorEvery = 4;
    public const float MeteorDamage = 5f;
    public const float MeteorRadius = 4f;
    public const float MeteorFall = 0.8f;
    public const float MeteorGroundSeconds = 3f;

    /// <summary>Inferno: the firestorm's reach, and how often it bites (a bolt's damage each time).</summary>
    public const float InfernoRadius = 5f;
    public const float InfernoTick = 0.5f;

    /// <summary>The blink on Shift: a very short, very fast push.</summary>
    public const float BlinkDuration = 0.12f;
    public const float BlinkSpeed = 38f;
    public const float BlinkCooldown = 2.2f;

    private readonly Dictionary<MageUpgrade, int> _levels = new();

    /// <summary>What the items carried this run add up to.</summary>
    public ItemBonuses Items { get; set; } = new();

    /// <summary>What the Frost tree's ranks add up to (empty while Pyromancy is active).</summary>
    public FrostBonuses Tree { get; set; } = new();

    /// <summary>What the Pyromancy tree's ranks add up to (empty while Frost is active).</summary>
    public PyromancyBonuses Pyro { get; set; } = new();

    /// <summary>What the barrage is made of: Frost unless Pyromancy is the active tree.</summary>
    public MageElement Element { get; set; } = MageElement.Frost;

    /// <summary>Whether the barrage is the Fire Barrage (Pyromancy active): its hits burn rather than chill.</summary>
    public bool Fire => Element == MageElement.Fire;

    /// <summary>The heat right now (0 to <see cref="MaxHeat"/>), set by the fire every frame (see <see cref="Flames"/>); always 0 without the Heat major.</summary>
    public float Heat { get; set; }

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
        Heat = 0f;
    }

    public int Projectiles => BaseProjectiles + LevelOf(MageUpgrade.SplinterBolt) + Tree.Projectiles + Pyro.Projectiles + Items.Projectiles;

    public float BoltDamage =>
        BaseBoltDamage * (1f + 0.20f * LevelOf(MageUpgrade.IceShards) + Items.Damage + Tree.ColdDamage + Pyro.FireDamage + HeatBonus) * Items.DamageMultiplier
        * Items.ProjectileDamage;

    /// <summary>What the heat adds to damage right now (Heat: +0.5% a point).</summary>
    public float HeatBonus => Pyro.Heat ? Heat * (HeatDamagePerPoint + Pyro.HeatDamage) : 0f;

    public float BarrageInterval =>
        BaseBarrageInterval
        / (MathF.Max(0.2f, 1f + 0.12f * LevelOf(MageUpgrade.QuickenedCasting) + Items.AttackSpeedNow + Tree.CastSpeed + Pyro.CastSpeed) * Items.AttackSpeedMultiplier);

    public float BoltSpeed => BaseBoltSpeed * (1f + 0.20f * LevelOf(MageUpgrade.WinterWind) + Tree.ProjectileSpeed + Pyro.ProjectileSpeed + Items.ProjectileSpeed);

    public float Range => BaseRange * (1f + 0.15f * LevelOf(MageUpgrade.WinterWind) + Tree.Range + Pyro.Range + Items.Range);

    /// <summary>How far away the barrage finds enemies to aim at: grows with range.</summary>
    public float TargetRange => BaseTargetRange * (1f + 0.15f * LevelOf(MageUpgrade.WinterWind) + Tree.Range + Pyro.Range + Items.Range);

    /// <summary>How many enemies a bolt passes through: an item's chains count as pierce for the Mage.</summary>
    public int Pierce => LevelOf(MageUpgrade.PiercingIce) + Tree.Pierce + Pyro.Pierce + Items.Chains;

    public float CritChance => BaseCritChance * MathF.Max(0f, 1f + 0.20f * LevelOf(MageUpgrade.FrozenPrecision) + Items.CritChance + Tree.CritChance + Pyro.CritChance);

    /// <summary>How many times normal damage a critical bolt does.</summary>
    public float CritMultiplier => BaseCritMultiplier + 0.15f * LevelOf(MageUpgrade.FrozenPrecision) + Items.CritDamage + Tree.CritDamage + Pyro.CritDamage;

    /// <summary>How much a chill slows an enemy's walk (0 to 1), capped at <see cref="MaxChill"/>.</summary>
    public float Chill => MathF.Min(MaxChill, BaseChill + 0.08f * LevelOf(MageUpgrade.NumbingCold) + Tree.Chill + Items.ChillOnHit);

    public float ChillDuration => BaseChillDuration + 0.3f * LevelOf(MageUpgrade.NumbingCold) + Tree.ChillDuration + Items.Duration;

    /// <summary>What a hit on a chilled (or frozen) enemy is multiplied by.</summary>
    public float ChilledMultiplier => 1f + Tree.ChilledDamage + 0.20f * LevelOf(MageUpgrade.GlacialSpikes);

    public float FreezeChance => Tree.DeepFreeze ? BaseFreezeChance + Tree.FreezeChance + 0.03f * LevelOf(MageUpgrade.DeepChill) : 0f;

    public float FreezeDuration => BaseFreezeDuration + Tree.FreezeDuration + 0.3f * LevelOf(MageUpgrade.DeepChill);

    public float EliteMultiplier => 1f + Tree.EliteDamage + Pyro.EliteDamage + 0.15f * LevelOf(MageUpgrade.Shatterpoint);

    public float BlastRadius => BaseBlastRadius * (1f + 0.25f * LevelOf(MageUpgrade.ConcussiveFrost) + Tree.BlastRadius + Items.Area) * Items.AreaMultiplier;

    /// <summary>What the fixed areas (the comet's blast, the blizzard, Shattering Ward's burst) are scaled by: an item's area.</summary>
    public float AreaScale => (1f + Items.Area) * Items.AreaMultiplier;

    public float BlastShare => BaseBlastDamage * (1f + Tree.BlastDamage + 0.25f * LevelOf(MageUpgrade.BlastPower));

    public float ShieldAmount => (ShieldFlat + ShieldShare * MaxHealth) * (1f + 0.30f * LevelOf(MageUpgrade.GlacialWard) + Tree.ShieldStrength + Items.WardShield);

    public float ShieldDuration => BaseShieldDuration + Tree.ShieldDuration + Items.Duration;

    /// <summary>The block chance items give (the Mage has no shield to block with).</summary>
    public float BlockChance => MathF.Min(0.6f, Items.BlockChance * Items.BlockMultiplier);

    /// <summary>Seconds for the blink to recharge.</summary>
    public float BlinkRecharge => BlinkCooldown / (1f + Items.DashRecharge);

    /// <summary>Seconds for the Frost Shield to form again after the last one ended.</summary>
    public float ShieldInterval => BaseShieldInterval / (1f + Tree.ShieldRecharge + (Tree.GlacialFortress ? FortressRecharge : 0f));

    public float MaxHealth =>
        (BaseMaxHealth + 15f * LevelOf(MageUpgrade.ArcaneVigor) + Items.MaxHealth + Tree.MaxHealth + Pyro.MaxHealth) * Items.MaxHealthMultiplier;

    public float Regeneration => Items.Regeneration + Tree.Regeneration + Pyro.Regeneration;

    public float DamageTaken => Items.DamageTaken * Tree.DamageTaken * Pyro.DamageTaken * MathF.Pow(0.94f, LevelOf(MageUpgrade.IceArmor));

    public float MoveSpeed => BaseMoveSpeed * (1f + 0.08f * LevelOf(MageUpgrade.FleetStep) + Items.MoveSpeed);

    public float PickupRadius => BasePickupRadius * (1f + 0.35f * LevelOf(MageUpgrade.Attunement) + Items.Pickup);

    /// <summary>
    /// A burn's share of the hit that lit it (30%, more with burn damage), before damage over time from items: the burn does that much over
    /// <see cref="BaseBurnDuration"/>, and keeps burning at the same rate for as long as it lasts.
    /// </summary>
    public float BurnShare => BaseBurnShare * (1f + Pyro.BurnDamage + 0.20f * LevelOf(MageUpgrade.FanTheFlames));

    /// <summary>How long a burn lasts.</summary>
    public float BurnDuration => BaseBurnDuration + Pyro.BurnDuration + 0.5f * LevelOf(MageUpgrade.LastingEmbers) + Items.Duration;

    /// <summary>Heat lost each second.</summary>
    public float HeatCooling => BaseHeatCooling + Pyro.HeatCooling + 1.5f * LevelOf(MageUpgrade.CoolingBreath);

    /// <summary>Combustion's burst: its damage and its reach.</summary>
    public float CombustionDamage => BoltDamage * CombustionShare * (1f + Pyro.CombustionDamage + 0.25f * LevelOf(MageUpgrade.BlastingAsh));

    public float CombustionRadius => BaseCombustionRadius * (1f + Pyro.CombustionRadius + 0.10f * LevelOf(MageUpgrade.BlastingAsh)) * AreaScale;

    /// <summary>Fireball: how far a bolt's burst reaches, and its share of the hit.</summary>
    public float FireballRadius => BaseFireballRadius * (1f + Pyro.FireballRadius + Items.Area) * Items.AreaMultiplier;

    public float FireballShare => BaseFireballShare * (1f + Pyro.FireballDamage);

    /// <summary>What fire on the ground does each second to what stands in it (damage over time).</summary>
    public float GroundFireDamage => BoltDamage * GroundFireShare * Items.OverTime;

    /// <summary>Seconds from one Flame Ward to the next.</summary>
    public float FlameWardInterval => BaseFlameWardInterval - 1.5f * LevelOf(MageUpgrade.RekindledWard);
}
