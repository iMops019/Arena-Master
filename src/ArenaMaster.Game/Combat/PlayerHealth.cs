namespace ArenaMaster.Game.Combat;

/// <summary>The player's hit points, with a short invulnerable moment after each hit so a crowd can't take them all at once.</summary>
internal sealed class PlayerHealth
{
    /// <summary>How long after a hit the player can't be hurt again, in seconds.</summary>
    public const float HitGrace = 0.25f;

    private float _grace;

    public PlayerHealth(float max)
    {
        Max = max;
        Current = max;
    }

    public float Max { get; private set; }

    public float Current { get; private set; }

    public bool IsDead => Current <= 0f;

    /// <summary>What every hit's damage is multiplied by (armour-like items bring it below 1).</summary>
    public float DamageTaken { get; set; } = 1f;

    /// <summary>1 right after a hit, fading to 0 - for a red flash on the HUD.</summary>
    public float HurtFlash => _grace / HitGrace;

    /// <summary>Takes <paramref name="amount"/> off unless the player is dead or still in the grace after the last hit. True if it landed.</summary>
    public bool TakeDamage(float amount)
    {
        if (IsDead || _grace > 0f || amount <= 0f)
        {
            return false;
        }

        Current = MathF.Max(0f, Current - amount * DamageTaken);
        _grace = HitGrace;
        return true;
    }

    public void Update(float deltaSeconds) => _grace = MathF.Max(0f, _grace - deltaSeconds);

    /// <summary>Raises the maximum by <paramref name="amount"/> and heals the same amount.</summary>
    public void RaiseMax(float amount)
    {
        Max += amount;
        Current += amount;
    }

    /// <summary>Heals up to the maximum. Does nothing for the dead.</summary>
    public void Heal(float amount)
    {
        if (!IsDead)
        {
            Current = MathF.Min(Max, Current + amount);
        }
    }

    /// <summary>Back to full health at <paramref name="max"/> (a new run).</summary>
    public void Reset(float max)
    {
        Max = max;
        Current = max;
        DamageTaken = 1f;
        _grace = 0f;
    }
}
