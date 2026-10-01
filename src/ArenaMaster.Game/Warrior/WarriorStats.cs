using ArenaMaster.Game.Items;

namespace ArenaMaster.Game.Warrior;

/// <summary>
/// The Warrior's numbers for the current run: the base values, the run's upgrades (see <see cref="WarriorUpgrades"/>), the items carried (<see cref="Items"/>), the
/// Berserker tree (<see cref="Tree"/>) or the Reaver tree (<see cref="Reaver"/>), whichever is active (the other's bonuses are empty), and what is building up during
/// the fight (<see cref="Rage"/>, <see cref="Frenzy"/>, <see cref="Scent"/>). Upgrade, item and tree bonuses add together; item multipliers then multiply the lot, and
/// rage multiplies on top. Items speak in general terms: here "damage" and "attack speed" mean the Cleave's or the throw's, "area" the Cleave's size (both its reach
/// and its sweep) or how wide a thrown axe hits, "range" how far the axes fly, "duration" how long rage or bleeding lasts.
/// </summary>
internal sealed class WarriorStats
{
    public const float BaseMaxHealth = 120f;
    public const float BaseMoveSpeed = 6.8f;
    public const float BasePickupRadius = 3f;

    /// <summary>
    /// The Cleave: damage to each enemy a wave reaches, seconds between swings (the axes take turns), how far a wave rolls out, how wide it sweeps (degrees, all the
    /// way round at most), where it starts from the Warrior, and how long it takes to roll out whatever its reach.
    /// </summary>
    public const float BaseCleaveDamage = 20f;
    public const float BaseSwingInterval = 0.55f;
    public const float BaseReach = 6f;
    public const float BaseArc = 100f;
    public const float StartRadius = 0.6f;
    public const float TravelTime = 0.28f;

    /// <summary>Each axe's wave leans this many degrees off the facing, the right axe one way, the left the other.</summary>
    public const float AxeLean = 10f;

    /// <summary>
    /// The class's own critical chance. Everything else only scales it: the level-up, the tree and items add <i>increased</i> critical chance, so +100% doubles
    /// the base.
    /// </summary>
    public const float BaseCritChance = 0.07f;
    public const float BaseCritMultiplier = 2f;

    /// <summary>Two axes parry: no block to start with (the tree and items give it), and the most it can reach.</summary>
    public const float MaxBlockChance = 0.5f;

    /// <summary>The battle charge on Shift: how long, how fast, how often.</summary>
    public const float ChargeDuration = 0.2f;
    public const float BaseChargeSpeed = 19f;
    public const float BaseChargeCooldown = 1.5f;

    /// <summary>Berserking: the most rage to start with, how long it lasts after the last hit, and what each rage gives (attack damage and attack speed).</summary>
    public const float BaseMaxRage = 20f;
    public const float BaseRageDuration = 5f;
    public const float RagePer = 0.01f;

    /// <summary>Fuelled by Pain: rage for every blow that reaches the Warrior.</summary>
    public const int PainRage = 4;

    /// <summary>Blood Rage: less damage taken for each rage.</summary>
    public const float BloodRageCut = 0.005f;

    /// <summary>Avatar of Rage: the Cleave's size for each rage.</summary>
    public const float AvatarSize = 0.01f;

    /// <summary>Great Sweep: degrees added to the Cleave's sweep.</summary>
    public const float GreatSweepArc = 60f;

    /// <summary>Echoing Cleave: how long after the swing the echo rolls out, and its share of the damage.</summary>
    public const float EchoDelay = 0.2f;
    public const float EchoDamage = 0.5f;

    /// <summary>Twin Fury: the other axe's wave, as a share of the damage.</summary>
    public const float TwinDamage = 0.7f;

    /// <summary>Whirlwind: every this-many-th swing goes all the way round, reaching this much further.</summary>
    public const int WhirlwindEvery = 4;
    public const float WhirlwindReach = 1.5f;

    /// <summary>Riposte: what an attacker whose blow was blocked takes, in Cleave hits.</summary>
    public const float RiposteDamage = 3f;

    /// <summary>Execute: a hit on a non-boss below this share of its health kills it.</summary>
    public const float ExecuteBelow = 0.2f;

    /// <summary>Last Stand: the share of max health it heals once it has saved the Warrior.</summary>
    public const float LastStandHeal = 0.4f;

    /// <summary>Blade Wall: block chance added; rage and a share of max health for every block.</summary>
    public const float BladeWallBlock = 0.1f;
    public const int BladeWallRage = 5;
    public const float BladeWallHeal = 0.02f;

    /// <summary>Blood Frenzy: attack speed per kill, for how long, and the most it adds.</summary>
    public const float FrenzyPerKill = 0.01f;
    public const float FrenzySeconds = 4f;
    public const float FrenzyCap = 0.3f;

    /// <summary>
    /// The Reaver's thrown axes: damage to each enemy an axe passes (on the way out, and again on the way back), how far it flies out, how fast it spins through the
    /// air (both ways), how far either side of its path it hits (besides the enemy's own size), and how far past its range an enemy may be and still be thrown at (it
    /// walks into the axe's path). An axe back within <see cref="CatchRadius"/> of the Warrior is in the hand again.
    /// </summary>
    public const float BaseThrowDamage = 20f;
    public const float BaseThrowRange = 10f;
    public const float BaseAxeSpeed = 24f;
    public const float BaseAxeRadius = 0.6f;
    public const float ThrowMargin = 2f;
    public const float CatchRadius = 0.8f;

    /// <summary>Hemorrhage: a hit's bleed, as a share of its damage, over <see cref="BaseBleedSeconds"/>; up to this many stacks on one enemy (Deep Wounds: more).</summary>
    public const float BleedShare = 0.3f;
    public const float BaseBleedSeconds = 4f;
    public const int BaseBleedStacks = 5;
    public const int DeepWoundsStacks = 8;

    /// <summary>Catch: how much stronger the throw after a catch is.</summary>
    public const float CatchBonus = 0.5f;

    /// <summary>
    /// How many axes a throw sends (the first is the hand's own, the rest spares fanned <see cref="AxeFan"/> degrees apart, gone once they are back), how many
    /// times an axe bounces on to another enemy before it turns back, and how far from the enemy it hit it finds the next. Ricochet Axes: more bounces, further.
    /// </summary>
    public const int BaseAxes = 1;
    public const float AxeFan = 14f;
    public const int BaseAxeChains = 2;
    public const float ChainRange = 5f;
    public const int RicochetChains = 2;
    public const float RicochetRange = 8f;

    /// <summary>Bloodbath: a pool's radius and how long it lies, and the share of max health it heals a second.</summary>
    public const float PoolRadius = 2f;
    public const float PoolSeconds = 4f;
    public const float PoolHeal = 0.02f;

    /// <summary>Run Them Down: how near the charging Warrior an enemy is hit.</summary>
    public const float ChargeHitRadius = 1.5f;

    /// <summary>Closing In: within this many metres of the Warrior, the axes hit this much harder.</summary>
    public const float CloseRange = 4f;
    public const float CloseDamage = 0.3f;

    /// <summary>Returning Edge: how much harder an axe hits on its way back.</summary>
    public const float ReturnDamage = 0.5f;

    /// <summary>Blood Scent: move speed and charge recharge for each bleeding enemy this near, up to the cap.</summary>
    public const float ScentRange = 12f;
    public const float ScentPer = 0.03f;
    public const float ScentCap = 0.3f;

    /// <summary>Axe Storm: how often, for how long, how far out the axes circle, how many turns a second, and a pass's damage in throws.</summary>
    public const float StormEvery = 12f;
    public const float StormSeconds = 4f;
    public const float StormRadius = 3f;
    public const float StormTurns = 1f;
    public const float StormDamage = 1.5f;

    /// <summary>Crimson Tide: how much more damage a bleeding enemy takes from the axes.</summary>
    public const float CrimsonDamage = 0.2f;

    /// <summary>Harvester: every this-many-th kill throws a free axe.</summary>
    public const int HarvesterEvery = 5;

    private readonly Dictionary<WarriorUpgrade, int> _levels = new();

    /// <summary>What the items carried this run add up to.</summary>
    public ItemBonuses Items { get; set; } = new();

    /// <summary>What the Berserker tree's ranks add up to (empty while the Reaver is active).</summary>
    public BerserkerBonuses Tree { get; set; } = new();

    /// <summary>What the Reaver tree's ranks add up to (empty while the Berserker is active).</summary>
    public ReaverBonuses Reaver { get; set; } = new();

    /// <summary>Whether the axes are thrown (the Reaver tree is active) rather than swung as Cleaves.</summary>
    public bool Throwing { get; set; }

    /// <summary>Blood Scent's move speed and charge recharge right now (0 to <see cref="ScentCap"/>), kept up to date by the class.</summary>
    public float Scent { get; set; }

    /// <summary>The rage the Warrior has right now (0 without Berserking), kept up to date by the class.</summary>
    public int Rage { get; set; }

    /// <summary>Blood Frenzy's attack speed right now (0 to <see cref="FrenzyCap"/>), kept up to date by the class.</summary>
    public float Frenzy { get; set; }

    public int LevelOf(WarriorUpgrade upgrade) => _levels.GetValueOrDefault(upgrade);

    /// <summary>Takes one more level of <paramref name="upgrade"/> (no further than its maximum).</summary>
    public void Increase(WarriorUpgrade upgrade)
    {
        int level = LevelOf(upgrade);
        if (level < WarriorUpgrades.Info(upgrade).MaxLevel)
        {
            _levels[upgrade] = level + 1;
        }
    }

    /// <summary>A new run: no upgrades, no items, no rage. The tree is left as it is.</summary>
    public void Reset()
    {
        _levels.Clear();
        Items = new ItemBonuses();
        Rage = 0;
        Frenzy = 0f;
        Scent = 0f;
    }

    /// <summary>What rage adds right now to attack damage and to attack speed (0.2 = +20%). Nothing without Berserking.</summary>
    public float RageBonus => Tree.Berserking ? Rage * RagePer * (1f + Tree.RageEffect) : 0f;

    public int MaxRage => (int)((BaseMaxRage + Tree.MaxRage + 4f * LevelOf(WarriorUpgrade.DeepFury) + Items.RageMax) * (Tree.AvatarOfRage ? 2f : 1f));

    /// <summary>Seconds rage lasts after the last hit.</summary>
    public float RageDuration => BaseRageDuration + Tree.RageDuration + 1f * LevelOf(WarriorUpgrade.LastingRage) + Items.Duration + Items.RageDuration;

    /// <summary>Whether rage drains a point at a time once it runs out, rather than going all at once (Horn of Fury).</summary>
    public bool RageDrains => Items.RageDrains > 0;

    /// <summary>With <see cref="RageDrains"/>, a point goes this often once the rage has run out.</summary>
    public const float DrainStep = 0.5f;

    public float CleaveDamage =>
        BaseCleaveDamage * (1f + 0.20f * LevelOf(WarriorUpgrade.BrutalSwings) + Items.Damage + Tree.CleaveDamage + ItemBonuses.ProjectileFallback * Items.Projectiles
                            + ItemBonuses.ChainFallback * Items.Chains)
        * Items.DamageMultiplier * (1f + RageBonus);

    /// <summary>
    /// Seconds between swings or throws (one axe, then the other). An item's attack speed for a class without rage isn't the Berserker's: he has the rage instead (the
    /// Reaver, with no rage, takes it).
    /// </summary>
    public float SwingInterval =>
        BaseSwingInterval
        / (MathF.Max(0.2f, 1f + 0.12f * LevelOf(WarriorUpgrade.QuickHands) + Items.AttackSpeedNow - (Throwing ? 0f : Items.RageFallback) + Tree.AttackSpeed
                           + Reaver.AttackSpeed + Frenzy) * Items.AttackSpeedMultiplier * (1f + RageBonus));

    /// <summary>What each enemy a thrown axe passes takes (before a crit, Catch and the rest), with the Reaver.</summary>
    public float ThrowDamage =>
        BaseThrowDamage * (1f + 0.20f * LevelOf(WarriorUpgrade.BrutalSwings) + Items.Damage + Reaver.ThrowDamage) * Items.DamageMultiplier;

    /// <summary>How many axes each throw sends: the tree's, the Armful of Axes card's, and an item's projectiles (thrown axes take them as axes, not as damage).</summary>
    public int AxesPerThrow => Math.Max(1, BaseAxes + Reaver.Axes + LevelOf(WarriorUpgrade.ArmfulOfAxes) + Items.Projectiles);

    /// <summary>How many times a thrown axe bounces on to another enemy before it turns back: the tree's, the Skipping Axes card's, and an item's chains.</summary>
    public int AxeChains => Math.Max(0, BaseAxeChains + Reaver.Chains + (Reaver.RicochetAxes ? RicochetChains : 0) + LevelOf(WarriorUpgrade.SkippingAxes) + Items.Chains);

    /// <summary>How far from the enemy it hit a bouncing axe finds the next.</summary>
    public float AxeChainRange => (Reaver.RicochetAxes ? RicochetRange : ChainRange) * (1f + Items.Area) * Items.AreaMultiplier;

    /// <summary>How far a thrown axe flies out before it turns back.</summary>
    public float ThrowRange => BaseThrowRange * (1f + Reaver.ThrowRange + 0.10f * LevelOf(WarriorUpgrade.FarThrow) + Items.Range);

    /// <summary>How far off an enemy is thrown at: the axes' range, and a little more.</summary>
    public float ThrowReach => ThrowRange + ThrowMargin;

    /// <summary>How fast a thrown axe flies, out and back (metres per second).</summary>
    public float AxeSpeed => BaseAxeSpeed * (1f + Reaver.AxeSpeed + 0.15f * LevelOf(WarriorUpgrade.SpinningAxes) + Items.ProjectileSpeed);

    /// <summary>How far either side of its path a thrown axe hits (besides the enemy's own size): wider with the items' area.</summary>
    public float AxeRadius => BaseAxeRadius * (1f + 0.15f * LevelOf(WarriorUpgrade.BroadBlades) + Items.Area) * Items.AreaMultiplier;

    /// <summary>What a hit's bleed is multiplied by: the bleed bonuses and the items' damage over time.</summary>
    public float BleedScale => (1f + Reaver.BleedDamage + 0.20f * LevelOf(WarriorUpgrade.CruelEdges) + Items.DotDamage) * Items.DotMultiplier;

    /// <summary>How long a stack of bleeding lasts.</summary>
    public float BleedSeconds => BaseBleedSeconds + Reaver.BleedDuration + 1f * LevelOf(WarriorUpgrade.LastingWounds) + Items.Duration;

    /// <summary>The most stacks of bleeding one enemy carries.</summary>
    public int BleedStacks => Reaver.DeepWounds ? DeepWoundsStacks : BaseBleedStacks;

    /// <summary>What the throw after a catch is multiplied by (Catch, and Sure Catch).</summary>
    public float CatchMultiplier => 1f + CatchBonus + 0.25f * LevelOf(WarriorUpgrade.SureCatch);

    /// <summary>What the Cleave's reach and sweep are both multiplied by: the size bonuses, the items' area, and (Avatar of Rage) the rage.</summary>
    public float CleaveSize =>
        (1f + 0.10f * LevelOf(WarriorUpgrade.WideCleave) + Tree.CleaveSize + Items.Area + (Tree.AvatarOfRage ? AvatarSize * Rage : 0f)) * Items.AreaMultiplier;

    /// <summary>How far a Cleave's wave rolls out.</summary>
    public float Reach => BaseReach * CleaveSize * (1f + 0.12f * LevelOf(WarriorUpgrade.LongAxes) + Tree.CleaveReach + Items.CleaveReach);

    /// <summary>How wide a Cleave sweeps, in degrees: 360 at most, all the way round.</summary>
    public float ArcDegrees => MathF.Min(360f, (BaseArc + Tree.CleaveArc + (Tree.GreatSweep ? GreatSweepArc : 0f)) * CleaveSize);

    public float CritChance => BaseCritChance * MathF.Max(0f, 1f + 0.20f * LevelOf(WarriorUpgrade.SavageEye) + Items.CritChance + Tree.CritChance + Reaver.CritChance);

    /// <summary>How many times normal damage a critical hit does.</summary>
    public float CritMultiplier => BaseCritMultiplier + 0.15f * LevelOf(WarriorUpgrade.SavageEye) + Items.CritDamage + Tree.CritDamage + Reaver.CritDamage;

    /// <summary>What every Warrior hit on an elite or a boss is multiplied by.</summary>
    public float EliteMultiplier => 1f + Tree.EliteDamage + Reaver.EliteDamage + 0.15f * LevelOf(WarriorUpgrade.Headsman);

    /// <summary>The share of Cleave (or thrown axe) damage dealt that heals the Warrior.</summary>
    public float LifeLeech => Tree.LifeLeech + Reaver.LifeLeech + 0.003f * LevelOf(WarriorUpgrade.Bloodletting);

    /// <summary>The chance to parry a blow with the axes, capped at <see cref="MaxBlockChance"/>.</summary>
    public float BlockChance => MathF.Min(MaxBlockChance,
        (Tree.BlockChance + 0.04f * LevelOf(WarriorUpgrade.ParryingBlades) + Items.BlockChance + (Tree.BladeWall ? BladeWallBlock : 0f)) * Items.BlockMultiplier);

    public float MaxHealth => (BaseMaxHealth + 20f * LevelOf(WarriorUpgrade.Hardy) + Items.MaxHealth + Tree.MaxHealth + Reaver.MaxHealth) * Items.MaxHealthMultiplier;

    public float Regeneration => 0.5f * LevelOf(WarriorUpgrade.Recovery) + Items.Regeneration + Tree.Regeneration + Reaver.Regeneration;

    /// <summary>What every blow's damage is multiplied by: the items, the tree's cuts, and (Blood Rage) the rage.</summary>
    public float DamageTaken => Items.DamageTaken * Tree.DamageTaken * Reaver.DamageTaken * (Tree.BloodRage ? MathF.Max(0.5f, 1f - BloodRageCut * Rage) : 1f);

    public float MoveSpeed => BaseMoveSpeed * (1f + 0.08f * LevelOf(WarriorUpgrade.BattleStride) + Items.MoveSpeed + Reaver.MoveSpeed + Scent);

    public float PickupRadius => BasePickupRadius * (1f + 0.35f * LevelOf(WarriorUpgrade.Plunderer) + Items.Pickup);

    /// <summary>Seconds for the battle charge to recharge.</summary>
    public float ChargeCooldown => BaseChargeCooldown / (1f + Items.DashRecharge + Reaver.ChargeRecharge + Scent);
}
