using Silk.NET.Maths;

namespace ArenaMaster.Game.Combat;

/// <summary>What enemy hits do to the player besides damage: a knock-back that shoves them and dies away, and a stun that stops them acting for a moment.</summary>
internal sealed class PlayerCondition
{
    /// <summary>How quickly a knock-back dies away (per second, exponential).</summary>
    private const float KnockbackDecay = 6f;

    /// <summary>The shove still being applied, metres per second, flat.</summary>
    public Vector3D<float> Knockback { get; private set; }

    /// <summary>Seconds of stun left.</summary>
    public float Stunned { get; private set; }

    public bool IsStunned => Stunned > 0f;

    /// <summary>Shoves the player along <paramref name="direction"/> (flattened) at <paramref name="speed"/>. A new shove replaces a weaker one in progress.</summary>
    public void Knock(Vector3D<float> direction, float speed)
    {
        var flat = new Vector3D<float>(direction.X, 0f, direction.Z);
        if (speed <= 0f || flat.LengthSquared < 1e-8f)
        {
            return;
        }

        var knock = Vector3D.Normalize(flat) * speed;
        if (knock.LengthSquared >= Knockback.LengthSquared)
        {
            Knockback = knock;
        }
    }

    /// <summary>Stuns the player for <paramref name="seconds"/>, unless they are already stunned for longer.</summary>
    public void Stun(float seconds) => Stunned = MathF.Max(Stunned, seconds);

    public void Update(float deltaSeconds)
    {
        Stunned = MathF.Max(0f, Stunned - deltaSeconds);
        Knockback *= MathF.Exp(-KnockbackDecay * deltaSeconds);
        if (Knockback.LengthSquared < 0.01f)
        {
            Knockback = Vector3D<float>.Zero;
        }
    }

    public void Clear()
    {
        Knockback = Vector3D<float>.Zero;
        Stunned = 0f;
    }
}
