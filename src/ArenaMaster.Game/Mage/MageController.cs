using ArenaMaster.Game.Classes;
using CEngine.Core;
using Silk.NET.Input;
using Silk.NET.Maths;

namespace ArenaMaster.Game.Mage;

/// <summary>
/// The Mage's body and movement on top of the engine's third-person walker: a walk at the stats' speed, a blink on Shift (a very short, very fast push), and a body
/// that turns to face where the camera looks. The engine does the walking, jumping and collision; this sets the speed, works out the blink's push, and draws the
/// body at the player's feet. The Mage's own, like everything under Mage/: it shares no code with the other classes' controllers.
/// </summary>
internal sealed class MageController
{
    public const string BodyModel = "mage_hero.glb";

    /// <summary>The upper body's attack clip: the staff thrust out as a barrage leaves (see tools/hero_models.py; the blow lands halfway, the ready pose at both ends).</summary>
    public const string AttackClip = "Cast";

    /// <summary>How quickly the upper body takes up its attack when a run's fighting starts, and lets it go after (fraction per second).</summary>
    private const float AttackFade = 5f;

    /// <summary>How quickly the body turns toward the camera's facing.</summary>
    private const float TurnRate = 14f;

    private readonly HeroBody _body = new(BodyModel);
    private readonly ClipWeight[] _layer = new ClipWeight[1];
    private float _attacking;
    private float _yaw;
    private float _blinkTimeLeft;
    private float _cooldownLeft;
    private Vector3D<float> _blinkDirection;
    private bool _blinkKeyWasDown;
    private float _cooldownLength = MageStats.BlinkCooldown;

    /// <summary>0 while the blink is recharging, rising to 1 when it is ready again.</summary>
    public float BlinkReadiness => 1f - _cooldownLeft / _cooldownLength;

    /// <summary>The blink's push this frame (metres per second, flat), zero when not blinking.</summary>
    public Vector3D<float> BlinkVelocity { get; private set; }

    /// <summary>A stunned Mage can't walk, jump or blink (the body still turns with the camera). <paramref name=\"attack\"/> is where the upper body's attack clip is (0 to 1), or null when it isn't fighting (at camp).</summary>
    public void Update(EngineWindow window, float deltaSeconds, MageStats stats, bool stunned, float? attack = null)
    {
        window.WalkSpeed = stunned ? 0f : stats.MoveSpeed;
        window.PlayerCanJump = !stunned;
        UpdateBlink(window, deltaSeconds, stats, stunned);
        UpdateBody(window, deltaSeconds, attack);
    }

    /// <summary>Takes the body out of the world (another class was chosen). The next <see cref="Update"/> puts it back.</summary>
    public void Hide(EngineWindow window) => _body.Hide(window);

    private void UpdateBlink(EngineWindow window, float deltaSeconds, MageStats stats, bool stunned)
    {
        _cooldownLeft = MathF.Max(0f, _cooldownLeft - deltaSeconds);

        var keyboard = window.Keyboard;
        bool blinkKey = keyboard is not null && (keyboard.IsKeyPressed(Key.ShiftLeft) || keyboard.IsKeyPressed(Key.ShiftRight));
        bool pressed = blinkKey && !_blinkKeyWasDown;
        _blinkKeyWasDown = blinkKey;

        if (pressed && !stunned && _cooldownLeft <= 0f && _blinkTimeLeft <= 0f)
        {
            // Blink the way the keys point, or straight ahead with none held.
            _blinkDirection = window.PlayerMoveDirection != Vector3D<float>.Zero ? window.PlayerMoveDirection : Facing(window);
            _blinkTimeLeft = MageStats.BlinkDuration;
            _cooldownLength = stats.BlinkRecharge;
            _cooldownLeft = _cooldownLength;
        }

        _blinkTimeLeft -= deltaSeconds;
        BlinkVelocity = _blinkTimeLeft > 0f ? _blinkDirection * MageStats.BlinkSpeed : Vector3D<float>.Zero;
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

    /// <summary>Where the camera looks, flattened onto the ground.</summary>
    public static Vector3D<float> Facing(EngineWindow window)
    {
        var front = window.Camera?.Front ?? Vector3D<float>.UnitZ;
        var flat = new Vector3D<float>(front.X, 0f, front.Z);
        return flat.LengthSquared > 1e-6f ? Vector3D.Normalize(flat) : Vector3D<float>.UnitZ;
    }
}
