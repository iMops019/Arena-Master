using ArenaMaster.Game.Combat;
using Silk.NET.Maths;

namespace ArenaMaster.Game.Warrior;

/// <summary>
/// One Cleave: a wedge-shaped wave rolling out over the ground from where the Warrior swung, sweeping <see cref="HalfArc"/> either side of <see cref="Yaw"/>. Its
/// front rolls from <see cref="WarriorStats.StartRadius"/> out to <see cref="Reach"/> in <see cref="WarriorStats.TravelTime"/>, whatever the reach, and everything
/// it passes over is hit once. An echo waits out its <see cref="Delay"/> first.
/// </summary>
internal sealed class CleaveWave
{
    /// <summary>How long a wave lingers on screen after it has rolled out, fading.</summary>
    public const float FadeTime = 0.1f;

    public Vector3D<float> Origin { get; init; }

    /// <summary>The way it rolls (0 is +Z, towards +X positive).</summary>
    public float Yaw { get; init; }

    /// <summary>Half its sweep, in radians (pi: all the way round).</summary>
    public float HalfArc { get; init; }

    public float Reach { get; init; }

    public float Damage { get; init; }

    public float Delay { get; set; }

    public float Age { get; set; }

    /// <summary>Whether its front has made its last pass, at full reach: from then on it only fades.</summary>
    public bool RolledOut { get; set; }

    /// <summary>The enemies it has hit already: once each.</summary>
    public HashSet<Enemy> Struck { get; } = new();

    /// <summary>How far out its front has rolled.</summary>
    public float Front => WarriorStats.StartRadius + (Reach - WarriorStats.StartRadius) * Math.Clamp(Age / WarriorStats.TravelTime, 0f, 1f);

    public bool Rolling => Delay <= 0f && Age < WarriorStats.TravelTime;

    public bool Done => Delay <= 0f && Age >= WarriorStats.TravelTime + FadeTime;
}

/// <summary>What dealt a Warrior hit: the Cleave's waves (which build rage and leech life) or a Riposte.</summary>
internal enum CleaveSource
{
    Cleave,
    Riposte,
}

internal readonly record struct CleaveHit(Enemy Enemy, Vector3D<float> Position, float Damage, bool Killed, bool Crit, CleaveSource Source);

/// <summary>
/// The Warrior's two axes: on its own every swing interval, one axe and then the other swings a Cleave - a wave leaning a little to that axe's side - out where the
/// Warrior faces; and the Berserker majors that add waves (Echoing Cleave, Twin Fury, Whirlwind) or change a hit (Execute). Pure simulation - no engine calls - so
/// it can be tested; <see cref="WarriorView"/> draws it.
/// </summary>
internal sealed class CleaveAxes
{
    /// <summary>The first swing of a run comes this soon.</summary>
    public const float FirstSwing = 0.3f;

    private readonly Random _random;
    private readonly List<CleaveWave> _waves = new();

    public CleaveAxes(Random random) => _random = random;

    public IReadOnlyList<CleaveWave> Waves => _waves;

    /// <summary>Seconds until the next swing.</summary>
    public float SwingIn { get; private set; } = FirstSwing;

    /// <summary>Swings this run (echoes and the twin's waves don't count). Odd swings are the right axe's.</summary>
    public int Swings { get; private set; }

    /// <summary>
    /// One frame: a swing when one is due (held while <paramref name="canSwing"/> is false - a stun) towards <paramref name="facingYaw"/> from <paramref name="feet"/>,
    /// and every wave rolling on, hitting what its front passes over. Every hit dealt goes on <paramref name="hits"/>.
    /// </summary>
    public void Update(float deltaSeconds, Vector3D<float> feet, float facingYaw, WarriorStats stats, EnemyField enemies, bool canSwing, List<CleaveHit> hits)
    {
        SwingIn -= deltaSeconds;
        if (!canSwing)
        {
            SwingIn = MathF.Max(SwingIn, 0.15f);
        }
        else if (SwingIn <= 0f)
        {
            SwingIn = MathF.Max(0f, SwingIn + stats.SwingInterval);   // after a pause, no flurry of swings to catch up
            Swing(feet, facingYaw, stats);
        }

        foreach (var wave in _waves)
        {
            if (wave.Delay > 0f)
            {
                wave.Delay -= deltaSeconds;
                if (wave.Delay > 0f)
                {
                    continue;
                }
            }

            wave.Age += deltaSeconds;
            Roll(wave, stats, enemies, hits);
        }

        _waves.RemoveAll(w => w.Done);
    }

    /// <summary>
    /// A swing: the next axe's wave (the right axe on odd swings, leaning its way), all the way round and further on a Whirlwind; the other axe's with Twin Fury; and an
    /// echo lined up with Echoing Cleave.
    /// </summary>
    public void Swing(Vector3D<float> feet, float facingYaw, WarriorStats stats)
    {
        Swings++;
        float lean = (Swings % 2 == 1 ? 1f : -1f) * WarriorStats.AxeLean * MathF.PI / 180f;
        bool whirl = stats.Tree.Whirlwind && Swings % WarriorStats.WhirlwindEvery == 0;
        float halfArc = whirl ? MathF.PI : stats.ArcDegrees * 0.5f * MathF.PI / 180f;
        float reach = stats.Reach * (whirl ? WarriorStats.WhirlwindReach : 1f);
        float damage = stats.CleaveDamage;

        Add(feet, facingYaw + lean, halfArc, reach, damage, 0f);
        if (stats.Tree.TwinFury && !whirl)
        {
            Add(feet, facingYaw - lean, halfArc, reach, damage * WarriorStats.TwinDamage, 0f);
        }

        if (stats.Tree.EchoingCleave)
        {
            Add(feet, facingYaw + lean, halfArc, reach, damage * WarriorStats.EchoDamage, WarriorStats.EchoDelay);
        }
    }

    /// <summary>
    /// Hurts one enemy (harder if it is an elite or a boss; outright, with Execute, if it is a low non-boss) and notes the hit. Nothing happens to one already dead.
    /// </summary>
    public void Hurt(Enemy enemy, float amount, bool crit, CleaveSource source, WarriorStats stats, EnemyField enemies, List<CleaveHit> hits)
    {
        if (!enemy.IsAlive || amount <= 0f)
        {
            return;
        }

        if (enemy.Kind.Tier != EnemyTier.Fodder)
        {
            amount *= stats.EliteMultiplier;
        }

        if (stats.Tree.Execute && enemy.Kind.Tier != EnemyTier.Boss && !enemy.Kind.IsProp && enemy.Health <= enemy.MaxHealth * WarriorStats.ExecuteBelow)
        {
            amount = MathF.Max(amount, enemy.Health);
        }

        bool killed = enemies.Damage(enemy, amount);
        hits.Add(new CleaveHit(enemy, enemy.Position + new Vector3D<float>(0f, enemy.Kind.Height * 0.7f, 0f), amount, killed, crit, source));
    }

    /// <summary>Everything out of the air (a restart), and the clock back to the start.</summary>
    public void Reset()
    {
        _waves.Clear();
        SwingIn = FirstSwing;
        Swings = 0;
    }

    /// <summary>Whether <paramref name="enemy"/>'s body is inside the wedge <paramref name="wave"/> sweeps, out to its front.</summary>
    public static bool Inside(CleaveWave wave, Enemy enemy)
    {
        var to = Geometry.FlatDirection(wave.Origin, enemy.Position, out float distance);
        if (distance > wave.Front + enemy.Kind.Radius)
        {
            return false;
        }

        if (wave.HalfArc >= MathF.PI || distance <= enemy.Kind.Radius)
        {
            return true;
        }

        float off = MathF.Abs(MathF.IEEERemainder(MathF.Atan2(to.X, to.Z) - wave.Yaw, MathF.Tau));
        return off <= wave.HalfArc + MathF.Asin(Math.Min(1f, enemy.Kind.Radius / distance));
    }

    private void Add(Vector3D<float> origin, float yaw, float halfArc, float reach, float damage, float delay) =>
        _waves.Add(new CleaveWave { Origin = origin, Yaw = yaw, HalfArc = halfArc, Reach = reach, Damage = damage, Delay = delay });

    /// <summary>What a wave's front has passed over since last frame: each enemy in the wedge not hit yet, rolled for a crit.</summary>
    private void Roll(CleaveWave wave, WarriorStats stats, EnemyField enemies, List<CleaveHit> hits)
    {
        if (wave.RolledOut)
        {
            return;   // only fading now
        }

        wave.RolledOut = wave.Age >= WarriorStats.TravelTime;

        foreach (var enemy in enemies.Within(wave.Origin, wave.Front))
        {
            if (wave.Struck.Contains(enemy) || !Inside(wave, enemy))
            {
                continue;
            }

            wave.Struck.Add(enemy);
            bool crit = _random.NextDouble() < stats.CritChance;
            Hurt(enemy, wave.Damage * (crit ? stats.CritMultiplier : 1f), crit, CleaveSource.Cleave, stats, enemies, hits);
        }
    }
}

/// <summary>
/// Berserking's rage: gained by hits, up to the most the Warrior can hold; every gain starts its clock again, and when the clock runs out it is all gone - or, with
/// the Horn of Fury, it drains a point every <see cref="WarriorStats.DrainStep"/>. Nothing builds without Berserking. Pure.
/// </summary>
internal sealed class Fury
{
    public int Rage { get; private set; }

    /// <summary>Seconds until the rage runs out, unless a hit comes first.</summary>
    public float Left { get; private set; }

    public void Gain(int amount, WarriorStats stats)
    {
        if (!stats.Tree.Berserking || amount <= 0)
        {
            return;
        }

        Rage = Math.Min(stats.MaxRage, Rage + amount);
        Left = stats.RageDuration;
    }

    public void Update(float deltaSeconds, bool drains = false)
    {
        if (Rage == 0)
        {
            return;
        }

        Left -= deltaSeconds;
        while (Left <= 0f && Rage > 0)
        {
            if (!drains)
            {
                Rage = 0;
                break;
            }

            Rage--;
            Left += WarriorStats.DrainStep;
        }

        if (Rage == 0)
        {
            Left = 0f;
        }
    }

    public void Reset()
    {
        Rage = 0;
        Left = 0f;
    }
}
