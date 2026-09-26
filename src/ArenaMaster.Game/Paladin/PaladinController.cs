using CEngine.Core;
using Silk.NET.Input;
using Silk.NET.Maths;

namespace ArenaMaster.Game.Paladin;

/// <summary>
/// The Paladin's body and movement on top of the engine's third-person walker: a steady walk at the stats' speed, a shield rush on Shift (shorter and heavier than
/// the Ranger's dash), and a body that turns to face where the camera looks. The engine does the walking, jumping and collision; this sets the speed, works out
/// the rush's push, and draws the body at the player's feet. The Paladin's own, like everything under Paladin/: it shares no code with the Ranger's controller.
/// </summary>
internal sealed class PaladinController
{
    public const string BodyModel = "paladin_placeholder.glb";

    /// <summary>How quickly the body turns toward the camera's facing (a heavier turn than the Ranger's).</summary>
    private const float TurnRate = 11f;

    private int? _bodyId;
    private float _yaw;
    private float _rushTimeLeft;
    private float _cooldownLeft;
    private Vector3D<float> _rushDirection;
    private bool _rushKeyWasDown;
    private float _cooldownLength = PaladinStats.BaseRushCooldown;

    /// <summary>0 while the rush is recharging, rising to 1 when it is ready again.</summary>
    public float RushReadiness => 1f - _cooldownLeft / _cooldownLength;

    /// <summary>The rush's push this frame (metres per second, flat), zero when not rushing.</summary>
    public Vector3D<float> RushVelocity { get; private set; }

    /// <summary>A stunned Paladin can't walk, jump or rush (the body still turns with the camera).</summary>
    public void Update(EngineWindow window, float deltaSeconds, PaladinStats stats, bool stunned)
    {
        window.WalkSpeed = stunned ? 0f : stats.MoveSpeed;
        window.PlayerCanJump = !stunned;
        UpdateRush(window, deltaSeconds, stats, stunned);
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

    private void UpdateRush(EngineWindow window, float deltaSeconds, PaladinStats stats, bool stunned)
    {
        _cooldownLeft = MathF.Max(0f, _cooldownLeft - deltaSeconds);

        var keyboard = window.Keyboard;
        bool rushKey = keyboard is not null && (keyboard.IsKeyPressed(Key.ShiftLeft) || keyboard.IsKeyPressed(Key.ShiftRight));
        bool pressed = rushKey && !_rushKeyWasDown;
        _rushKeyWasDown = rushKey;

        if (pressed && !stunned && _cooldownLeft <= 0f && _rushTimeLeft <= 0f)
        {
            // Rush the way the keys point, or straight ahead with none held.
            _rushDirection = window.PlayerMoveDirection != Vector3D<float>.Zero ? window.PlayerMoveDirection : Facing(window);
            _rushTimeLeft = PaladinStats.RushDuration;
            _cooldownLength = stats.RushCooldown;
            _cooldownLeft = _cooldownLength;
        }

        _rushTimeLeft -= deltaSeconds;
        RushVelocity = _rushTimeLeft > 0f ? _rushDirection * PaladinStats.BaseRushSpeed : Vector3D<float>.Zero;
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
    private static Vector3D<float> Facing(EngineWindow window)
    {
        var front = window.Camera?.Front ?? Vector3D<float>.UnitZ;
        var flat = new Vector3D<float>(front.X, 0f, front.Z);
        return flat.LengthSquared > 1e-6f ? Vector3D.Normalize(flat) : Vector3D<float>.UnitZ;
    }
}
