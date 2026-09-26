using ArenaMaster.Game.Camp;
using ArenaMaster.Game.Combat;
using ArenaMaster.Game.Delve;
using ArenaMaster.Game.Gear;
using ArenaMaster.Game.Items;
using ArenaMaster.Game.Progression;
using Silk.NET.Maths;

namespace ArenaMaster.Game.Tests;

public class DelveMapTests
{
    [Fact]
    public void AFloor_HasThreeNodesOfDifferentKinds_TheSameEveryTime()
    {
        for (int depth = 1; depth <= 30; depth++)
        {
            var floor = DelveMap.Floor(depth);
            var runs = floor.Where(n => !n.IsBoss).ToList();
            Assert.Equal(DelveMap.NodesPerFloor, runs.Count);
            Assert.Equal(runs.Count, runs.Select(n => n.Kind).Distinct().Count());
            Assert.Equal(floor.Select(n => n.Kind), DelveMap.Floor(depth).Select(n => n.Kind));
            Assert.All(floor, n => Assert.Equal(depth, n.Depth));
        }
    }

    [Fact]
    public void BossNodes_AreOnEveryFifthFloor_AndOnlyThere()
    {
        for (int depth = 1; depth <= 30; depth++)
        {
            int bosses = DelveMap.Floor(depth).Count(n => n.IsBoss);
            Assert.Equal(depth % 5 == 0 ? 1 : 0, bosses);
        }
    }

    [Fact]
    public void ANode_IsFoundAgainByItsId()
    {
        foreach (var node in DelveMap.Floor(10))
        {
            Assert.Equal(node, DelveMap.Find(node.Id));
        }

        Assert.Null(DelveMap.Find("nonsense"));
        Assert.Null(DelveMap.Find("0-1"));
        Assert.Null(DelveMap.Find("3-9"));
    }

    [Fact]
    public void Bands_GetDarkerAndColderGoingDown()
    {
        Assert.Equal("The Greenwood", DelveBands.For(1).Name);
        Assert.Equal("The Amber Hollows", DelveBands.For(5).Name);
        Assert.Equal("The Mistdeep", DelveBands.For(12).Name);
        Assert.Equal("The Night Hollows", DelveBands.For(15).Name);
        Assert.Equal("The Frozen Deep", DelveBands.For(99).Name);
        Assert.True(DelveBands.For(20).Snow > 0f);
    }
}

public class DelveRulesTests
{
    [Fact]
    public void OnlyTheFirstFloor_IsOpen_AtTheStart()
    {
        var save = new DelveSave();
        Assert.All(DelveMap.Floor(1), n => Assert.True(DelveRules.IsOpen(save, n)));
        Assert.All(DelveMap.Floor(2), n => Assert.False(DelveRules.IsOpen(save, n)));
    }

    [Fact]
    public void ClearingAnyNode_OpensTheFloorBelow_AndLeavesTheOthersOpen()
    {
        var save = new DelveSave();
        var floor = DelveMap.Floor(1);
        DelveRules.Clear(save, floor[1]);

        Assert.Equal(2, save.Deepest);
        Assert.False(DelveRules.IsOpen(save, floor[1]));
        Assert.True(DelveRules.IsCleared(save, floor[1]));
        Assert.True(DelveRules.IsOpen(save, floor[0]));
        Assert.True(DelveRules.IsOpen(save, floor[2]));
        Assert.All(DelveMap.Floor(2), n => Assert.True(DelveRules.IsOpen(save, n)));

        // Going back to a shallower floor doesn't close the deeper ones.
        DelveRules.Clear(save, floor[0]);
        Assert.Equal(2, save.Deepest);
        Assert.Equal(2, save.Cleared.Count);
    }

    [Fact]
    public void ClearingANodeTwice_CountsItOnce()
    {
        var save = new DelveSave();
        var node = DelveMap.Floor(1)[0];
        DelveRules.Clear(save, node);
        DelveRules.Clear(save, node);
        Assert.Single(save.Cleared);
        Assert.Equal(1, save.ClearedByKind[node.Kind.ToString()]);
    }

    [Fact]
    public void ABossNode_CountsTheBoss()
    {
        var save = new DelveSave { Deepest = 5 };
        var boss = DelveMap.Floor(5).Single(n => n.IsBoss);
        DelveRules.Clear(save, boss);
        Assert.Equal(1, save.BossesSlain);
        Assert.Equal(6, save.Deepest);
    }

    [Fact]
    public void EachKind_PaysWhatItSays_AndDeeperPaysMore()
    {
        DelveNode Node(int depth, DelveNodeKind kind) => new(depth, 0, kind);
        var currency = DelveRules.Reward(Node(3, DelveNodeKind.Currency));
        var armoury = DelveRules.Reward(Node(3, DelveNodeKind.Armoury));
        var knowledge = DelveRules.Reward(Node(3, DelveNodeKind.Knowledge));
        var relic = DelveRules.Reward(Node(3, DelveNodeKind.Relic));
        var boss = DelveRules.Reward(Node(10, DelveNodeKind.Boss));

        Assert.Equal(armoury.Silver * 4, currency.Silver);
        Assert.True(armoury.Gear);
        Assert.True(knowledge.TreeExperience > 0);
        Assert.Equal(2, relic.Items);
        Assert.True(boss.Gear && boss.Items == 1 && boss.Marks == 3);
        Assert.True(DelveRules.Reward(Node(9, DelveNodeKind.Currency)).Silver > currency.Silver);
    }

    [Fact]
    public void DeeperFloors_AreTougher_AndTheArenaStartsHigher()
    {
        Assert.Equal(1f, DelveRules.HealthMultiplier(1));
        Assert.True(DelveRules.HealthMultiplier(10) > DelveRules.HealthMultiplier(5));
        Assert.True(DelveRules.DamageMultiplier(10) > DelveRules.DamageMultiplier(5));
        Assert.Equal(15, DelveRules.ArenaLevel(5));
        Assert.Equal(17, DelveRules.ArenaLevel(10));
        Assert.True(DelveRules.ArenaBossHealth(10) > DelveRules.ArenaBossHealth(5));
    }

    [Fact]
    public void APlan_KnowsItsKind()
    {
        var run = DelveMap.Floor(5).First(n => !n.IsBoss);
        var boss = DelveMap.Floor(5).Single(n => n.IsBoss);
        Assert.Equal(RunKind.Delve, RunPlan.For(run).Kind);
        Assert.Equal(RunKind.Arena, RunPlan.For(boss).Kind);
        Assert.Equal(5, RunPlan.For(run).Depth);
        Assert.False(RunPlan.Classic.IsDelve);
        Assert.Equal(0, RunPlan.Classic.Depth);
        Assert.Contains("Amber Hollows", RunPlan.For(run).Title);
    }
}

public class DelveDirectorTests
{
    private static EnemyField Field() => new(new Random(1));

    [Fact]
    public void OneBrute_AtFiveMinutes_ThenTheKingAndTwoBrutes_AtTen()
    {
        var director = new DelveDirector(1);
        var field = Field();
        Assert.Equal(new DirectorOrders(0, false, false), director.Update(299f, field, null));
        Assert.Equal(1, director.Update(300f, field, null).Elites);
        Assert.Equal(0, director.Update(420f, field, null).Elites);
        var boss = director.Update(600f, field, null);
        Assert.True(boss.Boss);
        Assert.Equal(DelveDirector.ElitesWithBoss, boss.Elites);
        Assert.False(director.Update(700f, field, null).Boss);   // one King
    }

    [Fact]
    public void ThreeMoreBrutes_WhenTheKingIsAtHalf_Once()
    {
        var director = new DelveDirector(1);
        var field = Field();
        director.Update(600f, field, null);
        var king = field.Spawn(Vector3D<float>.Zero, EnemyKind.HollowKing);

        king.Health = king.MaxHealth * 0.6f;
        Assert.Equal(0, director.Update(610f, field, king).Elites);
        king.Health = king.MaxHealth * 0.5f;
        Assert.Equal(DelveDirector.ElitesAtHalf, director.Update(611f, field, king).Elites);
        king.Health = king.MaxHealth * 0.2f;
        Assert.Equal(0, director.Update(612f, field, king).Elites);
    }

    [Fact]
    public void ASkipPastBoth_FiresEachOnce()
    {
        var director = new DelveDirector(1);
        var orders = director.Update(660f, Field(), null);
        Assert.True(orders.Boss);
        Assert.Equal(1 + DelveDirector.ElitesWithBoss, orders.Elites);
    }

    [Fact]
    public void TheSwarm_RampsFaster_AndDeeperFloorsReachFurther()
    {
        Assert.Equal(10f, DelveDirector.PeakMinutes(1));
        Assert.Equal(28f, DelveDirector.PeakMinutes(50));
        var shallow = new DelveDirector(1);
        var deep = new DelveDirector(9);
        Assert.Equal(600f, shallow.ClassicSeconds(600f));
        Assert.Equal(300f, shallow.ClassicSeconds(300f));
        Assert.Equal(600f, shallow.ClassicSeconds(900f));   // holds at the boss's strength
        Assert.True(deep.ClassicSeconds(600f) > shallow.ClassicSeconds(600f));

        var a = Field();
        var b = Field();
        shallow.Update(600f, a, null);
        deep.Update(600f, b, null);
        Assert.True(b.TargetCount > a.TargetCount);
        Assert.True(b.Scaling.Health > a.Scaling.Health * DelveRules.HealthMultiplier(9) * 0.99f);
    }
}

public class BossFightTests
{
    private const float Step = 1f / 60f;

    private static float? FlatGround(float x, float z) => MathF.Abs(x) < 200f && MathF.Abs(z) < 200f ? 0f : null;

    private static EnemyField QuietField() => new(new Random(3)) { TargetCount = 0 };

    private static void Tick(EnemyField field, PlayerHealth player, Vector3D<float> feet) =>
        field.Update(Step, new PlayerTarget(feet, true, player, new PlayerCondition()), FlatGround);

    [Fact]
    public void TheUnboundKing_GoesThroughHisStages_AsHisHealthFalls()
    {
        var field = QuietField();
        var king = field.Spawn(Vector3D<float>.Zero, DelveBosses.HollowKingUnbound);
        var player = new PlayerHealth(1_000_000f);
        var far = new Vector3D<float>(100f, 0f, 0f);

        Tick(field, player, far);
        Assert.Equal(-1, king.PhaseIndex);
        Assert.Empty(field.TakePhaseChanges());

        king.Health = king.MaxHealth * 0.6f;
        Tick(field, player, far);
        Assert.Equal(0, king.PhaseIndex);
        var change = Assert.Single(field.TakePhaseChanges());
        Assert.Contains("court", change.Phase.Announcement);
        Assert.Contains(king.AttacksNow, a => a.Type == AttackType.Summon);
        Assert.True(king.WalkSpeed > king.Speed);

        king.Health = king.MaxHealth * 0.1f;
        Tick(field, player, far);
        Assert.Equal(1, king.PhaseIndex);
        Assert.Single(field.TakePhaseChanges());
        Assert.Equal(DelveBosses.HollowKingUnbound.Phases[1].AttackCooldown, king.AttackCooldownNow);
    }

    [Fact]
    public void AFallStraightToTheLastStage_SkipsTheMiddleOne()
    {
        var field = QuietField();
        var king = field.Spawn(Vector3D<float>.Zero, DelveBosses.HollowKingUnbound);
        king.Health = 1f;
        Tick(field, new PlayerHealth(100f), new Vector3D<float>(100f, 0f, 0f));
        Assert.Equal(1, king.PhaseIndex);
        Assert.Single(field.TakePhaseChanges());
    }

    [Fact]
    public void AChainedLeap_LeapsAgain_BeforeResting()
    {
        var leap = new AttackSpec(AttackType.LeapSlam, MinRange: 0f, MaxRange: 50f, WindUp: 0.5f, Active: 0.5f, Recover: 0.5f, Damage: 1f, Reach: 2f, LeapHeight: 2f, Chain: 2);
        var kind = EnemyKind.Brute with { Attacks = new[] { leap }, AttackCooldown = 99f };
        var field = QuietField();
        var enemy = field.Spawn(Vector3D<float>.Zero, kind);
        enemy.AttackCooldown = 0f;
        var player = new PlayerHealth(1_000_000f);
        var feet = new Vector3D<float>(8f, 0f, 0f);

        int windUps = 0;
        AttackPhase? last = null;
        bool rested = false;
        for (int i = 0; i < 60 * 6 && !rested; i++)
        {
            Tick(field, player, feet);
            if (enemy.Attack is not null && enemy.AttackPhase == AttackPhase.WindUp && last != AttackPhase.WindUp)
            {
                windUps++;
            }

            rested = enemy.Attack is null && last is not null;
            last = enemy.Attack is null ? null : enemy.AttackPhase;
        }

        Assert.True(rested);
        Assert.Equal(3, windUps);   // the leap and two more
    }

    [Fact]
    public void ABarrage_DropsItsFireballs_AroundThePlayer_TheFirstRightOnThem()
    {
        var rain = new AttackSpec(AttackType.Barrage, MinRange: 0f, MaxRange: 50f, WindUp: 0.2f, Active: 0.1f, Recover: 0.2f, Damage: 10f, Reach: 6f,
            ProjectileSpeed: 11f, Splash: 2f, ProjectileModel: "ghoul_fireball.glb", Count: 6);
        var kind = EnemyKind.Brute with { Attacks = new[] { rain }, AttackCooldown = 99f };
        var field = QuietField();
        var enemy = field.Spawn(Vector3D<float>.Zero, kind);
        enemy.AttackCooldown = 0f;
        var feet = new Vector3D<float>(10f, 0f, 0f);
        var player = new PlayerHealth(1_000_000f);

        for (int i = 0; i < 20 && field.Bolts.Count == 0; i++)
        {
            Tick(field, player, feet);
        }

        Assert.Equal(6, field.Bolts.Count);
        Assert.All(field.Bolts, b =>
        {
            Assert.True(b.Splash > 0f);
            Assert.True(Vector3D.Distance(b.Target, feet) <= 6f + 1e-3f);
            Assert.True(b.Position.Y > 10f);   // falling from the sky
        });
        Assert.Contains(field.Bolts, b => Vector3D.Distance(b.Target, feet) < 1e-3f);
    }

    [Fact]
    public void ABarrage_Hurts_WhereItLands()
    {
        var rain = new AttackSpec(AttackType.Barrage, MinRange: 0f, MaxRange: 50f, WindUp: 0.2f, Active: 0.1f, Recover: 0.2f, Damage: 10f, Reach: 0f,
            ProjectileSpeed: 11f, Splash: 2f, ProjectileModel: "ghoul_fireball.glb", Count: 1);
        var kind = EnemyKind.Brute with { Attacks = new[] { rain }, AttackCooldown = 99f };
        var field = QuietField();
        var enemy = field.Spawn(Vector3D<float>.Zero, kind);
        enemy.AttackCooldown = 0f;
        var player = new PlayerHealth(100f);
        for (int i = 0; i < 60 * 4; i++)
        {
            player.Update(Step);
            Tick(field, player, new Vector3D<float>(12f, 0f, 0f));
        }

        Assert.True(player.Current < 100f);
    }
}

public class GearTests
{
    [Fact]
    public void ThereAreFourUniquesForEachSlot()
    {
        Assert.Equal(12, GearCatalog.All.Count);
        Assert.Equal(GearCatalog.All.Count, GearCatalog.All.Select(p => p.Id).Distinct().Count());
        foreach (var slot in Enum.GetValues<GearSlot>())
        {
            Assert.Equal(4, GearCatalog.All.Count(p => p.Slot == slot));
        }

        Assert.All(GearCatalog.All, p => Assert.False(string.IsNullOrWhiteSpace(p.Flavour)));
    }

    [Fact]
    public void AFind_GoesToAnEmptySlot_AndIsWorn_UntilEveryPieceIsOwned()
    {
        var profile = new Profile();
        var random = new Random(4);
        var first = GearCatalog.Grant(profile, random)!;
        Assert.Equal(first, GearCatalog.WornIn(profile, first.Slot));

        var second = GearCatalog.Grant(profile, random)!;
        Assert.NotEqual(first.Slot, second.Slot);   // an empty slot's piece first
        var third = GearCatalog.Grant(profile, random)!;
        Assert.Equal(3, GearCatalog.Worn(profile).Count());
        Assert.Equal(3, new[] { first.Slot, second.Slot, third.Slot }.Distinct().Count());

        for (int i = 3; i < GearCatalog.All.Count; i++)
        {
            Assert.NotNull(GearCatalog.Grant(profile, random));
        }

        Assert.Null(GearCatalog.Grant(profile, random));
        Assert.Equal(GearCatalog.All.Count, profile.Gear.Owned.Distinct().Count());
    }

    [Fact]
    public void OnlyOwnedGear_CanBeWorn_AndItCanBeTakenOff()
    {
        var profile = new Profile();
        var fang = GearCatalog.Find("tempest_fang")!;
        Assert.False(GearCatalog.Wear(profile, fang));
        profile.Gear.Worn[GearSlot.Weapon.ToString()] = fang.Id;   // a hand-edited save
        Assert.Null(GearCatalog.WornIn(profile, GearSlot.Weapon));

        profile.Gear.Owned.Add(fang.Id);
        Assert.True(GearCatalog.Wear(profile, fang));
        Assert.Equal(fang, GearCatalog.WornIn(profile, GearSlot.Weapon));
        GearCatalog.TakeOff(profile, GearSlot.Weapon);
        Assert.Empty(GearCatalog.Worn(profile));
    }

    [Fact]
    public void WornGear_AddsToTheRunsBonuses_WithoutBeingAnItem()
    {
        var inventory = new ItemInventory();
        inventory.Add(ItemCatalog.All.First(i => i.Id == "swiftwind_sigil"));
        inventory.AddBonus(GearCatalog.Find("tempest_fang")!.Apply);
        Assert.Equal(1.15f * 1.2f, inventory.Bonuses.AttackSpeedMultiplier, 4);
        Assert.Single(inventory.Items);

        inventory.Clear();
        Assert.Equal(1f, inventory.Bonuses.AttackSpeedMultiplier);
    }

    [Fact]
    public void TheVestmentsShield_TakesTheHitFirst_AndComesBackAfterItsCooldown()
    {
        var bonuses = new ItemBonuses();
        GearCatalog.Find("great_mages_vestments")!.Apply(bonuses);
        var effects = new ItemEffects();
        effects.Begin();
        var health = new PlayerHealth(100f) { Barrier = 20f };
        var field = new EnemyField(new Random(1)) { TargetCount = 0 };
        var hits = new List<ItemHit>();

        effects.Update(0.1f, Vector3D<float>.Zero, bonuses, field, health, false, hits);
        Assert.Equal(100f, health.Shield);

        health.TakeDamage(30f);
        Assert.Equal(70f, health.Shield);
        Assert.Equal(20f, health.Barrier);   // the gear shield goes first
        Assert.Equal(100f, health.Current);

        health.Update(1f);
        health.TakeDamage(90f);                 // breaks it, then the barrier takes the rest
        Assert.Equal(0f, health.Shield);
        Assert.Equal(0f, health.Barrier);
        Assert.Equal(100f, health.Current);

        effects.Update(0.1f, Vector3D<float>.Zero, bonuses, field, health, false, hits);
        Assert.True(effects.ShieldReturnsIn > 9f);
        for (int i = 0; i < 95; i++)
        {
            effects.Update(0.1f, Vector3D<float>.Zero, bonuses, field, health, false, hits);
        }

        Assert.Equal(0f, health.Shield);
        for (int i = 0; i < 10; i++)
        {
            effects.Update(0.1f, Vector3D<float>.Zero, bonuses, field, health, false, hits);
        }

        Assert.Equal(100f, health.Shield);
    }

    [Fact]
    public void Emberbrand_BurnsWhatIsNear_EveryFiveSeconds()
    {
        var bonuses = new ItemBonuses();
        GearCatalog.Find("emberbrand")!.Apply(bonuses);
        var effects = new ItemEffects();
        effects.Begin();
        var field = new EnemyField(new Random(1)) { TargetCount = 0 };
        var near = field.Spawn(new Vector3D<float>(3f, 0f, 0f), EnemyKind.Brute);
        var far = field.Spawn(new Vector3D<float>(9f, 0f, 0f), EnemyKind.Brute);
        var hits = new List<ItemHit>();

        effects.Update(0.1f, Vector3D<float>.Zero, bonuses, field, new PlayerHealth(100f), false, hits);
        Assert.Equal(near.MaxHealth - 60f, near.Health, 2);
        Assert.Equal(far.MaxHealth, far.Health);
        Assert.Single(effects.Novas);

        for (int i = 0; i < 45; i++)
        {
            effects.Update(0.1f, Vector3D<float>.Zero, bonuses, field, new PlayerHealth(100f), false, hits);
        }

        Assert.Equal(near.MaxHealth - 60f, near.Health, 2);   // not yet again
        for (int i = 0; i < 6; i++)
        {
            effects.Update(0.1f, Vector3D<float>.Zero, bonuses, field, new PlayerHealth(100f), false, hits);
        }

        Assert.Equal(near.MaxHealth - 120f, near.Health, 2);
    }
}

public class DelveBountyTests
{
    private static TreeProgress Tree() => new(Ranger.SharpshooterTree.Tree, new TreeSave());

    [Fact]
    public void EveryBounty_HasItsOwnId()
    {
        Assert.Equal(Bounties.All.Count, Bounties.All.Select(b => b.Id).Distinct().Count());
    }

    [Fact]
    public void DepthBounties_PayOnceTheFloorIsOpen()
    {
        var profile = new Profile();
        profile.Delve.Deepest = 10;
        profile.Delve.Cleared.AddRange(Enumerable.Range(1, 9).Select(d => $"{d}-0"));
        var done = Bounties.Settle(new RunRecord(0, 0, 0, 600f, false, 1), profile, Tree()).Select(b => b.Id).ToList();
        Assert.Contains("into_the_dark", done);
        Assert.Contains("deeper_still", done);
        Assert.Contains("the_mistdeep", done);
        Assert.DoesNotContain("night_walker", done);
    }

    [Fact]
    public void ClassDepthBounties_AskForTheClass_TheDepth_AndAClear()
    {
        var profile = new Profile();
        var deepMage = new RunRecord(100, 0, 1, 640f, false, 20, "mage", Depth: 12, DelveCleared: true);
        var done = Bounties.Settle(deepMage, profile, Tree()).Select(b => b.Id).ToList();
        Assert.Contains("deep_mage", done);
        Assert.Contains("swift_delve", done);
        Assert.DoesNotContain("deep_ranger", done);

        var failed = new RunRecord(100, 0, 0, 300f, false, 10, "ranger", Depth: 12, DelveCleared: false);
        Assert.DoesNotContain("deep_ranger", Bounties.Settle(failed, profile, Tree()).Select(b => b.Id));
    }
}

public class ArenaTests
{
    [Fact]
    public void TheArenaWall_GoesAllTheWayRound()
    {
        var pieces = BossArena.WallPieces().Where(p => p.Model == CampWalls.PieceModel).ToList();
        Assert.True(pieces.Count * CampWalls.PieceLength >= MathF.Tau * BossArena.Radius);
        Assert.All(pieces, p => Assert.InRange(p.Offset.Length, BossArena.Radius, BossArena.Radius + 1f));
    }

    [Fact]
    public void TheArena_IsFarFromCampAndTheRuns_AndEverythingInItIsInside()
    {
        Assert.True(Vector2D.Distance(BossArena.Centre, CampLayout.Centre) > 200f);
        Assert.True(Vector2D.Distance(BossArena.Centre, CampLayout.RunStart) > 150f + EnemyField.LeashDistance);
        Assert.True(Vector2D.Distance(BossArena.Start, BossArena.Centre) < BossArena.Radius - 2f);
        Assert.All(BossArena.Decor, d => Assert.True(d.Offset.Length < BossArena.Radius - 0.5f));
    }

    [Fact]
    public void TheArenasFires_DontClashWithCamps_AndStayUnderTheEnginesCap()
    {
        var ids = BossArena.Fires().Select(f => f.Id).Concat(CampLayout.SmallFires().Select(f => f.Id)).Append(CampLayout.FireId).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
        Assert.True(ids.Count <= 16);
    }
}

public class DelveSaveTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"arenamaster-delve-{Guid.NewGuid():N}.json");

    public void Dispose()
    {
        if (File.Exists(_path))
        {
            File.Delete(_path);
        }
    }

    [Fact]
    public void TheDelveAndTheGear_AreSavedAndReadBack()
    {
        var profile = new Profile();
        DelveRules.Clear(profile.Delve, DelveMap.Floor(1)[2]);
        profile.Delve.Marks = 7;
        GearCatalog.Grant(profile, new Random(2));
        ProfileStore.Save(profile, _path);

        var back = ProfileStore.Load(_path);
        Assert.Equal(2, back.Delve.Deepest);
        Assert.Equal(profile.Delve.Cleared, back.Delve.Cleared);
        Assert.Equal(7, back.Delve.Marks);
        Assert.Equal(profile.Gear.Owned, back.Gear.Owned);
        Assert.Equal(GearCatalog.Worn(profile), GearCatalog.Worn(back));
    }

    [Fact]
    public void AnOldSave_StartsAtTheTopOfTheDelve_WithNoGear()
    {
        File.WriteAllText(_path, "{\"Version\":1,\"Silver\":50}");
        var profile = ProfileStore.Load(_path);
        Assert.Equal(1, profile.Delve.Deepest);
        Assert.Empty(profile.Gear.Owned);
        Assert.Equal(50, profile.Silver);
    }
}
