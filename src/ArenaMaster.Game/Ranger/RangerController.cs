using ArenaMaster.Game.Classes;
using CEngine.Core;
using Silk.NET.Input;
using Silk.NET.Maths;

namespace ArenaMaster.Game.Ranger;

/// <summary>
/// The Ranger's body and movement on top of the engine's third-person walker: a run at the stats' speed, a dash on Shift, and a body that turns to face where the camera aims (the bow fires
/// that way). The engine does the walking, jumping and collision; this sets the speed, works out the dash's push, and draws the body at the player's feet: its
/// legs run with the ground it covers (<see cref="HeroBody"/>), and while the bow is shooting the upper body draws and looses in time with it.
/// </summary>
internal sealed class RangerController
{
    public const string BodyModel = "ranger_hero.glb";

    /// <summary>The upper body's draw-and-loose clip, over one shot (0: the arrow has just left, 1: the next is about to).</summary>
    public const string ShootClip = "Shoot";

    /// <summary>How quickly the upper body takes up the bow when a run's shooting starts, and lowers it after (fraction per second).</summary>
    private const float ShootFade = 5f;

    public const float DashDuration = 0.18f;

    /// <summary>How quickly the body turns toward the aim: the fraction of the remaining turn made per second, roughly (exponential smoothing).</summary>
    private const float TurnRate = 16f;

    private readonly HeroBody _body = new(BodyModel);
    private readonly ClipWeight[] _layer = new ClipWeight[1];
    private float _shooting;
    private float _yaw;
    private float _dashTimeLeft;
    private float _cooldownLeft;
    private Vector3D<float> _dashDirection;
    private bool _dashKeyWasDown;
    private float _dashSpeed = RangerStats.BaseDashSpeed;

    private float _cooldownLength = RangerStats.BaseDashCooldown;

    /// <summary>0 while the dash is recharging, rising to 1 when it is ready again.</summary>
    public float DashReadiness => 1f - _cooldownLeft / _cooldownLength;

    /// <summary>The dash's push this frame (metres per second, flat), zero when not dashing. The content adds it to any knock-back and hands the sum to the engine.</summary>
    public Vector3D<float> DashVelocity { get; private set; }

    /// <summary>
    /// A stunned Ranger can't walk, jump or dash (the body still turns with the camera). <paramref name="draw"/> is how far the bow is through its shot (0 to 1),
    /// or null when it isn't shooting (at camp).
    /// </summary>
    public void Update(EngineWindow window, float deltaSeconds, RangerStats stats, bool stunned, float? draw)
    {
        window.WalkSpeed = stunned ? 0f : stats.MoveSpeed;
        window.PlayerCanJump = !stunned;
        UpdateDash(window, deltaSeconds, stats, stunned);
        UpdateBody(window, deltaSeconds, draw);
    }

    /// <summary>Takes the body out of the world (another class was chosen). The next <see cref="Update"/> puts it back.</summary>
    public void Hide(EngineWindow window) => _body.Hide(window);

    private void UpdateDash(EngineWindow window, float deltaSeconds, RangerStats stats, bool stunned)
    {
        _cooldownLeft = MathF.Max(0f, _cooldownLeft - deltaSeconds);

        var keyboard = window.Keyboard;
        bool dashKey = keyboard is not null && (keyboard.IsKeyPressed(Key.ShiftLeft) || keyboard.IsKeyPressed(Key.ShiftRight));
        bool pressed = dashKey && !_dashKeyWasDown;
        _dashKeyWasDown = dashKey;

        if (pressed && !stunned && _cooldownLeft <= 0f && _dashTimeLeft <= 0f)
        {
            // Dash the way the keys point, or straight ahead (where the camera looks) with none held.
            _dashDirection = window.PlayerMoveDirection != Vector3D<float>.Zero ? window.PlayerMoveDirection : AimFlat(window);
            _dashTimeLeft = DashDuration;
            _cooldownLength = stats.DashCooldown;
            _cooldownLeft = _cooldownLength;
            _dashSpeed = stats.DashSpeed;
        }

        _dashTimeLeft -= deltaSeconds;
        DashVelocity = _dashTimeLeft > 0f ? _dashDirection * _dashSpeed : Vector3D<float>.Zero;
    }

    private void UpdateBody(EngineWindow window, float deltaSeconds, float? draw)
    {
        var aim = AimFlat(window);
        float target = MathF.Atan2(aim.X, aim.Z);   // models face +Z; yaw 0 faces +Z
        float turn = MathF.IEEERemainder(target - _yaw, MathF.Tau);
        _yaw += turn * (1f - MathF.Exp(-TurnRate * deltaSeconds));

        _shooting = draw is null ? MathF.Max(0f, _shooting - ShootFade * deltaSeconds) : MathF.Min(1f, _shooting + ShootFade * deltaSeconds);
        _layer[0] = new ClipWeight(ShootClip, (draw ?? 1f) * _body.ClipLength(ShootClip), _shooting);
        _body.Pose(window, _yaw, deltaSeconds, _shooting > 0f ? _layer : ReadOnlySpan<ClipWeight>.Empty);
    }

    /// <summary>Where the camera looks, flattened onto the ground.</summary>
    private static Vector3D<float> AimFlat(EngineWindow window)
    {
        var front = window.Camera?.Front ?? Vector3D<float>.UnitZ;
        var flat = new Vector3D<float>(front.X, 0f, front.Z);
        return flat.LengthSquared > 1e-6f ? Vector3D.Normalize(flat) : Vector3D<float>.UnitZ;
    }
}
