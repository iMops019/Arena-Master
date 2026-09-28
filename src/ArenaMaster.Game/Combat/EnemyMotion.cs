using Silk.NET.Maths;

namespace ArenaMaster.Game.Combat;

/// <summary>
/// Which clip each enemy's animated model plays, and where in it, from what the enemy is doing (made by <c>tools/enemy_models.py</c>). Standing is <c>Idle</c>;
/// walking is <c>Walk</c>, one stride as many seconds long as it is metres, moved on by the ground the enemy covers so its planted feet stay put; an attack plays
/// the clip named after its <see cref="AttackType"/>, its wind-up, blow and recovery each mapped onto their stretch of it (<see cref="WindUpEnd"/>,
/// <see cref="ActiveEnd"/>); a frozen enemy stops where it is; a dying one plays <c>Die</c> over its <see cref="EnemyField.DeathDuration"/>. A change of clip
/// fades over <see cref="FadeSeconds"/>. Pure: no window, so it can be tested.
/// </summary>
internal sealed class EnemyMotion
{
    public const string IdleClip = "Idle";
    public const string WalkClip = "Walk";
    public const string DieClip = "Die";

    /// <summary>A boss's roar at the start of a stage (the Marauder's growl into his Frenzied Rage), played over the roar.</summary>
    public const string RoarClip = "Roar";

    /// <summary>Where an attack clip's wind-up ends and its blow ends (fractions of its one second). Must match WINDUP_END and ACTIVE_END in tools/enemy_models.py.</summary>
    public const float WindUpEnd = 0.4f;
    public const float ActiveEnd = 0.7f;

    public const float FadeSeconds = 0.15f;

    /// <summary>Faster than this (metres a second, flat) starts a walk, and a walk goes on until slower than half of it (a shove from the crowd is not a step).</summary>
    public const float MinWalkSpeed = 0.4f;

    /// <summary>A move of more than this in one frame is a jump in place (a spawn, a teleport), not a walk.</summary>
    private const float TeleportDistance = 3f;

    /// <summary>One enemy's clip this frame: the clip and its time, and a clip it is fading out of, at its time, and how much of the pose that still is.</summary>
    public readonly record struct Pose(string Clip, float Time, string? From, float FromTime, float Fade);

    private sealed class State
    {
        public Vector3D<float> Last;
        public bool Seen;
        public float Stride;
        public float Idle;
        public string Clip = IdleClip;
        public float Time;
        public string? From;
        public float FromTime;
        public float Fade;
        public bool Walking;
    }

    private readonly Dictionary<int, State> _states = new();

    /// <summary>
    /// Moves <paramref name="enemy"/>'s animation on by <paramref name="deltaSeconds"/> and returns its pose. <paramref name="clips"/> are its model's clips and
    /// their lengths; an attack the model has no clip for plays on as a walk or a stand.
    /// </summary>
    public Pose Update(Enemy enemy, float deltaSeconds, IReadOnlyDictionary<string, float> clips)
    {
        if (!_states.TryGetValue(enemy.Id, out var state))
        {
            state = new State { Idle = enemy.Phase };   // each starts its idle somewhere different
            _states[enemy.Id] = state;
        }

        var flat = new Vector2D<float>(enemy.Position.X - state.Last.X, enemy.Position.Z - state.Last.Z);
        float moved = state.Seen && flat.Length < TeleportDistance ? flat.Length : 0f;
        state.Last = enemy.Position;
        state.Seen = true;
        state.Idle = Wrap(state.Idle + deltaSeconds, clips.GetValueOrDefault(IdleClip));

        var (clip, time) = Choose(enemy, state, moved, deltaSeconds, clips);
        if (clip != state.Clip)
        {
            state.From = state.Clip;
            state.FromTime = state.Time;
            state.Fade = 1f;
            state.Clip = clip;
        }

        state.Time = time;
        state.Fade = MathF.Max(0f, state.Fade - (deltaSeconds > 0f ? deltaSeconds / FadeSeconds : 0f));
        return new Pose(state.Clip, state.Time, state.Fade > 0f ? state.From : null, state.FromTime, state.Fade);
    }

    /// <summary>Forgets an enemy that has gone.</summary>
    public void Forget(Enemy enemy) => _states.Remove(enemy.Id);

    public void Clear() => _states.Clear();

    /// <summary>Where an attack's clip is: its wind-up, blow and recovery each laid over their own stretch of the clip's one second.</summary>
    public static float AttackTime(Enemy enemy, AttackSpec attack) => enemy.AttackPhase switch
    {
        AttackPhase.WindUp => WindUpEnd * enemy.WindUpProgress,
        AttackPhase.Active => WindUpEnd + (ActiveEnd - WindUpEnd) * Math.Clamp(enemy.PhaseTime / MathF.Max(attack.Active, 1e-3f), 0f, 1f),
        _ => ActiveEnd + (1f - ActiveEnd) * Math.Clamp(enemy.PhaseTime / MathF.Max(attack.Recover, 1e-3f), 0f, 1f),
    };

    private static (string Clip, float Time) Choose(Enemy enemy, State state, float moved, float deltaSeconds, IReadOnlyDictionary<string, float> clips)
    {
        if (!enemy.IsAlive)
        {
            return (DieClip, clips.GetValueOrDefault(DieClip) * Math.Clamp(enemy.DeadFor / EnemyField.DeathDuration, 0f, 1f));
        }

        if (enemy.IsFrozen)
        {
            return (state.Clip, state.Time);   // held fast in the ice, mid-stride or mid-swing
        }

        if (enemy.IsRoaring && clips.TryGetValue(RoarClip, out float roar))
        {
            return (RoarClip, roar * Math.Clamp(1f - enemy.RoarLeft / MathF.Max(enemy.RoarSeconds, 1e-3f), 0f, 1f));
        }

        if (enemy.Attack is { } attack && clips.TryGetValue(attack.Type.ToString(), out float length))
        {
            return (attack.Type.ToString(), length * AttackTime(enemy, attack));
        }

        if (deltaSeconds > 0f)
        {
            float speed = moved / deltaSeconds;
            state.Walking = speed > (state.Walking ? MinWalkSpeed * 0.5f : MinWalkSpeed);
        }

        if (state.Walking)
        {
            state.Stride = Wrap(state.Stride + moved, clips.GetValueOrDefault(WalkClip));
            return (WalkClip, state.Stride);
        }

        return (IdleClip, state.Idle);
    }

    private static float Wrap(float time, float length) => length > 0f ? ((time % length) + length) % length : 0f;
}
