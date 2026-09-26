using ArenaMaster.Game.Classes;
using ArenaMaster.Game.Combat;
using ArenaMaster.Game.Items;
using ArenaMaster.Game.Progression;
using ArenaMaster.Game.Ui;
using CEngine.Core;
using Silk.NET.Maths;

namespace ArenaMaster.Game.Shaman;

/// <summary>
/// The Shaman as the content runs it (see <see cref="IHeroClass"/>): Rolling Lightning (<see cref="RollingLightning"/>) lobbed at whatever the crosshair is on,
/// bouncing and forking off enemies, trees and rocks - the engine's obstacles, through <see cref="EngineWindow.TouchesObstacle"/>. Here too is what answers the
/// enemies' blows (Static Skin's shock, Lightning Reflexes' free surge) and Surge Strike's ball.
/// </summary>
internal sealed class ShamanClass : IHeroClass
{
    /// <summary>Where on the Shaman the ball leaves from: the raised hand, this high above the feet and this far ahead.</summary>
    private const float HandHeight = 1.6f;
    private const float HandForward = 0.4f;

    /// <summary>Where the ball goes with the crosshair on the sky: this far ahead.</summary>
    private const float SkyThrow = 14f;

    private readonly ShamanController _controller = new();
    private readonly RollingLightning _storm;
    private readonly StormView _view = new();
    private readonly HashSet<ShamanUpgrade> _banished = new();
    private readonly List<StormHit> _hits = new();
    private List<ShamanChoice> _choices = new();
    private float _reflexesIn;

    public ShamanClass(Random random) => _storm = new RollingLightning(random);

    public ShamanStats Stats { get; } = new();

    /// <summary>The balls, arcs and rods, for the tests.</summary>
    internal RollingLightning Storm => _storm;

    public string Id => AlignmentTree.ClassId;

    public string Name => "Shaman";

    public string Summary =>
        "A caller of storms. Rolling Lightning lobs a ball of lightning that bounces along the ground and forks off enemies, trees and rocks before it fades.";

    public TreeDefinition Tree => AlignmentTree.Tree;

    public float MaxHealth => Stats.MaxHealth;

    public float PickupRadius => Stats.PickupRadius;

    public float Regeneration => Stats.Regeneration;

    public float DamageTaken => Stats.DamageTaken;

    public float BlockChance => Stats.BlockChance;

    public bool KeepsOwnBarrier => false;

    public string DashLabel => "SURGE";

    public float DashReadiness => _controller.SurgeReadiness;

    public Vector3D<float> DashVelocity => _controller.SurgeVelocity;

    public string? Status => null;

    public void UseTree(IReadOnlyDictionary<string, int> ranks) => Stats.Tree = AlignmentBonuses.From(ranks);

    public void BeginRun(ItemBonuses items, PlayerHealth health)
    {
        Stats.Reset();
        Stats.Items = items;
        health.Reset(Stats.MaxHealth);
        _banished.Clear();
        _storm.Reset();
        _reflexesIn = 0f;
    }

    public void ReturnToCamp() => Stats.Reset();

    public void Move(EngineWindow window, float deltaSeconds, bool stunned) => _controller.Update(window, deltaSeconds, Stats, stunned);

    public void Hide(EngineWindow window) => _controller.Hide(window);

    public void Attack(RunFrame frame)
    {
        var window = frame.Window;
        var feet = window.PlayerFeet;
        var aim = ShamanController.Facing(window);
        var hand = feet + new Vector3D<float>(0f, HandHeight, 0f) + aim * HandForward;
        var target = Crosshair(window, frame.Enemies, frame.GroundAt, feet, aim);

        if (Stats.Tree.SurgeStrike && _controller.SurgeBegun is { } way && !frame.Condition.IsStunned)
        {
            _storm.Roll(feet, way, Stats);
        }

        ObstacleProbe obstacles = (Vector3D<float> centre, float radius, out Vector3D<float> pushOut, out float depth) =>
            window.TouchesObstacle(centre, radius, out pushOut, out depth);
        Fight(frame.DeltaSeconds, hand, feet, target, !frame.StandingStill, frame.Condition.IsStunned, frame.Enemies, frame.GroundAt, obstacles, frame.Health, frame.Numbers);
        _view.Sync(window, _storm, frame.DeltaSeconds);
    }

    public void Answer(RunFrame frame) => AnswerStrikes(frame.Enemies.Strikes, frame.Window.PlayerFeet, frame.Enemies, frame.Health, frame.Numbers);

    /// <summary>
    /// The attacks for one frame, before the enemies move: a cast from <paramref name="hand"/> at <paramref name="target"/> when one is due (held by a stun), the balls,
    /// rods, the Eye of the Storm while <paramref name="moving"/>, and Call Lightning. Each lightning kill heals with Galvanic Recovery.
    /// </summary>
    internal void Fight(float deltaSeconds, Vector3D<float> hand, Vector3D<float> feet, Vector3D<float> target, bool moving, bool stunned, EnemyField enemies,
        Func<float, float, float?> groundAt, ObstacleProbe obstacles, PlayerHealth health, DamageNumbers numbers)
    {
        _reflexesIn = MathF.Max(0f, _reflexesIn - deltaSeconds);
        _hits.Clear();
        _storm.Update(deltaSeconds, hand, feet, target, moving, Stats, enemies, groundAt, obstacles, canCast: !stunned, _hits);
        Report(numbers, health);
    }

    /// <summary>The enemies' blows this frame, answered: Static Skin shocks each attacker; a blow that landed recharges the surge, with Lightning Reflexes.</summary>
    internal void AnswerStrikes(IReadOnlyList<Strike> strikes, Vector3D<float> feet, EnemyField enemies, PlayerHealth health, DamageNumbers numbers)
    {
        _hits.Clear();
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
        }

        Report(numbers, health);
    }

    public void OnKill(Enemy killed, float runSeconds)
    {
    }

    public void Clear(EngineWindow window)
    {
        _storm.Reset();
        _view.Clear(window);
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
    /// The hits just dealt, on screen, and Galvanic Recovery's heal for each kill. The zaps' and rods' steady crackle, and the Eye's, show as the enemies flashing
    /// rather than as numbers, so they don't bury the ball's and the forks'.
    /// </summary>
    private void Report(DamageNumbers numbers, PlayerHealth health)
    {
        foreach (var hit in _hits)
        {
            if (hit.Source is not (StormSource.Zap or StormSource.Rod or StormSource.Eye))
            {
                numbers.Add(hit.Position, hit.Damage, hit.Killed, hit.Crit);
            }

            if (hit.Killed)
            {
                health.Heal(Stats.Tree.HealOnKill);
            }
        }
    }

    /// <summary>
    /// What the crosshair is on - the nearest enemy or ground along the camera's line of sight, within throwing range - or, with it on the sky, a spot on the ground
    /// a way ahead.
    /// </summary>
    private Vector3D<float> Crosshair(EngineWindow window, EnemyField enemies, Func<float, float, float?> groundAt, Vector3D<float> feet, Vector3D<float> aim)
    {
        if (window.Camera is not { } camera || window.Terrain is not { } terrain)
        {
            return feet + aim * SkyThrow;
        }

        float reach = Stats.ThrowRange + 10f;
        var far = camera.Position + camera.Front * reach;
        float best = float.MaxValue;
        Vector3D<float>? target = null;

        if (terrain.TryRaycast(camera.Position, camera.Front, reach, out var ground))
        {
            best = Vector3D.Distance(camera.Position, ground);
            target = ground;
        }

        if (enemies.FirstHit(camera.Position, far, 0.05f, out float along) is { } enemy && along * reach < best)
        {
            target = enemy.Position;
        }

        if (target is { } spot)
        {
            return spot;
        }

        var ahead = feet + aim * SkyThrow;
        return new Vector3D<float>(ahead.X, groundAt(ahead.X, ahead.Z) ?? feet.Y, ahead.Z);
    }

    private List<LevelUpCard> Cards() =>
        _choices.Select(c => new LevelUpCard(c.Name, c.Description, c.NewLevel, c.MaxLevel, IsHeal: c.Upgrade is null)).ToList();
}
