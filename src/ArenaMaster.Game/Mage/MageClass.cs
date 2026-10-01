using ArenaMaster.Game.Classes;
using ArenaMaster.Game.Combat;
using ArenaMaster.Game.Items;
using ArenaMaster.Game.Progression;
using ArenaMaster.Game.Ui;
using CEngine.Core;
using Silk.NET.Maths;

namespace ArenaMaster.Game.Mage;

/// <summary>
/// The Mage as the content runs it (see <see cref="IHeroClass"/>): a staff of ice or fire, by the active tree. The barrage (<see cref="FrostBarrage"/>) sends a
/// volley of homing bolts off the staff one after another; with Frost active every hit chills, with Pyromancy every hit burns (<see cref="Flames"/>). Here too is
/// the Frost Shield - a barrier on the health that forms on its own every so often and holds for a few seconds (Shattering Ward bursts when it breaks, Glacial
/// Fortress keeps it until it does) - and Ice Block, the once-a-run last stand; and the Pyromancy tree's answers: the heat gauge (<see cref="Meter"/>) vented by
/// blinking (healing, with Cauterise), Fire Walk's line of fire along a blink, and Flame Ward burning a blow away.
/// </summary>
internal sealed class MageClass : IHeroClass
{
    /// <summary>Where on the Mage the bolts leave from: the staff's head, this high above the feet and this far ahead.</summary>
    private const float StaffHeight = 1.55f;
    private const float StaffForward = 0.45f;

    /// <summary>The first Frost Shield of a run forms this soon.</summary>
    private const float FirstShield = 2f;

    /// <summary>How long "ICE BLOCK" stays under the crosshair.</summary>
    private const float IceBlockShown = 2.5f;

    /// <summary>How long "FLAME WARD" stays under the crosshair after it burns a blow away.</summary>
    private const float FlameWardShown = 1.5f;

    private readonly MageController _controller = new();
    private readonly FrostBarrage _barrage;
    private readonly FrostView _view = new();
    private readonly FireView _fireView = new();
    private readonly HashSet<MageUpgrade> _banished = new();
    private readonly List<FrostHit> _hits = new();
    private List<MageChoice> _choices = new();
    private bool _shieldUp;
    private float _shieldLeft;
    private float _shieldIn = FirstShield;
    private float _iceBlockShown;

    /// <summary>Flame Ward: seconds until it is ready again (0: ready), and how long "FLAME WARD" is still shown.</summary>
    private float _wardIn;
    private float _wardShown;

    /// <summary>The controller's blink count as of the last run frame, so a new blink is seen once; and whether a line of fire is still being laid along one.</summary>
    private int _blinksSeen;
    private bool _laying;

    /// <summary>Ice Block's last stand, not yet used this run. Any other last stand (an item's) is the content's to answer.</summary>
    private int _iceBlockLeft;

    /// <summary>How long after the last attack frame the body still holds its fighting stance (a run's attacks come after the body in each frame).</summary>
    private const float AttackingHold = 0.25f;

    private float _attackingLeft;

    /// <summary>The flat way to the enemy it fights, as of the last attack frame, or null with none: the body faces it.</summary>
    private Vector3D<float>? _aim;

    public MageClass(Random random) => _barrage = new FrostBarrage(random);

    public MageStats Stats { get; } = new();

    /// <summary>The bolts and bursts, for the tests.</summary>
    internal FrostBarrage Barrage => _barrage;

    /// <summary>Whether a Frost Shield (or Ice Block's shield) is holding.</summary>
    public bool ShieldUp => _shieldUp;

    public string Id => FrostTree.ClassId;

    public string Name => "Mage";

    /// <summary>How it fights, for the active tree: a staff of ice with Frost, of fire with Pyromancy.</summary>
    public string Summary => Stats.Fire
        ? "A staff of fire. Fire Barrage sends three bolts off the staff one after another, each seeking out an enemy and setting it burning. "
            + "Fragile, but quick to blink away."
        : "A staff of ice. Frost Barrage sends three bolts off the staff one after another, each seeking out an enemy, and the cold slows what it hits. "
            + "Fragile, but quick to blink away.";

    public IReadOnlyList<TreeDefinition> Trees { get; } = new[] { FrostTree.Tree, PyromancyTree.Tree };

    public TreeDefinition Tree { get; private set; } = FrostTree.Tree;

    /// <summary>Makes a tree active, and with it the barrage's element: fire with Pyromancy, ice with Frost.</summary>
    public void ChooseTree(string treeId)
    {
        Tree = Trees.FirstOrDefault(t => t.Id == treeId) ?? Trees[0];
        Stats.Element = Tree.Id == PyromancyTree.TreeId ? MageElement.Fire : MageElement.Frost;
    }

    /// <summary>The heat, with the Pyromancy tree's Heat: how hot, or that it has overheated (or become the Inferno).</summary>
    public (string Label, float Fill)? Meter
    {
        get
        {
            if (!Stats.Fire || !Stats.Pyro.Heat)
            {
                return null;
            }

            var flames = _barrage.Flames;
            string label = flames.InInferno ? "INFERNO" : flames.OverheatLeft > 0f ? "OVERHEATED" : $"HEAT {flames.Heat:0}%";
            return (label, flames.Heat / MageStats.MaxHeat);
        }
    }

    public float MaxHealth => Stats.MaxHealth;

    public float PickupRadius => Stats.PickupRadius;

    public float Regeneration => Stats.Regeneration;

    public float DamageTaken => Stats.DamageTaken;

    /// <summary>The items' block chance - or, while Flame Ward is ready, every blow: the next one is burned away.</summary>
    public float BlockChance => FlameWardReady ? 1f : Stats.BlockChance;

    /// <summary>Whether Flame Ward will burn the next blow away.</summary>
    public bool FlameWardReady => Stats.Fire && Stats.Pyro.FlameWard && _wardIn <= 0f;

    public bool KeepsOwnBarrier => Stats.Tree.FrostShield;

    public string DashLabel => "BLINK";

    public float DashReadiness => _controller.BlinkReadiness;

    public Vector3D<float> DashVelocity => _controller.BlinkVelocity;

    public string? Status => _iceBlockShown > 0f ? "ICE BLOCK" : _wardShown > 0f ? "FLAME WARD" : null;

    /// <summary>The active tree's ranks count; the other tree's bonuses are left empty, so nothing of it counts.</summary>
    public void UseTree(IReadOnlyDictionary<string, int> ranks)
    {
        bool fire = Tree.Id == PyromancyTree.TreeId;
        Stats.Element = fire ? MageElement.Fire : MageElement.Frost;
        Stats.Tree = fire ? new FrostBonuses() : FrostBonuses.From(ranks);
        Stats.Pyro = fire ? PyromancyBonuses.From(ranks) : new PyromancyBonuses();
    }

    public void BeginRun(ItemBonuses items, PlayerHealth health)
    {
        Stats.Reset();
        Stats.Items = items;
        health.Reset(Stats.MaxHealth);
        _iceBlockLeft = Stats.Tree.IceBlock ? 1 : 0;
        health.LastStands = _iceBlockLeft;
        _banished.Clear();
        _barrage.Reset();
        _shieldUp = false;
        _shieldLeft = 0f;
        _shieldIn = FirstShield;
        _iceBlockShown = 0f;
        _wardIn = 0f;
        _wardShown = 0f;
        _blinksSeen = _controller.Blinks;
        _laying = false;
    }

    public void ReturnToCamp()
    {
        Stats.Reset();
        _shieldUp = false;
    }

    public void Move(EngineWindow window, float deltaSeconds, bool stunned)
    {
        _attackingLeft = MathF.Max(0f, _attackingLeft - deltaSeconds);
        bool fighting = _attackingLeft > 0f;
        _controller.Update(window, deltaSeconds, Stats, stunned, fighting ? AttackTime() : null, fighting ? _aim : null);
    }

    /// <summary>
    /// Where the body's attack clip is, in time with the barrages: through the blow just after (the clip's middle is the moment it lands), and ready
    /// in between (it waits for something to aim at, so there is no wind-up).
    /// </summary>
    private float AttackTime()
    {
        float interval = Stats.BarrageInterval;
        float until = _barrage.BarrageIn;
        float since = interval - until;
        float follow = MathF.Min(0.45f, 0.5f * interval);
        if (since >= 0f && since < follow)
        {
            return 0.5f + 0.5f * since / follow;
        }

        return 0f;
    }

    public void Hide(EngineWindow window) => _controller.Hide(window);

    public void Attack(RunFrame frame)
    {
        _attackingLeft = AttackingHold;
        var feet = frame.Window.PlayerFeet;
        _aim = frame.Enemies.Nearest(feet, Stats.TargetRange) is { } nearest ? Geometry.FlatDirection(feet, nearest.Position, out _) : null;
        FollowBlink(feet, frame.Health);
        Fight(frame.DeltaSeconds, feet, _controller.Facing, frame.Condition.IsStunned, frame.Enemies, frame.GroundAt, frame.Health, frame.Numbers);
        _view.Sync(frame.Window, _barrage, feet, _shieldUp, Stats.Tree.Blizzard, MageStats.BlizzardRadius * Stats.AreaScale, frame.DeltaSeconds, Stats.Fire);
        _fireView.Sync(frame.Window, _barrage, feet, Stats.Fire, Stats.Heat, MageStats.InfernoRadius * Stats.AreaScale, frame.DeltaSeconds);
    }

    /// <summary>
    /// A blink that began since the last run frame (see <see cref="Blinked"/>), and a line of fire drawn on behind one under way (Fire Walk) until it ends.
    /// </summary>
    private void FollowBlink(Vector3D<float> feet, PlayerHealth health)
    {
        if (_laying)
        {
            _barrage.Flames.ExtendLine(feet);
        }

        if (_controller.Blinks != _blinksSeen)
        {
            _blinksSeen = _controller.Blinks;
            Blinked(feet, health);
        }
        else if (_laying && !_controller.Blinking)
        {
            _barrage.Flames.CloseLine();
            _laying = false;
        }
    }

    /// <summary>
    /// A blink begins at <paramref name="feet"/>, with the Fire Barrage: it vents the heat (healing with Cauterise), and with Fire Walk starts a line of fire along
    /// the way it goes.
    /// </summary>
    internal void Blinked(Vector3D<float> feet, PlayerHealth health)
    {
        if (!Stats.Fire)
        {
            return;
        }

        float vented = _barrage.Flames.Vent(Stats);
        if (Stats.Pyro.Cauterise && vented > 0f)
        {
            health.Heal(vented / MageStats.CauteriseHeatPerHealth);
        }

        if (Stats.Pyro.FireWalk)
        {
            _barrage.Flames.StartLine(feet, Stats);
            _laying = true;
        }
    }

    public void Answer(RunFrame frame) => AfterBlows(frame.Window.PlayerFeet, frame.Enemies, frame.Health, frame.Numbers);

    /// <summary>
    /// The attacks for one frame, before the enemies move: the barrage from the staff (held by a stun) and the Blizzard, and the Frost Shield forming or fading.
    /// <paramref name="aimFlat"/> is where the Mage faces (toward its nearest enemy).
    /// </summary>
    internal void Fight(float deltaSeconds, Vector3D<float> feet, Vector3D<float> aimFlat, bool stunned, EnemyField enemies, Func<float, float, float?> groundAt,
        PlayerHealth health, DamageNumbers numbers)
    {
        _iceBlockShown = MathF.Max(0f, _iceBlockShown - deltaSeconds);
        _wardShown = MathF.Max(0f, _wardShown - deltaSeconds);
        _wardIn = MathF.Max(0f, _wardIn - deltaSeconds);
        var staff = feet + new Vector3D<float>(0f, StaffHeight, 0f) + aimFlat * StaffForward;

        _hits.Clear();
        _barrage.Update(deltaSeconds, staff, feet, aimFlat, Stats, enemies, groundAt, canCast: !stunned, _hits);
        UpdateShield(deltaSeconds, health);
        Report(numbers);
    }

    /// <summary>
    /// After the enemies' blows this frame: a blow Flame Ward turned aside sets its striker burning and spends the ward, a Frost Shield that has been broken ends
    /// (bursting, with Shattering Ward), and a last stand (Ice Block) heals and puts up a shield.
    /// </summary>
    internal void AfterBlows(Vector3D<float> feet, EnemyField enemies, PlayerHealth health, DamageNumbers numbers)
    {
        _hits.Clear();
        if (FlameWardReady)
        {
            bool warded = false;
            foreach (var strike in enemies.Strikes)
            {
                if (strike.Blocked)
                {
                    warded = true;
                    _barrage.Flames.Ignite(strike.Attacker, Stats.BoltDamage * MageStats.FlameWardBurn, Stats);
                }
            }

            if (warded)
            {
                _wardIn = Stats.FlameWardInterval;
                _wardShown = FlameWardShown;
            }
        }

        if (_shieldUp && health.Barrier <= 0f)
        {
            EndShield(health);
            if (Stats.Tree.ShatteringWard)
            {
                _barrage.Burst(feet, MageStats.WardBurstRadius * Stats.AreaScale, Stats.BoltDamage * MageStats.WardBurstShare, Stats, enemies, _hits, FrostSource.Ward);
            }
        }

        if (_iceBlockLeft > 0 && health.TakeLastStand())
        {
            _iceBlockLeft--;
            health.Heal(health.Max * MageStats.IceBlockHeal);   // Ice Block
            RaiseShield(health, health.Max * MageStats.IceBlockShield);
            _iceBlockShown = IceBlockShown;
        }

        Report(numbers);
    }

    public void OnKill(Enemy killed, float runSeconds)
    {
    }

    public void Clear(EngineWindow window)
    {
        _barrage.Reset();
        _view.Clear(window);
        _fireView.Clear(window);
        _shieldUp = false;
    }

    public IReadOnlyList<LevelUpCard> RollLevelUp(Random random)
    {
        _choices = MageUpgrades.Roll(Stats, random, excluded: _banished);
        return Cards();
    }

    public IReadOnlyList<LevelUpCard> BanishCard(int index, Random random)
    {
        if (index >= 0 && index < _choices.Count && _choices[index].Upgrade is { } banished)
        {
            _banished.Add(banished);
            _choices = MageUpgrades.Replace(_choices, index, Stats, random, _banished);
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
            health.Heal(MageUpgrades.SecondWindHeal);
        }
    }

    /// <summary>
    /// The Frost Shield's clock: while one holds it wears off after its time (never, with Glacial Fortress); while none does, the next forms when its time comes - if
    /// the tree has the Frost Shield.
    /// </summary>
    private void UpdateShield(float deltaSeconds, PlayerHealth health)
    {
        if (_shieldUp)
        {
            if (!Stats.Tree.GlacialFortress)
            {
                _shieldLeft -= deltaSeconds;
                if (_shieldLeft <= 0f)
                {
                    EndShield(health);
                }
            }

            return;
        }

        if (!Stats.Tree.FrostShield)
        {
            return;
        }

        _shieldIn -= deltaSeconds;
        if (_shieldIn <= 0f)
        {
            RaiseShield(health, Stats.ShieldAmount);
        }
    }

    private void RaiseShield(PlayerHealth health, float amount)
    {
        health.Barrier = MathF.Max(health.Barrier, amount);
        _shieldUp = true;
        _shieldLeft = Stats.ShieldDuration;
    }

    /// <summary>The shield is gone (broken or worn off): what is left of it melts, and the next one starts forming.</summary>
    private void EndShield(PlayerHealth health)
    {
        health.Barrier = 0f;
        _shieldUp = false;
        _shieldIn = Stats.ShieldInterval;
    }

    /// <summary>
    /// The hits just dealt, on screen. The steady bites - the Blizzard, burns, fire on the ground, the Inferno - are added up for each enemy and shown as damage over
    /// time, so they don't bury the bolts'.
    /// </summary>
    private void Report(DamageNumbers numbers)
    {
        foreach (var hit in _hits)
        {
            if (hit.Source is not (FrostSource.Blizzard or FrostSource.Burn or FrostSource.Ground or FrostSource.Inferno))
            {
                numbers.Add(hit.Position, hit.Damage, hit.Killed, hit.Crit);
            }
            else
            {
                numbers.AddOverTime(hit.Enemy, hit.Position, hit.Damage, hit.Killed);
            }
        }
    }

    private List<LevelUpCard> Cards() =>
        _choices.Select(c => new LevelUpCard(c.Name, c.Description, c.NewLevel, c.MaxLevel, IsHeal: c.Upgrade is null)).ToList();
}
