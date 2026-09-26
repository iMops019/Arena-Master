using ArenaMaster.Game.Classes;
using ArenaMaster.Game.Combat;
using ArenaMaster.Game.Items;
using ArenaMaster.Game.Progression;
using ArenaMaster.Game.Ui;
using CEngine.Core;
using Silk.NET.Maths;

namespace ArenaMaster.Game.Warrior;

/// <summary>
/// The Warrior as the content runs it (see <see cref="IHeroClass"/>): two axes, swung one after the other on their own, each sending a Cleave - a wedge-shaped wave
/// - rolling out where the Warrior faces (<see cref="CleaveAxes"/>). The Berserker tree's Berserking turns hits into rage (<see cref="Fury"/>) that speeds and
/// strengthens the swings. Here too is what answers kills and the enemies' blows: life leech, Blood Frenzy, blocking with the axes, Riposte, Fuelled by Pain,
/// Blade Wall and Last Stand.
/// </summary>
internal sealed class WarriorClass : IHeroClass
{
    /// <summary>How long "BLOCKED" and "LAST STAND" stay under the crosshair.</summary>
    private const float BlockedShown = 0.45f;
    private const float LastStandShown = 2.5f;

    private readonly WarriorController _controller = new();
    private readonly CleaveAxes _axes;
    private readonly WarriorView _view = new();
    private readonly Fury _fury = new();
    private readonly List<float> _frenzy = new();
    private readonly HashSet<WarriorUpgrade> _banished = new();
    private readonly List<CleaveHit> _hits = new();
    private List<WarriorChoice> _choices = new();
    private float _blockedShown;
    private float _lastStandShown;

    /// <summary>Last Stand, not yet used this run. Any other last stand (an item's) is the content's to answer.</summary>
    private int _lastStandLeft;

    /// <summary>How long after the last attack frame the body still holds its fighting stance (a run's attacks come after the body in each frame).</summary>
    private const float AttackingHold = 0.25f;

    private float _attackingLeft;

    public WarriorClass(Random random) => _axes = new CleaveAxes(random);

    public WarriorStats Stats { get; } = new();

    /// <summary>The axes and their waves, and the rage, for the tests.</summary>
    internal CleaveAxes Axes => _axes;

    internal Fury Fury => _fury;

    public string Id => BerserkerTree.ClassId;

    public string Name => "Warrior";

    public string Summary =>
        "Two axes, swung one after the other on their own. Each swing sends a Cleave rolling out in front of you, a wave that cuts through everything it passes. The Berserker turns every hit into rage.";

    public TreeDefinition Tree => BerserkerTree.Tree;

    public float MaxHealth => Stats.MaxHealth;

    public float PickupRadius => Stats.PickupRadius;

    public float Regeneration => Stats.Regeneration;

    public float DamageTaken => Stats.DamageTaken;

    public float BlockChance => Stats.BlockChance;

    public bool KeepsOwnBarrier => false;

    public string DashLabel => "CHARGE";

    public float DashReadiness => _controller.ChargeReadiness;

    public Vector3D<float> DashVelocity => _controller.ChargeVelocity;

    public string? Status =>
        _lastStandShown > 0f ? "LAST STAND"
        : _blockedShown > 0f ? "BLOCKED"
        : Stats.Tree.Berserking && _fury.Rage > 0 ? $"RAGE {_fury.Rage} / {Stats.MaxRage}"
        : null;

    public void UseTree(IReadOnlyDictionary<string, int> ranks) => Stats.Tree = BerserkerBonuses.From(ranks);

    public void BeginRun(ItemBonuses items, PlayerHealth health)
    {
        Stats.Reset();
        Stats.Items = items;
        health.Reset(Stats.MaxHealth);
        _lastStandLeft = Stats.Tree.LastStand ? 1 : 0;
        health.LastStands = _lastStandLeft;
        _banished.Clear();
        _axes.Reset();
        _fury.Reset();
        _frenzy.Clear();
        _blockedShown = 0f;
        _lastStandShown = 0f;
    }

    public void ReturnToCamp()
    {
        Stats.Reset();
        _fury.Reset();
        _frenzy.Clear();
    }

    public void Move(EngineWindow window, float deltaSeconds, bool stunned)
    {
        _attackingLeft = MathF.Max(0f, _attackingLeft - deltaSeconds);
        _controller.Update(window, deltaSeconds, Stats, stunned, _attackingLeft > 0f ? AttackTime() : null);
    }

    /// <summary>
    /// Where the body's attack clip is, in time with the swings: wound up over the last moments before each one, through the blow just
    /// after (the clip's middle is the moment it lands), and ready in between.
    /// </summary>
    private float AttackTime()
    {
        float interval = Stats.SwingInterval;
        float until = _axes.SwingIn;
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
        var feet = frame.Window.PlayerFeet;
        var ground = new Vector3D<float>(feet.X, frame.GroundAt(feet.X, feet.Z) ?? feet.Y, feet.Z);
        Fight(frame.DeltaSeconds, ground, WarriorController.FacingYaw(frame.Window), frame.Condition.IsStunned, frame.Enemies, frame.Health, frame.Numbers);
        _view.Sync(frame.Window, _axes, frame.GroundAt);
    }

    public void Answer(RunFrame frame) => AnswerStrikes(frame.Enemies.Strikes, frame.Enemies, frame.Health, frame.Numbers);

    /// <summary>
    /// The attacks for one frame, before the enemies move: rage and Blood Frenzy wearing off, the axes swinging toward <paramref name="facingYaw"/> (held by a stun) and
    /// their waves rolling on; then every enemy a wave hit adds a rage, and the damage dealt heals by the life leech.
    /// </summary>
    internal void Fight(float deltaSeconds, Vector3D<float> feet, float facingYaw, bool stunned, EnemyField enemies, PlayerHealth health, DamageNumbers numbers)
    {
        _blockedShown = MathF.Max(0f, _blockedShown - deltaSeconds);
        _lastStandShown = MathF.Max(0f, _lastStandShown - deltaSeconds);
        _fury.Update(deltaSeconds, Stats.RageDrains);
        for (int i = _frenzy.Count - 1; i >= 0; i--)
        {
            _frenzy[i] -= deltaSeconds;
            if (_frenzy[i] <= 0f)
            {
                _frenzy.RemoveAt(i);
            }
        }

        SyncStats();
        _hits.Clear();
        _axes.Update(deltaSeconds, feet, facingYaw, Stats, enemies, canSwing: !stunned, _hits);

        int struck = 0;
        float dealt = 0f;
        foreach (var hit in _hits)
        {
            if (hit.Source == CleaveSource.Cleave && !hit.Enemy.Kind.IsProp)
            {
                struck++;
                dealt += hit.Damage;
            }
        }

        _fury.Gain(struck, Stats);   // Berserking: +1 rage for every enemy hit
        health.Heal(dealt * Stats.LifeLeech);
        SyncStats();
        Report(numbers);
    }

    /// <summary>
    /// The enemies' blows this frame, answered: a block heals, strikes back (Riposte) and, with Blade Wall, gives rage and heals more; any blow that reaches the Warrior
    /// gives rage with Fuelled by Pain; a last stand (Last Stand) heals.
    /// </summary>
    internal void AnswerStrikes(IReadOnlyList<Strike> strikes, EnemyField enemies, PlayerHealth health, DamageNumbers numbers)
    {
        _hits.Clear();
        foreach (var strike in strikes)
        {
            if (strike.Blocked)
            {
                _blockedShown = BlockedShown;
                health.Heal(Stats.Tree.BlockHeal);
                if (Stats.Tree.Riposte)
                {
                    _axes.Hurt(strike.Attacker, Stats.CleaveDamage * WarriorStats.RiposteDamage, crit: false, CleaveSource.Riposte, Stats, enemies, _hits);
                }

                if (Stats.Tree.BladeWall)
                {
                    _fury.Gain(WarriorStats.BladeWallRage, Stats);
                    health.Heal(health.Max * WarriorStats.BladeWallHeal);
                }
            }

            if (Stats.Tree.FuelledByPain)
            {
                _fury.Gain(WarriorStats.PainRage, Stats);
            }
        }

        if (_lastStandLeft > 0 && health.TakeLastStand())
        {
            _lastStandLeft--;
            health.Heal(health.Max * WarriorStats.LastStandHeal);
            _lastStandShown = LastStandShown;
        }

        SyncStats();
        Report(numbers);
    }

    /// <summary>A kill: rage from an item (Bloodfury), and with Blood Frenzy, a little more attack speed for a while.</summary>
    public void OnKill(Enemy killed, float runSeconds)
    {
        _fury.Gain(Stats.Items.RageOnKill, Stats);
        SyncStats();
        if (Stats.Tree.BloodFrenzy && _frenzy.Count < (int)MathF.Round(WarriorStats.FrenzyCap / WarriorStats.FrenzyPerKill))
        {
            _frenzy.Add(WarriorStats.FrenzySeconds);
        }
    }

    public void Clear(EngineWindow window)
    {
        _axes.Reset();
        _view.Clear(window);
        _fury.Reset();
        _frenzy.Clear();
        SyncStats();
    }

    public IReadOnlyList<LevelUpCard> RollLevelUp(Random random)
    {
        _choices = WarriorUpgrades.Roll(Stats, random, excluded: _banished);
        return Cards();
    }

    public IReadOnlyList<LevelUpCard> BanishCard(int index, Random random)
    {
        if (index >= 0 && index < _choices.Count && _choices[index].Upgrade is { } banished)
        {
            _banished.Add(banished);
            _choices = WarriorUpgrades.Replace(_choices, index, Stats, random, _banished);
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
            health.Heal(WarriorUpgrades.SecondWindHeal);
        }
    }

    /// <summary>The rage and the frenzy as they stand, into the stats that read them.</summary>
    private void SyncStats()
    {
        Stats.Rage = _fury.Rage;
        Stats.Frenzy = WarriorStats.FrenzyPerKill * _frenzy.Count;
    }

    private void Report(DamageNumbers numbers)
    {
        foreach (var hit in _hits)
        {
            numbers.Add(hit.Position, hit.Damage, hit.Killed, hit.Crit);
        }
    }

    private List<LevelUpCard> Cards() =>
        _choices.Select(c => new LevelUpCard(c.Name, c.Description, c.NewLevel, c.MaxLevel, IsHeal: c.Upgrade is null)).ToList();
}
