using ArenaMaster.Game.Items;

namespace ArenaMaster.Game.Priest;

/// <summary>
/// The Priest's numbers for the current run: the base values, the run's upgrades (see <see cref="PriestUpgrades"/>), the items carried (<see cref="Items"/>) and the
/// Unholy tree (<see cref="Tree"/>). Upgrade, item and tree bonuses add together; item multipliers then multiply the lot. Items speak in general terms: here
/// "damage" is the skull's hit, its Plague and the rot alike, "attack speed" the cast speed, "projectiles" more skulls a cast, "chains" more pierce, "range" a longer
/// life for each skull, "area" wider rot, and "duration" longer Plague and rot.
/// </summary>
internal sealed class PriestStats
{
    public const float BaseMaxHealth = 110f;
    public const float BaseMoveSpeed = 6.8f;
    public const float BasePickupRadius = 3f;

    /// <summary>
    /// The Plague Skull: seconds between casts (it waits, ready, for an enemy within <see cref="CastRange"/>), the skull's hit, how fast it flies, how long it
    /// lives, how many enemies it goes on through after the first, and how far off it senses the next one.
    /// </summary>
    public const float BaseCastInterval = 1.3f;
    public const float CastRange = 22f;
    public const float BaseSkullDamage = 13.5f;
    public const float BaseSkullSpeed = 12f;
    public const float BaseSkullLife = 2.2f;
    public const int BasePierce = 4;
    public const float BaseSeekRange = 9f;

    /// <summary>Plague: every stack deals this much over its time, up to this many stacks on one enemy (Virulent Strain: <see cref="VirulentStacks"/>).</summary>
    public const float BasePlagueDamage = 29.5f;
    public const float BasePlagueDuration = 2f;
    public const int BasePlagueStacks = 3;
    public const int VirulentStacks = 5;

    /// <summary>The class's own critical chance on a skull's hit; everything else scales it (+100% doubles it).</summary>
    public const float BaseCritChance = 0.06f;
    public const float BaseCritMultiplier = 2f;

    /// <summary>The Skull Shield: its block chance to start with, and the most it can reach.</summary>
    public const float BaseBlockChance = 0.07f;
    public const float MaxBlockChance = 0.5f;

    /// <summary>
    /// Death and Decay: its chance on a block, the cone it spews (degrees wide, how far), how long its rot lies, and its damage a second to what is in it.
    /// </summary>
    public const float DecayChanceBase = 0.25f;
    public const float DecayConeDegrees = 110f;
    public const float DecayConeReach = 6f;
    public const float DecayConeSeconds = 3f;
    public const float DecayConeDamage = 15f;

    /// <summary>Rotting Step on Shift: a blink this long at this speed, its recharge, and the rot left where the Priest vanished (radius, seconds, damage a second).</summary>
    public const float StepDuration = 0.12f;
    public const float StepSpeed = 36f;
    public const float BaseStepCooldown = 2.2f;
    public const float StepRotRadius = 2f;
    public const float StepRotSeconds = 2.5f;
    public const float StepRotDamage = 8f;

    /// <summary>Grave Soil: how much rot on the ground slows.</summary>
    public const float GraveSoilSlow = 0.3f;

    /// <summary>Bone Armour: the barrier each block gives, and the most there can be, as shares of max health.</summary>
    public const float BoneArmourShare = 0.08f;
    public const float BoneArmourCap = 0.25f;

    /// <summary>Gnashing Skulls: damage a skull gains for every kill (and it gains a pierce each).</summary>
    public const float GnashingDamage = 0.1f;

    /// <summary>Epidemic: how near the struck enemy the others are Plagued too.</summary>
    public const float EpidemicRadius = 2.5f;

    /// <summary>Pestilence: how much longer Plague lasts, and the increased damage over time (Plague, rot and the Aura). The user's, 2026-09-27; it spread Plague before.</summary>
    public const float PestilenceDuration = 1.3f;
    public const float PestilenceDot = 0.25f;

    /// <summary>Soul Harvest: the share of max health a Plagued kill heals.</summary>
    public const float SoulHarvestHeal = 0.01f;

    /// <summary>Aura of Decay: its reach and damage a second.</summary>
    public const float AuraRadius = 3.5f;
    public const float AuraDamage = 8f;

    /// <summary>Mouth of the Grave: block chance added, and how much further the cone reaches.</summary>
    public const float MouthBlock = 0.08f;
    public const float MouthReach = 0.5f;

    /// <summary>
    /// Black Death: seconds more of Plague, and what everything the Priest does to a Plagued enemy is multiplied by. (Until 2026-09-27 it let Plague's ticks crit
    /// instead: the user wants no crit on damage over time, nor on the Unholy tree.)
    /// </summary>
    public const float BlackDeathDuration = 1f;
    public const float BlackDeathDamage = 1.15f;

    /// <summary>Creeping Death, the level-up: increased damage over time a rank.</summary>
    public const float CreepingDeathDot = 0.12f;

    /// <summary>Legion: seconds more of life for every skull.</summary>
    public const float LegionLife = 1f;

    private readonly Dictionary<PriestUpgrade, int> _levels = new();

    /// <summary>What the items carried this run add up to.</summary>
    public ItemBonuses Items { get; set; } = new();

    /// <summary>What the Unholy tree's ranks add up to.</summary>
    public UnholyBonuses Tree { get; set; } = new();

    public int LevelOf(PriestUpgrade upgrade) => _levels.GetValueOrDefault(upgrade);

    /// <summary>Takes one more level of <paramref name="upgrade"/> (no further than its maximum).</summary>
    public void Increase(PriestUpgrade upgrade)
    {
        int level = LevelOf(upgrade);
        if (level < PriestUpgrades.Info(upgrade).MaxLevel)
        {
            _levels[upgrade] = level + 1;
        }
    }

    /// <summary>A new run: no upgrades, no items. The tree is left as it is.</summary>
    public void Reset()
    {
        _levels.Clear();
        Items = new ItemBonuses();
    }

    /// <summary>What every Priest damage is raised by from items (it uses projectiles and chains itself, so none turn into damage).</summary>
    private float ItemDamage => Items.Damage;

    public float SkullDamage =>
        BaseSkullDamage * (1f + 0.20f * LevelOf(PriestUpgrade.WickedSkull) + ItemDamage + Tree.SkullDamage) * Items.DamageMultiplier * Items.ProjectileDamage;

    /// <summary>What one stack of Plague deals over its whole time.</summary>
    public float PlagueDamage =>
        BasePlagueDamage * (1f + 0.20f * LevelOf(PriestUpgrade.Virulence) + ItemDamage + Tree.PlagueDamage + OverTimeIncrease) * Items.DamageMultiplier * Items.DotMultiplier;

    /// <summary>Increased damage over time (Plague, rot and the Aura): items and gear, Pestilence, and the Creeping Death level-up.</summary>
    private float OverTimeIncrease => Items.DotDamage + (Tree.Pestilence ? PestilenceDot : 0f) + CreepingDeathDot * LevelOf(PriestUpgrade.CreepingDeath);

    /// <summary>How long a stack of Plague lasts.</summary>
    public float PlagueDuration =>
        BasePlagueDuration + 0.3f * LevelOf(PriestUpgrade.LongFever) + Tree.PlagueDuration + Items.Duration + (Tree.BlackDeath ? BlackDeathDuration : 0f)
        + (Tree.Pestilence ? PestilenceDuration : 0f);

    /// <summary>The most stacks of Plague one enemy can carry.</summary>
    public int PlagueStacks => (Tree.VirulentStrain ? VirulentStacks : BasePlagueStacks) + (int)Tree.PlagueStacks + Items.PlagueStacks;

    /// <summary>What the rot on the ground (Death and Decay, Rotting Step, the Aura) is multiplied by.</summary>
    public float DecayScale =>
        (1f + ItemDamage + Tree.DecayDamage + OverTimeIncrease + 0.20f * LevelOf(PriestUpgrade.DeepDecay)) * Items.DamageMultiplier * Items.DotMultiplier;

    /// <summary>Seconds between casts.</summary>
    public float CastInterval =>
        BaseCastInterval / (MathF.Max(0.2f, 1f + 0.12f * LevelOf(PriestUpgrade.HollowChant) + Items.AttackSpeedNow + Tree.CastSpeed) * Items.AttackSpeedMultiplier);

    /// <summary>Skulls a cast fires: one, one more with Twin Skulls and Legion, and one more for each extra projectile.</summary>
    public int Skulls => 1 + (Tree.TwinSkulls ? 1 : 0) + (Tree.Legion ? 1 : 0) + Items.Projectiles;

    public float SkullSpeed => BaseSkullSpeed * (1f + 0.15f * LevelOf(PriestUpgrade.SwiftBones) + Tree.ProjectileSpeed + Items.ProjectileSpeed);

    /// <summary>How long each skull flies.</summary>
    public float SkullLife =>
        (BaseSkullLife + 0.3f * LevelOf(PriestUpgrade.SwiftBones) + Tree.SkullLife + (Tree.Legion ? LegionLife : 0f)) * (1f + Items.Range);

    /// <summary>How many enemies a skull goes on through after the first it strikes.</summary>
    public int Pierce => BasePierce + LevelOf(PriestUpgrade.BoneSplinter) + (int)Tree.Pierce + Items.Chains;

    public float SeekRange => BaseSeekRange + Tree.SeekRange;

    /// <summary>The skull's crit chance (only the skull's hit can crit; the Plague and rot never do). Nothing of the Priest's own raises it: only generic items and gear.</summary>
    public float CritChance => BaseCritChance * MathF.Max(0f, 1f + Items.CritChance + Tree.CritChance);

    public float CritMultiplier => BaseCritMultiplier + Items.CritDamage + Tree.CritDamage;

    /// <summary>What every Priest hit on an elite or a boss is multiplied by.</summary>
    public float EliteMultiplier => 1f + Tree.EliteDamage + 0.15f * LevelOf(PriestUpgrade.Deathbringer);

    /// <summary>How much wider rot on the ground spreads.</summary>
    public float DecayArea => (1f + Tree.DecayArea + Items.Area) * Items.AreaMultiplier;

    /// <summary>Seconds more that rot on the ground lies (and twice as long with Necropolis).</summary>
    public float DecayTime(float seconds) => (seconds + Tree.DecayDuration + Items.Duration + Items.DecayDuration) * (Tree.Necropolis ? 2f : 1f);

    /// <summary>The chance a block spews Death and Decay (0 without it; every block with Mouth of the Grave).</summary>
    public float DecayChance => !Tree.DeathAndDecay ? 0f
        : Tree.MouthOfTheGrave ? 1f
        : MathF.Min(1f, DecayChanceBase + Tree.DecayChance + 0.05f * LevelOf(PriestUpgrade.SpreadingRot) + Items.DecayChance);

    /// <summary>How far Death and Decay's cone reaches.</summary>
    public float DecayConeLength => DecayConeReach * (Tree.MouthOfTheGrave ? 1f + MouthReach : 1f) * DecayArea;

    /// <summary>The chance to block a blow with the Skull Shield, capped at <see cref="MaxBlockChance"/>.</summary>
    public float BlockChance => MathF.Min(MaxBlockChance,
        (BaseBlockChance + Tree.BlockChance + 0.04f * LevelOf(PriestUpgrade.SkullWard) + Items.BlockChance + (Tree.MouthOfTheGrave ? MouthBlock : 0f)) * Items.BlockMultiplier);

    public float MaxHealth => (BaseMaxHealth + 20f * LevelOf(PriestUpgrade.GraveHardiness) + Items.MaxHealth + Tree.MaxHealth) * Items.MaxHealthMultiplier;

    public float Regeneration => 0.5f * LevelOf(PriestUpgrade.UnholyMending) + Items.Regeneration + Tree.Regeneration;

    public float DamageTaken => Items.DamageTaken * Tree.DamageTaken;

    public float MoveSpeed => BaseMoveSpeed * (1f + 0.08f * LevelOf(PriestUpgrade.RotWalker) + Items.MoveSpeed);

    public float PickupRadius => BasePickupRadius * (1f + 0.35f * LevelOf(PriestUpgrade.GraveRobber) + Items.Pickup);

    /// <summary>Seconds for Rotting Step to recharge.</summary>
    public float StepCooldown => BaseStepCooldown / (1f + Items.DashRecharge + Tree.StepRecharge);

    /// <summary>How wide Rotting Step's rot is.</summary>
    public float StepRotRadiusNow => StepRotRadius * DecayArea * (1f + Items.StepRot);
}
