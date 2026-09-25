namespace ArenaMaster.Game.Combat;

/// <summary>What the director wants spawned this frame, besides the fodder it keeps the field topped up with.</summary>
internal readonly record struct DirectorOrders(int Elites, bool Boss, bool FinalBoss);

/// <summary>
/// The 30-minute run's schedule: how many fodder enemies there are and how tough they spawn, minute by minute; when elites come and how many; and when the bosses
/// arrive (10:00, 20:00, and a final one at 27:00). Survive to 30:00 and the run is won. Pure - it only sets the enemy field's numbers and says what to spawn.
/// </summary>
internal sealed class RunDirector
{
    public const float RunLength = 30f * 60f;

    public const float FirstEliteAt = 3f * 60f;
    public const float EliteInterval = 150f;

    public static readonly float[] BossTimes = { 10f * 60f, 20f * 60f, 27f * 60f };

    private float _nextElite = FirstEliteAt;
    private int _nextBoss;

    /// <summary>How many fodder enemies to keep on the field at <paramref name="seconds"/> into the run: 14 at the start, about 70 by the end.</summary>
    public static int FodderCount(float seconds) => (int)MathF.Min(80f, 14f + 1.9f * seconds / 60f);

    /// <summary>How often to top the field up at <paramref name="seconds"/>: every 0.5 s at first, quicker as the field grows.</summary>
    public static float SpawnInterval(float seconds) => MathF.Max(0.08f, 0.5f - 0.014f * seconds / 60f);

    /// <summary>How much tougher than base everything spawns at <paramref name="seconds"/>: health climbs steeply, damage and speed gently.</summary>
    public static EnemyScaling ScalingAt(float seconds)
    {
        float minutes = seconds / 60f;
        return new EnemyScaling(Health: 1f + 0.12f * minutes, Damage: 1f + 0.05f * minutes, Speed: 1f + 0.01f * minutes);
    }

    /// <summary>How many elites come in a wave at <paramref name="seconds"/>: one, then two after 10:00, then three after 20:00.</summary>
    public static int ElitesPerWave(float seconds) => 1 + (int)(seconds / 600f);

    public static bool IsWon(float seconds) => seconds >= RunLength;

    /// <summary>
    /// Sets <paramref name="field"/>'s numbers for <paramref name="seconds"/> into the run and says what else to spawn now. If the clock jumped (a dev skip), each
    /// missed event fires once rather than all piling up.
    /// </summary>
    public DirectorOrders Update(float seconds, EnemyField field)
    {
        field.TargetCount = FodderCount(seconds);
        field.SpawnInterval = SpawnInterval(seconds);
        field.Scaling = ScalingAt(seconds);

        int elites = 0;
        if (seconds >= _nextElite)
        {
            elites = ElitesPerWave(seconds);
            _nextElite = MathF.Max(_nextElite + EliteInterval, seconds + 30f);
        }

        bool boss = false, final = false;
        if (_nextBoss < BossTimes.Length && seconds >= BossTimes[_nextBoss])
        {
            boss = true;
            final = _nextBoss == BossTimes.Length - 1;
            _nextBoss++;
        }

        return new DirectorOrders(elites, boss, final);
    }

    public void Reset()
    {
        _nextElite = FirstEliteAt;
        _nextBoss = 0;
    }
}
