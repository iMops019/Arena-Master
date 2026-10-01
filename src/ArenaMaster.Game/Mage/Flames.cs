using ArenaMaster.Game.Combat;
using Silk.NET.Maths;

namespace ArenaMaster.Game.Mage;

/// <summary>
/// A burn on one enemy: it bites every <see cref="MageStats.BurnTick"/> seconds at its rate for as many ticks as it has left. A new burn on a burning enemy
/// replaces a weaker one and starts its time again (with Living Flame the enemy keeps burning as long as it has, the stronger rate winning).
/// </summary>
internal sealed class Burn
{
    /// <summary>Its damage each second (before Living Flame's growth).</summary>
    public float PerSecond { get; set; }

    /// <summary>Its bites still to come.</summary>
    public int TicksLeft { get; set; }

    /// <summary>How long a burn lit fresh from this one lasts (Wildfire spreads a fresh copy).</summary>
    public float Duration { get; set; }

    /// <summary>Seconds it has been burning (Living Flame).</summary>
    public float Burned { get; set; }

    public float TickIn { get; set; } = MageStats.BurnTick;

    /// <summary>Seconds until it next spreads (Wildfire).</summary>
    public float SpreadIn { get; set; } = MageStats.WildfireEvery;

    /// <summary>Seconds of burning left, for the view.</summary>
    public float Left => MathF.Max(0f, (TicksLeft - 1) * MageStats.BurnTick + TickIn);
}

/// <summary>
/// Fire on the ground: a strip from <see cref="From"/> to <see cref="To"/>, reaching <see cref="Width"/> either side (a circle of that radius when the two ends are the
/// same - a meteor's crater). Everything standing in it takes its damage every <see cref="MageStats.GroundTick"/> seconds, and is set burning.
/// </summary>
internal sealed class GroundFire
{
    public Vector3D<float> From { get; set; }

    public Vector3D<float> To { get; set; }

    public float Width { get; init; }

    /// <summary>Its damage each second.</summary>
    public float PerSecond { get; init; }

    public float Seconds { get; init; }

    /// <summary>Seconds of burning left, for the view.</summary>
    public float Left { get; set; }

    /// <summary>Its bites still to come: one every <see cref="MageStats.GroundTick"/> seconds of its time.</summary>
    public int TicksLeft { get; set; }

    public float TickIn { get; set; } = MageStats.GroundTick;

    /// <summary>Whether it is a line of fire (Fire Walk) rather than a crater.</summary>
    public bool IsLine { get; init; }
}

/// <summary>A meteor on its way down (Meteor): where it will land, how long until it does, and what it does there.</summary>
internal sealed class FallingMeteor
{
    public Vector3D<float> Target { get; init; }

    public float FallLeft { get; set; }

    public float Damage { get; init; }

    public float Radius { get; init; }
}

/// <summary>A burst of fire spreading over the ground, for the view: Combustion, a Fireball, a meteor landing. Its damage is dealt when it appears.</summary>
internal sealed class FireBlast
{
    public Vector3D<float> Centre { get; init; }

    public float Radius { get; init; }

    public float Age { get; set; }
}

/// <summary>
/// The Pyromancy tree's fire, beside the barrage (<see cref="FrostBarrage"/> runs it while the Fire Barrage is the Mage's): the burns every hit leaves, Wildfire
/// spreading them, Combustion's bursts when a burning enemy dies, the Fireball's, the heat gauge with its overheat and the Inferno, Fire Walk's line of fire,
/// the meteors and their burning craters. Pure simulation - no engine calls - so it can be tested; <see cref="FireView"/> draws it.
/// </summary>
internal sealed class Flames
{
    /// <summary>How long a burst of fire takes to spread out.</summary>
    public const float BlastDuration = 0.35f;

    /// <summary>How many times the Inferno bites in one overheat: twice a second through its 1.5 s.</summary>
    public const int InfernoPulses = 3;

    private readonly Dictionary<Enemy, Burn> _burns = new();
    private readonly List<GroundFire> _ground = new();
    private readonly List<FallingMeteor> _meteors = new();
    private readonly List<FireBlast> _blasts = new();
    private GroundFire? _openLine;
    private float _infernoIn;
    private int _infernoPulsesLeft;

    public IReadOnlyDictionary<Enemy, Burn> Burns => _burns;

    public IReadOnlyList<GroundFire> Ground => _ground;

    public IReadOnlyList<FallingMeteor> Meteors => _meteors;

    public IReadOnlyList<FireBlast> Blasts => _blasts;

    /// <summary>The heat (0 to <see cref="MageStats.MaxHeat"/>): builds with every cast, with the Heat major.</summary>
    public float Heat { get; private set; }

    /// <summary>Seconds of the overheat left (0 when not overheated): no casting through it, unless it is the Inferno's firestorm.</summary>
    public float OverheatLeft { get; private set; }

    /// <summary>Whether the overheat under way is the Inferno's firestorm (the casting goes on).</summary>
    public bool InInferno { get; private set; }

    /// <summary>Whether the barrage may cast: not while overheated, unless the overheat is the Inferno.</summary>
    public bool CanCast => OverheatLeft <= 0f || InInferno;

    /// <summary>Burning enemies that have died this run (for Combustion, and the tests).</summary>
    public int BurningDeaths { get; private set; }

    public bool IsBurning(Enemy enemy) => _burns.ContainsKey(enemy);

    /// <summary>
    /// A barrage has just been cast (the <paramref name="barrages"/>-th of the run): the heat builds, overheating at the top, and every 4th (with Meteor) calls a
    /// meteor down on the thickest crowd within the barrage's reach of <paramref name="feet"/>.
    /// </summary>
    public void Cast(Vector3D<float> feet, int barrages, MageStats stats, EnemyField enemies)
    {
        if (stats.Pyro.Meteor && barrages % MageStats.MeteorEvery == 0)
        {
            CallMeteor(feet, stats, enemies);
        }

        if (stats.Pyro.Heat && OverheatLeft <= 0f)
        {
            Heat = MathF.Min(MageStats.MaxHeat, Heat + MageStats.HeatPerCast);
            if (Heat >= MageStats.MaxHeat)
            {
                OverheatLeft = MageStats.OverheatSeconds;
                InInferno = stats.Pyro.Inferno;
                _infernoPulsesLeft = InInferno ? InfernoPulses : 0;
                _infernoIn = 0f;   // the firestorm bites at once
            }
        }

        stats.Heat = Heat;
    }

    /// <summary>
    /// A blink vents the heat: all of it gone at once, an overheat ended with it (but the Inferno's firestorm runs its course). Returns the heat let out (for
    /// Cauterise).
    /// </summary>
    public float Vent(MageStats stats)
    {
        if (!stats.Pyro.Heat || InInferno)
        {
            return 0f;
        }

        float vented = Heat;
        Heat = 0f;
        OverheatLeft = 0f;
        stats.Heat = 0f;
        return vented;
    }

    /// <summary>Fire Walk: a line of fire starts where a blink began. <see cref="ExtendLine"/> draws it on to where the Mage has got to.</summary>
    public void StartLine(Vector3D<float> from, MageStats stats)
    {
        _openLine = new GroundFire
        {
            From = from,
            To = from,
            Width = MageStats.FireWalkWidth * stats.AreaScale,
            PerSecond = stats.GroundFireDamage,
            Seconds = MageStats.FireWalkSeconds,
            Left = MageStats.FireWalkSeconds,
            TicksLeft = GroundTicks(MageStats.FireWalkSeconds),
            IsLine = true,
        };
        _ground.Add(_openLine);
    }

    /// <summary>The line of fire the blink under way is laying reaches on to <paramref name="to"/>.</summary>
    public void ExtendLine(Vector3D<float> to)
    {
        if (_openLine is not null)
        {
            _openLine.To = to;
        }
    }

    /// <summary>The blink is over: its line of fire is laid.</summary>
    public void CloseLine() => _openLine = null;

    /// <summary>
    /// Sets <paramref name="enemy"/> burning from a hit of <paramref name="hitDamage"/>: 30% of it (more with burn damage and damage over time) over
    /// <see cref="MageStats.BaseBurnDuration"/>, at that rate for as long as a burn lasts. The hit is counted before a crit and an elite's share (a burn never crits,
    /// and an elite's share comes again on every tick). Nothing happens to one dead, or to a crate.
    /// </summary>
    public void Ignite(Enemy enemy, float hitDamage, MageStats stats)
    {
        if (hitDamage <= 0f)
        {
            return;
        }

        Light(enemy, hitDamage * stats.BurnShare * stats.Items.OverTime / MageStats.BaseBurnDuration, stats.BurnDuration, stats);
    }

    /// <summary>
    /// A burst of fire at <paramref name="centre"/>: <paramref name="damage"/> to every enemy within <paramref name="radius"/> but <paramref name="spare"/>, setting
    /// each one it doesn't kill burning. Combustion, the Fireball and a meteor all go through here.
    /// </summary>
    public void Blast(Vector3D<float> centre, float radius, float damage, FrostSource source, MageStats stats, EnemyField enemies, List<FrostHit> hits,
        Enemy? spare = null)
    {
        _blasts.Add(new FireBlast { Centre = centre, Radius = radius });
        foreach (var enemy in enemies.Within(centre, radius))
        {
            if (enemy != spare && !Hurt(enemy, damage, source, stats, enemies, hits, out _))
            {
                Ignite(enemy, damage, stats);
            }
        }
    }

    /// <summary>
    /// One frame of the fire: the heat cooling (or the overheat running out, and the Inferno biting), the burns (Combustion when a burning enemy dies, Wildfire
    /// spreading them), the fire on the ground, the meteors falling, the bursts spreading. <paramref name="feet"/> is where the Mage stands.
    /// </summary>
    public void Update(float deltaSeconds, Vector3D<float> feet, MageStats stats, EnemyField enemies, List<FrostHit> hits)
    {
        UpdateHeat(deltaSeconds, feet, stats, enemies, hits);
        UpdateBurns(deltaSeconds, stats, enemies, hits);
        UpdateGround(deltaSeconds, stats, enemies, hits);
        UpdateMeteors(deltaSeconds, stats, enemies, hits);

        foreach (var blast in _blasts)
        {
            blast.Age += deltaSeconds;
        }

        _blasts.RemoveAll(b => b.Age >= BlastDuration);
    }

    /// <summary>Everything put out (a restart), and the heat back to nothing.</summary>
    public void Reset()
    {
        _burns.Clear();
        _ground.Clear();
        _meteors.Clear();
        _blasts.Clear();
        _openLine = null;
        Heat = 0f;
        OverheatLeft = 0f;
        InInferno = false;
        _infernoPulsesLeft = 0;
        BurningDeaths = 0;
    }

    /// <summary>A burn at <paramref name="perSecond"/> for <paramref name="duration"/> on <paramref name="enemy"/>, or a stronger one (or, with Living Flame, a longer one) where it burns already.</summary>
    private void Light(Enemy enemy, float perSecond, float duration, MageStats stats)
    {
        if (!enemy.IsAlive || enemy.Kind.IsProp || perSecond <= 0f)
        {
            return;
        }

        int ticks = Math.Max(1, (int)MathF.Round(duration / MageStats.BurnTick));
        if (_burns.TryGetValue(enemy, out var burn))
        {
            if (stats.Pyro.LivingFlame)
            {
                // Living Flame: the fire takes the new fuel, the stronger rate and the longer time, but keeps burning as long as it has.
                burn.PerSecond = MathF.Max(burn.PerSecond, perSecond);
                burn.TicksLeft = Math.Max(burn.TicksLeft, ticks);
                burn.Duration = duration;
            }
            else if (perSecond >= burn.PerSecond)
            {
                burn.PerSecond = perSecond;
                burn.TicksLeft = ticks;
                burn.Duration = duration;
                burn.Burned = 0f;
            }

            return;
        }

        _burns[enemy] = new Burn { PerSecond = perSecond, TicksLeft = ticks, Duration = duration };
    }

    private void UpdateHeat(float deltaSeconds, Vector3D<float> feet, MageStats stats, EnemyField enemies, List<FrostHit> hits)
    {
        if (!stats.Pyro.Heat)
        {
            Heat = 0f;
            OverheatLeft = 0f;
            InInferno = false;
            stats.Heat = 0f;
            return;
        }

        if (OverheatLeft > 0f)
        {
            if (InInferno)
            {
                // The Inferno: a firestorm round the Mage, biting twice a second.
                _infernoIn -= deltaSeconds;
                while (_infernoIn <= 0f && _infernoPulsesLeft > 0)
                {
                    _infernoIn += MageStats.InfernoTick;
                    _infernoPulsesLeft--;
                    float damage = stats.BoltDamage * stats.Items.OverTime;
                    foreach (var enemy in enemies.Within(feet, MageStats.InfernoRadius * stats.AreaScale))
                    {
                        if (!Hurt(enemy, damage, FrostSource.Inferno, stats, enemies, hits, out _))
                        {
                            Ignite(enemy, damage, stats);
                        }
                    }
                }
            }

            OverheatLeft -= deltaSeconds;
            if (OverheatLeft <= 0f)
            {
                OverheatLeft = 0f;
                InInferno = false;
                Heat = 0f;   // the overheat has burned it all off
            }
        }
        else
        {
            Heat = MathF.Max(0f, Heat - stats.HeatCooling * deltaSeconds);
        }

        stats.Heat = Heat;
    }

    /// <summary>
    /// Every burn biting at its rate (growing with Living Flame; damage over time never crits), spreading once a second with Wildfire, and running out. A burning
    /// enemy that died - of its burn or of anything else - bursts with Combustion.
    /// </summary>
    private void UpdateBurns(float deltaSeconds, MageStats stats, EnemyField enemies, List<FrostHit> hits)
    {
        if (_burns.Count == 0)
        {
            return;
        }

        foreach (var (enemy, burn) in _burns.ToList())
        {
            if (!enemy.IsAlive)
            {
                Died(enemy, stats, enemies, hits);
                continue;
            }

            burn.Burned += deltaSeconds;
            burn.TickIn -= deltaSeconds;
            while (burn.TickIn <= 0f && burn.TicksLeft > 0 && enemy.IsAlive)
            {
                burn.TickIn += MageStats.BurnTick;
                burn.TicksLeft--;
                float growth = stats.Pyro.LivingFlame ? 1f + MageStats.LivingFlameGrowth * MathF.Min(burn.Burned, MageStats.LivingFlameMaxSeconds) : 1f;
                Hurt(enemy, burn.PerSecond * MageStats.BurnTick * growth, FrostSource.Burn, stats, enemies, hits, out _);
            }

            if (!enemy.IsAlive)
            {
                Died(enemy, stats, enemies, hits);
                continue;
            }

            if (stats.Pyro.Wildfire)
            {
                burn.SpreadIn -= deltaSeconds;
                if (burn.SpreadIn <= 0f)
                {
                    burn.SpreadIn += MageStats.WildfireEvery;
                    Spread(enemy, burn, stats, enemies);
                }
            }

            if (burn.TicksLeft <= 0)
            {
                _burns.Remove(enemy);
            }
        }
    }

    /// <summary>A burning enemy has died: its burn is out, and with Combustion it bursts.</summary>
    private void Died(Enemy enemy, MageStats stats, EnemyField enemies, List<FrostHit> hits)
    {
        _burns.Remove(enemy);
        BurningDeaths++;
        if (stats.Pyro.Combustion)
        {
            Blast(enemy.Position, stats.CombustionRadius, stats.CombustionDamage, FrostSource.Combustion, stats, enemies, hits, spare: enemy);
        }
    }

    /// <summary>Wildfire: a fresh copy of <paramref name="burn"/> catches on the nearest enemy near <paramref name="from"/> that isn't burning.</summary>
    private void Spread(Enemy from, Burn burn, MageStats stats, EnemyField enemies)
    {
        Enemy? best = null;
        float bestDistance = float.MaxValue;
        foreach (var enemy in enemies.Within(from.Position, MageStats.WildfireRange))
        {
            if (enemy == from || enemy.Kind.IsProp || _burns.ContainsKey(enemy))
            {
                continue;
            }

            Geometry.FlatDirection(from.Position, enemy.Position, out float distance);
            if (distance < bestDistance)
            {
                best = enemy;
                bestDistance = distance;
            }
        }

        if (best is not null)
        {
            Light(best, burn.PerSecond, burn.Duration, stats);
        }
    }

    /// <summary>The fire on the ground: running down, and every tick hurting and lighting everything standing in it.</summary>
    private void UpdateGround(float deltaSeconds, MageStats stats, EnemyField enemies, List<FrostHit> hits)
    {
        for (int i = _ground.Count - 1; i >= 0; i--)
        {
            var fire = _ground[i];
            fire.Left -= deltaSeconds;
            fire.TickIn -= deltaSeconds;
            while (fire.TickIn <= 0f && fire.TicksLeft > 0)
            {
                fire.TickIn += MageStats.GroundTick;
                fire.TicksLeft--;
                float damage = fire.PerSecond * MageStats.GroundTick;
                foreach (var enemy in InFire(fire, enemies))
                {
                    if (!Hurt(enemy, damage, FrostSource.Ground, stats, enemies, hits, out _))
                    {
                        Ignite(enemy, damage, stats);
                    }
                }
            }

            if (fire.TicksLeft <= 0)
            {
                if (fire == _openLine)
                {
                    _openLine = null;
                }

                _ground.RemoveAt(i);
            }
        }
    }

    /// <summary>How many times fire on the ground bites in <paramref name="seconds"/>.</summary>
    private static int GroundTicks(float seconds) => Math.Max(1, (int)MathF.Round(seconds / MageStats.GroundTick));

    /// <summary>The live enemies standing in <paramref name="fire"/>: within its width of the strip from one end to the other.</summary>
    private static IEnumerable<Enemy> InFire(GroundFire fire, EnemyField enemies)
    {
        var middle = (fire.From + fire.To) * 0.5f;
        Geometry.FlatDirection(fire.From, fire.To, out float length);
        foreach (var enemy in enemies.Within(middle, length * 0.5f + fire.Width))
        {
            if (FlatDistanceToStrip(enemy.Position, fire.From, fire.To) <= fire.Width + enemy.Kind.Radius)
            {
                yield return enemy;
            }
        }
    }

    /// <summary>How far <paramref name="point"/> is from the flat line from <paramref name="a"/> to <paramref name="b"/> (a point, if they are the same).</summary>
    internal static float FlatDistanceToStrip(Vector3D<float> point, Vector3D<float> a, Vector3D<float> b)
    {
        float abX = b.X - a.X, abZ = b.Z - a.Z;
        float lengthSquared = abX * abX + abZ * abZ;
        float t = lengthSquared > 1e-6f ? Math.Clamp(((point.X - a.X) * abX + (point.Z - a.Z) * abZ) / lengthSquared, 0f, 1f) : 0f;
        float dx = point.X - (a.X + abX * t), dz = point.Z - (a.Z + abZ * t);
        return MathF.Sqrt(dx * dx + dz * dz);
    }

    /// <summary>Meteor: one on its way down to the thickest crowd within the barrage's reach - the enemy with the most others round it - if there is anything there.</summary>
    private void CallMeteor(Vector3D<float> feet, MageStats stats, EnemyField enemies)
    {
        float radius = MageStats.MeteorRadius * stats.AreaScale;
        var inRange = enemies.Within(feet, stats.TargetRange).Where(e => !e.Kind.IsProp).ToList();
        if (inRange.Count == 0)
        {
            return;
        }

        var target = inRange.OrderByDescending(e => enemies.Within(e.Position, radius).Count).First();
        _meteors.Add(new FallingMeteor
        {
            Target = target.Position,
            FallLeft = MageStats.MeteorFall,
            Damage = stats.BoltDamage * MageStats.MeteorDamage,
            Radius = radius,
        });
    }

    /// <summary>The meteors falling: one that lands bursts, and leaves the ground burning.</summary>
    private void UpdateMeteors(float deltaSeconds, MageStats stats, EnemyField enemies, List<FrostHit> hits)
    {
        for (int i = _meteors.Count - 1; i >= 0; i--)
        {
            var meteor = _meteors[i];
            meteor.FallLeft -= deltaSeconds;
            if (meteor.FallLeft > 0f)
            {
                continue;
            }

            _meteors.RemoveAt(i);
            Blast(meteor.Target, meteor.Radius, meteor.Damage, FrostSource.Meteor, stats, enemies, hits);
            _ground.Add(new GroundFire
            {
                From = meteor.Target,
                To = meteor.Target,
                Width = meteor.Radius,
                PerSecond = stats.GroundFireDamage,
                Seconds = MageStats.MeteorGroundSeconds,
                Left = MageStats.MeteorGroundSeconds,
                TicksLeft = GroundTicks(MageStats.MeteorGroundSeconds),
            });
        }
    }

    /// <summary>
    /// Fire damage from the Mage to one enemy (harder on an elite or a boss). True if it killed; <paramref name="dealt"/> is the damage done. Nothing happens to one
    /// already dead. Fire never chills: only an item's chill does (the enemy field's hit effects).
    /// </summary>
    private static bool Hurt(Enemy enemy, float amount, FrostSource source, MageStats stats, EnemyField enemies, List<FrostHit> hits, out float dealt)
    {
        dealt = 0f;
        if (!enemy.IsAlive || amount <= 0f)
        {
            return false;
        }

        if (enemy.Kind.Tier != EnemyTier.Fodder)
        {
            amount *= stats.EliteMultiplier;
        }

        dealt = amount;
        bool killed = enemies.Damage(enemy, amount);
        hits.Add(new FrostHit(enemy, enemy.Position + new Vector3D<float>(0f, enemy.Kind.Height * 0.7f, 0f), amount, killed, false, source));
        return killed;
    }
}
