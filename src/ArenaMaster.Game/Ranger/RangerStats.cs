using ArenaMaster.Game.Items;

namespace ArenaMaster.Game.Ranger;

/// <summary>
/// What an arrow does on a hit beyond its base damage, depending on what it hit. Fractions for percentages. <paramref name="Fork"/>: its first hit splits it in two.
/// <paramref name="Splinters"/>: a kill bursts it into splinters. <paramref name="FirstHitCrits"/>: a hit on an enemy not yet hurt is always critical.
/// </summary>
internal readonly record struct HitRules(float EliteDamage, float HealthyDamage, float ExecuteChance, float ChainedDamage, float CascadeDamage,
    bool Fork = false, bool Splinters = false, bool FirstHitCrits = false)
{
    public static readonly HitRules None = default;
}

/// <summary>
/// The Ranger's numbers for the current run: the base values, the run's upgrades (see <see cref="RangerUpgrades"/>), the items carried (<see cref="Items"/>) and the
/// passive trees (<see cref="Tree"/>, the Sharpshooter's, and <see cref="Trapper"/>; only the active one's is filled, the other left empty). Upgrade, item and
/// tree bonuses add together; item multipliers then multiply the lot. Everything that fires, moves, picks up, sets a snare, poisons or sends the hawk asks here, so a
/// change takes effect the moment it is made.
/// </summary>
internal sealed class RangerStats
{
    public const float BaseDamage = 17f;
    public const float BaseFireInterval = 0.5f;
    public const float BaseMoveSpeed = 7f;
    public const float BaseArrowSpeed = 50f;
    public const float BaseRange = 60f;
    /// <summary>
    /// The class's own critical chance. Everything else only scales it: the level-up, the tree and items add <i>increased</i> critical chance, so +100% doubles
    /// the base.
    /// </summary>
    public const float BaseCritChance = 0.10f;
    public const float BaseCritMultiplier = 2f;
    public const float BasePickupRadius = 3f;
    public const float BaseMaxHealth = 100f;
    public const float BaseDashCooldown = 1.2f;
    public const float BaseDashSpeed = 20f;
    public const float BaseChainRange = 10f;

    /// <summary>Degrees between neighbouring arrows of a split shot.</summary>
    public const float SplitSpreadDegrees = 7f;

    /// <summary>What Twin Shot leaves of every arrow's damage.</summary>
    public const float TwinShotDamage = 0.85f;

    public const int MaxMomentumStacks = 15;

    /// <summary>Rain of Arrows: how often, how many (before Arrowstorm), and how wide the patch it covers.</summary>
    public const float RainInterval = 6f;
    public const int BaseRainArrows = 12;
    public const float RainRadius = 4f;

    /// <summary>Sniper's Focus: how long to stand still, and what the focused arrow's damage is multiplied by.</summary>
    public const float FocusTime = 1f;
    public const float FocusDamage = 2.5f;

    /// <summary>Endless Quiver: every this-many-th shot, a ring of this many arrows.</summary>
    public const int QuiverEvery = 10;
    public const int QuiverRing = 16;

    /// <summary>Snare Line: a snare's burst (a share of an arrow's damage) and reach, how long it holds what steps on it, how long it lies, and how many at once.</summary>
    public const float SnareBurst = 2.5f;
    public const float SnareRadius = 2.5f;
    public const float SnareHold = 1.5f;
    public const float SnareLife = 12f;
    public const int BaseMaxSnares = 6;

    /// <summary>An elite is held this share of the time (a boss not at all).</summary>
    public const float EliteHoldShare = 0.5f;

    /// <summary>Caltrops: how long they lie, how much they slow, and their bite each second (a share of the burst's damage).</summary>
    public const float CaltropTime = 4f;
    public const float CaltropSlow = 0.4f;
    public const float CaltropShare = 0.2f;

    /// <summary>Venom Tips: a poison is this share of the hit's damage over this long, and an enemy carries this many at most.</summary>
    public const float PoisonShare = 0.4f;
    public const float BasePoisonTime = 3f;
    public const int BasePoisonStacks = 5;

    /// <summary>Toxic Cloud: how wide (across) and how long.</summary>
    public const float CloudWidth = 2.5f;
    public const float BaseCloudTime = 3f;

    /// <summary>Crippling Venom: how much an enemy carrying all the poison it can is slowed.</summary>
    public const float CrippleSlow = 0.35f;

    /// <summary>Hawk Companion: how often it dives, how far it hunts, and its dive's damage (a share of an arrow's).</summary>
    public const float HawkInterval = 4f;
    public const float BaseHawkRange = 20f;
    public const float HawkShare = 2.5f;

    /// <summary>The hawk never dives more often than this, however fast it gets.</summary>
    public const float MinHawkInterval = 1f;

    /// <summary>Hawk's Mark: how much more the marked enemy takes from the Ranger, and for how long.</summary>
    public const float MarkDamage = 0.25f;
    public const float MarkTime = 4f;

    private readonly Dictionary<RangerUpgrade, int> _levels = new();

    /// <summary>What the items carried this run add up to. The content hands in a fresh one whenever an item is gained.</summary>
    public ItemBonuses Items { get; set; } = new();

    /// <summary>What the passive tree's ranks add up to. Set at the start of each run.</summary>
    public SharpshooterBonuses Tree { get; set; } = new();

    /// <summary>What the Trapper tree's ranks add up to: empty unless it is the active tree.</summary>
    public TrapperBonuses Trapper { get; set; } = new();

    /// <summary>Whether the Trapper is the active tree (its level-up cards are offered only then).</summary>
    public bool TrapperActive { get; set; }

    /// <summary>Kills in the last few seconds, for Momentum (set by the content each frame).</summary>
    public int MomentumStacks { get; set; }

    public int LevelOf(RangerUpgrade upgrade) => _levels.GetValueOrDefault(upgrade);

    /// <summary>Takes one more level of <paramref name="upgrade"/> (no further than its maximum).</summary>
    public void Increase(RangerUpgrade upgrade)
    {
        int level = LevelOf(upgrade);
        if (level < RangerUpgrades.Info(upgrade).MaxLevel)
        {
            _levels[upgrade] = level + 1;
        }
    }

    /// <summary>A new run: no upgrades, no items. The tree is left as it is (the content sets it).</summary>
    public void Reset()
    {
        _levels.Clear();
        Items = new ItemBonuses();
        MomentumStacks = 0;
    }

    public int ArrowsPerShot => 1 + LevelOf(RangerUpgrade.SplitShot) + (Tree.TwinShot ? 1 : 0) + Items.Projectiles;

    public float Damage =>
        BaseDamage
        * (1f + 0.20f * LevelOf(RangerUpgrade.SharpenedTips) + Items.Damage + Tree.Damage + Trapper.Damage + Tree.DamagePerExtraArrow * (ArrowsPerShot - 1))
        * Items.DamageMultiplier
        * Items.ProjectileDamage
        * (Tree.TwinShot ? TwinShotDamage : 1f);

    public float FireInterval =>
        BaseFireInterval
        / (MathF.Max(0.2f, 1f + 0.15f * LevelOf(RangerUpgrade.QuickDraw) + Items.AttackSpeedNow + Tree.AttackSpeed + Trapper.AttackSpeed + Tree.MomentumPerKill * MomentumStacks)
           * Items.AttackSpeedMultiplier);

    /// <summary>How many enemies an arrow passes through before it stops (0: it stops at the first).</summary>
    public int Pierce => LevelOf(RangerUpgrade.PiercingArrows) + Tree.Pierce;

    /// <summary>How many times an arrow can jump on to another enemy after it would stop.</summary>
    public int Chains => (Tree.ChainProjectiles ? 1 + Tree.ExtraChains : 0) + Items.Chains;

    public float ChainRange => BaseChainRange * (1f + Tree.ChainRange);

    public float CritChance => BaseCritChance * MathF.Max(0f, 1f + 0.20f * LevelOf(RangerUpgrade.Deadeye) + Items.CritChance + Tree.CritChance - (Tree.Deadeye ? 0.25f : 0f));

    /// <summary>How many times normal damage a critical hit does.</summary>
    public float CritMultiplier => (Tree.Deadeye ? 3f : BaseCritMultiplier) + Items.CritDamage + Tree.CritDamage;

    public float ArrowSpeed => BaseArrowSpeed * (1f + 0.20f * LevelOf(RangerUpgrade.Fletching) + Tree.ArrowSpeed + Items.ProjectileSpeed);

    public float Range => BaseRange * (1f + 0.15f * LevelOf(RangerUpgrade.Fletching) + Tree.Range + Items.Range);

    public float MoveSpeed => BaseMoveSpeed * (1f + 0.08f * LevelOf(RangerUpgrade.FleetFoot) + Items.MoveSpeed + Tree.MoveSpeed + Trapper.MoveSpeed);

    public float MaxHealth => (BaseMaxHealth + 20f * LevelOf(RangerUpgrade.Vitality) + Items.MaxHealth + Tree.MaxHealth + Trapper.MaxHealth) * Items.MaxHealthMultiplier;

    public float PickupRadius => BasePickupRadius * (1f + 0.35f * LevelOf(RangerUpgrade.Scavenger) + Items.Pickup + Trapper.Pickup);

    public float DashCooldown => BaseDashCooldown / (1f + Tree.DashRecharge + Trapper.DashRecharge + 0.10f * LevelOf(RangerUpgrade.TrapSetter) + Items.DashRecharge);

    /// <summary>The dash's push. A dash lasts the same time, so a faster push goes further.</summary>
    public float DashSpeed => BaseDashSpeed * (1f + Tree.DashDistance + Trapper.DashDistance);

    public float Regeneration => Items.Regeneration + Tree.Regeneration + Trapper.Regeneration;

    public float DamageTaken => Items.DamageTaken * Tree.DamageTaken * Trapper.DamageTaken;

    public int RainArrows => Tree.RainOfArrows ? BaseRainArrows + Tree.RainArrows : 0;

    /// <summary>How wide the patch Rain of Arrows covers: wider with an item's area.</summary>
    public float RainPatch => RainRadius * (1f + Items.Area) * Items.AreaMultiplier;

    /// <summary>The block chance items give (the Ranger has no shield).</summary>
    public float BlockChance => MathF.Min(0.6f, Items.BlockChance * Items.BlockMultiplier);

    /// <summary>A snare's burst: a share of an arrow's damage, raised by trap damage.</summary>
    public float SnareDamage => Damage * SnareBurst * (1f + Trapper.TrapDamage + 0.25f * LevelOf(RangerUpgrade.HeavySnares));

    /// <summary>How far a snare's burst reaches: wider with an item's area.</summary>
    public float SnareReach => SnareRadius * (1f + Trapper.BurstRadius) * (1f + Items.Area) * Items.AreaMultiplier;

    /// <summary>How long a snare holds a fodder enemy (an elite half that, a boss not at all).</summary>
    public float SnareHoldTime => SnareHold * (1f + Trapper.HoldTime);

    /// <summary>How long a snare lies before it rusts away unsprung.</summary>
    public float SnareLifetime => SnareLife * (1f + Trapper.SnareTime) + Items.Duration;

    public int MaxSnares => BaseMaxSnares + Trapper.Snares;

    /// <summary>Caltrops' bite each second: a share of the burst, damage over time (so the items' damage over time raises it).</summary>
    public float CaltropDps => SnareDamage * CaltropShare * Items.OverTime;

    /// <summary>What a poison's damage is multiplied by: poison damage, and the items' damage over time.</summary>
    public float PoisonScale => (1f + Trapper.PoisonDamage + 0.20f * LevelOf(RangerUpgrade.StrongVenom) + Items.DotDamage) * Items.DotMultiplier;

    /// <summary>
    /// How long a poison lasts. It bites at the same rate however long it lasts (its share of the hit over <see cref="BasePoisonTime"/>), so a longer poison hurts
    /// more in all.
    /// </summary>
    public float PoisonTime => BasePoisonTime * (1f + Trapper.PoisonTime) + 0.5f * LevelOf(RangerUpgrade.SlowPoison) + Items.Duration;

    /// <summary>How many poisons an enemy can carry at once.</summary>
    public int PoisonStacks => BasePoisonStacks + Trapper.PoisonStacks;

    /// <summary>A toxic cloud's radius (half of how wide it is) and time: bigger with an item's area, longer with its duration.</summary>
    public float CloudRadius => CloudWidth * 0.5f * (1f + Trapper.CloudSize) * (1f + Items.Area) * Items.AreaMultiplier;

    public float CloudTime => BaseCloudTime * (1f + Trapper.CloudTime) + Items.Duration;

    /// <summary>The hawk's dive: a share of an arrow's damage.</summary>
    public float HawkDamage => Damage * HawkShare * (1f + Trapper.HawkDamage + 0.20f * LevelOf(RangerUpgrade.SharpTalons));

    /// <summary>Seconds between the hawk's dives: quicker with Swift Wings and Hawk Training, half with Apex Predator.</summary>
    public float HawkDiveInterval =>
        MathF.Max(MinHawkInterval, HawkInterval / (1f + Trapper.HawkRate + 0.10f * LevelOf(RangerUpgrade.HawkTraining)) * (Trapper.ApexPredator ? 0.5f : 1f));

    public float HawkRange => BaseHawkRange * (1f + Trapper.HawkRange);

    public HitRules HitRules => new(Tree.EliteDamage, Tree.HealthyDamage, Tree.ExecuteChance, Tree.ChainedDamage, Tree.CascadeDamage,
        Fork: Tree.Fork, Splinters: Tree.StormOfSplinters, FirstHitCrits: Tree.OneShotOneKill);
}
