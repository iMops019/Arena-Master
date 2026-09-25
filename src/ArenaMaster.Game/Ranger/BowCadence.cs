namespace ArenaMaster.Game.Ranger;

/// <summary>
/// The bow's clocks for the majors that go by time or by count: Sniper's Focus (stand still long enough and the next shot is focused), Endless Quiver (every
/// tenth shot adds a ring) and Rain of Arrows (every few seconds). Pure, so the rhythm can be tested without a window.
/// </summary>
internal sealed class BowCadence
{
    /// <summary>How long the Ranger has stood still, in seconds.</summary>
    public float StillFor { get; private set; }

    /// <summary>Shots fired this run.</summary>
    public int Shots { get; private set; }

    /// <summary>Seconds until the next Rain of Arrows.</summary>
    public float RainIn { get; private set; } = RangerStats.RainInterval;

    public void Update(float deltaSeconds, bool standingStill)
    {
        StillFor = standingStill ? StillFor + deltaSeconds : 0f;
        RainIn -= deltaSeconds;
    }

    /// <summary>Whether the next shot will be focused (Sniper's Focus taken, and the Ranger has stood still long enough).</summary>
    public bool FocusReady(RangerStats stats) => stats.Tree.SnipersFocus && StillFor >= RangerStats.FocusTime;

    /// <summary>A shot goes: whether it is focused (which spends the focus - stand still again for the next), and whether it is the one that adds the ring.</summary>
    public (bool Focused, bool Ring) Shoot(RangerStats stats)
    {
        bool focused = FocusReady(stats);
        if (focused)
        {
            StillFor = 0f;
        }

        Shots++;
        bool ring = stats.Tree.EndlessQuiver && Shots % RangerStats.QuiverEvery == 0;
        return (focused, ring);
    }

    /// <summary>True when a Rain of Arrows is due now (and starts the wait for the next). Without the node, the timer just waits at full.</summary>
    public bool RainDue(RangerStats stats)
    {
        if (!stats.Tree.RainOfArrows)
        {
            RainIn = RangerStats.RainInterval;
            return false;
        }

        if (RainIn > 0f)
        {
            return false;
        }

        RainIn = MathF.Max(RainIn + RangerStats.RainInterval, 0.5f);
        return true;
    }

    public void Reset()
    {
        StillFor = 0f;
        Shots = 0;
        RainIn = RangerStats.RainInterval;
    }
}
