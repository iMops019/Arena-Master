using ArenaMaster.Game.Classes;
using ArenaMaster.Game.Combat;
using ArenaMaster.Game.Items;
using ArenaMaster.Game.Progression;
using ArenaMaster.Game.Ui;
using CEngine.Core;
using Silk.NET.Maths;

namespace ArenaMaster.Game.Priest;

/// <summary>
/// The Priest as the content runs it (see <see cref="IHeroClass"/>): a skull wand and a Skull Shield. The wand fires skulls that hunt through the crowd, piercing
/// what they pass (<see cref="PlagueSkulls"/>), and Plaguing it with Unholy active. Here too is what answers the enemies' blows - the Skull Shield's block, and
/// with Unholy, Death and Decay spewing from it and Bone Armour; with Grave Calling, the Bone Cage - and Rotting Step's rot, Soul Harvest and the Leech Jar's
/// healing. With Grave Calling active, the raised dead (<see cref="RaisedDead"/>) and the souls (<see cref="GatheredSouls"/>) are run here too.
/// </summary>
internal sealed class PriestClass : IHeroClass
{
    /// <summary>How long "BLOCKED" and "DEATH AND DECAY" stay under the crosshair.</summary>
    private const float BlockedShown = 0.45f;
    private const float DecayShown = 0.8f;

    /// <summary>How long after the last attack frame the body still holds its fighting stance (a run's attacks come after the body in each frame).</summary>
    private const float AttackingHold = 0.25f;

    /// <summary>Where the wand's skull is, from the feet: up, ahead, and to the right.</summary>
    private const float WandUp = 1.3f;
    private const float WandAhead = 0.5f;
    private const float WandRight = 0.3f;

    private readonly PriestController _controller = new();
    private readonly PlagueSkulls _skulls;
    private readonly PriestView _view = new();
    private readonly RaisedDead _servants = new();
    private readonly GatheredSouls _souls = new();
    private readonly GraveView _graveView = new();

    /// <summary>The enemies held in a Bone Cage (drawn with one over them while they stay held).</summary>
    private readonly HashSet<Enemy> _caged = new();
    private readonly Random _random;
    private readonly HashSet<PriestUpgrade> _banished = new();
    private readonly List<PriestHit> _hits = new();
    private List<PriestChoice> _choices = new();
    private float _blockedShown;
    private float _decayShown;
    private float _attackingLeft;

    /// <summary>The flat way to the enemy it fights, as of the last attack frame, or null with none: the body faces it.</summary>
    private Vector3D<float>? _aim;
    private bool _inRun;
    private bool _wasStepping;
    private Vector3D<float>? _rotDue;

    public PriestClass(Random random)
    {
        _random = random;
        _skulls = new PlagueSkulls(random);
        _skulls.Empower = () => _souls.Empower(Stats);
    }

    public PriestStats Stats { get; } = new();

    /// <summary>The skulls, the Plague and the rot, for the tests.</summary>
    internal PlagueSkulls Skulls => _skulls;

    /// <summary>The raised dead, the souls and the caged, for the tests.</summary>
    internal RaisedDead Servants => _servants;

    internal GatheredSouls Souls => _souls;

    internal IReadOnlySet<Enemy> Caged => _caged;

    public string Id => UnholyTree.ClassId;

    public string Name => "Priest";

    public string Summary =>
        "A skull wand and a Skull Shield. The wand fires skulls that hunt through the crowd, piercing one enemy after another. With the Unholy tree each skull leaves them Plagued, rotting from within, and the rot spreads; with Grave Calling the skulls hit harder, and the dead get back up to fight for you.";

    public IReadOnlyList<TreeDefinition> Trees { get; } = new[] { UnholyTree.Tree, GraveCallingTree.Tree };

    public TreeDefinition Tree { get; private set; } = UnholyTree.Tree;

    public void ChooseTree(string treeId) => Tree = Trees.FirstOrDefault(t => t.Id == treeId) ?? Trees[0];

    public float MaxHealth => Stats.MaxHealth;

    public float PickupRadius => Stats.PickupRadius;

    public float Regeneration => Stats.Regeneration;

    public float DamageTaken => Stats.DamageTaken;

    public float BlockChance => Stats.BlockChance;

    public bool KeepsOwnBarrier => false;

    public string DashLabel => "ROTTING STEP";

    public float DashReadiness => _controller.StepReadiness;

    public Vector3D<float> DashVelocity => _controller.StepVelocity;

    /// <summary>Soul Siphon's charges (or the Lich's time), on a run.</summary>
    public (string Label, float Fill)? Meter => _inRun ? _souls.Meter(Stats) : null;

    public string? Status =>
        _decayShown > 0f ? "DEATH AND DECAY"
        : _blockedShown > 0f ? "BLOCKED"
        : null;

    /// <summary>The active tree's ranks: its bonuses taken, the other tree's left empty, and the skulls told which tree they fight for.</summary>
    public void UseTree(IReadOnlyDictionary<string, int> ranks)
    {
        bool grave = Tree.Id == GraveCallingTree.TreeId;
        Stats.GraveCalling = grave;
        Stats.Tree = grave ? new UnholyBonuses() : UnholyBonuses.From(ranks);
        Stats.Grave = grave ? GraveCallingBonuses.From(ranks) : new GraveCallingBonuses();
    }

    public void BeginRun(ItemBonuses items, PlayerHealth health)
    {
        Stats.Reset();
        Stats.Items = items;
        health.Reset(Stats.MaxHealth);
        _banished.Clear();
        _skulls.Reset();
        _servants.Reset();
        _souls.Reset();
        _caged.Clear();
        _blockedShown = 0f;
        _decayShown = 0f;
        _rotDue = null;
        _inRun = true;
    }

    public void ReturnToCamp()
    {
        Stats.Reset();
        _skulls.Reset();
        _servants.Reset();
        _souls.Reset();
        _caged.Clear();
        _inRun = false;
    }

    public void Move(EngineWindow window, float deltaSeconds, bool stunned)
    {
        _attackingLeft = MathF.Max(0f, _attackingLeft - deltaSeconds);
        bool fighting = _attackingLeft > 0f;
        _controller.Update(window, deltaSeconds, Stats, stunned, fighting ? AttackTime() : null, fighting ? _aim : null);

        // Rotting Step: a puff of rot where the Priest vanished (and rot left there, on a run), and another where it comes back.
        if (_controller.StepBegunAt is { } vanished)
        {
            _view.Puff(vanished);
            if (_inRun)
            {
                _rotDue = vanished;
            }
        }

        if (_wasStepping && !_controller.Stepping)
        {
            _view.Puff(window.PlayerFeet);
        }

        _wasStepping = _controller.Stepping;
        if (!_inRun)
        {
            _view.Sync(window, _skulls, deltaSeconds, (_, _) => null, window.PlayerFeet, _controller.Facing, fighting: false);
        }
    }

    /// <summary>
    /// Where the body's attack clip is, in time with the casts: wound up over the last moments before each one, through the blow just after (the clip's middle is
    /// the moment it lands), and ready in between - and ready while it waits for something to aim at.
    /// </summary>
    private float AttackTime()
    {
        float interval = Stats.CastInterval;
        float until = _skulls.CastIn;
        float since = interval - until;
        float follow = MathF.Min(0.45f, 0.5f * interval);
        float windup = MathF.Min(0.4f, 0.45f * interval);
        if (since >= 0f && since < follow)
        {
            return 0.5f + 0.5f * since / follow;
        }

        if (until > 0f && until < windup)
        {
            return 0.5f * (1f - until / windup);
        }

        return 0f;
    }

    /// <summary>The body goes, and with it the wisps, motes and ash drawn at camp: they'd hang frozen in the air once nothing moved them.</summary>
    public void Hide(EngineWindow window)
    {
        _controller.Hide(window);
        _view.Clear(window);
        _graveView.Clear(window);
    }

    public void Attack(RunFrame frame)
    {
        _attackingLeft = AttackingHold;
        var window = frame.Window;
        var feet = window.PlayerFeet;
        _aim = frame.Enemies.Nearest(feet, PriestStats.CastRange) is { } nearest ? Geometry.FlatDirection(feet, nearest.Position, out _) : null;
        var facing = _controller.Facing;
        var right = new Vector3D<float>(facing.Z, 0f, -facing.X);
        var hand = feet + new Vector3D<float>(0f, WandUp, 0f) + facing * WandAhead + right * WandRight;
        if (_rotDue is { } rot)
        {
            _skulls.RotAt(new Vector3D<float>(rot.X, frame.GroundAt(rot.X, rot.Z) ?? rot.Y, rot.Z), Stats);
            _rotDue = null;
        }

        Fight(frame.DeltaSeconds, hand, feet, facing, frame.Condition.IsStunned, frame.Enemies, frame.GroundAt, frame.Health, frame.Numbers);
        _view.Sync(window, _skulls, frame.DeltaSeconds, frame.GroundAt, feet, facing);
        _graveView.Sync(window, _servants, _souls, _skulls, _caged, frame.DeltaSeconds, frame.GroundAt, feet);
    }

    public void Answer(RunFrame frame)
    {
        var feet = frame.Window.PlayerFeet;
        var facing = _controller.Facing;
        AnswerStrikes(frame.Enemies.Strikes, new Vector3D<float>(feet.X, frame.GroundAt(feet.X, feet.Z) ?? feet.Y, feet.Z), MathF.Atan2(facing.X, facing.Z),
            frame.Enemies, frame.Health, frame.Numbers);
    }

    /// <summary>
    /// The attacks for one frame, before the enemies move: the souls gathered at <paramref name="feet"/> (their charges ready for the cast), the wand casting at the
    /// best target toward <paramref name="facing"/> (held by a stun), the skulls, the bone spears, the Plague and the rot, and the raised dead; then Soul Harvest
    /// and the Leech Jar heal for every Plagued enemy that died.
    /// </summary>
    internal void Fight(float deltaSeconds, Vector3D<float> hand, Vector3D<float> feet, Vector3D<float> facing, bool stunned, EnemyField enemies,
        Func<float, float, float?> groundAt, PlayerHealth health, DamageNumbers numbers)
    {
        _blockedShown = MathF.Max(0f, _blockedShown - deltaSeconds);
        _decayShown = MathF.Max(0f, _decayShown - deltaSeconds);
        _hits.Clear();
        _caged.RemoveWhere(e => !e.IsAlive || !e.IsFrozen);
        _souls.Update(deltaSeconds, feet, Stats, health, enemies, _skulls, _hits);
        _skulls.Update(deltaSeconds, hand, feet, facing, Stats, enemies, groundAt, canCast: !stunned, _hits);
        _servants.Update(deltaSeconds, feet, Stats, enemies, groundAt, _skulls, _hits);

        int plaguedDeaths = _skulls.PlaguedDeaths;
        if (plaguedDeaths > 0)
        {
            health.Heal(plaguedDeaths * ((Stats.Tree.SoulHarvest ? health.Max * PriestStats.SoulHarvestHeal : 0f) + Stats.Items.PlagueKillHeal));
        }

        Report(numbers);
    }

    /// <summary>
    /// The enemies' blows this frame, answered: a block with the Skull Shield heals (the tree's), may spew Death and Decay toward <paramref name="facingYaw"/>
    /// from <paramref name="feet"/>, raises a bone barrier with Bone Armour (and the Bonebound Aegis), and with Bone Cage holds the attacker in a cage of bones.
    /// </summary>
    internal void AnswerStrikes(IReadOnlyList<Strike> strikes, Vector3D<float> feet, float facingYaw, EnemyField enemies, PlayerHealth health, DamageNumbers numbers)
    {
        foreach (var strike in strikes)
        {
            if (!strike.Blocked)
            {
                continue;
            }

            _blockedShown = BlockedShown;
            health.Heal(Stats.Tree.BlockHeal);
            if (Stats.Grave.BoneCage && strike.Attacker is { IsAlive: true } attacker && attacker.Kind.Tier != EnemyTier.Boss && !attacker.Kind.IsProp)
            {
                attacker.Freeze(Stats.CageSeconds);
                _caged.Add(attacker);
            }

            if (Stats.DecayChance > 0f && _random.NextDouble() < Stats.DecayChance)
            {
                _skulls.SpewDecay(feet, facingYaw, Stats);
                _decayShown = DecayShown;
            }

            float barrier = (Stats.Tree.BoneArmour ? PriestStats.BoneArmourShare * health.Max : 0f) + Stats.Items.BlockBarrier;
            if (barrier > 0f)
            {
                health.Barrier = MathF.Max(health.Barrier, MathF.Min(health.Barrier + barrier, PriestStats.BoneArmourCap * health.Max));
            }
        }
    }

    /// <summary>A kill (from anything): with Grave Calling it counts toward Raise Dead's next servant and leaves a soul with Soul Siphon.</summary>
    public void OnKill(Enemy killed, float runSeconds)
    {
        if (killed.Kind.IsProp)
        {
            return;
        }

        _servants.OnKill(killed.Position, Stats);
        _souls.OnKill(killed.Position, Stats);
    }

    public void Clear(EngineWindow window)
    {
        _skulls.Reset();
        _servants.Reset();
        _souls.Reset();
        _caged.Clear();
        _view.Clear(window);
        _graveView.Clear(window);
        _rotDue = null;
        _inRun = false;
    }

    public IReadOnlyList<LevelUpCard> RollLevelUp(Random random)
    {
        _choices = PriestUpgrades.Roll(Stats, random, excluded: _banished);
        return Cards();
    }

    public IReadOnlyList<LevelUpCard> BanishCard(int index, Random random)
    {
        if (index >= 0 && index < _choices.Count && _choices[index].Upgrade is { } banished)
        {
            _banished.Add(banished);
            _choices = PriestUpgrades.Replace(_choices, index, Stats, random, _banished);
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
            health.Heal(PriestUpgrades.SecondWindHeal);
        }
    }

    /// <summary>
    /// The hits as damage numbers (skulls, spears, servants' claws, bursts and spirits); Plague, rot and the Aura tick too often to number one by one, and are
    /// added up for each enemy instead.
    /// </summary>
    private void Report(DamageNumbers numbers)
    {
        foreach (var hit in _hits)
        {
            if (hit.Source is PriestSource.Plague or PriestSource.Rot or PriestSource.Aura)
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
