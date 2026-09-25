using ArenaMaster.Game.Items;

namespace ArenaMaster.Game.Ranger;

/// <summary>
/// The Ranger's numbers for the current run: the base values, what the upgrades taken so far (see <see cref="RangerUpgrades"/>) have made of them, and what the
/// items carried add (<see cref="Items"/>). Upgrade and item bonuses add together; item multipliers then multiply the lot. Everything that fires, moves or picks up
/// asks here, so an upgrade or an item takes effect the moment it is gained.
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

    /// <summary>Degrees between neighbouring arrows of a split shot.</summary>
    public const float SplitSpreadDegrees = 7f;

    private readonly Dictionary<RangerUpgrade, int> _levels = new();

    /// <summary>What the items carried this run add up to. The content hands in a fresh one whenever an item is gained.</summary>
    public ItemBonuses Items { get; set; } = new();

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

    /// <summary>A new run: no upgrades, no items.</summary>
    public void Reset()
    {
        _levels.Clear();
        Items = new ItemBonuses();
    }

    public float Damage => BaseDamage * (1f + 0.20f * LevelOf(RangerUpgrade.SharpenedTips) + Items.Damage) * Items.DamageMultiplier;

    public float FireInterval => BaseFireInterval / ((1f + 0.15f * LevelOf(RangerUpgrade.QuickDraw) + Items.AttackSpeed) * Items.AttackSpeedMultiplier);

    public int ArrowsPerShot => 1 + LevelOf(RangerUpgrade.SplitShot);

    /// <summary>How many enemies an arrow passes through before it stops (0: it stops at the first).</summary>
    public int Pierce => LevelOf(RangerUpgrade.PiercingArrows);

    public float CritChance => BaseCritChance + 0.08f * LevelOf(RangerUpgrade.Deadeye) + Items.CritChance;

    /// <summary>How many times normal damage a critical hit does.</summary>
    public float CritMultiplier => BaseCritMultiplier + Items.CritDamage;

    public float ArrowSpeed => BaseArrowSpeed * (1f + 0.20f * LevelOf(RangerUpgrade.Fletching));

    public float Range => BaseRange * (1f + 0.15f * LevelOf(RangerUpgrade.Fletching));

    public float MoveSpeed => BaseMoveSpeed * (1f + 0.08f * LevelOf(RangerUpgrade.FleetFoot) + Items.MoveSpeed);

    public float MaxHealth => BaseMaxHealth + 20f * LevelOf(RangerUpgrade.Vitality) + Items.MaxHealth;

    public float PickupRadius => BasePickupRadius * (1f + 0.35f * LevelOf(RangerUpgrade.Scavenger) + Items.Pickup);
}
