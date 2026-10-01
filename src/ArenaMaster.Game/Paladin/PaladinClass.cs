using ArenaMaster.Game.Classes;
using ArenaMaster.Game.Combat;
using ArenaMaster.Game.Items;
using ArenaMaster.Game.Progression;
using ArenaMaster.Game.Ui;
using CEngine.Core;
using Silk.NET.Maths;

namespace ArenaMaster.Game.Paladin;

/// <summary>
/// The Paladin as the content runs it (see <see cref="IHeroClass"/>): a flail and a great crusader shield. The Holy Nova bursts around the Paladin on its own
/// (<see cref="HolyLight"/>), leaving holy circles that burn enemies and heal the Paladin standing in one. The shield blocks blows outright, and the Defiance tree
/// adds thorns and turns blocks into weapons; the Crusade tree (<see cref="CrusadeLight"/>) rewards moving and paying health instead: Zeal, hammers of light, a
/// rush that hits, Blood Oath and Avenging Wings. Here too is what answers the enemies' blows: shield bash, heal on block, Holy Bastion, Retribution, Shield of
/// Faith and Unbroken Vow, and the Crusade's Penance and Avenging Wings.
/// </summary>
internal sealed class PaladinClass : IHeroClass
{
    /// <summary>How long "BLOCKED" and "UNBROKEN VOW" stay under the crosshair.</summary>
    private const float BlockedShown = 0.45f;
    private const float VowShown = 2.5f;

    private readonly PaladinController _controller = new();
    private readonly HolyLight _light;
    private readonly HolyView _view = new();
    private readonly CrusadeLight _crusade = new();
    private readonly CrusadeView _crusadeView = new();
    private readonly HashSet<PaladinUpgrade> _banished = new();
    private readonly List<HolyHit> _hits = new();
    private List<PaladinChoice> _choices = new();
    private PlayerHealth? _health;
    private bool _still;
    private int _circlesAround;
    private float _faithIn;
    private float _blockedShown;
    private float _vowShown;

    /// <summary>Unbroken Vow's last stand, not yet used this run. Any other last stand (an item's) is the content's to answer.</summary>
    private int _vowLeft;

    /// <summary>The health's running total of what blows took off, as of the last look (Penance pays back what it has grown by).</summary>
    private double _lostSeen;

    /// <summary>How long after the last attack frame the body still holds its fighting stance (a run's attacks come after the body in each frame).</summary>
    private const float AttackingHold = 0.25f;

    /// <summary>The body turns to the nearest enemy within this many metres (the nova goes off all round, so this is only which way it stands).</summary>
    private const float FaceRange = 15f;

    private float _attackingLeft;

    /// <summary>The flat way to the nearest enemy as of the last attack frame, or null with none near.</summary>
    private Vector3D<float>? _aim;

    public PaladinClass(Random random) => _light = new HolyLight(random);

    public PaladinStats Stats { get; } = new();

    /// <summary>The novas and circles, for the tests.</summary>
    internal HolyLight Light => _light;

    /// <summary>Zeal, the hammers and the wings, for the tests.</summary>
    internal CrusadeLight Crusade => _crusade;

    public string Id => DefianceTree.ClassId;

    public string Name => "Paladin";

    public string Summary =>
        "A flail and a great crusader shield. Holy Nova bursts around you on its own and leaves holy ground that burns enemies and heals you. Slow and tough: Defiance stands its ground in the crowd, the Crusade keeps marching and pays for its power in blood.";

    public IReadOnlyList<TreeDefinition> Trees { get; } = new[] { DefianceTree.Tree, CrusadeTree.Tree };

    public TreeDefinition Tree { get; private set; } = DefianceTree.Tree;

    public void ChooseTree(string treeId) => Tree = Trees.FirstOrDefault(t => t.Id == treeId) ?? Trees[0];

    public float MaxHealth => Stats.MaxHealth;

    public float PickupRadius => Stats.PickupRadius;

    public float Regeneration => Stats.Regeneration(low: _health is { } h && h.Current < h.Max * PaladinStats.LowHealth);

    public float DamageTaken => Stats.DamageTaken(InCircle);

    /// <summary>Certain while Shield of Faith is up; otherwise the shield's chance, with standing still and Sanctuary counted.</summary>
    public float BlockChance => FaithReady ? 1f : Stats.BlockChance(_still, InCircle);

    public bool KeepsOwnBarrier => false;

    public string DashLabel => "SHIELD RUSH";

    public float DashReadiness => _controller.RushReadiness;

    public Vector3D<float> DashVelocity => _controller.RushVelocity;

    public string? Status =>
        _vowShown > 0f ? "UNBROKEN VOW" : _crusade.Winged ? "AVENGING WINGS" : _blockedShown > 0f ? "BLOCKED" : FaithReady ? "SHIELD OF FAITH" : null;

    /// <summary>Zeal, on a run while the Crusade builds it.</summary>
    public (string Label, float Fill)? Meter =>
        _health is not null && Stats.BuildsZeal ? ($"ZEAL {(int)(_crusade.Zeal + 0.001f)} / {PaladinStats.MaxZeal:0}", _crusade.Zeal / PaladinStats.MaxZeal) : null;

    /// <summary>Whether the Paladin stands in a holy circle this frame.</summary>
    public bool InCircle => _circlesAround > 0;

    private bool FaithReady => Stats.Tree.ShieldOfFaith && _faithIn <= 0f;

    /// <summary>The active tree's bonuses from <paramref name="ranks"/>; the other tree's are left empty, so nothing of it counts.</summary>
    public void UseTree(IReadOnlyDictionary<string, int> ranks)
    {
        bool crusade = Tree.Id == CrusadeTree.TreeId;
        Stats.CrusadeActive = crusade;
        Stats.Tree = crusade ? new DefianceBonuses() : DefianceBonuses.From(ranks);
        Stats.Crusade = crusade ? CrusadeBonuses.From(ranks) : new CrusadeBonuses();
    }

    public void BeginRun(ItemBonuses items, PlayerHealth health)
    {
        Stats.Reset();
        Stats.Items = items;
        health.Reset(Stats.MaxHealth);
        _health = health;
        _vowLeft = Stats.Tree.UnbrokenVow ? 1 : 0;
        health.LastStands = _vowLeft;
        _banished.Clear();
        _light.Reset();
        _crusade.Reset();
        _lostSeen = health.HealthLost;
        _circlesAround = 0;
        _faithIn = 0f;
        _blockedShown = 0f;
        _vowShown = 0f;
    }

    public void ReturnToCamp()
    {
        Stats.Reset();
        _health = null;
        _circlesAround = 0;
        _still = false;
    }

    public void Move(EngineWindow window, float deltaSeconds, bool stunned)
    {
        _attackingLeft = MathF.Max(0f, _attackingLeft - deltaSeconds);
        bool fighting = _attackingLeft > 0f;
        _controller.Update(window, deltaSeconds, Stats, stunned, fighting ? AttackTime() : null, fighting ? _aim : null);
    }

    /// <summary>
    /// Where the body's attack clip is, in time with the novas: wound up over the last moments before each one, through the blow just
    /// after (the clip's middle is the moment it lands), and ready in between.
    /// </summary>
    private float AttackTime()
    {
        float interval = Stats.NovaInterval;
        float until = _light.NovaIn;
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
        _aim = frame.Enemies.Nearest(feet, FaceRange) is { } nearest ? Geometry.FlatDirection(feet, nearest.Position, out _) : null;
        Fight(frame.DeltaSeconds, frame.Window.PlayerFeet, Ground(frame), frame.StandingStill, frame.Condition.IsStunned, frame.Enemies, frame.Health, frame.Numbers,
            rushing: _controller.RushVelocity != Vector3D<float>.Zero);
        _view.Sync(frame.Window, _light);
        var facing = _controller.Facing;
        _crusadeView.Sync(frame.Window, _crusade, feet, MathF.Atan2(facing.X, facing.Z), frame.DeltaSeconds);
    }

    public void Answer(RunFrame frame) => AnswerStrikes(frame.Enemies.Strikes, Ground(frame), frame.Enemies, frame.Health, frame.Numbers);

    /// <summary>
    /// The attacks for one frame, before the enemies move: the novas, circles and thorns (held by a stun), the Crusade's Zeal, hammers, rush, trail, Blood Oath and
    /// wings, and the healing from standing in holy ground - once, or once per circle with Consecrated Ground. <paramref name="ground"/> is the ground under
    /// <paramref name="feet"/>; <paramref name="rushing"/> is whether the shield rush is carrying the Paladin this frame.
    /// </summary>
    internal void Fight(float deltaSeconds, Vector3D<float> feet, Vector3D<float> ground, bool standingStill, bool stunned, EnemyField enemies, PlayerHealth health,
        DamageNumbers numbers, bool rushing = false)
    {
        _still = standingStill;
        _faithIn = MathF.Max(0f, _faithIn - deltaSeconds);
        _blockedShown = MathF.Max(0f, _blockedShown - deltaSeconds);
        _vowShown = MathF.Max(0f, _vowShown - deltaSeconds);

        _hits.Clear();
        _crusade.Prepare(deltaSeconds, moving: !standingStill, Stats, health);
        int novas = _light.Novas;
        _light.Update(deltaSeconds, ground, Stats, enemies, canCast: !stunned, _hits);
        _crusade.Update(deltaSeconds, ground, moving: !standingStill, rushing, _light.Novas - novas, _light, Stats, enemies, health, _hits);

        _circlesAround = _light.CirclesAround(feet);
        int healing = Stats.Tree.ConsecratedGround ? _circlesAround : Math.Min(1, _circlesAround);
        health.Heal(Stats.CircleHealing * healing * deltaSeconds);
        Report(numbers, health);
    }

    /// <summary>
    /// The enemies' blows this frame, answered: a block bashes the attacker, heals, spends Shield of Faith and (Holy Bastion) bursts; Retribution pays every blow
    /// back; a last stand (Unbroken Vow) heals. The Crusade's answers: Penance stores what the blows took off for the next nova, and Avenging Wings take flight once
    /// health is low.
    /// </summary>
    internal void AnswerStrikes(IReadOnlyList<Strike> strikes, Vector3D<float> ground, EnemyField enemies, PlayerHealth health, DamageNumbers numbers)
    {
        _hits.Clear();
        foreach (var strike in strikes)
        {
            if (strike.Blocked)
            {
                _blockedShown = BlockedShown;
                if (FaithReady)
                {
                    _faithIn = PaladinStats.FaithInterval;   // the shield turned this one; it readies again in a while
                }

                _light.Hurt(strike.Attacker, Stats.BashDamage, crit: false, HolySource.ShieldBash, Stats, enemies, _hits);
                health.Heal(Stats.Tree.BlockHeal);
                if (Stats.Tree.HolyBastion)
                {
                    _light.Burst(ground, Stats.NovaRadius, Stats.NovaDamage * PaladinStats.BastionDamage, Stats, enemies, _hits);
                }
            }

            if (Stats.Tree.Retribution)
            {
                _light.Hurt(strike.Attacker, strike.Damage * PaladinStats.RetributionShare, crit: false, HolySource.Retribution, Stats, enemies, _hits);
            }
        }

        if (_vowLeft > 0 && health.TakeLastStand())
        {
            _vowLeft--;
            health.Heal(health.Max * PaladinStats.VowHeal);   // Unbroken Vow
            _vowShown = VowShown;
        }

        if (Stats.Crusade.Penance && health.HealthLost > _lostSeen)
        {
            _light.Penance += (float)(health.HealthLost - _lostSeen) * PaladinStats.PenanceShare;
        }

        _lostSeen = health.HealthLost;
        _crusade.CheckWings(Stats, health);
        Report(numbers, health);
    }

    /// <summary>A kill heals with Blood Oath, Lifeblood and Blood Tithe.</summary>
    public void OnKill(Enemy killed, float runSeconds) => _health?.Heal(Stats.KillHeal);

    public void Clear(EngineWindow window)
    {
        _light.Reset();
        _crusade.Reset();
        _view.Clear(window);
        _crusadeView.Clear(window);
        _circlesAround = 0;
    }

    public IReadOnlyList<LevelUpCard> RollLevelUp(Random random)
    {
        _choices = PaladinUpgrades.Roll(Stats, random, excluded: _banished);
        return Cards();
    }

    public IReadOnlyList<LevelUpCard> BanishCard(int index, Random random)
    {
        if (index >= 0 && index < _choices.Count && _choices[index].Upgrade is { } banished)
        {
            _banished.Add(banished);
            _choices = PaladinUpgrades.Replace(_choices, index, Stats, random, _banished);
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
            health.Heal(PaladinUpgrades.SecondWindHeal);
        }
    }

    /// <summary>
    /// The hits just dealt, on screen, and what answers kills: Bloodthorns heals for each thorns kill. The circles' steady burn is added up for each enemy and shown as
    /// damage over time, so it doesn't bury the nova's.
    /// </summary>
    private void Report(DamageNumbers numbers, PlayerHealth health)
    {
        foreach (var hit in _hits)
        {
            if (hit.Source != HolySource.Circle)
            {
                numbers.Add(hit.Position, hit.Damage, hit.Killed, hit.Crit);
            }
            else
            {
                numbers.AddOverTime(hit.Enemy, hit.Position, hit.Damage, hit.Killed);
            }

            if (hit.Killed && hit.Source == HolySource.Thorns)
            {
                health.Heal(Stats.Tree.ThornsKillHeal);
            }
        }
    }

    /// <summary>The ground under the Paladin, where novas burst and circles are left (not in the air mid-jump).</summary>
    private static Vector3D<float> Ground(RunFrame frame)
    {
        var feet = frame.Window.PlayerFeet;
        return new Vector3D<float>(feet.X, frame.GroundAt(feet.X, feet.Z) ?? feet.Y, feet.Z);
    }

    private List<LevelUpCard> Cards() =>
        _choices.Select(c => new LevelUpCard(c.Name, c.Description, c.NewLevel, c.MaxLevel, IsHeal: c.Upgrade is null)).ToList();
}
