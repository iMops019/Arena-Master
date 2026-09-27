namespace ArenaMaster.Game.Combat;

/// <summary>
/// A random event: the Monster Rush. Every so often (the first 3:00 to 5:00 into a run, then 2:30 to 4:30 after the last one ended), for
/// <see cref="Duration"/> seconds, monsters pour in: the field's fodder target climbs over <see cref="RampSeconds"/> to <see cref="CrowdMultiplier"/> times
/// the director's plus <see cref="CrowdExtra"/> (at most <see cref="MaxCrowd"/>), and it tops up almost at once, so every kill is replaced straight away.
/// Once it ends the director's numbers come back; the extra monsters stay until they are killed. A rush waits while it isn't allowed (a boss on the field, or
/// the boss hunt's arena) and starts as soon as it is. Pure: it only sets the enemy field's numbers.
/// </summary>
internal sealed class MonsterRush
{
    public const float Duration = 30f;

    /// <summary>When the first rush comes (between the two), and how long after one ends the next comes.</summary>
    public const float FirstMin = 180f;
    public const float FirstMax = 300f;
    public const float GapMin = 150f;
    public const float GapMax = 270f;

    /// <summary>How big the crowd gets: the director's fodder target times this, plus this, at most this.</summary>
    public const float CrowdMultiplier = 1.5f;
    public const int CrowdExtra = 20;
    public const int MaxCrowd = 350;

    /// <summary>The crowd builds to its full size over this long at the start of a rush.</summary>
    public const float RampSeconds = 8f;

    /// <summary>Seconds between spawns during a rush (the field spawns at most a few a frame whatever this is).</summary>
    public const float SpawnInterval = 0.02f;

    private readonly Random _random;
    private float _next;

    public MonsterRush(Random random)
    {
        _random = random;
        Reset();
    }

    /// <summary>Whether a rush is on now.</summary>
    public bool Active => Left > 0f;

    /// <summary>Seconds of the rush still to go (0 when none is on).</summary>
    public float Left { get; private set; }

    /// <summary>Seconds into the rush under way.</summary>
    public float Into => Active ? Duration - Left : 0f;

    /// <summary>Seconds until the next rush is due (it may still wait, if not allowed then).</summary>
    public float NextIn => _next;

    /// <summary>Moves the clocks on by <paramref name="deltaSeconds"/>. True if a rush began this frame. One due while not <paramref name="allowed"/> waits.</summary>
    public bool Update(float deltaSeconds, bool allowed)
    {
        if (Active)
        {
            Left = MathF.Max(0f, Left - deltaSeconds);
            if (!Active)
            {
                _next = Between(GapMin, GapMax);
            }

            return false;
        }

        _next = MathF.Max(0f, _next - deltaSeconds);
        if (_next > 0f || !allowed)
        {
            return false;
        }

        Start();
        return true;
    }

    /// <summary>Starts a rush now (a dev key, or when one is due).</summary>
    public void Start() => Left = Duration;

    /// <summary>
    /// While a rush is on, sets <paramref name="field"/>'s fodder target to the rush's crowd (from what the director set it to this frame) and its spawns to
    /// almost at once. Call it after the director has set its numbers.
    /// </summary>
    public void Apply(EnemyField field)
    {
        if (!Active)
        {
            return;
        }

        field.TargetCount = Crowd(field.TargetCount, Into);
        field.SpawnInterval = MathF.Min(field.SpawnInterval, SpawnInterval);
    }

    /// <summary>The fodder target <paramref name="into"/> seconds into a rush, for a director's <paramref name="normal"/> target.</summary>
    public static int Crowd(int normal, float into)
    {
        int full = Math.Min(MaxCrowd, (int)MathF.Round(normal * CrowdMultiplier) + CrowdExtra);
        float ramp = Math.Clamp(into / RampSeconds, 0f, 1f);
        return Math.Max(normal, (int)MathF.Round(normal + (full - normal) * ramp));
    }

    /// <summary>A fresh run: no rush on, the first one due 3:00 to 5:00 in.</summary>
    public void Reset()
    {
        Left = 0f;
        _next = Between(FirstMin, FirstMax);
    }

    private float Between(float low, float high) => low + (high - low) * (float)_random.NextDouble();
}
