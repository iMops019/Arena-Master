using CEngine.Core;
using Silk.NET.Maths;

namespace ArenaMaster.Game.Classes;

/// <summary>
/// How a hero's legs move, from where its feet go: every hero model has the same moving clips (made by <c>tools/hero_models.py</c>), and this mixes them each
/// frame. Standing is <c>Idle</c>. Running is one stride of <c>Run</c> and its versions to the side, each as many seconds long as the stride is metres, so the
/// clip moves on by the ground covered and a planted foot stays where it landed; the two versions either side of the way the hero is going are mixed by how
/// near each is, and running backward plays them backward. Off the ground for a moment is <c>Air</c>. Every change fades rather than snaps. The body's facing
/// is the class's own (it faces the aim); this only watches the feet. Pure: no window, so it can be tested.
/// </summary>
internal sealed class HeroMotion
{
    public const string IdleClip = "Idle";
    public const string AirClip = "Air";

    /// <summary>The run clips and the way each runs, in degrees from the body's front (+ to its right). Must match RUNS in tools/hero_models.py.</summary>
    public static readonly (string Clip, float Heading)[] Runs =
    {
        ("RunLeft", -90f), ("RunLeftAhead", -45f), ("Run", 0f), ("RunRightAhead", 45f), ("RunRight", 90f),
    };

    /// <summary>Slower than this (metres a second, flat) is standing.</summary>
    public const float MinRunSpeed = 0.8f;

    /// <summary>The stride never turns over faster than a run at this speed: a dash slides a little rather than blur the legs.</summary>
    public const float MaxStrideSpeed = 11f;

    /// <summary>How long fading between standing and running, or between running ahead and backward, takes.</summary>
    public const float FadeSeconds = 0.15f;

    /// <summary>How long off the ground before the body takes the air pose (a step down a slope isn't a jump), and how quickly it does.</summary>
    public const float AirDelay = 0.12f;
    public const float AirFadeSeconds = 0.1f;

    /// <summary>A move of more than this in one frame is a teleport, not a run.</summary>
    private const float TeleportDistance = 3f;

    /// <summary>How quickly the measured velocity and the mixed heading follow the real ones (per second, exponential).</summary>
    private const float VelocitySmoothing = 14f;
    private const float HeadingSmoothing = 10f;

    /// <summary>Turning around: running backward starts past this many degrees from the front, and ends inside the other.</summary>
    private const float BackwardFrom = 110f;
    private const float ForwardFrom = 70f;

    private Vector3D<float>? _lastFeet;
    private Vector2D<float> _velocity;
    private float _strideTime;
    private float _idleTime;
    private float _run;
    private float _air;
    private float _airborne;
    private bool _backward;
    private float _heading;
    private (string? Clip, float Time, float Weight) _fadingA, _fadingB;
    private float _fading;

    /// <summary>How fast the feet are going, flat (metres a second).</summary>
    public float Speed => _velocity.Length;

    /// <summary>How much of the pose is running (0 standing, 1 running).</summary>
    public float Running => _run;

    /// <summary>How much of the pose is the air pose.</summary>
    public float Airborne => _air;

    /// <summary>Whether the legs are running backward.</summary>
    public bool Backward => _backward;

    /// <summary>The run's heading the clips are mixed for: degrees from the front, -90 to 90 (backward runs mirror theirs).</summary>
    public float Heading => _heading;

    /// <summary>
    /// Moves the motion on by a frame: the feet are at <paramref name="feet"/>, the body faces <paramref name="bodyYaw"/> (radians, 0 = +Z, toward +X
    /// positive), and is or isn't standing on something. <paramref name="strideSeconds"/> and <paramref name="idleSeconds"/> are the model's Run and Idle
    /// clip lengths (the stride's length in metres, and the idle's loop).
    /// </summary>
    public void Update(Vector3D<float> feet, float bodyYaw, bool grounded, float deltaSeconds, float strideSeconds, float idleSeconds)
    {
        if (deltaSeconds <= 0f)
        {
            return;
        }

        var moved = _lastFeet is { } last ? new Vector2D<float>(feet.X - last.X, feet.Z - last.Z) : Vector2D<float>.Zero;
        _lastFeet = feet;
        if (moved.Length > TeleportDistance)
        {
            moved = Vector2D<float>.Zero;
            _velocity = Vector2D<float>.Zero;
        }

        _velocity += (moved / deltaSeconds - _velocity) * (1f - MathF.Exp(-VelocitySmoothing * deltaSeconds));
        float speed = _velocity.Length;

        _airborne = grounded ? 0f : _airborne + deltaSeconds;
        _air = Approach(_air, _airborne > AirDelay ? 1f : 0f, deltaSeconds / AirFadeSeconds);
        _run = Approach(_run, speed > MinRunSpeed ? 1f : 0f, deltaSeconds / FadeSeconds);
        _idleTime = Wrap(_idleTime + deltaSeconds, idleSeconds);
        _fading = MathF.Max(0f, _fading - deltaSeconds / FadeSeconds);

        if (speed > MinRunSpeed)
        {
            float relative = Degrees(MathF.Atan2(_velocity.X, _velocity.Y) - bodyYaw);
            bool backward = _backward ? MathF.Abs(relative) > ForwardFrom : MathF.Abs(relative) > BackwardFrom;
            float target = Math.Clamp(backward ? Degrees(Radians(relative) - MathF.PI) : relative, -90f, 90f);
            if (backward != _backward)
            {
                // Turning around: the old way's run fades out where it was while the new one takes over.
                (_fadingA, _fadingB) = RunPair(_heading, _strideTime);
                _fading = 1f;
                _backward = backward;
                _heading = target;
            }
            else
            {
                _heading += (target - _heading) * (1f - MathF.Exp(-HeadingSmoothing * deltaSeconds));
            }
        }

        if (!grounded)
        {
            return;   // the legs hold their stride in the air, so a landing picks it up where it left off
        }

        float stride = MathF.Min(speed, MaxStrideSpeed) * deltaSeconds;
        _strideTime = Wrap(_strideTime + (_backward ? -stride : stride), strideSeconds);
    }

    /// <summary>Writes this frame's clips and their weights into <paramref name="into"/> (room for 6) and returns how many.</summary>
    public int Pose(Span<ClipWeight> into)
    {
        int count = 0;
        float ground = 1f - _air;
        float standing = ground * (1f - _run);
        float running = ground * _run;
        if (standing > 0f)
        {
            into[count++] = new ClipWeight(IdleClip, _idleTime, standing);
        }

        if (_air > 0f)
        {
            into[count++] = new ClipWeight(AirClip, 0f, _air);
        }

        if (running > 0f)
        {
            var (a, b) = RunPair(_heading, _strideTime);
            float now = running * (1f - _fading);
            float then = running * _fading;
            Add(into, ref count, a.Clip, a.Time, a.Weight * now);
            Add(into, ref count, b.Clip, b.Time, b.Weight * now);
            Add(into, ref count, _fadingA.Clip, _fadingA.Time, _fadingA.Weight * then);
            Add(into, ref count, _fadingB.Clip, _fadingB.Time, _fadingB.Weight * then);
        }

        if (count == 0)
        {
            into[count++] = new ClipWeight(IdleClip, _idleTime);
        }

        return count;
    }

    /// <summary>The two run clips either side of <paramref name="heading"/>, each weighted by how near it is, at the stride's time.</summary>
    public static ((string Clip, float Time, float Weight) A, (string Clip, float Time, float Weight) B) RunPair(float heading, float time)
    {
        heading = Math.Clamp(heading, Runs[0].Heading, Runs[^1].Heading);
        for (int i = 0; i < Runs.Length - 1; i++)
        {
            var (low, high) = (Runs[i], Runs[i + 1]);
            if (heading <= high.Heading)
            {
                float toHigh = (heading - low.Heading) / (high.Heading - low.Heading);
                return ((low.Clip, time, 1f - toHigh), (high.Clip, time, toHigh));
            }
        }

        return ((Runs[^1].Clip, time, 1f), (Runs[^1].Clip, time, 0f));
    }

    private static void Add(Span<ClipWeight> into, ref int count, string? clip, float time, float weight)
    {
        if (weight > 0f && clip is not null)
        {
            into[count++] = new ClipWeight(clip, time, weight);
        }
    }

    private static float Approach(float value, float target, float step) =>
        value < target ? MathF.Min(target, value + step) : MathF.Max(target, value - step);

    private static float Wrap(float time, float length) => length > 0f ? ((time % length) + length) % length : 0f;

    private static float Degrees(float radians) => MathF.IEEERemainder(radians, MathF.Tau) * 180f / MathF.PI;

    private static float Radians(float degrees) => degrees * MathF.PI / 180f;
}

/// <summary>
/// A hero's body in the world: its skinned model at the player's feet, facing where the class says, its legs moved by a <see cref="HeroMotion"/> and, if the
/// class gives one, an attack laid over the chest (<see cref="LayerJoint"/>) and everything under it. The model is loaded the first time it is drawn.
/// </summary>
internal sealed class HeroBody
{
    /// <summary>The joint an attack clip is laid over: the chest carries the head, the arms and whatever the hands hold.</summary>
    public const string LayerJoint = "chest";

    private readonly string _model;
    private readonly HeroMotion _motion = new();
    private readonly ClipWeight[] _mix = new ClipWeight[6];
    private IReadOnlyDictionary<string, float>? _clips;
    private bool _placed;

    public HeroBody(string model) => _model = model;

    public HeroMotion Motion => _motion;

    /// <summary>A clip's length in seconds (0 until the model is loaded, or if it has no such clip).</summary>
    public float ClipLength(string clip) => _clips is { } clips && clips.TryGetValue(clip, out float seconds) ? seconds : 0f;

    /// <summary>Poses the body for this frame, facing <paramref name="yaw"/>, with <paramref name="layer"/> over the chest.</summary>
    public void Pose(EngineWindow window, float yaw, float deltaSeconds, ReadOnlySpan<ClipWeight> layer = default)
    {
        if (_clips is null)
        {
            if (!window.TryLoadSkinnedModel(_model, Path.Combine(EngineAssets.RepoRoot, "assets")))
            {
                return;
            }

            _clips = window.SkinnedClipDurations(_model);
        }

        _motion.Update(window.PlayerFeet, yaw, window.PlayerGrounded, deltaSeconds, ClipLength("Run"), ClipLength(HeroMotion.IdleClip));
        int count = _motion.Pose(_mix);
        window.SetSkinnedPropBlend(_model, _mix.AsSpan(0, count), window.PlayerFeet, yaw, 1f, layer: layer, layerJoint: layer.IsEmpty ? null : LayerJoint);
        _placed = true;
    }

    /// <summary>Takes the body out of the world (another class was chosen). The next <see cref="Pose"/> puts it back.</summary>
    public void Hide(EngineWindow window)
    {
        if (_placed)
        {
            window.RemoveSkinnedProp(_model);
            _placed = false;
        }
    }
}
