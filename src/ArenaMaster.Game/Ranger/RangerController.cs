using CEngine.Core;
using Silk.NET.Input;
using Silk.NET.Maths;

namespace ArenaMaster.Game.Ranger;

/// <summary>
/// The Ranger's body and movement on top of the engine's third-person walker: a run at the stats' speed, a dash on Shift, and a body that turns to face where the camera aims (the bow fires
/// that way). The engine does the walking, jumping and collision; this sets the speed, pushes for the dash, and draws the body at the player's feet.
/// </summary>
internal sealed class RangerController
{
    public const string BodyModel = "ranger_placeholder.glb";

    /// <summary>Extra speed during a dash, on top of running, metres per second.</summary>
    public const float DashSpeed = 20f;

    public const float DashDuration = 0.18f;
    public const float DashCooldown = 1.2f;

    /// <summary>How quickly the body turns toward the aim: the fraction of the remaining turn made per second, roughly (exponential smoothing).</summary>
    private const float TurnRate = 16f;

    private int? _bodyId;
    private float _yaw;
    private float _dashTimeLeft;
    private float _cooldownLeft;
    private Vector3D<float> _dashDirection;
    private bool _dashKeyWasDown;

    /// <summary>0 while the dash is recharging, rising to 1 when it is ready again.</summary>
    public float DashReadiness => 1f - _cooldownLeft / DashCooldown;

    public void Update(EngineWindow window, float deltaSeconds, RangerStats stats)
    {
        window.WalkSpeed = stats.MoveSpeed;
        UpdateDash(window, deltaSeconds);
        UpdateBody(window, deltaSeconds);
    }

    private void UpdateDash(EngineWindow window, float deltaSeconds)
    {
        _cooldownLeft = MathF.Max(0f, _cooldownLeft - deltaSeconds);

        var keyboard = window.Keyboard;
        bool dashKey = keyboard is not null && (keyboard.IsKeyPressed(Key.ShiftLeft) || keyboard.IsKeyPressed(Key.ShiftRight));
        bool pressed = dashKey && !_dashKeyWasDown;
        _dashKeyWasDown = dashKey;

        if (pressed && _cooldownLeft <= 0f && _dashTimeLeft <= 0f)
        {
            // Dash the way the keys point, or straight ahead (where the camera looks) with none held.
            _dashDirection = window.PlayerMoveDirection != Vector3D<float>.Zero ? window.PlayerMoveDirection : AimFlat(window);
            _dashTimeLeft = DashDuration;
            _cooldownLeft = DashCooldown;
        }

        if (_dashTimeLeft > 0f)
        {
            _dashTimeLeft -= deltaSeconds;
            window.PlayerPush = _dashTimeLeft > 0f ? _dashDirection * DashSpeed : Vector3D<float>.Zero;
        }
    }

    private void UpdateBody(EngineWindow window, float deltaSeconds)
    {
        var aim = AimFlat(window);
        float target = MathF.Atan2(aim.X, aim.Z);   // models face +Z; yaw 0 faces +Z
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
    private static Vector3D<float> AimFlat(EngineWindow window)
    {
        var front = window.Camera?.Front ?? Vector3D<float>.UnitZ;
        var flat = new Vector3D<float>(front.X, 0f, front.Z);
        return flat.LengthSquared > 1e-6f ? Vector3D.Normalize(flat) : Vector3D<float>.UnitZ;
    }
}
