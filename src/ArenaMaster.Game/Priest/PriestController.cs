using ArenaMaster.Game.Classes;
using CEngine.Core;
using Silk.NET.Input;
using Silk.NET.Maths;

namespace ArenaMaster.Game.Priest;

/// <summary>
/// The Priest's body and movement on top of the engine's third-person walker: a walk at the stats' speed, Rotting Step on Shift (dissolving into rot and
/// reappearing a few metres on: a very short, very fast push, leaving rot where it vanished), and a body that turns to face where the camera looks. The engine
/// does the walking, jumping and collision; this sets the speed, works out the step's push, and draws the body at the player's feet. The Priest's own, like
/// everything under Priest/.
/// </summary>
internal sealed class PriestController
{
    public const string BodyModel = "priest_hero.glb";

    /// <summary>The upper body's attack clip: the skull wand thrust out as a skull leaves it (see tools/hero_models.py; the blow lands halfway, the ready pose at both ends).</summary>
    public const string AttackClip = "Hex";

    /// <summary>How quickly the upper body takes up its attack when a run's fighting starts, and lets it go after (fraction per second).</summary>
    private const float AttackFade = 5f;

    /// <summary>How quickly the body turns toward its enemy or its walk.</summary>
    private const float TurnRate = 13f;

    private readonly HeroBody _body = new(BodyModel);
    private readonly ClipWeight[] _layer = new ClipWeight[1];
    private float _attacking;
    private float _yaw;
    private float _stepTimeLeft;
    private float _cooldownLeft;
    private Vector3D<float> _stepDirection;
    private bool _stepKeyWasDown;
    private float _cooldownLength = PriestStats.BaseStepCooldown;

    /// <summary>0 while Rotting Step is recharging, rising to 1 when it is ready again.</summary>
    public float StepReadiness => 1f - _cooldownLeft / _cooldownLength;

    /// <summary>The step's push this frame (metres per second, flat), zero when not stepping.</summary>
    public Vector3D<float> StepVelocity { get; private set; }

    /// <summary>Where the Priest vanished, the frame a Rotting Step began (null otherwise): the rot goes there.</summary>
    public Vector3D<float>? StepBegunAt { get; private set; }

    /// <summary>Whether the Priest is between vanishing and reappearing.</summary>
    public bool Stepping => _stepTimeLeft > 0f;

    /// <summary>A stunned Priest can't walk, jump or step (the body still turns to <paramref name="aim"/>). <paramref name="attack"/> is where the upper body's attack clip is (0 to 1), or null when it isn't fighting (at camp). <paramref name="aim"/> is the flat way to the enemy it fights, or null with none: the body faces it, or else the way it walks, so the mouse is free to look round.</summary>
    public void Update(EngineWindow window, float deltaSeconds, PriestStats stats, bool stunned, float? attack = null, Vector3D<float>? aim = null)
    {
        window.WalkSpeed = stunned ? 0f : stats.MoveSpeed;
        window.PlayerCanJump = !stunned;
        UpdateStep(window, deltaSeconds, stats, stunned);
        UpdateBody(window, deltaSeconds, attack, aim);
    }

    /// <summary>Takes the body out of the world (another class was chosen). The next <see cref="Update"/> puts it back.</summary>
    public void Hide(EngineWindow window) => _body.Hide(window);

    /// <summary>The flat way the body faces now.</summary>
    public Vector3D<float> Facing => new(MathF.Sin(_yaw), 0f, MathF.Cos(_yaw));

    /// <summary>Where the camera looks, flattened onto the ground: which way the Shift move goes with no keys held.</summary>
    private static Vector3D<float> Looking(EngineWindow window)
    {
        var front = window.Camera?.Front ?? Vector3D<float>.UnitZ;
        var flat = new Vector3D<float>(front.X, 0f, front.Z);
        return flat.LengthSquared > 1e-6f ? Vector3D.Normalize(flat) : Vector3D<float>.UnitZ;
    }

    private void UpdateStep(EngineWindow window, float deltaSeconds, PriestStats stats, bool stunned)
    {
        _cooldownLeft = MathF.Max(0f, _cooldownLeft - deltaSeconds);
        StepBegunAt = null;

        var keyboard = window.Keyboard;
        bool stepKey = keyboard is not null && (keyboard.IsKeyPressed(Key.ShiftLeft) || keyboard.IsKeyPressed(Key.ShiftRight));
        bool pressed = stepKey && !_stepKeyWasDown;
        _stepKeyWasDown = stepKey;

        if (pressed && !stunned && _cooldownLeft <= 0f && _stepTimeLeft <= 0f)
        {
            // Step the way the keys point, or straight ahead with none held.
            _stepDirection = window.PlayerMoveDirection != Vector3D<float>.Zero ? window.PlayerMoveDirection : Looking(window);
            _stepTimeLeft = PriestStats.StepDuration;
            _cooldownLength = stats.StepCooldown;
            _cooldownLeft = _cooldownLength;
            StepBegunAt = window.PlayerFeet;
        }

        _stepTimeLeft -= deltaSeconds;
        StepVelocity = _stepTimeLeft > 0f ? _stepDirection * PriestStats.StepSpeed : Vector3D<float>.Zero;
    }

    private void UpdateBody(EngineWindow window, float deltaSeconds, float? attack, Vector3D<float>? aim)
    {
        var facing = aim ?? (window.PlayerMoveDirection != Vector3D<float>.Zero ? window.PlayerMoveDirection : (Vector3D<float>?)null);
        if (facing is { } face && face != Vector3D<float>.Zero)
        {
            float target = MathF.Atan2(face.X, face.Z);   // models face +Z; yaw 0 faces +Z
            float turn = MathF.IEEERemainder(target - _yaw, MathF.Tau);
            _yaw += turn * (1f - MathF.Exp(-TurnRate * deltaSeconds));
        }

        _attacking = attack is null ? MathF.Max(0f, _attacking - AttackFade * deltaSeconds) : MathF.Min(1f, _attacking + AttackFade * deltaSeconds);
        _layer[0] = new ClipWeight(AttackClip, (attack ?? 0f) * _body.ClipLength(AttackClip), _attacking);
        _body.Pose(window, _yaw, deltaSeconds, _attacking > 0f ? _layer : ReadOnlySpan<ClipWeight>.Empty);
    }
}
