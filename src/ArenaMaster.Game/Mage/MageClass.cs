using ArenaMaster.Game.Classes;
using ArenaMaster.Game.Combat;
using ArenaMaster.Game.Items;
using ArenaMaster.Game.Progression;
using ArenaMaster.Game.Ui;
using CEngine.Core;
using Silk.NET.Maths;

namespace ArenaMaster.Game.Mage;

/// <summary>
/// The Mage as the content runs it (see <see cref="IHeroClass"/>): a staff of ice. The Frost Barrage (<see cref="FrostBarrage"/>) sends a volley of homing bolts off
/// the staff one after another; every hit chills. Here too is the Frost Shield - a barrier on the health that forms on its own every so often and holds for a few
/// seconds (Shattering Ward bursts when it breaks, Glacial Fortress keeps it until it does) - and Ice Block, the once-a-run last stand.
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

    private readonly MageController _controller = new();
    private readonly FrostBarrage _barrage;
    private readonly FrostView _view = new();
    private readonly HashSet<MageUpgrade> _banished = new();
    private readonly List<FrostHit> _hits = new();
    private List<MageChoice> _choices = new();
    private bool _shieldUp;
    private float _shieldLeft;
    private float _shieldIn = FirstShield;
    private float _iceBlockShown;

    public MageClass(Random random) => _barrage = new FrostBarrage(random);

    public MageStats Stats { get; } = new();

    /// <summary>The bolts and bursts, for the tests.</summary>
    internal FrostBarrage Barrage => _barrage;

    /// <summary>Whether a Frost Shield (or Ice Block's shield) is holding.</summary>
    public bool ShieldUp => _shieldUp;

    public string Id => FrostTree.ClassId;

    public string Name => "Mage";

    public string Summary =>
        "A staff of ice. Frost Barrage sends three bolts off the staff one after another, each seeking out an enemy, and the cold slows what it hits. Fragile, but quick to blink away.";

    public TreeDefinition Tree => FrostTree.Tree;

    public float MaxHealth => Stats.MaxHealth;

    public float PickupRadius => Stats.PickupRadius;

    public float Regeneration => Stats.Regeneration;

    public float DamageTaken => Stats.DamageTaken;

    public float BlockChance => 0f;

    public string DashLabel => "BLINK";

    public float DashReadiness => _controller.BlinkReadiness;

    public Vector3D<float> DashVelocity => _controller.BlinkVelocity;

    public string? Status => _iceBlockShown > 0f ? "ICE BLOCK" : null;

    public void UseTree(IReadOnlyDictionary<string, int> ranks) => Stats.Tree = FrostBonuses.From(ranks);

    public void BeginRun(ItemBonuses items, PlayerHealth health)
    {
        Stats.Reset();
        Stats.Items = items;
        health.Reset(Stats.MaxHealth);
        health.LastStands = Stats.Tree.IceBlock ? 1 : 0;
        _banished.Clear();
        _barrage.Reset();
        _shieldUp = false;
        _shieldLeft = 0f;
        _shieldIn = FirstShield;
        _iceBlockShown = 0f;
    }

    public void ReturnToCamp()
    {
        Stats.Reset();
        _shieldUp = false;
    }

    public void Move(EngineWindow window, float deltaSeconds, bool stunned) => _controller.Update(window, deltaSeconds, Stats, stunned);

    public void Hide(EngineWindow window) => _controller.Hide(window);

    public void Attack(RunFrame frame)
    {
        var feet = frame.Window.PlayerFeet;
        var aim = MageController.Facing(frame.Window);
        Fight(frame.DeltaSeconds, feet, aim, frame.Condition.IsStunned, frame.Enemies, frame.GroundAt, frame.Health, frame.Numbers);
        _view.Sync(frame.Window, _barrage, feet, _shieldUp, Stats.Tree.Blizzard, frame.DeltaSeconds);
    }

    public void Answer(RunFrame frame) => AfterBlows(frame.Window.PlayerFeet, frame.Enemies, frame.Health, frame.Numbers);

    /// <summary>
    /// The attacks for one frame, before the enemies move: the barrage from the staff (held by a stun) and the Blizzard, and the Frost Shield forming or fading.
    /// <paramref name="aimFlat"/> is where the camera faces.
    /// </summary>
    internal void Fight(float deltaSeconds, Vector3D<float> feet, Vector3D<float> aimFlat, bool stunned, EnemyField enemies, Func<float, float, float?> groundAt,
        PlayerHealth health, DamageNumbers numbers)
    {
        _iceBlockShown = MathF.Max(0f, _iceBlockShown - deltaSeconds);
        var staff = feet + new Vector3D<float>(0f, StaffHeight, 0f) + aimFlat * StaffForward;

        _hits.Clear();
        _barrage.Update(deltaSeconds, staff, feet, aimFlat, Stats, enemies, groundAt, canCast: !stunned, _hits);
        UpdateShield(deltaSeconds, health);
        Report(numbers);
    }

    /// <summary>
    /// After the enemies' blows this frame: a Frost Shield that has been broken ends (bursting, with Shattering Ward), and a last stand (Ice Block) heals and puts up a
    /// shield.
    /// </summary>
    internal void AfterBlows(Vector3D<float> feet, EnemyField enemies, PlayerHealth health, DamageNumbers numbers)
    {
        _hits.Clear();
        if (_shieldUp && health.Barrier <= 0f)
        {
            EndShield(health);
            if (Stats.Tree.ShatteringWard)
            {
                _barrage.Burst(feet, MageStats.WardBurstRadius, Stats.BoltDamage * MageStats.WardBurstShare, Stats, enemies, _hits, FrostSource.Ward);
            }
        }

        if (health.TakeLastStand())
        {
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
    /// The hits just dealt, on screen. The Blizzard's steady bite shows as the enemies flashing, not as numbers, so it doesn't bury the bolts'.
    /// </summary>
    private void Report(DamageNumbers numbers)
    {
        foreach (var hit in _hits)
        {
            if (hit.Source != FrostSource.Blizzard)
            {
                numbers.Add(hit.Position, hit.Damage, hit.Killed, hit.Crit);
            }
        }
    }

    private List<LevelUpCard> Cards() =>
        _choices.Select(c => new LevelUpCard(c.Name, c.Description, c.NewLevel, c.MaxLevel, IsHeal: c.Upgrade is null)).ToList();
}
