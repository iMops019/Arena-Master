using ArenaMaster.Game.Combat;

namespace ArenaMaster.Game.Delve;

/// <summary>
/// A Delve node's run, about 10 minutes: the swarm ramps up faster than a classic run's (the classic's own curves, played quicker, and how far along them a floor
/// gets depends on its depth); one Ghoul Brute at 5:00; the Hollow King with two Brutes at 10:00; and three more Brutes when the King is down to half his health.
/// The swarm holds at its 10:00 strength until the King falls, and the run is over when his cache is opened. Everything is tougher by the floor's depth. Pure.
/// </summary>
internal sealed class DelveDirector
{
    public const float FirstEliteAt = 5f * 60f;
    public const float BossAt = 10f * 60f;
    public const int ElitesWithBoss = 2;
    public const int ElitesAtHalf = 3;

    private bool _firstElite;
    private bool _boss;
    private bool _half;

    public DelveDirector(int depth) => Depth = Math.Max(1, depth);

    public int Depth { get; }

    /// <summary>How far along the classic run's curves (in its minutes) this floor's swarm is at the boss: 10:00 on the first floor, 1.5 minutes more each floor, 28:00 at most.</summary>
    public static float PeakMinutes(int depth) => MathF.Min(28f, 10f + 1.5f * (Math.Max(1, depth) - 1));

    /// <summary>The classic run's clock that <paramref name="seconds"/> into this run matches: running quicker, and holding once the boss is due.</summary>
    public float ClassicSeconds(float seconds) => MathF.Min(seconds, BossAt) / BossAt * PeakMinutes(Depth) * 60f;

    /// <summary>
    /// Sets <paramref name="field"/>'s numbers for <paramref name="seconds"/> into the run and says what else to spawn now. <paramref name="boss"/> is the King if he
    /// is on the field, for the wave at half his health.
    /// </summary>
    public DirectorOrders Update(float seconds, EnemyField field, Enemy? boss)
    {
        float classic = ClassicSeconds(seconds);
        field.TargetCount = RunDirector.FodderCount(classic);
        field.SpawnInterval = RunDirector.SpawnInterval(classic);
        var scaling = RunDirector.ScalingAt(classic);
        field.Scaling = scaling with { Health = scaling.Health * DelveRules.HealthMultiplier(Depth), Damage = scaling.Damage * DelveRules.DamageMultiplier(Depth) };
        field.Mix = new[] { (EnemyKind.CrossbowGhoul, RunDirector.CrossbowShareAt(classic)), (EnemyKind.GhoulMage, RunDirector.MageShareAt(classic)) };

        int elites = 0;
        bool spawnBoss = false;
        if (!_firstElite && seconds >= FirstEliteAt)
        {
            _firstElite = true;
            elites += 1;
        }

        if (!_boss && seconds >= BossAt)
        {
            _boss = true;
            spawnBoss = true;
            elites += ElitesWithBoss;
        }

        if (_boss && !_half && boss is { IsAlive: true } && boss.Health <= boss.MaxHealth * 0.5f)
        {
            _half = true;
            elites += ElitesAtHalf;
        }

        return new DirectorOrders(elites, spawnBoss, FinalBoss: spawnBoss);
    }

    /// <summary>Whether the King has been called yet.</summary>
    public bool BossCalled => _boss;
}
