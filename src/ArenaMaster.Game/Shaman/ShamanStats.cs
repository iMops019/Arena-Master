using ArenaMaster.Game.Items;

namespace ArenaMaster.Game.Shaman;

/// <summary>What the Shaman throws, which the active tree decides: Lightning Alignment's ball of lightning, or Earth Alignment's stone.</summary>
internal enum ShamanElement
{
    Lightning,
    Stone,
}

/// <summary>
/// The Shaman's numbers for the current run: the base values, the run's upgrades (see <see cref="ShamanUpgrades"/>), the items carried (<see cref="Items"/>) and
/// the active tree: Lightning Alignment (<see cref="Tree"/>) or Earth Alignment (<see cref="Earth"/>) - the other one's bonuses are always empty, so both can
/// simply be added in. Upgrade, item and tree bonuses add together; item multipliers then multiply the lot. Items speak in general terms, and here "damage"
/// means lightning or stone damage, "attack speed" cast speed, "projectiles" more balls or stones, "area" the zaps and forks or the quakes and cracks, "duration"
/// the ball's or stone's life, and "chains" a fork each, or for the stone an enemy it can bounce off without spending a bounce.
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

    /// <summary>
    /// The throw (made flat and quick 2026-09-29, the user's call: the old lob went up so high and hung so long it missed): the ball leaves the hand at this
    /// speed across the ground, and this much faster for each metre it is thrown, so a near throw is flung down at the ground and a far one is a low, quick
    /// arc (a 12 m throw tops out about half a metre above the hand and lands in 0.6 s; 24 m, about 1.7 m and 0.9 s). Projectile speed makes it quicker still.
    /// </summary>
    public const float ThrowBase = 12f;
    public const float ThrowPerMetre = 0.6f;

    /// <summary>How fast across the ground a throw of <paramref name="distance"/> metres goes.</summary>
    public float ThrowSpeedAt(float distance) => (ThrowBase + ThrowPerMetre * distance) * (1f + Items.ProjectileSpeed);

    /// <summary>The lob: how fast a surge's ball rolls off across the ground, how hard a ball falls, how much of its bounce it keeps, and the least bounce it ever has.</summary>
    public const float ThrowSpeed = 12f;
    public const float Gravity = 22f;
    public const float Restitution = 0.62f;
    public const float MinBounceSpeed = 5f;

    /// <summary>How far and how near the ball can be thrown.</summary>
    public const float BaseThrowRange = 24f;
    public const float MinThrow = 1.5f;

    /// <summary>A bounce's zap: its reach, and its share of the ball's damage.</summary>
    public const float BaseZapRadius = 1.6f;
    public const float BaseZapShare = 0.5f;

    /// <summary>The forks: how many enemies each reaches, how far it looks, and its share of the ball's damage.</summary>
    public const int BaseForks = 2;
    public const float BaseForkRange = 6f;
    public const float BaseForkShare = 0.6f;

    /// <summary>
    /// The class's own critical chance. Everything else only scales it: the level-up, the tree and items add <i>increased</i> critical chance, so +100% doubles
    /// the base.
    /// </summary>
    public const float BaseCritChance = 0.07f;
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

    /// <summary>
    /// Earth Alignment's stone: its damage to what it strikes. It does no zapping and no forking, which were most of the lightning's damage, so its own hit is
    /// much harder than the ball's (worked out in EarthAlignmentTests: into a crowd a fresh stone does about what a fresh ball of lightning does).
    /// </summary>
    public const float BaseStoneDamage = 34f;

    /// <summary>How far a stone knocks back what it strikes (elites half as far, bosses not at all), and how quickly the shove is over.</summary>
    public const float BaseKnockback = 1.5f;
    public const float KnockbackSeconds = 0.15f;

    /// <summary>A stone's quake on each bounce: its reach, and its share of the stone's damage.</summary>
    public const float BaseQuakeRadius = 1.8f;
    public const float BaseQuakeShare = 0.6f;

    /// <summary>Aftershock: how long a crack waits before it bursts, how far the burst reaches, and its share of the stone's damage.</summary>
    public const float CrackDelay = 1f;
    public const float BaseCrackRadius = 2f;
    public const float CrackShare = 1f;

    /// <summary>Stoneskin: how much damage taken each second of standing still takes off, the most it takes off, and how fast moving wears it away.</summary>
    public const float StoneskinGain = 0.05f;
    public const float BaseStoneskinMax = 0.30f;
    public const float StoneskinLoss = 0.10f;

    /// <summary>Tremor: how long a quake staggers an enemy (elites half as long; bosses not at all).</summary>
    public const float TremorSeconds = 0.5f;

    /// <summary>Gathering Weight: what each bounce adds to the stone's damage.</summary>
    public const float WeightPerBounce = 0.2f;

    /// <summary>Rumbling Earth: the quake's reach and damage multiplied by these.</summary>
    public const float RumblingRadius = 1.5f;
    public const float RumblingDamage = 1.5f;

    /// <summary>Earthen Totem: how long a totem stands and how far it draws enemies from.</summary>
    public const float BaseTotemSeconds = 4f;
    public const float BaseTotemReach = 6f;

    /// <summary>Shatter: how far a stone's rubble flies, and its share of the stone's damage.</summary>
    public const float ShatterRadius = 2.5f;
    public const float ShatterShare = 1.5f;

    /// <summary>Upheaval: how far the heave reaches, its share of the stone's damage, and the least time between two.</summary>
    public const float UpheavalRadius = 3f;
    public const float UpheavalShare = 1.5f;
    public const float UpheavalCooldown = 1f;

    /// <summary>Avalanche: every this-many-th cast; how many more stones it throws than a cast would, and how far apart they are fanned.</summary>
    public const int AvalancheEvery = 6;
    public const int AvalancheExtra = 4;
    public const float AvalancheSpreadDegrees = 12f;

    /// <summary>Tectonic Rift: how long a rift stays open, how wide it is each side of its line, how often it bites, and its share of the stone's damage a second.</summary>
    public const float BaseRiftSeconds = 3f;
    public const float RiftHalfWidth = 0.8f;
    public const float RiftTick = 0.5f;
    public const float RiftShare = 0.5f;

    private readonly Dictionary<ShamanUpgrade, int> _levels = new();

    /// <summary>What the items carried this run add up to.</summary>
    public ItemBonuses Items { get; set; } = new();

    /// <summary>What Lightning Alignment's ranks add up to (empty with Earth Alignment active).</summary>
    public AlignmentBonuses Tree { get; set; } = new();

    /// <summary>What Earth Alignment's ranks add up to (empty with Lightning Alignment active).</summary>
    public EarthBonuses Earth { get; set; } = new();

    /// <summary>What the Shaman throws: set with the active tree.</summary>
    public ShamanElement Element { get; set; } = ShamanElement.Lightning;

    /// <summary>Whether the Shaman throws stones (Earth Alignment is active).</summary>
    public bool Stone => Element == ShamanElement.Stone;

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

    /// <summary>What the damage of either element is raised by: the level-up's damage card, the items and the active tree.</summary>
    private float DamageBonus => 1f + 0.20f * LevelOf(ShamanUpgrade.ChargedCore) + Items.Damage + Tree.LightningDamage + Earth.StoneDamage;

    public float BallDamage => BaseBallDamage * DamageBonus * Items.DamageMultiplier * Items.ProjectileDamage;

    public float CastInterval =>
        BaseCastInterval
        / (MathF.Max(0.2f, 1f + 0.12f * LevelOf(ShamanUpgrade.SwiftCasting) + Items.AttackSpeedNow + Tree.CastSpeed + Earth.CastSpeed) * Items.AttackSpeedMultiplier);

    /// <summary>Balls, or stones, per cast.</summary>
    public int Balls => BaseBalls + LevelOf(ShamanUpgrade.TwinSpheres) + Items.Projectiles;

    public float BallRadius => BaseBallRadius * (1f + Tree.BallSize + 0.20f * LevelOf(ShamanUpgrade.HeavySphere));

    /// <summary>Bounces, the ball's or the stone's.</summary>
    public int Bounces => BaseBounces + LevelOf(ShamanUpgrade.Resonance) + Tree.Bounces + Earth.Bounces;

    /// <summary>How long a ball or stone lasts.</summary>
    public float Lifetime => BaseLifetime + Tree.Lifetime + Earth.Lifetime + Items.Duration + LevelOf(ShamanUpgrade.StormBolt);

    /// <summary>The stone's damage to what it strikes (its quakes, cracks and rifts are shares of it).</summary>
    public float StoneDamage => BaseStoneDamage * DamageBonus * Items.DamageMultiplier * Items.ProjectileDamage;

    /// <summary>The stone's radius: the ball's to start, and grown by the stone's own size bonuses.</summary>
    public float StoneRadius => BaseBallRadius * (1f + Earth.StoneSize + 0.20f * LevelOf(ShamanUpgrade.HeavySphere));

    /// <summary>How far the stone knocks back what it strikes.</summary>
    public float Knockback => BaseKnockback * (1f + Earth.Knockback + 0.25f * LevelOf(ShamanUpgrade.BruteStrength));

    /// <summary>How many enemies a stone may bounce off without spending a bounce: an item's chains.</summary>
    public int FreeRebounds => Items.Chains;

    public float QuakeRadius =>
        BaseQuakeRadius * (1f + Earth.QuakeSize + 0.15f * LevelOf(ShamanUpgrade.RumblingGround) + Items.Area) * Items.AreaMultiplier
        * (Earth.RumblingEarth ? RumblingRadius : 1f);

    /// <summary>A quake's damage as a share of the stone's (the stone's own, which Gathering Weight raises as it goes).</summary>
    public float QuakeShare =>
        BaseQuakeShare * (1f + Earth.QuakeDamage + 0.25f * LevelOf(ShamanUpgrade.RumblingGround)) * (Earth.RumblingEarth ? RumblingDamage : 1f);

    public float QuakeDamage => StoneDamage * QuakeShare;

    public float CrackRadius => BaseCrackRadius * (1f + 0.15f * LevelOf(ShamanUpgrade.DeepCracks) + Items.Area) * Items.AreaMultiplier;

    /// <summary>An Aftershock crack's burst as a share of the stone's damage.</summary>
    public float CrackDamageShare => CrackShare * (1f + Earth.CrackDamage + 0.30f * LevelOf(ShamanUpgrade.DeepCracks));

    /// <summary>The most Stoneskin takes off damage taken, and how fast it builds a second standing still.</summary>
    public float StoneskinMax => BaseStoneskinMax + 0.05f * LevelOf(ShamanUpgrade.SturdyStance);

    public float StoneskinRate => StoneskinGain * (1f + 0.25f * LevelOf(ShamanUpgrade.SturdyStance)) * (Earth.WalkingMountain ? 2f : 1f);

    /// <summary>Whether Stoneskin is on: its major, or Walking Mountain, which brings it.</summary>
    public bool HasStoneskin => Earth.Stoneskin || Earth.WalkingMountain;

    public float TotemSeconds => BaseTotemSeconds + Earth.TotemTime + LevelOf(ShamanUpgrade.TotemCarving);

    public float TotemReach => BaseTotemReach + LevelOf(ShamanUpgrade.TotemCarving);

    public float RiftSeconds => BaseRiftSeconds + LevelOf(ShamanUpgrade.WideningRift);

    /// <summary>A rift's bite a second as a share of the stone's damage (damage over time, so the items' damage over time raises it).</summary>
    public float RiftDamageShare => RiftShare * (1f + 0.20f * LevelOf(ShamanUpgrade.WideningRift)) * Items.OverTime;

    public float ThrowRange => BaseThrowRange * (1f + Items.Range);

    /// <summary>How fast the ball travels across the ground: faster with an item's projectile speed.</summary>
    public float ThrowSpeedNow => ThrowSpeed * (1f + Items.ProjectileSpeed);

    public float ZapRadius =>
        BaseZapRadius * (1f + 0.15f * LevelOf(ShamanUpgrade.StaticField) + Items.Area) * Items.AreaMultiplier * (Tree.Thunderclap ? ThunderclapRadius : 1f);

    public float ZapDamage => BallDamage * BaseZapShare * (1f + 0.25f * LevelOf(ShamanUpgrade.StaticField) + Tree.ZapDamage) * (Tree.Thunderclap ? ThunderclapDamage : 1f) * Items.OverTime;

    public int Forks => BaseForks + LevelOf(ShamanUpgrade.BranchingBolts) + Tree.Forks + Items.Chains;

    public float ForkRange => BaseForkRange * (1f + 0.20f * LevelOf(ShamanUpgrade.LongReach) + Tree.ForkRange + Items.Area) * Items.AreaMultiplier;

    public float ForkDamage => BallDamage * BaseForkShare * (1f + Tree.ForkDamage + 0.20f * LevelOf(ShamanUpgrade.Conductive));

    public float RodDamage => BallDamage * RodShare * (1f + Tree.RodDamage + 0.20f * LevelOf(ShamanUpgrade.RodMastery)) * Items.OverTime;

    /// <summary>How long a struck tree or rock stays charged.</summary>
    public float RodDuration => RodSeconds + 1.5f * LevelOf(ShamanUpgrade.RodMastery);

    /// <summary>What the fixed areas (the rods, the eye, the Thunder God's burst) are scaled by: an item's area.</summary>
    public float AreaScale => (1f + Items.Area) * Items.AreaMultiplier;

    public float CritChance =>
        BaseCritChance * MathF.Max(0f, 1f + 0.20f * LevelOf(ShamanUpgrade.Overcharge) + Items.CritChance + Tree.CritChance + Earth.CritChance);

    public float CritMultiplier => BaseCritMultiplier + 0.15f * LevelOf(ShamanUpgrade.Overcharge) + Items.CritDamage + Tree.CritDamage + Earth.CritDamage;

    public float EliteMultiplier => 1f + Tree.EliteDamage + Earth.EliteDamage;

    public float MaxHealth =>
        (BaseMaxHealth + 20f * LevelOf(ShamanUpgrade.EarthenHide) + Items.MaxHealth + Tree.MaxHealth + Earth.MaxHealth) * Items.MaxHealthMultiplier;

    public float Regeneration => Items.Regeneration + Tree.Regeneration + Earth.Regeneration;

    public float DamageTaken => Items.DamageTaken * Tree.DamageTaken * Earth.DamageTaken * MathF.Pow(0.94f, LevelOf(ShamanUpgrade.Insulation));

    public float MoveSpeed => BaseMoveSpeed * (1f + 0.08f * LevelOf(ShamanUpgrade.Stormstride) + Items.MoveSpeed + Tree.MoveSpeed + Earth.MoveSpeed);

    public float PickupRadius => BasePickupRadius * (1f + 0.35f * LevelOf(ShamanUpgrade.Magnetism) + Items.Pickup);

    public float SurgeCooldown => BaseSurgeCooldown / (1f + Tree.SurgeRecharge + Earth.SurgeRecharge + Items.DashRecharge);

    /// <summary>The block chance items give (the Shaman has no shield), and Earth Alignment's Stone Blood.</summary>
    public float BlockChance => MathF.Min(0.6f, (Items.BlockChance + Earth.BlockChance) * Items.BlockMultiplier);
}
