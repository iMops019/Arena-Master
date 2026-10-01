using ArenaMaster.Game.Combat;
using ArenaMaster.Game.Items;
using ArenaMaster.Game.Paladin;
using ArenaMaster.Game.Progression;
using Silk.NET.Maths;

namespace ArenaMaster.Game.Tests;

internal static class CrusadeTesting
{
    public static Dictionary<string, int> Ranks(params (string Id, int Ranks)[] ranks) => ranks.ToDictionary(r => r.Id, r => r.Ranks);

    /// <summary>Stats with the Crusade active and these ranks in it, and no crits (for exact numbers).</summary>
    public static PaladinStats With(params (string Id, int Ranks)[] ranks) =>
        new() { Crusade = CrusadeBonuses.From(Ranks(ranks)), CrusadeActive = true, Items = new ItemBonuses { CritChance = -1f } };

    /// <summary>A Paladin on the Crusade with these ranks, started on a run (no crits), and the health it started with.</summary>
    public static PaladinClass Paladin(out PlayerHealth health, params (string Id, int Ranks)[] ranks)
    {
        var paladin = new PaladinClass(new Random(1));
        paladin.ChooseTree(CrusadeTree.TreeId);
        paladin.UseTree(Ranks(ranks));
        health = new PlayerHealth(100f);
        paladin.BeginRun(new ItemBonuses { CritChance = -1f }, health);
        return paladin;
    }

    /// <summary>A frame of the fight with the nova held (a stun), so only the Crusade's own clocks run.</summary>
    public static void Quiet(PaladinClass paladin, float seconds, bool moving, EnemyField field, PlayerHealth health, float step = 0.1f)
    {
        for (float t = 0f; t < seconds - 1e-4f; t += step)
        {
            paladin.Fight(step, Vector3D<float>.Zero, Vector3D<float>.Zero, standingStill: !moving, stunned: true, field, health, new DamageNumbers());
        }
    }

    /// <summary>A brute (an elite) that won't die of the first hit, at (<paramref name="x"/>, <paramref name="z"/>).</summary>
    public static Enemy Brute(EnemyField field, float x, float z = 0f)
    {
        var brute = field.Spawn(new Vector3D<float>(x, 0f, z), EnemyKind.Brute);
        brute.Health = 10_000f;
        return brute;
    }
}

public class CrusadeTreeTests
{
    private static TreeNode Node(string id) => CrusadeTree.Tree.Node(id);

    [Fact]
    public void TheTree_IsWellFormed()
    {
        var tree = CrusadeTree.Tree;
        var nodes = tree.Nodes;
        Assert.Equal("crusade", tree.Id);
        Assert.Equal("Crusade", tree.Name);
        Assert.InRange(nodes.Count, 34, 40);
        Assert.Equal(nodes.Count, nodes.Select(n => n.Id).Distinct().Count());
        Assert.All(nodes, n => Assert.All(n.Parents, p => Assert.True(tree.Node(p).Tier < n.Tier, $"{n.Id} has a parent at or above its tier")));
        Assert.All(nodes.Where(n => n.Tier > 1), n => Assert.NotEmpty(n.Parents));
        Assert.All(nodes.Where(n => !n.Major), n => Assert.Contains("{", n.Text));
        Assert.All(nodes.Where(n => !n.Major), n => Assert.InRange(n.MaxRanks, 2, 5));
        Assert.All(nodes.Where(n => n.Major), n => Assert.Equal(1, n.MaxRanks));
        Assert.All(nodes, n => Assert.DoesNotContain("{", n.Describe(1)));   // every number named is one the node has
        Assert.All(nodes, n => Assert.Contains(n.Lane, tree.Lanes.Select(l => l.Name)));
        Assert.All(nodes, n => Assert.InRange(n.X, 60f, 920f));
        Assert.All(nodes, n => Assert.True(n.Playable, $"{n.Name} is still marked coming soon"));
        Assert.All(nodes, n => Assert.DoesNotContain("looses", n.Text));
        Assert.Equal(new[] { "Zeal", "Judgement", "Sacrifice" }, tree.Lanes.Select(l => l.Name));
        Assert.Equal(new[] { 1, 3, 6, 10, 15, 21, 28 }, tree.TierLevels);
        Assert.Equal(12, nodes.Count(n => n.Major));

        var starts = nodes.Where(n => n.Tier == 1).ToList();
        Assert.Equal(new[] { "Forward March", "Righteous Strike" }, starts.Select(n => n.Name));
        Assert.All(starts, n => Assert.Equal(5, n.MaxRanks));
        Assert.All(starts, n => Assert.False(n.Major));

        foreach (var tier in nodes.GroupBy(n => n.Tier))
        {
            var xs = tier.Select(n => n.X).OrderBy(x => x).ToList();
            for (int i = 1; i < xs.Count; i++)
            {
                Assert.True(xs[i] - xs[i - 1] >= 100f, $"tier {tier.Key}'s nodes at {xs[i - 1]} and {xs[i]} overlap");
            }
        }
    }

    [Fact]
    public void EachLane_EndsInACapstone()
    {
        foreach (var (lane, _) in CrusadeTree.Tree.Lanes)
        {
            Assert.Single(CrusadeTree.Tree.Nodes, n => n.Tier == 7 && n.Lane == lane && n.Major);
        }

        Assert.Equal(CrusadeTree.EndlessCrusade, CrusadeTree.Tree.Nodes.Single(n => n.Tier == 7 && n.Lane == "Zeal").Id);
        Assert.Equal(CrusadeTree.FinalJudgement, CrusadeTree.Tree.Nodes.Single(n => n.Tier == 7 && n.Lane == "Judgement").Id);
        Assert.Equal(CrusadeTree.AvengingWings, CrusadeTree.Tree.Nodes.Single(n => n.Tier == 7 && n.Lane == "Sacrifice").Id);
    }

    [Fact]
    public void Zeal_TheHammer_AndBloodOath_ComeEarly()
    {
        foreach (var id in new[] { CrusadeTree.Zeal, CrusadeTree.HammerOfJudgement, CrusadeTree.BloodOath })
        {
            Assert.True(Node(id).Major);
            Assert.InRange(Node(id).Tier, 2, 3);
        }
    }

    [Fact]
    public void WhatTheMajorsBuildOn_IsAlwaysTakenFirst()
    {
        // Condemn and Hallowed Impact need hammers: every way to them runs through Hammer of Judgement. (Final Judgement and Endless Crusade bring their own.)
        static bool Needs(string id, string ancestor) =>
            id == ancestor || (CrusadeTree.Tree.Node(id).Parents.Count > 0 && CrusadeTree.Tree.Node(id).Parents.All(p => Needs(p, ancestor)));

        Assert.True(Needs(CrusadeTree.Condemn, CrusadeTree.HammerOfJudgement));
        Assert.True(Needs(CrusadeTree.HallowedImpact, CrusadeTree.HammerOfJudgement));
        Assert.True(Needs("kindling", CrusadeTree.Zeal));
        Assert.True(Needs("unwavering", CrusadeTree.Zeal));
        Assert.True(Needs("heavy", CrusadeTree.HammerOfJudgement));
    }

    [Fact]
    public void EveryNodesRanks_ChangeTheBonuses()
    {
        var options = new System.Text.Json.JsonSerializerOptions { IncludeFields = true };
        var none = System.Text.Json.JsonSerializer.Serialize(CrusadeBonuses.From(new Dictionary<string, int>()), options);
        foreach (var node in CrusadeTree.Tree.Nodes)
        {
            var one = System.Text.Json.JsonSerializer.Serialize(CrusadeBonuses.From(new Dictionary<string, int> { [node.Id] = 1 }), options);
            Assert.True(one != none, $"{node.Name} does nothing");
        }
    }

    [Fact]
    public void Ranks_AddUpIntoTheBonuses()
    {
        var b = CrusadeBonuses.From(CrusadeTesting.Ranks(("march", 5), ("road", 5), ("fervent", 2), ("righteous", 3), ("verdict", 2), ("scarred", 4), ("scars", 2)));

        Assert.Equal(0.10f + 0.15f + 0.06f, b.MoveSpeed, 4);
        Assert.Equal(0.40f + 0.24f + 0.20f, b.NovaDamage, 4);
        Assert.Equal(0.08f, b.NovaFrequency, 4);
        Assert.Equal(0.24f, b.CritChance, 4);
        Assert.Equal(40f + 16f, b.MaxHealth, 3);
        Assert.Equal(0.97f * 0.97f, b.DamageTaken, 4);
        Assert.False(b.Zeal);
    }

    [Fact]
    public void Descriptions_ShowTheNumbersAtTheirRank()
    {
        Assert.Equal("+6% move speed and +24% Holy Nova damage.", Node("march").Describe(3));
        Assert.Equal("Every kill heals you 1.5 health.", Node("lifeblood").Describe(3));
    }
}

public class CrusadeActiveTreeTests
{
    [Fact]
    public void ThePaladin_HasBothTrees_DefianceFirst()
    {
        var paladin = new PaladinClass(new Random(1));

        Assert.Equal(new[] { DefianceTree.TreeId, CrusadeTree.TreeId }, paladin.Trees.Select(t => t.Id));
        Assert.Equal(DefianceTree.TreeId, paladin.Tree.Id);
        paladin.ChooseTree(CrusadeTree.TreeId);
        Assert.Equal(CrusadeTree.TreeId, paladin.Tree.Id);
        paladin.ChooseTree("nonsense");
        Assert.Equal(DefianceTree.TreeId, paladin.Tree.Id);
    }

    [Fact]
    public void UseTree_FillsOnlyTheActiveTree()
    {
        var ranks = CrusadeTesting.Ranks(("march", 5), (CrusadeTree.BloodOath, 1), ("shieldwall", 5), (DefianceTree.EchoingNova, 1));
        var paladin = new PaladinClass(new Random(1));

        paladin.ChooseTree(CrusadeTree.TreeId);
        paladin.UseTree(ranks);
        Assert.True(paladin.Stats.CrusadeActive);
        Assert.Equal(0.10f, paladin.Stats.Crusade.MoveSpeed, 4);
        Assert.True(paladin.Stats.Crusade.BloodOath);
        Assert.Equal(0f, paladin.Stats.Tree.BlockChance);
        Assert.False(paladin.Stats.Tree.EchoingNova);

        paladin.ChooseTree(DefianceTree.TreeId);
        paladin.UseTree(ranks);
        Assert.False(paladin.Stats.CrusadeActive);
        Assert.Equal(0f, paladin.Stats.Crusade.MoveSpeed);
        Assert.False(paladin.Stats.Crusade.BloodOath);
        Assert.Equal(0.10f, paladin.Stats.Tree.BlockChance, 4);
        Assert.True(paladin.Stats.Tree.EchoingNova);
    }

    [Fact]
    public void TheCrusadesMinors_ReachTheNumbers()
    {
        var stats = CrusadeTesting.With(("march", 5), ("righteous", 5), ("sentence", 2), ("charge", 2), ("scarred", 3), ("grit", 2));

        Assert.Equal(PaladinStats.BaseMoveSpeed * 1.1f, stats.MoveSpeed, 3);
        Assert.Equal(PaladinStats.BaseNovaDamage * 1.8f, stats.NovaDamage, 3);
        Assert.Equal(PaladinStats.BaseRushCooldown / 1.3f, stats.RushCooldown, 3);
        Assert.Equal(PaladinStats.BaseMaxHealth + 30f, stats.MaxHealth, 3);
        Assert.Equal(0.8f, stats.Regeneration(false), 3);
        Assert.Equal(1.24f, stats.EliteMultiplier, 3);
    }

    [Fact]
    public void BloodPrice_AddsNovaDamage_OnlyBelowHalfHealth()
    {
        var stats = CrusadeTesting.With(("price", 2));

        Assert.Equal(PaladinStats.BaseNovaDamage, stats.NovaDamage, 3);
        stats.HealthShare = 0.4f;
        Assert.Equal(PaladinStats.BaseNovaDamage * 1.3f, stats.NovaDamage, 3);
    }
}

public class ZealTests
{
    [Fact]
    public void Zeal_BuildsWhileMoving_UpToTwenty_AndDrainsStandingStill()
    {
        var field = PaladinTesting.QuietField();
        var paladin = CrusadeTesting.Paladin(out var health, (CrusadeTree.Zeal, 1));

        CrusadeTesting.Quiet(paladin, 5f, moving: true, field, health);
        Assert.Equal(5f, paladin.Crusade.Zeal, 2);
        Assert.Equal(PaladinStats.BaseNovaDamage * 1.1f, paladin.Stats.NovaDamage, 2);
        Assert.Equal("ZEAL 5 / 20", paladin.Meter!.Value.Label);
        Assert.Equal(0.25f, paladin.Meter!.Value.Fill, 2);

        CrusadeTesting.Quiet(paladin, 30f, moving: true, field, health);
        Assert.Equal(PaladinStats.MaxZeal, paladin.Crusade.Zeal, 3);
        Assert.Equal("ZEAL 20 / 20", paladin.Meter!.Value.Label);
        Assert.Equal(PaladinStats.BaseNovaDamage * 1.4f, paladin.Stats.NovaDamage, 2);

        CrusadeTesting.Quiet(paladin, 2f, moving: false, field, health);
        Assert.Equal(PaladinStats.MaxZeal - 2f * PaladinStats.ZealDrain, paladin.Crusade.Zeal, 2);
    }

    [Fact]
    public void WithoutZeal_ThereIsNoMeter_AndNothingBuilds()
    {
        var field = PaladinTesting.QuietField();
        var paladin = CrusadeTesting.Paladin(out var health, ("march", 1));

        CrusadeTesting.Quiet(paladin, 3f, moving: true, field, health);

        Assert.Null(paladin.Meter);
        Assert.Equal(0f, paladin.Crusade.Zeal);
    }

    [Fact]
    public void KindledZeal_AndUnwavering_BuildFaster_AndDrainSlower()
    {
        var stats = CrusadeTesting.With((CrusadeTree.Zeal, 1), ("kindling", 3), ("unwavering", 2));

        Assert.Equal(1.6f, stats.ZealGain, 3);
        Assert.Equal(PaladinStats.ZealDrain * 0.6f, stats.ZealLoss, 3);
    }

    [Fact]
    public void CrusadersRush_HitsEveryEnemyItPassesOnce_AndLeavesACircleWhereItEnds()
    {
        var field = PaladinTesting.QuietField();
        var first = PaladinTesting.Sturdy(field, 1.2f);
        var second = PaladinTesting.Sturdy(field, 2.8f, 0.6f);
        var aside = PaladinTesting.Sturdy(field, 2f, 4f);
        var paladin = CrusadeTesting.Paladin(out var health, (CrusadeTree.CrusadersRush, 1));
        var numbers = new DamageNumbers();

        var at = Vector3D<float>.Zero;
        for (int i = 0; i < 12; i++)   // 0.2 s at 17 m/s: 3.4 m along +X
        {
            at += new Vector3D<float>(PaladinStats.BaseRushSpeed * PaladinTesting.Step, 0f, 0f);
            paladin.Fight(PaladinTesting.Step, at, at, standingStill: false, stunned: true, field, health, numbers, rushing: true);
        }

        Assert.Empty(paladin.Light.Circles);
        paladin.Fight(PaladinTesting.Step, at, at, standingStill: true, stunned: true, field, health, numbers, rushing: false);

        Assert.Equal(10_000f - PaladinStats.BaseNovaDamage, first.Health, 3);
        Assert.Equal(10_000f - PaladinStats.BaseNovaDamage, second.Health, 3);
        Assert.Equal(10_000f, aside.Health);
        var circle = Assert.Single(paladin.Light.Circles);
        Assert.Equal(at, circle.Centre);
    }

    [Fact]
    public void WithoutCrusadersRush_TheRushHitsNothing()
    {
        var field = PaladinTesting.QuietField();
        var first = PaladinTesting.Sturdy(field, 1.2f);
        var paladin = CrusadeTesting.Paladin(out var health, ("march", 1));

        for (int i = 0; i < 12; i++)
        {
            var at = new Vector3D<float>(0.3f * i, 0f, 0f);
            paladin.Fight(PaladinTesting.Step, at, at, standingStill: false, stunned: true, field, health, new DamageNumbers(), rushing: i < 11);
        }

        Assert.Equal(10_000f, first.Health);
        Assert.Empty(paladin.Light.Circles);
    }

    [Fact]
    public void BlessedTrail_LeavesASmallerCircle_EveryThreeMetresWalked()
    {
        var field = PaladinTesting.QuietField();
        var paladin = CrusadeTesting.Paladin(out var health, (CrusadeTree.BlessedTrail, 1));

        for (int i = 0; i <= 70; i++)   // 7 m along +X, 0.1 m a frame
        {
            var at = new Vector3D<float>(0.1f * i, 0f, 0f);
            paladin.Fight(0.02f, at, at, standingStill: false, stunned: true, field, health, new DamageNumbers());
        }

        Assert.Equal(2, paladin.Light.Circles.Count);
        Assert.All(paladin.Light.Circles, c => Assert.Equal(PaladinStats.BaseCircleRadius * PaladinStats.TrailCircle, c.BaseRadius, 3));
        Assert.Equal(3f, paladin.Light.Circles[0].Centre.X, 1);
        Assert.Equal(6f, paladin.Light.Circles[1].Centre.X, 1);

        var far = new Vector3D<float>(100f, 0f, 0f);   // a teleport is no walk
        paladin.Fight(0.02f, far, far, standingStill: false, stunned: true, field, health, new DamageNumbers());
        Assert.Equal(2, paladin.Light.Circles.Count);
    }

    [Fact]
    public void EndlessCrusade_BurstsAgain_OnlyAtFullZeal()
    {
        var field = PaladinTesting.QuietField();
        var target = PaladinTesting.Sturdy(field, 2f);
        var stats = CrusadeTesting.With((CrusadeTree.EndlessCrusade, 1));
        var light = new HolyLight(new Random(1));

        stats.Zeal = PaladinStats.MaxZeal - 1f;
        light.CastNova(Vector3D<float>.Zero, stats, field, new List<HolyHit>());
        var notYet = PaladinTesting.Shine(light, stats, field, 0.5f, canCast: false).Where(h => h.Source == HolySource.Nova);
        Assert.Empty(notYet);

        stats.Zeal = PaladinStats.MaxZeal;
        var hits = new List<HolyHit>();
        light.CastNova(Vector3D<float>.Zero, stats, field, hits);
        hits.AddRange(PaladinTesting.Shine(light, stats, field, PaladinStats.EndlessDelay + 0.05f, canCast: false).Where(h => h.Source == HolySource.Nova));

        Assert.Equal(2, hits.Count);
        Assert.Equal(PaladinStats.BaseNovaDamage, hits[0].Damage, 3);   // Endless alone builds Zeal but adds no damage for it
        Assert.Equal(PaladinStats.BaseNovaDamage * PaladinStats.EndlessDamage, hits[1].Damage, 3);
    }

    [Fact]
    public void EndlessCrusade_BuildsZeal_WithoutTheZealNode()
    {
        var field = PaladinTesting.QuietField();
        var paladin = CrusadeTesting.Paladin(out var health, (CrusadeTree.EndlessCrusade, 1));

        CrusadeTesting.Quiet(paladin, 25f, moving: true, field, health);

        Assert.NotNull(paladin.Meter);
        Assert.True(paladin.Stats.ZealFull);
    }
}

public class JudgementTests
{
    /// <summary>Casts <paramref name="count"/> novas at the origin, each followed by the Crusade's frame that answers it.</summary>
    private static List<HolyHit> Novas(int count, HolyLight light, CrusadeLight crusade, PaladinStats stats, EnemyField field, PlayerHealth health)
    {
        var hits = new List<HolyHit>();
        for (int i = 0; i < count; i++)
        {
            light.CastNova(Vector3D<float>.Zero, stats, field, hits);
            crusade.Update(PaladinTesting.Step, Vector3D<float>.Zero, false, false, 1, light, stats, field, health, hits);
        }

        return hits;
    }

    private static List<HolyHit> Fall(HolyLight light, CrusadeLight crusade, PaladinStats stats, EnemyField field, PlayerHealth health, float seconds = CrusadeLight.FallTime + 0.05f)
    {
        var hits = new List<HolyHit>();
        for (float t = 0f; t < seconds; t += PaladinTesting.Step)
        {
            crusade.Update(PaladinTesting.Step, Vector3D<float>.Zero, false, false, 0, light, stats, field, health, hits);
        }

        return hits;
    }

    [Fact]
    public void EveryFifthNova_DropsAHammer_OnTheToughest_ForFourTimesTheNovasDamage()
    {
        var field = PaladinTesting.QuietField();
        var fodder = PaladinTesting.Sturdy(field, 8f);
        var brute = CrusadeTesting.Brute(field, 12f);
        var beside = PaladinTesting.Sturdy(field, 12f, 1.5f);
        var away = PaladinTesting.Sturdy(field, 12f, 4f);
        var stats = CrusadeTesting.With((CrusadeTree.HammerOfJudgement, 1));
        var light = new HolyLight(new Random(1));
        var crusade = new CrusadeLight();
        var health = new PlayerHealth(100f);

        Novas(4, light, crusade, stats, field, health);
        Assert.Empty(crusade.Hammers);
        Novas(1, light, crusade, stats, field, health);
        var hammer = Assert.Single(crusade.Hammers);
        Assert.Equal(brute, hammer.Target);

        var hits = Fall(light, crusade, stats, field, health).Where(h => h.Source == HolySource.Hammer).ToList();

        Assert.Empty(crusade.Hammers);
        Assert.Single(crusade.Impacts);
        Assert.Equal(PaladinStats.BaseNovaDamage * PaladinStats.HammerShare, Assert.Single(hits, h => h.Enemy == brute).Damage, 3);
        Assert.Contains(hits, h => h.Enemy == beside);
        Assert.DoesNotContain(hits, h => h.Enemy == away || h.Enemy == fodder);
    }

    [Fact]
    public void TheHammer_PicksABossFirst_ThenAnElite_ThenTheMostHealth_WithinFifteenMetres()
    {
        var stats = CrusadeTesting.With((CrusadeTree.HammerOfJudgement, 1));
        var field = PaladinTesting.QuietField();
        var weak = PaladinTesting.Sturdy(field, 5f);
        var strong = PaladinTesting.Sturdy(field, -5f);
        strong.Health = 50_000f;
        PaladinTesting.Sturdy(field, 20f).Health = 90_000f;   // out of reach

        var crusade = new CrusadeLight();
        crusade.CallHammers(Vector3D<float>.Zero, stats, field);
        Assert.Equal(strong, Assert.Single(crusade.Hammers).Target);

        var brute = CrusadeTesting.Brute(field, 0f, 6f);
        crusade.Reset();
        crusade.CallHammers(Vector3D<float>.Zero, stats, field);
        Assert.Equal(brute, Assert.Single(crusade.Hammers).Target);

        var king = field.Spawn(new Vector3D<float>(0f, 0f, -10f), EnemyKind.HollowKing);
        crusade.Reset();
        crusade.CallHammers(Vector3D<float>.Zero, stats, field);
        Assert.Equal(king, Assert.Single(crusade.Hammers).Target);

        crusade.Reset();
        crusade.CallHammers(new Vector3D<float>(100f, 0f, 100f), stats, field);
        Assert.Empty(crusade.Hammers);
    }

    [Fact]
    public void AFallingHammer_FollowsItsTarget()
    {
        var field = PaladinTesting.QuietField();
        var brute = CrusadeTesting.Brute(field, 10f);
        var stats = CrusadeTesting.With((CrusadeTree.HammerOfJudgement, 1));
        var crusade = new CrusadeLight();
        crusade.CallHammers(Vector3D<float>.Zero, stats, field);

        brute.Position = new Vector3D<float>(10f, 0f, 2f);
        var hits = Fall(new HolyLight(new Random(1)), crusade, stats, field, new PlayerHealth(100f));

        Assert.Contains(hits, h => h.Enemy == brute);
        Assert.Equal(brute.Position, Assert.Single(crusade.Impacts).Centre);
    }

    [Fact]
    public void FinalJudgement_DropsHammers_OnEveryEliteAndBossWithinTwentyFiveMetres()
    {
        var field = PaladinTesting.QuietField();
        PaladinTesting.Sturdy(field, 5f);
        var near = CrusadeTesting.Brute(field, 10f);
        var far = CrusadeTesting.Brute(field, -22f);
        CrusadeTesting.Brute(field, 0f, 30f);   // out of reach
        var stats = CrusadeTesting.With((CrusadeTree.FinalJudgement, 1));   // it drops hammers without Hammer of Judgement
        var light = new HolyLight(new Random(1));
        var crusade = new CrusadeLight();

        Novas(5, light, crusade, stats, field, new PlayerHealth(100f));

        Assert.Equal(2, crusade.Hammers.Count);
        Assert.Contains(crusade.Hammers, h => h.Target == near);
        Assert.Contains(crusade.Hammers, h => h.Target == far);
    }

    [Fact]
    public void Condemn_MakesWhatAHammerStrikes_TakeMore_ForFiveSeconds()
    {
        var field = PaladinTesting.QuietField();
        var brute = CrusadeTesting.Brute(field, 10f);
        var stats = CrusadeTesting.With((CrusadeTree.HammerOfJudgement, 1), (CrusadeTree.Condemn, 1));
        var light = new HolyLight(new Random(1));
        var crusade = new CrusadeLight();
        var health = new PlayerHealth(100f);
        crusade.CallHammers(Vector3D<float>.Zero, stats, field);

        var landed = Fall(light, crusade, stats, field, health);
        Assert.Equal(PaladinStats.BaseNovaDamage * PaladinStats.HammerShare, Assert.Single(landed).Damage, 3);   // the hammer itself isn't raised
        Assert.True(light.IsCondemned(brute));

        var hits = new List<HolyHit>();
        light.Hurt(brute, 100f, false, HolySource.Nova, stats, field, hits);
        Assert.Equal(100f * (1f + PaladinStats.CondemnBonus), hits[0].Damage, 3);

        PaladinTesting.Shine(light, stats, field, PaladinStats.CondemnDuration, canCast: false);
        Assert.False(light.IsCondemned(brute));
    }

    [Fact]
    public void HallowedImpact_LeavesAHolyCircle_WhereTheHammerLands()
    {
        var field = PaladinTesting.QuietField();
        var brute = CrusadeTesting.Brute(field, 10f);
        var stats = CrusadeTesting.With((CrusadeTree.HammerOfJudgement, 1), (CrusadeTree.HallowedImpact, 1));
        var light = new HolyLight(new Random(1));
        var crusade = new CrusadeLight();
        crusade.CallHammers(Vector3D<float>.Zero, stats, field);

        Fall(light, crusade, stats, field, new PlayerHealth(100f));

        Assert.Equal(brute.Position, Assert.Single(light.Circles).Centre);
    }

    [Fact]
    public void HammerNodes_MakeItHitHarder_AndWider()
    {
        var stats = CrusadeTesting.With((CrusadeTree.HammerOfJudgement, 1), ("heavy", 2), ("broad", 2));
        stats.Increase(PaladinUpgrade.Hammerfall);

        Assert.Equal(PaladinStats.BaseNovaDamage * PaladinStats.HammerShare * 1.8f, stats.HammerDamage, 3);
        Assert.Equal(PaladinStats.BaseHammerRadius * 1.3f, stats.HammerRadius, 3);
    }
}

public class SacrificeTests
{
    [Fact]
    public void BloodOath_CostsHealthEachNova_HitsHarder_AndKillsHeal()
    {
        var field = PaladinTesting.QuietField();
        var target = PaladinTesting.Sturdy(field, 2f);
        var paladin = CrusadeTesting.Paladin(out var health, (CrusadeTree.BloodOath, 1));
        float max = health.Max;
        health.TakeDamage(20f);   // room for the heals
        const float Frame = HolyLight.FirstNova + 0.01f;

        paladin.Fight(Frame, Vector3D<float>.Zero, Vector3D<float>.Zero, true, false, field, health, new DamageNumbers());

        Assert.Equal(1, paladin.Light.Novas);
        float paid = max - 20f - max * PaladinStats.BloodOathCost + PaladinStats.BaseCircleHealing * Frame;   // and the circle it left heals for the frame
        Assert.Equal(paid, health.Current, 3);
        Assert.Equal(20.0, health.HealthLost, 3);   // the price is paid, not lost to a blow
        float circleBurn = PaladinStats.BaseCircleDps * HolyLight.CircleTick;   // the circle's first burn, in the same long frame
        Assert.Equal(10_000f - PaladinStats.BaseNovaDamage * (1f + PaladinStats.BloodOathDamage) - circleBurn, target.Health, 3);

        paladin.OnKill(target, 0f);
        Assert.Equal(paid + PaladinStats.BloodOathHeal, health.Current, 3);
    }

    [Fact]
    public void BloodOath_NeverTakesYouBelowOne()
    {
        var field = PaladinTesting.QuietField();
        var stats = CrusadeTesting.With((CrusadeTree.BloodOath, 1));
        var light = new HolyLight(new Random(1));
        var crusade = new CrusadeLight();
        var health = new PlayerHealth(130f);
        health.TakeDamage(130f - 1.2f);

        for (int i = 0; i < 2; i++)
        {
            light.CastNova(Vector3D<float>.Zero, stats, field, new List<HolyHit>());
            crusade.Update(PaladinTesting.Step, Vector3D<float>.Zero, false, false, 1, light, stats, field, health, new List<HolyHit>());
            Assert.Equal(1f, health.Current, 4);
        }

        Assert.False(health.IsDead);
    }

    [Fact]
    public void Spending_Health_StopsAtOne_AndIsNotALoss()
    {
        var health = new PlayerHealth(50f);

        health.Spend(20f);
        Assert.Equal(30f, health.Current);
        health.Spend(100f);
        Assert.Equal(1f, health.Current);
        Assert.Equal(0.0, health.HealthLost);
        Assert.True(health.CanBeHurt);   // no grace after it
    }

    [Fact]
    public void Martyrdom_HitsHarder_TheLessHealthYouHave()
    {
        var stats = CrusadeTesting.With((CrusadeTree.Martyrdom, 1));

        stats.HealthShare = 1f;
        Assert.Equal(PaladinStats.BaseNovaDamage, stats.NovaDamage, 3);
        stats.HealthShare = 0.55f;
        Assert.Equal(PaladinStats.BaseNovaDamage * 1.3f, stats.NovaDamage, 3);
        stats.HealthShare = 0.1f;
        Assert.Equal(PaladinStats.BaseNovaDamage * 1.6f, stats.NovaDamage, 3);
        stats.HealthShare = 0.02f;
        Assert.Equal(PaladinStats.BaseNovaDamage * 1.6f, stats.NovaDamage, 3);
    }

    [Fact]
    public void Martyrdom_ReadsTheHealth_InAFight()
    {
        var field = PaladinTesting.QuietField();
        var paladin = CrusadeTesting.Paladin(out var health, (CrusadeTree.Martyrdom, 1));
        health.TakeDamage(health.Max * 0.55f);

        CrusadeTesting.Quiet(paladin, 0.1f, moving: false, field, health);

        Assert.Equal(PaladinStats.BaseNovaDamage * (1f + PaladinStats.MartyrdomMax * 0.55f / 0.9f), paladin.Stats.NovaDamage, 2);
    }

    [Fact]
    public void Penance_AddsWhatTheBlowsTookOff_ToTheNextNova_Twice()
    {
        var field = PaladinTesting.QuietField();
        var target = PaladinTesting.Sturdy(field, 2f);
        var paladin = CrusadeTesting.Paladin(out var health, (CrusadeTree.Penance, 1));

        health.TakeDamage(10f);
        paladin.AnswerStrikes(Array.Empty<Strike>(), Vector3D<float>.Zero, field, health, new DamageNumbers());
        Assert.Equal(10f * PaladinStats.PenanceShare, paladin.Light.Penance, 3);

        var hits = new List<HolyHit>();
        paladin.Light.CastNova(Vector3D<float>.Zero, paladin.Stats, field, hits);
        Assert.Equal(PaladinStats.BaseNovaDamage + 20f, Assert.Single(hits).Damage, 3);
        Assert.Equal(0f, paladin.Light.Penance);

        paladin.AnswerStrikes(Array.Empty<Strike>(), Vector3D<float>.Zero, field, health, new DamageNumbers());
        Assert.Equal(0f, paladin.Light.Penance);   // nothing new lost
    }

    [Fact]
    public void WithoutPenance_LostHealthAddsNothing()
    {
        var field = PaladinTesting.QuietField();
        var paladin = CrusadeTesting.Paladin(out var health, ("march", 1));

        health.TakeDamage(10f);
        paladin.AnswerStrikes(Array.Empty<Strike>(), Vector3D<float>.Zero, field, health, new DamageNumbers());

        Assert.Equal(0f, paladin.Light.Penance);
    }

    [Fact]
    public void AvengingWings_TakeFlightBelowThirtyPercent_ForSixSeconds_OnceAMinute()
    {
        var field = PaladinTesting.QuietField();
        var paladin = CrusadeTesting.Paladin(out var health, (CrusadeTree.AvengingWings, 1));

        health.TakeDamage(health.Max * 0.5f);
        paladin.AnswerStrikes(Array.Empty<Strike>(), Vector3D<float>.Zero, field, health, new DamageNumbers());
        Assert.False(paladin.Crusade.Winged);

        health.Update(1f);
        health.TakeDamage(health.Max * 0.25f);   // down to 25%
        paladin.AnswerStrikes(Array.Empty<Strike>(), Vector3D<float>.Zero, field, health, new DamageNumbers());
        Assert.True(paladin.Crusade.Winged);
        Assert.Equal("AVENGING WINGS", paladin.Status);
        Assert.Equal(0f, paladin.DamageTaken);
        Assert.Equal(PaladinStats.BaseNovaInterval / PaladinStats.WingsNovaSpeed, paladin.Stats.NovaInterval, 3);

        float before = health.Current;
        health.Update(1f);
        health.DamageTaken = paladin.DamageTaken;   // as the content sets it each frame
        health.TakeDamage(1000f);
        Assert.Equal(before, health.Current);   // nothing gets through
        health.DamageTaken = 1f;

        CrusadeTesting.Quiet(paladin, PaladinStats.WingsDuration + 0.1f, moving: false, field, health);
        Assert.False(paladin.Crusade.Winged);
        Assert.Equal(1f, paladin.DamageTaken);
        Assert.Equal(PaladinStats.BaseNovaInterval, paladin.Stats.NovaInterval, 3);

        CrusadeTesting.Quiet(paladin, 40f, moving: false, field, health);   // still low, but not yet a minute
        Assert.False(paladin.Crusade.Winged);
        CrusadeTesting.Quiet(paladin, PaladinStats.WingsCooldown - PaladinStats.WingsDuration - 40f, moving: false, field, health);
        Assert.True(paladin.Crusade.Winged);
    }

    [Fact]
    public void KillHeals_AddUp_FromBloodOath_Lifeblood_AndBloodTithe()
    {
        var stats = CrusadeTesting.With((CrusadeTree.BloodOath, 1), ("lifeblood", 2));
        stats.Increase(PaladinUpgrade.BloodTithe);

        Assert.Equal(1f + 1f + 0.5f, stats.KillHeal, 3);
        Assert.Equal(0f, new PaladinStats().KillHeal);
    }
}

public class CrusadeCardTests
{
    private static HashSet<PaladinUpgrade?> Offered(PaladinStats stats) =>
        Enumerable.Range(0, 300).SelectMany(seed => PaladinUpgrades.Roll(stats, new Random(seed))).Select(c => c.Upgrade).ToHashSet();

    private static readonly PaladinUpgrade[] CrusadeCards =
    {
        PaladinUpgrade.Fervour, PaladinUpgrade.Hammerfall, PaladinUpgrade.BloodTithe, PaladinUpgrade.Onslaught, PaladinUpgrade.BatteringCharge,
    };

    [Fact]
    public void WithDefiance_NoCrusadeCardComesUp_AndDefiancesOwnDo()
    {
        var offered = Offered(PaladinTesting.With((DefianceTree.CrownOfThorns, 1)));

        Assert.All(CrusadeCards, c => Assert.DoesNotContain(c, offered));
        Assert.Contains(PaladinUpgrade.ShieldSlam, offered);
        Assert.Contains(PaladinUpgrade.Steadfast, offered);
        Assert.Contains(PaladinUpgrade.BarbedPlating, offered);
    }

    [Fact]
    public void WithTheCrusade_DefiancesOwnCardsStayAway_AndTheSharedOnesStay()
    {
        var offered = Offered(CrusadeTesting.With(("march", 1)));

        Assert.DoesNotContain(PaladinUpgrade.ShieldSlam, offered);
        Assert.DoesNotContain(PaladinUpgrade.Steadfast, offered);
        Assert.DoesNotContain(PaladinUpgrade.BarbedPlating, offered);
        Assert.DoesNotContain(PaladinUpgrade.BrambleMail, offered);
        Assert.Contains(PaladinUpgrade.Onslaught, offered);
        foreach (var shared in new[] { PaladinUpgrade.HolyWrath, PaladinUpgrade.QuickenedPrayer, PaladinUpgrade.Radiance, PaladinUpgrade.Consecration,
                     PaladinUpgrade.HeavyPlate, PaladinUpgrade.PilgrimsStride, PaladinUpgrade.Gleaner, PaladinUpgrade.ZealotsEye })
        {
            Assert.Contains(shared, offered);
        }

        // The cards that build on a major wait for it.
        Assert.DoesNotContain(PaladinUpgrade.Fervour, offered);
        Assert.DoesNotContain(PaladinUpgrade.Hammerfall, offered);
        Assert.DoesNotContain(PaladinUpgrade.BloodTithe, offered);
        Assert.DoesNotContain(PaladinUpgrade.BatteringCharge, offered);
    }

    [Fact]
    public void TheCrusadesMajorCards_ComeUp_OnceTheirMajorIsTaken()
    {
        var offered = Offered(CrusadeTesting.With((CrusadeTree.Zeal, 1), (CrusadeTree.HammerOfJudgement, 1), (CrusadeTree.BloodOath, 1),
            (CrusadeTree.CrusadersRush, 1)));

        Assert.All(CrusadeCards, c => Assert.Contains(c, offered));
        Assert.Contains(PaladinUpgrade.Hammerfall, Offered(CrusadeTesting.With((CrusadeTree.FinalJudgement, 1))));
        Assert.Contains(PaladinUpgrade.Fervour, Offered(CrusadeTesting.With((CrusadeTree.EndlessCrusade, 1))));
    }

    [Fact]
    public void TheCrusadesCards_DoWhatTheySay()
    {
        var stats = CrusadeTesting.With((CrusadeTree.Zeal, 1), (CrusadeTree.CrusadersRush, 1));
        float gain = stats.ZealGain, loss = stats.ZealLoss, cooldown = stats.RushCooldown, rush = stats.RushDamage;

        stats.Increase(PaladinUpgrade.Fervour);
        stats.Increase(PaladinUpgrade.Onslaught);
        stats.Increase(PaladinUpgrade.BatteringCharge);

        Assert.Equal(gain * 1.25f, stats.ZealGain, 3);
        Assert.Equal(loss * 0.75f, stats.ZealLoss, 3);
        Assert.Equal(cooldown / 1.15f, stats.RushCooldown, 3);
        Assert.Equal(rush * 1.3f, stats.RushDamage, 3);
        Assert.All(CrusadeCards, c => Assert.DoesNotContain("looses", PaladinUpgrades.Info(c).Description));
    }
}
