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
/// to - Momentum's recent kills and Headhunter's heal on an elite crit.
/// </summary>
internal sealed class RangerClass : IHeroClass
{
    /// <summary>How long a kill counts toward Momentum.</summary>
    private const float MomentumWindow = 3f;

    private readonly RangerController _controller = new();
    private readonly RangerBow _bow;
    private readonly Queue<float> _recentKills = new();
    private readonly HashSet<RangerUpgrade> _banished = new();
    private List<UpgradeChoice> _choices = new();

    public RangerClass(Random random) => _bow = new RangerBow(random);

    public RangerStats Stats { get; } = new();

    public string Id => SharpshooterTree.ClassId;

    public string Name => "Ranger";

    public string Summary => "A bow that fires on its own at whatever the crosshair is on. Fast, fragile, and all about aim: crits, pierce and chains.";

    public TreeDefinition Tree => SharpshooterTree.Tree;

    public float MaxHealth => Stats.MaxHealth;

    public float PickupRadius => Stats.PickupRadius;

    public float Regeneration => Stats.Regeneration;

    public float DamageTaken => Stats.DamageTaken;

    public float BlockChance => 0f;

    public string DashLabel => "DASH";

    public float DashReadiness => _controller.DashReadiness;

    public Vector3D<float> DashVelocity => _controller.DashVelocity;

    public string? Status => _bow.FocusReady(Stats) ? "FOCUSED" : null;   // Sniper's Focus: the next shot is the big one

    public void UseTree(IReadOnlyDictionary<string, int> ranks) => Stats.Tree = SharpshooterBonuses.From(ranks);

    public void BeginRun(ItemBonuses items, PlayerHealth health)
    {
        Stats.Reset();
        Stats.Items = items;
        health.Reset(Stats.MaxHealth);
        _recentKills.Clear();
        _banished.Clear();
    }

    public void ReturnToCamp() => Stats.Reset();

    public void Move(EngineWindow window, float deltaSeconds, bool stunned) => _controller.Update(window, deltaSeconds, Stats, stunned);

    public void Hide(EngineWindow window) => _controller.Hide(window);

    public void Attack(RunFrame frame)
    {
        while (_recentKills.Count > 0 && _recentKills.Peek() < frame.RunSeconds - MomentumWindow)
        {
            _recentKills.Dequeue();
        }

        Stats.MomentumStacks = Math.Min(RangerStats.MaxMomentumStacks, _recentKills.Count);

        foreach (var hit in _bow.Update(frame.Window, frame.DeltaSeconds, Stats, frame.Enemies, frame.Numbers, frame.GroundAt,
                     canFire: !frame.Condition.IsStunned, frame.StandingStill))
        {
            if (hit.Crit && hit.Enemy.Kind.Tier != EnemyTier.Fodder)
            {
                frame.Health.Heal(Stats.Tree.EliteCritHeal);   // Headhunter
            }
        }
    }

    public void Answer(RunFrame frame)
    {
    }

    public void OnKill(Enemy killed, float runSeconds) => _recentKills.Enqueue(runSeconds);

    public void Clear(EngineWindow window) => _bow.Clear(window);

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
