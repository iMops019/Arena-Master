using ArenaMaster.Game.Combat;
using Silk.NET.Maths;

namespace ArenaMaster.Game.Paladin;

/// <summary>A patch of holy ground a nova left behind: it burns the enemies in it and heals the Paladin standing in it, until it fades.</summary>
internal sealed class HolyCircle
{
    public Vector3D<float> Centre { get; init; }

    /// <summary>Its size when it appeared. With Expanding Light it grows from there.</summary>
    public float BaseRadius { get; init; }

    public float Lifetime { get; init; }

    public bool Grows { get; init; }

    public float Age { get; set; }

    /// <summary>Seconds until it next burns what is in it.</summary>
    public float TickIn { get; set; }

    public float Radius => Grows ? BaseRadius * (1f + Math.Clamp(Age / Lifetime, 0f, 1f)) : BaseRadius;

    public bool Done => Age >= Lifetime;
}

/// <summary>A nova's light spreading out over the ground, for the view. It does its damage the moment it appears; this is only the look.</summary>
internal sealed class NovaFlash
{
    public Vector3D<float> Centre { get; init; }

    public float Radius { get; init; }

    public float Age { get; set; }
}

/// <summary>What dealt a Paladin hit: the numbers on screen and the heals that answer kills care.</summary>
internal enum HolySource
{
    Nova,
    Circle,
    Thorns,
    ShieldBash,
    Retribution,
    Rush,
    Hammer,
}

internal readonly record struct HolyHit(Enemy Enemy, Vector3D<float> Position, float Damage, bool Killed, bool Crit, HolySource Source);

/// <summary>
/// The Paladin's attacks: the Holy Nova bursting around the Paladin on its own every few moments (hurting everything it reaches), the holy circle each nova leaves
/// on the ground, thorns for whatever touches the Paladin, and the Defiance majors that add bursts - Echoing Nova, Radiant Avatar's Great Nova, Resonance's
/// bursts from every circle - and what the Crusade adds to them: Endless Crusade's second burst at full Zeal, Penance's extra damage on the next nova, and the
/// enemies Condemn marks to take more. Pure simulation - no engine calls - so it can be tested; <see cref="HolyView"/> draws it.
/// </summary>
internal sealed class HolyLight
{
    /// <summary>A circle burns what is in it this often.</summary>
    public const float CircleTick = 0.5f;

    /// <summary>At most this many circles at once; a new one past it puts out the oldest.</summary>
    public const int MaxCircles = 12;

    /// <summary>How long a nova's light takes to spread out.</summary>
    public const float FlashDuration = 0.3f;

    /// <summary>The first nova of a run comes this soon.</summary>
    public const float FirstNova = 0.5f;

    private readonly Random _random;
    private readonly List<HolyCircle> _circles = new();
    private readonly List<NovaFlash> _flashes = new();
    private readonly List<(float Delay, Vector3D<float> Centre, float Radius, float Damage)> _echoes = new();
    private readonly Dictionary<Enemy, float> _condemned = new();
    private float _thornsIn;

    public HolyLight(Random random) => _random = random;

    public IReadOnlyList<HolyCircle> Circles => _circles;

    public IReadOnlyList<NovaFlash> Flashes => _flashes;

    /// <summary>Seconds until the next nova.</summary>
    public float NovaIn { get; private set; } = FirstNova;

    /// <summary>Novas cast this run (echoes and other bursts don't count).</summary>
    public int Novas { get; private set; }

    /// <summary>Penance: extra damage the next nova deals to every enemy it hits, spent by that nova.</summary>
    public float Penance { get; set; }

    /// <summary>Whether <paramref name="enemy"/> is condemned (Condemn): it takes more from every Paladin hit until its time runs out.</summary>
    public bool IsCondemned(Enemy enemy) => _condemned.ContainsKey(enemy);

    /// <summary>Condemns <paramref name="enemy"/> for <paramref name="seconds"/> (from now, however long it had left).</summary>
    public void Condemn(Enemy enemy, float seconds) => _condemned[enemy] = seconds;

    /// <summary>A roll against the nova's crit chance, for the Crusade's hits (the rush, a hammer).</summary>
    public bool RollCrit(PaladinStats stats) => _random.NextDouble() < stats.CritChance;

    /// <summary>How many circles <paramref name="feet"/> stands in.</summary>
    public int CirclesAround(Vector3D<float> feet) => _circles.Count(c =>
    {
        Geometry.FlatDirection(c.Centre, feet, out float distance);
        return distance <= c.Radius;
    });

    /// <summary>
    /// One frame: a nova when one is due (held while <paramref name="canCast"/> is false - a stun), echoes coming due, the circles burning and fading, Condemn's
    /// clocks, and thorns.
    /// <paramref name="feet"/> is the ground under the Paladin. Every hit dealt goes on <paramref name="hits"/>.
    /// </summary>
    public void Update(float deltaSeconds, Vector3D<float> feet, PaladinStats stats, EnemyField enemies, bool canCast, List<HolyHit> hits)
    {
        NovaIn -= deltaSeconds;
        if (!canCast)
        {
            NovaIn = MathF.Max(NovaIn, 0.15f);
        }
        else if (NovaIn <= 0f)
        {
            NovaIn = MathF.Max(0f, NovaIn + stats.NovaInterval);   // after a pause, no burst of novas to catch up
            CastNova(feet, stats, enemies, hits);
        }

        for (int i = _echoes.Count - 1; i >= 0; i--)
        {
            var echo = _echoes[i];
            echo.Delay -= deltaSeconds;
            if (echo.Delay > 0f)
            {
                _echoes[i] = echo;
                continue;
            }

            _echoes.RemoveAt(i);
            Burst(echo.Centre, echo.Radius, echo.Damage, stats, enemies, hits);
        }

        foreach (var circle in _circles)
        {
            circle.Age += deltaSeconds;
            circle.TickIn -= deltaSeconds;
            while (circle.TickIn <= 0f && !circle.Done)
            {
                circle.TickIn += CircleTick;
                foreach (var enemy in enemies.Within(circle.Centre, circle.Radius))
                {
                    Hurt(enemy, stats.CircleDps * CircleTick, crit: false, HolySource.Circle, stats, enemies, hits);
                }
            }
        }

        _circles.RemoveAll(c => c.Done);

        foreach (var flash in _flashes)
        {
            flash.Age += deltaSeconds;
        }

        _flashes.RemoveAll(f => f.Age >= FlashDuration);
        UpdateCondemned(deltaSeconds);
        UpdateThorns(deltaSeconds, feet, stats, enemies, hits);
    }

    /// <summary>
    /// A Holy Nova at <paramref name="at"/>: with Resonance, a burst from every circle already on the ground first; then the nova itself (every 8th a Great Nova, with
    /// Radiant Avatar; Penance's stored damage on top); then the circle it leaves, and, with Echoing Nova or Endless Crusade at full Zeal, its echo lined up.
    /// </summary>
    public void CastNova(Vector3D<float> at, PaladinStats stats, EnemyField enemies, List<HolyHit> hits)
    {
        Novas++;
        bool great = stats.Tree.RadiantAvatar && Novas % PaladinStats.AvatarEvery == 0;
        float radius = stats.NovaRadius * (great ? PaladinStats.AvatarRadius : 1f);
        float damage = stats.NovaDamage * (great ? PaladinStats.AvatarDamage : 1f);

        if (stats.Tree.Resonance)
        {
            foreach (var circle in _circles.ToList())
            {
                Burst(circle.Centre, circle.Radius, damage * PaladinStats.ResonanceDamage, stats, enemies, hits);
            }
        }

        float penance = Penance;
        Penance = 0f;
        Burst(at, radius, damage + penance, stats, enemies, hits);
        LeaveCircle(at, stats.CircleRadius * (great ? PaladinStats.AvatarCircle : 1f), stats);
        if (stats.Tree.EchoingNova)
        {
            _echoes.Add((PaladinStats.EchoDelay, at, radius, damage * PaladinStats.EchoDamage));
        }
        else if (stats.Crusade.EndlessCrusade && stats.ZealFull)
        {
            _echoes.Add((PaladinStats.EndlessDelay, at, radius, damage * PaladinStats.EndlessDamage));
        }
    }

    /// <summary>
    /// A burst of holy light: <paramref name="damage"/> to every enemy <paramref name="radius"/> reaches, each rolled for a crit (and more for each enemy hit, with
    /// Wrath of the Many). The nova, its echo, a Resonance burst and a Holy Bastion block all go through here.
    /// </summary>
    public void Burst(Vector3D<float> centre, float radius, float damage, PaladinStats stats, EnemyField enemies, List<HolyHit> hits)
    {
        _flashes.Add(new NovaFlash { Centre = centre, Radius = radius });
        var struck = enemies.Within(centre, radius);
        float wrath = stats.Tree.WrathOfTheMany ? MathF.Min(PaladinStats.WrathCap, PaladinStats.WrathPerEnemy * struck.Count) : 0f;
        foreach (var enemy in struck)
        {
            bool crit = _random.NextDouble() < stats.CritChance;
            Hurt(enemy, damage * (1f + wrath) * (crit ? stats.CritMultiplier : 1f), crit, HolySource.Nova, stats, enemies, hits);
        }
    }

    /// <summary>Hurts one enemy (harder if it is an elite or a boss, or condemned) and notes the hit. Nothing happens to one already dead.</summary>
    public void Hurt(Enemy enemy, float amount, bool crit, HolySource source, PaladinStats stats, EnemyField enemies, List<HolyHit> hits)
    {
        if (!enemy.IsAlive || amount <= 0f)
        {
            return;
        }

        if (enemy.Kind.Tier != EnemyTier.Fodder)
        {
            amount *= stats.EliteMultiplier;
        }

        if (_condemned.ContainsKey(enemy))
        {
            amount *= 1f + PaladinStats.CondemnBonus;
        }

        bool killed = enemies.Damage(enemy, amount);
        hits.Add(new HolyHit(enemy, enemy.Position + new Vector3D<float>(0f, enemy.Kind.Height * 0.7f, 0f), amount, killed, crit, source));
    }

    /// <summary>Everything off the ground and out of the air (a restart), and the clocks back to the start.</summary>
    public void Reset()
    {
        _circles.Clear();
        _flashes.Clear();
        _echoes.Clear();
        _condemned.Clear();
        NovaIn = FirstNova;
        Novas = 0;
        Penance = 0f;
        _thornsIn = 0f;
    }

    /// <summary>A holy circle of <paramref name="radius"/> at <paramref name="at"/>, as a nova leaves (the Crusade leaves them after a rush, a hammer and along a trail too).</summary>
    public void LeaveCircle(Vector3D<float> at, float radius, PaladinStats stats)
    {
        if (_circles.Count >= MaxCircles)
        {
            _circles.RemoveAt(0);
        }

        _circles.Add(new HolyCircle
        {
            Centre = at,
            BaseRadius = radius,
            Lifetime = stats.CircleDuration,
            Grows = stats.Tree.ExpandingLight,
            TickIn = CircleTick,
        });
    }

    /// <summary>Condemn's clocks: an enemy goes free when its time runs out (or it dies).</summary>
    private void UpdateCondemned(float deltaSeconds)
    {
        if (_condemned.Count == 0)
        {
            return;
        }

        foreach (var enemy in _condemned.Keys.ToList())
        {
            float left = _condemned[enemy] - deltaSeconds;
            if (left <= 0f || !enemy.IsAlive)
            {
                _condemned.Remove(enemy);
            }
            else
            {
                _condemned[enemy] = left;
            }
        }
    }

    /// <summary>Thorns, once unlocked: every so often, each enemy touching the Paladin (or near, with Crown of Briars) takes the thorns damage.</summary>
    private void UpdateThorns(float deltaSeconds, Vector3D<float> feet, PaladinStats stats, EnemyField enemies, List<HolyHit> hits)
    {
        if (!stats.HasThorns)
        {
            _thornsIn = 0f;
            return;
        }

        _thornsIn -= deltaSeconds;
        if (_thornsIn > 0f)
        {
            return;
        }

        _thornsIn = MathF.Max(0f, _thornsIn + stats.ThornsInterval);
        float reach = stats.Tree.CrownOfBriars ? PaladinStats.BriarsReach : EnemyField.PlayerRadius + PaladinStats.ThornsReach;
        float thorns = stats.Thorns;
        foreach (var enemy in enemies.Within(feet, reach))
        {
            Hurt(enemy, thorns, crit: false, HolySource.Thorns, stats, enemies, hits);
        }
    }
}
