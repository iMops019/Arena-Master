using ArenaMaster.Game.Classes;
using ArenaMaster.Game.Combat;
using ArenaMaster.Game.Items;
using ArenaMaster.Game.Progression;
using ArenaMaster.Game.Ui;
using CEngine.Core;
using Silk.NET.Maths;

namespace ArenaMaster.Game.Warrior;

/// <summary>
/// The Warrior as the content runs it (see <see cref="IHeroClass"/>): two axes, used one after the other on their own at the nearest enemy, the body turned to it.
/// The attack follows the active tree: with the Berserker each swing sends a Cleave - a wedge-shaped wave - rolling out once the enemy is in reach
/// (<see cref="CleaveAxes"/>), and Berserking turns hits into rage (<see cref="Fury"/>) that speeds and strengthens the swings; with the Reaver the axes are thrown
/// instead, spinning out and back and leaving wounds that bleed (<see cref="ThrownAxes"/>). Here too is what answers kills and the enemies' blows: life leech, Blood
/// Frenzy, blocking with the axes, Riposte, Fuelled by Pain, Blade Wall and Last Stand; and the Reaver's pools of blood, Blood Scent and Harvester.
/// </summary>
internal sealed class WarriorClass : IHeroClass
{
    /// <summary>How long "BLOCKED" and "LAST STAND" stay under the crosshair.</summary>
    private const float BlockedShown = 0.45f;
    private const float LastStandShown = 2.5f;

    private readonly WarriorController _controller = new();
    private readonly CleaveAxes _axes;
    private readonly ThrownAxes _throws;
    private readonly WarriorView _view = new();
    private readonly ReaverView _reaverView = new();
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

    /// <summary>The flat way to the enemy it fights, as of the last attack frame, or null with none: the body faces it.</summary>
    private Vector3D<float>? _aim;

    /// <summary>The nearest enemy, which the body faces and the axes swing at (or are thrown at) once it is in reach; null with none near.</summary>
    private Enemy? _facing;

    /// <summary>The body turns to the nearest enemy within this many metres (further, if the axes are thrown further).</summary>
    private const float FaceRange = 15f;

    /// <summary>How far past the waves' reach an enemy may be and still be swung at: it walks into the wave as it rolls out.</summary>
    private const float SwingMargin = 1f;

    public WarriorClass(Random random)
    {
        _axes = new CleaveAxes(random);
        _throws = new ThrownAxes(random);
    }

    public WarriorStats Stats { get; } = new();

    /// <summary>The axes and their waves, the thrown axes, and the rage, for the tests.</summary>
    internal CleaveAxes Axes => _axes;

    internal ThrownAxes Throws => _throws;

    internal Fury Fury => _fury;

    public string Id => BerserkerTree.ClassId;

    public string Name => "Warrior";

    public string Summary =>
        "Two axes, used one after the other on their own at the nearest enemy. The Berserker swings them, each swing a Cleave rolling out, a wave that cuts through "
        + "everything it passes, and turns every hit into rage. The Reaver throws them, spinning out and back, and leaves wounds that bleed.";

    public IReadOnlyList<TreeDefinition> Trees { get; } = new[] { BerserkerTree.Tree, ReaverTree.Tree };

    public TreeDefinition Tree { get; private set; } = BerserkerTree.Tree;

    public void ChooseTree(string treeId)
    {
        Tree = Trees.FirstOrDefault(t => t.Id == treeId) ?? Trees[0];
        Stats.Throwing = Tree.Id == ReaverTree.TreeId;
    }

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
        : Stats.Throwing && _throws.Storming ? "AXE STORM"
        : null;

    public Enemy? AimTarget => _attackingLeft > 0f ? _facing : null;

    /// <summary>The active tree's ranks into its bonuses; the other tree's are left empty, so nothing of it counts.</summary>
    public void UseTree(IReadOnlyDictionary<string, int> ranks)
    {
        Stats.Throwing = Tree.Id == ReaverTree.TreeId;
        Stats.Tree = Stats.Throwing ? new BerserkerBonuses() : BerserkerBonuses.From(ranks);
        Stats.Reaver = Stats.Throwing ? ReaverBonuses.From(ranks) : new ReaverBonuses();
    }

    public void BeginRun(ItemBonuses items, PlayerHealth health)
    {
        Stats.Reset();
        Stats.Items = items;
        health.Reset(Stats.MaxHealth);
        _lastStandLeft = Stats.Tree.LastStand ? 1 : 0;
        health.LastStands = _lastStandLeft;
        _banished.Clear();
        _axes.Reset();
        _throws.Reset();
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
        bool fighting = _attackingLeft > 0f;
        _controller.Update(window, deltaSeconds, Stats, stunned, fighting ? AttackTime() : null, fighting ? _aim : null);
    }

    /// <summary>
    /// Where the body's attack clip is, in time with the swings (or throws): wound up over the last moments before each one, through the blow just after (the clip's
    /// middle is the moment it lands), and ready in between.
    /// </summary>
    private float AttackTime()
    {
        float interval = Stats.SwingInterval;
        float until = Stats.Throwing ? _throws.ThrowIn : _axes.SwingIn;
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
        float reach = Stats.Throwing ? Stats.ThrowReach : Stats.Reach + SwingMargin;
        _facing = frame.Enemies.Nearest(feet, Stats.Throwing ? MathF.Max(FaceRange, reach) : FaceRange);
        float? attackYaw = null;
        _aim = null;
        if (_facing is { } nearest)
        {
            var toward = Geometry.FlatDirection(feet, nearest.Position, out float distance);
            if (toward != Vector3D<float>.Zero)
            {
                _aim = toward;
                if (distance - nearest.Kind.Radius <= reach)
                {
                    attackYaw = MathF.Atan2(toward.X, toward.Z);
                }
            }
        }

        Fight(frame.DeltaSeconds, ground, attackYaw, frame.Condition.IsStunned, frame.Enemies, frame.Health, frame.Numbers, _controller.Charging);
        _view.Sync(frame.Window, _axes, frame.GroundAt);
        _reaverView.Sync(frame.Window, _throws, frame.DeltaSeconds, frame.GroundAt);
    }

    public void Answer(RunFrame frame) => AnswerStrikes(frame.Enemies.Strikes, frame.Enemies, frame.Health, frame.Numbers);

    /// <summary>
    /// The attacks for one frame, before the enemies move: rage and Blood Frenzy wearing off, and the axes used toward <paramref name="facingYaw"/> (held by a stun, and
    /// waiting with nothing in reach: null) - swung, their waves rolling on, or thrown, flying on (and, while <paramref name="charging"/>, Run Them Down's charge); then
    /// every enemy a wave hit adds a rage, the damage the axes dealt heals by the life leech, and a pool of blood underfoot heals (Bloodbath).
    /// </summary>
    internal void Fight(float deltaSeconds, Vector3D<float> feet, float? facingYaw, bool stunned, EnemyField enemies, PlayerHealth health, DamageNumbers numbers,
                        bool charging = false)
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

        SyncStats(feet);
        _hits.Clear();
        if (Stats.Throwing)
        {
            _throws.Update(deltaSeconds, feet, facingYaw, Stats, enemies, canThrow: !stunned, charging, _hits);
            if (Stats.Reaver.Bloodbath && _throws.InPool(feet))
            {
                health.Heal(health.Max * WarriorStats.PoolHeal * deltaSeconds);
            }
        }
        else
        {
            _axes.Update(deltaSeconds, feet, facingYaw, Stats, enemies, canSwing: !stunned, _hits);
        }

        int struck = 0;
        float dealt = 0f;
        foreach (var hit in _hits)
        {
            if ((hit.Source is CleaveSource.Cleave or CleaveSource.Throw or CleaveSource.Charge) && !hit.Enemy.Kind.IsProp)
            {
                struck += hit.Source == CleaveSource.Cleave ? 1 : 0;
                dealt += hit.Damage;
            }
        }

        _fury.Gain(struck, Stats);   // Berserking: +1 rage for every enemy a wave hit
        health.Heal(dealt * Stats.LifeLeech);
        SyncStats(feet);
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

        SyncStats(null);
        Report(numbers);
    }

    /// <summary>
    /// A kill: rage from an item (Bloodfury); with Blood Frenzy, a little more attack speed for a while; with Harvester, every so often a free axe.
    /// </summary>
    public void OnKill(Enemy killed, float runSeconds)
    {
        _fury.Gain(Stats.Items.RageOnKill, Stats);
        SyncStats(null);
        if (Stats.Tree.BloodFrenzy && _frenzy.Count < (int)MathF.Round(WarriorStats.FrenzyCap / WarriorStats.FrenzyPerKill))
        {
            _frenzy.Add(WarriorStats.FrenzySeconds);
        }

        if (Stats.Throwing)
        {
            _throws.CountKill(Stats);
        }
    }

    public void Clear(EngineWindow window)
    {
        _axes.Reset();
        _throws.Reset();
        _view.Clear(window);
        _reaverView.Clear(window);
        _fury.Reset();
        _frenzy.Clear();
        SyncStats(null);
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

    /// <summary>
    /// The rage, the frenzy and (with <paramref name="feet"/>, where the Warrior stands) Blood Scent as they stand, into the stats that read them. Without
    /// <paramref name="feet"/> the scent is left as it was.
    /// </summary>
    private void SyncStats(Vector3D<float>? feet)
    {
        Stats.Rage = _fury.Rage;
        Stats.Frenzy = WarriorStats.FrenzyPerKill * _frenzy.Count;
        if (feet is { } at)
        {
            Stats.Scent = Stats.Reaver.BloodScent
                ? MathF.Min(WarriorStats.ScentCap, WarriorStats.ScentPer * _throws.BleedingNear(at, WarriorStats.ScentRange))
                : 0f;
        }
    }

    /// <summary>The axes' hits as damage numbers; bleeding ticks too often to number one by one, and is added up for each enemy instead.</summary>
    private void Report(DamageNumbers numbers)
    {
        foreach (var hit in _hits)
        {
            if (hit.Source == CleaveSource.Bleed)
            {
                numbers.AddOverTime(hit.Enemy, hit.Position, hit.Damage, hit.Killed);
            }
            else
            {
                numbers.Add(hit.Position, hit.Damage, hit.Killed, hit.Crit);
            }
        }
    }

    private List<LevelUpCard> Cards() =>
        _choices.Select(c => new LevelUpCard(c.Name, c.Description, c.NewLevel, c.MaxLevel, IsHeal: c.Upgrade is null)).ToList();
}
