using ArenaMaster.Game.Combat;
using Silk.NET.Maths;

namespace ArenaMaster.Game.Shaman;

/// <summary>
/// Whether a ball at <paramref name="center"/> touches a solid obstacle (a tree trunk, a rock, a solid prop), the flat way out of it and how deep it is in. In the
/// game this is the engine's <c>EngineWindow.TouchesObstacle</c>; tests hand in their own.
/// </summary>
internal delegate bool ObstacleProbe(Vector3D<float> center, float radius, out Vector3D<float> pushOut, out float depth);

/// <summary>A ball of lightning: lobbed, falling, bouncing along the ground, glancing off enemies and trees, until its bounces or its life run out.</summary>
internal sealed class LightningBall
{
    public Vector3D<float> Position { get; set; }

    public Vector3D<float> Velocity { get; set; }

    public float Radius { get; init; }

    public float Damage { get; init; }

    /// <summary>Bounces left: the ball fades when it strikes the ground with none left.</summary>
    public int BouncesLeft { get; set; }

    public float Life { get; init; }

    public float Age { get; set; }

    /// <summary>A supercell (every 5th cast with the major): big and hard-hitting.</summary>
    public bool Supercell { get; init; }

    /// <summary>The enemies it has just struck, and how long until it may strike each again - so rolling through one hits it once, not every frame.</summary>
    public Dictionary<Enemy, float> Recent { get; } = new();

    /// <summary>Seconds until it can glance off another obstacle (it is pushed clear of one, but a corner could catch it twice).</summary>
    public float ObstacleCooldown { get; set; }
}

/// <summary>A bolt of lightning drawn for a moment between two points: a fork, a rod's zap, a strike from the sky.</summary>
internal sealed class LightningArc
{
    public Vector3D<float> From { get; init; }

    public Vector3D<float> To { get; init; }

    public float Age { get; set; }
}

/// <summary>A ring of lightning spreading over the ground, for the view: a bounce's zap, the Eye of the Storm's shock, the Thunder God's burst.</summary>
internal sealed class LightningZap
{
    public Vector3D<float> Centre { get; init; }

    public float Radius { get; init; }

    public float Age { get; set; }
}

/// <summary>A tree or rock the ball glanced off, charged for a while (Lightning Rod): it zaps every enemy near it every so often.</summary>
internal sealed class LightningRodSpot
{
    public Vector3D<float> Position { get; init; }

    public float Left { get; set; }

    public float TickIn { get; set; }
}

internal enum StormSource
{
    Ball,
    Zap,
    Fork,
    LivingFork,
    Rod,
    Eye,
    Burst,
    Call,
    Shock,
}

internal readonly record struct StormHit(Enemy Enemy, Vector3D<float> Position, float Damage, bool Killed, bool Crit, StormSource Source);

/// <summary>
/// The Shaman's attack, Rolling Lightning: every so often a ball of lightning (more with upgrades) is lobbed in an arc at the crosshair. It bounces along the ground,
/// zapping around it each time it lands, and it glances off what it meets - an enemy it strikes, a tree or rock - forking lightning to the enemies nearby each time.
/// It fades once its bounces or its life run out. And Lightning Alignment's majors: Paralysis, Thunderclap, Chain Reaction, Supercell, Lightning Rod, Ground
/// Current, Eye of the Storm, the Thunder God's burst, Living Current and Call Lightning. Pure - no engine calls; <see cref="StormView"/> draws it.
/// </summary>
internal sealed class RollingLightning
{
    /// <summary>How long a drawn arc lasts, and a zap's ring.</summary>
    public const float ArcSeconds = 0.18f;
    public const float ZapSeconds = 0.3f;

    /// <summary>A ball won't strike the same enemy again for this long.</summary>
    public const float EnemyRehit = 0.5f;

    /// <summary>The first cast of a run comes this soon.</summary>
    public const float FirstCast = 0.4f;

    /// <summary>At most this many charged trees at once; a new one past it takes the oldest's place.</summary>
    public const int MaxRods = 6;

    /// <summary>Extra balls of a cast are aimed this many degrees apart around the crosshair.</summary>
    public const float SpreadDegrees = 14f;

    /// <summary>A surge's ball (Surge Strike) leaves low: this much upward speed.</summary>
    public const float SurgeLift = 4f;

    /// <summary>A ball keeps this much of its speed across the ground on a bounce, and when it glances off an enemy.</summary>
    public const float GroundFriction = 0.92f;
    public const float EnemyGlance = 0.9f;

    private readonly Random _random;
    private readonly List<LightningBall> _balls = new();
    private readonly List<LightningArc> _arcs = new();
    private readonly List<LightningZap> _zaps = new();
    private readonly List<LightningRodSpot> _rods = new();
    private float _eyeIn;
    private float _callIn = ShamanStats.CallInterval;

    public RollingLightning(Random random) => _random = random;

    public IReadOnlyList<LightningBall> Balls => _balls;

    public IReadOnlyList<LightningArc> Arcs => _arcs;

    public IReadOnlyList<LightningZap> Zaps => _zaps;

    public IReadOnlyList<LightningRodSpot> Rods => _rods;

    /// <summary>Seconds until the next cast.</summary>
    public float CastIn { get; private set; } = FirstCast;

    /// <summary>Casts this run.</summary>
    public int Casts { get; private set; }

    /// <summary>
    /// One frame: a cast from <paramref name="hand"/> at <paramref name="target"/> when one is due (held while <paramref name="canCast"/> is false - a stun), the balls
    /// in flight, the charged trees, the Eye of the Storm around <paramref name="feet"/> while <paramref name="moving"/>, and Call Lightning. Every hit goes on
    /// <paramref name="hits"/>.
    /// </summary>
    public void Update(float deltaSeconds, Vector3D<float> hand, Vector3D<float> feet, Vector3D<float> target, bool moving, ShamanStats stats, EnemyField enemies,
        Func<float, float, float?> groundAt, ObstacleProbe obstacles, bool canCast, List<StormHit> hits)
    {
        CastIn -= deltaSeconds;
        if (!canCast)
        {
            CastIn = MathF.Max(CastIn, 0.15f);
        }
        else if (CastIn <= 0f)
        {
            CastIn = MathF.Max(0f, CastIn + stats.CastInterval);   // after a pause, no burst of casts to catch up
            Cast(hand, target, stats);
        }

        for (int i = _balls.Count - 1; i >= 0; i--)
        {
            if (!MoveBall(_balls[i], deltaSeconds, stats, enemies, groundAt, obstacles, hits))
            {
                _balls.RemoveAt(i);
            }
        }

        UpdateRods(deltaSeconds, stats, enemies, hits);
        UpdateEye(deltaSeconds, feet, moving, stats, enemies, hits);
        UpdateCall(deltaSeconds, feet, stats, enemies, hits);

        foreach (var arc in _arcs)
        {
            arc.Age += deltaSeconds;
        }

        _arcs.RemoveAll(a => a.Age >= ArcSeconds);
        foreach (var zap in _zaps)
        {
            zap.Age += deltaSeconds;
        }

        _zaps.RemoveAll(z => z.Age >= ZapSeconds);
    }

    /// <summary>A cast: its balls lobbed at <paramref name="target"/> (extra ones fanned around it), the first a supercell on every 5th cast with the major.</summary>
    public void Cast(Vector3D<float> hand, Vector3D<float> target, ShamanStats stats)
    {
        Casts++;
        bool supercell = stats.Tree.Supercell && Casts % ShamanStats.SupercellEvery == 0;
        int count = stats.Balls;
        for (int i = 0; i < count; i++)
        {
            float angle = (i - (count - 1) * 0.5f) * SpreadDegrees * MathF.PI / 180f;
            var offset = target - hand;
            float cos = MathF.Cos(angle), sin = MathF.Sin(angle);
            var turned = hand + new Vector3D<float>(offset.X * cos + offset.Z * sin, offset.Y, -offset.X * sin + offset.Z * cos);
            Lob(hand, turned, stats, supercell && i == 0);
        }
    }

    /// <summary>Lobs one ball from <paramref name="from"/> in an arc that comes down at <paramref name="to"/> (kept between the nearest and furthest it can throw).</summary>
    public LightningBall Lob(Vector3D<float> from, Vector3D<float> to, ShamanStats stats, bool supercell = false)
    {
        var flat = Geometry.FlatDirection(from, to, out float distance);
        if (flat == Vector3D<float>.Zero)
        {
            flat = Vector3D<float>.UnitZ;
        }

        distance = Math.Clamp(distance, ShamanStats.MinThrow, stats.ThrowRange);
        float speed = stats.ThrowSpeedNow;
        float time = distance / speed;
        float rise = (to.Y - from.Y + 0.5f * ShamanStats.Gravity * time * time) / time;
        return Launch(from, flat * speed + new Vector3D<float>(0f, rise, 0f), stats, supercell);
    }

    /// <summary>A ball dropped at <paramref name="feet"/>, rolling low along <paramref name="direction"/> (Surge Strike).</summary>
    public LightningBall Roll(Vector3D<float> feet, Vector3D<float> direction, ShamanStats stats)
    {
        var flat = new Vector3D<float>(direction.X, 0f, direction.Z);
        flat = flat.LengthSquared > 1e-6f ? Vector3D.Normalize(flat) : Vector3D<float>.UnitZ;
        return Launch(feet + new Vector3D<float>(0f, stats.BallRadius + 0.2f, 0f), flat * stats.ThrowSpeedNow + new Vector3D<float>(0f, SurgeLift, 0f), stats, false);
    }

    /// <summary>
    /// Lightning striking <paramref name="enemy"/> (harder on an elite or a boss). A kill with Living Current forks on to more enemies - unless this was itself one of
    /// Living Current's forks. True if it killed. Nothing happens to one already dead.
    /// </summary>
    public bool Shock(Enemy enemy, float amount, StormSource source, ShamanStats stats, EnemyField enemies, List<StormHit> hits, bool crit = false)
    {
        if (!enemy.IsAlive || amount <= 0f)
        {
            return false;
        }

        if (enemy.Kind.Tier != EnemyTier.Fodder)
        {
            amount *= stats.EliteMultiplier;
        }

        bool killed = enemies.Damage(enemy, amount);
        hits.Add(new StormHit(enemy, Chest(enemy), amount, killed, crit, source));
        if (killed && stats.Tree.LivingCurrent && source != StormSource.LivingFork)
        {
            Fork(enemy.Position + new Vector3D<float>(0f, 1f, 0f), ShamanStats.LivingCurrentForks, new HashSet<Enemy> { enemy }, chain: false, StormSource.LivingFork,
                stats, enemies, hits);
        }

        return killed;
    }

    /// <summary>
    /// A fork from <paramref name="origin"/>: lightning leaps to the <paramref name="count"/> nearest live enemies within fork range that aren't in
    /// <paramref name="spent"/>, each struck for the fork's damage (and paralysed, with Paralysis). With Chain Reaction (and <paramref name="chain"/>), each leaps on
    /// once more to one it hasn't struck.
    /// </summary>
    public void Fork(Vector3D<float> origin, int count, HashSet<Enemy> spent, bool chain, StormSource source, ShamanStats stats, EnemyField enemies, List<StormHit> hits)
    {
        var targets = enemies.Within(origin, stats.ForkRange)
            .Where(e => !spent.Contains(e))
            .OrderBy(e => Vector3D.DistanceSquared(e.Position, origin))
            .Take(count)
            .ToList();
        spent.UnionWith(targets);

        foreach (var enemy in targets)
        {
            _arcs.Add(new LightningArc { From = origin, To = Chest(enemy) });
            Shock(enemy, stats.ForkDamage, source, stats, enemies, hits);
            if (stats.Tree.Paralysis && enemy.IsAlive && enemy.Kind.Tier != EnemyTier.Boss)
            {
                enemy.Freeze(ShamanStats.ParalysisSeconds * (enemy.Kind.Tier == EnemyTier.Elite ? 0.5f : 1f));
            }

            if (chain && stats.Tree.ChainReaction)
            {
                Fork(Chest(enemy), 1, spent, chain: false, source, stats, enemies, hits);
            }
        }
    }

    /// <summary>Draws a bolt from <paramref name="from"/> to <paramref name="to"/> for a moment (Static Skin's shock, say).</summary>
    public void AddArc(Vector3D<float> from, Vector3D<float> to) => _arcs.Add(new LightningArc { From = from, To = to });

    /// <summary>Everything out of the world (a restart), and the clocks back to the start.</summary>
    public void Reset()
    {
        _balls.Clear();
        _arcs.Clear();
        _zaps.Clear();
        _rods.Clear();
        CastIn = FirstCast;
        Casts = 0;
        _eyeIn = 0f;
        _callIn = ShamanStats.CallInterval;
    }

    private LightningBall Launch(Vector3D<float> from, Vector3D<float> velocity, ShamanStats stats, bool supercell)
    {
        var ball = new LightningBall
        {
            Position = from,
            Velocity = velocity,
            Radius = stats.BallRadius * (supercell ? ShamanStats.SupercellSize : 1f),
            Damage = stats.BallDamage * (supercell ? ShamanStats.SupercellDamage : 1f),
            BouncesLeft = stats.Bounces + (supercell ? ShamanStats.SupercellBounces : 0),
            Life = stats.Lifetime,
            Supercell = supercell,
        };
        _balls.Add(ball);
        return ball;
    }

    /// <summary>
    /// Moves a ball one frame: falling, bouncing off the ground (with its zap, and a fork with Ground Current), glancing off obstacles (a fork, and a rod with Lightning
    /// Rod) and enemies (its strike and a fork). False once it has faded.
    /// </summary>
    private bool MoveBall(LightningBall ball, float deltaSeconds, ShamanStats stats, EnemyField enemies, Func<float, float, float?> groundAt, ObstacleProbe obstacles,
        List<StormHit> hits)
    {
        ball.Age += deltaSeconds;
        if (ball.Age >= ball.Life)
        {
            Fade(ball, stats, enemies, hits);
            return false;
        }

        foreach (var enemy in ball.Recent.Keys.ToList())
        {
            float left = ball.Recent[enemy] - deltaSeconds;
            if (left <= 0f)
            {
                ball.Recent.Remove(enemy);
            }
            else
            {
                ball.Recent[enemy] = left;
            }
        }

        ball.ObstacleCooldown = MathF.Max(0f, ball.ObstacleCooldown - deltaSeconds);
        ball.Velocity -= new Vector3D<float>(0f, ShamanStats.Gravity * deltaSeconds, 0f);
        ball.Position += ball.Velocity * deltaSeconds;

        // The ground: a bounce, with its zap - or, with no bounces left, the end.
        float ground = groundAt(ball.Position.X, ball.Position.Z) ?? float.NegativeInfinity;
        if (ball.Position.Y - ball.Radius <= ground && ball.Velocity.Y < 0f)
        {
            var landed = new Vector3D<float>(ball.Position.X, ground, ball.Position.Z);
            ball.Position = landed + new Vector3D<float>(0f, ball.Radius, 0f);
            if (ball.BouncesLeft <= 0)
            {
                Fade(ball, stats, enemies, hits);
                return false;
            }

            ball.BouncesLeft--;
            Zap(landed, stats.ZapRadius * (ball.Supercell ? ShamanStats.SupercellSize : 1f), stats.ZapDamage * (ball.Supercell ? ShamanStats.SupercellDamage : 1f),
                StormSource.Zap, stats, enemies, hits);
            if (stats.Tree.GroundCurrent)
            {
                Fork(landed + new Vector3D<float>(0f, 0.3f, 0f), stats.Forks, new HashSet<Enemy>(), chain: true, StormSource.Fork, stats, enemies, hits);
            }

            var v = ball.Velocity;
            ball.Velocity = new Vector3D<float>(v.X * GroundFriction, MathF.Max(-v.Y * ShamanStats.Restitution, ShamanStats.MinBounceSpeed), v.Z * GroundFriction);
        }

        // A tree, a rock, a solid prop: it glances off, and the lightning forks from where it touched.
        if (ball.ObstacleCooldown <= 0f && obstacles(ball.Position, ball.Radius, out var pushOut, out float depth))
        {
            ball.ObstacleCooldown = 0.25f;
            var touched = ball.Position - pushOut * ball.Radius;
            ball.Position += pushOut * depth;
            ball.Velocity = Glance(ball.Velocity, pushOut, 1f);
            Fork(touched, stats.Forks, new HashSet<Enemy>(), chain: true, StormSource.Fork, stats, enemies, hits);
            if (stats.Tree.LightningRod)
            {
                AddRod(new Vector3D<float>(touched.X, groundAt(touched.X, touched.Z) ?? touched.Y, touched.Z), stats.RodDuration);
            }
        }

        // An enemy: it is struck, the lightning forks from it, and the ball glances off.
        foreach (var enemy in enemies.Within(ball.Position, ball.Radius))
        {
            if (ball.Recent.ContainsKey(enemy) || ball.Position.Y < enemy.Position.Y - ball.Radius || ball.Position.Y > enemy.Position.Y + enemy.Kind.Height + ball.Radius)
            {
                continue;
            }

            ball.Recent[enemy] = EnemyRehit;
            bool crit = _random.NextDouble() < stats.CritChance;
            Shock(enemy, crit ? ball.Damage * stats.CritMultiplier : ball.Damage, StormSource.Ball, stats, enemies, hits, crit);
            Fork(Chest(enemy), stats.Forks, new HashSet<Enemy> { enemy }, chain: true, StormSource.Fork, stats, enemies, hits);
            var away = Geometry.FlatDirection(enemy.Position, ball.Position, out _);
            if (away != Vector3D<float>.Zero)
            {
                ball.Velocity = Glance(ball.Velocity, away, EnemyGlance);
            }
        }

        return true;
    }

    /// <summary>A ball fading: with the Thunder God's wrath, it goes out in a thunderclap.</summary>
    private void Fade(LightningBall ball, ShamanStats stats, EnemyField enemies, List<StormHit> hits)
    {
        if (stats.Tree.ThunderGod)
        {
            Zap(ball.Position, ShamanStats.ThunderGodRadius * stats.AreaScale, ball.Damage * ShamanStats.ThunderGodShare, StormSource.Burst, stats, enemies, hits);
        }
    }

    /// <summary>A ring of lightning at <paramref name="centre"/>: <paramref name="damage"/> to every enemy within <paramref name="radius"/>.</summary>
    private void Zap(Vector3D<float> centre, float radius, float damage, StormSource source, ShamanStats stats, EnemyField enemies, List<StormHit> hits)
    {
        _zaps.Add(new LightningZap { Centre = centre, Radius = radius });
        foreach (var enemy in enemies.Within(centre, radius))
        {
            Shock(enemy, damage, source, stats, enemies, hits);
        }
    }

    /// <summary>Charges the tree or rock at <paramref name="at"/> (Lightning Rod), or keeps one already charged there going.</summary>
    private void AddRod(Vector3D<float> at, float seconds)
    {
        var near = _rods.FirstOrDefault(r => Vector3D.DistanceSquared(r.Position, at) < 1.5f * 1.5f);
        if (near is not null)
        {
            near.Left = seconds;
            return;
        }

        if (_rods.Count >= MaxRods)
        {
            _rods.RemoveAt(0);
        }

        _rods.Add(new LightningRodSpot { Position = at, Left = seconds, TickIn = 0f });
    }

    private void UpdateRods(float deltaSeconds, ShamanStats stats, EnemyField enemies, List<StormHit> hits)
    {
        foreach (var rod in _rods)
        {
            rod.Left -= deltaSeconds;
            rod.TickIn -= deltaSeconds;
            if (rod.TickIn > 0f || rod.Left <= 0f)
            {
                continue;
            }

            rod.TickIn += ShamanStats.RodTick;
            var top = rod.Position + new Vector3D<float>(0f, 1.2f, 0f);
            foreach (var enemy in enemies.Within(rod.Position, ShamanStats.RodRadius * stats.AreaScale))
            {
                _arcs.Add(new LightningArc { From = top, To = Chest(enemy) });
                Shock(enemy, stats.RodDamage, StormSource.Rod, stats, enemies, hits);
            }
        }

        _rods.RemoveAll(r => r.Left <= 0f);
    }

    /// <summary>The Eye of the Storm: while the Shaman moves, a shock to everything near every so often.</summary>
    private void UpdateEye(float deltaSeconds, Vector3D<float> feet, bool moving, ShamanStats stats, EnemyField enemies, List<StormHit> hits)
    {
        if (!stats.Tree.EyeOfTheStorm || !moving)
        {
            _eyeIn = MathF.Max(0f, _eyeIn - deltaSeconds);
            return;
        }

        _eyeIn -= deltaSeconds;
        if (_eyeIn > 0f)
        {
            return;
        }

        _eyeIn = MathF.Max(0f, _eyeIn + ShamanStats.EyeTick);
        Zap(feet, ShamanStats.EyeRadius * stats.AreaScale, stats.BallDamage * ShamanStats.EyeShare, StormSource.Eye, stats, enemies, hits);
    }

    /// <summary>Call Lightning: every so often, strikes from the sky on the nearest enemies.</summary>
    private void UpdateCall(float deltaSeconds, Vector3D<float> feet, ShamanStats stats, EnemyField enemies, List<StormHit> hits)
    {
        if (!stats.Tree.CallLightning)
        {
            _callIn = ShamanStats.CallInterval;
            return;
        }

        _callIn -= deltaSeconds;
        if (_callIn > 0f)
        {
            return;
        }

        _callIn += ShamanStats.CallInterval;
        foreach (var enemy in enemies.Within(feet, ShamanStats.CallRange).OrderBy(e => Vector3D.DistanceSquared(e.Position, feet)).Take(ShamanStats.CallStrikes).ToList())
        {
            _arcs.Add(new LightningArc { From = enemy.Position + new Vector3D<float>(0f, 14f, 0f), To = enemy.Position });
            Shock(enemy, stats.BallDamage * ShamanStats.CallShare, StormSource.Call, stats, enemies, hits);
        }
    }

    /// <summary>A velocity glancing off a surface whose flat way out is <paramref name="normal"/>: what heads into it turns back out, keeping <paramref name="keep"/> of the speed across the ground.</summary>
    private static Vector3D<float> Glance(Vector3D<float> velocity, Vector3D<float> normal, float keep)
    {
        var flat = new Vector3D<float>(velocity.X, 0f, velocity.Z);
        float into = Vector3D.Dot(flat, normal);
        if (into < 0f)
        {
            flat -= normal * (2f * into);
        }

        return new Vector3D<float>(flat.X * keep, velocity.Y, flat.Z * keep);
    }

    private static Vector3D<float> Chest(Enemy enemy) => enemy.Position + new Vector3D<float>(0f, enemy.Kind.Height * 0.6f, 0f);
}
