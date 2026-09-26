using ArenaMaster.Game.Classes;
using CEngine.Core;
using Silk.NET.Input;
using Silk.NET.Maths;

namespace ArenaMaster.Game.Shaman;

/// <summary>
/// The Shaman's body and movement on top of the engine's third-person walker: a walk at the stats' speed, a crackling surge on Shift, and a body that turns to
/// face where the camera looks. Tells the class when a surge begins (Surge Strike drops a ball) and can be recharged at once (Lightning Reflexes). The Shaman's
/// own, like everything under Shaman/: it shares no code with the other classes' controllers.
/// </summary>
internal sealed class ShamanController
{
    public const string BodyModel = "shaman_hero.glb";

    /// <summary>The upper body's attack clip: the free hand lobbing the lightning as a cast leaves (see tools/hero_models.py; the blow lands halfway, the ready pose at both ends).</summary>
    public const string AttackClip = "Hurl";

    /// <summary>How quickly the upper body takes up its attack when a run's fighting starts, and lets it go after (fraction per second).</summary>
    private const float AttackFade = 5f;

    /// <summary>How quickly the body turns toward the camera's facing.</summary>
    private const float TurnRate = 14f;

    private readonly HeroBody _body = new(BodyModel);
    private readonly ClipWeight[] _layer = new ClipWeight[1];
    private float _attacking;
    private float _yaw;
    private float _surgeTimeLeft;
    private float _cooldownLeft;
    private float _cooldownLength = ShamanStats.BaseSurgeCooldown;
    private Vector3D<float> _surgeDirection;
    private bool _surgeKeyWasDown;

    /// <summary>0 while the surge is recharging, rising to 1 when it is ready again.</summary>
    public float SurgeReadiness => 1f - _cooldownLeft / _cooldownLength;

    /// <summary>The surge's push this frame (metres per second, flat), zero when not surging.</summary>
    public Vector3D<float> SurgeVelocity { get; private set; }

    /// <summary>The way a surge that began this frame goes, or null if none did.</summary>
    public Vector3D<float>? SurgeBegun { get; private set; }

    /// <summary>A stunned Shaman can't walk, jump or surge (the body still turns with the camera). <paramref name=\"attack\"/> is where the upper body's attack clip is (0 to 1), or null when it isn't fighting (at camp).</summary>
    public void Update(EngineWindow window, float deltaSeconds, ShamanStats stats, bool stunned, float? attack = null)
    {
        window.WalkSpeed = stunned ? 0f : stats.MoveSpeed;
        window.PlayerCanJump = !stunned;
        UpdateSurge(window, deltaSeconds, stats, stunned);
        UpdateBody(window, deltaSeconds, attack);
    }

    /// <summary>The surge is ready again at once (Lightning Reflexes).</summary>
    public void Recharge() => _cooldownLeft = 0f;

    /// <summary>Takes the body out of the world (another class was chosen). The next <see cref="Update"/> puts it back.</summary>
    public void Hide(EngineWindow window) => _body.Hide(window);

    /// <summary>Where the camera looks, flattened onto the ground.</summary>
    public static Vector3D<float> Facing(EngineWindow window)
    {
        var front = window.Camera?.Front ?? Vector3D<float>.UnitZ;
        var flat = new Vector3D<float>(front.X, 0f, front.Z);
        return flat.LengthSquared > 1e-6f ? Vector3D.Normalize(flat) : Vector3D<float>.UnitZ;
    }

    private void UpdateSurge(EngineWindow window, float deltaSeconds, ShamanStats stats, bool stunned)
    {
        _cooldownLeft = MathF.Max(0f, _cooldownLeft - deltaSeconds);
        SurgeBegun = null;

        var keyboard = window.Keyboard;
        bool surgeKey = keyboard is not null && (keyboard.IsKeyPressed(Key.ShiftLeft) || keyboard.IsKeyPressed(Key.ShiftRight));
        bool pressed = surgeKey && !_surgeKeyWasDown;
        _surgeKeyWasDown = surgeKey;

        if (pressed && !stunned && _cooldownLeft <= 0f && _surgeTimeLeft <= 0f)
        {
            // Surge the way the keys point, or straight ahead with none held.
            _surgeDirection = window.PlayerMoveDirection != Vector3D<float>.Zero ? window.PlayerMoveDirection : Facing(window);
            _surgeTimeLeft = ShamanStats.SurgeDuration;
            _cooldownLength = stats.SurgeCooldown;
            _cooldownLeft = _cooldownLength;
            SurgeBegun = _surgeDirection;
        }

        _surgeTimeLeft -= deltaSeconds;
        SurgeVelocity = _surgeTimeLeft > 0f ? _surgeDirection * ShamanStats.SurgeSpeed : Vector3D<float>.Zero;
    }

    private void UpdateBody(EngineWindow window, float deltaSeconds, float? attack)
    {
        var facing = Facing(window);
        float target = MathF.Atan2(facing.X, facing.Z);   // models face +Z; yaw 0 faces +Z
        float turn = MathF.IEEERemainder(target - _yaw, MathF.Tau);
        _yaw += turn * (1f - MathF.Exp(-TurnRate * deltaSeconds));

        _attacking = attack is null ? MathF.Max(0f, _attacking - AttackFade * deltaSeconds) : MathF.Min(1f, _attacking + AttackFade * deltaSeconds);
        _layer[0] = new ClipWeight(AttackClip, (attack ?? 0f) * _body.ClipLength(AttackClip), _attacking);
        _body.Pose(window, _yaw, deltaSeconds, _attacking > 0f ? _layer : ReadOnlySpan<ClipWeight>.Empty);
    }
}
