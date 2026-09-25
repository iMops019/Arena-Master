namespace ArenaMaster.Game.Combat;

/// <summary>The player's level and the experience toward the next one.</summary>
internal sealed class Experience
{
    public int Level { get; private set; } = 1;

    /// <summary>Experience gathered toward the next level.</summary>
    public int Current { get; private set; }

    /// <summary>What it takes to go from <see cref="Level"/> to the next.</summary>
    public int Required => RequiredFor(Level);

    /// <summary>0 to 1 of the way to the next level.</summary>
    public float Progress => (float)Current / Required;

    /// <summary>Experience to go from <paramref name="level"/> to the one after: 5 at first, 5 more each level after.</summary>
    public static int RequiredFor(int level) => 5 * level;

    /// <summary>Adds <paramref name="amount"/>, levelling up as many times as it covers. Returns how many levels were gained.</summary>
    public int Add(int amount)
    {
        Current += amount;
        int gained = 0;
        while (Current >= Required)
        {
            Current -= Required;
            Level++;
            gained++;
        }

        return gained;
    }

    public void Reset()
    {
        Level = 1;
        Current = 0;
    }
}
