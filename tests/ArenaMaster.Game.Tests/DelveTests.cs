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
    public void AFloor_MixesDescentsWithKingNodesOfDifferentKinds_TheSameEveryTime()
    {
        for (int depth = 1; depth <= 30; depth++)
        {
            var floor = DelveMap.Floor(depth);
            var runs = floor.ToList();
            Assert.Equal(DelveMap.NodesPerFloor, runs.Count);
            var kings = runs.Where(n => n.HasKing).ToList();
            Assert.InRange(kings.Count, DelveMap.FewestKingNodes, DelveMap.FewestKingNodes + 1);
            Assert.Equal(kings.Count, kings.Select(n => n.Kind).Distinct().Count());
            Assert.All(runs.Where(n => !n.HasKing), n => Assert.Equal(DelveNodeKind.Descent, n.Kind));
            Assert.Equal(Enumerable.Range(0, floor.Count), floor.Select(n => n.Slot));
            Assert.Equal(floor.Select(n => n.Kind), DelveMap.Floor(depth).Select(n => n.Kind));
            Assert.All(floor, n => Assert.Equal(depth, n.Depth));
        }
    }

    [Fact]
    public void ADescent_HasNoKing_AndPaysAModestCache()
    {
        var descent = new DelveNode(4, 0, DelveNodeKind.Descent);
        Assert.False(descent.HasKing);
        Assert.True(new DelveNode(4, 0, DelveNodeKind.Relic).HasKing);

        var reward = DelveRules.Reward(descent);
        var currency = DelveRules.Reward(new DelveNode(4, 0, DelveNodeKind.Currency));
        Assert.True(reward.Silver < currency.Silver && reward.TreeExperience > 0 && reward.Items == 0);
    }

    [Fact]
    public void TheChart_HasNoArmouryOrBossNodes_GearIsTheBossHuntsNow()
    {
        var kinds = new[] { DelveNodeKind.Currency, DelveNodeKind.Knowledge, DelveNodeKind.Relic, DelveNodeKind.Descent };
        for (int depth = 1; depth <= 30; depth++)
        {
            Assert.Equal(DelveMap.NodesPerFloor, DelveMap.Floor(depth).Count);
            Assert.All(DelveMap.Floor(depth), n => Assert.Contains(n.Kind, kinds));
        }

        Assert.Null(DelveMap.Find("5-5"));   // an old save's Boss node is gone
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
    public void EachKind_PaysWhatItSays_AndDeeperPaysMore()
    {
        DelveNode Node(int depth, DelveNodeKind kind) => new(depth, 0, kind);
        var currency = DelveRules.Reward(Node(3, DelveNodeKind.Currency));
        var knowledge = DelveRules.Reward(Node(3, DelveNodeKind.Knowledge));
        var relic = DelveRules.Reward(Node(3, DelveNodeKind.Relic));

        Assert.Equal(knowledge.Silver * 4, currency.Silver);
        Assert.True(knowledge.TreeExperience > 0);
        Assert.True(relic.Items == 1 && relic.ItemOdds == RarityWeights.Elite);
        Assert.True(DelveRules.Reward(Node(9, DelveNodeKind.Currency)).Silver > currency.Silver);
    }

    [Fact]
    public void TheBossHunt_GearIsPureLuck_AboutOneKillInFive()
    {
        var random = new Random(11);
        var king = BossHunt.HollowKing;
        int drops = Enumerable.Range(0, 4000).Count(_ => king.RollsGear(random));
        Assert.InRange(drops / 4000f, king.GearChance - 0.03f, king.GearChance + 0.03f);
        Assert.Equal(0.25f, king.GearChance);
        Assert.True(king.Health > DelveBosses.HollowKingUnbound.MaxHealth);
    }

    [Fact]
    public void DeeperFloors_AreTougher_ButNotAWall()
    {
        Assert.Equal(1f, DelveRules.HealthMultiplier(1));
        Assert.True(DelveRules.HealthMultiplier(10) > DelveRules.HealthMultiplier(5));
        Assert.True(DelveRules.DamageMultiplier(10) > DelveRules.DamageMultiplier(5));
        Assert.Equal(1.56f, DelveRules.HealthMultiplier(8), 3);   // gentler than before: depth 8 was 2.05
    }

    [Fact]
    public void APlan_KnowsItsKind()
    {
        var run = DelveMap.Floor(5)[0];
        Assert.Equal(RunKind.Delve, RunPlan.For(run).Kind);
        var hunt = RunPlan.Hunt(BossHunt.HollowKing);
        Assert.Equal(RunKind.Arena, hunt.Kind);
        Assert.True(hunt.IsDelve);   // it ends in a cache
        Assert.Equal(0, hunt.Depth);
        Assert.Null(hunt.Node);
        Assert.Equal(5, RunPlan.For(run).Depth);
        Assert.False(RunPlan.Classic.IsDelve);
        Assert.Equal(0, RunPlan.Classic.Depth);
        Assert.Contains("Amber Hollows", RunPlan.For(run).Title);
    }
}

public class DelveDirectorTests
{
    [Fact]
    public void ADescent_CallsNoKing_TheCacheIsDueAt10()
    {
        var field = new EnemyField(new Random(1));
        var director = new DelveDirector(1, king: false);
        Assert.False(director.Update(599f, field, null).Boss);
        Assert.False(director.CacheDue);

        var orders = director.Update(DelveDirector.BossAt, field, null);
        Assert.False(orders.Boss);
        Assert.Equal(0, orders.Elites);   // no Brutes at 10:00 without the King
        Assert.True(director.CacheDue);
        Assert.False(director.BossCalled);
    }

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
        Assert.Equal(20f, DelveDirector.PeakMinutes(50));
        Assert.Equal(13.5f, DelveDirector.PeakMinutes(8));
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

        king.Health = king.MaxHealth * 0.3f;
        Tick(field, player, far);
        Assert.Equal(1, king.PhaseIndex);
        Assert.Contains("enraged", Assert.Single(field.TakePhaseChanges()).Phase.Announcement);

        king.Health = king.MaxHealth * 0.1f;
        Tick(field, player, far);
        Assert.Equal(2, king.PhaseIndex);
        Assert.Contains("last stand", Assert.Single(field.TakePhaseChanges()).Phase.Announcement);
        Assert.Equal(DelveBosses.HollowKingUnbound.Phases[2].AttackCooldown, king.AttackCooldownNow);
    }

    [Fact]
    public void AFallStraightToTheLastStage_SkipsTheOnesBetween()
    {
        var field = QuietField();
        var king = field.Spawn(Vector3D<float>.Zero, DelveBosses.HollowKingUnbound);
        king.Health = 1f;
        Tick(field, new PlayerHealth(100f), new Vector3D<float>(100f, 0f, 0f));
        Assert.Equal(DelveBosses.HollowKingUnbound.Phases.Count - 1, king.PhaseIndex);
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
    public void TheCrownOfFire_RingsThePlayer_WithOneGap_AndOneOnThem()
    {
        var king = DelveBosses.HollowKingUnbound;
        var crown = king.Attacks.Single(a => a.Pattern == BarragePattern.Ring);
        var feet = new Vector3D<float>(10f, 0f, 4f);
        var spots = QuietField().BarrageSpots(Vector3D<float>.Zero, crown, feet);

        Assert.Equal(crown.Count, spots.Count);
        Assert.Contains(spots, s => s.X == feet.X && s.Z == feet.Z);
        var ring = spots.Where(s => s.X != feet.X || s.Z != feet.Z).ToList();
        Assert.All(ring, s => Assert.Equal(crown.Reach, MathF.Sqrt((s.X - feet.X) * (s.X - feet.X) + (s.Z - feet.Z) * (s.Z - feet.Z)), 3));

        // The widest space between neighbours round the ring is the gap: wide enough to get through between two bursts.
        var angles = ring.Select(s => MathF.Atan2(s.X - feet.X, s.Z - feet.Z)).OrderBy(a => a).ToList();
        float widest = angles.Zip(angles.Skip(1), (a, b) => b - a).Append(angles[0] + MathF.Tau - angles[^1]).Max();
        float gap = widest * crown.Reach - 2f * crown.Splash;
        Assert.InRange(gap, 1.2f, 4f);
    }

    [Fact]
    public void TheLineOfFire_RunsFromTheKingThroughThePlayer_LandingOutward()
    {
        var line = DelveBosses.HollowKingUnbound.Phases[0].Attacks.Single(a => a.Pattern == BarragePattern.Line);
        var feet = new Vector3D<float>(12f, 0f, 0f);
        var spots = QuietField().BarrageSpots(Vector3D<float>.Zero, line, feet);

        Assert.All(spots, s => Assert.Equal(0f, s.Z, 3));                 // on the line from him to the player
        Assert.Contains(spots, s => MathF.Abs(s.X - feet.X) < EnemyField.LineSpacing);
        Assert.True(spots.Max(s => s.X) > feet.X + line.Reach - EnemyField.LineSpacing);   // and on past them
        Assert.Equal(spots.OrderBy(s => s.X).Select(s => s.Delay), spots.Select(s => s.Delay));   // landing one after another, outward
        Assert.True(spots[^1].Delay > spots[0].Delay);
    }

    [Fact]
    public void TheUnboundKing_HasMoreToDodge_ThanBefore()
    {
        var king = DelveBosses.HollowKingUnbound;
        Assert.Equal(3, king.Phases.Count);
        Assert.Contains(king.Attacks, a => a.Pattern == BarragePattern.Ring);
        Assert.All(king.Phases, phase => Assert.Contains(phase.Attacks, a => a.Pattern == BarragePattern.Line));
        Assert.True(king.Phases[^1].AttackCooldown < king.Phases[^2].AttackCooldown);
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
    public void ThereAreSixUniquesForEachSlot()
    {
        Assert.Equal(36, GearCatalog.All.Count);
        Assert.Equal(GearCatalog.All.Count, GearCatalog.All.Select(p => p.Id).Distinct().Count());
        foreach (var slot in Enum.GetValues<GearSlot>())
        {
            Assert.Equal(6, GearCatalog.All.Count(p => p.Slot == slot));
        }

        Assert.All(GearCatalog.All, p => Assert.False(string.IsNullOrWhiteSpace(p.Flavour)));
        Assert.All(GearCatalog.All, p => Assert.Contains(p.Stats, s => s.Rolls));   // every piece has something to roll
    }

    [Fact]
    public void ARoll_FallsWithinEachStatsRange()
    {
        var random = new Random(9);
        foreach (var piece in GearCatalog.All)
        {
            for (int n = 0; n < 50; n++)
            {
                var item = GearCatalog.RollCopy(piece, random);
                Assert.Equal(piece.Stats.Count, item.Rolls.Count);
                for (int i = 0; i < piece.Stats.Count; i++)
                {
                    var stat = piece.Stats[i];
                    Assert.InRange(item.Rolls[i], MathF.Min(stat.Min, stat.Max) - 1e-4f, MathF.Max(stat.Min, stat.Max) + 1e-4f);
                }

                Assert.InRange(GearCatalog.Quality(item), 0f, 1f);
            }
        }
    }

    [Fact]
    public void APerfectCopy_IsTheOldFixedNumbers()
    {
        var hauberk = GearCatalog.Perfect(GearCatalog.Find("ironhide_hauberk")!);
        var bonuses = new ItemBonuses();
        GearCatalog.Apply(hauberk, bonuses);
        Assert.Equal(80f, bonuses.Armour);
        Assert.Equal(0.85f, bonuses.DamageTaken, 4);
        Assert.Equal(30f, bonuses.MaxHealth);
        Assert.Equal(1f, GearCatalog.Quality(hauberk));
    }

    [Fact]
    public void ALowRoll_GivesLess_AndQualityReadsWhereItSits()
    {
        var piece = GearCatalog.Find("tempest_fang")!;
        var worst = new GearItem { Id = "a", Piece = piece.Id, Rolls = new List<float> { piece.Stats[0].Min } };
        var bonuses = new ItemBonuses();
        GearCatalog.Apply(worst, bonuses);
        Assert.Equal(1.06f, bonuses.AttackSpeedMultiplier, 4);
        Assert.Equal(0f, GearCatalog.Quality(worst));
        Assert.Equal("x1.06 attack speed", piece.Stats[0].Line(worst.Rolls[0]));
        Assert.Equal("1.06-1.20", piece.Stats[0].Range);
    }

    [Fact]
    public void ALowerIsBetterStat_RollsTheRightWay()
    {
        var chalice = GearCatalog.Find("blood_chalice")!.Stats[0];
        Assert.Equal(1f, chalice.Quality(40f));
        Assert.Equal(0f, chalice.Quality(80f));
        Assert.Equal("40-80", chalice.Range);
    }

    [Fact]
    public void AFind_IsAnyPiece_Rolled_WornIfItsSlotIsEmpty_AndEveryCopyKept()
    {
        var profile = new Profile();
        var random = new Random(4);
        var first = GearCatalog.Grant(profile, random);
        var slot = GearCatalog.PieceOf(first).Slot;
        Assert.Equal(first, GearCatalog.WornIn(profile, slot));

        var finds = Enumerable.Range(0, 400).Select(_ => GearCatalog.Grant(profile, random)).ToList();
        Assert.Equal(GearCatalog.All.Count, finds.Select(f => f.Piece).Distinct().Count());   // any piece can come
        Assert.Equal(401, profile.Gear.Items.Count);                                          // every copy kept, each its own
        Assert.Equal(401, profile.Gear.Items.Select(i => i.Id).Distinct().Count());
        Assert.Equal(GearCatalog.All.Count, GearCatalog.PiecesFound(profile));
        Assert.Equal(first, GearCatalog.WornIn(profile, slot));                               // a later find doesn't swap what is worn
        Assert.True(finds.Select(GearCatalog.Quality).Distinct().Count() > 20);               // and they roll differently
    }

    [Fact]
    public void OnlyOwnedGear_CanBeWorn_AndItCanBeTakenOff()
    {
        var profile = new Profile();
        var fang = GearCatalog.Perfect(GearCatalog.Find("tempest_fang")!);
        Assert.False(GearCatalog.Wear(profile, fang));
        profile.Gear.Worn[GearSlot.Weapon.ToString()] = fang.Id;   // a hand-edited save
        Assert.Null(GearCatalog.WornIn(profile, GearSlot.Weapon));

        profile.Gear.Items.Add(fang);
        Assert.True(GearCatalog.Wear(profile, fang));
        Assert.Equal(fang, GearCatalog.WornIn(profile, GearSlot.Weapon));
        GearCatalog.TakeOff(profile, GearSlot.Weapon);
        Assert.Empty(GearCatalog.Worn(profile));
    }

    [Fact]
    public void GearIsWornInSevenPlaces_TwoOfThemRings()
    {
        Assert.Equal(7, GearCatalog.Places.Count);
        Assert.Equal(GearCatalog.Places.Count, GearCatalog.Places.Select(p => p.Key).Distinct().Count());
        Assert.Equal(2, GearCatalog.PlacesFor(GearSlot.Ring).Count);
        Assert.All(Enum.GetValues<GearSlot>().Where(s => s != GearSlot.Ring), slot => Assert.Single(GearCatalog.PlacesFor(slot)));
        Assert.All(new[] { GearSlot.BodyArmour, GearSlot.Weapon, GearSlot.Trinket }, slot =>
            Assert.Equal(slot.ToString(), GearCatalog.PlacesFor(slot)[0].Key));   // an old save's worn gear stays worn
    }

    [Fact]
    public void TwoRings_CanBeWorn_AndARingMovesBetweenThem()
    {
        var profile = new Profile();
        var left = GearCatalog.Places.First(p => p.Key == "Ring");
        var right = GearCatalog.Places.First(p => p.Key == "Ring2");
        var signet = GearCatalog.Perfect(GearCatalog.Find("signet_of_the_deep")!);
        var band = GearCatalog.Perfect(GearCatalog.Find("band_of_the_hollow_court")!);
        var another = GearCatalog.Perfect(GearCatalog.Find("signet_of_the_deep")!);
        profile.Gear.Items.AddRange(new[] { signet, band, another });

        Assert.True(GearCatalog.Wear(profile, signet));
        Assert.True(GearCatalog.Wear(profile, band));   // the empty one
        Assert.Equal(signet, GearCatalog.WornAt(profile, left));
        Assert.Equal(band, GearCatalog.WornAt(profile, right));

        Assert.True(GearCatalog.Wear(profile, band, left));   // moved across: the right is free again
        Assert.Equal(band, GearCatalog.WornAt(profile, left));
        Assert.Null(GearCatalog.WornAt(profile, right));

        Assert.True(GearCatalog.Wear(profile, another, right));   // two of the same ring is fine
        Assert.Equal(2, GearCatalog.Worn(profile).Count());
        Assert.False(GearCatalog.Wear(profile, band, GearCatalog.Places.First(p => p.Slot == GearSlot.Amulet)));   // a ring isn't an amulet

        var bonuses = new ItemBonuses();
        foreach (var worn in GearCatalog.Worn(profile))
        {
            GearCatalog.Apply(worn, bonuses);
        }

        Assert.Equal(0.08f, bonuses.Damage, 4);       // the band's
        Assert.Equal(0.15f, bonuses.CritChance, 4);   // one signet's: the first came off when the band took its hand
    }

    [Fact]
    public void ARingFind_GoesOnTheSecondHand_IfTheFirstIsTaken()
    {
        var profile = new Profile();
        var random = new Random(5);
        GearItem? first = null, second = null;
        while (second is null)
        {
            var found = GearCatalog.Grant(profile, random);
            if (GearCatalog.PieceOf(found).Slot == GearSlot.Ring)
            {
                (first, second) = first is null ? (found, null) : (first, found);
            }
        }

        Assert.Equal(first, GearCatalog.WornAt(profile, GearCatalog.PlacesFor(GearSlot.Ring)[0]));
        Assert.Equal(second, GearCatalog.WornAt(profile, GearCatalog.PlacesFor(GearSlot.Ring)[1]));
    }

    [Fact]
    public void TheNewPieces_DoWhatTheySay()
    {
        var bonuses = new ItemBonuses();
        GearTesting.Best("soulbound_amulet")(bonuses);
        GearTesting.Best("wardstone_ring")(bonuses);
        GearTesting.Best("studded_war_belt")(bonuses);
        Assert.Equal(1, bonuses.LastStands);
        Assert.Equal(20f, bonuses.Ward);
        Assert.Equal(0.25f, bonuses.WardShield, 4);   // a Mage's Frost Shield grows instead
        Assert.Equal(0.75f, bonuses.RangedDamageTaken, 4);
        Assert.Equal(50f, bonuses.Armour);
    }

    [Fact]
    public void FullyKitted_AsksForEveryPlace()
    {
        var profile = new Profile();
        var bounty = Bounties.All.Single(b => b.Id == "fully_kitted");
        var tree = new TreeProgress(Ranger.SharpshooterTree.Tree, new TreeSave());
        var run = new RunRecord(0, 0, 0, 0f, false, 1);
        foreach (var place in GearCatalog.Places.Skip(1))
        {
            var item = GearCatalog.Perfect(GearCatalog.All.First(p => p.Slot == place.Slot));
            profile.Gear.Items.Add(item);
            GearCatalog.Wear(profile, item, place);
        }

        Assert.False(bounty.Met(run, profile, tree));
        var weapon = GearCatalog.Perfect(GearCatalog.All.First(p => p.Slot == GearSlot.Weapon));
        profile.Gear.Items.Add(weapon);
        GearCatalog.Wear(profile, weapon);
        Assert.True(bounty.Met(run, profile, tree));
    }

    [Fact]
    public void SpareCopies_SellForMore_TheBetterTheyRolled_ButNotWhileWorn()
    {
        var piece = GearCatalog.Find("kingsbane")!;
        var best = GearCatalog.Perfect(piece);
        var worst = new GearItem { Id = "w", Piece = piece.Id, Rolls = new List<float> { piece.Stats[0].Min } };
        Assert.Equal(GearCatalog.SellCeiling, GearCatalog.SellPrice(best));
        Assert.Equal(GearCatalog.SellFloor, GearCatalog.SellPrice(worst));

        var profile = new Profile();
        profile.Gear.Items.Add(best);
        profile.Gear.Items.Add(worst);
        GearCatalog.Wear(profile, best);
        Assert.False(GearCatalog.Sell(profile, best));
        Assert.Equal(0, profile.Silver);

        Assert.True(GearCatalog.Sell(profile, worst));
        Assert.Equal(GearCatalog.SellFloor, profile.Silver);
        Assert.Equal(new[] { best }, profile.Gear.Items);
        Assert.False(GearCatalog.Sell(profile, worst));   // gone
    }

    [Fact]
    public void AnOldSave_BecomesPerfectCopies_AndKeepsWhatWasWorn()
    {
        var profile = new Profile();
        profile.Gear.Owned.AddRange(new[] { "tempest_fang", "heart_of_the_mountain", "gone_from_the_game" });
        profile.Gear.Copies["tempest_fang"] = 2;
        profile.Gear.Worn[GearSlot.Weapon.ToString()] = "tempest_fang";

        GearCatalog.Migrate(profile);

        Assert.Empty(profile.Gear.Owned);
        Assert.Empty(profile.Gear.Copies);
        Assert.Equal(3, profile.Gear.Items.Count);   // two fangs and the heart
        Assert.All(profile.Gear.Items, item => Assert.Equal(1f, GearCatalog.Quality(item)));
        Assert.Equal("tempest_fang", GearCatalog.WornIn(profile, GearSlot.Weapon)!.Piece);
        Assert.Null(GearCatalog.WornIn(profile, GearSlot.Trinket));

        GearCatalog.Migrate(profile);   // twice does nothing more
        Assert.Equal(3, profile.Gear.Items.Count);
    }

    [Fact]
    public void RolledGear_LastsInTheSave()
    {
        string path = Path.Combine(Path.GetTempPath(), $"am-gear-{Guid.NewGuid():N}.json");
        try
        {
            var profile = new Profile();
            var item = GearCatalog.Grant(profile, new Random(2));
            ProfileStore.Save(profile, path);
            var loaded = ProfileStore.Load(path);
            var back = Assert.Single(loaded.Gear.Items);
            Assert.Equal(item.Id, back.Id);
            Assert.Equal(item.Rolls, back.Rolls);
            Assert.Equal(item.Id, GearCatalog.WornIn(loaded, GearCatalog.PieceOf(item).Slot)!.Id);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void WornGear_AddsToTheRunsBonuses_WithoutBeingAnItem()
    {
        var inventory = new ItemInventory();
        inventory.Add(ItemCatalog.All.First(i => i.Id == "swiftwind_sigil"));
        inventory.AddBonus(GearTesting.Best("tempest_fang"));
        Assert.Equal(1.15f * 1.2f, inventory.Bonuses.AttackSpeedMultiplier, 4);
        Assert.Single(inventory.Items);

        inventory.Clear();
        Assert.Equal(1f, inventory.Bonuses.AttackSpeedMultiplier);
    }

    [Fact]
    public void TheVestmentsShield_TakesTheHitFirst_AndComesBackAfterItsCooldown()
    {
        var bonuses = new ItemBonuses();
        GearTesting.Best("great_mages_vestments")(bonuses);
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
        GearTesting.Best("emberbrand")(bonuses);
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
        Assert.Equal(profile.Gear.Items.Select(i => i.Id), back.Gear.Items.Select(i => i.Id));
        Assert.Equal(GearCatalog.Worn(profile).Select(i => i.Id), GearCatalog.Worn(back).Select(i => i.Id));
    }

    [Fact]
    public void AnOldSave_StartsAtTheTopOfTheDelve_WithNoGear()
    {
        File.WriteAllText(_path, "{\"Version\":1,\"Silver\":50}");
        var profile = ProfileStore.Load(_path);
        Assert.Equal(1, profile.Delve.Deepest);
        Assert.Empty(profile.Gear.Items);
        Assert.Equal(50, profile.Silver);
    }
}

/// <summary>Gear at its best rolls, for tests of what a piece does.</summary>
internal static class GearTesting
{
    public static Action<ItemBonuses> Best(string id) => bonuses => GearCatalog.Apply(GearCatalog.Perfect(GearCatalog.Find(id)!), bonuses);
}
