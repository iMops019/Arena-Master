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
/// passive tree (<see cref="Tree"/>). Upgrade, item and tree bonuses add together; item multipliers then multiply the lot. Everything that fires, moves or picks up
/// asks here, so a change takes effect the moment it is made.
/// </summary>
internal sealed class RangerStats
{
    public const float BaseDamage = 12f;
    public const float BaseFireInterval = 0.5f;
    public const float BaseMoveSpeed = 7f;
    public const float BaseArrowSpeed = 50f;
    public const float BaseRange = 60f;
    public const float BaseCritChance = 0.05f;
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

    private readonly Dictionary<RangerUpgrade, int> _levels = new();

    /// <summary>What the items carried this run add up to. The content hands in a fresh one whenever an item is gained.</summary>
    public ItemBonuses Items { get; set; } = new();

    /// <summary>What the passive tree's ranks add up to. Set at the start of each run.</summary>
    public SharpshooterBonuses Tree { get; set; } = new();

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
        * (1f + 0.20f * LevelOf(RangerUpgrade.SharpenedTips) + Items.Damage + Tree.Damage + Tree.DamagePerExtraArrow * (ArrowsPerShot - 1))
        * Items.DamageMultiplier
        * Items.ProjectileDamage
        * (Tree.TwinShot ? TwinShotDamage : 1f);

    public float FireInterval =>
        BaseFireInterval
        / (MathF.Max(0.2f, 1f + 0.15f * LevelOf(RangerUpgrade.QuickDraw) + Items.AttackSpeedNow + Tree.AttackSpeed + Tree.MomentumPerKill * MomentumStacks)
           * Items.AttackSpeedMultiplier);

    /// <summary>How many enemies an arrow passes through before it stops (0: it stops at the first).</summary>
    public int Pierce => LevelOf(RangerUpgrade.PiercingArrows) + Tree.Pierce;

    /// <summary>How many times an arrow can jump on to another enemy after it would stop.</summary>
    public int Chains => (Tree.ChainProjectiles ? 1 + Tree.ExtraChains : 0) + Items.Chains;

    public float ChainRange => BaseChainRange * (1f + Tree.ChainRange);

    public float CritChance => MathF.Max(0f, BaseCritChance + 0.08f * LevelOf(RangerUpgrade.Deadeye) + Items.CritChance + Tree.CritChance - (Tree.Deadeye ? 0.05f : 0f));

    /// <summary>How many times normal damage a critical hit does.</summary>
    public float CritMultiplier => (Tree.Deadeye ? 3f : BaseCritMultiplier) + Items.CritDamage + Tree.CritDamage;

    public float ArrowSpeed => BaseArrowSpeed * (1f + 0.20f * LevelOf(RangerUpgrade.Fletching) + Tree.ArrowSpeed + Items.ProjectileSpeed);

    public float Range => BaseRange * (1f + 0.15f * LevelOf(RangerUpgrade.Fletching) + Tree.Range + Items.Range);

    public float MoveSpeed => BaseMoveSpeed * (1f + 0.08f * LevelOf(RangerUpgrade.FleetFoot) + Items.MoveSpeed + Tree.MoveSpeed);

    public float MaxHealth => BaseMaxHealth + 20f * LevelOf(RangerUpgrade.Vitality) + Items.MaxHealth + Tree.MaxHealth;

    public float PickupRadius => BasePickupRadius * (1f + 0.35f * LevelOf(RangerUpgrade.Scavenger) + Items.Pickup);

    public float DashCooldown => BaseDashCooldown / (1f + Tree.DashRecharge + Items.DashRecharge);

    /// <summary>The dash's push. A dash lasts the same time, so a faster push goes further.</summary>
    public float DashSpeed => BaseDashSpeed * (1f + Tree.DashDistance);

    public float Regeneration => Items.Regeneration + Tree.Regeneration;

    public float DamageTaken => Items.DamageTaken * Tree.DamageTaken;

    public int RainArrows => Tree.RainOfArrows ? BaseRainArrows + Tree.RainArrows : 0;

    /// <summary>How wide the patch Rain of Arrows covers: wider with an item's area.</summary>
    public float RainPatch => RainRadius * (1f + Items.Area) * Items.AreaMultiplier;

    /// <summary>The block chance items give (the Ranger has no shield).</summary>
    public float BlockChance => MathF.Min(0.6f, Items.BlockChance * Items.BlockMultiplier);

    public HitRules HitRules => new(Tree.EliteDamage, Tree.HealthyDamage, Tree.ExecuteChance, Tree.ChainedDamage, Tree.CascadeDamage,
        Fork: Tree.Fork, Splinters: Tree.StormOfSplinters, FirstHitCrits: Tree.OneShotOneKill);
}
