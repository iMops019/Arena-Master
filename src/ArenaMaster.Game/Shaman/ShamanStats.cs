using ArenaMaster.Game.Items;

namespace ArenaMaster.Game.Shaman;

/// <summary>
/// The Shaman's numbers for the current run: the base values, the run's upgrades (see <see cref="ShamanUpgrades"/>), the items carried (<see cref="Items"/>) and
/// Lightning Alignment (<see cref="Tree"/>). Upgrade, item and tree bonuses add together; item multipliers then multiply the lot. Items speak in general terms,
/// and here "damage" means lightning damage, "attack speed" cast speed, "projectiles" more balls, "area" the zaps and forks, "duration" the ball's life.
/// </summary>
internal sealed class ShamanStats
{
    public const float BaseMaxHealth = 100f;
    public const float BaseMoveSpeed = 7f;
    public const float BasePickupRadius = 3f;

    /// <summary>Rolling Lightning: seconds between casts, balls per cast, a ball's damage to what it rolls into, its radius, bounces and life.</summary>
    public const float BaseCastInterval = 1.3f;
    public const int BaseBalls = 1;
    public const float BaseBallDamage = 22f;
    public const float BaseBallRadius = 0.35f;
    public const int BaseBounces = 4;
    public const float BaseLifetime = 4f;

    /// <summary>The lob: how fast the ball travels across the ground, how hard it falls, how much of its bounce it keeps, and the least bounce it ever has.</summary>
    public const float ThrowSpeed = 12f;
    public const float Gravity = 22f;
    public const float Restitution = 0.62f;
    public const float MinBounceSpeed = 5f;

    /// <summary>How far and how near the ball can be lobbed, and where it goes with the crosshair on the sky.</summary>
    public const float BaseThrowRange = 24f;
    public const float MinThrow = 3f;

    /// <summary>A bounce's zap: its reach, and its share of the ball's damage.</summary>
    public const float BaseZapRadius = 1.6f;
    public const float BaseZapShare = 0.5f;

    /// <summary>The forks: how many enemies each reaches, how far it looks, and its share of the ball's damage.</summary>
    public const int BaseForks = 2;
    public const float BaseForkRange = 6f;
    public const float BaseForkShare = 0.6f;

    public const float BaseCritChance = 0.05f;
    public const float BaseCritMultiplier = 2f;

    /// <summary>The surge on Shift: a quick crackling dash.</summary>
    public const float SurgeDuration = 0.16f;
    public const float SurgeSpeed = 22f;
    public const float BaseSurgeCooldown = 1.4f;

    /// <summary>Paralysis: how long a fork holds an enemy (elites half as long; bosses not at all).</summary>
    public const float ParalysisSeconds = 0.5f;

    /// <summary>Thunderclap: the zap's reach and damage multiplied by these.</summary>
    public const float ThunderclapRadius = 1.6f;
    public const float ThunderclapDamage = 1.5f;

    /// <summary>Supercell: every this-many-th cast; its size, damage and extra bounces.</summary>
    public const int SupercellEvery = 5;
    public const float SupercellSize = 2f;
    public const float SupercellDamage = 2f;
    public const int SupercellBounces = 3;

    /// <summary>Lightning Rod: how long a struck tree or rock stays charged, how far it zaps, how often, and its share of the ball's damage per zap.</summary>
    public const float RodSeconds = 4f;
    public const float RodRadius = 4f;
    public const float RodTick = 0.5f;
    public const float RodShare = 0.4f;

    /// <summary>Eye of the Storm: its reach, how often it shocks, and its share of the ball's damage per shock.</summary>
    public const float EyeRadius = 3f;
    public const float EyeTick = 0.5f;
    public const float EyeShare = 0.3f;

    /// <summary>Lightning Reflexes: the least time between two free surges.</summary>
    public const float ReflexesCooldown = 5f;

    /// <summary>Wrath of the Thunder God: a fading ball's burst.</summary>
    public const float ThunderGodRadius = 5f;
    public const float ThunderGodShare = 3f;

    /// <summary>Living Current: how many a kill forks to.</summary>
    public const int LivingCurrentForks = 2;

    /// <summary>Call Lightning: how often, how many, how far it looks, and its share of the ball's damage.</summary>
    public const float CallInterval = 8f;
    public const int CallStrikes = 5;
    public const float CallRange = 15f;
    public const float CallShare = 4f;

    private readonly Dictionary<ShamanUpgrade, int> _levels = new();

    /// <summary>What the items carried this run add up to.</summary>
    public ItemBonuses Items { get; set; } = new();

    /// <summary>What Lightning Alignment's ranks add up to.</summary>
    public AlignmentBonuses Tree { get; set; } = new();

    public int LevelOf(ShamanUpgrade upgrade) => _levels.GetValueOrDefault(upgrade);

    /// <summary>Takes one more level of <paramref name="upgrade"/> (no further than its maximum).</summary>
    public void Increase(ShamanUpgrade upgrade)
    {
        int level = LevelOf(upgrade);
        if (level < ShamanUpgrades.Info(upgrade).MaxLevel)
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

    public float BallDamage =>
        BaseBallDamage * (1f + 0.20f * LevelOf(ShamanUpgrade.ChargedCore) + Items.Damage + Tree.LightningDamage) * Items.DamageMultiplier * Items.ProjectileDamage;

    public float CastInterval =>
        BaseCastInterval
        / (MathF.Max(0.2f, 1f + 0.12f * LevelOf(ShamanUpgrade.SwiftCasting) + Items.AttackSpeedNow + Tree.CastSpeed) * Items.AttackSpeedMultiplier);

    public int Balls => BaseBalls + LevelOf(ShamanUpgrade.TwinSpheres) + Items.Projectiles;

    public float BallRadius => BaseBallRadius * (1f + Tree.BallSize + 0.20f * LevelOf(ShamanUpgrade.HeavySphere));

    public int Bounces => BaseBounces + LevelOf(ShamanUpgrade.Resonance) + Tree.Bounces;

    public float Lifetime => BaseLifetime + Tree.Lifetime + Items.Duration + LevelOf(ShamanUpgrade.StormBolt);

    public float ThrowRange => BaseThrowRange * (1f + Items.Range);

    /// <summary>How fast the ball travels across the ground: faster with an item's projectile speed.</summary>
    public float ThrowSpeedNow => ThrowSpeed * (1f + Items.ProjectileSpeed);

    public float ZapRadius =>
        BaseZapRadius * (1f + 0.15f * LevelOf(ShamanUpgrade.StaticField) + Items.Area) * Items.AreaMultiplier * (Tree.Thunderclap ? ThunderclapRadius : 1f);

    public float ZapDamage => BallDamage * BaseZapShare * (1f + 0.25f * LevelOf(ShamanUpgrade.StaticField) + Tree.ZapDamage) * (Tree.Thunderclap ? ThunderclapDamage : 1f);

    public int Forks => BaseForks + LevelOf(ShamanUpgrade.BranchingBolts) + Tree.Forks + Items.Chains;

    public float ForkRange => BaseForkRange * (1f + 0.20f * LevelOf(ShamanUpgrade.LongReach) + Tree.ForkRange + Items.Area) * Items.AreaMultiplier;

    public float ForkDamage => BallDamage * BaseForkShare * (1f + Tree.ForkDamage + 0.20f * LevelOf(ShamanUpgrade.Conductive));

    public float RodDamage => BallDamage * RodShare * (1f + Tree.RodDamage + 0.20f * LevelOf(ShamanUpgrade.RodMastery));

    /// <summary>How long a struck tree or rock stays charged.</summary>
    public float RodDuration => RodSeconds + 1.5f * LevelOf(ShamanUpgrade.RodMastery);

    /// <summary>What the fixed areas (the rods, the eye, the Thunder God's burst) are scaled by: an item's area.</summary>
    public float AreaScale => (1f + Items.Area) * Items.AreaMultiplier;

    public float CritChance => BaseCritChance + 0.06f * LevelOf(ShamanUpgrade.Overcharge) + Items.CritChance + Tree.CritChance;

    public float CritMultiplier => BaseCritMultiplier + 0.15f * LevelOf(ShamanUpgrade.Overcharge) + Items.CritDamage + Tree.CritDamage;

    public float EliteMultiplier => 1f + Tree.EliteDamage;

    public float MaxHealth => BaseMaxHealth + 20f * LevelOf(ShamanUpgrade.EarthenHide) + Items.MaxHealth + Tree.MaxHealth;

    public float Regeneration => Items.Regeneration + Tree.Regeneration;

    public float DamageTaken => Items.DamageTaken * Tree.DamageTaken * MathF.Pow(0.94f, LevelOf(ShamanUpgrade.Insulation));

    public float MoveSpeed => BaseMoveSpeed * (1f + 0.08f * LevelOf(ShamanUpgrade.Stormstride) + Items.MoveSpeed + Tree.MoveSpeed);

    public float PickupRadius => BasePickupRadius * (1f + 0.35f * LevelOf(ShamanUpgrade.Magnetism) + Items.Pickup);

    public float SurgeCooldown => BaseSurgeCooldown / (1f + Tree.SurgeRecharge + Items.DashRecharge);

    /// <summary>The block chance items give (the Shaman has no shield).</summary>
    public float BlockChance => MathF.Min(0.6f, Items.BlockChance * Items.BlockMultiplier);
}
