using ArenaMaster.Game.Combat;
using ArenaMaster.Game.Items;
using ArenaMaster.Game.Progression;
using ArenaMaster.Game.Warrior;
using Silk.NET.Maths;

namespace ArenaMaster.Game.Tests;

internal static class ReaverTesting
{
    public const float Step = WarriorTesting.Step;

    /// <summary>A Reaver's stats (the axes thrown), no crits and none of the bounces an axe starts with, for exact numbers: a plain straight throw.</summary>
    public static WarriorStats With(params (string Id, int Ranks)[] ranks) =>
        new() { Throwing = true, Reaver = ReaverBonuses.From(ranks.ToDictionary(r => r.Id, r => r.Ranks)), Items = new ItemBonuses { CritChance = -1f, Chains = -WarriorStats.BaseAxeChains } };

    /// <summary>A Warrior with the Reaver tree active and <paramref name="ranks"/> in it, a run begun, no crits.</summary>
    public static WarriorClass Warrior(params (string Id, int Ranks)[] ranks)
    {
        var warrior = new WarriorClass(new Random(3));
        warrior.ChooseTree(ReaverTree.TreeId);
        warrior.UseTree(ranks.ToDictionary(r => r.Id, r => r.Ranks));
        warrior.BeginRun(new ItemBonuses { CritChance = -1f }, new PlayerHealth(100f));
        return warrior;
    }

    /// <summary>
    /// Runs the thrown axes for <paramref name="seconds"/> with the Warrior at <paramref name="feet"/> (the origin by default), throwing toward
    /// <paramref name="yaw"/> (null: no throws of their own, only what is already in the air).
    /// </summary>
    public static List<CleaveHit> Run(ThrownAxes throws, WarriorStats stats, EnemyField field, float seconds, float? yaw = null, Vector3D<float>? feet = null,
                                      bool charging = false)
    {
        var hits = new List<CleaveHit>();
        for (float t = 0f; t < seconds - 1e-4f; t += Step)
        {
            throws.Update(Step, feet ?? Vector3D<float>.Zero, yaw, stats, field, canThrow: true, charging, hits);
        }

        return hits;
    }

    /// <summary>How long a throw takes to fly out its range and back to a Warrior standing still, and a little more.</summary>
    public static float RoundTrip(WarriorStats stats) => 2f * stats.ThrowRange / stats.AxeSpeed + 0.2f;
}

public class ReaverTreeTests
{
    private static TreeNode Node(string id) => ReaverTree.Tree.Node(id);

    [Fact]
    public void TheTree_IsWellFormed()
    {
        var tree = ReaverTree.Tree;
        var nodes = tree.Nodes;
        Assert.InRange(nodes.Count, 34, 40);
        Assert.Equal(nodes.Count, nodes.Select(n => n.Id).Distinct().Count());
        Assert.All(nodes, n => Assert.All(n.Parents, p => Assert.True(tree.Node(p).Tier < n.Tier, $"{n.Id} has a parent at or above its tier")));
        Assert.All(nodes.Where(n => n.Tier > 1), n => Assert.NotEmpty(n.Parents));
        Assert.All(nodes.Where(n => !n.Major), n => Assert.Contains("{", n.Text));
        Assert.All(nodes.Where(n => !n.Major), n => Assert.InRange(n.MaxRanks, 2, 5));
        Assert.All(nodes.Where(n => n.Major), n => Assert.Equal(1, n.MaxRanks));
        Assert.All(nodes, n => Assert.DoesNotContain("{", n.Describe(1)));
        Assert.All(nodes, n => Assert.DoesNotContain("looses", n.Text));
        Assert.All(nodes, n => Assert.Contains(n.Lane, tree.Lanes.Select(l => l.Name)));
        Assert.All(nodes, n => Assert.InRange(n.X, 60f, 920f));
        Assert.All(nodes, n => Assert.True(n.Playable, $"{n.Name} is still marked coming soon"));
        Assert.Equal(new[] { "balanced", "serrated" }, nodes.Where(n => n.Tier == 1).Select(n => n.Id));
        Assert.All(nodes.Where(n => n.Tier == 1), n => Assert.Equal(5, n.MaxRanks));
        Assert.Equal(12, nodes.Count(n => n.Major));
        Assert.Equal(new[] { 1, 3, 6, 10, 15, 21, 28 }, tree.TierLevels);
        Assert.Equal(new[] { "Throw", "Bleed", "Hunt" }, tree.Lanes.Select(l => l.Name));

        // Nodes in a tier stand apart on the screen.
        foreach (var tier in nodes.GroupBy(n => n.Tier))
        {
            var xs = tier.Select(n => n.X).OrderBy(x => x).ToList();
            Assert.All(xs.Zip(xs.Skip(1)), p => Assert.True(p.Second - p.First >= 100f, $"tier {tier.Key} has nodes too close together"));
        }
    }

    [Fact]
    public void EachLane_EndsInACapstone()
    {
        foreach (var (lane, _) in ReaverTree.Tree.Lanes)
        {
            Assert.Single(ReaverTree.Tree.Nodes, n => n.Tier == 7 && n.Lane == lane && n.Major);
        }

        Assert.Equal(ReaverTree.Tree.Nodes.Where(n => n.Tier == 7).Select(n => n.Id).OrderBy(id => id),
            new[] { ReaverTree.AxeStorm, ReaverTree.CrimsonTide, ReaverTree.Harvester }.OrderBy(id => id));
    }

    [Fact]
    public void HemorrhageAndCatch_ComeEarly_AndTheWoundsGrowFromHemorrhage()
    {
        Assert.InRange(Node(ReaverTree.Hemorrhage).Tier, 2, 3);
        Assert.InRange(Node(ReaverTree.Catch).Tier, 2, 3);

        bool Below(string id) => id == ReaverTree.Hemorrhage || (Node(id).Parents.Count > 0 && Node(id).Parents.All(Below));
        Assert.All(ReaverTree.Tree.Nodes.Where(n => n.Lane == "Bleed" && n.Tier > 2), n => Assert.True(Below(n.Id), $"{n.Name} can be taken without Hemorrhage"));
    }

    [Fact]
    public void EveryNode_ChangesTheBonuses()
    {
        var options = new System.Text.Json.JsonSerializerOptions { IncludeFields = true };
        var none = System.Text.Json.JsonSerializer.Serialize(ReaverBonuses.From(new Dictionary<string, int>()), options);
        foreach (var node in ReaverTree.Tree.Nodes)
        {
            var one = System.Text.Json.JsonSerializer.Serialize(ReaverBonuses.From(new Dictionary<string, int> { [node.Id] = 1 }), options);
            Assert.True(one != none, $"{node.Name} does nothing");
        }
    }

    [Fact]
    public void TheRanks_AddUpIntoTheBonuses()
    {
        var b = ReaverBonuses.From(new Dictionary<string, int> { ["balanced"] = 5, ["keen"] = 2, ["serrated"] = 3, ["veins"] = 2, ["hide"] = 2, ["tireless"] = 1 });
        Assert.Equal(0.5f + 0.24f, b.ThrowDamage, 4);
        Assert.Equal(0.2f, b.ThrowRange, 4);
        Assert.Equal(0.36f, b.BleedDamage, 4);
        Assert.Equal(0.24f, b.CritDamage, 4);
        Assert.Equal(1f, b.BleedDuration, 4);
        Assert.Equal(20f + 15f, b.MaxHealth, 4);
        Assert.Equal(0.97f * 0.97f, b.DamageTaken, 4);

        var stats = ReaverTesting.With(("balanced", 5), ("keen", 2));
        Assert.Equal(WarriorStats.BaseThrowDamage * 1.74f, stats.ThrowDamage, 3);
        Assert.Equal(WarriorStats.BaseThrowRange * 1.2f, stats.ThrowRange, 3);
    }
}

public class ReaverAttackTests
{
    [Fact]
    public void AnAxe_BouncesOnFromTheEndOfItsThrow_ToEnemiesItHasNotHit_ThenComesBack()
    {
        var field = WarriorTesting.QuietField();
        var ahead = WarriorTesting.Sturdy(field, 0f, 5f);      // on the throw's line
        var left = WarriorTesting.Sturdy(field, -3f, 9f);      // off it, near its far end
        var right = WarriorTesting.Sturdy(field, -5f, 6f);     // off it, near the first bounce
        var far = WarriorTesting.Sturdy(field, 14f, 20f);      // out of any bounce's reach
        var stats = ReaverTesting.With();
        stats.Items = new ItemBonuses { CritChance = -1f };    // the two bounces every axe starts with
        Assert.Equal(WarriorStats.BaseAxeChains, stats.AxeChains);
        var throws = new ThrownAxes(new Random(1));

        throws.Throw(Vector3D<float>.Zero, 0f, stats, hand: 1);
        var hits = ReaverTesting.Run(throws, stats, field, ReaverTesting.RoundTrip(stats) + 1.5f);

        Assert.Contains(hits, h => h.Enemy == ahead);
        Assert.Contains(hits, h => h.Enemy == left);
        Assert.Contains(hits, h => h.Enemy == right);
        Assert.DoesNotContain(hits, h => h.Enemy == far);
        Assert.True(hits.FindIndex(h => h.Enemy == left) > hits.FindIndex(h => h.Enemy == ahead));   // the line first, then the bounces
        Assert.Empty(throws.Axes);
        Assert.True(throws.InHand(1));
    }

    [Fact]
    public void MoreAxesAThrow_FanOut_AndOnlyTheHandsOwnKeepsTheHandBusy()
    {
        var field = WarriorTesting.QuietField();
        var stats = ReaverTesting.With(("spare", 2));
        Assert.Equal(3, stats.AxesPerThrow);
        var throws = new ThrownAxes(new Random(1));

        throws.Throw(Vector3D<float>.Zero, 0f, stats, hand: 1);

        Assert.Equal(3, throws.Axes.Count);
        Assert.Equal(1, throws.Throws);
        Assert.Single(throws.Axes, a => a.Hand == 1);
        Assert.Equal(0f, throws.Axes.Single(a => a.Hand == 1).Heading.X, 4);   // the hand's own down the middle
        Assert.Contains(throws.Axes, a => a.Heading.X < -0.2f);
        Assert.Contains(throws.Axes, a => a.Heading.X > 0.2f);

        ReaverTesting.Run(throws, stats, field, ReaverTesting.RoundTrip(stats));
        Assert.Empty(throws.Axes);   // the spares are gone once back
        Assert.True(throws.InHand(1));
    }

    [Fact]
    public void ItemProjectilesAndChains_AreAxesAndBounces_NotDamage()
    {
        var plain = ReaverTesting.With();
        var stats = ReaverTesting.With();
        stats.Items = new ItemBonuses { CritChance = -1f, Projectiles = 2, Chains = 1 };

        Assert.Equal(WarriorStats.BaseAxes + 2, stats.AxesPerThrow);
        Assert.Equal(WarriorStats.BaseAxeChains + 1, stats.AxeChains);
        Assert.Equal(plain.ThrowDamage, stats.ThrowDamage, 4);
    }

    [Fact]
    public void TheTreeAndTheCards_AddAxesAndBounces()
    {
        var stats = ReaverTesting.With(("spare", 2), ("armful", 1), ("skipping", 2), (ReaverTree.RicochetAxes, 1));
        stats.Items = new ItemBonuses { CritChance = -1f };
        Assert.Equal(WarriorStats.BaseAxes + 3, stats.AxesPerThrow);
        Assert.Equal(WarriorStats.BaseAxeChains + 2 + WarriorStats.RicochetChains, stats.AxeChains);
        Assert.Equal(WarriorStats.RicochetRange, stats.AxeChainRange, 4);

        stats.Increase(WarriorUpgrade.ArmfulOfAxes);
        stats.Increase(WarriorUpgrade.SkippingAxes);
        Assert.Equal(WarriorStats.BaseAxes + 4, stats.AxesPerThrow);
        Assert.Equal(WarriorStats.BaseAxeChains + 3 + WarriorStats.RicochetChains, stats.AxeChains);

        Assert.True(WarriorUpgrades.Offered(WarriorUpgrade.ArmfulOfAxes, stats));
        Assert.True(WarriorUpgrades.Offered(WarriorUpgrade.SkippingAxes, stats));
        var berserker = new WarriorStats();
        Assert.False(WarriorUpgrades.Offered(WarriorUpgrade.ArmfulOfAxes, berserker));
        Assert.False(WarriorUpgrades.Offered(WarriorUpgrade.SkippingAxes, berserker));
    }

    [Fact]
    public void TheAttack_FollowsTheActiveTree()
    {
        var field = WarriorTesting.QuietField();
        WarriorTesting.Sturdy(field, 0f, 3f);

        var berserker = WarriorTesting.Warrior();
        WarriorTesting.Fight(berserker, field, new PlayerHealth(100f), CleaveAxes.FirstSwing + 0.1f);
        Assert.Equal(1, berserker.Axes.Swings);
        Assert.Empty(berserker.Throws.Axes);

        var reaver = ReaverTesting.Warrior();
        WarriorTesting.Fight(reaver, field, new PlayerHealth(100f), ThrownAxes.FirstThrow + 0.1f);
        Assert.Equal(0, reaver.Axes.Swings);
        Assert.Empty(reaver.Axes.Waves);
        Assert.Equal(1, reaver.Throws.Throws);
        Assert.Single(reaver.Throws.Axes);
    }

    [Fact]
    public void UseTree_TakesOnlyTheActiveTree()
    {
        var warrior = new WarriorClass(new Random(1));
        var ranks = new Dictionary<string, int> { [BerserkerTree.Berserking] = 1, ["mastery"] = 5, [ReaverTree.Hemorrhage] = 1, ["balanced"] = 5 };

        warrior.ChooseTree(ReaverTree.TreeId);
        warrior.UseTree(ranks);
        Assert.Same(ReaverTree.Tree, warrior.Tree);
        Assert.True(warrior.Stats.Throwing);
        Assert.True(warrior.Stats.Reaver.Hemorrhage);
        Assert.False(warrior.Stats.Tree.Berserking);
        Assert.Equal(0f, warrior.Stats.Tree.CleaveDamage);

        warrior.ChooseTree(BerserkerTree.TreeId);
        warrior.UseTree(ranks);
        Assert.False(warrior.Stats.Throwing);
        Assert.True(warrior.Stats.Tree.Berserking);
        Assert.False(warrior.Stats.Reaver.Hemorrhage);
        Assert.Equal(0f, warrior.Stats.Reaver.ThrowDamage);

        Assert.Equal(new[] { BerserkerTree.TreeId, ReaverTree.TreeId }, warrior.Trees.Select(t => t.Id));
        Assert.Contains("throws", warrior.Summary);
    }

    [Fact]
    public void TheReaversModels_AreInTheAssets()
    {
        string models = Path.Combine(CEngine.Core.EngineAssets.RepoRoot, "assets", "models");
        foreach (string file in new[] { ReaverView.AxeModel, ReaverView.PoolModel, ReaverView.DropModel, ReaverView.StormRingModel })
        {
            Assert.True(File.Exists(Path.Combine(models, file)), $"{file} is missing");
        }
    }

    [Fact]
    public void AReaver_ThrowsFurtherThanABerserkerSwings()
    {
        var reaver = ReaverTesting.With();
        Assert.Equal(WarriorStats.BaseThrowRange + WarriorStats.ThrowMargin, reaver.ThrowReach, 3);
        Assert.True(reaver.ThrowReach > WarriorTesting.With().Reach + 1f);
    }

    [Fact]
    public void AThrow_FliesOutItsRange_AndBack_HittingWhatItPasses_BothWays()
    {
        var field = WarriorTesting.QuietField();
        var ahead = WarriorTesting.Sturdy(field, 0f, 5f);
        var far = WarriorTesting.Sturdy(field, 0f, 9.5f);
        var aside = WarriorTesting.Sturdy(field, 3f, 5f);    // well off the path
        var beyond = WarriorTesting.Sturdy(field, 0f, 13f);  // past its range
        var stats = ReaverTesting.With();
        var throws = new ThrownAxes(new Random(1));

        throws.Throw(Vector3D<float>.Zero, 0f, stats, hand: 1);
        var hits = ReaverTesting.Run(throws, stats, field, ReaverTesting.RoundTrip(stats));

        Assert.Equal(2, hits.Count(h => h.Enemy == ahead));
        Assert.Equal(2, hits.Count(h => h.Enemy == far));
        Assert.DoesNotContain(hits, h => h.Enemy == aside || h.Enemy == beyond);
        Assert.All(hits, h => Assert.Equal(WarriorStats.BaseThrowDamage, h.Damage, 3));
        Assert.All(hits, h => Assert.Equal(CleaveSource.Throw, h.Source));
        Assert.Empty(throws.Axes);   // back in the hand
        Assert.True(throws.InHand(1));
    }

    [Fact]
    public void AThrownAxe_ComesBackToTheWarrior_WhereverHeGoes()
    {
        var field = WarriorTesting.QuietField();
        var stats = ReaverTesting.With();
        var throws = new ThrownAxes(new Random(1));
        throws.Throw(Vector3D<float>.Zero, 0f, stats, hand: 1);

        ReaverTesting.Run(throws, stats, field, 0.5f);
        Assert.True(throws.Axes[0].Returning);

        var moved = new Vector3D<float>(6f, 0f, -4f);
        ReaverTesting.Run(throws, stats, field, 0.3f, feet: moved);
        Assert.Single(throws.Axes);
        Geometry.FlatDirection(throws.Axes[0].Position, moved, out float before);
        ReaverTesting.Run(throws, stats, field, 1f, feet: moved);
        Assert.Empty(throws.Axes);
        Assert.True(before > 0f);
    }

    [Fact]
    public void TheAxes_TakeTurns_AndWithBothOut_TheNextThrowWaits()
    {
        var field = WarriorTesting.QuietField();
        WarriorTesting.Sturdy(field, 0f, 5f);
        var stats = ReaverTesting.With();
        stats.Items = new ItemBonuses { CritChance = -1f, AttackSpeed = 3f };   // a throw every 0.14 s: far quicker than an axe comes back
        var throws = new ThrownAxes(new Random(1));

        ReaverTesting.Run(throws, stats, field, ThrownAxes.FirstThrow + 0.05f, yaw: 0f);
        Assert.Equal(1, throws.Throws);
        Assert.False(throws.InHand(1));
        Assert.True(throws.InHand(-1));

        ReaverTesting.Run(throws, stats, field, stats.SwingInterval, yaw: 0f);
        Assert.Equal(2, throws.Throws);
        Assert.False(throws.InHand(-1));

        ReaverTesting.Run(throws, stats, field, 3f * stats.SwingInterval, yaw: 0f);
        Assert.Equal(2, throws.Throws);   // both in the air: waiting
        Assert.Equal(2, throws.Axes.Count);

        ReaverTesting.Run(throws, stats, field, ReaverTesting.RoundTrip(stats), yaw: 0f);
        Assert.True(throws.Throws > 2);   // one came back, and went again
        Assert.True(throws.Axes.Count <= 2);
    }

    [Fact]
    public void TheAxes_Wait_WithNothingInRange_ThenThrowAtOnce()
    {
        var field = WarriorTesting.QuietField();
        var stats = ReaverTesting.With();
        var throws = new ThrownAxes(new Random(1));
        ReaverTesting.Run(throws, stats, field, 2f, yaw: null);
        Assert.Equal(0, throws.Throws);
        Assert.Equal(0f, throws.ThrowIn);

        ReaverTesting.Run(throws, stats, field, ReaverTesting.Step, yaw: 0f);
        Assert.Equal(1, throws.Throws);
    }

    [Fact]
    public void AStun_HoldsTheThrows()
    {
        var field = WarriorTesting.QuietField();
        var stats = ReaverTesting.With();
        var throws = new ThrownAxes(new Random(1));
        var hits = new List<CleaveHit>();
        for (int i = 0; i < 120; i++)
        {
            throws.Update(ReaverTesting.Step, Vector3D<float>.Zero, 0f, stats, field, canThrow: false, charging: false, hits);
        }

        Assert.Equal(0, throws.Throws);
    }

    [Fact]
    public void TheRageNodes_DoNothing_WithTheReaverActive()
    {
        var reaver = ReaverTesting.Warrior();
        var field = WarriorTesting.QuietField();
        for (int i = 0; i < 5; i++)
        {
            WarriorTesting.Sturdy(field, i - 2f, 3f);
        }

        WarriorTesting.Fight(reaver, field, new PlayerHealth(100f), 3f);
        Assert.Equal(0, reaver.Fury.Rage);
        Assert.Equal(0f, reaver.Stats.RageBonus);
    }

    /// <summary>
    /// How the throw's damage was tuned: a fresh Reaver and a fresh Berserker, standing in the middle of a still crowd (enemies on a grid, too sturdy to die) for 20 s,
    /// turning now and then as the nearest enemy changes. The Cleave covers more of a crowd pressed close; the axes reach further, hit twice and bounce on. At 20 a
    /// throw and two bounces an axe, the Reaver deals about 70% (a thick crowd pressed close) to 135% (a looser, wider one) of what the Berserker does.
    /// </summary>
    [Theory]
    [InlineData(1.2f, 5f)]
    [InlineData(1.6f, 8f)]
    [InlineData(2.2f, 13f)]
    [InlineData(3f, 8f)]
    public void AFreshReaver_DealsAboutAsMuchIntoACrowd_AsAFreshBerserker(float spacing, float radius)
    {
        float berserker = Crowd(throwing: false, spacing, radius), reaver = Crowd(throwing: true, spacing, radius);
        Assert.InRange(reaver / berserker, 0.6f, 1.5f);
    }

    private static float Crowd(bool throwing, float spacing, float radius)
    {
        var field = WarriorTesting.QuietField();
        for (float x = -radius; x <= radius; x += spacing)
        {
            for (float z = -radius; z <= radius; z += spacing)
            {
                float d = MathF.Sqrt(x * x + z * z);
                if (d > 1.2f && d <= radius)
                {
                    field.Spawn(new Vector3D<float>(x, 0f, z)).Health = 1e9f;
                }
            }
        }

        var warrior = throwing ? ReaverTesting.Warrior() : WarriorTesting.Warrior();
        var health = new PlayerHealth(100f);
        int frame = 0;
        for (float t = 0f; t < 20f; t += WarriorTesting.Step, frame++)
        {
            warrior.Fight(WarriorTesting.Step, Vector3D<float>.Zero, frame / 30 * 0.7f, stunned: false, field, health, new DamageNumbers());
        }

        return field.DamageDealt;
    }
}

public class ReaverMajorTests
{
    [Fact]
    public void Hemorrhage_BleedsAShareOfTheHit_OverFourSeconds_StackingToFive()
    {
        var field = WarriorTesting.QuietField();
        var enemy = WarriorTesting.Sturdy(field, 0f, 3f);
        var stats = ReaverTesting.With((ReaverTree.Hemorrhage, 1));
        var throws = new ThrownAxes(new Random(1));
        var hits = new List<CleaveHit>();

        throws.Hurt(enemy, 100f, crit: false, CleaveSource.Throw, 1, stats, field, hits);
        Assert.True(throws.IsBleeding(enemy));
        var bled = ReaverTesting.Run(throws, stats, field, WarriorStats.BaseBleedSeconds + 0.5f).Where(h => h.Source == CleaveSource.Bleed).ToList();
        Assert.Equal(100f * WarriorStats.BleedShare, bled.Sum(h => h.Damage), 1);
        Assert.All(bled, h => Assert.False(h.Crit));
        Assert.False(throws.IsBleeding(enemy));

        for (int i = 0; i < 8; i++)
        {
            throws.Hurt(enemy, 100f, crit: false, CleaveSource.Throw, 1, stats, field, hits);
        }

        Assert.Equal(WarriorStats.BaseBleedStacks, throws.Wounds[enemy].Stacks.Count);

        var deep = ReaverTesting.With((ReaverTree.Hemorrhage, 1), (ReaverTree.DeepWounds, 1));
        for (int i = 0; i < 12; i++)
        {
            throws.Hurt(enemy, 100f, crit: false, CleaveSource.Throw, 1, deep, field, hits);
        }

        Assert.Equal(WarriorStats.DeepWoundsStacks, throws.Wounds[enemy].Stacks.Count);
    }

    [Fact]
    public void WithoutHemorrhage_AxeHitsDontBleed()
    {
        var field = WarriorTesting.QuietField();
        var enemy = WarriorTesting.Sturdy(field, 0f, 5f);
        var stats = ReaverTesting.With();
        var throws = new ThrownAxes(new Random(1));
        throws.Throw(Vector3D<float>.Zero, 0f, stats, hand: 1);
        ReaverTesting.Run(throws, stats, field, 0.4f);
        Assert.True(enemy.Health < 10_000f);
        Assert.False(throws.IsBleeding(enemy));

        var bleeding = ReaverTesting.With((ReaverTree.Hemorrhage, 1));
        throws.Throw(Vector3D<float>.Zero, 0f, bleeding, hand: -1);
        ReaverTesting.Run(throws, bleeding, field, 0.4f);
        Assert.True(throws.IsBleeding(enemy));
    }

    [Fact]
    public void Bleeding_TakesTheItemsDamageOverTime_AndShowsAsOverTimeNumbers()
    {
        var plain = ReaverTesting.With((ReaverTree.Hemorrhage, 1));
        var dotted = ReaverTesting.With((ReaverTree.Hemorrhage, 1));
        dotted.Items = new ItemBonuses { CritChance = -1f, DotDamage = 0.5f, DotMultiplier = 1.2f };
        Assert.Equal(plain.BleedScale * 1.5f * 1.2f, dotted.BleedScale, 4);

        var warrior = ReaverTesting.Warrior((ReaverTree.Hemorrhage, 1));
        var field = WarriorTesting.QuietField();
        WarriorTesting.Sturdy(field, 0f, 4f);
        var numbers = new DamageNumbers();
        for (float t = 0f; t < 3f; t += WarriorTesting.Step)
        {
            warrior.Fight(WarriorTesting.Step, Vector3D<float>.Zero, 0f, stunned: false, field, new PlayerHealth(100f), numbers);
            numbers.Update(WarriorTesting.Step);
        }

        Assert.Contains(numbers.Shown, n => n.OverTime);
        Assert.Contains(numbers.Shown, n => !n.OverTime);
    }

    [Fact]
    public void Catch_MakesTheNextThrowStronger()
    {
        var field = WarriorTesting.QuietField();
        var stats = ReaverTesting.With((ReaverTree.Catch, 1));
        var throws = new ThrownAxes(new Random(1));
        throws.Throw(Vector3D<float>.Zero, 0f, stats, hand: 1);
        Assert.False(throws.CatchReady);
        ReaverTesting.Run(throws, stats, field, ReaverTesting.RoundTrip(stats));
        Assert.True(throws.CatchReady);

        throws.Throw(Vector3D<float>.Zero, 0f, stats, hand: 1);
        Assert.Equal(stats.ThrowDamage * (1f + WarriorStats.CatchBonus), throws.Axes[0].Damage, 3);
        Assert.False(throws.CatchReady);

        throws.Throw(Vector3D<float>.Zero, 0f, stats, hand: -1);
        Assert.Equal(stats.ThrowDamage, throws.Axes[1].Damage, 3);   // spent

        var without = ReaverTesting.With();
        var plain = new ThrownAxes(new Random(1));
        plain.Throw(Vector3D<float>.Zero, 0f, without, hand: 1);
        ReaverTesting.Run(plain, without, field, ReaverTesting.RoundTrip(without));
        Assert.False(plain.CatchReady);
    }

    [Fact]
    public void RicochetAxes_BounceOnToMoreEnemies_ThenTurnBack()
    {
        var field = WarriorTesting.QuietField();
        var first = WarriorTesting.Sturdy(field, 0f, 4f);
        var side = WarriorTesting.Sturdy(field, 3.5f, 5f);   // off the path, within Ricochet's reach of the throw's far end
        var stats = ReaverTesting.With((ReaverTree.RicochetAxes, 1));
        var throws = new ThrownAxes(new Random(1));
        throws.Throw(Vector3D<float>.Zero, 0f, stats, hand: 1);
        var hits = ReaverTesting.Run(throws, stats, field, ReaverTesting.RoundTrip(stats));

        Assert.Contains(hits, h => h.Enemy == first);
        Assert.Contains(hits, h => h.Enemy == side);
        Assert.True(hits.FindIndex(h => h.Enemy == side) > hits.FindIndex(h => h.Enemy == first));
        Assert.Empty(throws.Axes);   // turned back after the bounce, and caught

        var plainThrows = new ThrownAxes(new Random(1));
        var plain = ReaverTesting.With();
        plainThrows.Throw(Vector3D<float>.Zero, 0f, plain, hand: 1);
        Assert.DoesNotContain(ReaverTesting.Run(plainThrows, plain, field, ReaverTesting.RoundTrip(plain)), h => h.Enemy == side);
    }

    [Fact]
    public void ReturningEdge_HitsHarderOnTheWayBack()
    {
        var field = WarriorTesting.QuietField();
        var enemy = WarriorTesting.Sturdy(field, 0f, 5f);
        var stats = ReaverTesting.With((ReaverTree.ReturningEdge, 1));
        var throws = new ThrownAxes(new Random(1));
        throws.Throw(Vector3D<float>.Zero, 0f, stats, hand: 1);
        var hits = ReaverTesting.Run(throws, stats, field, ReaverTesting.RoundTrip(stats)).Where(h => h.Enemy == enemy).ToList();

        Assert.Equal(2, hits.Count);
        Assert.Equal(stats.ThrowDamage, hits[0].Damage, 3);
        Assert.Equal(stats.ThrowDamage * (1f + WarriorStats.ReturnDamage), hits[1].Damage, 3);
    }

    [Fact]
    public void ClosingIn_HitsNearEnemiesHarder()
    {
        var field = WarriorTesting.QuietField();
        var near = WarriorTesting.Sturdy(field, 0f, 2.5f);
        var far = WarriorTesting.Sturdy(field, 0f, 8f);
        var stats = ReaverTesting.With((ReaverTree.ClosingIn, 1));
        var throws = new ThrownAxes(new Random(1));
        throws.Throw(Vector3D<float>.Zero, 0f, stats, hand: 1);
        var hits = ReaverTesting.Run(throws, stats, field, ReaverTesting.RoundTrip(stats));

        Assert.All(hits.Where(h => h.Enemy == near), h => Assert.Equal(stats.ThrowDamage * (1f + WarriorStats.CloseDamage), h.Damage, 3));
        Assert.All(hits.Where(h => h.Enemy == far), h => Assert.Equal(stats.ThrowDamage, h.Damage, 3));
        Assert.Equal(4, hits.Count);
    }

    [Fact]
    public void CrimsonTide_BleedingEnemiesTakeMoreFromTheAxes()
    {
        var field = WarriorTesting.QuietField();
        var bleeding = WarriorTesting.Sturdy(field, -0.3f, 5f);
        var clean = WarriorTesting.Sturdy(field, 0.3f, 7f);
        var stats = ReaverTesting.With((ReaverTree.CrimsonTide, 1));
        var throws = new ThrownAxes(new Random(1));
        throws.Bleed(bleeding, 1f, 1, stats);
        throws.Throw(Vector3D<float>.Zero, 0f, stats, hand: 1);
        var hits = ReaverTesting.Run(throws, stats, field, 0.45f).Where(h => h.Source == CleaveSource.Throw).ToList();

        Assert.Equal(stats.ThrowDamage * (1f + WarriorStats.CrimsonDamage), hits.Single(h => h.Enemy == bleeding).Damage, 3);
        Assert.Equal(stats.ThrowDamage, hits.Single(h => h.Enemy == clean).Damage, 3);
    }

    [Fact]
    public void Bloodbath_ABleedingEnemyThatDies_LeavesAPoolThatHeals()
    {
        var warrior = ReaverTesting.Warrior((ReaverTree.Hemorrhage, 1), (ReaverTree.Bloodbath, 1));
        var field = WarriorTesting.QuietField();
        var enemy = field.Spawn(new Vector3D<float>(0f, 0f, 1.5f));
        enemy.Health = 1e6f;
        warrior.Throws.Bleed(enemy, 1f, 1, warrior.Stats);
        enemy.Health = 0f;   // dies (of anything) while bleeding

        var health = new PlayerHealth(100f);
        health.TakeDamage(50f);
        warrior.Fight(WarriorTesting.Step, Vector3D<float>.Zero, null, stunned: false, field, health, new DamageNumbers());
        Assert.Single(warrior.Throws.Pools);
        Assert.True(warrior.Throws.InPool(Vector3D<float>.Zero));

        float before = health.Current;
        for (int frame = 0; frame < 60; frame++)
        {
            warrior.Fight(WarriorTesting.Step, Vector3D<float>.Zero, null, stunned: false, field, health, new DamageNumbers());
        }

        Assert.InRange(health.Current - before, 100f * WarriorStats.PoolHeal * 0.95f, 100f * WarriorStats.PoolHeal * 1.05f);   // 2% of max health a second
        Assert.False(warrior.Throws.InPool(new Vector3D<float>(0f, 0f, 5f)));

        for (float t = 0f; t < WarriorStats.PoolSeconds; t += WarriorTesting.Step)
        {
            warrior.Fight(WarriorTesting.Step, Vector3D<float>.Zero, null, stunned: false, field, health, new DamageNumbers());
        }

        Assert.Empty(warrior.Throws.Pools);

        var without = new ThrownAxes(new Random(1));
        var stats = ReaverTesting.With((ReaverTree.Hemorrhage, 1));
        var other = WarriorTesting.Sturdy(field, 2f, 2f);
        without.Bleed(other, 1f, 1, stats);
        other.Health = 0f;
        ReaverTesting.Run(without, stats, field, 0.1f);
        Assert.Empty(without.Pools);
    }

    [Fact]
    public void RunThemDown_TheChargeHitsWhatItPasses_OnceACharge_LeavingItBleedingAtFullStacks()
    {
        var field = WarriorTesting.QuietField();
        var enemy = WarriorTesting.Sturdy(field, 0f, 1f);
        var stats = ReaverTesting.With((ReaverTree.RunThemDown, 1));
        var throws = new ThrownAxes(new Random(1));

        var hits = ReaverTesting.Run(throws, stats, field, 0.2f, charging: true);
        var charged = hits.Where(h => h.Source == CleaveSource.Charge).ToList();
        Assert.Single(charged);
        Assert.Equal(stats.ThrowDamage, charged[0].Damage, 3);
        Assert.Equal(WarriorStats.BaseBleedStacks, throws.Wounds[enemy].Stacks.Count);   // even without Hemorrhage

        ReaverTesting.Run(throws, stats, field, 0.2f);   // the charge ends
        hits = ReaverTesting.Run(throws, stats, field, 0.2f, charging: true);
        Assert.Single(hits, h => h.Source == CleaveSource.Charge);   // a new charge hits it again

        var plain = ReaverTesting.With();
        var plainThrows = new ThrownAxes(new Random(1));
        Assert.DoesNotContain(ReaverTesting.Run(plainThrows, plain, field, 0.2f, charging: true), h => h.Source == CleaveSource.Charge);
    }

    [Fact]
    public void BloodScent_SpeedsTheWarrior_ForEveryBleedingEnemyNear()
    {
        var warrior = ReaverTesting.Warrior((ReaverTree.BloodScent, 1));
        var field = WarriorTesting.QuietField();
        float speed = warrior.Stats.MoveSpeed, cooldown = warrior.Stats.ChargeCooldown;
        for (int i = 0; i < 4; i++)
        {
            warrior.Throws.Bleed(WarriorTesting.Sturdy(field, 20f + i, 20f), 1f, 1, warrior.Stats);   // too far
        }

        for (int i = 0; i < 4; i++)
        {
            warrior.Throws.Bleed(WarriorTesting.Sturdy(field, i - 2f, 6f), 1f, 1, warrior.Stats);
        }

        warrior.Fight(WarriorTesting.Step, Vector3D<float>.Zero, null, stunned: false, field, new PlayerHealth(100f), new DamageNumbers());
        Assert.Equal(4f * WarriorStats.ScentPer, warrior.Stats.Scent, 4);
        Assert.Equal(speed * (1f + 4f * WarriorStats.ScentPer), warrior.Stats.MoveSpeed, 3);
        Assert.True(warrior.Stats.ChargeCooldown < cooldown);

        for (int i = 0; i < 20; i++)
        {
            warrior.Throws.Bleed(WarriorTesting.Sturdy(field, i * 0.4f - 4f, -5f), 1f, 1, warrior.Stats);
        }

        warrior.Fight(WarriorTesting.Step, Vector3D<float>.Zero, null, stunned: false, field, new PlayerHealth(100f), new DamageNumbers());
        Assert.Equal(WarriorStats.ScentCap, warrior.Stats.Scent, 4);

        var plain = ReaverTesting.Warrior();
        plain.Throws.Bleed(WarriorTesting.Sturdy(field, 0f, 3f), 1f, 1, plain.Stats);
        plain.Fight(WarriorTesting.Step, Vector3D<float>.Zero, null, stunned: false, field, new PlayerHealth(100f), new DamageNumbers());
        Assert.Equal(0f, plain.Stats.Scent);
    }

    [Fact]
    public void AxeStorm_EveryTwelveSeconds_TheAxesCircle_HittingTwiceASecond_WithNoThrows()
    {
        var field = WarriorTesting.QuietField();
        var ring = WarriorTesting.Sturdy(field, WarriorStats.StormRadius, 0f);
        var stats = ReaverTesting.With((ReaverTree.AxeStorm, 1));
        var throws = new ThrownAxes(new Random(1));

        ReaverTesting.Run(throws, stats, field, WarriorStats.StormEvery - 0.1f, yaw: MathF.PI);   // throwing the other way meanwhile
        Assert.False(throws.Storming);
        int thrown = throws.Throws;

        // Due: the axes come back, and the storm starts.
        int waited = 0;
        while (!throws.Storming && waited++ < 120)
        {
            ReaverTesting.Run(throws, stats, field, ReaverTesting.Step, yaw: MathF.PI);
        }

        Assert.True(throws.Storming);
        Assert.Empty(throws.Axes);
        int beforeStorm = throws.Throws;

        var hits = new List<CleaveHit>();
        int frames = 0;
        while (throws.Storming)
        {
            hits.AddRange(ReaverTesting.Run(throws, stats, field, ReaverTesting.Step, yaw: MathF.PI));
            Assert.True(!throws.Storming || throws.Throws == beforeStorm, "a throw while the axes circle");
            frames++;
        }

        Assert.InRange(frames * ReaverTesting.Step, WarriorStats.StormSeconds - 0.05f, WarriorStats.StormSeconds + 0.05f);
        Assert.True(thrown > 10);
        int passes = hits.Count(h => h.Enemy == ring);
        Assert.InRange(passes, 7, 9);   // twice a second for 4 s
        Assert.All(hits.Where(h => h.Enemy == ring), h => Assert.Equal(stats.ThrowDamage * WarriorStats.StormDamage, h.Damage, 3));
        Assert.False(throws.Storming);
        Assert.InRange(throws.StormIn, WarriorStats.StormEvery - 0.5f, WarriorStats.StormEvery);

        ReaverTesting.Run(throws, stats, field, 1f, yaw: MathF.PI);
        Assert.True(throws.Throws > beforeStorm);   // throwing again
    }

    [Fact]
    public void Harvester_EveryFifthKill_ThrowsAFreeAxe()
    {
        var warrior = ReaverTesting.Warrior((ReaverTree.Harvester, 1));
        var field = WarriorTesting.QuietField();
        WarriorTesting.Sturdy(field, 0f, 20f);
        for (int i = 0; i < 4; i++)
        {
            warrior.OnKill(field.Spawn(new Vector3D<float>(50f, 0f, 50f)), 0f);
        }

        Assert.Equal(0, warrior.Throws.FreeThrowsDue);
        warrior.OnKill(field.Spawn(new Vector3D<float>(50f, 0f, 50f)), 0f);
        Assert.Equal(1, warrior.Throws.FreeThrowsDue);

        warrior.Fight(WarriorTesting.Step, Vector3D<float>.Zero, 0f, stunned: false, field, new PlayerHealth(100f), new DamageNumbers());
        Assert.Equal(0, warrior.Throws.FreeThrowsDue);
        Assert.Contains(warrior.Throws.Axes, a => a.Hand == 0);
        Assert.Equal(0, warrior.Throws.Throws);   // the hand's throws aren't touched
        Assert.True(warrior.Throws.InHand(1) && warrior.Throws.InHand(-1));

        var plain = ReaverTesting.Warrior();
        for (int i = 0; i < 10; i++)
        {
            plain.OnKill(field.Spawn(new Vector3D<float>(50f, 0f, 50f)), 0f);
        }

        Assert.Equal(0, plain.Throws.FreeThrowsDue);
    }
}

public class ReaverCardTests
{
    private static HashSet<WarriorUpgrade> Offered(WarriorStats stats)
    {
        var random = new Random(2);
        return Enumerable.Range(0, 300).SelectMany(_ => WarriorUpgrades.Roll(stats, random)).Where(c => c.Upgrade is not null).Select(c => c.Upgrade!.Value).ToHashSet();
    }

    private static readonly WarriorUpgrade[] BerserkerOnly = { WarriorUpgrade.WideCleave, WarriorUpgrade.LongAxes, WarriorUpgrade.DeepFury, WarriorUpgrade.LastingRage };
    private static readonly WarriorUpgrade[] ReaverOnly =
        { WarriorUpgrade.FarThrow, WarriorUpgrade.SpinningAxes, WarriorUpgrade.BroadBlades, WarriorUpgrade.CruelEdges, WarriorUpgrade.LastingWounds, WarriorUpgrade.SureCatch };

    [Fact]
    public void TheBerserkersCards_ComeUpOnlyWithTheBerserker()
    {
        var berserker = Offered(WarriorTesting.With((BerserkerTree.Berserking, 1)));
        Assert.All(BerserkerOnly, u => Assert.Contains(u, berserker));
        Assert.All(ReaverOnly, u => Assert.DoesNotContain(u, berserker));

        var reaver = Offered(ReaverTesting.With((ReaverTree.Hemorrhage, 1), (ReaverTree.Catch, 1)));
        Assert.All(BerserkerOnly, u => Assert.DoesNotContain(u, reaver));
        Assert.All(ReaverOnly, u => Assert.Contains(u, reaver));

        // The body's and the attack's cards come up with either.
        foreach (var shared in new[] { WarriorUpgrade.BrutalSwings, WarriorUpgrade.QuickHands, WarriorUpgrade.Hardy, WarriorUpgrade.SavageEye, WarriorUpgrade.Plunderer })
        {
            Assert.Contains(shared, berserker);
            Assert.Contains(shared, reaver);
        }
    }

    [Fact]
    public void TheBleedCards_WaitForBleeding_AndSureCatch_ForCatch()
    {
        var fresh = Offered(ReaverTesting.With());
        Assert.DoesNotContain(WarriorUpgrade.CruelEdges, fresh);
        Assert.DoesNotContain(WarriorUpgrade.LastingWounds, fresh);
        Assert.DoesNotContain(WarriorUpgrade.SureCatch, fresh);
        Assert.Contains(WarriorUpgrade.FarThrow, fresh);

        Assert.Contains(WarriorUpgrade.CruelEdges, Offered(ReaverTesting.With((ReaverTree.RunThemDown, 1))));
        Assert.Contains(WarriorUpgrade.SureCatch, Offered(ReaverTesting.With((ReaverTree.Catch, 1))));
    }

    [Fact]
    public void TheCards_ReadRightForTheActiveTree()
    {
        var random = new Random(4);
        var reaver = ReaverTesting.With();
        var cards = Enumerable.Range(0, 200).SelectMany(_ => WarriorUpgrades.Roll(reaver, random)).ToList();
        Assert.DoesNotContain(cards, c => c.Name.Contains("Cleave") || c.Description.Contains("Cleave"));
        Assert.Contains(cards, c => c.Name == "Brutal Throws" && c.Description == "+20% throw damage");

        var berserker = WarriorTesting.With();
        Assert.Contains(Enumerable.Range(0, 200).SelectMany(_ => WarriorUpgrades.Roll(berserker, random)), c => c.Name == "Brutal Swings");
    }

    [Fact]
    public void TheReaverCards_ChangeTheNumbers()
    {
        var stats = ReaverTesting.With((ReaverTree.Catch, 1));
        float range = stats.ThrowRange, speed = stats.AxeSpeed, radius = stats.AxeRadius, bleed = stats.BleedScale, seconds = stats.BleedSeconds, caught = stats.CatchMultiplier;
        foreach (var upgrade in ReaverOnly)
        {
            stats.Increase(upgrade);
        }

        Assert.Equal(range * 1.1f, stats.ThrowRange, 3);
        Assert.Equal(speed * 1.15f, stats.AxeSpeed, 3);
        Assert.Equal(radius * 1.15f, stats.AxeRadius, 3);
        Assert.Equal(bleed + 0.2f, stats.BleedScale, 3);
        Assert.Equal(seconds + 1f, stats.BleedSeconds, 3);
        Assert.Equal(caught + 0.25f, stats.CatchMultiplier, 3);
    }
}
