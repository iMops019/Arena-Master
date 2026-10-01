using ArenaMaster.Game.Combat;
using ArenaMaster.Game.Items;
using ArenaMaster.Game.Progression;
using ArenaMaster.Game.Shaman;
using Silk.NET.Maths;

namespace ArenaMaster.Game.Tests;

internal static class EarthTesting
{
    public static readonly Vector3D<float> Ahead = new(10f, 0f, 0f);

    /// <summary>Stats with Earth Alignment active and <paramref name="ranks"/> in it, with crits turned off so the numbers are exact.</summary>
    public static ShamanStats With(params (string Id, int Ranks)[] ranks) => new()
    {
        Element = ShamanElement.Stone,
        Earth = EarthBonuses.From(ranks.ToDictionary(r => r.Id, r => r.Ranks)),
        Items = new ItemBonuses { CritChance = -1f },
    };

    /// <summary>Runs the stones for <paramref name="seconds"/>, casting from the Shaman's hand at <paramref name="target"/> unless told not to.</summary>
    public static List<EarthHit> Run(RollingStone stones, ShamanStats stats, EnemyField field, float seconds, Vector3D<float>? target = null, bool canCast = false,
        bool moving = false, Func<float, float, float?>? groundAt = null)
    {
        var hits = new List<EarthHit>();
        for (float t = 0f; t < seconds - 1e-4f; t += ShamanTesting.Step)
        {
            stones.Update(ShamanTesting.Step, ShamanTesting.Hand, target ?? Ahead, moving, stats, field, groundAt ?? ShamanTesting.FlatGround, ShamanTesting.NoObstacles,
                canCast, hits);
        }

        return hits;
    }

    /// <summary>A Shaman with Earth Alignment active, <paramref name="ranks"/> in it, and a run begun.</summary>
    public static ShamanClass EarthShaman(params (string Id, int Ranks)[] ranks)
    {
        var shaman = new ShamanClass(new Random(1));
        shaman.ChooseTree(EarthTree.TreeId);
        shaman.UseTree(ranks.ToDictionary(r => r.Id, r => r.Ranks));
        shaman.BeginRun(new ItemBonuses { CritChance = -1f }, new PlayerHealth(100f));
        return shaman;
    }
}

public class EarthTreeTests
{
    [Fact]
    public void TheTree_IsWellFormed()
    {
        var tree = EarthTree.Tree;
        var nodes = tree.Nodes;
        Assert.Equal("Earth Alignment", tree.Name);
        Assert.Equal("earth", tree.Id);
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
        Assert.Equal(new[] { "Stone", "Quake", "Mountain" }, tree.Lanes.Select(l => l.Name));
        Assert.Equal(new[] { 1, 3, 6, 10, 15, 21, 28 }, tree.TierLevels);
        Assert.Equal(new[] { "Heavy Stone", "Deep Roots" }, nodes.Where(n => n.Tier == 1).Select(n => n.Name));
        Assert.All(nodes.Where(n => n.Tier == 1), n => Assert.Equal(5, n.MaxRanks));
        Assert.InRange(nodes.Count(n => n.Major), AlignmentTree.Tree.Nodes.Count(n => n.Major) - 2, AlignmentTree.Tree.Nodes.Count(n => n.Major) + 2);
    }

    [Fact]
    public void NodesInATier_DoNotOverlap()
    {
        foreach (var tier in EarthTree.Tree.Nodes.GroupBy(n => n.Tier))
        {
            var xs = tier.Select(n => n.X).OrderBy(x => x).ToList();
            Assert.All(xs.Zip(xs.Skip(1)), p => Assert.True(p.Second - p.First >= 100f, $"tier {tier.Key}: {p.First} and {p.Second} are too close"));
        }
    }

    [Fact]
    public void EachLane_EndsInACapstone_AndTheKeyMajorsComeEarly()
    {
        var tree = EarthTree.Tree;
        var capstones = tree.Nodes.Where(n => n.Tier == 7).ToList();
        Assert.All(capstones, n => Assert.True(n.Major));
        Assert.Equal(tree.Lanes.Select(l => l.Name).OrderBy(n => n), capstones.Select(n => n.Lane).OrderBy(n => n));
        Assert.Equal("Stone", tree.Node(EarthTree.Avalanche).Lane);
        Assert.Equal("Quake", tree.Node(EarthTree.TectonicRift).Lane);
        Assert.Equal("Mountain", tree.Node(EarthTree.WalkingMountain).Lane);
        foreach (var early in new[] { EarthTree.Boulder, EarthTree.Aftershock, EarthTree.Stoneskin })
        {
            Assert.InRange(tree.Node(early).Tier, 2, 3);
        }

        Assert.Equal("Stone", tree.Node(EarthTree.Boulder).Lane);
        Assert.Equal("Quake", tree.Node(EarthTree.Aftershock).Lane);
        Assert.Equal("Mountain", tree.Node(EarthTree.Stoneskin).Lane);
        Assert.Equal("Mountain", tree.Node(EarthTree.EarthenTotem).Lane);
        Assert.Equal("Quake", tree.Node(EarthTree.Tremor).Lane);
    }

    [Fact]
    public void TheEarthModels_AreInTheAssets()
    {
        string models = Path.Combine(CEngine.Core.EngineAssets.RepoRoot, "assets", "models");
        Assert.All(EarthView.Models, file => Assert.True(File.Exists(Path.Combine(models, file)), $"{file} is missing"));
    }

    [Fact]
    public void EveryNode_ChangesTheBonuses()
    {
        var options = new System.Text.Json.JsonSerializerOptions { IncludeFields = true };
        var none = System.Text.Json.JsonSerializer.Serialize(EarthBonuses.From(new Dictionary<string, int>()), options);
        foreach (var node in EarthTree.Tree.Nodes)
        {
            var one = System.Text.Json.JsonSerializer.Serialize(EarthBonuses.From(new Dictionary<string, int> { [node.Id] = 1 }), options);
            Assert.True(one != none, $"{node.Name} does nothing");
        }
    }

    [Fact]
    public void Ranks_AddUpIntoTheBonuses()
    {
        var b = EarthBonuses.From(new Dictionary<string, int> { ["heavystone"] = 5, ["granite"] = 5, ["deeproots"] = 5, ["skipping"] = 2, ["hardened"] = 3 });

        Assert.Equal(1.1f, b.StoneDamage, 3);
        Assert.Equal(0.3f, b.StoneSize, 3);
        Assert.Equal(40f, b.MaxHealth, 3);
        Assert.Equal(2, b.Bounces);
        Assert.Equal(MathF.Pow(0.98f, 5) * MathF.Pow(0.95f, 3), b.DamageTaken, 4);
        Assert.False(b.Boulder);

        var stats = EarthTesting.With(("heavystone", 5), ("granite", 5), ("deeproots", 5));
        Assert.Equal(ShamanStats.BaseStoneDamage * 2.1f, stats.StoneDamage, 3);
        Assert.Equal(ShamanStats.BaseBallRadius * 1.3f, stats.StoneRadius, 3);
        Assert.Equal(ShamanStats.BaseMaxHealth + 40f, stats.MaxHealth, 3);
    }
}

public class EarthActiveTreeTests
{
    [Fact]
    public void TheShaman_HasBothTrees_LightningFirst()
    {
        var shaman = new ShamanClass(new Random(1));

        Assert.Equal(new[] { AlignmentTree.TreeId, EarthTree.TreeId }, shaman.Trees.Select(t => t.Id));
        Assert.Equal(AlignmentTree.TreeId, shaman.Tree.Id);
        Assert.Contains("stone", shaman.Summary);
        Assert.Contains("lightning", shaman.Summary);
    }

    [Fact]
    public void UsingATree_EmptiesTheOther_AndPicksWhatIsThrown()
    {
        var shaman = new ShamanClass(new Random(1));
        shaman.ChooseTree(EarthTree.TreeId);
        shaman.UseTree(new Dictionary<string, int> { ["heavystone"] = 3, [EarthTree.Boulder] = 1 });

        Assert.Equal(ShamanElement.Stone, shaman.Stats.Element);
        Assert.True(shaman.Stats.Earth.Boulder);
        Assert.Equal(0.3f, shaman.Stats.Earth.StoneDamage, 3);

        shaman.ChooseTree(AlignmentTree.TreeId);
        shaman.UseTree(new Dictionary<string, int> { ["charged"] = 2 });

        Assert.Equal(ShamanElement.Lightning, shaman.Stats.Element);
        Assert.False(shaman.Stats.Earth.Boulder);
        Assert.Equal(0f, shaman.Stats.Earth.StoneDamage);
        Assert.Equal(0.2f, shaman.Stats.Tree.LightningDamage, 3);

        shaman.ChooseTree(EarthTree.TreeId);
        shaman.UseTree(new Dictionary<string, int>());
        Assert.Equal(0f, shaman.Stats.Tree.LightningDamage);   // nothing of Lightning Alignment counts with Earth active
    }

    [Fact]
    public void WithEarthActive_ItThrowsStones_AndNoLightning()
    {
        var field = ShamanTesting.QuietField();
        ShamanTesting.Sturdy(field, 10f);
        var shaman = EarthTesting.EarthShaman();

        for (float t = 0f; t < 1.5f; t += ShamanTesting.Step)
        {
            shaman.Fight(ShamanTesting.Step, ShamanTesting.Hand, Vector3D<float>.Zero, EarthTesting.Ahead, false, false, field, ShamanTesting.FlatGround,
                ShamanTesting.NoObstacles, new PlayerHealth(100f), new DamageNumbers());
        }

        Assert.True(shaman.Stones.Casts >= 1);
        Assert.Equal(0, shaman.Storm.Casts);
        Assert.Empty(shaman.Storm.Balls);
        Assert.Empty(shaman.Storm.Arcs);
        Assert.Empty(shaman.Storm.Zaps);
    }

    [Fact]
    public void WithLightningActive_NothingOfTheStoneHappens()
    {
        var field = ShamanTesting.QuietField();
        ShamanTesting.Sturdy(field, 10f);
        var shaman = new ShamanClass(new Random(1));
        shaman.UseTree(new Dictionary<string, int>());
        shaman.BeginRun(new ItemBonuses(), new PlayerHealth(100f));

        for (float t = 0f; t < 1.5f; t += ShamanTesting.Step)
        {
            shaman.Fight(ShamanTesting.Step, ShamanTesting.Hand, Vector3D<float>.Zero, EarthTesting.Ahead, false, false, field, ShamanTesting.FlatGround,
                ShamanTesting.NoObstacles, new PlayerHealth(100f), new DamageNumbers());
        }

        Assert.True(shaman.Storm.Casts >= 1);
        Assert.Equal(0, shaman.Stones.Casts);
        Assert.Empty(shaman.Stones.Quakes);
        Assert.Null(shaman.Meter);
    }

    [Fact]
    public void TheStone_IsThrownJustLikeTheBall()
    {
        var stats = EarthTesting.With();
        var stone = new RollingStone(new Random(1)).Lob(ShamanTesting.Hand, EarthTesting.Ahead, stats);
        var ball = new RollingLightning(new Random(1)).Lob(ShamanTesting.Hand, EarthTesting.Ahead, new ShamanStats());

        Assert.Equal(ball.Velocity.X, stone.Velocity.X, 3);
        Assert.Equal(ball.Velocity.Y, stone.Velocity.Y, 3);
        Assert.Equal(stats.Bounces, stone.BouncesLeft);
        Assert.Equal(stats.Lifetime, stone.Life, 3);
    }

    /// <summary>
    /// The stone's hit is set so that into a crowd a fresh stone does about what a fresh ball of lightning does (its zaps and forks were most of the lightning's
    /// damage, and the stone has neither): a pack walking in, dense and looser, over 20 s of throwing at the nearest.
    /// </summary>
    [Theory]
    [InlineData(1.2f, 7)]
    [InlineData(2f, 5)]
    public void AFreshStone_HitsACrowd_AboutAsHardAsFreshLightning(float spacing, int side)
    {
        float lightning = IntoACrowd(stone: false, spacing, side);
        float stone = IntoACrowd(stone: true, spacing, side);

        Assert.InRange(stone / lightning, 0.75f, 1.3f);
    }

    private static float IntoACrowd(bool stone, float spacing, int side)
    {
        var field = new EnemyField(new Random(3)) { TargetCount = 0 };
        for (int i = 0; i < side; i++)
        {
            for (int j = 0; j < side; j++)
            {
                field.Spawn(new Vector3D<float>(16f + (i - side / 2) * spacing, 0f, (j - side / 2) * spacing)).Health = 100_000f;
            }
        }

        var stats = stone ? EarthTesting.With() : new ShamanStats();
        stats.Items = new ItemBonuses();
        var storm = new RollingLightning(new Random(1));
        var stones = new RollingStone(new Random(1));
        var stormHits = new List<StormHit>();
        var stoneHits = new List<EarthHit>();
        var player = new PlayerTarget(Vector3D<float>.Zero, true, new PlayerHealth(1_000_000f), new PlayerCondition());
        for (float t = 0f; t < 20f; t += ShamanTesting.Step)
        {
            var aim = field.Nearest(Vector3D<float>.Zero, 24f)?.Position;
            if (stone)
            {
                stones.Update(ShamanTesting.Step, ShamanTesting.Hand, aim, false, stats, field, ShamanTesting.FlatGround, ShamanTesting.NoObstacles, true, stoneHits);
            }
            else
            {
                storm.Update(ShamanTesting.Step, ShamanTesting.Hand, Vector3D<float>.Zero, aim, false, stats, field, ShamanTesting.FlatGround, ShamanTesting.NoObstacles,
                    true, stormHits);
            }

            field.Update(ShamanTesting.Step, player, ShamanTesting.FlatGround);
        }

        return stone ? stoneHits.Sum(h => h.Damage) : stormHits.Sum(h => h.Damage);
    }
}

public class RollingStoneTests
{
    [Fact]
    public void TheStone_StrikesTheFirstEnemy_BouncesOffIt_AndKnocksItBack()
    {
        var field = ShamanTesting.QuietField();
        var struck = ShamanTesting.Sturdy(field, 10f);
        var stones = new RollingStone(new Random(1));
        var stats = EarthTesting.With();
        var stone = stones.Lob(ShamanTesting.Hand, EarthTesting.Ahead, stats);

        var hits = new List<EarthHit>();
        for (int i = 0; i < 120 && !hits.Any(h => h.Source == EarthSource.Stone); i++)
        {
            stones.Update(ShamanTesting.Step, ShamanTesting.Hand, null, false, stats, field, ShamanTesting.FlatGround, ShamanTesting.NoObstacles, false, hits);
        }

        var hit = Assert.Single(hits, h => h.Source == EarthSource.Stone);
        Assert.Equal(struck, hit.Enemy);
        Assert.Equal(stats.StoneDamage, hit.Damage, 3);
        Assert.Equal(stats.Bounces - 1, stone.BouncesLeft);   // bouncing off it spent a bounce
        Assert.True(stone.Velocity.X < 0f, "it bounced back off the enemy");
        Assert.Contains(hits, h => h.Source == EarthSource.Quake);   // and the bounce shook the ground

        EarthTesting.Run(stones, stats, field, 0.3f);
        Assert.Equal(10f + ShamanStats.BaseKnockback, struck.Position.X, 1);
    }

    [Fact]
    public void KnockBack_IsHalvedForElites_AndNoneForBosses()
    {
        var field = ShamanTesting.QuietField();
        var ghoul = ShamanTesting.Sturdy(field, 0f, 0f);
        var brute = ShamanTesting.Sturdy(field, 0f, 10f, EnemyKind.Brute);
        var king = ShamanTesting.Sturdy(field, 0f, 20f, EnemyKind.HollowKing);
        var stones = new RollingStone(new Random(1));
        var stats = EarthTesting.With();
        foreach (var enemy in new[] { ghoul, brute, king })
        {
            stones.Shove(enemy, Vector3D<float>.UnitX, stats.Knockback);
        }

        EarthTesting.Run(stones, stats, field, 0.3f);

        Assert.Equal(1.5f, ghoul.Position.X, 2);
        Assert.Equal(0.75f, brute.Position.X, 2);
        Assert.Equal(0f, king.Position.X, 3);
    }

    [Fact]
    public void KnockBack_NeverPushesAnEnemyIntoRock_OrUpACliff()
    {
        var field = ShamanTesting.QuietField();
        var byTheWall = ShamanTesting.Sturdy(field, 10f);
        var underTheCliff = ShamanTesting.Sturdy(field, 10f, 20f);
        var stones = new RollingStone(new Random(1));
        var stats = EarthTesting.With();
        stones.Shove(byTheWall, Vector3D<float>.UnitX, 3f);
        stones.Shove(underTheCliff, Vector3D<float>.UnitX, 3f);

        // Rock (no ground at all, as the cave's walls read) past x = 10.6 by the first; a 3 m cliff past x = 10.6 by the second.
        float? Cave(float x, float z) => z < 10f ? (x < 10.6f ? 0f : null) : (x < 10.6f ? 0f : 3f);
        EarthTesting.Run(stones, stats, field, 0.4f, groundAt: Cave);

        Assert.InRange(byTheWall.Position.X, 10.3f, 10.6f);
        Assert.InRange(underTheCliff.Position.X, 10.3f, 10.6f);
        Assert.Equal(0f, underTheCliff.Position.Y);
    }

    [Fact]
    public void EachBounce_Quakes()
    {
        var field = ShamanTesting.QuietField();
        var nearTheLanding = ShamanTesting.Sturdy(field, 10f, 1.4f);   // beside where it comes down, not in its path
        var stones = new RollingStone(new Random(1));
        var stats = EarthTesting.With();
        stones.Lob(ShamanTesting.Hand, EarthTesting.Ahead, stats);

        var hits = EarthTesting.Run(stones, stats, field, 1f);

        var quake = Assert.Single(hits);
        Assert.Equal(EarthSource.Quake, quake.Source);
        Assert.Equal(nearTheLanding, quake.Enemy);
        Assert.Equal(stats.StoneDamage * ShamanStats.BaseQuakeShare, quake.Damage, 3);
        Assert.Equal(stats.QuakeDamage, quake.Damage, 3);
    }

    [Fact]
    public void TheStone_ComesToRest_OnceItsBouncesAreSpent()
    {
        var stones = new RollingStone(new Random(1));
        var stats = EarthTesting.With();
        stones.Lob(ShamanTesting.Hand, EarthTesting.Ahead, stats);

        EarthTesting.Run(stones, stats, ShamanTesting.QuietField(), stats.Lifetime + 0.1f);

        Assert.Empty(stones.Stones);
    }

    [Fact]
    public void AnItemsChain_LetsItBounceOffAnEnemy_ForFree()
    {
        var field = ShamanTesting.QuietField();
        ShamanTesting.Sturdy(field, 10f);
        var stones = new RollingStone(new Random(1));
        var stats = EarthTesting.With();
        stats.Items = new ItemBonuses { CritChance = -1f, Chains = 1 };
        var stone = stones.Lob(ShamanTesting.Hand, EarthTesting.Ahead, stats);

        var hits = new List<EarthHit>();
        for (int i = 0; i < 120 && !hits.Any(h => h.Source == EarthSource.Stone); i++)
        {
            stones.Update(ShamanTesting.Step, ShamanTesting.Hand, null, false, stats, field, ShamanTesting.FlatGround, ShamanTesting.NoObstacles, false, hits);
        }

        Assert.Equal(stats.Bounces, stone.BouncesLeft);
        Assert.Equal(0, stone.FreeRebounds);
    }

    [Fact]
    public void Boulder_CrushesThroughEveryEnemyInTheWay_OnceEach_AndKnocksThemAside()
    {
        var field = ShamanTesting.QuietField();
        var line = new[] { ShamanTesting.Sturdy(field, 8f, 0.2f), ShamanTesting.Sturdy(field, 9.5f, 0.2f), ShamanTesting.Sturdy(field, 11f, 0.2f) };   // where it comes down through body height
        var stones = new RollingStone(new Random(1));
        var stats = EarthTesting.With((EarthTree.Boulder, 1));
        var stone = stones.Lob(ShamanTesting.Hand, new Vector3D<float>(12f, 0f, 0f), stats);

        var hits = EarthTesting.Run(stones, stats, field, 0.9f);

        Assert.All(line, e => Assert.Single(hits, h => h.Enemy == e && h.Source == EarthSource.Stone));
        Assert.True(stone.Velocity.X > 0f, "it rolled on");
        Assert.All(line, e => Assert.True(e.Position.Z > 0.5f, "knocked aside"));
    }

    [Fact]
    public void WithoutBoulder_TheFirstInTheWay_StopsTheStone()
    {
        var field = ShamanTesting.QuietField();
        var first = ShamanTesting.Sturdy(field, 8f, 0.2f);
        var behind = ShamanTesting.Sturdy(field, 11f, 0.2f);
        var stones = new RollingStone(new Random(1));
        var stats = EarthTesting.With();
        stones.Lob(ShamanTesting.Hand, new Vector3D<float>(12f, 0f, 0f), stats);

        var hits = EarthTesting.Run(stones, stats, field, 0.75f);

        Assert.Contains(hits, h => h.Enemy == first && h.Source == EarthSource.Stone);
        Assert.DoesNotContain(hits, h => h.Enemy == behind && h.Source == EarthSource.Stone);
    }

    [Fact]
    public void Aftershock_LeavesACrack_ThatBurstsASecondLater()
    {
        var field = ShamanTesting.QuietField();
        var nearTheLanding = ShamanTesting.Sturdy(field, 10f, 1.4f);
        var stones = new RollingStone(new Random(1));
        var stats = EarthTesting.With((EarthTree.Aftershock, 1));
        stones.Lob(ShamanTesting.Hand, EarthTesting.Ahead, stats);

        var early = EarthTesting.Run(stones, stats, field, 1f);
        Assert.NotEmpty(stones.Cracks);
        Assert.DoesNotContain(early, h => h.Source == EarthSource.Crack);

        var later = EarthTesting.Run(stones, stats, field, 1f);
        var burst = Assert.Single(later, h => h.Source == EarthSource.Crack);
        Assert.Equal(nearTheLanding, burst.Enemy);
        Assert.Equal(stats.StoneDamage * ShamanStats.CrackShare, burst.Damage, 3);
    }

    [Fact]
    public void Stoneskin_BuildsStandingStill_AndWearsAwayMoving()
    {
        var stones = new RollingStone(new Random(1));
        var stats = EarthTesting.With((EarthTree.Stoneskin, 1));
        var field = ShamanTesting.QuietField();

        EarthTesting.Run(stones, stats, field, 3f);
        Assert.Equal(0.15f, stones.Stoneskin, 2);
        EarthTesting.Run(stones, stats, field, 10f);
        Assert.Equal(0.30f, stones.Stoneskin, 3);
        EarthTesting.Run(stones, stats, field, 1f, moving: true);
        Assert.Equal(0.20f, stones.Stoneskin, 2);
    }

    [Fact]
    public void WalkingMountain_BuildsStoneskinTwiceAsFast_AndKeepsItMoving()
    {
        var stones = new RollingStone(new Random(1));
        var stats = EarthTesting.With((EarthTree.WalkingMountain, 1));   // brings Stoneskin with it
        var field = ShamanTesting.QuietField();

        EarthTesting.Run(stones, stats, field, 2f);
        Assert.Equal(0.20f, stones.Stoneskin, 2);
        EarthTesting.Run(stones, stats, field, 3f, moving: true);
        Assert.Equal(0.20f, stones.Stoneskin, 2);
    }

    [Fact]
    public void Stoneskin_CutsTheDamageTaken_AndShowsOnTheMeter()
    {
        var shaman = EarthTesting.EarthShaman((EarthTree.Stoneskin, 1));
        var field = ShamanTesting.QuietField();
        float before = shaman.DamageTaken;

        for (float t = 0f; t < 4f - 1e-4f; t += ShamanTesting.Step)
        {
            shaman.Fight(ShamanTesting.Step, ShamanTesting.Hand, Vector3D<float>.Zero, null, moving: false, stunned: false, field, ShamanTesting.FlatGround,
                ShamanTesting.NoObstacles, new PlayerHealth(100f), new DamageNumbers());
        }

        Assert.Equal(before * 0.8f, shaman.DamageTaken, 2);
        var (label, fill) = Assert.IsType<(string, float)>(shaman.Meter);
        Assert.Equal("STONESKIN 20%", label);
        Assert.Equal(0.2f / 0.3f, fill, 2);
    }

    [Fact]
    public void Tremor_StaggersWhatAQuakeHits_ElitesLess_BossesNot()
    {
        var field = ShamanTesting.QuietField();
        var ghoul = ShamanTesting.Sturdy(field, 1.5f, 0f);
        var brute = ShamanTesting.Sturdy(field, -1.5f, 0f, EnemyKind.Brute);
        var king = ShamanTesting.Sturdy(field, 0f, 2.5f, EnemyKind.HollowKing);
        var stones = new RollingStone(new Random(1));
        var stats = EarthTesting.With((EarthTree.Tremor, 1), (EarthTree.Upheaval, 1));   // Upheaval's heave is a quake too: a sure way to set one off

        Assert.True(stones.Upheave(Vector3D<float>.Zero, stats, field, new List<EarthHit>()));

        Assert.Equal(ShamanStats.TremorSeconds, ghoul.StaggeredFor, 3);
        Assert.Equal(ShamanStats.TremorSeconds * 0.5f, brute.StaggeredFor, 3);
        Assert.False(king.IsStaggered);
    }

    [Fact]
    public void Tremor_StaggersWhatABouncesQuakeHits()
    {
        var field = ShamanTesting.QuietField();
        var nearTheLanding = ShamanTesting.Sturdy(field, 10f, 1.4f);
        var stones = new RollingStone(new Random(1));
        var stats = EarthTesting.With((EarthTree.Tremor, 1));
        stones.Lob(ShamanTesting.Hand, EarthTesting.Ahead, stats);

        EarthTesting.Run(stones, stats, field, 0.9f);

        Assert.True(nearTheLanding.IsStaggered);
    }

    [Fact]
    public void GatheringWeight_MakesEachBounceHitHarder()
    {
        var stones = new RollingStone(new Random(1));
        var stats = EarthTesting.With((EarthTree.GatheringWeight, 1));
        var stone = stones.Lob(ShamanTesting.Hand, EarthTesting.Ahead, stats);
        var field = ShamanTesting.QuietField();

        while (stone.BouncesLeft > stats.Bounces - 2)
        {
            EarthTesting.Run(stones, stats, field, ShamanTesting.Step);
        }

        Assert.Equal(1f + 2f * ShamanStats.WeightPerBounce, stone.Weight, 3);
        Assert.Equal(stats.StoneDamage * 1.4f, stone.Damage, 3);
    }

    [Fact]
    public void RumblingEarth_MakesQuakesWiderAndHarder()
    {
        var plain = EarthTesting.With();
        var rumbling = EarthTesting.With((EarthTree.RumblingEarth, 1));

        Assert.Equal(plain.QuakeRadius * ShamanStats.RumblingRadius, rumbling.QuakeRadius, 3);
        Assert.Equal(plain.QuakeDamage * ShamanStats.RumblingDamage, rumbling.QuakeDamage, 3);
    }

    [Fact]
    public void Shatter_BurstsAStoneThatComesToRest()
    {
        var field = ShamanTesting.QuietField();
        var nearby = ShamanTesting.Sturdy(field, 10f, 2.2f);
        var stats = EarthTesting.With((EarthTree.Shatter, 1));
        var stones = new RollingStone(new Random(1));
        stones.Lob(ShamanTesting.Hand, EarthTesting.Ahead, stats).BouncesLeft = 0;   // it comes to rest where it lands

        var hits = EarthTesting.Run(stones, stats, field, 1f);

        Assert.Empty(stones.Stones);
        var rubble = Assert.Single(hits);
        Assert.Equal(EarthSource.Shatter, rubble.Source);
        Assert.Equal(nearby, rubble.Enemy);
        Assert.Equal(stats.StoneDamage * ShamanStats.ShatterShare, rubble.Damage, 3);

        var plainStones = new RollingStone(new Random(1));
        plainStones.Lob(ShamanTesting.Hand, EarthTesting.Ahead, EarthTesting.With()).BouncesLeft = 0;
        Assert.Empty(EarthTesting.Run(plainStones, EarthTesting.With(), field, 1f));
    }

    [Fact]
    public void Avalanche_MakesEverySixthCastARockslide()
    {
        var stones = new RollingStone(new Random(1));
        var stats = EarthTesting.With((EarthTree.Avalanche, 1));
        for (int i = 0; i < 5; i++)
        {
            stones.Cast(ShamanTesting.Hand, EarthTesting.Ahead, stats);
        }

        Assert.Equal(5, stones.Stones.Count);
        stones.Cast(ShamanTesting.Hand, EarthTesting.Ahead, stats);

        Assert.Equal(10, stones.Stones.Count);   // five stones fanned out
        var slide = stones.Stones.Skip(5).ToList();
        Assert.True(slide.Select(s => MathF.Round(MathF.Atan2(s.Velocity.Z, s.Velocity.X), 3)).Distinct().Count() == 5, "fanned out, each its own way");
    }

    [Fact]
    public void TectonicRift_SplitsTheGround_FromOneLandingToTheNext_AndBitesWhatStandsInIt()
    {
        var field = ShamanTesting.QuietField();
        var standing = ShamanTesting.Sturdy(field, 13f, 0.9f);   // beside the stone's path, between its first two landings
        var stones = new RollingStone(new Random(1));
        var stats = EarthTesting.With((EarthTree.TectonicRift, 1));
        stones.Lob(ShamanTesting.Hand, EarthTesting.Ahead, stats);

        var hits = EarthTesting.Run(stones, stats, field, 2.5f);

        Assert.NotEmpty(stones.Rifts);
        var bites = hits.Where(h => h.Source == EarthSource.Rift && h.Enemy == standing).ToList();
        Assert.True(bites.Count >= 2);
        Assert.All(bites, b => Assert.Equal(stats.StoneDamage * ShamanStats.RiftShare * ShamanStats.RiftTick, b.Damage, 3));
        Assert.DoesNotContain(hits, h => h.Enemy == standing && h.Source != EarthSource.Rift);
    }

    [Fact]
    public void EarthenTotem_StandsAWhile_AndLuresTheEnemiesNearIt()
    {
        var field = ShamanTesting.QuietField();
        var stones = new RollingStone(new Random(1));
        var stats = EarthTesting.With((EarthTree.EarthenTotem, 1));

        var totem = stones.PlantTotem(new Vector3D<float>(5f, 0f, 0f), stats, field);

        var lure = Assert.Single(field.Lures);
        Assert.Equal(totem.Position, lure.Spot);
        Assert.Equal(ShamanStats.BaseTotemReach, lure.Reach, 3);
        Assert.Equal(ShamanStats.BaseTotemSeconds, lure.Left, 3);
        EarthTesting.Run(stones, stats, field, ShamanStats.BaseTotemSeconds + 0.1f);
        Assert.Empty(stones.Totems);
    }
}

public class EarthAnswerTests
{
    [Fact]
    public void Upheaval_HeavesTheGround_WhenABlowLands_AtMostOnceASecond()
    {
        var field = ShamanTesting.QuietField();
        var attacker = ShamanTesting.Sturdy(field, 1.5f);
        var shaman = EarthTesting.EarthShaman((EarthTree.Upheaval, 1));
        var numbers = new DamageNumbers();
        var health = new PlayerHealth(100f);

        shaman.AnswerStrikes(new[] { new Strike(attacker, 8f, true) }, Vector3D<float>.Zero, field, health, numbers);
        Assert.Equal(10_000f, attacker.Health);   // a blocked blow: no heave

        shaman.AnswerStrikes(new[] { new Strike(attacker, 8f, false), new Strike(attacker, 8f, false) }, Vector3D<float>.Zero, field, health, numbers);
        Assert.Equal(10_000f - shaman.Stats.StoneDamage * ShamanStats.UpheavalShare, attacker.Health, 2);   // once, for both blows

        shaman.AnswerStrikes(new[] { new Strike(attacker, 8f, false) }, Vector3D<float>.Zero, field, health, numbers);
        Assert.Equal(10_000f - shaman.Stats.StoneDamage * ShamanStats.UpheavalShare, attacker.Health, 2);   // too soon again
    }

    [Fact]
    public void WithoutUpheaval_ABlowIsNotAnswered()
    {
        var field = ShamanTesting.QuietField();
        var attacker = ShamanTesting.Sturdy(field, 1.5f);
        var shaman = EarthTesting.EarthShaman();

        shaman.AnswerStrikes(new[] { new Strike(attacker, 8f, false) }, Vector3D<float>.Zero, field, new PlayerHealth(100f), new DamageNumbers());

        Assert.Equal(10_000f, attacker.Health);
    }
}

public class EarthCardTests
{
    private static readonly ShamanUpgrade[] LightningOnly =
        { ShamanUpgrade.BranchingBolts, ShamanUpgrade.LongReach, ShamanUpgrade.StaticField, ShamanUpgrade.Conductive, ShamanUpgrade.RodMastery };

    private static readonly ShamanUpgrade[] EarthOnly =
    {
        ShamanUpgrade.RumblingGround, ShamanUpgrade.BruteStrength, ShamanUpgrade.DeepCracks, ShamanUpgrade.SturdyStance, ShamanUpgrade.TotemCarving,
        ShamanUpgrade.WideningRift,
    };

    private static HashSet<ShamanUpgrade> Seen(ShamanStats stats)
    {
        var seen = new HashSet<ShamanUpgrade>();
        for (int seed = 0; seed < 300; seed++)
        {
            seen.UnionWith(ShamanUpgrades.Roll(stats, new Random(seed)).Where(c => c.Upgrade is not null).Select(c => c.Upgrade!.Value));
        }

        return seen;
    }

    [Fact]
    public void WithLightningActive_TheEarthCardsNeverComeUp()
    {
        var lightning = new ShamanStats { Tree = AlignmentBonuses.From(new Dictionary<string, int> { [AlignmentTree.LightningRod] = 1 }) };

        var seen = Seen(lightning);

        Assert.DoesNotContain(seen, u => EarthOnly.Contains(u));
        Assert.All(LightningOnly, u => Assert.Contains(u, seen));
    }

    [Fact]
    public void WithEarthActive_TheLightningCardsNeverComeUp_AndTheMajorsCardsWaitForTheirMajors()
    {
        var fresh = Seen(EarthTesting.With());
        Assert.DoesNotContain(fresh, u => LightningOnly.Contains(u));
        Assert.Contains(ShamanUpgrade.RumblingGround, fresh);
        Assert.Contains(ShamanUpgrade.BruteStrength, fresh);
        Assert.Contains(ShamanUpgrade.ChargedCore, fresh);
        Assert.Contains(ShamanUpgrade.TwinSpheres, fresh);
        Assert.DoesNotContain(ShamanUpgrade.DeepCracks, fresh);
        Assert.DoesNotContain(ShamanUpgrade.SturdyStance, fresh);
        Assert.DoesNotContain(ShamanUpgrade.TotemCarving, fresh);
        Assert.DoesNotContain(ShamanUpgrade.WideningRift, fresh);

        var built = EarthTesting.With((EarthTree.Aftershock, 1), (EarthTree.Stoneskin, 1), (EarthTree.EarthenTotem, 1), (EarthTree.TectonicRift, 1));
        Assert.All(EarthOnly, u => Assert.True(ShamanUpgrades.Offered(u, built), $"{u} is not offered"));
        Assert.True(ShamanUpgrades.Offered(ShamanUpgrade.SturdyStance, EarthTesting.With((EarthTree.WalkingMountain, 1))));
    }

    [Fact]
    public void TheCards_ReadRightForTheStone()
    {
        var stone = EarthTesting.With();
        var lightning = new ShamanStats();

        Assert.Equal(("Hardened Stone", "+20% stone damage"), ShamanUpgrades.Named(ShamanUpgrade.ChargedCore, stone));
        Assert.Equal(("Charged Core", "+20% lightning damage"), ShamanUpgrades.Named(ShamanUpgrade.ChargedCore, lightning));
        foreach (var upgrade in ShamanUpgrades.All.Select(u => u.Upgrade).Where(u => ShamanUpgrades.Offered(u, stone)))
        {
            var (name, text) = ShamanUpgrades.Named(upgrade, stone);
            Assert.DoesNotContain("lightning", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("ball", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Storm", name);
            Assert.DoesNotContain("Charge", name);
        }

        var rolled = ShamanUpgrades.Roll(stone, new Random(4), count: 40);
        Assert.All(rolled, c => Assert.Equal(ShamanUpgrades.Named(c.Upgrade!.Value, stone).Name, c.Name));
    }

    [Fact]
    public void TheEarthCards_DoWhatTheySay()
    {
        var stats = EarthTesting.With((EarthTree.Aftershock, 1), (EarthTree.Stoneskin, 1), (EarthTree.EarthenTotem, 1), (EarthTree.TectonicRift, 1));
        var before = (stats.QuakeRadius, stats.QuakeShare, stats.Knockback, stats.CrackRadius);
        foreach (var upgrade in new[]
                 {
                     ShamanUpgrade.RumblingGround, ShamanUpgrade.BruteStrength, ShamanUpgrade.DeepCracks, ShamanUpgrade.SturdyStance, ShamanUpgrade.TotemCarving,
                     ShamanUpgrade.WideningRift,
                 })
        {
            stats.Increase(upgrade);
        }

        Assert.Equal(before.QuakeRadius * 1.15f, stats.QuakeRadius, 3);
        Assert.Equal(before.QuakeShare * 1.25f, stats.QuakeShare, 3);
        Assert.Equal(before.Knockback * 1.25f, stats.Knockback, 3);
        Assert.Equal(before.CrackRadius * 1.15f, stats.CrackRadius, 3);
        Assert.Equal(1.3f, stats.CrackDamageShare, 3);
        Assert.Equal(0.35f, stats.StoneskinMax, 3);
        Assert.Equal(ShamanStats.StoneskinGain * 1.25f, stats.StoneskinRate, 4);
        Assert.Equal(ShamanStats.BaseTotemSeconds + 1f, stats.TotemSeconds, 3);
        Assert.Equal(ShamanStats.BaseTotemReach + 1f, stats.TotemReach, 3);
        Assert.Equal(ShamanStats.BaseRiftSeconds + 1f, stats.RiftSeconds, 3);
        Assert.Equal(ShamanStats.RiftShare * 1.2f, stats.RiftDamageShare, 3);
    }

    [Fact]
    public void AnEarthShaman_StillRollsThreeDifferentCards()
    {
        for (int seed = 0; seed < 20; seed++)
        {
            Assert.Equal(3, ShamanUpgrades.Roll(EarthTesting.With(), new Random(seed)).Select(c => c.Upgrade).Distinct().Count());
        }
    }
}

/// <summary>The enemy field's general lure and stagger, which Earth Alignment's totem and Tremor use.</summary>
public class EnemyLureAndStaggerTests
{
    private static List<Strike> Run(EnemyField field, float seconds)
    {
        var strikes = new List<Strike>();
        var player = new PlayerTarget(Vector3D<float>.Zero, true, new PlayerHealth(10_000f), new PlayerCondition());
        for (float t = 0f; t < seconds; t += ShamanTesting.Step)
        {
            field.Update(ShamanTesting.Step, player, ShamanTesting.FlatGround);
            strikes.AddRange(field.Strikes);
        }

        return strikes;
    }

    [Fact]
    public void AnEnemyNearALure_WalksToIt_InsteadOfThePlayer()
    {
        var field = ShamanTesting.QuietField();
        var lured = field.Spawn(new Vector3D<float>(14f, 0f, 0f));
        var free = field.Spawn(new Vector3D<float>(0f, 0f, 14f));
        field.AddLure(new Vector3D<float>(10f, 0f, 0f), 6f, 5f);

        Run(field, 2f);

        Assert.InRange(Vector3D.Distance(lured.Position, new Vector3D<float>(10f, 0f, 0f)), 0f, EnemyField.LureStop + 0.3f);
        Assert.True(free.Position.Z < 8f, "one out of its reach still comes for the player");
    }

    [Fact]
    public void ALuredEnemy_StartsNoAttack_UntilTheLureIsGone()
    {
        var field = ShamanTesting.QuietField();
        field.Spawn(new Vector3D<float>(10f, 0f, 0f), EnemyKind.CrossbowGhoul);
        var shot = EnemyKind.CrossbowGhoul.Attacks[0];
        float cycle = EnemyKind.CrossbowGhoul.AttackCooldown + shot.WindUp + shot.Active + shot.Recover;
        field.AddLure(new Vector3D<float>(10f, 0f, 0f), 6f, 2f * cycle - 0.1f);

        Assert.Empty(Run(field, 2f * cycle));
        Assert.Empty(field.Lures);   // its time is up

        Assert.NotEmpty(Run(field, 2f * cycle));
    }

    [Fact]
    public void ABoss_PaysALureNoMind()
    {
        var field = ShamanTesting.QuietField();
        var king = field.Spawn(new Vector3D<float>(14f, 0f, 0f), EnemyKind.HollowKing);
        field.AddLure(king.Position, 6f, 5f);

        Run(field, 0.9f);   // before its first attack: it just walks

        Assert.True(king.Position.X < 13f);
    }

    [Fact]
    public void AStaggeredEnemy_NeitherStepsNorAttacks_UntilItRecovers()
    {
        var field = ShamanTesting.QuietField();
        var shooter = field.Spawn(new Vector3D<float>(10f, 0f, 0f), EnemyKind.CrossbowGhoul);
        var walker = field.Spawn(new Vector3D<float>(0f, 0f, 14f));
        var shot = EnemyKind.CrossbowGhoul.Attacks[0];
        float cycle = EnemyKind.CrossbowGhoul.AttackCooldown + shot.WindUp + shot.Active + shot.Recover;
        shooter.Stagger(2f * cycle);
        walker.Stagger(2f * cycle);

        Assert.Empty(Run(field, 2f * cycle - 0.1f));
        Assert.Equal(14f, walker.Position.Z, 3);
        Assert.False(shooter.IsFrozen);   // a stagger isn't a freeze

        Assert.NotEmpty(Run(field, 2f * cycle));
    }
}
