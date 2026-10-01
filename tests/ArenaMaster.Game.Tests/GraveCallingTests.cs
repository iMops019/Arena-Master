using System.Numerics;
using ArenaMaster.Game.Combat;
using ArenaMaster.Game.Items;
using ArenaMaster.Game.Priest;
using ArenaMaster.Game.Progression;
using CEngine.Core;
using Silk.NET.Maths;

namespace ArenaMaster.Game.Tests;

internal static class GraveTesting
{
    public const float Step = PriestTesting.Step;

    /// <summary>Grave Calling active with these ranks, no crits.</summary>
    public static PriestStats With(params (string Id, int Ranks)[] ranks) =>
        new()
        {
            GraveCalling = true, Grave = GraveCallingBonuses.From(ranks.ToDictionary(r => r.Id, r => r.Ranks)), Items = new ItemBonuses { CritChance = -1f },
        };

    /// <summary>A Priest on a run with Grave Calling active and these ranks, no crits.</summary>
    public static PriestClass Priest(params (string Id, int Ranks)[] ranks)
    {
        var priest = new PriestClass(new Random(3));
        priest.ChooseTree(GraveCallingTree.TreeId);
        priest.UseTree(ranks.ToDictionary(r => r.Id, r => r.Ranks));
        priest.BeginRun(new ItemBonuses { CritChance = -1f }, new PlayerHealth(100f));
        return priest;
    }

    /// <summary>Runs the raised dead for <paramref name="seconds"/> with the Priest standing at <paramref name="feet"/>.</summary>
    public static List<PriestHit> Run(RaisedDead dead, PriestStats stats, EnemyField field, float seconds, Vector3D<float> feet = default)
    {
        var skulls = new PlagueSkulls(new Random(1));
        var hits = new List<PriestHit>();
        for (float t = 0f; t < seconds - 1e-4f; t += Step)
        {
            dead.Update(Step, feet, stats, field, PriestTesting.FlatGround, skulls, hits);
        }

        return hits;
    }

    /// <summary>A servant raised at once at (<paramref name="x"/>, <paramref name="z"/>).</summary>
    public static Servant Raise(RaisedDead dead, PriestStats stats, float x, float z, float healthScale = 1f)
    {
        dead.Raise(new Vector3D<float>(x, 0f, z), stats, healthScale, PriestTesting.FlatGround);
        return dead.Servants[^1];
    }

    /// <summary>Kills at the Priest's feet, leaving souls there, and one frame to gather them.</summary>
    public static void Gather(GatheredSouls souls, PriestStats stats, int count, EnemyField field, PlayerHealth? health = null, List<PriestHit>? hits = null)
    {
        for (int i = 0; i < count; i++)
        {
            souls.OnKill(Vector3D<float>.Zero, stats);
        }

        souls.Update(Step, Vector3D<float>.Zero, stats, health ?? new PlayerHealth(100f), field, new PlagueSkulls(new Random(1)), hits ?? new List<PriestHit>());
    }
}

public class GraveCallingTreeTests
{
    [Fact]
    public void TheTree_IsWellFormed()
    {
        var tree = GraveCallingTree.Tree;
        var nodes = tree.Nodes;
        Assert.Equal("gravecalling", tree.Id);
        Assert.Equal("Grave Calling", tree.Name);
        Assert.InRange(nodes.Count, 34, 40);
        Assert.Equal(nodes.Count, nodes.Select(n => n.Id).Distinct().Count());
        Assert.All(nodes, n => Assert.All(n.Parents, p => Assert.True(tree.Node(p).Tier < n.Tier, $"{n.Id} has a parent at or above its tier")));
        Assert.All(nodes.Where(n => n.Tier > 1), n => Assert.NotEmpty(n.Parents));
        Assert.All(nodes.Where(n => !n.Major), n => Assert.Contains("{", n.Text));
        Assert.All(nodes.Where(n => !n.Major), n => Assert.InRange(n.MaxRanks, 2, 5));
        Assert.All(nodes.Where(n => n.Major), n => Assert.Equal(1, n.MaxRanks));
        Assert.All(nodes, n => Assert.DoesNotContain("{", n.Describe(1)));
        Assert.All(nodes, n => Assert.DoesNotContain("loose", n.Text, StringComparison.OrdinalIgnoreCase));
        Assert.All(nodes, n => Assert.Contains(n.Lane, tree.Lanes.Select(l => l.Name)));
        Assert.All(nodes, n => Assert.InRange(n.X, 60f, 920f));
        Assert.All(nodes, n => Assert.True(n.Playable, $"{n.Name} is still marked coming soon"));
        Assert.Equal(new[] { 1, 3, 6, 10, 15, 21, 28 }, tree.TierLevels);
        Assert.Equal(new[] { "Servants", "Soul", "Bone" }, tree.Lanes.Select(l => l.Name));
        Assert.Equal(12, nodes.Count(n => n.Major));   // Unholy's fourteen, give or take two

        var starts = nodes.Where(n => n.Tier == 1).ToList();
        Assert.Equal(new[] { "Grave Pact", "Bone Craft" }, starts.Select(n => n.Name));
        Assert.All(starts, n => Assert.False(n.Major));
        Assert.All(starts, n => Assert.Equal(5, n.MaxRanks));

        // Nodes in a tier don't sit on one another.
        foreach (var tier in nodes.GroupBy(n => n.Tier))
        {
            var xs = tier.Select(n => n.X).Order().ToList();
            for (int i = 1; i < xs.Count; i++)
            {
                Assert.True(xs[i] - xs[i - 1] >= 100f, $"tier {tier.Key}: nodes at {xs[i - 1]} and {xs[i]} overlap");
            }
        }
    }

    [Fact]
    public void EachLane_EndsInACapstone()
    {
        var capstones = GraveCallingTree.Tree.Nodes.Where(n => n.Tier == 7).ToList();
        Assert.All(capstones, n => Assert.True(n.Major));
        Assert.Equal(GraveCallingTree.Tree.Lanes.Select(l => l.Name).OrderBy(n => n), capstones.Select(n => n.Lane).OrderBy(n => n));
        Assert.Equal(new[] { "Army of the Dead", "Lich Form", "Bone Colossus" }, capstones.Select(n => n.Name));
        Assert.Contains("Needs Raise Dead", GraveCallingTree.Tree.Node(GraveCallingTree.BoneColossus).Text);
    }

    [Fact]
    public void TheKeyMajors_ComeEarly_InTheirLanes()
    {
        var tree = GraveCallingTree.Tree;
        foreach (var (id, lane) in new[] { (GraveCallingTree.RaiseDead, "Servants"), (GraveCallingTree.SoulSiphon, "Soul"), (GraveCallingTree.BoneSpear, "Bone") })
        {
            var node = tree.Node(id);
            Assert.True(node.Major);
            Assert.InRange(node.Tier, 2, 3);
            Assert.Equal(lane, node.Lane);
        }

        Assert.Equal("Servants", tree.Node(GraveCallingTree.CorpseBurst).Lane);
        Assert.Equal("Bone", tree.Node(GraveCallingTree.BoneCage).Lane);
    }

    [Fact]
    public void Ranks_AddUpIntoTheBonuses()
    {
        var bonuses = GraveCallingBonuses.From(new Dictionary<string, int>
        {
            ["gravepact"] = 3, ["bonecraft"] = 2, ["boneplate"] = 2, ["soulcall"] = 3, [GraveCallingTree.RaiseDead] = 1,
        });
        Assert.Equal(0.24f + 0.16f, bonuses.SkullDamage, 4);
        Assert.Equal(0.3f, bonuses.ServantDamage, 4);
        Assert.Equal(16f + 24f, bonuses.MaxHealth, 3);
        Assert.Equal(0.97f * 0.97f, bonuses.DamageTaken, 4);
        Assert.Equal(1.5f, bonuses.SoulReach, 3);
        Assert.Equal(3f, bonuses.SoulDuration, 3);
        Assert.True(bonuses.RaiseDead);
        Assert.False(bonuses.SoulSiphon);
    }

    [Fact]
    public void ThePriest_HasBothTrees_GraveCallingSecond()
    {
        var priest = new PriestClass(new Random(1));
        Assert.Equal(new[] { UnholyTree.TreeId, GraveCallingTree.TreeId }, priest.Trees.Select(t => t.Id));
        Assert.Equal(UnholyTree.TreeId, priest.Tree.Id);
        priest.ChooseTree(GraveCallingTree.TreeId);
        Assert.Equal(GraveCallingTree.TreeId, priest.Tree.Id);
        priest.ChooseTree("nothing");
        Assert.Equal(UnholyTree.TreeId, priest.Tree.Id);
        Assert.Contains("Grave Calling", priest.Summary);
        Assert.Contains("Unholy", priest.Summary);
    }

    [Fact]
    public void UseTree_TakesTheActiveTreesBonuses_AndLeavesTheOthersEmpty()
    {
        var priest = new PriestClass(new Random(1));
        var ranks = new Dictionary<string, int> { ["unholymight"] = 5, ["gravepact"] = 5, [UnholyTree.VirulentStrain] = 1, [GraveCallingTree.RaiseDead] = 1 };

        priest.ChooseTree(GraveCallingTree.TreeId);
        priest.UseTree(ranks);
        Assert.True(priest.Stats.GraveCalling);
        Assert.True(priest.Stats.Grave.RaiseDead);
        Assert.Equal(0.4f, priest.Stats.Grave.SkullDamage, 4);
        Assert.False(priest.Stats.Tree.VirulentStrain);
        Assert.Equal(0f, priest.Stats.Tree.SkullDamage);

        priest.ChooseTree(UnholyTree.TreeId);
        priest.UseTree(ranks);
        Assert.False(priest.Stats.GraveCalling);
        Assert.True(priest.Stats.Tree.VirulentStrain);
        Assert.False(priest.Stats.Grave.RaiseDead);
        Assert.Equal(0f, priest.Stats.Grave.SkullDamage);
    }
}

/// <summary>The skulls follow the active tree: Plague with Unholy, a harder hit and no Plague with Grave Calling.</summary>
public class GraveCallingAttackTests
{
    [Fact]
    public void WithGraveCalling_TheSkullsHitHarder_AndLeaveNoPlague()
    {
        var unholy = PriestTesting.With();
        var grave = GraveTesting.With();
        Assert.Equal(PriestStats.BaseSkullDamage, unholy.SkullDamage, 3);
        Assert.Equal(PriestStats.BaseSkullDamage * PriestStats.GraveSkullMultiplier, grave.SkullDamage, 3);

        var field = PriestTesting.QuietField();
        var enemy = PriestTesting.Sturdy(field, 0f, 3f);
        var skulls = new PlagueSkulls(new Random(1));
        var hits = PriestTesting.Fire(skulls, grave, field);
        hits.AddRange(PriestTesting.Run(skulls, grave, field, 3f));

        var hit = Assert.Single(hits);
        Assert.Equal(PriestSource.Skull, hit.Source);
        Assert.Equal(grave.SkullDamage, hit.Damage, 3);
        Assert.Equal(0, skulls.StacksOn(enemy));
        Assert.Empty(skulls.Infections);
    }

    [Fact]
    public void WithUnholy_TheSkullsStillPlague_AsBefore()
    {
        var field = PriestTesting.QuietField();
        var enemy = PriestTesting.Sturdy(field, 0f, 3f);
        var priest = PriestTesting.Priest();
        Assert.False(priest.Stats.GraveCalling);
        priest.Skulls.Cast(priest.Stats);
        PriestTesting.Run(priest.Skulls, priest.Stats, field, 0.5f, canCast: true);
        Assert.True(priest.Skulls.StacksOn(enemy) > 0);
    }

    [Fact]
    public void AFreshGraveCallingPriest_KillsAboutAsFastAsAFreshUnholyOne()
    {
        // The Priest standing among a crowd that keeps coming, at enemy health from a run's first minute to its twentieth. Grave Calling's harder hits
        // (GraveSkullMultiplier) stand in for the Plague it no longer spreads.
        float unholy = 0f, grave = 0f;
        for (float scale = 1f; scale <= 3.45f; scale += 0.3f)
        {
            unholy += Simulate(UnholyTree.TreeId, scale);
            grave += Simulate(GraveCallingTree.TreeId, scale);
        }

        Assert.InRange(grave / unholy, 0.85f, 1.15f);
    }

    private static int Simulate(string treeId, float healthScale)
    {
        var priest = new PriestClass(new Random(2));
        priest.ChooseTree(treeId);
        priest.UseTree(new Dictionary<string, int>());
        var health = new PlayerHealth(1e9f);
        priest.BeginRun(new ItemBonuses(), health);
        var field = new EnemyField(new Random(2)) { TargetCount = 40, SpawnInterval = 0.1f, Scaling = new EnemyScaling(healthScale, 1f, 1f) };
        var condition = new PlayerCondition();
        var numbers = new DamageNumbers();
        const float step = 1f / 30f;
        for (float t = 0f; t < 45f; t += step)
        {
            priest.Fight(step, PriestTesting.Hand, Vector3D<float>.Zero, PriestTesting.North, false, field, PriestTesting.FlatGround, health, numbers);
            field.Update(step, new PlayerTarget(Vector3D<float>.Zero, true, health, condition), PriestTesting.FlatGround);
            foreach (var killed in field.TakeNewlyKilled())
            {
                priest.OnKill(killed, t);
            }
        }

        return field.Kills;
    }
}

public class RaisedDeadTests
{
    [Fact]
    public void RaiseDead_RaisesEvery8thKill_UpToFour()
    {
        var field = PriestTesting.QuietField();
        var dead = new RaisedDead();
        var stats = GraveTesting.With((GraveCallingTree.RaiseDead, 1));
        for (int i = 0; i < 7; i++)
        {
            dead.OnKill(new Vector3D<float>(2f, 0f, 2f), stats);
        }

        GraveTesting.Run(dead, stats, field, GraveTesting.Step);
        Assert.Empty(dead.Servants);

        dead.OnKill(new Vector3D<float>(2f, 0f, 2f), stats);
        GraveTesting.Run(dead, stats, field, GraveTesting.Step);
        var servant = Assert.Single(dead.Servants);
        Assert.Equal(PriestStats.ServantBaseHealth, servant.MaxHealth, 3);
        Assert.True(Vector3D.Distance(servant.Position, new Vector3D<float>(2f, 0f, 2f)) < 0.3f, "it rose where the 8th fell");

        for (int i = 0; i < 8 * 6; i++)
        {
            dead.OnKill(Vector3D<float>.Zero, stats);
        }

        GraveTesting.Run(dead, stats, field, GraveTesting.Step);
        Assert.Equal(PriestStats.BaseServants, dead.Standing);
    }

    [Fact]
    public void WithoutRaiseDead_NothingRises()
    {
        var dead = new RaisedDead();
        var stats = GraveTesting.With();
        for (int i = 0; i < 20; i++)
        {
            dead.OnKill(Vector3D<float>.Zero, stats);
        }

        GraveTesting.Run(dead, stats, PriestTesting.QuietField(), GraveTesting.Step);
        Assert.Empty(dead.Servants);
    }

    [Fact]
    public void AServantsHealth_GrowsAsTheEnemiesDo()
    {
        var field = PriestTesting.QuietField();
        field.Scaling = new EnemyScaling(2f, 1f, 1f);
        var dead = new RaisedDead();
        var stats = GraveTesting.With((GraveCallingTree.RaiseDead, 1));
        for (int i = 0; i < 8; i++)
        {
            dead.OnKill(Vector3D<float>.Zero, stats);
        }

        GraveTesting.Run(dead, stats, field, GraveTesting.Step);
        Assert.Equal(2f * PriestStats.ServantBaseHealth, Assert.Single(dead.Servants).MaxHealth, 3);
    }

    [Fact]
    public void AServant_WalksToTheNearestEnemyNearYou_AndClawsItFor12_Every0Point8Seconds()
    {
        var field = PriestTesting.QuietField();
        var near = PriestTesting.Sturdy(field, 6f, 0f);
        var dead = new RaisedDead();
        var stats = GraveTesting.With((GraveCallingTree.RaiseDead, 1));
        var servant = GraveTesting.Raise(dead, stats, 0f, 0f);

        var hits = GraveTesting.Run(dead, stats, field, 4f);

        Assert.True(servant.Position.X > 4.5f, "it walked to the enemy");
        var claws = hits.Where(h => h.Source == PriestSource.Servant).ToList();
        Assert.All(claws, h => Assert.Equal(near, h.Enemy));
        Assert.All(claws, h => Assert.Equal(PriestStats.ServantBaseDamage, h.Damage, 3));
        Assert.InRange(claws.Count, 3, 5);   // about 3.3 s at it, a claw each 0.8 s
    }

    [Fact]
    public void AServant_NeverGoesForAnEnemyMoreThan12MetresFromThePriest()
    {
        var field = PriestTesting.QuietField();
        PriestTesting.Sturdy(field, 15f, 0f);
        var dead = new RaisedDead();
        var stats = GraveTesting.With((GraveCallingTree.RaiseDead, 1));
        var servant = GraveTesting.Raise(dead, stats, 0f, 0f);

        var hits = GraveTesting.Run(dead, stats, field, 4f);

        Assert.Empty(hits);
        Assert.Null(servant.Target);
        Geometry.FlatDirection(Vector3D<float>.Zero, servant.Position, out float fromPriest);
        Assert.True(fromPriest < 4f, "it stays by the Priest");
    }

    [Fact]
    public void AServantLeftFarBehind_ComesBackToThePriestsSide()
    {
        var field = PriestTesting.QuietField();
        var dead = new RaisedDead();
        var stats = GraveTesting.With((GraveCallingTree.RaiseDead, 1));
        var servant = GraveTesting.Raise(dead, stats, 0f, 0f);
        var feet = new Vector3D<float>(0f, 0f, 30f);

        GraveTesting.Run(dead, stats, field, GraveTesting.Step, feet);

        Geometry.FlatDirection(feet, servant.Position, out float fromPriest);
        Assert.True(fromPriest < 4f, $"it is {fromPriest:0.0} m from the Priest");
    }

    [Fact]
    public void EnemiesTouchingAServant_WearItDown_5ASecondEach()
    {
        var field = PriestTesting.QuietField();
        PriestTesting.Sturdy(field, 0.3f, 0f);
        PriestTesting.Sturdy(field, -0.3f, 0f);
        var dead = new RaisedDead();
        var stats = GraveTesting.With((GraveCallingTree.RaiseDead, 1));
        var servant = GraveTesting.Raise(dead, stats, 0f, 0f);

        GraveTesting.Run(dead, stats, field, 1f);

        Assert.Equal(PriestStats.ServantBaseHealth - 2f * PriestStats.ServantWear, servant.Health, 0.5f);
    }

    [Fact]
    public void AServant_CrumblesAfter30Seconds_AndIsGoneOnceItHasFallenApart()
    {
        var field = PriestTesting.QuietField();
        var dead = new RaisedDead();
        var stats = GraveTesting.With((GraveCallingTree.RaiseDead, 1));
        var servant = GraveTesting.Raise(dead, stats, 0f, 0f);

        GraveTesting.Run(dead, stats, field, 29.9f);
        Assert.True(servant.IsAlive);
        GraveTesting.Run(dead, stats, field, 0.2f);
        Assert.False(servant.IsAlive);
        GraveTesting.Run(dead, stats, field, RaisedDead.DeathSeconds);
        Assert.Empty(dead.Servants);
    }

    [Fact]
    public void CorpseBurst_AFallenServantBursts_For200PercentOfASkull_Within3Metres()
    {
        var field = PriestTesting.QuietField();
        var close = PriestTesting.Sturdy(field, 2f, 20f);
        var far = PriestTesting.Sturdy(field, 5f, 20f);
        var dead = new RaisedDead();
        var stats = GraveTesting.With((GraveCallingTree.RaiseDead, 1), (GraveCallingTree.CorpseBurst, 1));
        GraveTesting.Raise(dead, stats, 0f, 20f).Health = 0f;

        var hits = GraveTesting.Run(dead, stats, field, 0.1f, new Vector3D<float>(0f, 0f, 20f));

        var burst = hits.Where(h => h.Source == PriestSource.Burst).ToList();
        var hit = Assert.Single(burst, h => h.Enemy == close);
        Assert.Equal(2f * stats.SkullDamage, hit.Damage, 3);
        Assert.DoesNotContain(burst, h => h.Enemy == far);
        Assert.Single(dead.Bursts);
    }

    [Fact]
    public void WithoutCorpseBurst_AFallenServantJustFalls()
    {
        var field = PriestTesting.QuietField();
        PriestTesting.Sturdy(field, 1.5f, 0f);
        var dead = new RaisedDead();
        var stats = GraveTesting.With((GraveCallingTree.RaiseDead, 1));
        GraveTesting.Raise(dead, stats, 0f, 0f).Health = 0f;

        var hits = GraveTesting.Run(dead, stats, field, 0.1f);

        Assert.DoesNotContain(hits, h => h.Source == PriestSource.Burst);
    }

    [Fact]
    public void DeathsCommand_ServantsClawFaster_AndLastLonger()
    {
        var plain = GraveTesting.With((GraveCallingTree.RaiseDead, 1));
        var command = GraveTesting.With((GraveCallingTree.RaiseDead, 1), (GraveCallingTree.DeathsCommand, 1));
        Assert.Equal(plain.ServantClawInterval / 1.4f, command.ServantClawInterval, 4);
        Assert.Equal(plain.ServantLife + 10f, command.ServantLife, 3);
    }

    [Fact]
    public void ArmyOfTheDead_KeepsEight_AndAKillByAServantRisesAtOnce()
    {
        var field = PriestTesting.QuietField();
        var weak = field.Spawn(new Vector3D<float>(1f, 0f, 0f));
        weak.Health = 1f;
        var dead = new RaisedDead();
        var stats = GraveTesting.With((GraveCallingTree.RaiseDead, 1), (GraveCallingTree.ArmyOfTheDead, 1));
        Assert.Equal(8, stats.MaxServants);
        GraveTesting.Raise(dead, stats, 0f, 0f);

        GraveTesting.Run(dead, stats, field, 1f);

        Assert.False(weak.IsAlive);
        Assert.Equal(2, dead.Standing);

        for (int i = 0; i < 10; i++)
        {
            GraveTesting.Raise(dead, stats, 0f, 0f);
        }

        Assert.Equal(8, dead.Standing);
    }

    [Fact]
    public void BoneColossus_AtTheCap_TheNextRaiseMergesThemAll()
    {
        var field = PriestTesting.QuietField();
        var dead = new RaisedDead();
        var stats = GraveTesting.With((GraveCallingTree.RaiseDead, 1), (GraveCallingTree.BoneColossus, 1));
        for (int i = 0; i < 4; i++)
        {
            GraveTesting.Raise(dead, stats, i, 0f);
        }

        Assert.Null(dead.Colossus);
        GraveTesting.Raise(dead, stats, 10f, 0f);

        var colossus = Assert.Single(dead.Servants);
        Assert.True(colossus.IsColossus);
        Assert.Equal(0, dead.Standing);
        Assert.Equal(5f * PriestStats.ServantBaseHealth, colossus.MaxHealth, 3);
        Assert.Equal(PriestStats.ColossusLife, colossus.Life, 3);
        Assert.Equal(3f * RaisedDead.ServantRadius, colossus.Radius, 3);
        Assert.Equal(1.5f, colossus.Position.X, 2);   // where they stood, together
    }

    [Fact]
    public void BoneColossus_HitsEverythingWithin2Point5Metres_ForFourTimesAClaw()
    {
        var field = PriestTesting.QuietField();
        var dead = new RaisedDead();
        var stats = GraveTesting.With((GraveCallingTree.RaiseDead, 1), (GraveCallingTree.BoneColossus, 1));
        for (int i = 0; i < 5; i++)
        {
            GraveTesting.Raise(dead, stats, 0f, 0f);
        }

        var colossus = dead.Colossus!;
        colossus.Position = Vector3D<float>.Zero;
        var a = PriestTesting.Sturdy(field, 1.6f, 0f);
        var b = PriestTesting.Sturdy(field, -1.4f, 0.8f);
        var c = PriestTesting.Sturdy(field, 0f, -1.8f);
        var far = PriestTesting.Sturdy(field, 6f, 6f);

        var hits = GraveTesting.Run(dead, stats, field, 0.5f);

        foreach (var enemy in new[] { a, b, c })
        {
            Assert.Contains(hits, h => h.Enemy == enemy && h.Source == PriestSource.Servant && MathF.Abs(h.Damage - 4f * PriestStats.ServantBaseDamage) < 0.01f);
        }

        Assert.DoesNotContain(hits, h => h.Enemy == far);
    }

    [Fact]
    public void WithoutBoneColossus_ARaiseAtTheCap_DoesNothing()
    {
        var dead = new RaisedDead();
        var stats = GraveTesting.With((GraveCallingTree.RaiseDead, 1));
        for (int i = 0; i < 6; i++)
        {
            GraveTesting.Raise(dead, stats, 0f, 0f);
        }

        Assert.Equal(4, dead.Servants.Count);
        Assert.Null(dead.Colossus);
    }
}

public class GatheredSoulsTests
{
    [Fact]
    public void SoulSiphon_EveryKillLeavesASoul_AtMost30_EachFadingAfter8Seconds()
    {
        var field = PriestTesting.QuietField();
        var souls = new GatheredSouls();
        var stats = GraveTesting.With((GraveCallingTree.SoulSiphon, 1));
        for (int i = 0; i < 35; i++)
        {
            souls.OnKill(new Vector3D<float>(10f + i * 0.1f, 0f, 0f), stats);
        }

        Assert.Equal(PriestStats.MaxSouls, souls.Souls.Count);
        Assert.Equal(10.5f, souls.Souls[0].Position.X, 3);   // the oldest went

        var hits = new List<PriestHit>();
        for (float t = 0f; t < 8.1f; t += GraveTesting.Step)
        {
            souls.Update(GraveTesting.Step, Vector3D<float>.Zero, stats, new PlayerHealth(100f), field, new PlagueSkulls(new Random(1)), hits);
        }

        Assert.Empty(souls.Souls);
    }

    [Fact]
    public void WithoutSoulSiphon_KillsLeaveNoSouls_AndThereIsNoMeter()
    {
        var souls = new GatheredSouls();
        var stats = GraveTesting.With();
        souls.OnKill(Vector3D<float>.Zero, stats);
        Assert.Empty(souls.Souls);
        Assert.Null(souls.Meter(stats));
        Assert.Equal((1f, 1), souls.Empower(stats));
    }

    [Fact]
    public void WalkingOverASoul_Heals1Percent_AndAddsACharge_UpTo10()
    {
        var field = PriestTesting.QuietField();
        var souls = new GatheredSouls();
        var stats = GraveTesting.With((GraveCallingTree.SoulSiphon, 1));
        var health = new PlayerHealth(200f);
        health.TakeDamage(100f);
        souls.OnKill(new Vector3D<float>(5f, 0f, 0f), stats);   // too far to gather

        GraveTesting.Gather(souls, stats, 3, field, health);

        Assert.Equal(3, souls.Charges);
        Assert.Equal(106f, health.Current, 3);   // 1% of 200, three times
        Assert.Single(souls.Souls);
        Assert.Equal(("SOULS 3 / 10", 0.3f), souls.Meter(stats));

        GraveTesting.Gather(souls, stats, 12, field, health);
        Assert.Equal(10, souls.Charges);
    }

    [Fact]
    public void TheNextCast_Deals10PercentMoreForEachCharge_AndSpendsThem()
    {
        var field = PriestTesting.QuietField();
        var souls = new GatheredSouls();
        var stats = GraveTesting.With((GraveCallingTree.SoulSiphon, 1));
        GraveTesting.Gather(souls, stats, 5, field);

        Assert.Equal((1.5f, 1), souls.Empower(stats));
        Assert.Equal(0, souls.Charges);
        Assert.Equal((1f, 1), souls.Empower(stats));
    }

    [Fact]
    public void TheCharges_RaiseTheSkullsOfThePriestsNextCast()
    {
        var field = PriestTesting.QuietField();
        PriestTesting.Sturdy(field, 0f, 15f);
        var priest = GraveTesting.Priest((GraveCallingTree.SoulSiphon, 1));
        for (int i = 0; i < 4; i++)
        {
            priest.OnKill(field.Spawn(Vector3D<float>.Zero), 0f);
        }

        var health = new PlayerHealth(100f);
        for (int i = 0; i < 30 && priest.Skulls.Skulls.Count == 0; i++)
        {
            priest.Fight(GraveTesting.Step, PriestTesting.Hand, Vector3D<float>.Zero, PriestTesting.North, false, field, PriestTesting.FlatGround, health,
                new DamageNumbers());
        }

        var skull = Assert.Single(priest.Skulls.Skulls);
        Assert.Equal(priest.Stats.SkullDamage * 1.4f, skull.Damage, 3);
        Assert.Equal(0, priest.Souls.Charges);
        Assert.Equal("SOULS 0 / 10", priest.Meter?.Label);
    }

    [Fact]
    public void SoulWell_ACastSpendsAtMostThree()
    {
        var field = PriestTesting.QuietField();
        var souls = new GatheredSouls();
        var stats = GraveTesting.With((GraveCallingTree.SoulSiphon, 1), (GraveCallingTree.SoulWell, 1));
        GraveTesting.Gather(souls, stats, 7, field);

        Assert.Equal((1.3f, 1), souls.Empower(stats));
        Assert.Equal(4, souls.Charges);
    }

    [Fact]
    public void LichForm_At10Charges_ThreeSkullsACast_For8Seconds_SpendingNothing_ThenTheChargesStartAgain()
    {
        var field = PriestTesting.QuietField();
        var souls = new GatheredSouls();
        var stats = GraveTesting.With((GraveCallingTree.SoulSiphon, 1), (GraveCallingTree.LichForm, 1));
        GraveTesting.Gather(souls, stats, 10, field);

        Assert.Equal((1f, PriestStats.LichVolleys), souls.Empower(stats));
        Assert.True(souls.IsLich);
        Assert.Equal(10, souls.Charges);
        Assert.Equal("LICH FORM", souls.Meter(stats)?.Label);
        Assert.Equal((1f, PriestStats.LichVolleys), souls.Empower(stats));
        Assert.Equal(10, souls.Charges);

        var hits = new List<PriestHit>();
        for (float t = 0f; t < PriestStats.LichSeconds + 0.1f; t += GraveTesting.Step)
        {
            souls.Update(GraveTesting.Step, Vector3D<float>.Zero, stats, new PlayerHealth(100f), field, new PlagueSkulls(new Random(1)), hits);
        }

        Assert.False(souls.IsLich);
        Assert.Equal(0, souls.Charges);
    }

    [Fact]
    public void WithLichForm_CastsSaveTheChargesUp_UntilThereAreTen()
    {
        var field = PriestTesting.QuietField();
        var souls = new GatheredSouls();
        var stats = GraveTesting.With((GraveCallingTree.SoulSiphon, 1), (GraveCallingTree.LichForm, 1));
        GraveTesting.Gather(souls, stats, 6, field);

        Assert.Equal((1f, 1), souls.Empower(stats));
        Assert.Equal(6, souls.Charges);

        GraveTesting.Gather(souls, stats, 4, field);
        Assert.Equal((1f, PriestStats.LichVolleys), souls.Empower(stats));
        Assert.True(souls.IsLich);
    }

    [Fact]
    public void AsALich_ACastFiresThreeSkulls()
    {
        var field = PriestTesting.QuietField();
        PriestTesting.Sturdy(field, 0f, 18f);
        var skulls = new PlagueSkulls(new Random(1));
        var stats = GraveTesting.With();
        skulls.Empower = () => (1f, PriestStats.LichVolleys);

        PriestTesting.Fire(skulls, stats, field);

        Assert.Equal(3, skulls.Skulls.Count);
    }

    [Fact]
    public void WithoutLichForm_TenChargesAreSimplySpent()
    {
        var field = PriestTesting.QuietField();
        var souls = new GatheredSouls();
        var stats = GraveTesting.With((GraveCallingTree.SoulSiphon, 1));
        GraveTesting.Gather(souls, stats, 10, field);

        Assert.Equal((2f, 1), souls.Empower(stats));
        Assert.False(souls.IsLich);
    }

    [Fact]
    public void VengefulSpirits_AGatheredSoulFliesAtTheNearestEnemy_For60PercentOfASkull()
    {
        var field = PriestTesting.QuietField();
        var enemy = PriestTesting.Sturdy(field, 0f, 7f);
        PriestTesting.Sturdy(field, 0f, 14f);   // out of its reach
        var souls = new GatheredSouls();
        var stats = GraveTesting.With((GraveCallingTree.SoulSiphon, 1), (GraveCallingTree.VengefulSpirits, 1));
        var hits = new List<PriestHit>();

        GraveTesting.Gather(souls, stats, 1, field, hits: hits);
        Assert.Single(souls.Spirits);
        for (float t = 0f; t < 1f; t += GraveTesting.Step)
        {
            souls.Update(GraveTesting.Step, Vector3D<float>.Zero, stats, new PlayerHealth(100f), field, new PlagueSkulls(new Random(1)), hits);
        }

        var hit = Assert.Single(hits);
        Assert.Equal(enemy, hit.Enemy);
        Assert.Equal(PriestSource.Spirit, hit.Source);
        Assert.Equal(0.6f * stats.SkullDamage, hit.Damage, 3);
        Assert.Empty(souls.Spirits);
    }
}

public class BoneTests
{
    [Fact]
    public void BoneSpear_Every4thCast_FliesStraight25Metres_ThroughEverythingInItsLine_For300Percent()
    {
        var field = PriestTesting.QuietField();
        var line = new[] { 3f, 7f, 11f, 16f, 22f }.Select(z => PriestTesting.Sturdy(field, 0f, z)).ToList();
        var beyond = PriestTesting.Sturdy(field, 0f, 28f);
        var aside = PriestTesting.Sturdy(field, 4f, 12f);
        var skulls = new PlagueSkulls(new Random(1));
        var stats = GraveTesting.With((GraveCallingTree.BoneSpear, 1));

        var hits = new List<PriestHit>();
        for (int i = 0; i < 3; i++)
        {
            hits.AddRange(PriestTesting.Fire(skulls, stats, field));
            Assert.Empty(skulls.Spears);
        }

        Assert.DoesNotContain(hits, h => h.Source == PriestSource.Spear);
        int skullsBefore = skulls.Skulls.Count;
        skulls.Cast(stats);
        hits.AddRange(PriestTesting.Run(skulls, stats, field, 0.05f, canCast: true));
        Assert.Single(skulls.Spears);
        Assert.True(skulls.Skulls.Count <= skullsBefore, "the spear's cast fired no skull");
        hits.AddRange(PriestTesting.Run(skulls, stats, field, 1.5f));

        var spear = hits.Where(h => h.Source == PriestSource.Spear).ToList();
        Assert.Equal(line.OrderBy(e => e.Id), spear.Select(h => h.Enemy).OrderBy(e => e.Id));
        Assert.All(spear, h => Assert.Equal(3f * stats.SkullDamage, h.Damage, 3));
        Assert.DoesNotContain(spear, h => h.Enemy == beyond || h.Enemy == aside);
        Assert.Empty(skulls.Spears);
    }

    [Fact]
    public void Impaler_EveryThirdCast_AndHalfAgainAsFar()
    {
        var stats = GraveTesting.With((GraveCallingTree.BoneSpear, 1), (GraveCallingTree.Impaler, 1));
        Assert.Equal(3, stats.SpearCadence);
        Assert.Equal(37.5f, stats.SpearRange, 3);
        Assert.Equal(4, GraveTesting.With((GraveCallingTree.BoneSpear, 1)).SpearCadence);
    }

    [Fact]
    public void WithoutBoneSpear_EveryCastIsSkulls()
    {
        var field = PriestTesting.QuietField();
        PriestTesting.Sturdy(field, 0f, 18f);
        var skulls = new PlagueSkulls(new Random(1));
        var stats = GraveTesting.With();
        for (int i = 0; i < 8; i++)
        {
            PriestTesting.Fire(skulls, stats, field);
        }

        Assert.Empty(skulls.Spears);
    }

    [Fact]
    public void BoneCage_ABlockHoldsTheAttacker_ForASecond_ButNeverABoss()
    {
        var field = PriestTesting.QuietField();
        var ghoul = PriestTesting.Sturdy(field, 0f, 1f);
        var boss = field.Spawn(new Vector3D<float>(2f, 0f, 1f), EnemyKind.HollowKing);
        var priest = GraveTesting.Priest((GraveCallingTree.BoneCage, 1));

        priest.AnswerStrikes(new[] { new Strike(ghoul, 10f, Blocked: true), new Strike(boss, 10f, Blocked: true) }, Vector3D<float>.Zero, 0f, field,
            new PlayerHealth(100f), new DamageNumbers());

        Assert.True(ghoul.IsFrozen);
        Assert.Equal(PriestStats.BaseCageSeconds, ghoul.FrozenFor, 3);
        Assert.False(boss.IsFrozen);
        Assert.Contains(ghoul, priest.Caged);
        Assert.DoesNotContain(boss, priest.Caged);
    }

    [Fact]
    public void WithoutBoneCage_ABlockHoldsNothing_AndALandedBlowNeverCages()
    {
        var field = PriestTesting.QuietField();
        var ghoul = PriestTesting.Sturdy(field, 0f, 1f);
        var other = PriestTesting.Sturdy(field, 2f, 1f);
        var plain = GraveTesting.Priest();
        plain.AnswerStrikes(new[] { new Strike(ghoul, 10f, Blocked: true) }, Vector3D<float>.Zero, 0f, field, new PlayerHealth(100f), new DamageNumbers());
        Assert.False(ghoul.IsFrozen);

        var caging = GraveTesting.Priest((GraveCallingTree.BoneCage, 1));
        caging.AnswerStrikes(new[] { new Strike(other, 10f, Blocked: false) }, Vector3D<float>.Zero, 0f, field, new PlayerHealth(100f), new DamageNumbers());
        Assert.False(other.IsFrozen);
    }
}

public class GraveCallingCardTests
{
    [Fact]
    public void ThePlagueAndRotCards_ComeUpOnlyWithUnholy()
    {
        var unholy = PriestTesting.With((UnholyTree.DeathAndDecay, 1));
        var grave = GraveTesting.With();
        foreach (var card in new[] { PriestUpgrade.Virulence, PriestUpgrade.LongFever, PriestUpgrade.CreepingDeath, PriestUpgrade.SpreadingRot, PriestUpgrade.DeepDecay })
        {
            Assert.True(PriestUpgrades.Offered(card, unholy), $"{card} with Unholy");
            Assert.False(PriestUpgrades.Offered(card, grave), $"{card} with Grave Calling");
        }
    }

    [Fact]
    public void TheSkullShieldAndBodyCards_ComeUpWithEitherTree()
    {
        var unholy = PriestTesting.With();
        var grave = GraveTesting.With();
        foreach (var card in new[]
                 {
                     PriestUpgrade.WickedSkull, PriestUpgrade.HollowChant, PriestUpgrade.BoneSplinter, PriestUpgrade.SwiftBones, PriestUpgrade.GraveHardiness,
                     PriestUpgrade.UnholyMending, PriestUpgrade.SkullWard, PriestUpgrade.RotWalker, PriestUpgrade.GraveRobber, PriestUpgrade.Deathbringer,
                 })
        {
            Assert.True(PriestUpgrades.Offered(card, unholy), $"{card} with Unholy");
            Assert.True(PriestUpgrades.Offered(card, grave), $"{card} with Grave Calling");
        }
    }

    [Fact]
    public void TheGraveCallingCards_ComeUpOnlyWithIt_AndMostOnlyWithTheirMajor()
    {
        var unholy = PriestTesting.With();
        var fresh = GraveTesting.With();
        var built = GraveTesting.With((GraveCallingTree.RaiseDead, 1), (GraveCallingTree.SoulSiphon, 1), (GraveCallingTree.BoneSpear, 1));
        var cards = new[] { PriestUpgrade.DeathKnell, PriestUpgrade.RestlessDead, PriestUpgrade.HardenedBones, PriestUpgrade.SoulLure, PriestUpgrade.SharpenedBone };
        Assert.All(cards, c => Assert.False(PriestUpgrades.Offered(c, unholy)));
        Assert.All(cards, c => Assert.True(PriestUpgrades.Offered(c, built)));
        Assert.True(PriestUpgrades.Offered(PriestUpgrade.DeathKnell, fresh));
        Assert.All(cards.Skip(1), c => Assert.False(PriestUpgrades.Offered(c, fresh)));
        Assert.True(PriestUpgrades.Offered(PriestUpgrade.RestlessDead, GraveTesting.With((GraveCallingTree.RaiseDead, 1))));
        Assert.False(PriestUpgrades.Offered(PriestUpgrade.SoulLure, GraveTesting.With((GraveCallingTree.RaiseDead, 1))));

        var rolled = Enumerable.Range(0, 200).SelectMany(i => PriestUpgrades.Roll(fresh, new Random(i))).Select(c => c.Upgrade).ToHashSet();
        Assert.DoesNotContain(PriestUpgrade.Virulence, rolled);
        Assert.DoesNotContain(PriestUpgrade.RestlessDead, rolled);
        Assert.Contains(PriestUpgrade.DeathKnell, rolled);
    }

    [Fact]
    public void TheNewCards_DoWhatTheySay()
    {
        var stats = GraveTesting.With((GraveCallingTree.RaiseDead, 1), (GraveCallingTree.SoulSiphon, 1), (GraveCallingTree.BoneSpear, 1));
        var plain = GraveTesting.With((GraveCallingTree.RaiseDead, 1), (GraveCallingTree.SoulSiphon, 1), (GraveCallingTree.BoneSpear, 1));
        foreach (var card in new[] { PriestUpgrade.RestlessDead, PriestUpgrade.HardenedBones, PriestUpgrade.SoulLure, PriestUpgrade.SharpenedBone })
        {
            stats.Increase(card);
        }

        Assert.Equal(plain.ServantDamage * 1.2f, stats.ServantDamage, 3);
        Assert.Equal(plain.ServantHealth * 1.25f, stats.ServantHealth, 3);
        Assert.Equal(plain.ServantLife + 3f, stats.ServantLife, 3);
        Assert.Equal(plain.SoulReach + 0.75f, stats.SoulReach, 3);
        Assert.Equal(plain.SoulLife + 2f, stats.SoulLife, 3);
        Assert.Equal(plain.SpearDamage * 1.25f, stats.SpearDamage, 3);
        Assert.All(PriestUpgrades.All, u => Assert.DoesNotContain("loose", u.Description, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DeathKnell_HitsTheWoundedHarder_OnlyWithGraveCalling()
    {
        var field = PriestTesting.QuietField();
        var wounded = field.Spawn(new Vector3D<float>(0f, 0f, 5f));
        wounded.Health = wounded.MaxHealth * 0.4f;
        var healthy = field.Spawn(new Vector3D<float>(3f, 0f, 5f));
        var skulls = new PlagueSkulls(new Random(1));
        var stats = GraveTesting.With();
        stats.Increase(PriestUpgrade.DeathKnell);
        var hits = new List<PriestHit>();

        skulls.Hurt(wounded, 1f, false, PriestSource.Skull, stats, field, hits);
        skulls.Hurt(healthy, 1f, false, PriestSource.Skull, stats, field, hits);

        Assert.Equal(1.15f, hits[0].Damage, 3);
        Assert.Equal(1f, hits[1].Damage, 3);
    }
}

public class GraveCallingClassTests
{
    [Fact]
    public void ThePriest_RaisesServantsAndLeavesSouls_FromItsKills_OnlyWithGraveCalling()
    {
        var field = PriestTesting.QuietField();
        var priest = GraveTesting.Priest((GraveCallingTree.RaiseDead, 1), (GraveCallingTree.SoulSiphon, 1));
        for (int i = 0; i < 8; i++)
        {
            priest.OnKill(field.Spawn(new Vector3D<float>(8f, 0f, 0f)), 0f);
        }

        priest.Fight(GraveTesting.Step, PriestTesting.Hand, Vector3D<float>.Zero, PriestTesting.North, false, field, PriestTesting.FlatGround, new PlayerHealth(100f),
            new DamageNumbers());
        Assert.Single(priest.Servants.Servants);
        Assert.Equal(8, priest.Souls.Souls.Count);

        var unholy = PriestTesting.Priest((UnholyTree.VirulentStrain, 1));
        for (int i = 0; i < 8; i++)
        {
            unholy.OnKill(field.Spawn(new Vector3D<float>(8f, 0f, 0f)), 0f);
        }

        unholy.Fight(GraveTesting.Step, PriestTesting.Hand, Vector3D<float>.Zero, PriestTesting.North, false, field, PriestTesting.FlatGround, new PlayerHealth(100f),
            new DamageNumbers());
        Assert.Empty(unholy.Servants.Servants);
        Assert.Empty(unholy.Souls.Souls);
        Assert.Null(unholy.Meter);
    }

    [Fact]
    public void ARunsEnd_TakesTheDeadAndTheSoulsAway()
    {
        var priest = GraveTesting.Priest((GraveCallingTree.RaiseDead, 1), (GraveCallingTree.SoulSiphon, 1));
        priest.Servants.Raise(Vector3D<float>.Zero, priest.Stats, 1f, PriestTesting.FlatGround);
        priest.OnKill(PriestTesting.QuietField().Spawn(new Vector3D<float>(5f, 0f, 0f)), 0f);

        priest.ReturnToCamp();

        Assert.Empty(priest.Servants.Servants);
        Assert.Empty(priest.Souls.Souls);
        Assert.Null(priest.Meter);
    }

    [Fact]
    public void TheGraveCallingTree_AddsToThePriestsNumbers()
    {
        var priest = GraveTesting.Priest(("bonecraft", 5), ("ossified", 2), ("boneplate", 1), ("marrowchant", 5));
        Assert.Equal(PriestStats.BaseMaxHealth + 40f + 24f + 12f, priest.MaxHealth, 3);
        Assert.Equal(PriestStats.BaseBlockChance + 0.03f, priest.BlockChance, 4);
        Assert.Equal(0.97f, priest.DamageTaken, 4);
        Assert.Equal(PriestStats.BaseCastInterval / 1.4f, priest.Stats.CastInterval, 4);
    }
}

/// <summary>The Grave Calling models (tools/gravecalling_models.py) are in the assets, and the servant has the clips the view plays.</summary>
public class GraveCallingModelTests
{
    private static string Models => Path.Combine(EngineAssets.RepoRoot, "assets", "models");

    [Fact]
    public void TheTreesModels_AreInTheAssets()
    {
        foreach (string file in new[]
                 {
                     GraveView.ServantModel, GraveView.SoulModel, GraveView.SpearModel, GraveView.CageModel, GraveView.LichModel, GraveView.BurstModel, GraveView.RiseModel,
                 })
        {
            Assert.True(File.Exists(Path.Combine(Models, file)), $"{file} is missing");
        }
    }

    [Fact]
    public void TheServant_HasItsClips_AndItsWalkKeepsAFootOnTheGround()
    {
        var model = SkinnedModel.Load(Path.Combine(Models, GraveView.ServantModel));
        foreach (var clip in new[] { GraveView.IdleClip, GraveView.WalkClip, GraveView.ClawClip, GraveView.DieClip })
        {
            Assert.Contains(clip, model.Clips.Keys);
        }

        Assert.Equal(1f, model.Clips[GraveView.ClawClip].Duration, 3);
        var walk = model.Clips[GraveView.WalkClip];
        int left = model.JointIndex("foot_l"), right = model.JointIndex("foot_r");
        for (float t = 0f; t < walk.Duration; t += 0.02f)
        {
            var bones = new Matrix4x4[model.JointCount];
            walk.Sample(t, bones);
            float lowest = MathF.Min(Lowest(model, bones, left), Lowest(model, bones, right));
            Assert.True(lowest > -0.03f, $"at {t:0.00}: a foot is {-lowest:0.000} m under the ground");
            Assert.True(lowest < 0.03f, $"at {t:0.00}: both feet are {lowest:0.000} m up");
        }
    }

    private static float Lowest(SkinnedModel model, Matrix4x4[] bones, int joint)
    {
        const int stride = 19;
        float lowest = float.MaxValue;
        for (int o = 0; o < model.Vertices.Length; o += stride)
        {
            if ((int)model.Vertices[o + 11] == joint)
            {
                lowest = MathF.Min(lowest, Vector3.Transform(new Vector3(model.Vertices[o], model.Vertices[o + 1], model.Vertices[o + 2]), bones[joint]).Y);
            }
        }

        return lowest;
    }
}
