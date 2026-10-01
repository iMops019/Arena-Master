using ArenaMaster.Game.Combat;
using ArenaMaster.Game.Items;
using ArenaMaster.Game.Progression;
using ArenaMaster.Game.Ranger;
using Silk.NET.Maths;

namespace ArenaMaster.Game.Tests;

internal static class TrapperTesting
{
    public const float Step = 1f / 60f;

    public static float? FlatGround(float x, float z) => MathF.Abs(x) < 200f && MathF.Abs(z) < 200f ? 0f : null;

    public static EnemyField QuietField() => new(new Random(1)) { TargetCount = 0 };

    /// <summary>The Trapper active with these ranks, and no crits, for exact numbers.</summary>
    public static RangerStats With(params (string Id, int Ranks)[] ranks) =>
        new()
        {
            Trapper = TrapperBonuses.From(ranks.ToDictionary(r => r.Id, r => r.Ranks)), TrapperActive = true, Items = new ItemBonuses { CritChance = -1f },
        };

    public static TrapperKit Kit(RangerStats stats) => new(new Random(1), stats);

    /// <summary>A ghoul (or <paramref name="kind"/>) that won't die of a hit, standing at (<paramref name="x"/>, <paramref name="z"/>).</summary>
    public static Enemy Sturdy(EnemyField field, float x, float z, EnemyKind? kind = null)
    {
        var enemy = field.Spawn(new Vector3D<float>(x, 0f, z), kind);
        enemy.Health = 10_000f;
        return enemy;
    }

    /// <summary>Runs <paramref name="kit"/> with the Ranger at the origin for <paramref name="seconds"/>.</summary>
    public static List<TrapperHit> Run(TrapperKit kit, EnemyField field, float seconds, bool canAct = true)
    {
        var hits = new List<TrapperHit>();
        for (float t = 0f; t < seconds - 1e-4f; t += Step)
        {
            kit.Update(Step, Vector3D<float>.Zero, field, canAct, hits);
        }

        return hits;
    }

    public static float Lost(Enemy enemy) => 10_000f - enemy.Health;
}

public class TrapperTreeTests
{
    [Fact]
    public void TheTree_IsWellFormed()
    {
        var tree = TrapperTree.Tree;
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
        Assert.Equal(new[] { "Snares", "Venom", "Wilds" }, tree.Lanes.Select(l => l.Name));
        Assert.Equal(new[] { 1, 3, 6, 10, 15, 21, 28 }, tree.TierLevels);

        var starting = nodes.Where(n => n.Tier == 1).ToList();
        Assert.Equal(new[] { "Trap Craft", "Tipped Arrows" }, starting.Select(n => n.Name));
        Assert.All(starting, n => Assert.Equal(5, n.MaxRanks));
        Assert.Equal(11, nodes.Count(n => n.Major));   // the Sharpshooter's nine, give or take two
    }

    [Fact]
    public void NodesInATier_DontOverlap()
    {
        foreach (var tier in TrapperTree.Tree.Nodes.GroupBy(n => n.Tier))
        {
            var xs = tier.Select(n => n.X).OrderBy(x => x).ToList();
            for (int i = 1; i < xs.Count; i++)
            {
                Assert.True(xs[i] - xs[i - 1] >= 100f, $"tier {tier.Key}: nodes at {xs[i - 1]} and {xs[i]} are too close");
            }
        }
    }

    [Fact]
    public void EachLane_GetsGoingEarly_AndEndsInACapstone()
    {
        var tree = TrapperTree.Tree;
        foreach (var (id, lane) in new[] { (TrapperTree.SnareLine, "Snares"), (TrapperTree.VenomTips, "Venom"), (TrapperTree.HawkCompanion, "Wilds") })
        {
            var node = tree.Node(id);
            Assert.True(node.Major);
            Assert.Equal(lane, node.Lane);
            Assert.InRange(node.Tier, 2, 3);
        }

        var capstones = tree.Nodes.Where(n => n.Tier == 7).ToList();
        Assert.All(capstones, n => Assert.True(n.Major));
        Assert.Equal(tree.Lanes.Select(l => l.Name).OrderBy(n => n), capstones.Select(n => n.Lane).OrderBy(n => n));
        Assert.Equal(new[] { "Killing Field", "Blight Arrows", "Apex Predator" }, capstones.Select(n => n.Name));
    }

    [Fact]
    public void Ranks_AddUpIntoTheBonuses()
    {
        var bonuses = TrapperBonuses.From(new Dictionary<string, int>
        {
            ["trapcraft"] = 3, ["tipped"] = 2, ["spares"] = 2, [TrapperTree.VenomTips] = 1, ["instinct"] = 2, ["wildheart"] = 1,
        });
        Assert.Equal(0.36f, bonuses.TrapDamage, 4);
        Assert.Equal(0.18f, bonuses.DashRecharge, 4);
        Assert.Equal(0.2f, bonuses.PoisonDamage, 4);
        Assert.Equal(0.16f, bonuses.Damage, 4);
        Assert.Equal(2, bonuses.Snares);
        Assert.Equal(0.4f, bonuses.SnareTime, 4);
        Assert.Equal(40f, bonuses.MaxHealth, 3);
        Assert.Equal(0.2f, bonuses.Regeneration, 4);
        Assert.Equal(0.95f * 0.95f, bonuses.DamageTaken, 4);
        Assert.True(bonuses.VenomTips);
        Assert.False(bonuses.SnareLine);
        Assert.False(bonuses.BlightArrows);
    }

    [Fact]
    public void MinorNodes_AddToTheRangersNumbers()
    {
        var stats = TrapperTesting.With(("tipped", 5), ("trapcraft", 5), ("fletching", 5), ("lightfeet", 5), ("stride", 3), ("keensight", 2));
        Assert.Equal(RangerStats.BaseDamage * 1.4f, stats.Damage, 3);
        Assert.Equal(RangerStats.BaseFireInterval / 1.3f, stats.FireInterval, 4);
        Assert.Equal(RangerStats.BaseDashCooldown / (1f + 0.30f + 0.24f), stats.DashCooldown, 4);
        Assert.Equal(RangerStats.BaseDashSpeed * 1.3f, stats.DashSpeed, 3);
        Assert.Equal(RangerStats.BaseMoveSpeed * 1.2f, stats.MoveSpeed, 3);
        Assert.Equal(RangerStats.BasePickupRadius * 1.2f, stats.PickupRadius, 3);
        Assert.Equal(RangerStats.BaseHawkRange * 1.3f, stats.HawkRange, 3);
        Assert.Equal(stats.Damage * RangerStats.SnareBurst * 1.6f, stats.SnareDamage, 3);
        Assert.Equal(1.5f, stats.PoisonScale, 4);
    }

    [Fact]
    public void Poison_TakesTheItemsDamageOverTime()
    {
        var stats = TrapperTesting.With();
        stats.Items = new ItemBonuses { DotDamage = 0.5f, DotMultiplier = 1.2f };
        Assert.Equal(1.5f * 1.2f, stats.PoisonScale, 4);
        Assert.Equal(stats.SnareDamage * RangerStats.CaltropShare * 1.8f, stats.CaltropDps, 3);
    }
}

public class TrapperClassTests
{
    private static RangerClass Ranger(string treeId, params (string Id, int Ranks)[] ranks)
    {
        var ranger = new RangerClass(new Random(3));
        ranger.ChooseTree(treeId);
        ranger.UseTree(ranks.ToDictionary(r => r.Id, r => r.Ranks));
        return ranger;
    }

    [Fact]
    public void TheTrapper_IsTheRangersSecondTree()
    {
        var ranger = new RangerClass(new Random(3));
        Assert.Equal(new[] { SharpshooterTree.TreeId, TrapperTree.TreeId }, ranger.Trees.Select(t => t.Id));
        Assert.Equal("Trapper", ranger.Trees[1].Name);
        Assert.Same(SharpshooterTree.Tree, ranger.Tree);

        ranger.ChooseTree(TrapperTree.TreeId);
        Assert.Same(TrapperTree.Tree, ranger.Tree);
        ranger.ChooseTree("nonsense");
        Assert.Same(SharpshooterTree.Tree, ranger.Tree);
    }

    [Fact]
    public void OnlyTheActiveTree_Counts()
    {
        var trapper = Ranger(TrapperTree.TreeId, ("tipped", 5), (TrapperTree.VenomTips, 1), ("steady", 5));
        Assert.True(trapper.Stats.TrapperActive);
        Assert.True(trapper.Stats.Trapper.VenomTips);
        Assert.Equal(0.4f, trapper.Stats.Trapper.Damage, 4);
        Assert.Equal(0f, trapper.Stats.Tree.CritChance);   // a Sharpshooter node's id among the ranks counts for nothing

        var sharpshooter = Ranger(SharpshooterTree.TreeId, ("honed", 5), (TrapperTree.VenomTips, 1));
        Assert.False(sharpshooter.Stats.TrapperActive);
        Assert.False(sharpshooter.Stats.Trapper.VenomTips);
        Assert.Equal(0f, sharpshooter.Stats.Trapper.Damage);
        Assert.Equal(0.5f, sharpshooter.Stats.Tree.Damage, 4);
    }

    [Fact]
    public void WithTheSharpshooterActive_TheKitDoesNothing()
    {
        var ranger = Ranger(SharpshooterTree.TreeId);
        var field = TrapperTesting.QuietField();
        var ghoul = TrapperTesting.Sturdy(field, 0.2f, 0f);
        ranger.Kit.LaySnare(Vector3D<float>.Zero);
        ranger.Kit.ArrowHit(ghoul, 20f);
        TrapperTesting.Run(ranger.Kit, field, 5f);

        Assert.Empty(ranger.Kit.Snares);
        Assert.False(ranger.Kit.IsPoisoned(ghoul));
        Assert.Null(ranger.Kit.Hawk);
        Assert.Equal(10_000f, ghoul.Health);
    }
}

public class TrapperCardTests
{
    private static readonly RangerUpgrade[] TrapperCards =
    {
        RangerUpgrade.TrapSetter, RangerUpgrade.HeavySnares, RangerUpgrade.StrongVenom, RangerUpgrade.SlowPoison, RangerUpgrade.SharpTalons, RangerUpgrade.HawkTraining,
    };

    private static HashSet<RangerUpgrade> Offered(RangerStats stats) => RangerUpgrades.All.Where(u => RangerUpgrades.Offered(u.Upgrade, stats)).Select(u => u.Upgrade).ToHashSet();

    [Fact]
    public void WithTheSharpshooter_NoneOfTheTrappersCards_AreOffered()
    {
        var offered = Offered(new RangerStats());
        Assert.Equal(9, offered.Count);
        Assert.DoesNotContain(offered, TrapperCards.Contains);

        for (int seed = 0; seed < 40; seed++)
        {
            Assert.DoesNotContain(RangerUpgrades.Roll(new RangerStats(), new Random(seed)), c => c.Upgrade is { } u && TrapperCards.Contains(u));
        }
    }

    [Fact]
    public void WithTheTrapper_TheBowsCardsStay_AndItsOwnComeWithTheirMajors()
    {
        var bare = Offered(TrapperTesting.With());
        Assert.Equal(10, bare.Count);   // the nine, and Trap Setter
        Assert.Contains(RangerUpgrade.TrapSetter, bare);

        var snares = Offered(TrapperTesting.With((TrapperTree.SnareLine, 1)));
        Assert.Contains(RangerUpgrade.HeavySnares, snares);
        Assert.DoesNotContain(RangerUpgrade.StrongVenom, snares);

        var venom = Offered(TrapperTesting.With((TrapperTree.VenomTips, 1)));
        Assert.Contains(RangerUpgrade.StrongVenom, venom);
        Assert.Contains(RangerUpgrade.SlowPoison, venom);
        Assert.DoesNotContain(RangerUpgrade.SharpTalons, venom);

        var hawk = Offered(TrapperTesting.With((TrapperTree.HawkCompanion, 1)));
        Assert.Contains(RangerUpgrade.SharpTalons, hawk);
        Assert.Contains(RangerUpgrade.HawkTraining, hawk);
        Assert.DoesNotContain(RangerUpgrade.HeavySnares, hawk);
    }

    [Fact]
    public void TheTrappersCards_ChangeTheNumbersTheyName()
    {
        var stats = TrapperTesting.With((TrapperTree.SnareLine, 1), (TrapperTree.VenomTips, 1), (TrapperTree.HawkCompanion, 1));
        float snare = stats.SnareDamage, hawk = stats.HawkDamage, poison = stats.PoisonTime, dash = stats.DashCooldown;
        stats.Increase(RangerUpgrade.HeavySnares);
        stats.Increase(RangerUpgrade.StrongVenom);
        stats.Increase(RangerUpgrade.SlowPoison);
        stats.Increase(RangerUpgrade.SharpTalons);
        stats.Increase(RangerUpgrade.HawkTraining);
        stats.Increase(RangerUpgrade.TrapSetter);

        Assert.Equal(snare * 1.25f, stats.SnareDamage, 3);
        Assert.Equal(hawk * 1.2f, stats.HawkDamage, 3);
        Assert.Equal(poison + 0.5f, stats.PoisonTime, 4);
        Assert.Equal(1.2f, stats.PoisonScale, 4);
        Assert.Equal(RangerStats.HawkInterval / 1.1f, stats.HawkDiveInterval, 4);
        Assert.Equal(RangerStats.BaseDashCooldown / 1.1f, stats.DashCooldown, 4);
        Assert.True(stats.DashCooldown < dash);
    }
}

public class SnareTests
{
    [Fact]
    public void WithoutSnareLine_ADashSetsNothing()
    {
        var kit = TrapperTesting.Kit(TrapperTesting.With());
        kit.LaySnare(Vector3D<float>.Zero);
        Assert.Empty(kit.Snares);
    }

    [Fact]
    public void ASnare_HoldsWhatStepsOnIt_ThenBursts()
    {
        var stats = TrapperTesting.With((TrapperTree.SnareLine, 1));
        var kit = TrapperTesting.Kit(stats);
        var field = TrapperTesting.QuietField();
        kit.LaySnare(Vector3D<float>.Zero);
        var stepper = TrapperTesting.Sturdy(field, 0.3f, 0f);
        var near = TrapperTesting.Sturdy(field, 2.2f, 0f);
        var far = TrapperTesting.Sturdy(field, 4f, 0f);

        TrapperTesting.Run(kit, field, 1.4f);
        Assert.True(kit.Snares.Single().Sprung);
        Assert.Same(stepper, kit.Snares.Single().Holding);
        Assert.True(stepper.IsFrozen);
        Assert.Equal(RangerStats.SnareHold, stepper.FrozenFor, 3);   // (the field isn't running, so the hold isn't counting down on it)
        Assert.True(kit.IsHeld(stepper));
        Assert.Equal(10_000f, stepper.Health);

        var hits = TrapperTesting.Run(kit, field, 0.2f);
        Assert.Empty(kit.Snares);
        Assert.Equal(stats.SnareDamage, TrapperTesting.Lost(stepper), 2);
        Assert.Equal(RangerStats.BaseDamage * 2.5f, stats.SnareDamage, 3);
        Assert.Equal(stats.SnareDamage, TrapperTesting.Lost(near), 2);
        Assert.Equal(10_000f, far.Health);
        Assert.All(hits, h => Assert.Equal(TrapperSource.Snare, h.Source));
        Assert.Single(kit.Bursts);
    }

    [Fact]
    public void AnElite_IsHeldHalfAsLong_AndABoss_NotAtAll()
    {
        var stats = TrapperTesting.With((TrapperTree.SnareLine, 1));
        var kit = TrapperTesting.Kit(stats);
        var field = TrapperTesting.QuietField();
        kit.LaySnare(Vector3D<float>.Zero);
        var brute = TrapperTesting.Sturdy(field, 0.3f, 0f, EnemyKind.Brute);
        TrapperTesting.Run(kit, field, 0.7f);
        Assert.Equal(10_000f, brute.Health);
        TrapperTesting.Run(kit, field, 0.1f);
        Assert.True(brute.Health < 10_000f);
        Assert.Equal(RangerStats.SnareHold * RangerStats.EliteHoldShare, brute.FrozenFor, 3);

        var bossField = TrapperTesting.QuietField();
        var bossKit = TrapperTesting.Kit(stats);
        bossKit.LaySnare(Vector3D<float>.Zero);
        var king = TrapperTesting.Sturdy(bossField, 0.3f, 0f, EnemyKind.HollowKing);
        TrapperTesting.Run(bossKit, bossField, TrapperTesting.Step);
        Assert.False(king.IsFrozen);
        Assert.True(king.Health < 10_000f);   // not held, but it still takes the burst
    }

    [Fact]
    public void Snares_LastTwelveSeconds_SixAtMost()
    {
        var kit = TrapperTesting.Kit(TrapperTesting.With((TrapperTree.SnareLine, 1)));
        var field = TrapperTesting.QuietField();
        for (int i = 0; i < 8; i++)
        {
            kit.LaySnare(new Vector3D<float>(i * 3f, 0f, 0f));
        }

        Assert.Equal(6, kit.Snares.Count);
        Assert.Equal(6f, kit.Snares[0].Position.X);   // the two oldest went

        TrapperTesting.Run(kit, field, 11.9f);
        Assert.Equal(6, kit.Snares.Count);
        TrapperTesting.Run(kit, field, 0.2f);
        Assert.Empty(kit.Snares);
    }

    [Fact]
    public void ACrate_DoesntSpringASnare()
    {
        var kit = TrapperTesting.Kit(TrapperTesting.With((TrapperTree.SnareLine, 1)));
        var field = TrapperTesting.QuietField();
        kit.LaySnare(Vector3D<float>.Zero);
        field.Spawn(new Vector3D<float>(0.2f, 0f, 0f), EnemyKind.Crate);
        TrapperTesting.Run(kit, field, 1f);
        Assert.False(kit.Snares.Single().Sprung);
    }

    [Fact]
    public void Caltrops_SlowAndBite_WhereASnareBurst()
    {
        var stats = TrapperTesting.With((TrapperTree.SnareLine, 1), (TrapperTree.Caltrops, 1));
        var kit = TrapperTesting.Kit(stats);
        var field = TrapperTesting.QuietField();
        kit.LaySnare(Vector3D<float>.Zero);
        var ghoul = TrapperTesting.Sturdy(field, 0.3f, 0f);
        TrapperTesting.Run(kit, field, 1.55f);   // the burst
        var patch = Assert.Single(kit.Caltrops);
        Assert.Equal(stats.SnareReach, patch.Radius, 3);
        Assert.True(ghoul.IsChilled);   // they bite (and slow) the moment they're down
        Assert.Equal(RangerStats.CaltropSlow, ghoul.ChillSlow, 3);

        var hits = TrapperTesting.Run(kit, field, 4f);
        Assert.Empty(kit.Caltrops);
        Assert.NotEmpty(hits);
        Assert.Equal(stats.SnareDamage + stats.CaltropDps * RangerStats.CaltropTime, TrapperTesting.Lost(ghoul), 1);
        Assert.All(hits, h => Assert.Equal(TrapperSource.Caltrops, h.Source));
    }

    [Fact]
    public void KillingField_SetsASnareThatKilled_AgainOnce()
    {
        var stats = TrapperTesting.With((TrapperTree.SnareLine, 1), (TrapperTree.KillingField, 1));
        var kit = TrapperTesting.Kit(stats);
        var field = TrapperTesting.QuietField();
        kit.LaySnare(Vector3D<float>.Zero);
        var first = field.Spawn(new Vector3D<float>(0.3f, 0f, 0f));
        first.Health = 1f;
        TrapperTesting.Run(kit, field, 1.6f);
        Assert.False(first.IsAlive);
        var snare = Assert.Single(kit.Snares);
        Assert.False(snare.Sprung);
        Assert.True(snare.Reset);

        var second = field.Spawn(new Vector3D<float>(-0.3f, 0f, 0f));
        second.Health = 1f;
        TrapperTesting.Run(kit, field, 1.6f);
        Assert.False(second.IsAlive);
        Assert.Empty(kit.Snares);   // only once

        var plain = TrapperTesting.Kit(TrapperTesting.With((TrapperTree.SnareLine, 1)));
        plain.LaySnare(Vector3D<float>.Zero);
        var third = field.Spawn(new Vector3D<float>(0.3f, 0f, 0.1f));
        third.Health = 1f;
        TrapperTesting.Run(plain, field, 1.6f);
        Assert.Empty(plain.Snares);
    }

    [Fact]
    public void Ambush_HeldEnemiesTakeMore()
    {
        var kit = TrapperTesting.Kit(TrapperTesting.With((TrapperTree.SnareLine, 1), ("ambush", 2)));
        var field = TrapperTesting.QuietField();
        kit.LaySnare(Vector3D<float>.Zero);
        var ghoul = TrapperTesting.Sturdy(field, 0.3f, 0f);
        Assert.Equal(1f, kit.DamageFactor(ghoul));
        TrapperTesting.Run(kit, field, 0.1f);
        Assert.Equal(1.3f, kit.DamageFactor(ghoul), 4);
    }
}

public class VenomTests
{
    [Fact]
    public void WithoutVenomTips_ArrowsDontPoison()
    {
        var kit = TrapperTesting.Kit(TrapperTesting.With());
        var field = TrapperTesting.QuietField();
        var ghoul = TrapperTesting.Sturdy(field, 5f, 0f);
        kit.ArrowHit(ghoul, 20f);
        Assert.False(kit.IsPoisoned(ghoul));
    }

    [Fact]
    public void VenomTips_PoisonsFortyPercentOfTheHit_OverThreeSeconds()
    {
        var kit = TrapperTesting.Kit(TrapperTesting.With((TrapperTree.VenomTips, 1)));
        var field = TrapperTesting.QuietField();
        var ghoul = TrapperTesting.Sturdy(field, 5f, 0f);
        kit.ArrowHit(ghoul, 20f);
        Assert.Equal(1, kit.DosesOn(ghoul));

        var hits = TrapperTesting.Run(kit, field, 1.5f);
        Assert.Equal(4f, TrapperTesting.Lost(ghoul), 2);   // half the time, half the 8
        TrapperTesting.Run(kit, field, 1.6f);
        Assert.Equal(8f, TrapperTesting.Lost(ghoul), 2);
        Assert.False(kit.IsPoisoned(ghoul));
        Assert.All(hits, h => Assert.Equal(TrapperSource.Poison, h.Source));
        Assert.All(hits, h => Assert.False(h.Crit));
    }

    [Fact]
    public void Poison_StacksToFive_EachItsOwn()
    {
        var kit = TrapperTesting.Kit(TrapperTesting.With((TrapperTree.VenomTips, 1)));
        var field = TrapperTesting.QuietField();
        var ghoul = TrapperTesting.Sturdy(field, 5f, 0f);
        for (int i = 0; i < 7; i++)
        {
            kit.ArrowHit(ghoul, 10f * (i + 1));
        }

        Assert.Equal(5, kit.DosesOn(ghoul));
        Assert.Contains(kit.Poisons[ghoul].Doses, d => MathF.Abs(d.PerSecond - 70f * 0.4f / 3f) < 1e-3f);   // the last replaced the weakest
        Assert.DoesNotContain(kit.Poisons[ghoul].Doses, d => MathF.Abs(d.PerSecond - 10f * 0.4f / 3f) < 1e-3f);

        var more = TrapperTesting.Kit(TrapperTesting.With((TrapperTree.VenomTips, 1), ("potent", 2)));
        for (int i = 0; i < 9; i++)
        {
            more.ArrowHit(ghoul, 10f);
        }

        Assert.Equal(7, more.DosesOn(ghoul));
    }

    [Fact]
    public void AnArrowsHit_PoisonsThroughTheKit()
    {
        var stats = TrapperTesting.With((TrapperTree.VenomTips, 1));
        var kit = TrapperTesting.Kit(stats);
        var field = TrapperTesting.QuietField();
        var ghoul = TrapperTesting.Sturdy(field, 5f, 0f);
        var arrows = new RangerArrows(new Random(1)) { Kit = kit };
        arrows.Fire(new Vector3D<float>(0f, 1f, 0f), new Vector3D<float>(1f, 0f, 0f), 50f, 60f, damage: 20f);
        arrows.Update(0.2f, field, TrapperTesting.FlatGround, new List<Arrow>());

        Assert.Equal(20f, TrapperTesting.Lost(ghoul), 3);
        Assert.Equal(1, kit.DosesOn(ghoul));
        Assert.Equal(20f * 0.4f / 3f, kit.Poisons[ghoul].Doses[0].PerSecond, 4);
    }

    [Fact]
    public void ToxicCloud_APoisonedEnemyThatDies_LeavesACloudThatPoisons()
    {
        var stats = TrapperTesting.With((TrapperTree.VenomTips, 1), (TrapperTree.ToxicCloud, 1));
        var kit = TrapperTesting.Kit(stats);
        var field = TrapperTesting.QuietField();
        var dying = field.Spawn(new Vector3D<float>(10f, 0f, 0f));
        kit.ArrowHit(dying, 20f);
        field.Damage(dying, dying.Health);   // killed by something else: it still counts
        var walker = TrapperTesting.Sturdy(field, 10.5f, 0f);

        TrapperTesting.Run(kit, field, TrapperTesting.Step * 2);
        var cloud = Assert.Single(kit.Clouds);
        Assert.Equal(RangerStats.CloudWidth / 2f, cloud.Radius, 3);
        Assert.Equal(1, kit.DosesOn(walker));

        TrapperTesting.Run(kit, field, 2.5f);
        Assert.Equal(3, kit.DosesOn(walker));   // a dose a second: at 0, 1 and 2
        TrapperTesting.Run(kit, field, 0.6f);
        Assert.Empty(kit.Clouds);

        var plain = TrapperTesting.Kit(TrapperTesting.With((TrapperTree.VenomTips, 1)));
        var other = field.Spawn(new Vector3D<float>(-10f, 0f, 0f));
        plain.ArrowHit(other, 20f);
        field.Damage(other, other.Health);
        TrapperTesting.Run(plain, field, 0.1f);
        Assert.Empty(plain.Clouds);
    }

    [Fact]
    public void CripplingVenom_SlowsAnEnemyCarryingAllItCan()
    {
        var kit = TrapperTesting.Kit(TrapperTesting.With((TrapperTree.VenomTips, 1), (TrapperTree.CripplingVenom, 1)));
        var field = TrapperTesting.QuietField();
        var ghoul = TrapperTesting.Sturdy(field, 5f, 0f);
        for (int i = 0; i < 4; i++)
        {
            kit.ArrowHit(ghoul, 10f);
        }

        TrapperTesting.Run(kit, field, 0.1f);
        Assert.False(ghoul.IsChilled);

        kit.ArrowHit(ghoul, 10f);
        TrapperTesting.Run(kit, field, 0.1f);
        Assert.True(ghoul.IsChilled);
        Assert.Equal(RangerStats.CrippleSlow, ghoul.ChillSlow, 3);
    }

    [Fact]
    public void BlightArrows_PassThroughPoisonedEnemies_WithoutUsingPierce()
    {
        var field = TrapperTesting.QuietField();
        var first = TrapperTesting.Sturdy(field, 5f, 0f);
        var second = TrapperTesting.Sturdy(field, 10f, 0f);
        var third = TrapperTesting.Sturdy(field, 15f, 0f);

        var blight = TrapperTesting.Kit(TrapperTesting.With((TrapperTree.VenomTips, 1), (TrapperTree.BlightArrows, 1)));
        blight.ArrowHit(first, 10f);
        blight.ArrowHit(second, 10f);
        var arrows = new RangerArrows(new Random(1)) { Kit = blight };
        arrows.Fire(new Vector3D<float>(0f, 1f, 0f), new Vector3D<float>(1f, 0f, 0f), 50f, 60f, damage: 20f);
        var hits = new List<ArrowHit>();
        for (int i = 0; i < 30; i++)
        {
            hits.AddRange(arrows.Update(TrapperTesting.Step, field, TrapperTesting.FlatGround, new List<Arrow>()));
        }

        Assert.Equal(new[] { first, second, third }, hits.Select(h => h.Enemy));   // no pierce at all, yet through both poisoned
        Assert.Empty(arrows.Arrows);   // stopped in the third, which wasn't

        var field2 = TrapperTesting.QuietField();
        var a = TrapperTesting.Sturdy(field2, 5f, 0f);
        TrapperTesting.Sturdy(field2, 10f, 0f);
        var venom = TrapperTesting.Kit(TrapperTesting.With((TrapperTree.VenomTips, 1)));
        venom.ArrowHit(a, 10f);
        var plain = new RangerArrows(new Random(1)) { Kit = venom };
        plain.Fire(new Vector3D<float>(0f, 1f, 0f), new Vector3D<float>(1f, 0f, 0f), 50f, 60f, damage: 20f);
        var plainHits = new List<ArrowHit>();
        for (int i = 0; i < 30; i++)
        {
            plainHits.AddRange(plain.Update(TrapperTesting.Step, field2, TrapperTesting.FlatGround, new List<Arrow>()));
        }

        Assert.Single(plainHits);
    }

    [Fact]
    public void FesteringWounds_PoisonedEnemiesTakeMore()
    {
        var kit = TrapperTesting.Kit(TrapperTesting.With((TrapperTree.VenomTips, 1), ("festering", 3)));
        var field = TrapperTesting.QuietField();
        var ghoul = TrapperTesting.Sturdy(field, 5f, 0f);
        Assert.Equal(1f, kit.DamageFactor(ghoul));
        kit.ArrowHit(ghoul, 10f);
        Assert.Equal(1.24f, kit.DamageFactor(ghoul), 4);
    }
}

public class HawkTests
{
    [Fact]
    public void WithoutHawkCompanion_ThereIsNoHawk()
    {
        var kit = TrapperTesting.Kit(TrapperTesting.With());
        var field = TrapperTesting.QuietField();
        TrapperTesting.Sturdy(field, 5f, 0f);
        TrapperTesting.Run(kit, field, 5f);
        Assert.Null(kit.Hawk);
    }

    [Fact]
    public void TheHawk_DivesEveryFourSeconds_AtTheToughestInRange()
    {
        var stats = TrapperTesting.With((TrapperTree.HawkCompanion, 1));
        var kit = TrapperTesting.Kit(stats);
        var field = TrapperTesting.QuietField();
        var weak = TrapperTesting.Sturdy(field, 5f, 0f);
        weak.Health = 5_000f;
        var strong = TrapperTesting.Sturdy(field, 8f, 0f);
        var far = TrapperTesting.Sturdy(field, 0f, 25f, EnemyKind.Brute);   // an elite, but out of range

        var hits = TrapperTesting.Run(kit, field, 3.9f);
        Assert.Empty(hits);
        Assert.Equal(HawkFlight.Circling, kit.Hawk!.Flight);

        TrapperTesting.Run(kit, field, 0.15f);
        Assert.Equal(HawkFlight.Diving, kit.Hawk.Flight);
        Assert.Same(strong, kit.Hawk.Prey);

        hits = TrapperTesting.Run(kit, field, TrapperKit.DiveTime + 0.05f);
        var hit = Assert.Single(hits);
        Assert.Same(strong, hit.Enemy);
        Assert.Equal(TrapperSource.Hawk, hit.Source);
        Assert.Equal(RangerStats.BaseDamage * 2.5f, hit.Damage, 3);
        Assert.Equal(10_000f, far.Health);

        hits = TrapperTesting.Run(kit, field, 3.4f);
        Assert.Empty(hits);
        hits = TrapperTesting.Run(kit, field, 0.6f);
        Assert.Single(hits);   // the next, four seconds after the first
    }

    [Fact]
    public void TheHawk_PrefersBosses_ThenElites_ThenTheMostHealth()
    {
        var field = TrapperTesting.QuietField();
        var ghoul = TrapperTesting.Sturdy(field, 3f, 0f);
        ghoul.Health = 50_000f;
        Assert.Same(ghoul, TrapperKit.Toughest(field, Vector3D<float>.Zero, 20f));
        var brute = field.Spawn(new Vector3D<float>(6f, 0f, 0f), EnemyKind.Brute);
        Assert.Same(brute, TrapperKit.Toughest(field, Vector3D<float>.Zero, 20f));
        var king = field.Spawn(new Vector3D<float>(12f, 0f, 0f), EnemyKind.HollowKing);
        Assert.Same(king, TrapperKit.Toughest(field, Vector3D<float>.Zero, 20f));
        Assert.Same(brute, TrapperKit.Toughest(field, Vector3D<float>.Zero, 8f));
    }

    [Fact]
    public void AStun_HoldsTheDive()
    {
        var kit = TrapperTesting.Kit(TrapperTesting.With((TrapperTree.HawkCompanion, 1)));
        var field = TrapperTesting.QuietField();
        TrapperTesting.Sturdy(field, 5f, 0f);
        Assert.Empty(TrapperTesting.Run(kit, field, 6f, canAct: false));
        Assert.NotEmpty(TrapperTesting.Run(kit, field, 0.6f));
    }

    [Fact]
    public void HawksMark_TheDivedAtEnemyTakesMoreFromYou_ForFourSeconds()
    {
        var kit = TrapperTesting.Kit(TrapperTesting.With((TrapperTree.HawkCompanion, 1), (TrapperTree.HawksMark, 1)));
        var field = TrapperTesting.QuietField();
        var ghoul = TrapperTesting.Sturdy(field, 5f, 0f);
        TrapperTesting.Run(kit, field, 4.05f);
        Assert.True(kit.Marks.ContainsKey(ghoul));
        Assert.Equal(1.25f, kit.DamageFactor(ghoul), 4);

        var hits = TrapperTesting.Run(kit, field, TrapperKit.DiveTime + 0.05f);
        Assert.Equal(RangerStats.BaseDamage * 2.5f * 1.25f, Assert.Single(hits).Damage, 3);   // the dive itself lands on the mark

        var arrows = new RangerArrows(new Random(1)) { Kit = kit };
        arrows.Fire(new Vector3D<float>(0f, 1f, 0f), new Vector3D<float>(1f, 0f, 0f), 50f, 60f, damage: 20f);
        var arrowHit = Assert.Single(arrows.Update(0.2f, field, TrapperTesting.FlatGround, new List<Arrow>()));
        Assert.Equal(25f, arrowHit.Damage, 3);

        ghoul.Position = new Vector3D<float>(40f, 0f, 0f);   // out of the hawk's reach, so it isn't marked again
        TrapperTesting.Run(kit, field, 3.6f);
        Assert.False(kit.Marks.ContainsKey(ghoul));
    }

    [Fact]
    public void KeenTalons_TheDiveAlwaysCrits()
    {
        var stats = TrapperTesting.With((TrapperTree.HawkCompanion, 1), (TrapperTree.KeenTalons, 1));
        var kit = TrapperTesting.Kit(stats);
        var field = TrapperTesting.QuietField();
        TrapperTesting.Sturdy(field, 5f, 0f);
        var hit = Assert.Single(TrapperTesting.Run(kit, field, 4.6f));
        Assert.True(hit.Crit);
        Assert.Equal(stats.HawkDamage * stats.CritMultiplier, hit.Damage, 3);
    }

    [Fact]
    public void ApexPredator_DivesTwiceAsOften_AndPoisonsToTheFull()
    {
        var stats = TrapperTesting.With((TrapperTree.HawkCompanion, 1), (TrapperTree.ApexPredator, 1));
        Assert.Equal(RangerStats.HawkInterval / 2f, stats.HawkDiveInterval, 4);
        var kit = TrapperTesting.Kit(stats);
        var field = TrapperTesting.QuietField();
        var ghoul = TrapperTesting.Sturdy(field, 5f, 0f);
        var hits = TrapperTesting.Run(kit, field, 4.6f);
        Assert.Single(hits, h => h.Source == TrapperSource.Hawk);
        Assert.Equal(stats.PoisonStacks, kit.DosesOn(ghoul));   // no Venom Tips needed
        Assert.Equal(stats.HawkDamage * 0.4f / 3f, kit.Poisons[ghoul].Doses[0].PerSecond, 3);

        hits = TrapperTesting.Run(kit, field, 2f);
        Assert.Single(hits, h => h.Source == TrapperSource.Hawk);
    }

    [Fact]
    public void TheHawk_CirclesAboveTheRanger()
    {
        var kit = TrapperTesting.Kit(TrapperTesting.With((TrapperTree.HawkCompanion, 1)));
        var field = TrapperTesting.QuietField();
        TrapperTesting.Run(kit, field, 1f);
        var hawk = kit.Hawk!;
        Assert.Equal(TrapperKit.CircleHeight, hawk.Position.Y, 3);
        Assert.Equal(TrapperKit.CircleRadius, MathF.Sqrt(hawk.Position.X * hawk.Position.X + hawk.Position.Z * hawk.Position.Z), 3);
    }
}
