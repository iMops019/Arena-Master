namespace ArenaMaster.Game.Combat;

/// <summary>The player's hit points, with a short invulnerable moment after each hit so a crowd can't take them all at once.</summary>
internal sealed class PlayerHealth
{
    /// <summary>How long after a hit the player can't be hurt again, in seconds.</summary>
    public const float HitGrace = 0.25f;

    private float _grace;
    private float _hurt;
    private bool _lastStandUsed;

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

    /// <summary>1 right after a hit, fading to 0 - for a red flash on the HUD. A blow turned aside doesn't flash.</summary>
    public float HurtFlash => _hurt / HitGrace;

    /// <summary>Whether a blow landing now would hurt: alive, and past the grace after the last hit.</summary>
    public bool CanBeHurt => !IsDead && _grace <= 0f;

    /// <summary>How many times a blow that would kill leaves the player on 1 health instead. Each one used is spent. A class sets it for a run; it's 0 otherwise.</summary>
    public int LastStands { get; set; }

    /// <summary>Takes <paramref name="amount"/> off unless the player is dead or still in the grace after the last hit. True if it landed.</summary>
    public bool TakeDamage(float amount)
    {
        if (IsDead || _grace > 0f || amount <= 0f)
        {
            return false;
        }

        Current = MathF.Max(0f, Current - amount * DamageTaken);
        if (Current <= 0f && LastStands > 0)
        {
            LastStands--;
            Current = 1f;
            _lastStandUsed = true;
        }

        _grace = HitGrace;
        _hurt = HitGrace;
        return true;
    }

    /// <summary>A blow turned aside (a shield's block): no damage, but the same short grace as a hit, so the next blow in a crowd waits as it would after a real one.</summary>
    public void Deflect()
    {
        if (!IsDead)
        {
            _grace = HitGrace;
        }
    }

    /// <summary>True once after a last stand has saved the player from a killing blow.</summary>
    public bool TakeLastStand()
    {
        bool used = _lastStandUsed;
        _lastStandUsed = false;
        return used;
    }

    public void Update(float deltaSeconds)
    {
        _grace = MathF.Max(0f, _grace - deltaSeconds);
        _hurt = MathF.Max(0f, _hurt - deltaSeconds);
    }

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
        LastStands = 0;
        _lastStandUsed = false;
        _grace = 0f;
        _hurt = 0f;
    }
}
