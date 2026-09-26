using ArenaMaster.Game.Items;

namespace ArenaMaster.Game.Warrior;

/// <summary>
/// The Warrior's numbers for the current run: the base values, the run's upgrades (see <see cref="WarriorUpgrades"/>), the items carried (<see cref="Items"/>), the
/// Berserker tree (<see cref="Tree"/>), and what is building up during the fight (<see cref="Rage"/>, <see cref="Frenzy"/>). Upgrade, item and tree bonuses add
/// together; item multipliers then multiply the lot, and rage multiplies on top. Items speak in general terms: here "damage" and "attack speed" mean the Cleave's,
/// "area" its size (both its reach and its sweep), "duration" how long rage lasts.
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

    private readonly Dictionary<WarriorUpgrade, int> _levels = new();

    /// <summary>What the items carried this run add up to.</summary>
    public ItemBonuses Items { get; set; } = new();

    /// <summary>What the Berserker tree's ranks add up to.</summary>
    public BerserkerBonuses Tree { get; set; } = new();

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

    /// <summary>Seconds between swings (one axe, then the other). An item's attack speed for a class without rage isn't the Warrior's: he has the rage instead.</summary>
    public float SwingInterval =>
        BaseSwingInterval
        / (MathF.Max(0.2f, 1f + 0.12f * LevelOf(WarriorUpgrade.QuickHands) + Items.AttackSpeedNow - Items.RageFallback + Tree.AttackSpeed + Frenzy) * Items.AttackSpeedMultiplier * (1f + RageBonus));

    /// <summary>What the Cleave's reach and sweep are both multiplied by: the size bonuses, the items' area, and (Avatar of Rage) the rage.</summary>
    public float CleaveSize =>
        (1f + 0.10f * LevelOf(WarriorUpgrade.WideCleave) + Tree.CleaveSize + Items.Area + (Tree.AvatarOfRage ? AvatarSize * Rage : 0f)) * Items.AreaMultiplier;

    /// <summary>How far a Cleave's wave rolls out.</summary>
    public float Reach => BaseReach * CleaveSize * (1f + 0.12f * LevelOf(WarriorUpgrade.LongAxes) + Tree.CleaveReach + Items.CleaveReach);

    /// <summary>How wide a Cleave sweeps, in degrees: 360 at most, all the way round.</summary>
    public float ArcDegrees => MathF.Min(360f, (BaseArc + Tree.CleaveArc + (Tree.GreatSweep ? GreatSweepArc : 0f)) * CleaveSize);

    public float CritChance => BaseCritChance * MathF.Max(0f, 1f + 0.20f * LevelOf(WarriorUpgrade.SavageEye) + Items.CritChance + Tree.CritChance);

    /// <summary>How many times normal damage a critical hit does.</summary>
    public float CritMultiplier => BaseCritMultiplier + 0.15f * LevelOf(WarriorUpgrade.SavageEye) + Items.CritDamage + Tree.CritDamage;

    /// <summary>What every Warrior hit on an elite or a boss is multiplied by.</summary>
    public float EliteMultiplier => 1f + Tree.EliteDamage + 0.15f * LevelOf(WarriorUpgrade.Headsman);

    /// <summary>The share of Cleave damage dealt that heals the Warrior.</summary>
    public float LifeLeech => Tree.LifeLeech + 0.003f * LevelOf(WarriorUpgrade.Bloodletting);

    /// <summary>The chance to parry a blow with the axes, capped at <see cref="MaxBlockChance"/>.</summary>
    public float BlockChance => MathF.Min(MaxBlockChance,
        (Tree.BlockChance + 0.04f * LevelOf(WarriorUpgrade.ParryingBlades) + Items.BlockChance + (Tree.BladeWall ? BladeWallBlock : 0f)) * Items.BlockMultiplier);

    public float MaxHealth => BaseMaxHealth + 20f * LevelOf(WarriorUpgrade.Hardy) + Items.MaxHealth + Tree.MaxHealth;

    public float Regeneration => 0.5f * LevelOf(WarriorUpgrade.Recovery) + Items.Regeneration + Tree.Regeneration;

    /// <summary>What every blow's damage is multiplied by: the items, the tree's cuts, and (Blood Rage) the rage.</summary>
    public float DamageTaken => Items.DamageTaken * Tree.DamageTaken * (Tree.BloodRage ? MathF.Max(0.5f, 1f - BloodRageCut * Rage) : 1f);

    public float MoveSpeed => BaseMoveSpeed * (1f + 0.08f * LevelOf(WarriorUpgrade.BattleStride) + Items.MoveSpeed);

    public float PickupRadius => BasePickupRadius * (1f + 0.35f * LevelOf(WarriorUpgrade.Plunderer) + Items.Pickup);

    /// <summary>Seconds for the battle charge to recharge.</summary>
    public float ChargeCooldown => BaseChargeCooldown / (1f + Items.DashRecharge);
}
