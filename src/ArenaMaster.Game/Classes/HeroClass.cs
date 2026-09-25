using ArenaMaster.Game.Combat;
using ArenaMaster.Game.Items;
using ArenaMaster.Game.Progression;
using ArenaMaster.Game.Ui;
using CEngine.Core;
using Silk.NET.Maths;

namespace ArenaMaster.Game.Classes;

/// <summary>What a class's attacks see in one frame of a run.</summary>
internal sealed record RunFrame(
    EngineWindow Window,
    float DeltaSeconds,
    float RunSeconds,
    EnemyField Enemies,
    DamageNumbers Numbers,
    Func<float, float, float?> GroundAt,
    PlayerHealth Health,
    PlayerCondition Condition,
    bool StandingStill);

/// <summary>
/// A playable class, as the rest of the game sees it: the seam between the content (camp, the run, the HUD) and everything the class owns - its body and
/// movement, its attacks, its level-up pool and its passive tree. Classes share nothing through here; each one implements all of it in its own folder
/// (<c>Ranger/</c>, <c>Paladin/</c>). The content holds one of each and runs whichever the profile has chosen.
/// </summary>
internal interface IHeroClass
{
    /// <summary>The id the profile saves ("ranger"), and the first half of its trees' save keys.</summary>
    string Id { get; }

    string Name { get; }

    /// <summary>A line or two for the class picker: how it fights.</summary>
    string Summary { get; }

    /// <summary>Its passive tree (one per class for now).</summary>
    TreeDefinition Tree { get; }

    float MaxHealth { get; }

    float PickupRadius { get; }

    /// <summary>Health back per second, always on.</summary>
    float Regeneration { get; }

    /// <summary>What every hit's damage is multiplied by.</summary>
    float DamageTaken { get; }

    /// <summary>The chance (0 to 1) an enemy's blow is turned aside completely.</summary>
    float BlockChance { get; }

    /// <summary>What the Shift move is called on the HUD, and how charged it is (0 to 1).</summary>
    string DashLabel { get; }

    float DashReadiness { get; }

    /// <summary>The Shift move's push this frame, which the content adds to any knock-back.</summary>
    Vector3D<float> DashVelocity { get; }

    /// <summary>A short line under the crosshair (a readied shot, a shield up), or null.</summary>
    string? Status { get; }

    /// <summary>The ranks spent in its tree changed (or a run is about to start): take their bonuses.</summary>
    void UseTree(IReadOnlyDictionary<string, int> ranks);

    /// <summary>A fresh run: no upgrades, <paramref name="items"/> the loadout's bonuses, and <paramref name="health"/> reset to full at the new <see cref="MaxHealth"/>.</summary>
    void BeginRun(ItemBonuses items, PlayerHealth health);

    /// <summary>Back at camp: no upgrades, no items.</summary>
    void ReturnToCamp();

    /// <summary>Walking, the Shift move and the body, every frame at camp and on a run. A stunned player can't move.</summary>
    void Move(EngineWindow window, float deltaSeconds, bool stunned);

    /// <summary>Takes the body out of the world (another class was chosen).</summary>
    void Hide(EngineWindow window);

    /// <summary>The class's attacks for this frame, before the enemies move.</summary>
    void Attack(RunFrame frame);

    /// <summary>After the enemies have moved: answers to what they did (the blows in <see cref="EnemyField.Strikes"/>, who is touching).</summary>
    void Answer(RunFrame frame);

    void OnKill(Enemy killed, float runSeconds);

    /// <summary>Takes everything of the run's attacks out of the world (the run ended).</summary>
    void Clear(EngineWindow window);

    /// <summary>Three upgrades to choose from (or a heal once all are maxed), avoiding any banished this run.</summary>
    IReadOnlyList<LevelUpCard> RollLevelUp(Random random);

    /// <summary>Strikes the card at <paramref name="index"/> from the pool for the rest of the run and returns the cards with a fresh one in its place.</summary>
    IReadOnlyList<LevelUpCard> BanishCard(int index, Random random);

    /// <summary>Takes the card at <paramref name="index"/> of the last roll.</summary>
    void TakeCard(int index, PlayerHealth health);
}
