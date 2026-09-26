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
    public const string BodyModel = "mage_placeholder.glb";

    /// <summary>How quickly the body turns toward the camera's facing.</summary>
    private const float TurnRate = 14f;

    private int? _bodyId;
    private float _yaw;
    private float _blinkTimeLeft;
    private float _cooldownLeft;
    private Vector3D<float> _blinkDirection;
    private bool _blinkKeyWasDown;

    /// <summary>0 while the blink is recharging, rising to 1 when it is ready again.</summary>
    public float BlinkReadiness => 1f - _cooldownLeft / MageStats.BlinkCooldown;

    /// <summary>The blink's push this frame (metres per second, flat), zero when not blinking.</summary>
    public Vector3D<float> BlinkVelocity { get; private set; }

    /// <summary>A stunned Mage can't walk, jump or blink (the body still turns with the camera).</summary>
    public void Update(EngineWindow window, float deltaSeconds, MageStats stats, bool stunned)
    {
        window.WalkSpeed = stunned ? 0f : stats.MoveSpeed;
        window.PlayerCanJump = !stunned;
        UpdateBlink(window, deltaSeconds, stunned);
        UpdateBody(window, deltaSeconds);
    }

    /// <summary>Takes the body out of the world (another class was chosen). The next <see cref="Update"/> puts it back.</summary>
    public void Hide(EngineWindow window)
    {
        if (_bodyId is { } id)
        {
            window.RemovePlacedProp(id);
            _bodyId = null;
        }
    }

    private void UpdateBlink(EngineWindow window, float deltaSeconds, bool stunned)
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
            _cooldownLeft = MageStats.BlinkCooldown;
        }

        _blinkTimeLeft -= deltaSeconds;
        BlinkVelocity = _blinkTimeLeft > 0f ? _blinkDirection * MageStats.BlinkSpeed : Vector3D<float>.Zero;
    }

    private void UpdateBody(EngineWindow window, float deltaSeconds)
    {
        var facing = Facing(window);
        float target = MathF.Atan2(facing.X, facing.Z);   // models face +Z; yaw 0 faces +Z
        float turn = MathF.IEEERemainder(target - _yaw, MathF.Tau);
        _yaw += turn * (1f - MathF.Exp(-TurnRate * deltaSeconds));

        var placement = new PropPlacement(BodyModel, window.PlayerFeet, _yaw, 1f);
        if (_bodyId is { } id)
        {
            window.SetPlacedProp(id, placement);
        }
        else
        {
            _bodyId = window.PlaceProp(placement);
        }
    }

    /// <summary>Where the camera looks, flattened onto the ground.</summary>
    public static Vector3D<float> Facing(EngineWindow window)
    {
        var front = window.Camera?.Front ?? Vector3D<float>.UnitZ;
        var flat = new Vector3D<float>(front.X, 0f, front.Z);
        return flat.LengthSquared > 1e-6f ? Vector3D.Normalize(flat) : Vector3D<float>.UnitZ;
    }
}
