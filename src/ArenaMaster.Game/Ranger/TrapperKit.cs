using ArenaMaster.Game.Combat;
using Silk.NET.Maths;

namespace ArenaMaster.Game.Ranger;

/// <summary>
/// A snare lying on the ground (Snare Line): armed until an enemy steps on it, then shut on that enemy, holding it, until it bursts. With Killing Field a burst that
/// kills sets it once more.
/// </summary>
internal sealed class Snare
{
    public Vector3D<float> Position { get; init; }

    /// <summary>A turn of its own, so a row of them doesn't lie in step (for the view).</summary>
    public float Yaw { get; init; }

    /// <summary>Seconds it has left to lie armed before it rusts away.</summary>
    public float Left { get; set; }

    /// <summary>Whether it has shut on something and is counting down to its burst.</summary>
    public bool Sprung { get; set; }

    /// <summary>What it shut on (held unless a boss), or null while armed.</summary>
    public Enemy? Holding { get; set; }

    /// <summary>Seconds until it bursts, once sprung.</summary>
    public float BurstIn { get; set; }

    /// <summary>Whether Killing Field has already set it again (it does once).</summary>
    public bool Reset { get; set; }
}

/// <summary>Caltrops a burst left (Caltrops): everything in them is slowed and bitten every <see cref="TrapperKit.GroundTick"/> seconds.</summary>
internal sealed class CaltropPatch
{
    public Vector3D<float> Centre { get; init; }

    public float Radius { get; init; }

    public float Lifetime { get; init; }

    public float Left { get; set; }

    public float DamagePerSecond { get; init; }

    public float TickIn { get; set; }
}

/// <summary>A cloud a poisoned enemy left as it died (Toxic Cloud): it poisons whatever is in it once every <see cref="TrapperKit.CloudTick"/> seconds.</summary>
internal sealed class ToxicCloud
{
    public Vector3D<float> Centre { get; init; }

    public float Radius { get; init; }

    public float Lifetime { get; init; }

    public float Left { get; set; }

    public float TickIn { get; set; }
}

/// <summary>A snare's burst, for the view: where, how far it reaches, and how long ago.</summary>
internal sealed class SnareBurst
{
    public Vector3D<float> Centre { get; init; }

    public float Radius { get; init; }

    public float Age { get; set; }
}

/// <summary>The poison on one enemy: each dose its own (seconds left, and its bite a second), up to the most it can carry, and when it next bites.</summary>
internal sealed class Poisoning
{
    public List<(float Left, float PerSecond)> Doses { get; } = new();

    public float TickIn { get; set; }
}

internal enum HawkFlight
{
    Circling,
    Diving,
    Returning,
}

/// <summary>The hawk (Hawk Companion): where it is, which way it flies, and what it's doing - circling, diving at its prey, or climbing back.</summary>
internal sealed class Hawk
{
    public Vector3D<float> Position { get; set; }

    /// <summary>Which way it flies (unit length; not flat while diving or climbing).</summary>
    public Vector3D<float> Heading { get; set; } = Vector3D<float>.UnitZ;

    public HawkFlight Flight { get; set; }

    /// <summary>What it is diving at, or null.</summary>
    public Enemy? Prey { get; set; }

    /// <summary>How far through the dive or the climb (0 to 1).</summary>
    public float Progress { get; set; }

    /// <summary>Where the dive or the climb began.</summary>
    public Vector3D<float> From { get; set; }

    /// <summary>Where on its circle round the Ranger it is (radians).</summary>
    public float Angle { get; set; }
}

internal enum TrapperSource
{
    Snare,
    Caltrops,
    Poison,
    Hawk,
}

internal readonly record struct TrapperHit(Enemy Enemy, Vector3D<float> Position, float Damage, bool Killed, bool Crit, TrapperSource Source);

/// <summary>
/// The Trapper tree's side of the fight, beside the bow: snares dropped where each dash began (Snare Line), holding and bursting, with Caltrops and Killing Field;
/// poison from the arrows (Venom Tips) biting over time, with Toxic Cloud and Crippling Venom; and the hawk (Hawk Companion) diving at the toughest enemy near,
/// with Hawk's Mark, Keen Talons and Apex Predator. It also says how much more an enemy takes from the Ranger (Hawk's Mark, Ambush, Festering Wounds) and whether
/// an arrow goes through it for nothing (Blight Arrows): <see cref="RangerArrows"/> asks. Everything it does reads the Trapper bonuses on <see cref="RangerStats"/>,
/// so with the Sharpshooter active it does nothing. Pure - no engine calls; <see cref="TrapperView"/> draws it.
/// </summary>
internal sealed class TrapperKit
{
    /// <summary>How close (beyond its body) an enemy has to come to a snare to step on it.</summary>
    public const float TriggerRadius = 0.7f;

    /// <summary>Poison bites this often; caltrops, this often; a cloud poisons this often.</summary>
    public const float PoisonTick = 0.5f;
    public const float GroundTick = 0.5f;
    public const float CloudTick = 1f;

    /// <summary>At most this many clouds at once (a big poisoned crowd dying at once would otherwise lay hundreds).</summary>
    public const int MaxClouds = 40;

    /// <summary>How long a burst shows.</summary>
    public const float BurstShow = 0.4f;

    /// <summary>The hawk circles this far out from the Ranger and this high above, taking this long a round; its dive and its climb back take this long.</summary>
    public const float CircleRadius = 3f;
    public const float CircleHeight = 5f;
    public const float CircleTime = 3.5f;
    public const float DiveTime = 0.4f;
    public const float ClimbTime = 0.6f;

    /// <summary>Where on its prey the hawk strikes: this share of its height.</summary>
    private const float StrikeHeight = 0.7f;

    private readonly Random _random;
    private readonly List<Snare> _snares = new();
    private readonly List<CaltropPatch> _caltrops = new();
    private readonly List<ToxicCloud> _clouds = new();
    private readonly List<SnareBurst> _bursts = new();
    private readonly Dictionary<Enemy, Poisoning> _poisons = new();
    private readonly Dictionary<Enemy, float> _marks = new();

    public TrapperKit(Random random, RangerStats stats)
    {
        _random = random;
        Stats = stats;
    }

    public RangerStats Stats { get; }

    public IReadOnlyList<Snare> Snares => _snares;

    public IReadOnlyList<CaltropPatch> Caltrops => _caltrops;

    public IReadOnlyList<ToxicCloud> Clouds => _clouds;

    public IReadOnlyList<SnareBurst> Bursts => _bursts;

    public IReadOnlyDictionary<Enemy, Poisoning> Poisons => _poisons;

    /// <summary>The enemies Hawk's Mark is on, and for how much longer.</summary>
    public IReadOnlyDictionary<Enemy, float> Marks => _marks;

    /// <summary>The hawk, or null without Hawk Companion (or before the first frame of a run).</summary>
    public Hawk? Hawk { get; private set; }

    /// <summary>Seconds until the hawk may dive again (it waits, at 0, for prey in range).</summary>
    public float DiveIn { get; private set; } = RangerStats.HawkInterval;

    /// <summary>How many doses of poison <paramref name="enemy"/> carries now.</summary>
    public int DosesOn(Enemy enemy) => _poisons.TryGetValue(enemy, out var poisoning) ? poisoning.Doses.Count : 0;

    public bool IsPoisoned(Enemy enemy) => _poisons.ContainsKey(enemy);

    /// <summary>Whether a snare is holding <paramref name="enemy"/> right now.</summary>
    public bool IsHeld(Enemy enemy)
    {
        foreach (var snare in _snares)
        {
            if (snare.Sprung && snare.Holding == enemy && enemy.IsFrozen)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>What everything the Ranger does to <paramref name="enemy"/> is multiplied by: Hawk's Mark, Ambush (held by a snare), Festering Wounds (poisoned).</summary>
    public float DamageFactor(Enemy enemy)
    {
        var tree = Stats.Trapper;
        float factor = 1f;
        if (_marks.ContainsKey(enemy))
        {
            factor *= 1f + RangerStats.MarkDamage;
        }

        if (tree.HeldDamage > 0f && IsHeld(enemy))
        {
            factor *= 1f + tree.HeldDamage;
        }

        if (tree.PoisonedDamage > 0f && IsPoisoned(enemy))
        {
            factor *= 1f + tree.PoisonedDamage;
        }

        return factor;
    }

    /// <summary>Blight Arrows: an arrow goes through <paramref name="enemy"/> without using up any pierce if it is poisoned.</summary>
    public bool PassesThrough(Enemy enemy) => Stats.Trapper.BlightArrows && IsPoisoned(enemy);

    /// <summary>An arrow hit <paramref name="enemy"/> for <paramref name="hit"/> (before a crit): with Venom Tips, it is poisoned.</summary>
    public void ArrowHit(Enemy enemy, float hit)
    {
        if (Stats.Trapper.VenomTips)
        {
            Poison(enemy, hit * RangerStats.PoisonShare);
        }
    }

    /// <summary>
    /// Poisons <paramref name="enemy"/> with a dose worth <paramref name="amount"/> over <see cref="RangerStats.BasePoisonTime"/> (lasting <see
    /// cref="RangerStats.PoisonTime"/>, biting at the same rate): a fresh dose, or - at the most it can carry - its weakest made fresh again. Nothing happens to one
    /// dead, or to a crate.
    /// </summary>
    public void Poison(Enemy enemy, float amount)
    {
        if (!enemy.IsAlive || enemy.Kind.IsProp || amount <= 0f)
        {
            return;
        }

        if (!_poisons.TryGetValue(enemy, out var poisoning))
        {
            poisoning = new Poisoning { TickIn = PoisonTick };
            _poisons[enemy] = poisoning;
        }

        var dose = (Stats.PoisonTime, amount * Stats.PoisonScale / RangerStats.BasePoisonTime);
        if (poisoning.Doses.Count < Stats.PoisonStacks)
        {
            poisoning.Doses.Add(dose);
            return;
        }

        int weakest = 0;
        for (int i = 1; i < poisoning.Doses.Count; i++)
        {
            var d = poisoning.Doses[i];
            var w = poisoning.Doses[weakest];
            if (d.PerSecond * d.Left < w.PerSecond * w.Left)
            {
                weakest = i;
            }
        }

        poisoning.Doses[weakest] = dose;
    }

    /// <summary>A dash began at <paramref name="feet"/>: with Snare Line, a snare is set there (the oldest goes, past the most that can be out).</summary>
    public void LaySnare(Vector3D<float> feet)
    {
        if (!Stats.Trapper.SnareLine)
        {
            return;
        }

        _snares.Add(new Snare { Position = feet, Yaw = (float)_random.NextDouble() * MathF.Tau, Left = Stats.SnareLifetime });
        while (_snares.Count > Stats.MaxSnares)
        {
            int oldest = _snares.FindIndex(s => !s.Sprung);
            _snares.RemoveAt(oldest >= 0 ? oldest : 0);
        }
    }

    /// <summary>
    /// One frame of it all around the Ranger at <paramref name="feet"/>: the snares, the caltrops, the poison and its clouds, and the hawk (which only dives while
    /// <paramref name="canAct"/> - a stun holds it). Every hit goes on <paramref name="hits"/>.
    /// </summary>
    public void Update(float deltaSeconds, Vector3D<float> feet, EnemyField enemies, bool canAct, List<TrapperHit> hits)
    {
        UpdateSnares(deltaSeconds, enemies, hits);
        UpdateCaltrops(deltaSeconds, enemies, hits);
        UpdateClouds(deltaSeconds, enemies);
        UpdatePoison(deltaSeconds, enemies, hits);
        UpdateHawk(deltaSeconds, feet, enemies, canAct, hits);

        foreach (var (enemy, left) in _marks.ToList())
        {
            if (!enemy.IsAlive || left <= deltaSeconds)
            {
                _marks.Remove(enemy);
            }
            else
            {
                _marks[enemy] = left - deltaSeconds;
            }
        }

        foreach (var burst in _bursts)
        {
            burst.Age += deltaSeconds;
        }

        _bursts.RemoveAll(b => b.Age >= BurstShow);
    }

    /// <summary>Damage from the Ranger's traps, poison or hawk to one enemy, raised by <see cref="DamageFactor"/>. True if it killed.</summary>
    public bool Hurt(Enemy enemy, float amount, bool crit, TrapperSource source, EnemyField enemies, List<TrapperHit> hits)
    {
        if (!enemy.IsAlive || enemy.IsRoaring || amount <= 0f)
        {
            return false;   // (a roaring boss shrugs everything off: no number for it)
        }

        amount *= DamageFactor(enemy);
        bool killed = enemies.Damage(enemy, amount);
        hits.Add(new TrapperHit(enemy, enemy.Position + new Vector3D<float>(0f, enemy.Kind.Height * 0.7f, 0f), amount, killed, crit, source));
        return killed;
    }

    /// <summary>Everything out of the world (a run ended), and the clocks back to the start.</summary>
    public void Reset()
    {
        _snares.Clear();
        _caltrops.Clear();
        _clouds.Clear();
        _bursts.Clear();
        _poisons.Clear();
        _marks.Clear();
        Hawk = null;
        DiveIn = RangerStats.HawkInterval;
    }

    private void UpdateSnares(float deltaSeconds, EnemyField enemies, List<TrapperHit> hits)
    {
        for (int i = _snares.Count - 1; i >= 0; i--)
        {
            var snare = _snares[i];
            if (!snare.Sprung)
            {
                snare.Left -= deltaSeconds;
                if (snare.Left <= 0f)
                {
                    _snares.RemoveAt(i);
                    continue;
                }

                if (Stepper(snare, enemies) is not { } enemy)
                {
                    continue;
                }

                float hold = enemy.Kind.Tier switch
                {
                    EnemyTier.Boss => 0f,
                    EnemyTier.Elite => Stats.SnareHoldTime * RangerStats.EliteHoldShare,
                    _ => Stats.SnareHoldTime,
                };
                snare.Sprung = true;
                snare.Holding = enemy;
                snare.BurstIn = hold;
                if (hold > 0f)
                {
                    enemy.Freeze(hold);
                }
            }
            else
            {
                snare.BurstIn -= deltaSeconds;
            }

            if (snare.BurstIn > 0f)
            {
                continue;
            }

            if (Burst(snare, enemies, hits) && Stats.Trapper.KillingField && !snare.Reset)
            {
                snare.Reset = true;   // Killing Field: set again, once
                snare.Sprung = false;
                snare.Holding = null;
                snare.Left = Stats.SnareLifetime;
            }
            else
            {
                _snares.RemoveAt(i);
            }
        }
    }

    /// <summary>The first enemy (not a crate) standing on <paramref name="snare"/>, or null.</summary>
    private static Enemy? Stepper(Snare snare, EnemyField enemies)
    {
        foreach (var enemy in enemies.Within(snare.Position, TriggerRadius))
        {
            if (!enemy.Kind.IsProp)
            {
                return enemy;
            }
        }

        return null;
    }

    /// <summary>A snare bursts: everything within its reach is hurt, and with Caltrops they're left lying there. True if it killed anything.</summary>
    private bool Burst(Snare snare, EnemyField enemies, List<TrapperHit> hits)
    {
        float reach = Stats.SnareReach;
        bool killed = false;
        foreach (var enemy in enemies.Within(snare.Position, reach))
        {
            killed |= Hurt(enemy, Stats.SnareDamage, crit: false, TrapperSource.Snare, enemies, hits) && !enemy.Kind.IsProp;
        }

        _bursts.Add(new SnareBurst { Centre = snare.Position, Radius = reach });
        if (Stats.Trapper.Caltrops)
        {
            _caltrops.Add(new CaltropPatch
            {
                Centre = snare.Position, Radius = reach, Lifetime = RangerStats.CaltropTime, Left = RangerStats.CaltropTime, DamagePerSecond = Stats.CaltropDps,
            });
        }

        return killed;
    }

    /// <summary>The caltrops: running down, and every tick slowing and biting everything in them.</summary>
    private void UpdateCaltrops(float deltaSeconds, EnemyField enemies, List<TrapperHit> hits)
    {
        for (int i = _caltrops.Count - 1; i >= 0; i--)
        {
            var patch = _caltrops[i];
            patch.Left -= deltaSeconds;
            if (patch.Left <= 1e-3f)   // (a hair's slack, so time added up frame by frame doesn't sneak in a tick past its end)
            {
                _caltrops.RemoveAt(i);
                continue;
            }

            patch.TickIn -= deltaSeconds;
            if (patch.TickIn > 0f)
            {
                continue;
            }

            patch.TickIn += GroundTick;
            foreach (var enemy in enemies.Within(patch.Centre, patch.Radius))
            {
                if (enemy.Kind.IsProp)
                {
                    continue;
                }

                enemy.Chill(GroundTick + 0.2f, RangerStats.CaltropSlow);
                Hurt(enemy, patch.DamagePerSecond * GroundTick, crit: false, TrapperSource.Caltrops, enemies, hits);
            }
        }
    }

    /// <summary>The toxic clouds: running down, and every tick poisoning everything in them with a dose worth a share of an arrow's damage.</summary>
    private void UpdateClouds(float deltaSeconds, EnemyField enemies)
    {
        for (int i = _clouds.Count - 1; i >= 0; i--)
        {
            var cloud = _clouds[i];
            cloud.Left -= deltaSeconds;
            if (cloud.Left <= 1e-3f)
            {
                _clouds.RemoveAt(i);
                continue;
            }

            cloud.TickIn -= deltaSeconds;
            if (cloud.TickIn > 0f)
            {
                continue;
            }

            cloud.TickIn += CloudTick;
            foreach (var enemy in enemies.Within(cloud.Centre, cloud.Radius))
            {
                Poison(enemy, Stats.Damage * RangerStats.PoisonShare);
            }
        }
    }

    /// <summary>
    /// The poison on every enemy: each dose running down, and every tick the enemy takes every dose's bite (never a crit: damage over time doesn't crit). An enemy
    /// carrying all it can is slowed (Crippling Venom). A poisoned enemy that died, of anything, leaves a cloud (Toxic Cloud).
    /// </summary>
    private void UpdatePoison(float deltaSeconds, EnemyField enemies, List<TrapperHit> hits)
    {
        if (_poisons.Count == 0)
        {
            return;
        }

        foreach (var (enemy, poisoning) in _poisons.ToList())
        {
            if (!enemy.IsAlive)
            {
                Died(enemy);
                continue;
            }

            if (Stats.Trapper.CripplingVenom && poisoning.Doses.Count >= Stats.PoisonStacks)
            {
                enemy.Chill(0.2f, RangerStats.CrippleSlow);
            }

            for (int i = 0; i < poisoning.Doses.Count; i++)
            {
                var dose = poisoning.Doses[i];
                poisoning.Doses[i] = (dose.Left - deltaSeconds, dose.PerSecond);
            }

            poisoning.TickIn -= deltaSeconds;
            while (poisoning.TickIn <= 0f && poisoning.Doses.Count > 0 && enemy.IsAlive)
            {
                poisoning.TickIn += PoisonTick;
                float bite = 0f;
                foreach (var dose in poisoning.Doses)
                {
                    bite += dose.PerSecond * PoisonTick;
                }

                Hurt(enemy, bite, crit: false, TrapperSource.Poison, enemies, hits);
            }

            poisoning.Doses.RemoveAll(d => d.Left <= 1e-3f);
            if (!enemy.IsAlive)
            {
                Died(enemy);
            }
            else if (poisoning.Doses.Count == 0)
            {
                _poisons.Remove(enemy);
            }
        }
    }

    /// <summary>A poisoned enemy died: its poison goes, and with Toxic Cloud it leaves a cloud where it fell.</summary>
    private void Died(Enemy enemy)
    {
        _poisons.Remove(enemy);
        if (!Stats.Trapper.ToxicCloud)
        {
            return;
        }

        if (_clouds.Count >= MaxClouds)
        {
            _clouds.RemoveAt(0);
        }

        float time = Stats.CloudTime;
        _clouds.Add(new ToxicCloud { Centre = enemy.Position, Radius = Stats.CloudRadius, Lifetime = time, Left = time, TickIn = 0f });
    }

    /// <summary>
    /// The hawk: circling above the Ranger, and when its dive is due, diving at the toughest enemy in range (bosses, then elites, then the most health), striking
    /// it, and climbing back. It comes with Hawk Companion and goes without it.
    /// </summary>
    private void UpdateHawk(float deltaSeconds, Vector3D<float> feet, EnemyField enemies, bool canAct, List<TrapperHit> hits)
    {
        if (!Stats.Trapper.HawkCompanion)
        {
            Hawk = null;
            return;
        }

        var hawk = Hawk ??= new Hawk { Position = CirclePoint(feet, 0f), Angle = 0f };
        DiveIn = MathF.Max(0f, DiveIn - deltaSeconds);
        hawk.Angle += MathF.Tau / CircleTime * deltaSeconds;

        switch (hawk.Flight)
        {
            case HawkFlight.Circling:
            {
                var next = CirclePoint(feet, hawk.Angle);
                Face(hawk, next - hawk.Position);
                hawk.Position = next;
                if (canAct && DiveIn <= 0f && Toughest(enemies, feet, Stats.HawkRange) is { } prey)
                {
                    hawk.Flight = HawkFlight.Diving;
                    hawk.Prey = prey;
                    hawk.From = hawk.Position;
                    hawk.Progress = 0f;
                    DiveIn = Stats.HawkDiveInterval;
                    if (Stats.Trapper.HawksMark)
                    {
                        _marks[prey] = RangerStats.MarkTime;
                    }
                }

                break;
            }

            case HawkFlight.Diving:
            {
                var prey = hawk.Prey!;
                hawk.Progress = MathF.Min(1f, hawk.Progress + deltaSeconds / DiveTime);
                var target = prey.Position + new Vector3D<float>(0f, prey.Kind.Height * StrikeHeight, 0f);
                var next = Vector3D.Lerp(hawk.From, target, hawk.Progress * hawk.Progress);   // gathering speed as it stoops
                Face(hawk, next - hawk.Position);
                hawk.Position = next;
                if (!prey.IsAlive)
                {
                    Climb(hawk);   // something else got it first
                }
                else if (hawk.Progress >= 1f)
                {
                    Strike(prey, enemies, hits);
                    Climb(hawk);
                }

                break;
            }

            case HawkFlight.Returning:
            {
                hawk.Progress = MathF.Min(1f, hawk.Progress + deltaSeconds / ClimbTime);
                float t = hawk.Progress;
                var next = Vector3D.Lerp(hawk.From, CirclePoint(feet, hawk.Angle), t * (2f - t));   // slowing as it reaches its circle
                Face(hawk, next - hawk.Position);
                hawk.Position = next;
                if (hawk.Progress >= 1f)
                {
                    hawk.Flight = HawkFlight.Circling;
                }

                break;
            }
        }
    }

    /// <summary>The dive lands: its damage (a crit roll, always with Keen Talons), and with Apex Predator its prey poisoned as much as it can carry.</summary>
    private void Strike(Enemy prey, EnemyField enemies, List<TrapperHit> hits)
    {
        float damage = Stats.HawkDamage;
        bool crit = Stats.Trapper.KeenTalons || _random.NextDouble() < Stats.CritChance;
        Hurt(prey, crit ? damage * Stats.CritMultiplier : damage, crit, TrapperSource.Hawk, enemies, hits);
        if (Stats.Trapper.ApexPredator)
        {
            for (int i = 0; i < Stats.PoisonStacks; i++)
            {
                Poison(prey, damage * RangerStats.PoisonShare);
            }
        }
    }

    private static void Climb(Hawk hawk)
    {
        hawk.Flight = HawkFlight.Returning;
        hawk.Prey = null;
        hawk.From = hawk.Position;
        hawk.Progress = 0f;
    }

    private static void Face(Hawk hawk, Vector3D<float> way)
    {
        if (way.LengthSquared > 1e-8f)
        {
            hawk.Heading = Vector3D.Normalize(way);
        }
    }

    private static Vector3D<float> CirclePoint(Vector3D<float> feet, float angle) =>
        feet + new Vector3D<float>(MathF.Sin(angle) * CircleRadius, CircleHeight, MathF.Cos(angle) * CircleRadius);

    /// <summary>The toughest live enemy (not a crate) within <paramref name="range"/> of <paramref name="feet"/>: a boss, then an elite, then the most health left.</summary>
    public static Enemy? Toughest(EnemyField enemies, Vector3D<float> feet, float range)
    {
        Enemy? best = null;
        foreach (var enemy in enemies.Within(feet, range))
        {
            if (enemy.Kind.IsProp)
            {
                continue;
            }

            if (best is null || Rank(enemy.Kind.Tier) > Rank(best.Kind.Tier) || (Rank(enemy.Kind.Tier) == Rank(best.Kind.Tier) && enemy.Health > best.Health))
            {
                best = enemy;
            }
        }

        return best;
    }

    private static int Rank(EnemyTier tier) => tier switch
    {
        EnemyTier.Boss => 2,
        EnemyTier.Elite => 1,
        _ => 0,
    };
}
