using ArenaMaster.Game.Combat;
using Silk.NET.Maths;

namespace ArenaMaster.Game.Shaman;

/// <summary>A stone (Earth Alignment): thrown like the ball of lightning, falling, bouncing along the ground and off enemies, until its bounces or its life run out.</summary>
internal sealed class ThrownStone
{
    public Vector3D<float> Position { get; set; }

    public Vector3D<float> Velocity { get; set; }

    public float Radius { get; init; }

    /// <summary>Its damage as thrown; <see cref="Damage"/> is that, made heavier by each bounce with Gathering Weight.</summary>
    public float BaseDamage { get; init; }

    /// <summary>What Gathering Weight has made of it so far: 1, and more with each bounce.</summary>
    public float Weight { get; set; } = 1f;

    public float Damage => BaseDamage * Weight;

    /// <summary>Bounces left: it comes to rest when it strikes the ground, or an enemy, with none left.</summary>
    public int BouncesLeft { get; set; }

    /// <summary>Enemies it may still bounce off without spending a bounce (an item's chains).</summary>
    public int FreeRebounds { get; set; }

    public float Life { get; init; }

    public float Age { get; set; }

    /// <summary>The enemies it has just struck, and how long until it may strike each again, so bouncing off one strikes it once.</summary>
    public Dictionary<Enemy, float> Recent { get; } = new();

    /// <summary>With Boulder: every enemy it has crushed on this throw (each is hit only once).</summary>
    public HashSet<Enemy> Crushed { get; } = new();

    /// <summary>Seconds until it can glance off another obstacle (it is pushed clear of one, but a corner could catch it twice).</summary>
    public float ObstacleCooldown { get; set; }

    /// <summary>Where it last came down (Tectonic Rift splits the ground from there to where it comes down next), or null before it has.</summary>
    public Vector3D<float>? LastLanding { get; set; }
}

/// <summary>A quake's ring spreading over the ground, for the view: a bounce's, or Upheaval's.</summary>
internal sealed class StoneQuake
{
    public Vector3D<float> Centre { get; init; }

    public float Radius { get; init; }

    public float Age { get; set; }
}

/// <summary>An Aftershock crack: a split in the ground where the stone bounced, bursting once its time is up.</summary>
internal sealed class StoneCrack
{
    public Vector3D<float> Position { get; init; }

    public float Radius { get; init; }

    public float Damage { get; init; }

    /// <summary>Seconds until it bursts.</summary>
    public float Left { get; set; }

    /// <summary>Which way it runs, for the view.</summary>
    public float Yaw { get; init; }
}

/// <summary>Rock bursting up from the ground, for the view: a crack's burst, or a shattered stone's rubble.</summary>
internal sealed class StoneBurst
{
    public Vector3D<float> Centre { get; init; }

    public float Radius { get; init; }

    public float Age { get; set; }

    /// <summary>A shattered stone (Shatter) rather than a crack bursting.</summary>
    public bool Rubble { get; init; }
}

/// <summary>A stone totem a surge planted (Earthen Totem): it stands for a while, and the enemy field's lure at its foot draws the enemies near it.</summary>
internal sealed class EarthTotem
{
    public Vector3D<float> Position { get; init; }

    /// <summary>How far it draws enemies from.</summary>
    public float Reach { get; init; }

    public float Left { get; set; }

    public float Age { get; set; }
}

/// <summary>A rift a stone split in the ground as it went (Tectonic Rift): from one landing to the next, biting what stands in it.</summary>
internal sealed class StoneRift
{
    public Vector3D<float> From { get; init; }

    public Vector3D<float> To { get; init; }

    public float Left { get; set; }

    /// <summary>How long it has been open, for the view.</summary>
    public float Age { get; set; }

    public float TickIn { get; set; }

    /// <summary>What each bite does.</summary>
    public float Bite { get; init; }
}

/// <summary>An enemy being shoved along the ground by a stone or a heave, over a moment rather than all at once.</summary>
internal sealed class StoneShove
{
    public required Enemy Enemy { get; init; }

    public Vector3D<float> Velocity { get; init; }

    public float Left { get; set; }
}

internal enum EarthSource
{
    Stone,
    Quake,
    Crack,
    Shatter,
    Rift,
    Upheaval,
}

internal readonly record struct EarthHit(Enemy Enemy, Vector3D<float> Position, float Damage, bool Killed, bool Crit, EarthSource Source);

/// <summary>
/// The Shaman's attack with Earth Alignment active: the same throw as Rolling Lightning (lobbed at the nearest enemy, led, bouncing along the ground), but a stone.
/// It strikes the first enemy it meets, knocking it back, and bounces off it (spending a bounce); every bounce, off the ground or an enemy, shakes the ground in a
/// small quake. And Earth Alignment's majors: Boulder, Aftershock, Stoneskin, Tremor, Gathering Weight, Rumbling Earth, the Earthen Totem, Shatter, Upheaval,
/// Avalanche and Tectonic Rift (Walking Mountain is in Stoneskin's numbers). Pure - no engine calls; <see cref="EarthView"/> draws it.
/// </summary>
internal sealed class RollingStone
{
    /// <summary>How long a quake's ring and a burst last, for the view.</summary>
    public const float QuakeSeconds = 0.35f;
    public const float BurstSeconds = 0.45f;

    /// <summary>A stone won't strike the same enemy again for this long.</summary>
    public const float EnemyRehit = 0.5f;

    /// <summary>A stone keeps this much of its speed across the ground bouncing off an enemy, and leaves it at least this fast upward.</summary>
    public const float EnemyRebound = 0.7f;
    public const float ReboundLift = 3f;

    /// <summary>A shove stops at a rise steeper than this over one of its steps (a step is at most <see cref="ShoveStep"/> long).</summary>
    public const float ShoveClimb = 0.5f;
    public const float ShoveStep = 0.25f;

    /// <summary>At most this many totems stand at once; a new one past it takes the oldest's place.</summary>
    public const int MaxTotems = 4;

    private readonly Random _random;
    private readonly List<ThrownStone> _stones = new();
    private readonly List<StoneQuake> _quakes = new();
    private readonly List<StoneCrack> _cracks = new();
    private readonly List<StoneBurst> _bursts = new();
    private readonly List<EarthTotem> _totems = new();
    private readonly List<StoneRift> _rifts = new();
    private readonly List<StoneShove> _shoves = new();
    private float _upheavalIn;

    public RollingStone(Random random) => _random = random;

    public IReadOnlyList<ThrownStone> Stones => _stones;

    public IReadOnlyList<StoneQuake> Quakes => _quakes;

    public IReadOnlyList<StoneCrack> Cracks => _cracks;

    public IReadOnlyList<StoneBurst> Bursts => _bursts;

    public IReadOnlyList<EarthTotem> Totems => _totems;

    public IReadOnlyList<StoneRift> Rifts => _rifts;

    /// <summary>Seconds until the next cast.</summary>
    public float CastIn { get; private set; } = RollingLightning.FirstCast;

    /// <summary>Casts this run.</summary>
    public int Casts { get; private set; }

    /// <summary>How much Stoneskin takes off the damage the Shaman takes now (0 to its most).</summary>
    public float Stoneskin { get; private set; }

    /// <summary>
    /// One frame: a cast from <paramref name="hand"/> at <paramref name="target"/> when one is due (held while <paramref name="canCast"/> is false - a stun; with no
    /// target, it waits, ready), the stones in flight, the cracks, rifts and shoves, the totems, and Stoneskin (built standing still, worn away while
    /// <paramref name="moving"/>). Every hit goes on <paramref name="hits"/>.
    /// </summary>
    public void Update(float deltaSeconds, Vector3D<float> hand, Vector3D<float>? target, bool moving, ShamanStats stats, EnemyField enemies,
        Func<float, float, float?> groundAt, ObstacleProbe obstacles, bool canCast, List<EarthHit> hits)
    {
        CastIn -= deltaSeconds;
        if (!canCast)
        {
            CastIn = MathF.Max(CastIn, 0.15f);
        }
        else if (CastIn <= 0f)
        {
            if (target is { } spot)
            {
                CastIn = MathF.Max(0f, CastIn + stats.CastInterval);   // after a pause, no burst of casts to catch up
                Cast(hand, spot, stats);
            }
            else
            {
                CastIn = 0f;   // ready, and waiting for something to throw at
            }
        }

        for (int i = _stones.Count - 1; i >= 0; i--)
        {
            if (!MoveStone(_stones[i], deltaSeconds, stats, enemies, groundAt, obstacles, hits))
            {
                _stones.RemoveAt(i);
            }
        }

        UpdateCracks(deltaSeconds, stats, enemies, hits);
        UpdateRifts(deltaSeconds, stats, enemies, hits);
        UpdateShoves(deltaSeconds, groundAt);
        UpdateStoneskin(deltaSeconds, moving, stats);
        _upheavalIn = MathF.Max(0f, _upheavalIn - deltaSeconds);

        foreach (var totem in _totems)
        {
            totem.Left -= deltaSeconds;
            totem.Age += deltaSeconds;
        }

        _totems.RemoveAll(t => t.Left <= 0f);
        foreach (var quake in _quakes)
        {
            quake.Age += deltaSeconds;
        }

        _quakes.RemoveAll(q => q.Age >= QuakeSeconds);
        foreach (var burst in _bursts)
        {
            burst.Age += deltaSeconds;
        }

        _bursts.RemoveAll(b => b.Age >= BurstSeconds);
    }

    /// <summary>A cast: its stones thrown at <paramref name="target"/> (extra ones fanned around it); with Avalanche, every 6th cast a rockslide of four more.</summary>
    public void Cast(Vector3D<float> hand, Vector3D<float> target, ShamanStats stats)
    {
        Casts++;
        bool rockslide = stats.Earth.Avalanche && Casts % ShamanStats.AvalancheEvery == 0;
        int count = stats.Balls + (rockslide ? ShamanStats.AvalancheExtra : 0);
        float spread = rockslide ? ShamanStats.AvalancheSpreadDegrees : RollingLightning.SpreadDegrees;
        for (int i = 0; i < count; i++)
        {
            float angle = (i - (count - 1) * 0.5f) * spread * MathF.PI / 180f;
            var offset = target - hand;
            float cos = MathF.Cos(angle), sin = MathF.Sin(angle);
            var turned = hand + new Vector3D<float>(offset.X * cos + offset.Z * sin, offset.Y, -offset.X * sin + offset.Z * cos);
            Lob(hand, turned, stats);
        }
    }

    /// <summary>Throws one stone from <paramref name="from"/> so it comes down at <paramref name="to"/>: the very same throw as the ball of lightning's.</summary>
    public ThrownStone Lob(Vector3D<float> from, Vector3D<float> to, ShamanStats stats)
    {
        var (flat, distance, rise, time) = RollingLightning.Arc(from, to, stats, stats.StoneRadius);
        var stone = new ThrownStone
        {
            Position = from,
            Velocity = flat * (distance / time) + new Vector3D<float>(0f, rise, 0f),
            Radius = stats.StoneRadius,
            BaseDamage = stats.StoneDamage,
            BouncesLeft = stats.Bounces,
            FreeRebounds = stats.FreeRebounds,
            Life = stats.Lifetime,
        };
        _stones.Add(stone);
        return stone;
    }

    /// <summary>Plants a totem at <paramref name="at"/> (Earthen Totem, where a surge began): it stands a while, and a lure at its foot draws the enemies near it.</summary>
    public EarthTotem PlantTotem(Vector3D<float> at, ShamanStats stats, EnemyField enemies)
    {
        if (_totems.Count >= MaxTotems)
        {
            _totems.RemoveAt(0);
        }

        var totem = new EarthTotem { Position = at, Reach = stats.TotemReach, Left = stats.TotemSeconds };
        _totems.Add(totem);
        enemies.AddLure(at, stats.TotemReach, stats.TotemSeconds);
        return totem;
    }

    /// <summary>
    /// Upheaval, when a blow lands on the Shaman standing at <paramref name="feet"/>: the ground heaves round it, a quake that throws every enemy in it back - at most
    /// once a second. True if it heaved.
    /// </summary>
    public bool Upheave(Vector3D<float> feet, ShamanStats stats, EnemyField enemies, List<EarthHit> hits)
    {
        if (!stats.Earth.Upheaval || _upheavalIn > 0f)
        {
            return false;
        }

        _upheavalIn = ShamanStats.UpheavalCooldown;
        var struck = Quake(feet, ShamanStats.UpheavalRadius * stats.AreaScale, stats.StoneDamage * ShamanStats.UpheavalShare, EarthSource.Upheaval, stats, enemies, hits);
        foreach (var enemy in struck)
        {
            var away = Geometry.FlatDirection(feet, enemy.Position, out _);
            Shove(enemy, away == Vector3D<float>.Zero ? Vector3D<float>.UnitZ : away, stats.Knockback);
        }

        return true;
    }

    /// <summary>
    /// A stone striking <paramref name="enemy"/> (harder on an elite or a boss), or a quake, crack or rift hurting it. True if it killed. Nothing happens to one
    /// already dead.
    /// </summary>
    public bool Strike(Enemy enemy, float amount, EarthSource source, ShamanStats stats, EnemyField enemies, List<EarthHit> hits, bool crit = false)
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
        hits.Add(new EarthHit(enemy, Chest(enemy), amount, killed, crit, source));
        return killed;
    }

    /// <summary>
    /// Shoves <paramref name="enemy"/> <paramref name="distance"/> metres along <paramref name="way"/> over a moment: half as far for an elite, not at all for a boss
    /// or a crate. It never goes where there is no ground to stand on (the cave's rock) or up a sudden rise: it stops short.
    /// </summary>
    public void Shove(Enemy enemy, Vector3D<float> way, float distance)
    {
        if (!enemy.IsAlive || enemy.Kind.Tier == EnemyTier.Boss || enemy.Kind.IsProp || distance <= 0f)
        {
            return;
        }

        if (enemy.Kind.Tier == EnemyTier.Elite)
        {
            distance *= 0.5f;
        }

        var flat = new Vector3D<float>(way.X, 0f, way.Z);
        if (flat.LengthSquared < 1e-6f)
        {
            return;
        }

        _shoves.RemoveAll(s => s.Enemy == enemy);
        _shoves.Add(new StoneShove
        {
            Enemy = enemy,
            Velocity = Vector3D.Normalize(flat) * (distance / ShamanStats.KnockbackSeconds),
            Left = ShamanStats.KnockbackSeconds,
        });
    }

    /// <summary>Everything out of the world (a restart), and the clocks back to the start.</summary>
    public void Reset()
    {
        _stones.Clear();
        _quakes.Clear();
        _cracks.Clear();
        _bursts.Clear();
        _totems.Clear();
        _rifts.Clear();
        _shoves.Clear();
        CastIn = RollingLightning.FirstCast;
        Casts = 0;
        Stoneskin = 0f;
        _upheavalIn = 0f;
    }

    /// <summary>
    /// Moves a stone one frame: falling, bouncing off the ground (a quake, and a crack and a rift with their majors), glancing off obstacles, and striking enemies
    /// (bouncing off the first, or crushing through all with Boulder). False once it has come to rest.
    /// </summary>
    private bool MoveStone(ThrownStone stone, float deltaSeconds, ShamanStats stats, EnemyField enemies, Func<float, float, float?> groundAt, ObstacleProbe obstacles,
        List<EarthHit> hits)
    {
        stone.Age += deltaSeconds;
        if (stone.Age >= stone.Life)
        {
            Settle(stone, stats, enemies, groundAt, hits);
            return false;
        }

        foreach (var enemy in stone.Recent.Keys.ToList())
        {
            float left = stone.Recent[enemy] - deltaSeconds;
            if (left <= 0f)
            {
                stone.Recent.Remove(enemy);
            }
            else
            {
                stone.Recent[enemy] = left;
            }
        }

        stone.ObstacleCooldown = MathF.Max(0f, stone.ObstacleCooldown - deltaSeconds);
        stone.Velocity -= new Vector3D<float>(0f, ShamanStats.Gravity * deltaSeconds, 0f);
        stone.Position += stone.Velocity * deltaSeconds;

        // The ground: a bounce, with its quake - or, with no bounces left, it comes to rest.
        float ground = groundAt(stone.Position.X, stone.Position.Z) ?? float.NegativeInfinity;
        if (stone.Position.Y - stone.Radius <= ground && stone.Velocity.Y < 0f)
        {
            var landed = new Vector3D<float>(stone.Position.X, ground, stone.Position.Z);
            stone.Position = landed + new Vector3D<float>(0f, stone.Radius, 0f);
            Landed(stone, landed, stats);
            if (stone.BouncesLeft <= 0)
            {
                Settle(stone, stats, enemies, groundAt, hits);
                return false;
            }

            stone.BouncesLeft--;
            Bounce(stone, landed, stats, enemies, hits);
            var v = stone.Velocity;
            stone.Velocity = new Vector3D<float>(v.X * RollingLightning.GroundFriction, MathF.Max(-v.Y * ShamanStats.Restitution, ShamanStats.MinBounceSpeed),
                v.Z * RollingLightning.GroundFriction);
        }

        // A tree, a rock, a solid prop: it glances off.
        if (stone.ObstacleCooldown <= 0f && obstacles(stone.Position, stone.Radius, out var pushOut, out float depth))
        {
            stone.ObstacleCooldown = 0.25f;
            stone.Position += pushOut * depth;
            stone.Velocity = Glance(stone.Velocity, pushOut, 1f);
        }

        return stats.Earth.Boulder ? Crush(stone, stats, enemies, hits) : StrikeFirst(stone, stats, enemies, groundAt, hits);
    }

    /// <summary>Whether the stone at its height touches <paramref name="enemy"/>'s body.</summary>
    private static bool Touches(ThrownStone stone, Enemy enemy) =>
        stone.Position.Y >= enemy.Position.Y - stone.Radius && stone.Position.Y <= enemy.Position.Y + enemy.Kind.Height + stone.Radius;

    /// <summary>
    /// The stone striking the nearest enemy it touches (not one it has just struck): its damage, a knock-back, and a bounce off it with its quake - one of its bounces,
    /// unless an item's chain pays for it. With none left it drops where it is. False if it came to rest.
    /// </summary>
    private bool StrikeFirst(ThrownStone stone, ShamanStats stats, EnemyField enemies, Func<float, float, float?> groundAt, List<EarthHit> hits)
    {
        Enemy? first = null;
        float nearest = float.MaxValue;
        foreach (var enemy in enemies.Within(stone.Position, stone.Radius))
        {
            if (stone.Recent.ContainsKey(enemy) || !Touches(stone, enemy))
            {
                continue;
            }

            Geometry.FlatDirection(stone.Position, enemy.Position, out float distance);
            if (distance < nearest)
            {
                first = enemy;
                nearest = distance;
            }
        }

        if (first is null)
        {
            return true;
        }

        stone.Recent[first] = EnemyRehit;
        bool crit = _random.NextDouble() < stats.CritChance;
        Strike(first, crit ? stone.Damage * stats.CritMultiplier : stone.Damage, EarthSource.Stone, stats, enemies, hits, crit);
        var away = Geometry.FlatDirection(stone.Position, first.Position, out _);
        if (away == Vector3D<float>.Zero)
        {
            away = Geometry.FlatDirection(Vector3D<float>.Zero, stone.Velocity, out _);
        }

        Shove(first, away, stats.Knockback);

        var below = new Vector3D<float>(stone.Position.X, groundAt(stone.Position.X, stone.Position.Z) ?? first.Position.Y, stone.Position.Z);
        if (stone.FreeRebounds > 0)
        {
            stone.FreeRebounds--;
        }
        else if (stone.BouncesLeft > 0)
        {
            stone.BouncesLeft--;
        }
        else
        {
            Settle(stone, stats, enemies, groundAt, hits);
            return false;
        }

        Bounce(stone, below, stats, enemies, hits);
        if (away != Vector3D<float>.Zero)
        {
            // Back out of it, and off the way it came.
            float clear = first.Kind.Radius + stone.Radius + 0.05f;
            stone.Position = new Vector3D<float>(first.Position.X - away.X * clear, stone.Position.Y, first.Position.Z - away.Z * clear);
            var turned = Glance(stone.Velocity, -away, EnemyRebound);
            stone.Velocity = new Vector3D<float>(turned.X, MathF.Max(turned.Y, ReboundLift), turned.Z);
        }

        return true;
    }

    /// <summary>Boulder: the stone rolls on through every enemy it touches, hitting each once a throw and knocking it aside.</summary>
    private bool Crush(ThrownStone stone, ShamanStats stats, EnemyField enemies, List<EarthHit> hits)
    {
        var forward = Geometry.FlatDirection(Vector3D<float>.Zero, stone.Velocity, out _);
        foreach (var enemy in enemies.Within(stone.Position, stone.Radius))
        {
            if (stone.Crushed.Contains(enemy) || !Touches(stone, enemy))
            {
                continue;
            }

            stone.Crushed.Add(enemy);
            bool crit = _random.NextDouble() < stats.CritChance;
            Strike(enemy, crit ? stone.Damage * stats.CritMultiplier : stone.Damage, EarthSource.Stone, stats, enemies, hits, crit);

            // Aside: square to the way it rolls, to whichever side the enemy is on, and a little on.
            var side = new Vector3D<float>(forward.Z, 0f, -forward.X);
            var off = enemy.Position - stone.Position;
            if (off.X * side.X + off.Z * side.Z < 0f)
            {
                side = -side;
            }

            Shove(enemy, side + forward * 0.35f, stats.Knockback);
        }

        return true;
    }

    /// <summary>A bounce at <paramref name="at"/> (off the ground or an enemy): its quake, an Aftershock crack, and Gathering Weight's heavier stone after.</summary>
    private void Bounce(ThrownStone stone, Vector3D<float> at, ShamanStats stats, EnemyField enemies, List<EarthHit> hits)
    {
        Quake(at, stats.QuakeRadius, stone.Damage * stats.QuakeShare, EarthSource.Quake, stats, enemies, hits);
        if (stats.Earth.Aftershock)
        {
            _cracks.Add(new StoneCrack
            {
                Position = at,
                Radius = stats.CrackRadius,
                Damage = stone.Damage * stats.CrackDamageShare,
                Left = ShamanStats.CrackDelay,
                Yaw = (float)_random.NextDouble() * MathF.Tau,
            });
        }

        if (stats.Earth.GatheringWeight)
        {
            stone.Weight += ShamanStats.WeightPerBounce;
        }
    }

    /// <summary>The stone came down on the ground at <paramref name="at"/>: with Tectonic Rift, the ground splits from where it last came down.</summary>
    private void Landed(ThrownStone stone, Vector3D<float> at, ShamanStats stats)
    {
        if (stats.Earth.TectonicRift && stone.LastLanding is { } from && Vector3D.DistanceSquared(from, at) > 0.3f * 0.3f)
        {
            _rifts.Add(new StoneRift
            {
                From = from,
                To = at,
                Left = stats.RiftSeconds,
                TickIn = ShamanStats.RiftTick,
                Bite = stone.Damage * stats.RiftDamageShare * ShamanStats.RiftTick,
            });
        }

        stone.LastLanding = at;
    }

    /// <summary>A stone coming to rest: with Shatter, it bursts into rubble.</summary>
    private void Settle(ThrownStone stone, ShamanStats stats, EnemyField enemies, Func<float, float, float?> groundAt, List<EarthHit> hits)
    {
        if (!stats.Earth.Shatter)
        {
            return;
        }

        var at = new Vector3D<float>(stone.Position.X, groundAt(stone.Position.X, stone.Position.Z) ?? stone.Position.Y - stone.Radius, stone.Position.Z);
        float radius = ShamanStats.ShatterRadius * stats.AreaScale;
        _bursts.Add(new StoneBurst { Centre = at, Radius = radius, Rubble = true });
        foreach (var enemy in enemies.Within(at, radius))
        {
            Strike(enemy, stone.Damage * ShamanStats.ShatterShare, EarthSource.Shatter, stats, enemies, hits);
        }
    }

    /// <summary>
    /// A quake at <paramref name="centre"/>: <paramref name="damage"/> to every enemy within <paramref name="radius"/>, and with Tremor a stagger (half as long for an
    /// elite, none for a boss). Returns the enemies it hit.
    /// </summary>
    private List<Enemy> Quake(Vector3D<float> centre, float radius, float damage, EarthSource source, ShamanStats stats, EnemyField enemies, List<EarthHit> hits)
    {
        _quakes.Add(new StoneQuake { Centre = centre, Radius = radius });
        var struck = enemies.Within(centre, radius);
        foreach (var enemy in struck)
        {
            Strike(enemy, damage, source, stats, enemies, hits);
            if (stats.Earth.Tremor && enemy.IsAlive && enemy.Kind.Tier != EnemyTier.Boss && !enemy.Kind.IsProp)
            {
                enemy.Stagger(ShamanStats.TremorSeconds * (enemy.Kind.Tier == EnemyTier.Elite ? 0.5f : 1f));
            }
        }

        return struck;
    }

    /// <summary>The Aftershock cracks: each bursts once its time is up, hurting everything within its reach.</summary>
    private void UpdateCracks(float deltaSeconds, ShamanStats stats, EnemyField enemies, List<EarthHit> hits)
    {
        foreach (var crack in _cracks)
        {
            crack.Left -= deltaSeconds;
            if (crack.Left > 0f)
            {
                continue;
            }

            _bursts.Add(new StoneBurst { Centre = crack.Position, Radius = crack.Radius });
            foreach (var enemy in enemies.Within(crack.Position, crack.Radius))
            {
                Strike(enemy, crack.Damage, EarthSource.Crack, stats, enemies, hits);
            }
        }

        _cracks.RemoveAll(c => c.Left <= 0f);
    }

    /// <summary>The rifts: each bites everything standing in it every so often, until it closes.</summary>
    private void UpdateRifts(float deltaSeconds, ShamanStats stats, EnemyField enemies, List<EarthHit> hits)
    {
        foreach (var rift in _rifts)
        {
            rift.Left -= deltaSeconds;
            rift.Age += deltaSeconds;
            rift.TickIn -= deltaSeconds;
            if (rift.TickIn > 0f || rift.Left <= 0f)
            {
                continue;
            }

            rift.TickIn += ShamanStats.RiftTick;
            var middle = (rift.From + rift.To) * 0.5f;
            float half = Vector3D.Distance(rift.From, rift.To) * 0.5f;
            foreach (var enemy in enemies.Within(middle, half + ShamanStats.RiftHalfWidth))
            {
                if (FlatDistanceToSegment(enemy.Position, rift.From, rift.To) <= ShamanStats.RiftHalfWidth + enemy.Kind.Radius)
                {
                    Strike(enemy, rift.Bite, EarthSource.Rift, stats, enemies, hits);
                }
            }
        }

        _rifts.RemoveAll(r => r.Left <= 0f);
    }

    /// <summary>The shoves under way: each moves its enemy on over the ground, a short step at a time, stopping short of rock or a sudden rise.</summary>
    private void UpdateShoves(float deltaSeconds, Func<float, float, float?> groundAt)
    {
        foreach (var shove in _shoves)
        {
            var enemy = shove.Enemy;
            float time = MathF.Min(deltaSeconds, shove.Left);
            shove.Left -= deltaSeconds;
            if (!enemy.IsAlive || time <= 0f)
            {
                continue;
            }

            var move = shove.Velocity * time;
            int steps = Math.Max(1, (int)MathF.Ceiling(move.Length / ShoveStep));
            var step = move / steps;
            for (int i = 0; i < steps; i++)
            {
                var next = enemy.Position + step;
                if (groundAt(next.X, next.Z) is not { } ground || ground - enemy.Position.Y > ShoveClimb)
                {
                    shove.Left = 0f;   // rock, the map's edge or a wall of earth: it stops here
                    break;
                }

                enemy.Position = new Vector3D<float>(next.X, ground, next.Z);
            }
        }

        _shoves.RemoveAll(s => s.Left <= 0f || !s.Enemy.IsAlive);
    }

    /// <summary>Stoneskin: built up standing still, worn away moving (not with Walking Mountain), nothing without it.</summary>
    private void UpdateStoneskin(float deltaSeconds, bool moving, ShamanStats stats)
    {
        if (!stats.HasStoneskin)
        {
            Stoneskin = 0f;
        }
        else if (!moving)
        {
            Stoneskin = MathF.Min(stats.StoneskinMax, Stoneskin + stats.StoneskinRate * deltaSeconds);
        }
        else if (!stats.Earth.WalkingMountain)
        {
            Stoneskin = MathF.Max(0f, Stoneskin - ShamanStats.StoneskinLoss * deltaSeconds);
        }
    }

    /// <summary>How far <paramref name="point"/> is from the segment <paramref name="a"/>-<paramref name="b"/>, across the ground.</summary>
    private static float FlatDistanceToSegment(Vector3D<float> point, Vector3D<float> a, Vector3D<float> b)
    {
        float abx = b.X - a.X, abz = b.Z - a.Z;
        float length = abx * abx + abz * abz;
        float t = length > 1e-6f ? Math.Clamp(((point.X - a.X) * abx + (point.Z - a.Z) * abz) / length, 0f, 1f) : 0f;
        float dx = point.X - (a.X + abx * t), dz = point.Z - (a.Z + abz * t);
        return MathF.Sqrt(dx * dx + dz * dz);
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
