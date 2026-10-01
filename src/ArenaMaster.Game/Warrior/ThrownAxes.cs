using ArenaMaster.Game.Combat;
using Silk.NET.Maths;

namespace ArenaMaster.Game.Warrior;

/// <summary>
/// One axe in the air: thrown from the hand, it spins out toward <see cref="TurnPoint"/> (or, bouncing, straight at <see cref="Chase"/>), then back to the
/// Warrior wherever he has gone. It hits each enemy it passes once on the way out and once on the way back. Its bounces come after its throw's straight line:
/// from the far end it flies on at the nearest enemy it hasn't hit, and from that one to the next, before it turns back.
/// </summary>
internal sealed class ThrownAxe
{
    /// <summary>Which hand it is from: 1 the right, -1 the left, 0 a spare (one of a throw's extra axes, or Harvester's free one), which is gone when it comes back.</summary>
    public int Hand { get; init; }

    /// <summary>Where it is, at the height of the Warrior's feet when it was thrown (the view lifts it to the hand's height).</summary>
    public Vector3D<float> Position { get; set; }

    /// <summary>The spot it flies out to, its range away, where it turns back.</summary>
    public Vector3D<float> TurnPoint { get; set; }

    /// <summary>The enemy it has bounced to: it flies straight at it, and bounces on or turns back once it has hit it.</summary>
    public Enemy? Chase { get; set; }

    /// <summary>The way it last flew (flat, unit length), for the view.</summary>
    public Vector3D<float> Heading { get; set; }

    public bool Returning { get; set; }

    /// <summary>What each enemy it passes takes, before a crit and the rest (Catch's boost is in it already).</summary>
    public float Damage { get; init; }

    /// <summary>Bounces left before it turns back.</summary>
    public int Bounces { get; set; }

    /// <summary>How far it has spun over (radians), for the view.</summary>
    public float Spin { get; set; }

    public float Age { get; set; }

    /// <summary>Back in the hand (or, a free axe, gone): taken out of the air at the end of the frame.</summary>
    public bool Done { get; set; }

    public HashSet<Enemy> StruckOut { get; } = new();

    public HashSet<Enemy> StruckBack { get; } = new();
}

/// <summary>Bleeding on one enemy: a stack for each wound (each with its own damage a second and time left, up to the most it can carry), and when it next ticks.</summary>
internal sealed class Wound
{
    public List<BleedStack> Stacks { get; } = new();

    public float TickIn { get; set; }
}

internal struct BleedStack
{
    public float PerSecond;
    public float Left;
}

/// <summary>A pool of blood a bleeding enemy left where it died (Bloodbath): standing in it heals.</summary>
internal sealed class BloodPool
{
    public Vector3D<float> Centre { get; init; }

    public float Left { get; set; }
}

/// <summary>
/// The Reaver's axes: with the Reaver tree active, the Warrior throws his axes instead of swinging them. On the same clock as the Cleave (the right axe, then the
/// left), each is thrown at the nearest enemy in range: it spins out its range and back to the hand, hitting what it passes both ways. An axe is either in the hand
/// or in the air, so with both out the next throw waits. Here too is what the Reaver tree adds: bleeding (Hemorrhage, Deep Wounds, Run Them Down's charge), the pools
/// of blood (Bloodbath), Catch, the bounces (and Ricochet Axes' more), the spare axes of a throw, Closing In, Returning Edge, Axe Storm, Crimson Tide and Harvester's free axes. Pure simulation - no engine calls - so it
/// can be tested; <see cref="ReaverView"/> draws it.
/// </summary>
internal sealed class ThrownAxes
{
    /// <summary>The first throw of a run comes this soon.</summary>
    public const float FirstThrow = 0.3f;

    /// <summary>Bleeding ticks this often.</summary>
    public const float BleedTick = 0.25f;

    /// <summary>An axe in the air this long is back in the hand, wherever it is (it can't be lost).</summary>
    public const float MaxFlight = 4f;

    /// <summary>How fast a thrown axe spins end over end (radians a second), for the view.</summary>
    public const float SpinRate = 20f;

    /// <summary>A thrown axe leaves the hand this far in front of the Warrior.</summary>
    private const float ReleaseAhead = 0.5f;

    private readonly Random _random;
    private readonly List<ThrownAxe> _axes = new();
    private readonly Dictionary<Enemy, Wound> _wounds = new();
    private readonly List<BloodPool> _pools = new();
    private readonly HashSet<Enemy> _chargeStruck = new();
    private bool _wasCharging;
    private int _nextHand = 1;
    private int _kills;

    public ThrownAxes(Random random) => _random = random;

    public IReadOnlyList<ThrownAxe> Axes => _axes;

    public IReadOnlyDictionary<Enemy, Wound> Wounds => _wounds;

    public IReadOnlyList<BloodPool> Pools => _pools;

    /// <summary>Seconds until the next throw.</summary>
    public float ThrowIn { get; private set; } = FirstThrow;

    /// <summary>Throws from the hand this run (free axes don't count).</summary>
    public int Throws { get; private set; }

    /// <summary>Catch: an axe was caught, and the next throw is stronger.</summary>
    public bool CatchReady { get; private set; }

    /// <summary>Axe Storm: seconds until the next storm is due (it then waits for both axes to be in hand and something to fight).</summary>
    public float StormIn { get; private set; } = WarriorStats.StormEvery;

    /// <summary>Axe Storm: seconds left of the storm now on.</summary>
    public float StormLeft { get; private set; }

    public bool Storming => StormLeft > 0f;

    /// <summary>Axe Storm: where the right axe is on its circle (radians, 0 is +Z); the left is opposite.</summary>
    public float StormAngle { get; private set; }

    /// <summary>Harvester: free axes owed, thrown as soon as there is something to throw at.</summary>
    public int FreeThrowsDue { get; private set; }

    /// <summary>Where the Warrior's feet were last frame: the axes come back to him, and the storm circles him.</summary>
    public Vector3D<float> Owner { get; private set; }

    /// <summary>Whether the axe from <paramref name="hand"/> (1 the right, -1 the left) is in the hand, not in the air.</summary>
    public bool InHand(int hand) => !_axes.Exists(a => a.Hand == hand);

    public bool IsBleeding(Enemy enemy) => enemy.IsAlive && _wounds.TryGetValue(enemy, out var wound) && wound.Stacks.Count > 0;

    /// <summary>How many bleeding enemies are within <paramref name="range"/> of <paramref name="centre"/> (Blood Scent).</summary>
    public int BleedingNear(Vector3D<float> centre, float range)
    {
        int count = 0;
        foreach (var (enemy, wound) in _wounds)
        {
            if (enemy.IsAlive && wound.Stacks.Count > 0)
            {
                Geometry.FlatDirection(centre, enemy.Position, out float distance);
                if (distance <= range)
                {
                    count++;
                }
            }
        }

        return count;
    }

    /// <summary>Whether <paramref name="feet"/> stand in a pool of blood (Bloodbath).</summary>
    public bool InPool(Vector3D<float> feet)
    {
        foreach (var pool in _pools)
        {
            Geometry.FlatDirection(pool.Centre, feet, out float distance);
            if (distance <= WarriorStats.PoolRadius)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// One frame: a throw when one is due and an axe is in the hand (held while <paramref name="canThrow"/> is false - a stun) toward <paramref name="facingYaw"/> from
    /// <paramref name="feet"/>, or an Axe Storm when one is due; Harvester's free axes; every axe in the air flying on and hitting what it passes; the storm's axes
    /// circling; the battle charge hitting what it passes, while <paramref name="charging"/> (Run Them Down); and the bleeding and the pools. With no
    /// <paramref name="facingYaw"/> (nothing in range) the axes wait, ready, and are thrown the moment something comes. Every hit dealt goes on <paramref name="hits"/>.
    /// </summary>
    public void Update(float deltaSeconds, Vector3D<float> feet, float? facingYaw, WarriorStats stats, EnemyField enemies, bool canThrow, bool charging,
                       List<CleaveHit> hits)
    {
        Owner = feet;
        UpdateStorm(deltaSeconds, facingYaw, stats);

        ThrowIn -= deltaSeconds;
        if (!canThrow)
        {
            ThrowIn = MathF.Max(ThrowIn, 0.15f);
        }
        else if (Storming || (stats.Reaver.AxeStorm && StormIn <= 0f))
        {
            ThrowIn = MathF.Max(ThrowIn, 0f);   // the axes are circling, or called back for a storm: no throws meanwhile
        }
        else if (ThrowIn <= 0f)
        {
            int hand = InHand(_nextHand) ? _nextHand : InHand(-_nextHand) ? -_nextHand : 0;
            if (facingYaw is { } yaw && hand != 0)
            {
                ThrowIn = MathF.Max(0f, ThrowIn + stats.SwingInterval);   // after a pause, no flurry of throws to catch up
                Throw(feet, yaw, stats, hand);
            }
            else
            {
                ThrowIn = 0f;   // ready, and waiting for something to throw at or an axe to come back
            }
        }

        if (canThrow && facingYaw is { } free)
        {
            for (; FreeThrowsDue > 0; FreeThrowsDue--)
            {
                Throw(feet, free, stats, hand: 0);
            }
        }

        foreach (var axe in _axes)
        {
            Fly(axe, deltaSeconds, feet, stats, enemies, hits);
        }

        _axes.RemoveAll(a => a.Done);

        if (Storming)
        {
            Circle(deltaSeconds, feet, stats, enemies, hits);
        }

        if (charging && !_wasCharging)
        {
            _chargeStruck.Clear();
        }

        _wasCharging = charging;
        if (charging && stats.Reaver.RunThemDown)
        {
            Charge(feet, stats, enemies, hits);
        }

        TickBleeding(deltaSeconds, stats, enemies, hits);
        for (int i = _pools.Count - 1; i >= 0; i--)
        {
            _pools[i].Left -= deltaSeconds;
            if (_pools[i].Left <= 0f)
            {
                _pools.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// A throw from <paramref name="feet"/> toward <paramref name="facingYaw"/>: the axe from <paramref name="hand"/> (0 for a free axe), stronger if one was just caught
    /// (Catch), and with it as many spares as the stats give, fanned out either side. Each bounces on between enemies as many times as the stats say.
    /// </summary>
    public void Throw(Vector3D<float> feet, float facingYaw, WarriorStats stats, int hand)
    {
        var way = new Vector3D<float>(MathF.Sin(facingYaw), 0f, MathF.Cos(facingYaw));
        float damage = stats.ThrowDamage;
        if (hand != 0)
        {
            Throws++;
            _nextHand = -hand;
            if (CatchReady)
            {
                damage *= stats.CatchMultiplier;
                CatchReady = false;
            }
        }

        int count = stats.AxesPerThrow;
        int own = count / 2;   // the hand's own axe flies down the middle; the spares fan out round it
        for (int i = 0; i < count; i++)
        {
            float yaw = facingYaw + (i - (count - 1) * 0.5f) * WarriorStats.AxeFan * MathF.PI / 180f;
            var fanned = count == 1 ? way : new Vector3D<float>(MathF.Sin(yaw), 0f, MathF.Cos(yaw));
            _axes.Add(new ThrownAxe
            {
                Hand = i == own ? hand : 0,
                Position = feet + fanned * ReleaseAhead,
                TurnPoint = feet + fanned * stats.ThrowRange,
                Heading = fanned,
                Damage = damage,
                Bounces = stats.AxeChains,
            });
        }
    }

    /// <summary>A kill: Harvester owes a free axe every <see cref="WarriorStats.HarvesterEvery"/>th.</summary>
    public void CountKill(WarriorStats stats)
    {
        _kills++;
        if (stats.Reaver.Harvester && _kills % WarriorStats.HarvesterEvery == 0)
        {
            FreeThrowsDue++;
        }
    }

    /// <summary>
    /// Hurts one enemy with an axe or the charge (harder if it is an elite or a boss), rolled crit already in <paramref name="crit"/>, and leaves it bleeding with
    /// <paramref name="bleedStacks"/> stacks of the hit's share (a crit's extra not counted: bleeding never crits). Nothing happens to one already dead.
    /// </summary>
    public void Hurt(Enemy enemy, float amount, bool crit, CleaveSource source, int bleedStacks, WarriorStats stats, EnemyField enemies, List<CleaveHit> hits)
    {
        if (!enemy.IsAlive || amount <= 0f)
        {
            return;
        }

        if (enemy.Kind.Tier != EnemyTier.Fodder)
        {
            amount *= stats.EliteMultiplier;
        }

        float dealt = amount * (crit ? stats.CritMultiplier : 1f);
        bool killed = enemies.Damage(enemy, dealt);
        hits.Add(new CleaveHit(enemy, enemy.Position + new Vector3D<float>(0f, enemy.Kind.Height * 0.7f, 0f), dealt, killed, crit, source));
        if (!killed && bleedStacks > 0)
        {
            Bleed(enemy, amount * WarriorStats.BleedShare / WarriorStats.BaseBleedSeconds * stats.BleedScale, bleedStacks, stats);
        }
    }

    /// <summary>
    /// Leaves <paramref name="enemy"/> bleeding <paramref name="perSecond"/>, <paramref name="stacks"/> times over: a fresh stack each, or - at the most it can carry -
    /// its oldest stack replaced. Nothing happens to one dead, or to a crate.
    /// </summary>
    public void Bleed(Enemy enemy, float perSecond, int stacks, WarriorStats stats)
    {
        if (!enemy.IsAlive || enemy.Kind.IsProp || perSecond <= 0f)
        {
            return;
        }

        if (!_wounds.TryGetValue(enemy, out var wound))
        {
            wound = new Wound { TickIn = BleedTick };
            _wounds[enemy] = wound;
        }

        var fresh = new BleedStack { PerSecond = perSecond, Left = stats.BleedSeconds };
        for (int n = 0; n < stacks; n++)
        {
            if (wound.Stacks.Count < stats.BleedStacks)
            {
                wound.Stacks.Add(fresh);
                continue;
            }

            int oldest = 0;
            for (int i = 1; i < wound.Stacks.Count; i++)
            {
                if (wound.Stacks[i].Left < wound.Stacks[oldest].Left)
                {
                    oldest = i;
                }
            }

            wound.Stacks[oldest] = fresh;
        }
    }

    /// <summary>Everything out of the air and off the ground (a restart), and the clocks back to the start.</summary>
    public void Reset()
    {
        _axes.Clear();
        _wounds.Clear();
        _pools.Clear();
        _chargeStruck.Clear();
        _wasCharging = false;
        _nextHand = 1;
        _kills = 0;
        ThrowIn = FirstThrow;
        Throws = 0;
        CatchReady = false;
        StormIn = WarriorStats.StormEvery;
        StormLeft = 0f;
        StormAngle = 0f;
        FreeThrowsDue = 0;
    }

    /// <summary>
    /// Axe Storm's clock: counting down to the next storm; once due, it starts when both axes are in hand and there is something to fight, and runs its time out.
    /// </summary>
    private void UpdateStorm(float deltaSeconds, float? facingYaw, WarriorStats stats)
    {
        if (!stats.Reaver.AxeStorm)
        {
            StormLeft = 0f;
            return;
        }

        if (Storming)
        {
            StormLeft -= deltaSeconds;
            if (StormLeft <= 0f)
            {
                StormLeft = 0f;
                StormIn = WarriorStats.StormEvery;
            }

            return;
        }

        StormIn -= deltaSeconds;
        if (StormIn <= 0f && facingYaw is { } yaw && InHand(1) && InHand(-1))
        {
            StormIn = 0f;
            StormLeft = WarriorStats.StormSeconds;
            StormAngle = yaw;
        }
    }

    /// <summary>
    /// One axe's flight this frame: out toward its turning point or the enemy it bounced to, or back to the Warrior (caught once it is near him: then taken out of the
    /// air, with Catch readying a stronger throw), hitting what it passes along the way.
    /// </summary>
    private void Fly(ThrownAxe axe, float deltaSeconds, Vector3D<float> feet, WarriorStats stats, EnemyField enemies, List<CleaveHit> hits)
    {
        axe.Age += deltaSeconds;
        axe.Spin += SpinRate * deltaSeconds;
        float step = stats.AxeSpeed * deltaSeconds;
        var from = axe.Position;
        bool back = axe.Returning;
        bool caught = false;
        if (!back)
        {
            if (axe.Chase is { IsAlive: false } dead)
            {
                Bounce(axe, dead.Position, stats, enemies);   // what it bounced to is dead already: on to another, or straight back
            }

            var to = axe.Chase?.Position ?? axe.TurnPoint;
            var way = Geometry.FlatDirection(from, to, out float distance);
            if (distance <= step)
            {
                axe.Position = new Vector3D<float>(to.X, from.Y, to.Z);
                if (axe.Chase is null)
                {
                    Bounce(axe, axe.Position, stats, enemies);   // the end of its throw: on to an enemy near there, or back
                }
            }
            else
            {
                axe.Position = from + way * step;
            }

            if (way != Vector3D<float>.Zero)
            {
                axe.Heading = way;
            }
        }
        else
        {
            var way = Geometry.FlatDirection(from, feet, out float distance);
            if (distance <= step + WarriorStats.CatchRadius)
            {
                axe.Position = new Vector3D<float>(feet.X, feet.Y, feet.Z);
                caught = true;
            }
            else
            {
                axe.Position = new Vector3D<float>(from.X + way.X * step, feet.Y, from.Z + way.Z * step);
            }

            if (way != Vector3D<float>.Zero)
            {
                axe.Heading = way;
            }
        }

        Strike(axe, from, axe.Position, back, feet, stats, enemies, hits);

        if (caught || axe.Age >= MaxFlight)
        {
            if (caught && axe.Hand != 0 && stats.Reaver.Catch)
            {
                CatchReady = true;
            }

            axe.Done = true;
        }
    }

    /// <summary>
    /// What an axe passed over between <paramref name="from"/> and <paramref name="to"/>: each enemy near its path not yet hit on this way, hit (harder on the way back
    /// with Returning Edge). Hitting the enemy it bounced to sends it on to the next, or back.
    /// </summary>
    private void Strike(ThrownAxe axe, Vector3D<float> from, Vector3D<float> to, bool back, Vector3D<float> feet, WarriorStats stats, EnemyField enemies,
                        List<CleaveHit> hits)
    {
        var struck = back ? axe.StruckBack : axe.StruckOut;
        var a = new Vector3D<float>(from.X, 0f, from.Z);
        var b = new Vector3D<float>(to.X, 0f, to.Z);
        float reach = stats.AxeRadius;
        float damage = axe.Damage * (back && stats.Reaver.ReturningEdge ? 1f + WarriorStats.ReturnDamage : 1f);
        foreach (var enemy in enemies.Within((a + b) * 0.5f, (b - a).Length * 0.5f + reach))
        {
            if (struck.Contains(enemy))
            {
                continue;
            }

            var at = new Vector3D<float>(enemy.Position.X, 0f, enemy.Position.Z);
            if (Geometry.SegmentDistance(a, b, at, at, out _) > reach + enemy.Kind.Radius)
            {
                continue;
            }

            struck.Add(enemy);
            AxeHit(enemy, damage, feet, stats, enemies, hits);

            if (back)
            {
                continue;
            }

            if (axe.Chase == enemy)
            {
                Bounce(axe, enemy.Position, stats, enemies);   // the enemy it bounced to: on to the next, or back
            }
        }
    }

    /// <summary>
    /// The end of a leg of an axe's way out (its throw's far end, or the enemy it bounced to), at <paramref name="from"/>: with a bounce left and an enemy within
    /// reach it hasn't hit, it flies on at that one; otherwise it turns back. So the bounces come on top of the throw's own straight line, never in place of it.
    /// </summary>
    private static void Bounce(ThrownAxe axe, Vector3D<float> from, WarriorStats stats, EnemyField enemies)
    {
        if (axe.Bounces > 0 && NextBounce(from, axe, stats, enemies) is { } next)
        {
            axe.Bounces--;
            axe.Chase = next;
        }
        else
        {
            axe.Chase = null;
            axe.Returning = true;
        }
    }

    /// <summary>The nearest live enemy within a bounce's reach of <paramref name="from"/> that <paramref name="axe"/> hasn't hit on its way out, or null for none.</summary>
    private static Enemy? NextBounce(Vector3D<float> from, ThrownAxe axe, WarriorStats stats, EnemyField enemies)
    {
        Enemy? best = null;
        float bestDistance = float.MaxValue;
        foreach (var enemy in enemies.Within(from, stats.AxeChainRange))
        {
            if (!enemy.IsAlive || axe.StruckOut.Contains(enemy) || enemy.Kind.IsProp)
            {
                continue;
            }

            Geometry.FlatDirection(from, enemy.Position, out float distance);
            if (distance < bestDistance)
            {
                best = enemy;
                bestDistance = distance;
            }
        }

        return best;
    }

    /// <summary>
    /// An axe's hit on one enemy, rolled for a crit: harder near the Warrior (Closing In) and on a bleeding enemy (Crimson Tide); bleeding with Hemorrhage.
    /// </summary>
    private void AxeHit(Enemy enemy, float damage, Vector3D<float> feet, WarriorStats stats, EnemyField enemies, List<CleaveHit> hits)
    {
        if (stats.Reaver.ClosingIn)
        {
            Geometry.FlatDirection(feet, enemy.Position, out float distance);
            if (distance - enemy.Kind.Radius <= WarriorStats.CloseRange)
            {
                damage *= 1f + WarriorStats.CloseDamage;
            }
        }

        if (stats.Reaver.CrimsonTide && IsBleeding(enemy))
        {
            damage *= 1f + WarriorStats.CrimsonDamage;
        }

        bool crit = _random.NextDouble() < stats.CritChance;
        Hurt(enemy, damage, crit, CleaveSource.Throw, stats.Reaver.Hemorrhage ? 1 : 0, stats, enemies, hits);
    }

    /// <summary>
    /// Axe Storm: both axes on their circle round the Warrior, the right at <see cref="StormAngle"/> and the left opposite, turning on; everything on the circle one of
    /// them passes this frame is hit.
    /// </summary>
    private void Circle(float deltaSeconds, Vector3D<float> feet, WarriorStats stats, EnemyField enemies, List<CleaveHit> hits)
    {
        float turn = MathF.Tau * WarriorStats.StormTurns * deltaSeconds;
        float start = StormAngle;
        StormAngle = MathF.IEEERemainder(StormAngle + turn, MathF.Tau);
        float reach = stats.AxeRadius;
        float damage = stats.ThrowDamage * WarriorStats.StormDamage;
        foreach (var enemy in enemies.Within(feet, WarriorStats.StormRadius + reach))
        {
            float angle = MathF.Atan2(enemy.Position.X - feet.X, enemy.Position.Z - feet.Z);
            Geometry.FlatDirection(feet, enemy.Position, out float distance);
            if (MathF.Abs(distance - WarriorStats.StormRadius) > reach + enemy.Kind.Radius)
            {
                continue;
            }

            for (int k = 0; k < 2; k++)
            {
                float past = angle - (start + k * MathF.PI);
                past -= MathF.Tau * MathF.Floor(past / MathF.Tau);   // how far round from the axe, 0 to a full turn
                if (past < turn)
                {
                    AxeHit(enemy, damage, feet, stats, enemies, hits);
                }
            }
        }
    }

    /// <summary>Run Them Down: everything near the charging Warrior not hit yet this charge takes a throw's damage and bleeds at full stacks.</summary>
    private void Charge(Vector3D<float> feet, WarriorStats stats, EnemyField enemies, List<CleaveHit> hits)
    {
        foreach (var enemy in enemies.Within(feet, WarriorStats.ChargeHitRadius))
        {
            if (!_chargeStruck.Add(enemy))
            {
                continue;
            }

            bool crit = _random.NextDouble() < stats.CritChance;
            Hurt(enemy, stats.ThrowDamage, crit, CleaveSource.Charge, stats.BleedStacks, stats, enemies, hits);
        }
    }

    /// <summary>
    /// The bleeding on every enemy: each stack running down, and every tick the enemy takes every stack's share (never a crit: damage over time doesn't crit). A bleeding
    /// enemy that died (from anything) leaves a pool of blood with Bloodbath.
    /// </summary>
    private void TickBleeding(float deltaSeconds, WarriorStats stats, EnemyField enemies, List<CleaveHit> hits)
    {
        if (_wounds.Count == 0)
        {
            return;
        }

        foreach (var (enemy, wound) in _wounds.ToList())
        {
            if (!enemy.IsAlive)
            {
                Died(enemy, stats);
                continue;
            }

            float perSecond = 0f;
            for (int i = wound.Stacks.Count - 1; i >= 0; i--)
            {
                var stack = wound.Stacks[i];
                perSecond += stack.PerSecond;
                stack.Left -= deltaSeconds;
                wound.Stacks[i] = stack;
            }

            wound.TickIn -= deltaSeconds;
            while (wound.TickIn <= 0f && wound.Stacks.Count > 0 && enemy.IsAlive)
            {
                wound.TickIn += BleedTick;
                float amount = perSecond * BleedTick;
                bool killed = enemies.Damage(enemy, amount);
                hits.Add(new CleaveHit(enemy, enemy.Position + new Vector3D<float>(0f, enemy.Kind.Height * 0.7f, 0f), amount, killed, false, CleaveSource.Bleed));
            }

            wound.Stacks.RemoveAll(s => s.Left <= 0f);
            if (!enemy.IsAlive)
            {
                Died(enemy, stats);
            }
            else if (wound.Stacks.Count == 0)
            {
                _wounds.Remove(enemy);
            }
        }
    }

    /// <summary>A bleeding enemy died: its bleeding is over, and with Bloodbath it leaves a pool of blood where it fell.</summary>
    private void Died(Enemy enemy, WarriorStats stats)
    {
        _wounds.Remove(enemy);
        if (stats.Reaver.Bloodbath)
        {
            _pools.Add(new BloodPool { Centre = enemy.Position, Left = WarriorStats.PoolSeconds });
        }
    }
}
