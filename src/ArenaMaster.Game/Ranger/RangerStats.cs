namespace ArenaMaster.Game.Ranger;

/// <summary>
/// The Ranger's numbers for the current run: the base values, and what the upgrades taken so far (see <see cref="RangerUpgrades"/>) have made of them.
/// Everything that fires, moves or picks up asks here, so an upgrade takes effect the moment it is chosen.
/// </summary>
internal sealed class RangerStats
{
    public const float BaseDamage = 12f;
    public const float BaseFireInterval = 0.5f;
    public const float BaseMoveSpeed = 7f;
    public const float BaseArrowSpeed = 50f;
    public const float BaseRange = 60f;
    public const float BaseCritChance = 0.05f;
    public const float CritMultiplier = 2f;
    public const float BasePickupRadius = 3f;
    public const float BaseMaxHealth = 100f;

    /// <summary>Degrees between neighbouring arrows of a split shot.</summary>
    public const float SplitSpreadDegrees = 7f;

    private readonly Dictionary<RangerUpgrade, int> _levels = new();

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

    public void Reset() => _levels.Clear();

    public float Damage => BaseDamage * (1f + 0.20f * LevelOf(RangerUpgrade.SharpenedTips));

    public float FireInterval => BaseFireInterval / (1f + 0.15f * LevelOf(RangerUpgrade.QuickDraw));

    public int ArrowsPerShot => 1 + LevelOf(RangerUpgrade.SplitShot);

    /// <summary>How many enemies an arrow passes through before it stops (0: it stops at the first).</summary>
    public int Pierce => LevelOf(RangerUpgrade.PiercingArrows);

    public float CritChance => BaseCritChance + 0.08f * LevelOf(RangerUpgrade.Deadeye);

    public float ArrowSpeed => BaseArrowSpeed * (1f + 0.20f * LevelOf(RangerUpgrade.Fletching));

    public float Range => BaseRange * (1f + 0.15f * LevelOf(RangerUpgrade.Fletching));

    public float MoveSpeed => BaseMoveSpeed * (1f + 0.08f * LevelOf(RangerUpgrade.FleetFoot));

    public float MaxHealth => BaseMaxHealth + 20f * LevelOf(RangerUpgrade.Vitality);

    public float PickupRadius => BasePickupRadius * (1f + 0.35f * LevelOf(RangerUpgrade.Scavenger));
}
