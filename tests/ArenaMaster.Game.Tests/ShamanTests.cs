using ArenaMaster.Game.Combat;
using ArenaMaster.Game.Items;
using ArenaMaster.Game.Progression;
using ArenaMaster.Game.Shaman;
using Silk.NET.Maths;

namespace ArenaMaster.Game.Tests;

internal static class ShamanTesting
{
    public const float Step = 1f / 60f;

    public static readonly Vector3D<float> Hand = new(0f, 1.6f, 0f);

    public static float? FlatGround(float x, float z) => MathF.Abs(x) < 200f && MathF.Abs(z) < 200f ? 0f : null;

    public static bool NoObstacles(Vector3D<float> centre, float radius, out Vector3D<float> pushOut, out float depth)
    {
        pushOut = Vector3D<float>.Zero;
        depth = 0f;
        return false;
    }

    /// <summary>A tree trunk of radius 0.5 standing at (<paramref name="x"/>, <paramref name="z"/>), as the engine would answer for it.</summary>
    public static ObstacleProbe Trunk(float x, float z) => (Vector3D<float> centre, float radius, out Vector3D<float> pushOut, out float depth) =>
    {
        var away = Geometry.FlatDirection(new Vector3D<float>(x, 0f, z), centre, out float distance);
        depth = 0.5f + radius - distance;
        pushOut = away == Vector3D<float>.Zero ? Vector3D<float>.UnitX : away;
        return depth > 0f;
    };

    public static EnemyField QuietField() => new(new Random(1)) { TargetCount = 0 };

    /// <summary>Stats from <paramref name="ranks"/> in Lightning Alignment, with crits turned off so the numbers are exact.</summary>
    public static ShamanStats With(params (string Id, int Ranks)[] ranks) =>
        new() { Tree = AlignmentBonuses.From(ranks.ToDictionary(r => r.Id, r => r.Ranks)), Items = new ItemBonuses { CritChance = -1f } };

    public static Enemy Sturdy(EnemyField field, float x, float z = 0f, EnemyKind? kind = null)
    {
        var enemy = field.Spawn(new Vector3D<float>(x, 0f, z), kind);
        enemy.Health = 10_000f;
        return enemy;
    }

    /// <summary>Runs the storm for <paramref name="seconds"/>, casting from the origin at <paramref name="target"/> unless told not to.</summary>
    public static List<StormHit> Run(RollingLightning storm, ShamanStats stats, EnemyField field, float seconds, Vector3D<float> target, bool canCast = true,
        bool moving = false, ObstacleProbe? obstacles = null)
    {
        var hits = new List<StormHit>();
        for (float t = 0f; t < seconds - 1e-4f; t += Step)
        {
            storm.Update(Step, Hand, Vector3D<float>.Zero, target, moving, stats, field, FlatGround, obstacles ?? NoObstacles, canCast, hits);
        }

        return hits;
    }
}

public class AlignmentTreeTests
{
    [Fact]
    public void TheTree_IsWellFormed()
    {
        var tree = AlignmentTree.Tree;
        var nodes = tree.Nodes;
        Assert.Equal("Lightning Alignment", tree.Name);
        Assert.InRange(nodes.Count, 30, 45);
        Assert.Equal(nodes.Count, nodes.Select(n => n.Id).Distinct().Count());
        Assert.All(nodes, n => Assert.All(n.Parents, p => Assert.True(tree.Node(p).Tier < n.Tier, $"{n.Id} has a parent at or above its tier")));
        Assert.All(nodes.Where(n => n.Tier > 1), n => Assert.NotEmpty(n.Parents));
        Assert.All(nodes.Where(n => !n.Major), n => Assert.Contains("{", n.Text));
        Assert.All(nodes, n => Assert.DoesNotContain("{", n.Describe(1)));
        Assert.All(nodes, n => Assert.Contains(n.Lane, tree.Lanes.Select(l => l.Name)));
        Assert.All(nodes, n => Assert.InRange(n.X, 60f, 920f));
        Assert.Equal(2, nodes.Count(n => n.Tier == 1));
        foreach (var (lane, _) in tree.Lanes)
        {
            Assert.Single(nodes, n => n.Tier == 7 && n.Lane == lane && n.Major);
        }
    }

    [Fact]
    public void EveryNode_ChangesTheBonuses()
    {
        var options = new System.Text.Json.JsonSerializerOptions { IncludeFields = true };
        var none = System.Text.Json.JsonSerializer.Serialize(AlignmentBonuses.From(new Dictionary<string, int>()), options);
        foreach (var node in AlignmentTree.Tree.Nodes)
        {
            var one = System.Text.Json.JsonSerializer.Serialize(AlignmentBonuses.From(new Dictionary<string, int> { [node.Id] = 1 }), options);
            Assert.True(one != none, $"{node.Name} does nothing");
        }
    }
}

public class ShamanStatsTests
{
    [Fact]
    public void ItemsSpeakTheShamansLanguage()
    {
        var stats = new ShamanStats { Items = new ItemBonuses { Projectiles = 1, Area = 0.2f, Duration = 1f } };

        Assert.Equal(ShamanStats.BaseBalls + 1, stats.Balls);
        Assert.Equal(ShamanStats.BaseZapRadius * 1.2f, stats.ZapRadius, 3);
        Assert.Equal(ShamanStats.BaseForkRange * 1.2f, stats.ForkRange, 3);
        Assert.Equal(ShamanStats.BaseLifetime + 1f, stats.Lifetime, 3);
    }

    [Fact]
    public void UpgradesAndTree_AddUp()
    {
        var stats = ShamanTesting.With(("charged", 5), ("voltage", 5), ("rebound", 2), ("branching", 2));
        stats.Increase(ShamanUpgrade.ChargedCore);

        Assert.Equal(ShamanStats.BaseBallDamage * 2.3f, stats.BallDamage, 3);
        Assert.Equal(ShamanStats.BaseBounces + 2, stats.Bounces);
        Assert.Equal(ShamanStats.BaseForks + 2, stats.Forks);
    }
}

public class RollingLightningTests
{
    private static readonly Vector3D<float> Ahead = new(10f, 0f, 0f);

    [Fact]
    public void TheBall_IsLobbed_AndFirstComesDownWhereItWasAimed()
    {
        var storm = new RollingLightning(new Random(1));
        var stats = new ShamanStats();
        var ball = storm.Lob(ShamanTesting.Hand, Ahead, stats);
        var field = ShamanTesting.QuietField();

        for (int i = 0; i < 240 && ball.BouncesLeft == stats.Bounces; i++)
        {
            storm.Update(ShamanTesting.Step, ShamanTesting.Hand, Vector3D<float>.Zero, Ahead, false, stats, field, ShamanTesting.FlatGround, ShamanTesting.NoObstacles,
                canCast: false, new List<StormHit>());
        }

        Assert.Equal(stats.Bounces - 1, ball.BouncesLeft);
        Assert.InRange(ball.Position.X, 9.4f, 10.6f);
        Assert.True(ball.Velocity.Y > 0f);   // on its way back up
    }

    [Fact]
    public void TheBall_BouncesAlong_ThenFades()
    {
        var storm = new RollingLightning(new Random(1));
        var stats = new ShamanStats();
        storm.Lob(ShamanTesting.Hand, Ahead, stats);

        ShamanTesting.Run(storm, stats, ShamanTesting.QuietField(), stats.Lifetime + 0.1f, Ahead, canCast: false);

        Assert.Empty(storm.Balls);
    }

    [Fact]
    public void EachBounce_Zaps()
    {
        var field = ShamanTesting.QuietField();
        var nearTheLanding = ShamanTesting.Sturdy(field, 10f, 1.4f);   // beside where it comes down, not in its path
        var storm = new RollingLightning(new Random(1));
        var stats = ShamanTesting.With();
        storm.Lob(ShamanTesting.Hand, Ahead, stats);

        var hits = ShamanTesting.Run(storm, stats, field, 1f, Ahead, canCast: false);   // it comes down at about 0.8 s, and up again

        var zap = Assert.Single(hits, h => h.Source == StormSource.Zap);
        Assert.Equal(nearTheLanding, zap.Enemy);
        Assert.Equal(stats.ZapDamage, zap.Damage, 3);
    }

    [Fact]
    public void AStruckEnemy_ForksTheLightning_ToItsNeighbours()
    {
        var field = ShamanTesting.QuietField();
        var struck = ShamanTesting.Sturdy(field, 10f);
        var near1 = ShamanTesting.Sturdy(field, 12f, 3f);
        var near2 = ShamanTesting.Sturdy(field, 12f, -3f);
        var near3 = ShamanTesting.Sturdy(field, 14f, 0f);
        var far = ShamanTesting.Sturdy(field, 10f, 12f);
        var storm = new RollingLightning(new Random(1));
        var stats = ShamanTesting.With();
        storm.Lob(ShamanTesting.Hand, Ahead, stats);

        var hits = ShamanTesting.Run(storm, stats, field, 1f, Ahead, canCast: false);

        Assert.Contains(hits, h => h.Enemy == struck && h.Source == StormSource.Ball && h.Damage == stats.BallDamage);
        var forks = hits.Where(h => h.Source == StormSource.Fork).ToList();
        Assert.Equal(ShamanStats.BaseForks, forks.Count);   // the two nearest of the three
        Assert.DoesNotContain(forks, h => h.Enemy == struck || h.Enemy == far || h.Enemy == near3);
        Assert.All(forks, h => Assert.Equal(stats.ForkDamage, h.Damage, 3));
        Assert.True(near1.Health < 10_000f && near2.Health < 10_000f);
    }

    [Fact]
    public void AGlanceOffATree_TurnsTheBall_AndForksFromTheTrunk()
    {
        var field = ShamanTesting.QuietField();
        var byTheTree = ShamanTesting.Sturdy(field, 6f, 3f);
        var storm = new RollingLightning(new Random(1));
        var stats = ShamanTesting.With();
        var ball = storm.Roll(Vector3D<float>.Zero, Vector3D<float>.UnitX, stats);   // rolling low along +X, into the trunk at x = 6

        var hits = ShamanTesting.Run(storm, stats, field, 0.8f, Ahead, canCast: false, obstacles: ShamanTesting.Trunk(6f, 0f));

        Assert.True(ball.Velocity.X < 0f, "it bounced back off the trunk");
        Assert.Contains(hits, h => h.Enemy == byTheTree && h.Source == StormSource.Fork);
    }

    [Fact]
    public void LightningRod_ChargesTheTree_WhichZapsWhatIsNear()
    {
        var field = ShamanTesting.QuietField();
        var byTheTree = ShamanTesting.Sturdy(field, 6f, 3f);
        var storm = new RollingLightning(new Random(1));
        var stats = ShamanTesting.With((AlignmentTree.LightningRod, 1));
        storm.Roll(Vector3D<float>.Zero, Vector3D<float>.UnitX, stats);

        var hits = ShamanTesting.Run(storm, stats, field, 2f, Ahead, canCast: false, obstacles: ShamanTesting.Trunk(6f, 0f));

        Assert.NotEmpty(storm.Rods);
        Assert.True(hits.Count(h => h.Enemy == byTheTree && h.Source == StormSource.Rod) >= 2);
    }

    [Fact]
    public void Paralysis_HoldsForkedEnemies_ButNotABoss()
    {
        var field = ShamanTesting.QuietField();
        var ghoul = ShamanTesting.Sturdy(field, 2f);
        var king = ShamanTesting.Sturdy(field, -2f, 0f, EnemyKind.HollowKing);
        var storm = new RollingLightning(new Random(1));

        storm.Fork(Vector3D<float>.Zero, 5, new HashSet<Enemy>(), chain: false, StormSource.Fork, ShamanTesting.With((AlignmentTree.Paralysis, 1)), field,
            new List<StormHit>());

        Assert.True(ghoul.IsFrozen);
        Assert.False(king.IsFrozen);
    }

    [Fact]
    public void ChainReaction_LeapsOnOnceMore()
    {
        var field = ShamanTesting.QuietField();
        var first = ShamanTesting.Sturdy(field, 3f);
        var second = ShamanTesting.Sturdy(field, 7f);   // out of reach of the origin, but near the first
        var storm = new RollingLightning(new Random(1));
        var hits = new List<StormHit>();

        storm.Fork(Vector3D<float>.Zero, 1, new HashSet<Enemy>(), chain: true, StormSource.Fork, ShamanTesting.With((AlignmentTree.ChainReaction, 1)), field, hits);

        Assert.Equal(new[] { first, second }, hits.Select(h => h.Enemy));
        Assert.Equal(2, storm.Arcs.Count);   // a bolt drawn for each leap
    }

    [Fact]
    public void Supercell_IsEveryFifthCast()
    {
        var storm = new RollingLightning(new Random(1));
        var stats = ShamanTesting.With((AlignmentTree.Supercell, 1));
        for (int i = 0; i < 4; i++)
        {
            storm.Cast(ShamanTesting.Hand, Ahead, stats);
        }

        Assert.DoesNotContain(storm.Balls, b => b.Supercell);
        storm.Cast(ShamanTesting.Hand, Ahead, stats);

        var supercell = Assert.Single(storm.Balls, b => b.Supercell);
        Assert.Equal(stats.BallRadius * ShamanStats.SupercellSize, supercell.Radius, 3);
        Assert.Equal(stats.BallDamage * ShamanStats.SupercellDamage, supercell.Damage, 3);
        Assert.Equal(stats.Bounces + ShamanStats.SupercellBounces, supercell.BouncesLeft);
    }

    [Fact]
    public void TheThunderGod_BurstsAFadingBall()
    {
        var field = ShamanTesting.QuietField();
        for (float x = 0f; x <= 40f; x += 2f)
        {
            ShamanTesting.Sturdy(field, x, 3.5f);   // a line of bystanders beside its path: out of reach of its zaps, not of the burst
        }

        var storm = new RollingLightning(new Random(1));
        var stats = ShamanTesting.With((AlignmentTree.ThunderGod, 1));
        storm.Lob(ShamanTesting.Hand, Ahead, stats);

        var hits = ShamanTesting.Run(storm, stats, field, stats.Lifetime + 0.1f, Ahead, canCast: false);

        Assert.Empty(storm.Balls);
        Assert.DoesNotContain(hits, h => h.Source == StormSource.Zap);
        Assert.Contains(hits, h => h.Source == StormSource.Burst && Math.Abs(h.Damage - stats.BallDamage * ShamanStats.ThunderGodShare) < 0.01f);
    }

    [Fact]
    public void LivingCurrent_ForksFromEveryKill_ButNotFromItsOwn()
    {
        var field = ShamanTesting.QuietField();
        var weak = field.Spawn(new Vector3D<float>(0f, 0f, 0f));
        weak.Health = 1f;
        var n1 = field.Spawn(new Vector3D<float>(3f, 0f, 0f));
        n1.Health = 1f;   // dies to the living fork, which must not fork again
        var n2 = ShamanTesting.Sturdy(field, -3f);
        var beyond = ShamanTesting.Sturdy(field, 6.5f);   // near n1 only
        var storm = new RollingLightning(new Random(1));
        var hits = new List<StormHit>();

        storm.Shock(weak, 5f, StormSource.Ball, ShamanTesting.With((AlignmentTree.LivingCurrent, 1)), field, hits);

        Assert.Contains(hits, h => h.Enemy == n1 && h.Source == StormSource.LivingFork && h.Killed);
        Assert.Contains(hits, h => h.Enemy == n2 && h.Source == StormSource.LivingFork);
        Assert.DoesNotContain(hits, h => h.Enemy == beyond);
    }

    [Fact]
    public void TheEye_ShocksOnlyWhileMoving()
    {
        var field = ShamanTesting.QuietField();
        var near = ShamanTesting.Sturdy(field, 2f);
        field.Spawn(new Vector3D<float>(60f, 0f, 0f));   // something in the world, far off
        var stats = ShamanTesting.With((AlignmentTree.EyeOfTheStorm, 1));

        var still = ShamanTesting.Run(new RollingLightning(new Random(1)), stats, field, 1.2f, Ahead, canCast: false, moving: false);
        var moving = ShamanTesting.Run(new RollingLightning(new Random(1)), stats, field, 1.2f, Ahead, canCast: false, moving: true);

        Assert.DoesNotContain(still, h => h.Source == StormSource.Eye);
        Assert.Equal(3, moving.Count(h => h.Source == StormSource.Eye && h.Enemy == near));
    }

    [Fact]
    public void CallLightning_StrikesTheNearest_EveryEightSeconds()
    {
        var field = ShamanTesting.QuietField();
        var enemies = Enumerable.Range(1, 7).Select(i => ShamanTesting.Sturdy(field, i * 2f)).ToList();
        var stats = ShamanTesting.With((AlignmentTree.CallLightning, 1));

        var hits = ShamanTesting.Run(new RollingLightning(new Random(1)), stats, field, ShamanStats.CallInterval + 0.05f, Ahead, canCast: false);

        var strikes = hits.Where(h => h.Source == StormSource.Call).ToList();
        Assert.Equal(enemies.Take(ShamanStats.CallStrikes), strikes.Select(h => h.Enemy));
        Assert.All(strikes, h => Assert.Equal(stats.BallDamage * ShamanStats.CallShare, h.Damage, 3));
    }

    [Fact]
    public void AStun_HoldsTheCast()
    {
        var storm = new RollingLightning(new Random(1));

        ShamanTesting.Run(storm, new ShamanStats(), ShamanTesting.QuietField(), 3f, Ahead, canCast: false);

        Assert.Equal(0, storm.Casts);
    }

    [Fact]
    public void Casts_ComeAtTheirInterval_WithTheirBalls()
    {
        var storm = new RollingLightning(new Random(1));
        var stats = new ShamanStats { Items = new ItemBonuses { Projectiles = 1 } };

        ShamanTesting.Run(storm, stats, ShamanTesting.QuietField(), RollingLightning.FirstCast + 0.05f, Ahead);

        Assert.Equal(1, storm.Casts);
        Assert.Equal(2, storm.Balls.Count);
    }
}

public class ShamanAnswerTests
{
    [Fact]
    public void StaticSkin_ShocksTheAttacker()
    {
        var field = ShamanTesting.QuietField();
        var attacker = ShamanTesting.Sturdy(field, 1f);
        var shaman = new ShamanClass(new Random(1));
        shaman.UseTree(new Dictionary<string, int> { ["staticskin"] = 2 });
        shaman.BeginRun(new ItemBonuses(), new PlayerHealth(100f));

        shaman.AnswerStrikes(new[] { new Strike(attacker, 8f, false) }, Vector3D<float>.Zero, field, new PlayerHealth(100f), new DamageNumbers());

        Assert.Equal(10_000f - 30f, attacker.Health, 3);
    }
}
