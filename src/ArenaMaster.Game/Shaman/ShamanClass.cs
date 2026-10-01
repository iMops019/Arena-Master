using ArenaMaster.Game.Classes;
using ArenaMaster.Game.Combat;
using ArenaMaster.Game.Items;
using ArenaMaster.Game.Progression;
using ArenaMaster.Game.Ui;
using CEngine.Core;
using Silk.NET.Maths;

namespace ArenaMaster.Game.Shaman;

/// <summary>
/// The Shaman as the content runs it (see <see cref="IHeroClass"/>): a throw on its own at the nearest enemy (<see cref="LobSight"/>), whose element follows the
/// active tree. With Lightning Alignment it is Rolling Lightning (<see cref="RollingLightning"/>), bouncing and forking off enemies, trees and rocks - the
/// engine's obstacles, through <see cref="EngineWindow.TouchesObstacle"/>; with Earth Alignment a stone (<see cref="RollingStone"/>), bouncing off the ground and
/// enemies in quakes. Here too is what answers the enemies' blows (Static Skin's shock, Lightning Reflexes' free surge, Upheaval), Surge Strike's ball, the
/// Earthen Totem, and Stoneskin's cut to the damage taken.
/// </summary>
internal sealed class ShamanClass : IHeroClass
{
    /// <summary>Where on the Shaman the ball leaves from: the raised hand, this high above the feet and this far ahead.</summary>
    private const float HandHeight = 1.6f;
    private const float HandForward = 0.4f;

    private readonly ShamanController _controller = new();
    private readonly RollingLightning _storm;
    private readonly RollingStone _stones;
    private readonly StormView _view = new();
    private readonly EarthView _earthView = new();
    private readonly LobSight _sight = new();
    private readonly HashSet<ShamanUpgrade> _banished = new();
    private readonly List<StormHit> _hits = new();
    private readonly List<EarthHit> _earthHits = new();
    private List<ShamanChoice> _choices = new();
    private float _reflexesIn;

    /// <summary>How long after the last attack frame the body still holds its fighting stance (a run's attacks come after the body in each frame).</summary>
    private const float AttackingHold = 0.25f;

    private float _attackingLeft;

    /// <summary>The flat way to the enemy it fights, as of the last attack frame, or null with none: the body faces it.</summary>
    private Vector3D<float>? _aim;

    public ShamanClass(Random random)
    {
        _storm = new RollingLightning(random);
        _stones = new RollingStone(random);
    }

    public ShamanStats Stats { get; } = new();

    /// <summary>The balls, arcs and rods, for the tests.</summary>
    internal RollingLightning Storm => _storm;

    /// <summary>The stones, quakes, cracks and totems, for the tests.</summary>
    internal RollingStone Stones => _stones;

    public string Id => AlignmentTree.ClassId;

    public string Name => "Shaman";

    public string Summary =>
        "A caller of storms and stone. It throws at the nearest enemy, and what it throws follows its tree: with Lightning Alignment a ball of lightning that bounces "
        + "along the ground and forks off enemies, trees and rocks; with Earth Alignment a heavy stone that knocks enemies back and shakes the ground each time it bounces.";

    public IReadOnlyList<TreeDefinition> Trees { get; } = new[] { AlignmentTree.Tree, EarthTree.Tree };

    public TreeDefinition Tree { get; private set; } = AlignmentTree.Tree;

    public void ChooseTree(string treeId) => Tree = Trees.FirstOrDefault(t => t.Id == treeId) ?? Trees[0];

    public float MaxHealth => Stats.MaxHealth;

    public float PickupRadius => Stats.PickupRadius;

    public float Regeneration => Stats.Regeneration;

    /// <summary>What every hit's damage is multiplied by: the stats', less Stoneskin's cut.</summary>
    public float DamageTaken => Stats.DamageTaken * (1f - _stones.Stoneskin);

    public float BlockChance => Stats.BlockChance;

    public bool KeepsOwnBarrier => false;

    public string DashLabel => "SURGE";

    public float DashReadiness => _controller.SurgeReadiness;

    public Vector3D<float> DashVelocity => _controller.SurgeVelocity;

    public string? Status => null;

    public Enemy? AimTarget => _attackingLeft > 0f ? _sight.Target : null;

    /// <summary>Stoneskin's cut, while Earth Alignment has it.</summary>
    public (string Label, float Fill)? Meter =>
        Stats.Stone && Stats.HasStoneskin ? ($"STONESKIN {MathF.Round(_stones.Stoneskin * 100f):0}%", _stones.Stoneskin / Stats.StoneskinMax) : null;

    /// <summary>The active tree's ranks: its bonuses, and what the Shaman throws. The other tree's bonuses are emptied, so nothing of it counts.</summary>
    public void UseTree(IReadOnlyDictionary<string, int> ranks)
    {
        if (Tree.Id == EarthTree.TreeId)
        {
            Stats.Earth = EarthBonuses.From(ranks);
            Stats.Tree = new AlignmentBonuses();
            Stats.Element = ShamanElement.Stone;
        }
        else
        {
            Stats.Tree = AlignmentBonuses.From(ranks);
            Stats.Earth = new EarthBonuses();
            Stats.Element = ShamanElement.Lightning;
        }
    }

    public void BeginRun(ItemBonuses items, PlayerHealth health)
    {
        Stats.Reset();
        Stats.Items = items;
        health.Reset(Stats.MaxHealth);
        _banished.Clear();
        _storm.Reset();
        _stones.Reset();
        _sight.Clear();
        _reflexesIn = 0f;
    }

    public void ReturnToCamp() => Stats.Reset();

    public void Move(EngineWindow window, float deltaSeconds, bool stunned)
    {
        _attackingLeft = MathF.Max(0f, _attackingLeft - deltaSeconds);
        bool fighting = _attackingLeft > 0f;
        _controller.Update(window, deltaSeconds, Stats, stunned, fighting ? AttackTime() : null, fighting ? _aim : null);
    }

    /// <summary>
    /// Where the body's attack clip is, in time with the casts: wound up over the last moments before each one, through the blow just
    /// after (the clip's middle is the moment it lands), and ready in between.
    /// </summary>
    private float AttackTime()
    {
        float interval = Stats.CastInterval;
        float until = Stats.Stone ? _stones.CastIn : _storm.CastIn;
        float since = interval - until;
        float follow = MathF.Min(0.45f, 0.5f * interval);
        float windup = MathF.Min(0.4f, 0.45f * interval);
        if (since >= 0f && since < follow)
        {
            return 0.5f + 0.5f * since / follow;
        }

        if (until < windup)
        {
            return 0.5f * (1f - until / windup);
        }

        return 0f;
    }

    public void Hide(EngineWindow window) => _controller.Hide(window);

    public void Attack(RunFrame frame)
    {
        _attackingLeft = AttackingHold;
        var window = frame.Window;
        var feet = window.PlayerFeet;
        var hand = feet + new Vector3D<float>(0f, HandHeight, 0f) + _controller.Facing * HandForward;
        _sight.Update(frame.Enemies, feet, Stats.ThrowRange, frame.DeltaSeconds);
        var target = _sight.AimPoint(hand, Stats);
        _aim = _sight.Target is { } foe && Geometry.FlatDirection(feet, foe.Position, out _) is var toward && toward != Vector3D<float>.Zero ? toward : null;

        if (Stats.Tree.SurgeStrike && _controller.SurgeBegun is { } way && !frame.Condition.IsStunned)
        {
            _storm.Roll(feet, way, Stats);
        }

        if (Stats.Stone && Stats.Earth.EarthenTotem && _controller.SurgeBegun is not null && !frame.Condition.IsStunned)
        {
            _stones.PlantTotem(feet, Stats, frame.Enemies);   // where it surged from: the surge's push comes after this frame's attacks
        }

        ObstacleProbe obstacles = (Vector3D<float> centre, float radius, out Vector3D<float> pushOut, out float depth) =>
            window.TouchesObstacle(centre, radius, out pushOut, out depth);
        Fight(frame.DeltaSeconds, hand, feet, target, !frame.StandingStill, frame.Condition.IsStunned, frame.Enemies, frame.GroundAt, obstacles, frame.Health, frame.Numbers);
        if (Stats.Stone)
        {
            _earthView.Sync(window, _stones, frame.DeltaSeconds, frame.GroundAt);
        }
        else
        {
            _view.Sync(window, _storm, frame.DeltaSeconds, frame.GroundAt);
        }
    }

    public void Answer(RunFrame frame) => AnswerStrikes(frame.Enemies.Strikes, frame.Window.PlayerFeet, frame.Enemies, frame.Health, frame.Numbers);

    /// <summary>
    /// The attacks for one frame, before the enemies move: a cast from <paramref name="hand"/> at <paramref name="target"/> when one is due (held by a stun, and
    /// waiting with no target: null). With Lightning Alignment the balls, rods, the Eye of the Storm while <paramref name="moving"/>, and Call Lightning (each
    /// lightning kill heals with Galvanic Recovery); with Earth Alignment the stones, their cracks and rifts, and Stoneskin (built while not
    /// <paramref name="moving"/>).
    /// </summary>
    internal void Fight(float deltaSeconds, Vector3D<float> hand, Vector3D<float> feet, Vector3D<float>? target, bool moving, bool stunned, EnemyField enemies,
        Func<float, float, float?> groundAt, ObstacleProbe obstacles, PlayerHealth health, DamageNumbers numbers)
    {
        _reflexesIn = MathF.Max(0f, _reflexesIn - deltaSeconds);
        _hits.Clear();
        _earthHits.Clear();
        if (Stats.Stone)
        {
            _stones.Update(deltaSeconds, hand, target, moving, Stats, enemies, groundAt, obstacles, canCast: !stunned, _earthHits);
        }
        else
        {
            _storm.Update(deltaSeconds, hand, feet, target, moving, Stats, enemies, groundAt, obstacles, canCast: !stunned, _hits);
        }

        Report(numbers, health);
    }

    /// <summary>
    /// The enemies' blows this frame, answered: Static Skin shocks each attacker; a blow that landed recharges the surge, with Lightning Reflexes, or heaves the
    /// ground, with Upheaval.
    /// </summary>
    internal void AnswerStrikes(IReadOnlyList<Strike> strikes, Vector3D<float> feet, EnemyField enemies, PlayerHealth health, DamageNumbers numbers)
    {
        _hits.Clear();
        _earthHits.Clear();
        foreach (var strike in strikes)
        {
            if (Stats.Tree.ShockOnStruck > 0f && strike.Attacker.IsAlive)
            {
                _storm.AddArc(feet + new Vector3D<float>(0f, 1.2f, 0f), strike.Attacker.Position + new Vector3D<float>(0f, strike.Attacker.Kind.Height * 0.6f, 0f));
                _storm.Shock(strike.Attacker, Stats.Tree.ShockOnStruck * Stats.Items.DamageMultiplier, StormSource.Shock, Stats, enemies, _hits);
            }

            if (Stats.Tree.LightningReflexes && !strike.Blocked && _reflexesIn <= 0f)
            {
                _controller.Recharge();
                _reflexesIn = ShamanStats.ReflexesCooldown;
            }

            if (!strike.Blocked)
            {
                _stones.Upheave(feet, Stats, enemies, _earthHits);   // (only with Upheaval, at most once a second)
            }
        }

        Report(numbers, health);
    }

    public void OnKill(Enemy killed, float runSeconds)
    {
    }

    public void Clear(EngineWindow window)
    {
        _storm.Reset();
        _stones.Reset();
        _sight.Clear();
        _view.Clear(window);
        _earthView.Clear(window);
    }

    public IReadOnlyList<LevelUpCard> RollLevelUp(Random random)
    {
        _choices = ShamanUpgrades.Roll(Stats, random, excluded: _banished);
        return Cards();
    }

    public IReadOnlyList<LevelUpCard> BanishCard(int index, Random random)
    {
        if (index >= 0 && index < _choices.Count && _choices[index].Upgrade is { } banished)
        {
            _banished.Add(banished);
            _choices = ShamanUpgrades.Replace(_choices, index, Stats, random, _banished);
        }

        return Cards();
    }

    public void TakeCard(int index, PlayerHealth health)
    {
        if (index < 0 || index >= _choices.Count)
        {
            return;
        }

        if (_choices[index].Upgrade is { } upgrade)
        {
            Stats.Increase(upgrade);
        }
        else
        {
            health.Heal(ShamanUpgrades.SecondWindHeal);
        }
    }

    /// <summary>
    /// The hits just dealt, on screen, and Galvanic Recovery's heal for each kill. The zaps' and rods' steady crackle, and the Eye's, are added up for each enemy and
    /// shown as damage over time, so they don't bury the ball's and the forks'; so are the rifts' bites.
    /// </summary>
    private void Report(DamageNumbers numbers, PlayerHealth health)
    {
        foreach (var hit in _earthHits)
        {
            if (hit.Source == EarthSource.Rift)
            {
                numbers.AddOverTime(hit.Enemy, hit.Position, hit.Damage, hit.Killed);
            }
            else
            {
                numbers.Add(hit.Position, hit.Damage, hit.Killed, hit.Crit);
            }
        }

        foreach (var hit in _hits)
        {
            if (hit.Source is not (StormSource.Zap or StormSource.Rod or StormSource.Eye))
            {
                numbers.Add(hit.Position, hit.Damage, hit.Killed, hit.Crit);
            }
            else
            {
                numbers.AddOverTime(hit.Enemy, hit.Position, hit.Damage, hit.Killed);
            }

            if (hit.Killed)
            {
                health.Heal(Stats.Tree.HealOnKill);
            }
        }
    }

    private List<LevelUpCard> Cards() =>
        _choices.Select(c => new LevelUpCard(c.Name, c.Description, c.NewLevel, c.MaxLevel, IsHeal: c.Upgrade is null)).ToList();
}
