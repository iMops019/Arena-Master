using ArenaMaster.Game.Camp;
using ArenaMaster.Game.Combat;
using ArenaMaster.Game.Items;
using ArenaMaster.Game.Progression;
using ArenaMaster.Game.Ranger;
using ArenaMaster.Game.Ui;
using Silk.NET.Maths;

namespace ArenaMaster.Game.Tests;

public class ProfileStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"arenamaster-test-{Guid.NewGuid():N}");

    private string ProfilePath => Path.Combine(_directory, "profile.json");

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void AMissingFile_IsANewPlayer()
    {
        var profile = ProfileStore.Load(ProfilePath);

        Assert.Empty(profile.Stash);
        Assert.Empty(profile.Loadout);
    }

    [Fact]
    public void AProfile_ComesBackAsItWasSaved()
    {
        var profile = new Profile();
        profile.AddToStash("whetstone", 3);
        profile.Loadout.Add("whetstone");
        var tree = profile.Tree(SharpshooterTree.ClassId, SharpshooterTree.TreeId);
        tree.Experience = 1234;
        tree.Ranks["honed"] = 2;

        ProfileStore.Save(profile, ProfilePath);
        var loaded = ProfileStore.Load(ProfilePath);

        Assert.Equal(3, loaded.CountOf("whetstone"));
        Assert.Equal(new[] { "whetstone" }, loaded.Loadout);
        var loadedTree = loaded.Tree(SharpshooterTree.ClassId, SharpshooterTree.TreeId);
        Assert.Equal(1234, loadedTree.Experience);
        Assert.Equal(2, loadedTree.Ranks["honed"]);
    }

    [Fact]
    public void AnUnreadableFile_IsSetAside_NotLost()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(ProfilePath, "{ this is not json");

        var profile = ProfileStore.Load(ProfilePath);

        Assert.Empty(profile.Stash);
        Assert.True(File.Exists(ProfilePath + ".bad"));
    }
}

public class TreeProgressTests
{
    private static TreeProgress Fresh(long experience = 0) => new(SharpshooterTree.Tree, new TreeSave { Experience = experience });

    private static TreeNode Node(string id) => SharpshooterTree.Tree.Node(id);

    [Fact]
    public void Levels_FollowTheCurve_AndStopAtTheCap()
    {
        Assert.Equal(1, Fresh().Level);
        Assert.Equal(2, Fresh(TreeProgress.RequiredFor(1)).Level);
        Assert.Equal(1, Fresh(TreeProgress.RequiredFor(1) - 1).Level);
        Assert.Equal(10, Fresh(TreeProgress.TotalFor(10)).Level);
        Assert.Equal(TreeProgress.MaxLevel, Fresh(long.MaxValue / 2).Level);
    }

    [Fact]
    public void ItStartsWithTwoChoices_AndOnePoint()
    {
        var tree = Fresh();

        Assert.Equal(1, tree.FreePoints);
        var open = SharpshooterTree.Tree.Nodes.Where(n => tree.WhyNotTake(n) is null).Select(n => n.Id).ToList();
        Assert.Equal(new[] { "steady", "honed" }, open);
    }

    [Fact]
    public void ANodeNeedsItsTiersLevel_AndARankedParent()
    {
        var tree = Fresh(TreeProgress.TotalFor(3));   // level 3: tier 2 open, three points

        Assert.NotNull(tree.WhyNotTake(Node(SharpshooterTree.ChainProjectiles)));   // no parent taken yet
        Assert.True(tree.Take(Node("honed")));
        Assert.Null(tree.WhyNotTake(Node(SharpshooterTree.ChainProjectiles)));      // tier 2, and Honed Draw leads to it
        Assert.NotNull(tree.WhyNotTake(Node("ricochet")));                           // tier 3 needs level 6
    }

    [Fact]
    public void PointsRunOut_AndARefundGivesThemBack()
    {
        var tree = Fresh(TreeProgress.TotalFor(2));
        var honed = Node("honed");

        Assert.True(tree.Take(honed));
        Assert.True(tree.Take(honed));
        Assert.False(tree.Take(honed));   // two levels, two points
        Assert.True(tree.Refund(honed));
        Assert.Equal(1, tree.FreePoints);
    }

    [Fact]
    public void ANodeHoldingUpOthers_CantBeEmptied_UntilTheyGo()
    {
        var tree = Fresh(TreeProgress.TotalFor(3));
        tree.Take(Node("honed"));
        tree.Take(Node(SharpshooterTree.ChainProjectiles));

        Assert.NotNull(tree.WhyNotRefund(Node("honed")));
        Assert.True(tree.Refund(Node(SharpshooterTree.ChainProjectiles)));
        Assert.True(tree.Refund(Node("honed")));
    }

    [Fact]
    public void AnotherParent_KeepsANodeConnected()
    {
        var tree = Fresh(TreeProgress.TotalFor(4));
        tree.Take(Node("steady"));
        tree.Take(Node("honed"));
        tree.Take(Node("nock"));   // Rapid Nock hangs off both starters

        Assert.Null(tree.WhyNotRefund(Node("steady")));
    }

    [Fact]
    public void EveryNode_IsPlayable()
    {
        Assert.All(SharpshooterTree.Tree.Nodes, n => Assert.True(n.Playable, $"{n.Name} is still marked coming soon"));
    }

    [Fact]
    public void ANodeNotYetInTheGame_CantBeTaken()
    {
        var tree = new TreeProgress(SharpshooterTree.Tree with
        {
            Nodes = SharpshooterTree.Tree.Nodes.Select(n => n.Id == "honed" ? n with { Playable = false } : n).ToList(),
        }, new TreeSave());

        Assert.Contains("Coming soon", tree.WhyNotTake(tree.Tree.Node("honed")));
    }

    [Fact]
    public void ChainProjectiles_IsATierTwoMajor()
    {
        var chain = Node(SharpshooterTree.ChainProjectiles);

        Assert.Equal(2, chain.Tier);
        Assert.True(chain.Major);
        Assert.Equal(3, SharpshooterTree.Tree.LevelFor(chain.Tier));
    }

    [Fact]
    public void TheTree_IsWellFormed()
    {
        var nodes = SharpshooterTree.Tree.Nodes;
        Assert.InRange(nodes.Count, 30, 40);
        Assert.Equal(nodes.Count, nodes.Select(n => n.Id).Distinct().Count());
        Assert.All(nodes, n => Assert.All(n.Parents, p => Assert.True(SharpshooterTree.Tree.Node(p).Tier < n.Tier, $"{n.Id} has a parent at or above its tier")));
        Assert.All(nodes.Where(n => n.Tier > 1), n => Assert.NotEmpty(n.Parents));
        Assert.All(nodes.Where(n => !n.Major), n => Assert.Contains("{", n.Text));   // every minor names its numbers
        Assert.Equal(2, nodes.Count(n => n.Tier == 1));
    }

    [Fact]
    public void Descriptions_ShowTheNumbersAtTheirRank()
    {
        Assert.Equal("+30% damage and +30% attack speed.", Node("honed").Describe(3));
        Assert.Equal("+10% damage and +10% attack speed.", Node("honed").Describe(0));   // an untaken node reads as one rank
    }
}

public class SharpshooterEffectTests
{
    private static RangerStats With(params (string Id, int Ranks)[] ranks) =>
        new() { Tree = SharpshooterBonuses.From(ranks.ToDictionary(r => r.Id, r => r.Ranks)) };

    [Fact]
    public void MinorNodes_AddToTheRangersNumbers()
    {
        var stats = With(("honed", 5), ("steady", 2));

        Assert.Equal(RangerStats.BaseDamage * 1.5f, stats.Damage, 3);
        Assert.Equal(RangerStats.BaseFireInterval / 1.5f, stats.FireInterval, 4);
        Assert.Equal(RangerStats.BaseCritChance + 0.08f, stats.CritChance, 4);
        Assert.Equal(RangerStats.BaseCritMultiplier + 0.2f, stats.CritMultiplier, 4);
    }

    [Fact]
    public void ChainProjectiles_AndChainReach_GiveChains()
    {
        Assert.Equal(0, new RangerStats().Chains);
        Assert.Equal(1, With((SharpshooterTree.ChainProjectiles, 1)).Chains);
        Assert.Equal(3, With((SharpshooterTree.ChainProjectiles, 1), ("reach", 2)).Chains);
    }

    [Fact]
    public void TwinShot_AddsAnArrow_ForLessDamageEach()
    {
        var stats = With((SharpshooterTree.TwinShot, 1));

        Assert.Equal(2, stats.ArrowsPerShot);
        Assert.Equal(RangerStats.BaseDamage * RangerStats.TwinShotDamage, stats.Damage, 3);
    }

    [Fact]
    public void Deadeye_MakesCritsTriple_ForLessCritChance()
    {
        var stats = With(("steady", 2), (SharpshooterTree.Deadeye, 1));

        Assert.Equal(3f + 0.2f, stats.CritMultiplier, 4);
        Assert.Equal(RangerStats.BaseCritChance + 0.08f - 0.05f, stats.CritChance, 4);
    }

    [Fact]
    public void HardenedLeathers_CutsDamageTaken_AndMomentum_AddsAttackSpeedPerKill()
    {
        var stats = With(("hardened", 2), ("momentum", 3));
        stats.MomentumStacks = 10;

        Assert.Equal(0.95f * 0.95f, stats.DamageTaken, 4);
        Assert.Equal(RangerStats.BaseFireInterval / 1.3f, stats.FireInterval, 4);   // 3% x 10 kills
    }

    [Fact]
    public void DashNodes_ChangeTheDash()
    {
        var stats = With(("recovery", 3), ("windrunner", 2));

        Assert.Equal(RangerStats.BaseDashCooldown / 1.24f, stats.DashCooldown, 4);
        Assert.Equal(RangerStats.BaseDashSpeed * 1.2f, stats.DashSpeed, 3);
    }
}

public class ChainingAndHitRuleTests
{
    private static float? FlatGround(float x, float z) => MathF.Abs(x) < 200f && MathF.Abs(z) < 200f ? 0f : null;

    private static EnemyField QuietField() => new(new Random(1)) { TargetCount = 0 };

    private static List<ArrowHit> Fly(RangerArrows arrows, EnemyField enemies, float seconds = 1f)
    {
        var hits = new List<ArrowHit>();
        for (float t = 0f; t < seconds; t += 1f / 60f)
        {
            hits.AddRange(arrows.Update(1f / 60f, enemies, FlatGround, new List<Arrow>()));
        }

        return hits;
    }

    [Fact]
    public void AChainingArrow_JumpsToTheNearestOtherEnemy()
    {
        var enemies = QuietField();
        var first = enemies.Spawn(new Vector3D<float>(10f, 0f, 0f));
        var near = enemies.Spawn(new Vector3D<float>(10f, 0f, 6f));   // off the arrow's line, 6 m from the first
        enemies.Spawn(new Vector3D<float>(10f, 0f, -9.5f));            // further away
        var arrows = new RangerArrows(new Random(1));
        arrows.Fire(new Vector3D<float>(0f, 1f, 0f), new Vector3D<float>(1f, 0f, 0f), speed: 50f, range: 60f, damage: 5f, chains: 1);

        var hits = Fly(arrows, enemies);

        Assert.Equal(new[] { first, near }, hits.Select(h => h.Enemy));
        Assert.Empty(arrows.Arrows);   // out of chains: it stopped in the second
    }

    [Fact]
    public void AChainingArrow_WithNoOneInRange_Stops()
    {
        var enemies = QuietField();
        enemies.Spawn(new Vector3D<float>(10f, 0f, 0f));
        enemies.Spawn(new Vector3D<float>(10f, 0f, 30f));
        var arrows = new RangerArrows(new Random(1));
        arrows.Fire(new Vector3D<float>(0f, 1f, 0f), new Vector3D<float>(1f, 0f, 0f), speed: 50f, range: 60f, damage: 5f, chains: 2, chainRange: 10f);

        Assert.Single(Fly(arrows, enemies));
    }

    [Fact]
    public void ChainedHits_DealMore_WithRicochetAndCascade()
    {
        var enemies = QuietField();
        var target = enemies.Spawn(new Vector3D<float>(0f, 0f, 0f));
        var arrow = new Arrow { Damage = 10f, ChainIndex = 2, Rules = new HitRules(0f, 0f, 0f, ChainedDamage: 0.3f, CascadeDamage: 0.1f) };
        target.Health = target.MaxHealth * 0.5f;   // not "healthy"

        Assert.Equal(10f * 1.3f * 1.1f, RangerArrows.DamageAgainst(arrow, target), 3);
    }

    [Fact]
    public void ElitesAndHealthyEnemies_TakeTheirBonuses()
    {
        var enemies = QuietField();
        var brute = enemies.Spawn(Vector3D<float>.Zero, EnemyKind.Brute);
        var arrow = new Arrow { Damage = 10f, Rules = new HitRules(EliteDamage: 0.5f, HealthyDamage: 0.2f, 0f, 0f, 0f) };

        Assert.Equal(10f * 1.5f * 1.2f, RangerArrows.DamageAgainst(arrow, brute), 3);
    }

    [Fact]
    public void ACertainExecute_FinishesAWoundedEnemy_ButNeverABoss()
    {
        var enemies = QuietField();
        var ghoul = enemies.Spawn(new Vector3D<float>(8f, 0f, 0f));
        ghoul.Health = ghoul.MaxHealth * 0.25f;   // one small hit takes it under 20%
        var king = enemies.Spawn(new Vector3D<float>(8f, 0f, 20f), EnemyKind.HollowKing);
        king.Health = 30f;

        var arrows = new RangerArrows(new Random(1));
        var rules = new HitRules(0f, 0f, ExecuteChance: 1f, 0f, 0f);
        arrows.Fire(new Vector3D<float>(0f, 1f, 0f), new Vector3D<float>(1f, 0f, 0f), 50f, 60f, damage: 2f, rules: rules);
        arrows.Fire(new Vector3D<float>(0f, 1f, 20f), new Vector3D<float>(1f, 0f, 0f), 50f, 60f, damage: 2f, rules: rules);
        Fly(arrows, enemies);

        Assert.False(ghoul.IsAlive);
        Assert.True(king.IsAlive);
    }
}

public class LoadoutTests
{
    private static Profile WithItems(params (string Id, int Count)[] items)
    {
        var profile = new Profile();
        foreach (var (id, count) in items)
        {
            profile.AddToStash(id, count);
        }

        return profile;
    }

    [Fact]
    public void AtMostFiveDifferentItems_CanBeChosen()
    {
        var ids = ItemCatalog.All.Take(6).Select(i => i.Id).ToArray();
        var profile = WithItems(ids.Select(id => (id, 1)).ToArray());

        foreach (var id in ids.Take(5))
        {
            Assert.True(Loadout.Toggle(profile, id));
        }

        Assert.False(Loadout.Toggle(profile, ids[5]));
        Assert.True(Loadout.Toggle(profile, ids[0]));   // taking one out makes room
        Assert.True(Loadout.Toggle(profile, ids[5]));
        Assert.Equal(Loadout.MaxItems, profile.Loadout.Count);
    }

    [Fact]
    public void EveryCopyOwned_ComesAlong()
    {
        var profile = WithItems(("whetstone", 4), ("troll_heart", 1), ("old_tome", 2));
        Loadout.Toggle(profile, "whetstone");
        Loadout.Toggle(profile, "troll_heart");

        var brought = Loadout.ItemsToBring(profile).ToList();

        Assert.Equal(4, brought.Count(i => i.Id == "whetstone"));
        Assert.Equal(1, brought.Count(i => i.Id == "troll_heart"));
        Assert.DoesNotContain(brought, i => i.Id == "old_tome");
    }

    [Fact]
    public void ASavedLoadout_DropsWhatIsNoLongerOwned()
    {
        var profile = WithItems(("whetstone", 1));
        profile.Loadout.AddRange(new[] { "whetstone", "troll_heart", "not_an_item", "whetstone" });

        Loadout.Sanitize(profile);

        Assert.Equal(new[] { "whetstone" }, profile.Loadout);
    }
}

public class CampLayoutTests
{
    [Fact]
    public void EachStation_IsFoundWithinReach_AndNothingFromTheFire()
    {
        foreach (var (station, _, offset, _) in CampLayout.Stations)
        {
            var at = CampLayout.Centre + offset;
            var near = CampLayout.StationNear(new Vector3D<float>(at.X + 1f, 0f, at.Y));
            Assert.Equal(station, near?.Station);
        }

        Assert.Null(CampLayout.StationNear(new Vector3D<float>(CampLayout.Centre.X, 0f, CampLayout.Centre.Y)));
    }

    [Fact]
    public void CampAndTheRunStart_AreFarApart()
    {
        Assert.True(Vector2D.Distance(CampLayout.Centre, CampLayout.RunStart) > 150f);
        Assert.True(CampLayout.InCamp(new Vector3D<float>(CampLayout.Spawn.X, 0f, CampLayout.Spawn.Y)));
        Assert.False(CampLayout.InCamp(new Vector3D<float>(CampLayout.RunStart.X, 0f, CampLayout.RunStart.Y)));
    }
}
