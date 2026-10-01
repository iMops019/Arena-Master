using ArenaMaster.Game.Classes;
using ArenaMaster.Game.Combat;
using ArenaMaster.Game.Items;
using ArenaMaster.Game.Progression;
using ArenaMaster.Game.Ui;
using CEngine.Core;
using Silk.NET.Maths;

namespace ArenaMaster.Game.Ranger;

/// <summary>
/// The Ranger as the content runs it (see <see cref="IHeroClass"/>): the stats, the controller, the bow and the level-up pool, plus what only the Ranger reacts
/// to - Momentum's recent kills and Headhunter's heal on an elite crit - and, with the Trapper tree active, its traps, venom and hawk (<see cref="TrapperKit"/>):
/// a snare where each dash began, poison on the arrows' hits, the hawk's dives.
/// </summary>
internal sealed class RangerClass : IHeroClass
{
    /// <summary>How long a kill counts toward Momentum.</summary>
    private const float MomentumWindow = 3f;

    /// <summary>How long after the bow last shot the body still holds it up (a run's attacks come after the body in each frame).</summary>
    private const float ShootingHold = 0.25f;

    private readonly RangerController _controller = new();
    private readonly RangerBow _bow;
    private readonly TrapperKit _kit;
    private readonly TrapperView _view = new();
    private readonly List<TrapperHit> _trapperHits = new();
    private readonly Queue<float> _recentKills = new();
    private readonly HashSet<RangerUpgrade> _banished = new();
    private List<UpgradeChoice> _choices = new();
    private float _shootingLeft;
    private Vector3D<float>? _snareDue;

    public RangerClass(Random random)
    {
        _kit = new TrapperKit(random, Stats);
        _bow = new RangerBow(random, _kit);
    }

    /// <summary>The Trapper tree's snares, poison and hawk (idle unless that tree is active and its majors are taken).</summary>
    public TrapperKit Kit => _kit;

    public RangerStats Stats { get; } = new();

    public string Id => SharpshooterTree.ClassId;

    public string Name => "Ranger";

    public string Summary =>
        "A bow that aims and fires on its own at the nearest enemy. Fast and fragile: keep moving, and let crits and chains, or traps, venom and a hawk, do the work.";

    public IReadOnlyList<TreeDefinition> Trees { get; } = new[] { SharpshooterTree.Tree, TrapperTree.Tree };

    public TreeDefinition Tree { get; private set; } = SharpshooterTree.Tree;

    public void ChooseTree(string treeId) => Tree = Trees.FirstOrDefault(t => t.Id == treeId) ?? Trees[0];

    public float MaxHealth => Stats.MaxHealth;

    public float PickupRadius => Stats.PickupRadius;

    public float Regeneration => Stats.Regeneration;

    public float DamageTaken => Stats.DamageTaken;

    public float BlockChance => Stats.BlockChance;

    public bool KeepsOwnBarrier => false;

    public string DashLabel => "DASH";

    public float DashReadiness => _controller.DashReadiness;

    public Vector3D<float> DashVelocity => _controller.DashVelocity;

    public string? Status => _bow.FocusReady(Stats) ? "FOCUSED" : null;   // Sniper's Focus: the next shot is the big one

    public Enemy? AimTarget => _bow.Target;

    /// <summary>The active tree's ranks become its bonuses; the other tree's are emptied, so nothing of it counts.</summary>
    public void UseTree(IReadOnlyDictionary<string, int> ranks)
    {
        Stats.TrapperActive = Tree.Id == TrapperTree.TreeId;
        Stats.Tree = Stats.TrapperActive ? new SharpshooterBonuses() : SharpshooterBonuses.From(ranks);
        Stats.Trapper = Stats.TrapperActive ? TrapperBonuses.From(ranks) : new TrapperBonuses();
    }

    public void BeginRun(ItemBonuses items, PlayerHealth health)
    {
        Stats.Reset();
        Stats.Items = items;
        health.Reset(Stats.MaxHealth);
        _recentKills.Clear();
        _banished.Clear();
        _kit.Reset();
        _snareDue = null;
    }

    public void ReturnToCamp() => Stats.Reset();

    public void Move(EngineWindow window, float deltaSeconds, bool stunned)
    {
        _shootingLeft = MathF.Max(0f, _shootingLeft - deltaSeconds);
        bool shooting = _shootingLeft > 0f;
        _controller.Update(window, deltaSeconds, Stats, stunned, shooting ? _bow.DrawProgress(Stats) : null, shooting ? _bow.AimFlat(window.PlayerFeet) : null);
        if (_controller.DashedFrom is { } from)
        {
            _snareDue = from;   // set on the run's next attack (never at camp, where there is none)
        }
    }

    public void Hide(EngineWindow window) => _controller.Hide(window);

    public void Attack(RunFrame frame)
    {
        while (_recentKills.Count > 0 && _recentKills.Peek() < frame.RunSeconds - MomentumWindow - Stats.Items.Duration)
        {
            _recentKills.Dequeue();
        }

        Stats.MomentumStacks = Math.Min(RangerStats.MaxMomentumStacks, _recentKills.Count);
        _shootingLeft = ShootingHold;

        foreach (var hit in _bow.Update(frame.Window, frame.DeltaSeconds, Stats, frame.Enemies, frame.Numbers, frame.GroundAt,
                     canFire: !frame.Condition.IsStunned, frame.StandingStill))
        {
            if (hit.Crit && hit.Enemy.Kind.Tier != EnemyTier.Fodder)
            {
                frame.Health.Heal(Stats.Tree.EliteCritHeal);   // Headhunter
            }
        }

        if (_snareDue is { } dashedFrom)
        {
            _kit.LaySnare(dashedFrom);
            _snareDue = null;
        }

        _trapperHits.Clear();
        _kit.Update(frame.DeltaSeconds, frame.Window.PlayerFeet, frame.Enemies, canAct: !frame.Condition.IsStunned, _trapperHits);
        foreach (var hit in _trapperHits)
        {
            if (hit.Source is TrapperSource.Poison or TrapperSource.Caltrops)
            {
                frame.Numbers.AddOverTime(hit.Enemy, hit.Position, hit.Damage, hit.Killed);   // too often to number one by one
            }
            else
            {
                frame.Numbers.Add(hit.Position, hit.Damage, hit.Killed, hit.Crit);
            }
        }

        _view.Sync(frame.Window, _kit, frame.DeltaSeconds, frame.GroundAt);
    }

    public void Answer(RunFrame frame)
    {
    }

    public void OnKill(Enemy killed, float runSeconds) => _recentKills.Enqueue(runSeconds);

    public void Clear(EngineWindow window)
    {
        _bow.Clear(window);
        _kit.Reset();
        _view.Clear(window);
        _snareDue = null;
    }

    public IReadOnlyList<LevelUpCard> RollLevelUp(Random random)
    {
        _choices = RangerUpgrades.Roll(Stats, random, excluded: _banished);
        return Cards();
    }

    public IReadOnlyList<LevelUpCard> BanishCard(int index, Random random)
    {
        if (index >= 0 && index < _choices.Count && _choices[index].Upgrade is { } banished)
        {
            _banished.Add(banished);
            _choices = RangerUpgrades.Replace(_choices, index, Stats, random, _banished);
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
            health.Heal(RangerUpgrades.SecondWindHeal);
        }
    }

    private List<LevelUpCard> Cards() =>
        _choices.Select(c => new LevelUpCard(c.Name, c.Description, c.NewLevel, c.MaxLevel, IsHeal: c.Upgrade is null)).ToList();
}
