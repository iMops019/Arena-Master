using ArenaMaster.Game.Combat;
using Silk.NET.Maths;

namespace ArenaMaster.Game.Priest;

/// <summary>
/// One of the Priest's raised dead (Raise Dead): a skeleton that walks to an enemy near the Priest and claws it, wearing down while enemies touch it, until its
/// health or its time runs out. A Bone Colossus is one too, <see cref="IsColossus"/>: bigger, tougher, harder-hitting, and hitting all round it.
/// </summary>
internal sealed class Servant
{
    public Vector3D<float> Position { get; set; }

    /// <summary>Which way it faces (0 is +Z).</summary>
    public float Yaw { get; set; }

    public float Health { get; set; }

    public float MaxHealth { get; init; }

    /// <summary>Seconds since it rose, and how long it lasts before it crumbles.</summary>
    public float Age { get; set; }

    public float Life { get; init; }

    /// <summary>Seconds until its next claw lands, while it is at an enemy, and the seconds between its claws.</summary>
    public float ClawIn { get; set; }

    public float ClawInterval { get; set; } = PriestStats.BaseClawInterval;

    /// <summary>The enemy it is going for, or null.</summary>
    public Enemy? Target { get; set; }

    /// <summary>Whether it is at its enemy, clawing (the view plays its attack), this frame.</summary>
    public bool Clawing { get; set; }

    public bool IsColossus { get; init; }

    /// <summary>Where round the Priest it stands when there is nothing to fight (an angle).</summary>
    public float Slot { get; init; }

    /// <summary>Seconds since it fell, or null while it stands.</summary>
    public float? DeadFor { get; set; }

    public bool IsAlive => DeadFor is null;

    /// <summary>How fat it is (for reaching and being touched).</summary>
    public float Radius => IsColossus ? RaisedDead.ServantRadius * PriestStats.ColossusSize : RaisedDead.ServantRadius;
}

/// <summary>A servant bursting as it falls (Corpse Burst), for the view: where, how far it reaches, and how long ago.</summary>
internal sealed class CorpseBurstShow
{
    public Vector3D<float> Centre { get; init; }

    public float Radius { get; init; }

    public float Age { get; set; }
}

/// <summary>A servant or a colossus rising out of the ground, for the view: where, how big, and how long ago.</summary>
internal sealed class RisingShow
{
    public Vector3D<float> Centre { get; init; }

    public float Size { get; init; }

    public float Age { get; set; }
}

/// <summary>
/// The Grave Calling tree's raised dead: Raise Dead (every 8th kill rises again as a servant, up to 4 at once), what the servants do each frame (go for the
/// nearest enemy within <see cref="PriestStats.ServantLeash"/> of the Priest, claw it, wear down while enemies touch them - the enemies never go for them - and
/// crumble in time), Corpse Burst, Death's Command, Army of the Dead and the Bone Colossus. Pure - no engine calls; <see cref="GraveView"/> draws it.
/// </summary>
internal sealed class RaisedDead
{
    /// <summary>How fat a servant is.</summary>
    public const float ServantRadius = 0.4f;

    /// <summary>How far past touching an enemy a claw reaches.</summary>
    public const float ClawReach = 0.5f;

    /// <summary>How close to its place by the Priest a servant with nothing to fight counts as there, and how far out that place is.</summary>
    public const float HomeSlack = 0.8f;
    public const float HomeDistance = 2.4f;

    /// <summary>How long a fallen servant takes to fall apart and go, and how long a burst and a rising show.</summary>
    public const float DeathSeconds = 1f;
    public const float BurstSeconds = 0.4f;
    public const float RiseSeconds = 0.6f;

    private readonly List<Servant> _servants = new();
    private readonly List<Vector3D<float>> _due = new();
    private readonly List<CorpseBurstShow> _bursts = new();
    private readonly List<RisingShow> _risings = new();
    private int _slots;

    /// <summary>Every servant, standing or falling (a fallen one stays <see cref="DeathSeconds"/> to fall apart).</summary>
    public IReadOnlyList<Servant> Servants => _servants;

    public IReadOnlyList<CorpseBurstShow> Bursts => _bursts;

    public IReadOnlyList<RisingShow> Risings => _risings;

    /// <summary>Kills counted toward the next raise.</summary>
    public int KillsToward { get; private set; }

    /// <summary>The standing servants, the colossus not among them.</summary>
    public int Standing => _servants.Count(s => s.IsAlive && !s.IsColossus);

    /// <summary>The standing colossus, or null.</summary>
    public Servant? Colossus => _servants.FirstOrDefault(s => s.IsAlive && s.IsColossus);

    /// <summary>A kill at <paramref name="at"/>: with Raise Dead, every 8th rises again there (at the next <see cref="Update"/>).</summary>
    public void OnKill(Vector3D<float> at, PriestStats stats)
    {
        if (!stats.Grave.RaiseDead)
        {
            return;
        }

        if (++KillsToward >= PriestStats.RaiseEvery)
        {
            KillsToward = 0;
            _due.Add(at);
        }
    }

    /// <summary>
    /// One frame: the raises due, then every servant going for its enemy (or back to the Priest at <paramref name="feet"/>), clawing, wearing down and crumbling,
    /// and the fallen falling apart. Every hit goes on <paramref name="hits"/> through <paramref name="skulls"/> (the Priest's damage, with its multipliers).
    /// </summary>
    public void Update(float deltaSeconds, Vector3D<float> feet, PriestStats stats, EnemyField enemies, Func<float, float, float?> groundAt, PlagueSkulls skulls,
        List<PriestHit> hits)
    {
        foreach (var at in _due)
        {
            Raise(at, stats, enemies.Scaling.Health, groundAt);
        }

        _due.Clear();

        foreach (var servant in _servants.ToList())
        {
            if (servant.DeadFor is { } dead)
            {
                servant.DeadFor = dead + deltaSeconds;
                if (dead + deltaSeconds >= DeathSeconds)
                {
                    _servants.Remove(servant);
                }

                continue;
            }

            servant.Age += deltaSeconds;
            Act(servant, deltaSeconds, feet, stats, enemies, groundAt, skulls, hits);

            // Enemies touching it wear it down; none of them go for it.
            int touching = enemies.Within(servant.Position, servant.Radius).Count(e => !e.Kind.IsProp);
            servant.Health -= touching * PriestStats.ServantWear * enemies.Scaling.Damage * deltaSeconds;
            if (servant.Health <= 0f || servant.Age >= servant.Life)
            {
                Fall(servant, stats, enemies, skulls, hits);
            }
        }

        KeepApart(deltaSeconds);

        foreach (var burst in _bursts)
        {
            burst.Age += deltaSeconds;
        }

        _bursts.RemoveAll(b => b.Age >= BurstSeconds);
        foreach (var rising in _risings)
        {
            rising.Age += deltaSeconds;
        }

        _risings.RemoveAll(r => r.Age >= RiseSeconds);
    }

    /// <summary>
    /// A servant rises at <paramref name="at"/>, with its health grown by <paramref name="healthScale"/> (the enemies' own growth) - unless there are as many as
    /// can be: then, with Bone Colossus and no colossus standing, they all merge into one; otherwise nothing.
    /// </summary>
    public void Raise(Vector3D<float> at, PriestStats stats, float healthScale, Func<float, float, float?> groundAt)
    {
        var ground = new Vector3D<float>(at.X, groundAt(at.X, at.Z) ?? at.Y, at.Z);
        if (Standing < stats.MaxServants)
        {
            float health = stats.ServantHealth * healthScale;
            _servants.Add(new Servant
            {
                Position = ground, Health = health, MaxHealth = health, Life = stats.ServantLife, ClawIn = stats.ServantClawInterval * 0.5f,
                Slot = _slots++ * 2.4f,
            });
            _risings.Add(new RisingShow { Centre = ground, Size = 1f });
            return;
        }

        if (stats.Grave.BoneColossus && Colossus is null)
        {
            var merged = _servants.Where(s => s.IsAlive && !s.IsColossus).ToList();
            var centre = merged.Aggregate(Vector3D<float>.Zero, (sum, s) => sum + s.Position) / merged.Count;
            centre.Y = groundAt(centre.X, centre.Z) ?? centre.Y;
            foreach (var servant in merged)
            {
                _servants.Remove(servant);   // taken into it: no burst
            }

            float health = stats.ServantHealth * healthScale * PriestStats.ColossusHealth;
            _servants.Add(new Servant
            {
                Position = centre, Health = health, MaxHealth = health, Life = PriestStats.ColossusLife, ClawIn = stats.ServantClawInterval * 0.5f, IsColossus = true,
                Slot = _slots++ * 2.4f,
            });
            _risings.Add(new RisingShow { Centre = centre, Size = PriestStats.ColossusSize });
        }
    }

    /// <summary>Everything out of the world (a run's end), and the count back to the start.</summary>
    public void Reset()
    {
        _servants.Clear();
        _due.Clear();
        _bursts.Clear();
        _risings.Clear();
        KillsToward = 0;
        _slots = 0;
    }

    /// <summary>
    /// A servant's frame: back to the Priest's side if it fell far behind, then for the nearest enemy within the leash of the Priest (clawing it once in reach),
    /// or back to its place by the Priest.
    /// </summary>
    private void Act(Servant servant, float deltaSeconds, Vector3D<float> feet, PriestStats stats, EnemyField enemies, Func<float, float, float?> groundAt,
        PlagueSkulls skulls, List<PriestHit> hits)
    {
        Geometry.FlatDirection(feet, servant.Position, out float fromPriest);
        if (fromPriest > PriestStats.ServantRecall)
        {
            var side = feet + Place(servant);
            servant.Position = new Vector3D<float>(side.X, groundAt(side.X, side.Z) ?? feet.Y, side.Z);
            servant.Target = null;
        }

        if (servant.Target is not { IsAlive: true } || Far(feet, servant.Target.Position, PriestStats.ServantLeash))
        {
            servant.Target = NearestFoe(enemies, feet, servant.Position);
            servant.ClawIn = MathF.Max(servant.ClawIn, stats.ServantClawInterval * 0.5f);   // a moment to wind up at a new enemy
        }

        servant.Clawing = false;
        servant.ClawInterval = stats.ServantClawInterval;
        if (servant.Target is { } target)
        {
            var toward = Geometry.FlatDirection(servant.Position, target.Position, out float distance);
            if (toward != Vector3D<float>.Zero)
            {
                servant.Yaw = MathF.Atan2(toward.X, toward.Z);
            }

            float reach = servant.Radius + target.Kind.Radius + ClawReach;
            if (distance > reach)
            {
                Walk(servant, toward, MathF.Min(PriestStats.ServantSpeed * deltaSeconds, distance - reach * 0.8f), groundAt);
                return;
            }

            servant.Clawing = true;
            servant.ClawIn -= deltaSeconds;
            if (servant.ClawIn <= 0f)
            {
                servant.ClawIn += stats.ServantClawInterval;
                Claw(servant, target, stats, enemies, groundAt, skulls, hits);
            }

            return;
        }

        // Nothing to fight: back to its place by the Priest.
        var home = feet + Place(servant);
        var back = Geometry.FlatDirection(servant.Position, home, out float away);
        if (away > HomeSlack)
        {
            servant.Yaw = MathF.Atan2(back.X, back.Z);
            Walk(servant, back, MathF.Min(PriestStats.ServantSpeed * deltaSeconds, away), groundAt);
        }
    }

    /// <summary>
    /// A claw landing: on its enemy (a colossus's on everything within <see cref="PriestStats.ColossusCleave"/> of it); with Army of the Dead, an enemy it kills
    /// rises at once as another servant.
    /// </summary>
    private void Claw(Servant servant, Enemy target, PriestStats stats, EnemyField enemies, Func<float, float, float?> groundAt, PlagueSkulls skulls,
        List<PriestHit> hits)
    {
        float damage = stats.ServantDamage * (servant.IsColossus ? PriestStats.ColossusDamage : 1f);
        var struck = servant.IsColossus
            ? enemies.Within(servant.Position, PriestStats.ColossusCleave).Where(e => !e.Kind.IsProp).ToList()
            : new List<Enemy> { target };
        if (servant.IsColossus && !struck.Contains(target))
        {
            struck.Add(target);
        }

        foreach (var enemy in struck)
        {
            var at = enemy.Position;
            if (skulls.Hurt(enemy, damage, crit: false, PriestSource.Servant, stats, enemies, hits) && stats.Grave.ArmyOfTheDead)
            {
                Raise(at, stats, enemies.Scaling.Health, groundAt);
            }
        }
    }

    /// <summary>A servant falls (its health or its time gone): with Corpse Burst it bursts, hurting everything round it.</summary>
    private void Fall(Servant servant, PriestStats stats, EnemyField enemies, PlagueSkulls skulls, List<PriestHit> hits)
    {
        servant.DeadFor = 0f;
        servant.Health = MathF.Max(0f, servant.Health);
        servant.Clawing = false;
        if (!stats.Grave.CorpseBurst)
        {
            return;
        }

        float reach = stats.CorpseBurstReach;
        foreach (var enemy in enemies.Within(servant.Position, reach))
        {
            if (!enemy.Kind.IsProp)
            {
                skulls.Hurt(enemy, stats.CorpseBurstDamage, crit: false, PriestSource.Burst, stats, enemies, hits);
            }
        }

        _bursts.Add(new CorpseBurstShow { Centre = servant.Position, Radius = reach });
    }

    /// <summary>Steps a servant along <paramref name="way"/> by <paramref name="distance"/>, standing on the ground - not into rock (no ground there).</summary>
    private static void Walk(Servant servant, Vector3D<float> way, float distance, Func<float, float, float?> groundAt)
    {
        if (distance <= 0f)
        {
            return;
        }

        var to = servant.Position + way * distance;
        if (groundAt(to.X, to.Z) is { } ground)
        {
            servant.Position = new Vector3D<float>(to.X, ground, to.Z);
        }
    }

    /// <summary>Whether two points are further apart than <paramref name="distance"/>, on the flat.</summary>
    private static bool Far(Vector3D<float> a, Vector3D<float> b, float distance)
    {
        Geometry.FlatDirection(a, b, out float apart);
        return apart > distance;
    }

    /// <summary>Where a servant stands by the Priest when there is nothing to fight: its own spot on a ring round the Priest.</summary>
    private static Vector3D<float> Place(Servant servant) =>
        new(MathF.Sin(servant.Slot) * HomeDistance, 0f, MathF.Cos(servant.Slot) * HomeDistance);

    /// <summary>The nearest enemy (never a crate) to <paramref name="from"/> among those within the leash of the Priest at <paramref name="feet"/>.</summary>
    private static Enemy? NearestFoe(EnemyField enemies, Vector3D<float> feet, Vector3D<float> from)
    {
        Enemy? best = null;
        float bestDistance = float.MaxValue;
        foreach (var enemy in enemies.Within(feet, PriestStats.ServantLeash))
        {
            if (enemy.Kind.IsProp)
            {
                continue;
            }

            float distance = Vector3D.DistanceSquared(enemy.Position, from);
            if (distance < bestDistance)
            {
                best = enemy;
                bestDistance = distance;
            }
        }

        return best;
    }

    /// <summary>Servants standing in one another are nudged apart.</summary>
    private void KeepApart(float deltaSeconds)
    {
        for (int i = 0; i < _servants.Count; i++)
        {
            var a = _servants[i];
            if (!a.IsAlive)
            {
                continue;
            }

            for (int k = i + 1; k < _servants.Count; k++)
            {
                var b = _servants[k];
                if (!b.IsAlive)
                {
                    continue;
                }

                var apart = Geometry.FlatDirection(b.Position, a.Position, out float distance);
                float overlap = a.Radius + b.Radius - distance;
                if (overlap > 0f && apart != Vector3D<float>.Zero)
                {
                    float push = MathF.Min(overlap, 3f * deltaSeconds) * 0.5f;
                    a.Position += apart * push;
                    b.Position -= apart * push;
                }
            }
        }
    }
}
