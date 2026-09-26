using CEngine.Core;
using Silk.NET.Input;
using Silk.NET.Maths;

namespace ArenaMaster.Game.Warrior;

/// <summary>
/// The Warrior's body and movement on top of the engine's third-person walker: a walk at the stats' speed, a battle charge on Shift, and a body that turns to face
/// where the camera looks (where the axes cleave). The engine does the walking, jumping and collision; this sets the speed, works out the charge's push, and draws
/// the body at the player's feet. The Warrior's own, like everything under Warrior/.
/// </summary>
internal sealed class WarriorController
{
    public const string BodyModel = "warrior_placeholder.glb";

    /// <summary>How quickly the body turns toward the camera's facing.</summary>
    private const float TurnRate = 13f;

    private int? _bodyId;
    private float _yaw;
    private float _chargeTimeLeft;
    private float _cooldownLeft;
    private Vector3D<float> _chargeDirection;
    private bool _chargeKeyWasDown;
    private float _cooldownLength = WarriorStats.BaseChargeCooldown;

    /// <summary>0 while the charge is recharging, rising to 1 when it is ready again.</summary>
    public float ChargeReadiness => 1f - _cooldownLeft / _cooldownLength;

    /// <summary>The charge's push this frame (metres per second, flat), zero when not charging.</summary>
    public Vector3D<float> ChargeVelocity { get; private set; }

    /// <summary>A stunned Warrior can't walk, jump or charge (the body still turns with the camera).</summary>
    public void Update(EngineWindow window, float deltaSeconds, WarriorStats stats, bool stunned)
    {
        window.WalkSpeed = stunned ? 0f : stats.MoveSpeed;
        window.PlayerCanJump = !stunned;
        UpdateCharge(window, deltaSeconds, stats, stunned);
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

    /// <summary>The way the camera looks, as a yaw (0 is +Z): where the axes cleave.</summary>
    public static float FacingYaw(EngineWindow window)
    {
        var facing = Facing(window);
        return MathF.Atan2(facing.X, facing.Z);
    }

    private void UpdateCharge(EngineWindow window, float deltaSeconds, WarriorStats stats, bool stunned)
    {
        _cooldownLeft = MathF.Max(0f, _cooldownLeft - deltaSeconds);

        var keyboard = window.Keyboard;
        bool chargeKey = keyboard is not null && (keyboard.IsKeyPressed(Key.ShiftLeft) || keyboard.IsKeyPressed(Key.ShiftRight));
        bool pressed = chargeKey && !_chargeKeyWasDown;
        _chargeKeyWasDown = chargeKey;

        if (pressed && !stunned && _cooldownLeft <= 0f && _chargeTimeLeft <= 0f)
        {
            // Charge the way the keys point, or straight ahead with none held.
            _chargeDirection = window.PlayerMoveDirection != Vector3D<float>.Zero ? window.PlayerMoveDirection : Facing(window);
            _chargeTimeLeft = WarriorStats.ChargeDuration;
            _cooldownLength = stats.ChargeCooldown;
            _cooldownLeft = _cooldownLength;
        }

        _chargeTimeLeft -= deltaSeconds;
        ChargeVelocity = _chargeTimeLeft > 0f ? _chargeDirection * WarriorStats.BaseChargeSpeed : Vector3D<float>.Zero;
    }

    private void UpdateBody(EngineWindow window, float deltaSeconds)
    {
        float target = FacingYaw(window);   // models face +Z; yaw 0 faces +Z
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
    private static Vector3D<float> Facing(EngineWindow window)
    {
        var front = window.Camera?.Front ?? Vector3D<float>.UnitZ;
        var flat = new Vector3D<float>(front.X, 0f, front.Z);
        return flat.LengthSquared > 1e-6f ? Vector3D.Normalize(flat) : Vector3D<float>.UnitZ;
    }
}
