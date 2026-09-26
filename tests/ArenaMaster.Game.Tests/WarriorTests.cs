using ArenaMaster.Game.Combat;
using ArenaMaster.Game.Items;
using ArenaMaster.Game.Progression;
using ArenaMaster.Game.Warrior;
using Silk.NET.Maths;

namespace ArenaMaster.Game.Tests;

internal static class WarriorTesting
{
    public const float Step = 1f / 60f;

    public static EnemyField QuietField() => new(new Random(1)) { TargetCount = 0 };

    /// <summary>No crits, for exact numbers.</summary>
    public static WarriorStats With(params (string Id, int Ranks)[] ranks) =>
        new() { Tree = BerserkerBonuses.From(ranks.ToDictionary(r => r.Id, r => r.Ranks)), Items = new ItemBonuses { CritChance = -1f } };

    /// <summary>A ghoul that won't die of a hit, standing at (<paramref name="x"/>, <paramref name="z"/>).</summary>
    public static Enemy Sturdy(EnemyField field, float x, float z)
    {
        var enemy = field.Spawn(new Vector3D<float>(x, 0f, z));
        enemy.Health = 10_000f;
        return enemy;
    }

    /// <summary>Runs the axes at the origin, facing +Z, for <paramref name="seconds"/>.</summary>
    public static List<CleaveHit> Swing(CleaveAxes axes, WarriorStats stats, EnemyField field, float seconds)
    {
        var hits = new List<CleaveHit>();
        for (float t = 0f; t < seconds - 1e-4f; t += Step)
        {
            axes.Update(Step, Vector3D<float>.Zero, 0f, stats, field, canSwing: true, hits);
        }

        return hits;
    }

    /// <summary>A Warrior whose class runs its fight at the origin facing +Z for <paramref name="seconds"/>.</summary>
    public static void Fight(WarriorClass warrior, EnemyField field, PlayerHealth health, float seconds)
    {
        for (float t = 0f; t < seconds - 1e-4f; t += Step)
        {
            warrior.Fight(Step, Vector3D<float>.Zero, 0f, stunned: false, field, health, new DamageNumbers());
        }
    }

    public static WarriorClass Warrior(params (string Id, int Ranks)[] ranks)
    {
        var warrior = new WarriorClass(new Random(3));
        warrior.UseTree(ranks.ToDictionary(r => r.Id, r => r.Ranks));
        warrior.BeginRun(new ItemBonuses { CritChance = -1f }, new PlayerHealth(100f));
        return warrior;
    }
}

public class BerserkerTreeTests
{
    private static TreeNode Node(string id) => BerserkerTree.Tree.Node(id);

    [Fact]
    public void TheTree_IsWellFormed()
    {
        var tree = BerserkerTree.Tree;
        var nodes = tree.Nodes;
        Assert.InRange(nodes.Count, 30, 45);
        Assert.Equal(nodes.Count, nodes.Select(n => n.Id).Distinct().Count());
        Assert.All(nodes, n => Assert.All(n.Parents, p => Assert.True(tree.Node(p).Tier < n.Tier, $"{n.Id} has a parent at or above its tier")));
        Assert.All(nodes.Where(n => n.Tier > 1), n => Assert.NotEmpty(n.Parents));
        Assert.All(nodes.Where(n => !n.Major), n => Assert.Contains("{", n.Text));
        Assert.All(nodes, n => Assert.DoesNotContain("{", n.Describe(1)));
        Assert.All(nodes, n => Assert.Contains(n.Lane, tree.Lanes.Select(l => l.Name)));
        Assert.All(nodes, n => Assert.InRange(n.X, 60f, 920f));
        Assert.Equal(2, nodes.Count(n => n.Tier == 1));
        Assert.Equal(13, nodes.Count(n => n.Major));
        Assert.Equal(7, tree.TierLevels.Count);
    }

    [Fact]
    public void EachLane_EndsInACapstone()
    {
        foreach (var (lane, _) in BerserkerTree.Tree.Lanes)
        {
            Assert.Single(BerserkerTree.Tree.Nodes, n => n.Tier == 7 && n.Lane == lane && n.Major);
        }
    }

    [Fact]
    public void Berserking_IsATierTwoMajor_ThatEveryRageNodeGrowsFrom()
    {
        var berserking = Node(BerserkerTree.Berserking);
        Assert.True(berserking.Major);
        Assert.Equal(2, berserking.Tier);

        // Every node in the Rage lane is reached through Berserking.
        bool Below(string id) => id == BerserkerTree.Berserking || Node(id).Parents.All(Below);
        Assert.All(BerserkerTree.Tree.Nodes.Where(n => n.Lane == "Rage"), n => Assert.True(Below(n.Id), $"{n.Name} can be taken without Berserking"));
    }

    [Fact]
    public void EveryNode_ChangesTheBonuses()
    {
        var options = new System.Text.Json.JsonSerializerOptions { IncludeFields = true };
        var none = System.Text.Json.JsonSerializer.Serialize(BerserkerBonuses.From(new Dictionary<string, int>()), options);
        foreach (var node in BerserkerTree.Tree.Nodes)
        {
            var one = System.Text.Json.JsonSerializer.Serialize(BerserkerBonuses.From(new Dictionary<string, int> { [node.Id] = 1 }), options);
            Assert.True(one != none, $"{node.Name} does nothing");
        }
    }
}

public class CleaveTests
{
    [Fact]
    public void ACleave_HitsWhatIsInItsWedge_OnceEach_AndNotWhatIsBehind()
    {
        var field = WarriorTesting.QuietField();
        var ahead = WarriorTesting.Sturdy(field, 0f, 3f);
        var aside = WarriorTesting.Sturdy(field, 2.5f, 2.5f);   // 45 degrees off: inside a 100-degree sweep
        var behind = WarriorTesting.Sturdy(field, 0f, -3f);
        var far = WarriorTesting.Sturdy(field, 0f, 9f);          // past the 6 m reach
        var axes = new CleaveAxes(new Random(1));
        var stats = WarriorTesting.With();

        var hits = WarriorTesting.Swing(axes, stats, field, CleaveAxes.FirstSwing + WarriorStats.TravelTime + 0.05f);

        Assert.Single(hits, h => h.Enemy == ahead);
        Assert.Single(hits, h => h.Enemy == aside);
        Assert.DoesNotContain(hits, h => h.Enemy == behind || h.Enemy == far);
        Assert.All(hits, h => Assert.Equal(WarriorStats.BaseCleaveDamage, h.Damage, 3));
    }

    [Fact]
    public void TheWave_RollsOut_ReachingNearEnemiesFirst()
    {
        var field = WarriorTesting.QuietField();
        var near = WarriorTesting.Sturdy(field, 0f, 1.5f);
        var out5 = WarriorTesting.Sturdy(field, 0f, 5f);
        var axes = new CleaveAxes(new Random(1));
        var stats = WarriorTesting.With();
        axes.Swing(Vector3D<float>.Zero, 0f, stats);

        var hits = new List<CleaveHit>();
        axes.Update(0.05f, Vector3D<float>.Zero, 0f, stats, field, canSwing: false, hits);
        Assert.Contains(hits, h => h.Enemy == near);
        Assert.DoesNotContain(hits, h => h.Enemy == out5);

        axes.Update(WarriorStats.TravelTime, Vector3D<float>.Zero, 0f, stats, field, canSwing: false, hits);
        Assert.Contains(hits, h => h.Enemy == out5);
    }

    [Fact]
    public void TheAxes_TakeTurns_EachWaveLeaningItsOwnWay()
    {
        var axes = new CleaveAxes(new Random(1));
        var stats = WarriorTesting.With();
        axes.Swing(Vector3D<float>.Zero, 0f, stats);
        axes.Swing(Vector3D<float>.Zero, 0f, stats);
        Assert.True(axes.Waves[0].Yaw > 0f && axes.Waves[1].Yaw < 0f);
        Assert.Equal(axes.Waves[0].Yaw, -axes.Waves[1].Yaw, 4);
    }

    [Fact]
    public void Size_GrowsTheReachAndTheSweep_AllTheWayRoundAtMost()
    {
        var stats = WarriorTesting.With();
        Assert.Equal(WarriorStats.BaseReach, stats.Reach, 3);
        Assert.Equal(WarriorStats.BaseArc, stats.ArcDegrees, 3);

        stats.Items = new ItemBonuses { Area = 1f, CritChance = -1f };   // twice the size
        Assert.Equal(WarriorStats.BaseReach * 2f, stats.Reach, 3);
        Assert.Equal(WarriorStats.BaseArc * 2f, stats.ArcDegrees, 3);

        stats.Items = new ItemBonuses { Area = 3f, AreaMultiplier = 1.3f };
        Assert.Equal(360f, stats.ArcDegrees);
        Assert.True(stats.Reach > 30f);

        var tree = WarriorTesting.With((BerserkerTree.GreatSweep, 1), ("widearc", 3), ("longreach", 3));
        Assert.Equal(WarriorStats.BaseArc + 60f + 45f, tree.ArcDegrees, 3);
        Assert.Equal(WarriorStats.BaseReach * 1.3f, tree.Reach, 3);
    }

    [Fact]
    public void AWholeCircle_HitsEverythingAround()
    {
        var field = WarriorTesting.QuietField();
        var around = Enumerable.Range(0, 8).Select(i => WarriorTesting.Sturdy(field, 3f * MathF.Sin(i * MathF.Tau / 8f), 3f * MathF.Cos(i * MathF.Tau / 8f))).ToList();
        var axes = new CleaveAxes(new Random(1));
        var stats = WarriorTesting.With();
        stats.Items = new ItemBonuses { Area = 3f, CritChance = -1f };

        var hits = WarriorTesting.Swing(axes, stats, field, CleaveAxes.FirstSwing + WarriorStats.TravelTime + 0.05f);

        Assert.All(around, e => Assert.Single(hits, h => h.Enemy == e));
    }

    [Fact]
    public void Whirlwind_EveryFourthSwing_GoesAllTheWayRound_Further()
    {
        var axes = new CleaveAxes(new Random(1));
        var stats = WarriorTesting.With((BerserkerTree.Whirlwind, 1));
        for (int i = 0; i < 4; i++)
        {
            axes.Swing(Vector3D<float>.Zero, 0f, stats);
        }

        Assert.Equal(MathF.PI, axes.Waves[3].HalfArc);
        Assert.Equal(stats.Reach * WarriorStats.WhirlwindReach, axes.Waves[3].Reach, 3);
        Assert.True(axes.Waves[0].HalfArc < MathF.PI);
    }

    [Fact]
    public void TwinFury_AndEchoingCleave_AddWaves()
    {
        var axes = new CleaveAxes(new Random(1));
        var stats = WarriorTesting.With((BerserkerTree.TwinFury, 1), (BerserkerTree.EchoingCleave, 1));
        axes.Swing(Vector3D<float>.Zero, 0f, stats);

        Assert.Equal(3, axes.Waves.Count);
        Assert.Contains(axes.Waves, w => MathF.Abs(w.Damage - stats.CleaveDamage * WarriorStats.TwinDamage) < 1e-3f && w.Delay == 0f);
        Assert.Contains(axes.Waves, w => MathF.Abs(w.Damage - stats.CleaveDamage * WarriorStats.EchoDamage) < 1e-3f && w.Delay > 0f);
    }

    [Fact]
    public void Execute_FinishesALowEnemy_ButNotABoss()
    {
        var field = WarriorTesting.QuietField();
        var low = field.Spawn(new Vector3D<float>(0f, 0f, 3f));
        low.Health = low.MaxHealth * 0.15f + 1000f;   // well over a hit, but...
        var boss = field.Spawn(new Vector3D<float>(0f, 0f, 4f), EnemyKind.HollowKing);
        boss.Health = boss.MaxHealth * 0.1f;
        var axes = new CleaveAxes(new Random(1));
        var hits = new List<CleaveHit>();

        var stats = WarriorTesting.With((BerserkerTree.Execute, 1));
        low.Health = low.MaxHealth * 0.15f;
        float bossBefore = boss.Health;
        axes.Hurt(low, 1f, false, CleaveSource.Cleave, stats, field, hits);
        axes.Hurt(boss, 1f, false, CleaveSource.Cleave, stats, field, hits);

        Assert.False(low.IsAlive);
        Assert.True(boss.IsAlive && boss.Health < bossBefore);
    }
}

public class BerserkingTests
{
    [Fact]
    public void WithoutBerserking_NoRageBuilds()
    {
        var warrior = WarriorTesting.Warrior();
        var field = WarriorTesting.QuietField();
        for (int i = 0; i < 5; i++)
        {
            WarriorTesting.Sturdy(field, i - 2f, 3f);
        }

        WarriorTesting.Fight(warrior, field, new PlayerHealth(100f), 2f);
        Assert.Equal(0, warrior.Fury.Rage);
        Assert.Null(warrior.Status);
    }

    [Fact]
    public void Hits_GrantRage_OnePerEnemy_UpToTheMost()
    {
        var warrior = WarriorTesting.Warrior((BerserkerTree.Berserking, 1));
        var field = WarriorTesting.QuietField();
        for (int i = 0; i < 5; i++)
        {
            WarriorTesting.Sturdy(field, i - 2f, 3f);
        }

        WarriorTesting.Fight(warrior, field, new PlayerHealth(100f), CleaveAxes.FirstSwing + WarriorStats.TravelTime + 0.05f);
        Assert.Equal(5, warrior.Fury.Rage);   // one swing, five enemies
        Assert.Equal("RAGE 5 / 20", warrior.Status);

        WarriorTesting.Fight(warrior, field, new PlayerHealth(100f), 5f);
        Assert.Equal(20, warrior.Fury.Rage);   // the most, to start with
    }

    [Fact]
    public void Rage_MakesTheSwingsHarderAndFaster_OnePercentEach()
    {
        var stats = WarriorTesting.With((BerserkerTree.Berserking, 1));
        float damage = stats.CleaveDamage, interval = stats.SwingInterval;
        stats.Rage = 20;
        Assert.Equal(damage * 1.2f, stats.CleaveDamage, 3);
        Assert.Equal(interval / 1.2f, stats.SwingInterval, 4);

        var seething = WarriorTesting.With((BerserkerTree.Berserking, 1), ("seething", 2));
        seething.Rage = 20;
        Assert.Equal(damage * 1.3f, seething.CleaveDamage, 3);   // each rage counts 50% more
    }

    [Fact]
    public void Rage_LastsFiveSeconds_UnlessHitsKeepItUp()
    {
        var stats = WarriorTesting.With((BerserkerTree.Berserking, 1));
        var fury = new Fury();
        fury.Gain(8, stats);
        fury.Update(4.9f);
        Assert.Equal(8, fury.Rage);
        fury.Gain(1, stats);   // a hit: the clock starts again
        fury.Update(4.9f);
        Assert.Equal(9, fury.Rage);
        fury.Update(0.2f);
        Assert.Equal(0, fury.Rage);
    }

    [Fact]
    public void TheRageNodes_RaiseTheMost_AndTheTime()
    {
        var stats = WarriorTesting.With((BerserkerTree.Berserking, 1), ("hotblood", 3), ("simmer", 2));
        Assert.Equal(29, stats.MaxRage);
        Assert.Equal(7f, stats.RageDuration);
        Assert.Equal(58, WarriorTesting.With((BerserkerTree.Berserking, 1), ("hotblood", 3), (BerserkerTree.AvatarOfRage, 1)).MaxRage);
    }

    [Fact]
    public void BloodRage_CutsDamageTaken_ByTheRage()
    {
        var stats = WarriorTesting.With((BerserkerTree.Berserking, 1), (BerserkerTree.BloodRage, 1));
        stats.Rage = 20;
        Assert.Equal(0.9f, stats.DamageTaken, 4);
    }

    [Fact]
    public void Blows_AreAnswered_ByPainRiposteAndBladeWall()
    {
        var warrior = WarriorTesting.Warrior((BerserkerTree.Berserking, 1), (BerserkerTree.FuelledByPain, 1), (BerserkerTree.Riposte, 1), (BerserkerTree.BladeWall, 1));
        var field = WarriorTesting.QuietField();
        var attacker = WarriorTesting.Sturdy(field, 0f, 1f);
        var health = new PlayerHealth(100f);
        health.TakeDamage(50f);

        warrior.AnswerStrikes(new[] { new Strike(attacker, 10f, Blocked: false) }, field, health, new DamageNumbers());
        Assert.Equal(WarriorStats.PainRage, warrior.Fury.Rage);

        warrior.AnswerStrikes(new[] { new Strike(attacker, 10f, Blocked: true) }, field, health, new DamageNumbers());
        Assert.Equal(2 * WarriorStats.PainRage + WarriorStats.BladeWallRage, warrior.Fury.Rage);
        Assert.True(attacker.Health < 10_000f);   // the riposte
        Assert.True(health.Current > 50f);      // Blade Wall's heal
    }

    [Fact]
    public void BloodFrenzy_KillsGiveAttackSpeed_ForAWhile()
    {
        var warrior = WarriorTesting.Warrior((BerserkerTree.BloodFrenzy, 1));
        var field = WarriorTesting.QuietField();
        float before = warrior.Stats.SwingInterval;
        for (int i = 0; i < 50; i++)
        {
            warrior.OnKill(field.Spawn(new Vector3D<float>(50f, 0f, 50f)), 0f);
        }

        WarriorTesting.Fight(warrior, field, new PlayerHealth(100f), WarriorTesting.Step);
        Assert.Equal(before / (1f + WarriorStats.FrenzyCap), warrior.Stats.SwingInterval, 3);

        WarriorTesting.Fight(warrior, field, new PlayerHealth(100f), WarriorStats.FrenzySeconds + 0.1f);
        Assert.Equal(before, warrior.Stats.SwingInterval, 4);
    }

    [Fact]
    public void TheRageUpgrades_OnlyComeUp_WithBerserking()
    {
        var random = new Random(2);
        var plain = WarriorTesting.With();
        var rage = WarriorTesting.With((BerserkerTree.Berserking, 1));
        bool RageCard(WarriorStats stats) => Enumerable.Range(0, 200).SelectMany(_ => WarriorUpgrades.Roll(stats, random))
            .Any(c => c.Upgrade is WarriorUpgrade.DeepFury or WarriorUpgrade.LastingRage);

        Assert.False(RageCard(plain));
        Assert.True(RageCard(rage));
    }

    [Fact]
    public void LifeLeech_HealsFromTheDamageDealt()
    {
        var warrior = WarriorTesting.Warrior(("bloodthirst", 3));
        var field = WarriorTesting.QuietField();
        WarriorTesting.Sturdy(field, 0f, 3f);
        var health = new PlayerHealth(100f);
        health.TakeDamage(50f);

        WarriorTesting.Fight(warrior, field, health, CleaveAxes.FirstSwing + WarriorStats.TravelTime + 0.05f);

        Assert.Equal(50f + WarriorStats.BaseCleaveDamage * 0.009f, health.Current, 3);
    }
}
