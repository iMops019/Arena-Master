using ArenaMaster.Game.Combat;
using Silk.NET.Maths;

namespace ArenaMaster.Game.Paladin;

/// <summary>A hammer of light falling on an enemy (Hammer of Judgement): it follows its target down while that lives, and lands <see cref="CrusadeLight.FallTime"/> after it was called.</summary>
internal sealed class FallingHammer
{
    /// <summary>What it falls on, or null once that died (it lands where the enemy was).</summary>
    public Enemy? Target { get; set; }

    /// <summary>Where it will land: under its target, on the ground.</summary>
    public Vector3D<float> Position { get; set; }

    /// <summary>What it does to everything within <see cref="Radius"/>, fixed when it was called.</summary>
    public float Damage { get; init; }

    public float Radius { get; init; }

    public float Age { get; set; }
}

/// <summary>Where a hammer landed, for the view: a ring of light spreading out over the ground. The damage was done the moment it landed.</summary>
internal sealed class HammerImpact
{
    public Vector3D<float> Centre { get; init; }

    public float Radius { get; init; }

    public float Age { get; set; }
}

/// <summary>
/// What the Crusade tree adds to the Paladin's fight, alongside the Holy Nova (<see cref="HolyLight"/>): Zeal built by moving and drained by standing still, the
/// hammers of light every 5th nova (Hammer of Judgement, Final Judgement, Condemn, Hallowed Impact), Crusader's Rush hitting what the shield rush passes, Blessed
/// Trail's holy ground, Blood Oath's price on each nova, and Avenging Wings. It keeps the stats' run state (<see cref="PaladinStats.Zeal"/>,
/// <see cref="PaladinStats.HealthShare"/>, <see cref="PaladinStats.Winged"/>) up to date. Pure simulation - no engine calls - so it can be tested;
/// <see cref="CrusadeView"/> draws it.
/// </summary>
internal sealed class CrusadeLight
{
    /// <summary>How long a hammer takes to fall from the sky.</summary>
    public const float FallTime = 0.5f;

    /// <summary>How long a hammer's ring of light takes to spread out.</summary>
    public const float ImpactDuration = 0.45f;

    /// <summary>A step longer than this in one frame is a teleport, not walking: Blessed Trail doesn't count it.</summary>
    private const float LongestStep = 2.5f;

    private readonly List<FallingHammer> _hammers = new();
    private readonly List<HammerImpact> _impacts = new();
    private readonly HashSet<Enemy> _rushed = new();
    private bool _wasRushing;
    private Vector3D<float>? _lastStep;
    private float _walked;

    public IReadOnlyList<FallingHammer> Hammers => _hammers;

    public IReadOnlyList<HammerImpact> Impacts => _impacts;

    /// <summary>The Zeal built up, 0 to <see cref="PaladinStats.MaxZeal"/> (always 0 without Zeal or Endless Crusade).</summary>
    public float Zeal { get; private set; }

    /// <summary>Seconds of Avenging Wings left (0 when not flying).</summary>
    public float WingsLeft { get; private set; }

    /// <summary>Seconds until Avenging Wings can come again.</summary>
    public float WingsReadyIn { get; private set; }

    public bool Winged => WingsLeft > 0f;

    /// <summary>
    /// The first half of a frame, before the novas: Zeal builds while <paramref name="moving"/> (drains otherwise), the wings' clocks run, and the stats take the
    /// Zeal, the wings and the health share the novas will read.
    /// </summary>
    public void Prepare(float deltaSeconds, bool moving, PaladinStats stats, PlayerHealth health)
    {
        Zeal = stats.BuildsZeal ? Math.Clamp(Zeal + (moving ? stats.ZealGain : -stats.ZealLoss) * deltaSeconds, 0f, PaladinStats.MaxZeal) : 0f;
        WingsLeft = MathF.Max(0f, WingsLeft - deltaSeconds);
        WingsReadyIn = MathF.Max(0f, WingsReadyIn - deltaSeconds);
        Share(stats, health);
    }

    /// <summary>
    /// The second half of a frame, after the novas (<paramref name="novasCast"/> of them this frame, the last of them <paramref name="light"/>'s newest): Blood
    /// Oath's price for each, a hammer call for every 5th, the hammers falling, Crusader's Rush while <paramref name="rushing"/> (and its circle once the rush
    /// ends), Blessed Trail's circles, and Avenging Wings if health has dropped low. <paramref name="ground"/> is the ground under the Paladin.
    /// </summary>
    public void Update(float deltaSeconds, Vector3D<float> ground, bool moving, bool rushing, int novasCast, HolyLight light, PaladinStats stats, EnemyField enemies,
        PlayerHealth health, List<HolyHit> hits)
    {
        for (int i = 0; i < novasCast; i++)
        {
            if (stats.Crusade.BloodOath)
            {
                health.Spend(health.Max * PaladinStats.BloodOathCost);
            }

            int nova = light.Novas - novasCast + 1 + i;
            if (stats.DropsHammers && nova % PaladinStats.HammerEvery == 0)
            {
                CallHammers(ground, stats, enemies);
            }
        }

        UpdateHammers(deltaSeconds, light, stats, enemies, hits);
        UpdateRush(ground, rushing, light, stats, enemies, hits);
        UpdateTrail(ground, moving, light, stats);
        CheckWings(stats, health);
    }

    /// <summary>
    /// Hammers called down round <paramref name="ground"/>: on the toughest enemy within <see cref="PaladinStats.HammerRange"/> (a boss first, then an elite, then the
    /// most health), and with Final Judgement on every elite and boss within <see cref="PaladinStats.FinalJudgementRange"/> too. None with nothing near.
    /// </summary>
    public void CallHammers(Vector3D<float> ground, PaladinStats stats, EnemyField enemies)
    {
        var targets = new List<Enemy>();
        var toughest = enemies.Within(ground, PaladinStats.HammerRange)
            .Where(e => !e.Kind.IsProp)
            .OrderByDescending(e => e.Kind.Tier)
            .ThenByDescending(e => e.Health)
            .FirstOrDefault();
        if (toughest is not null)
        {
            targets.Add(toughest);
        }

        if (stats.Crusade.FinalJudgement)
        {
            targets.AddRange(enemies.Within(ground, PaladinStats.FinalJudgementRange)
                .Where(e => !e.Kind.IsProp && e.Kind.Tier != EnemyTier.Fodder && e != toughest));
        }

        foreach (var target in targets)
        {
            _hammers.Add(new FallingHammer { Target = target, Position = target.Position, Damage = stats.HammerDamage, Radius = stats.HammerRadius });
        }
    }

    /// <summary>Avenging Wings: below <see cref="PaladinStats.WingsBelow"/> of max health, and ready, the Paladin takes flight. The stats take it at once.</summary>
    public void CheckWings(PaladinStats stats, PlayerHealth health)
    {
        if (stats.Crusade.AvengingWings && !Winged && WingsReadyIn <= 0f && !health.IsDead && health.Current < health.Max * PaladinStats.WingsBelow)
        {
            WingsLeft = PaladinStats.WingsDuration;
            WingsReadyIn = PaladinStats.WingsCooldown;
        }

        Share(stats, health);
    }

    /// <summary>Nothing in the air, no Zeal, the wings ready (a new run, or the run ended).</summary>
    public void Reset()
    {
        _hammers.Clear();
        _impacts.Clear();
        _rushed.Clear();
        _wasRushing = false;
        _lastStep = null;
        _walked = 0f;
        Zeal = 0f;
        WingsLeft = 0f;
        WingsReadyIn = 0f;
    }

    private void Share(PaladinStats stats, PlayerHealth health)
    {
        stats.Zeal = Zeal;
        stats.Winged = Winged;
        stats.HealthShare = health.Max > 0f ? Math.Clamp(health.Current / health.Max, 0f, 1f) : 1f;
    }

    /// <summary>The hammers coming down (following their targets), landing - hurting all under them, condemning them, leaving a circle - and their rings spreading.</summary>
    private void UpdateHammers(float deltaSeconds, HolyLight light, PaladinStats stats, EnemyField enemies, List<HolyHit> hits)
    {
        foreach (var impact in _impacts)
        {
            impact.Age += deltaSeconds;
        }

        _impacts.RemoveAll(i => i.Age >= ImpactDuration);

        for (int i = _hammers.Count - 1; i >= 0; i--)
        {
            var hammer = _hammers[i];
            hammer.Age += deltaSeconds;
            if (hammer.Target is { } target)
            {
                if (target.IsAlive)
                {
                    hammer.Position = target.Position;
                }
                else
                {
                    hammer.Target = null;   // it lands where the enemy fell
                }
            }

            if (hammer.Age < FallTime)
            {
                continue;
            }

            _hammers.RemoveAt(i);
            foreach (var enemy in enemies.Within(hammer.Position, hammer.Radius))
            {
                bool crit = light.RollCrit(stats);
                light.Hurt(enemy, hammer.Damage * (crit ? stats.CritMultiplier : 1f), crit, HolySource.Hammer, stats, enemies, hits);
                if (stats.Crusade.Condemn && enemy.IsAlive)
                {
                    light.Condemn(enemy, PaladinStats.CondemnDuration);
                }
            }

            if (stats.Crusade.HallowedImpact)
            {
                light.LeaveCircle(hammer.Position, stats.CircleRadius, stats);
            }

            _impacts.Add(new HammerImpact { Centre = hammer.Position, Radius = hammer.Radius });
        }
    }

    /// <summary>Crusader's Rush: each enemy the rush passes is hit once, for a nova's damage; a holy circle where it ends.</summary>
    private void UpdateRush(Vector3D<float> ground, bool rushing, HolyLight light, PaladinStats stats, EnemyField enemies, List<HolyHit> hits)
    {
        if (stats.Crusade.CrusadersRush && rushing)
        {
            foreach (var enemy in enemies.Within(ground, EnemyField.PlayerRadius + PaladinStats.RushReach))
            {
                if (_rushed.Add(enemy))
                {
                    bool crit = light.RollCrit(stats);
                    light.Hurt(enemy, stats.RushDamage * (crit ? stats.CritMultiplier : 1f), crit, HolySource.Rush, stats, enemies, hits);
                }
            }
        }

        if (_wasRushing && !rushing)
        {
            if (stats.Crusade.CrusadersRush)
            {
                light.LeaveCircle(ground, stats.CircleRadius, stats);
            }

            _rushed.Clear();
        }

        _wasRushing = rushing;
    }

    /// <summary>Blessed Trail: while walking, a smaller holy circle every <see cref="PaladinStats.TrailStep"/> metres.</summary>
    private void UpdateTrail(Vector3D<float> ground, bool moving, HolyLight light, PaladinStats stats)
    {
        var last = _lastStep;
        _lastStep = ground;
        if (!stats.Crusade.BlessedTrail || !moving || last is not { } from)
        {
            return;
        }

        Geometry.FlatDirection(from, ground, out float step);
        if (step > LongestStep)
        {
            return;
        }

        _walked += step;
        if (_walked >= PaladinStats.TrailStep)
        {
            _walked -= PaladinStats.TrailStep;
            light.LeaveCircle(ground, stats.CircleRadius * PaladinStats.TrailCircle, stats);
        }
    }
}
