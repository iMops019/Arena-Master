using ArenaMaster.Game.Items;

namespace ArenaMaster.Game.Paladin;

/// <summary>
/// The Paladin's numbers for the current run: the base values, the run's upgrades (see <see cref="PaladinUpgrades"/>), the items carried (<see cref="Items"/>),
/// the Defiance tree (<see cref="Tree"/>) and the Crusade tree (<see cref="Crusade"/>) - only the active one's bonuses are filled, the other's are empty - and
/// what the Crusade builds up during the fight (<see cref="Zeal"/>, <see cref="HealthShare"/> for Martyrdom, <see cref="Winged"/>), set by the class each frame.
/// Upgrade, item and tree bonuses add together; item multipliers then multiply the lot, and the Crusade's own "more damage" majors multiply after that. Items
/// speak in general terms, and here "damage" and "attack speed" mean the Holy Nova's (and the circles' and thorns' damage too), "critical" means a nova's crit.
/// </summary>
internal sealed class PaladinStats
{
    public const float BaseMaxHealth = 130f;
    public const float BaseMoveSpeed = 6.4f;
    public const float BasePickupRadius = 3f;

    /// <summary>The Holy Nova: damage to everything it reaches, seconds between bursts, and how far it reaches from the Paladin.</summary>
    public const float BaseNovaDamage = 30f;
    public const float BaseNovaInterval = 1.25f;
    public const float BaseNovaRadius = 4.5f;

    /// <summary>The holy circle a nova leaves: its size, how long it burns, damage per second to each enemy in it, and health per second to the Paladin standing in it.</summary>
    public const float BaseCircleRadius = 3f;
    public const float BaseCircleDuration = 4f;
    public const float BaseCircleDps = 10f;
    public const float BaseCircleHealing = 2f;

    /// <summary>
    /// The class's own critical chance. Everything else only scales it: the level-up, the tree and items add <i>increased</i> critical chance, so +100% doubles
    /// the base.
    /// </summary>
    public const float BaseCritChance = 0.06f;
    public const float BaseCritMultiplier = 2f;

    /// <summary>The big shield: the chance to block with nothing else, and the most block chance can reach.</summary>
    public const float BaseBlockChance = 0.1f;
    public const float MaxBlockChance = 0.6f;

    /// <summary>Thorns (once Crown of Thorns unlocks them): damage per strike, seconds between strikes, and how far past touching they reach.</summary>
    public const float BaseThorns = 6f;
    public const float BaseThornsInterval = 0.5f;
    public const float ThornsReach = 0.4f;

    /// <summary>Crown of Briars: thorns reach this far from the Paladin's feet, and hit this much harder.</summary>
    public const float BriarsReach = 3f;
    public const float BriarsBonus = 0.5f;

    /// <summary>The shield rush on Shift: how long, how fast, how often.</summary>
    public const float RushDuration = 0.2f;
    public const float BaseRushSpeed = 17f;
    public const float BaseRushCooldown = 1.6f;

    /// <summary>Echoing Nova: how long after the first burst the echo comes, and its share of the damage.</summary>
    public const float EchoDelay = 0.35f;
    public const float EchoDamage = 0.6f;

    /// <summary>Wrath of the Many: extra nova damage per enemy hit, and the most it can add.</summary>
    public const float WrathPerEnemy = 0.02f;
    public const float WrathCap = 0.6f;

    /// <summary>Radiant Avatar: every this-many-th nova is a Great Nova, this much bigger and harder, its circle this much bigger.</summary>
    public const int AvatarEvery = 8;
    public const float AvatarRadius = 2f;
    public const float AvatarDamage = 3f;
    public const float AvatarCircle = 1.6f;

    /// <summary>Resonance: a circle's own burst, as a share of the nova's damage.</summary>
    public const float ResonanceDamage = 0.5f;

    /// <summary>Holy Bastion: a block's nova, as a share of the nova's damage.</summary>
    public const float BastionDamage = 0.75f;

    /// <summary>Retribution: what an attacker takes back, as a share of its blow.</summary>
    public const float RetributionShare = 2f;

    /// <summary>Shield of Faith: seconds for the shield to ready itself again after it turns a blow aside.</summary>
    public const float FaithInterval = 12f;

    /// <summary>Unbroken Vow: the share of max health it heals once it has saved the Paladin.</summary>
    public const float VowHeal = 0.4f;

    /// <summary>Desperate Prayer works below this share of max health.</summary>
    public const float LowHealth = 0.4f;

    /// <summary>Sanctuary, while standing in a holy circle: damage taken multiplied by this, and block chance added.</summary>
    public const float SanctuaryDamageTaken = 0.8f;
    public const float SanctuaryBlock = 0.1f;

    /// <summary>Zeal (the Crusade): the most there can be, how much a second of moving builds, how much a second of standing still drains, and each one's nova damage.</summary>
    public const float MaxZeal = 20f;
    public const float ZealPerSecond = 1f;
    public const float ZealDrain = 4f;
    public const float ZealDamage = 0.02f;

    /// <summary>Crusader's Rush: how far past touching the rush's hits reach.</summary>
    public const float RushReach = 1f;

    /// <summary>Hammer of Judgement: every this-many-th nova drops one, on the toughest enemy within this many metres, for this much of the nova's damage within this radius.</summary>
    public const int HammerEvery = 5;
    public const float HammerRange = 15f;
    public const float HammerShare = 4f;
    public const float BaseHammerRadius = 2f;

    /// <summary>Final Judgement: hammers also fall on every elite and boss within this many metres.</summary>
    public const float FinalJudgementRange = 25f;

    /// <summary>Condemn: how long an enemy a hammer strikes stays condemned, and how much more damage it takes meanwhile.</summary>
    public const float CondemnDuration = 5f;
    public const float CondemnBonus = 0.3f;

    /// <summary>Blessed Trail: a holy circle every this many metres walked, this much of a nova's circle in size.</summary>
    public const float TrailStep = 3f;
    public const float TrailCircle = 0.6f;

    /// <summary>Blood Oath: each nova's cost (a share of max health), its extra damage, and the heal every kill gives.</summary>
    public const float BloodOathCost = 0.01f;
    public const float BloodOathDamage = 0.4f;
    public const float BloodOathHeal = 1f;

    /// <summary>Martyrdom: the most extra nova damage, reached at this share of max health or less (none at full health, in a straight line between).</summary>
    public const float MartyrdomMax = 0.6f;
    public const float MartyrdomFloor = 0.1f;

    /// <summary>Blood Price and Blood Fury work below this share of max health.</summary>
    public const float HalfHealth = 0.5f;

    /// <summary>Penance: the next nova's extra damage to each enemy, as a multiple of the health blows took off.</summary>
    public const float PenanceShare = 2f;

    /// <summary>Avenging Wings: taken below this share of max health, for this long, with novas this much more often; ready again this long after it began.</summary>
    public const float WingsBelow = 0.3f;
    public const float WingsDuration = 6f;
    public const float WingsNovaSpeed = 1.5f;
    public const float WingsCooldown = 60f;

    /// <summary>Endless Crusade: at full Zeal, how long after a nova its second burst comes, and its share of the damage.</summary>
    public const float EndlessDelay = 0.35f;
    public const float EndlessDamage = 0.6f;

    private readonly Dictionary<PaladinUpgrade, int> _levels = new();

    /// <summary>What the items carried this run add up to.</summary>
    public ItemBonuses Items { get; set; } = new();

    /// <summary>What the Defiance tree's ranks add up to (empty while the Crusade is the active tree).</summary>
    public DefianceBonuses Tree { get; set; } = new();

    /// <summary>What the Crusade tree's ranks add up to (empty while Defiance is the active tree).</summary>
    public CrusadeBonuses Crusade { get; set; } = new();

    /// <summary>Whether the Crusade is the active tree (which level-up cards come up follows it).</summary>
    public bool CrusadeActive { get; set; }

    /// <summary>The Zeal built up (0 to <see cref="MaxZeal"/>), set by the class each frame.</summary>
    public float Zeal { get; set; }

    /// <summary>Health as a share of max health (1 at full), set by the class each frame: Martyrdom, Blood Price and Blood Fury read it.</summary>
    public float HealthShare { get; set; } = 1f;

    /// <summary>Whether Avenging Wings are up, set by the class each frame.</summary>
    public bool Winged { get; set; }

    public int LevelOf(PaladinUpgrade upgrade) => _levels.GetValueOrDefault(upgrade);

    /// <summary>Takes one more level of <paramref name="upgrade"/> (no further than its maximum).</summary>
    public void Increase(PaladinUpgrade upgrade)
    {
        int level = LevelOf(upgrade);
        if (level < PaladinUpgrades.Info(upgrade).MaxLevel)
        {
            _levels[upgrade] = level + 1;
        }
    }

    /// <summary>A new run: no upgrades, no items, no Zeal, full health, no wings. The trees are left as they are.</summary>
    public void Reset()
    {
        _levels.Clear();
        Items = new ItemBonuses();
        Zeal = 0f;
        HealthShare = 1f;
        Winged = false;
    }

    public float NovaDamage =>
        BaseNovaDamage * (1f + 0.20f * LevelOf(PaladinUpgrade.HolyWrath) + Items.Damage + Tree.NovaDamage + Crusade.NovaDamage
                              + (HealthShare < HalfHealth ? Crusade.LowHealthDamage : 0f)
                              + ItemBonuses.ProjectileFallback * Items.Projectiles + ItemBonuses.ChainFallback * Items.Chains)
        * Items.DamageMultiplier * CrusadeMore;

    /// <summary>What the Crusade's "more damage" majors multiply a nova by: Zeal (2% a Zeal), Blood Oath and Martyrdom.</summary>
    public float CrusadeMore =>
        (1f + (Crusade.Zeal ? ZealDamage * Zeal : 0f)) * (Crusade.BloodOath ? 1f + BloodOathDamage : 1f) * (1f + MartyrdomBonus);

    /// <summary>Martyrdom's extra nova damage at the current health: none at full, rising in a straight line to <see cref="MartyrdomMax"/> at <see cref="MartyrdomFloor"/>.</summary>
    public float MartyrdomBonus =>
        !Crusade.Martyrdom ? 0f : MartyrdomMax * Math.Clamp((1f - HealthShare) / (1f - MartyrdomFloor), 0f, 1f);

    public float NovaInterval =>
        BaseNovaInterval
        / (MathF.Max(0.2f, 1f + 0.12f * LevelOf(PaladinUpgrade.QuickenedPrayer) + Items.AttackSpeedNow + Tree.NovaFrequency + Crusade.NovaFrequency)
           * Items.AttackSpeedMultiplier * (Winged ? WingsNovaSpeed : 1f));

    /// <summary>Whether Zeal builds at all: with the Zeal major, or Endless Crusade (which builds it too).</summary>
    public bool BuildsZeal => Crusade.Zeal || Crusade.EndlessCrusade;

    /// <summary>Zeal full (Endless Crusade's second bursts).</summary>
    public bool ZealFull => BuildsZeal && Zeal >= MaxZeal - 1e-3f;

    /// <summary>Zeal built for each second of moving.</summary>
    public float ZealGain => ZealPerSecond * (1f + Crusade.ZealGain + 0.25f * LevelOf(PaladinUpgrade.Fervour));

    /// <summary>Zeal drained for each second of standing still.</summary>
    public float ZealLoss => ZealDrain * MathF.Max(0.1f, 1f - Crusade.ZealKept - 0.25f * LevelOf(PaladinUpgrade.Fervour));

    /// <summary>What Crusader's Rush does to each enemy it passes: a nova's damage, more with Battering Rush and Battering Charge.</summary>
    public float RushDamage => NovaDamage * (1f + Crusade.RushDamage + 0.30f * LevelOf(PaladinUpgrade.BatteringCharge));

    /// <summary>Whether hammers fall: Hammer of Judgement, or Final Judgement (which drops them too).</summary>
    public bool DropsHammers => Crusade.HammerOfJudgement || Crusade.FinalJudgement;

    /// <summary>A hammer's damage to everything under it.</summary>
    public float HammerDamage => NovaDamage * HammerShare * (1f + Crusade.HammerDamage + 0.30f * LevelOf(PaladinUpgrade.Hammerfall));

    /// <summary>How far round where it lands a hammer hits.</summary>
    public float HammerRadius => BaseHammerRadius * (1f + Crusade.HammerRadius + Items.Area) * Items.AreaMultiplier;

    /// <summary>Health each kill heals: Blood Oath, Lifeblood and Blood Tithe.</summary>
    public float KillHeal => (Crusade.BloodOath ? BloodOathHeal : 0f) + Crusade.HealOnKill + 0.5f * LevelOf(PaladinUpgrade.BloodTithe);

    public float NovaRadius => BaseNovaRadius * (1f + 0.10f * LevelOf(PaladinUpgrade.Radiance) + Tree.NovaRadius + Items.Area) * Items.AreaMultiplier;

    public float CircleRadius => BaseCircleRadius * (1f + 0.10f * LevelOf(PaladinUpgrade.Radiance) + Tree.CircleRadius + Items.Area) * Items.AreaMultiplier;

    public float CircleDuration => BaseCircleDuration + 0.5f * LevelOf(PaladinUpgrade.Consecration) + Tree.CircleDuration + Items.Duration;

    /// <summary>A circle's damage per second to each enemy in it.</summary>
    public float CircleDps =>
        BaseCircleDps * (1f + 0.25f * LevelOf(PaladinUpgrade.Consecration) + Items.Damage + Tree.CircleDamage + Items.DotDamage) * Items.DamageMultiplier * Items.DotMultiplier;

    /// <summary>Health per second from standing in a circle (from each circle, with Consecrated Ground).</summary>
    public float CircleHealing => BaseCircleHealing * (1f + 0.25f * LevelOf(PaladinUpgrade.SanctifiedGround) + Tree.CircleHealing);

    /// <summary>What a block does to the attacker: the tree's Shield Bash and the run's Shield Slam.</summary>
    public float BashDamage => Tree.BashDamage + 15f * LevelOf(PaladinUpgrade.ShieldSlam);

    public float CritChance => BaseCritChance * MathF.Max(0f, 1f + 0.20f * LevelOf(PaladinUpgrade.ZealotsEye) + Items.CritChance + Tree.CritChance + Crusade.CritChance);

    /// <summary>How many times normal damage a critical nova hit does.</summary>
    public float CritMultiplier => BaseCritMultiplier + 0.15f * LevelOf(PaladinUpgrade.ZealotsEye) + Items.CritDamage + Tree.CritDamage + Crusade.CritDamage;

    /// <summary>What every Paladin hit on an elite or a boss is multiplied by.</summary>
    public float EliteMultiplier => 1f + Tree.EliteDamage + Crusade.EliteDamage;

    /// <summary>The chance to block a blow, capped at <see cref="MaxBlockChance"/>. Braced Stance adds more standing still, Sanctuary more in a holy circle.</summary>
    public float BlockChance(bool standingStill, bool inCircle) => MathF.Min(MaxBlockChance,
        (BaseBlockChance + 0.04f * LevelOf(PaladinUpgrade.ShieldTraining) + Tree.BlockChance + Items.BlockChance
         + (standingStill ? Tree.StillBlock + 0.05f * LevelOf(PaladinUpgrade.Steadfast) : 0f)
         + (inCircle && Tree.Sanctuary ? SanctuaryBlock : 0f))
        * Items.BlockMultiplier);

    /// <summary>Seconds for the shield rush to recharge.</summary>
    public float RushCooldown => BaseRushCooldown / (1f + Items.DashRecharge + Crusade.RushRecharge + 0.15f * LevelOf(PaladinUpgrade.Onslaught));

    public bool HasThorns => Tree.CrownOfThorns;

    /// <summary>Damage each thorns strike does, or 0 without thorns.</summary>
    public float Thorns => !HasThorns ? 0f
        : (BaseThorns + Tree.Thorns + Tree.ThornsFromHealth * MaxHealth)
          * (1f + 0.40f * LevelOf(PaladinUpgrade.BarbedPlating) + Items.Damage + Tree.ThornsBonus + (Tree.CrownOfBriars ? BriarsBonus : 0f))
          * Items.DamageMultiplier;

    public float ThornsInterval => BaseThornsInterval / (1f + Tree.ThornsSpeed + 0.20f * LevelOf(PaladinUpgrade.BrambleMail));

    public float MaxHealth => (BaseMaxHealth + 20f * LevelOf(PaladinUpgrade.HeavyPlate) + Items.MaxHealth + Tree.MaxHealth + Crusade.MaxHealth) * Items.MaxHealthMultiplier;

    /// <summary>Health back per second, always on - more while low, with Desperate Prayer.</summary>
    public float Regeneration(bool low) =>
        (0.5f * LevelOf(PaladinUpgrade.PrayerOfMending) + Items.Regeneration + Tree.Regeneration + Crusade.Regeneration) * (low ? 1f + Tree.LowRegen : 1f);

    /// <summary>What every blow's damage is multiplied by: nothing gets through while Avenging Wings are up.</summary>
    public float DamageTaken(bool inCircle) =>
        Winged ? 0f : Items.DamageTaken * Tree.DamageTaken * Crusade.DamageTaken * (inCircle && Tree.Sanctuary ? SanctuaryDamageTaken : 1f);

    public float MoveSpeed => BaseMoveSpeed * (1f + 0.08f * LevelOf(PaladinUpgrade.PilgrimsStride) + Items.MoveSpeed + Crusade.MoveSpeed);

    public float PickupRadius => BasePickupRadius * (1f + 0.35f * LevelOf(PaladinUpgrade.Gleaner) + Items.Pickup);
}
