using ArenaMaster.Game.Combat;
using ArenaMaster.Game.Items;
using ArenaMaster.Game.Mage;
using ArenaMaster.Game.Progression;
using Silk.NET.Maths;

namespace ArenaMaster.Game.Tests;

internal static class PyroTesting
{
    /// <summary>Fire Barrage stats from <paramref name="ranks"/> in the Pyromancy tree, with crits turned off so the numbers are exact.</summary>
    public static MageStats With(params (string Id, int Ranks)[] ranks) => new()
    {
        Element = MageElement.Fire,
        Pyro = PyromancyBonuses.From(ranks.ToDictionary(r => r.Id, r => r.Ranks)),
        Items = new ItemBonuses { CritChance = -1f },
    };

    /// <summary>The same, firing one bolt a barrage (at the best target).</summary>
    public static MageStats OneBolt(params (string Id, int Ranks)[] ranks)
    {
        var stats = With(ranks);
        stats.Pyro.Projectiles = 1 - MageStats.BaseProjectiles;
        return stats;
    }

    /// <summary>A Mage on the Pyromancy tree with <paramref name="ranks"/>, starting a run (crits off).</summary>
    public static MageClass Mage(PlayerHealth health, params (string Id, int Ranks)[] ranks)
    {
        var mage = new MageClass(new Random(1));
        mage.ChooseTree(PyromancyTree.TreeId);
        mage.UseTree(ranks.ToDictionary(r => r.Id, r => r.Ranks));
        mage.BeginRun(new ItemBonuses { CritChance = -1f }, health);
        return mage;
    }

    /// <summary>Runs the fire alone (no casting) for <paramref name="seconds"/>, from a Mage at the origin.</summary>
    public static List<FrostHit> Burn(Flames flames, MageStats stats, EnemyField field, float seconds)
    {
        var hits = new List<FrostHit>();
        for (float t = 0f; t < seconds - 1e-4f; t += MageTesting.Step)
        {
            flames.Update(MageTesting.Step, Vector3D<float>.Zero, stats, field, hits);
        }

        return hits;
    }
}

public class PyromancyTreeTests
{
    private static TreeNode Node(string id) => PyromancyTree.Tree.Node(id);

    [Fact]
    public void TheTree_IsWellFormed()
    {
        var tree = PyromancyTree.Tree;
        var nodes = tree.Nodes;
        Assert.Equal("pyromancy", tree.Id);
        Assert.Equal("Pyromancy", tree.Name);
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
        Assert.Equal(new[] { 1, 3, 6, 10, 15, 21, 28 }, tree.TierLevels);
        Assert.Equal(new[] { "Ignite", "Combustion", "Heat" }, tree.Lanes.Select(l => l.Name));

        var starts = nodes.Where(n => n.Tier == 1).ToList();
        Assert.Equal(new[] { "Kindling", "Quick Flame" }, starts.Select(n => n.Name));
        Assert.All(starts, n => Assert.False(n.Major));
        Assert.All(starts, n => Assert.Equal(5, n.MaxRanks));

        Assert.InRange(nodes.Count(n => n.Major), FrostTree.Tree.Nodes.Count(n => n.Major) - 2, FrostTree.Tree.Nodes.Count(n => n.Major) + 2);
    }

    [Fact]
    public void NodesInATier_DontOverlap()
    {
        foreach (var tier in PyromancyTree.Tree.Nodes.GroupBy(n => n.Tier))
        {
            var xs = tier.Select(n => n.X).OrderBy(x => x).ToList();
            for (int i = 1; i < xs.Count; i++)
            {
                Assert.True(xs[i] - xs[i - 1] >= 100f, $"tier {tier.Key}: nodes at {xs[i - 1]} and {xs[i]} overlap");
            }
        }
    }

    [Fact]
    public void EachLane_EndsInACapstone()
    {
        var capstones = PyromancyTree.Tree.Nodes.Where(n => n.Tier == 7).ToList();
        Assert.All(capstones, n => Assert.True(n.Major));
        Assert.Equal(PyromancyTree.Tree.Lanes.Select(l => l.Name).OrderBy(n => n), capstones.Select(n => n.Lane).OrderBy(n => n));
        Assert.Equal("Meteor", Node(PyromancyTree.Meteor).Name);
        Assert.Equal("Combustion", Node(PyromancyTree.Meteor).Lane);
        Assert.Equal("Heat", Node(PyromancyTree.Inferno).Lane);
        Assert.Equal("Ignite", Node(PyromancyTree.LivingFlame).Lane);
    }

    [Fact]
    public void HeatAndCombustion_ComeEarly()
    {
        Assert.InRange(Node(PyromancyTree.Heat).Tier, 2, 3);
        Assert.InRange(Node(PyromancyTree.Combustion).Tier, 2, 3);
        Assert.Equal("Heat", Node(PyromancyTree.Heat).Lane);
        Assert.Equal("Combustion", Node(PyromancyTree.Combustion).Lane);
        Assert.Contains(Node(PyromancyTree.FireWalk).Lane, new[] { "Heat", "Combustion" });
        Assert.Equal("Ignite", Node(PyromancyTree.Wildfire).Lane);
        Assert.Equal("Heat", Node(PyromancyTree.FlameWard).Lane);
    }

    [Fact]
    public void TheHeatOnlyMajors_CanOnlyBeReachedThroughHeat()
    {
        // Cauterise and the Inferno do nothing without heat, so every way up to them runs through the Heat major.
        var tree = PyromancyTree.Tree;
        bool OnlyThroughHeat(string id) =>
            id == PyromancyTree.Heat || (tree.Node(id).Parents.Count > 0 && tree.Node(id).Parents.All(OnlyThroughHeat));

        Assert.True(OnlyThroughHeat(PyromancyTree.Cauterise));
        Assert.True(OnlyThroughHeat(PyromancyTree.Inferno));
    }

    [Fact]
    public void EveryNode_ChangesTheBonuses()
    {
        var options = new System.Text.Json.JsonSerializerOptions { IncludeFields = true };
        var none = System.Text.Json.JsonSerializer.Serialize(PyromancyBonuses.From(new Dictionary<string, int>()), options);
        foreach (var node in PyromancyTree.Tree.Nodes)
        {
            var one = System.Text.Json.JsonSerializer.Serialize(PyromancyBonuses.From(new Dictionary<string, int> { [node.Id] = 1 }), options);
            Assert.True(one != none, $"{node.Name} does nothing");
        }
    }

    [Fact]
    public void Ranks_AddUpIntoTheBonuses()
    {
        var b = PyromancyBonuses.From(new Dictionary<string, int>
        {
            ["kindling"] = 5, ["tinder"] = 2, ["scald"] = 3, ["fanned"] = 1, ["twinflame"] = 2, ["flamevolley"] = 1, ["coolhead"] = 3, ["stoked"] = 2,
            ["ashenskin"] = 2, ["warmblood"] = 2, [PyromancyTree.Heat] = 1,
        });

        Assert.Equal(0.5f + 0.12f, b.BurnDamage, 4);
        Assert.Equal(1f + 1f, b.BurnDuration, 4);
        Assert.Equal(0.36f + 0.06f, b.FireDamage, 4);
        Assert.Equal(3, b.Projectiles);
        Assert.Equal(3f, b.HeatCooling, 4);
        Assert.Equal(0.002f, b.HeatDamage, 5);
        Assert.Equal(20f + 30f, b.MaxHealth, 4);
        Assert.Equal(0.96f * 0.96f, b.DamageTaken, 4);
        Assert.True(b.Heat);
        Assert.False(b.Combustion);

        var stats = new MageStats { Element = MageElement.Fire, Pyro = b };
        Assert.Equal(MageStats.BaseProjectiles + 3, stats.Projectiles);
        Assert.Equal(MageStats.BaseBurnDuration + 2f, stats.BurnDuration, 4);
        Assert.Equal(MageStats.BaseBurnShare * 1.62f, stats.BurnShare, 4);
        Assert.Equal(MageStats.BaseHeatCooling + 3f, stats.HeatCooling, 4);
        Assert.Equal(MageStats.BaseMaxHealth + 50f, stats.MaxHealth, 4);
        Assert.Equal(MageStats.BaseBoltDamage * 1.42f, stats.BoltDamage, 3);
    }

    [Fact]
    public void Descriptions_ShowTheNumbersAtTheirRank()
    {
        Assert.Equal("+30% burn damage, and burns last 0.6 s longer.", Node("kindling").Describe(3));
        Assert.Equal("+16% cast speed and +16% projectile speed.", Node("quickflame").Describe(2));
        Assert.Equal("+0.2% more damage for every point of heat.", Node("stoked").Describe(2));
    }
}

public class FireBarrageTests
{
    [Fact]
    public void TheMage_HasBothTrees_PyromancySecond()
    {
        var mage = new MageClass(new Random(1));
        Assert.Equal(new[] { FrostTree.Tree, PyromancyTree.Tree }, mage.Trees);
        Assert.Equal(FrostTree.Tree, mage.Tree);
    }

    [Fact]
    public void TheBarrage_FollowsTheActiveTree_AndOnlyTheActiveTreeCounts()
    {
        var mage = new MageClass(new Random(1));
        var ranks = new Dictionary<string, int>
        {
            [FrostTree.FrostShield] = 1, ["coldhands"] = 5, [FrostTree.IceBlock] = 1, ["kindling"] = 5, [PyromancyTree.Heat] = 1, ["scald"] = 5,
        };

        mage.ChooseTree(PyromancyTree.TreeId);
        mage.UseTree(ranks);
        Assert.True(mage.Stats.Fire);
        Assert.True(mage.Stats.Pyro.Heat);
        Assert.False(mage.Stats.Tree.FrostShield);
        Assert.Equal(0f, mage.Stats.Tree.ColdDamage);
        Assert.False(mage.KeepsOwnBarrier);   // no Frost Shield: an item's ward forms as for anyone
        Assert.StartsWith("A staff of fire", mage.Summary);
        Assert.Equal(MageStats.BaseBoltDamage * 1.6f, mage.Stats.BoltDamage, 3);   // Scald, not Cold Hands

        var health = new PlayerHealth(100f);
        mage.BeginRun(new ItemBonuses(), health);
        Assert.Equal(0, health.LastStands);   // no Ice Block

        mage.ChooseTree(FrostTree.TreeId);
        mage.UseTree(ranks);
        Assert.False(mage.Stats.Fire);
        Assert.False(mage.Stats.Pyro.Heat);
        Assert.Equal(0f, mage.Stats.Pyro.BurnDamage);
        Assert.True(mage.Stats.Tree.FrostShield);
        Assert.True(mage.KeepsOwnBarrier);
        Assert.StartsWith("A staff of ice", mage.Summary);
        Assert.Null(mage.Meter);

        mage.ChooseTree("no such tree");
        Assert.Equal(FrostTree.Tree, mage.Tree);
        Assert.False(mage.Stats.Fire);
    }

    [Fact]
    public void AFireBolt_SetsItsTargetBurning_AndNeverChills()
    {
        var field = MageTesting.QuietField();
        var target = MageTesting.Sturdy(field, 8f);
        var stats = PyroTesting.OneBolt();
        var barrage = new FrostBarrage(new Random(1));
        barrage.BeginBarrage(stats);

        var hit = Assert.Single(MageTesting.Cast(barrage, stats, field, 0.7f), h => h.Source == FrostSource.Bolt);

        Assert.Equal(MageStats.BaseBoltDamage, hit.Damage, 3);
        Assert.False(target.IsChilled);
        Assert.True(barrage.Flames.IsBurning(target));
    }

    [Fact]
    public void ABurn_Does30PercentOfTheHit_OverThreeSeconds_ThenGoesOut()
    {
        var field = MageTesting.QuietField();
        var target = MageTesting.Sturdy(field, 8f);
        var stats = PyroTesting.With();
        var flames = new Flames();

        flames.Ignite(target, 20f, stats);
        var hits = PyroTesting.Burn(flames, stats, field, 3.2f);

        Assert.All(hits, h => Assert.Equal(FrostSource.Burn, h.Source));
        Assert.All(hits, h => Assert.False(h.Crit));
        Assert.Equal(12, hits.Count);   // a tick every quarter second
        Assert.Equal(20f * MageStats.BaseBurnShare, hits.Sum(h => h.Damage), 3);
        Assert.False(flames.IsBurning(target));
    }

    [Fact]
    public void ALongerBurn_DoesMoreInAll_AtTheSameRate()
    {
        var field = MageTesting.QuietField();
        var target = MageTesting.Sturdy(field, 8f);
        var stats = PyroTesting.With(("tinder", 2));   // +1 s
        var flames = new Flames();

        flames.Ignite(target, 20f, stats);
        var hits = PyroTesting.Burn(flames, stats, field, 4.2f);

        Assert.Equal(16, hits.Count);
        Assert.Equal(20f * MageStats.BaseBurnShare * 4f / 3f, hits.Sum(h => h.Damage), 3);
    }

    [Fact]
    public void ANewBurn_ReplacesAWeakerOne_AndStartsItsTimeAgain()
    {
        var field = MageTesting.QuietField();
        var target = MageTesting.Sturdy(field, 8f);
        var stats = PyroTesting.With();
        var flames = new Flames();
        flames.Ignite(target, 10f, stats);
        PyroTesting.Burn(flames, stats, field, 1f);
        float rate = flames.Burns[target].PerSecond;
        int left = flames.Burns[target].TicksLeft;

        flames.Ignite(target, 5f, stats);   // weaker: nothing changes
        Assert.Equal(rate, flames.Burns[target].PerSecond);
        Assert.Equal(left, flames.Burns[target].TicksLeft);

        flames.Ignite(target, 30f, stats);   // stronger: takes over, its time from the start
        Assert.Equal(30f * MageStats.BaseBurnShare / MageStats.BaseBurnDuration, flames.Burns[target].PerSecond, 4);
        Assert.Equal(12, flames.Burns[target].TicksLeft);
    }

    [Fact]
    public void Burns_AreDamageOverTime_AndTakeItsItems()
    {
        var field = MageTesting.QuietField();
        var target = MageTesting.Sturdy(field, 8f);
        var stats = PyroTesting.With();
        stats.Items = new ItemBonuses { CritChance = -1f, DotDamage = 0.5f, DotMultiplier = 1.2f };
        var flames = new Flames();

        flames.Ignite(target, 10f, stats);

        Assert.Equal(10f * MageStats.BaseBurnShare * 1.5f * 1.2f / MageStats.BaseBurnDuration, flames.Burns[target].PerSecond, 4);
    }

    [Fact]
    public void Burns_ShowAsDamageOverTime()
    {
        var field = MageTesting.QuietField();
        var target = MageTesting.Sturdy(field, 30f);   // out of the barrage's reach
        var health = new PlayerHealth(100f);
        var mage = PyroTesting.Mage(health);
        var numbers = new DamageNumbers();
        mage.Barrage.Flames.Ignite(target, 40f, mage.Stats);

        for (float t = 0f; t < 1.6f; t += 0.05f)
        {
            mage.Fight(0.05f, Vector3D<float>.Zero, MageTesting.Aim, stunned: true, field, MageTesting.FlatGround, health, numbers);
            numbers.Update(0.05f);
        }

        Assert.NotEmpty(numbers.Shown);
        Assert.All(numbers.Shown, n => Assert.True(n.OverTime));
    }

    [Fact]
    public void AnItemsChill_StillComes_WithTheFireBarrage()
    {
        var field = MageTesting.QuietField();
        field.HitEffects = new HitEffects(ChillSlow: 0.1f, ChillSeconds: 1f);
        var target = MageTesting.Sturdy(field, 8f);
        var stats = PyroTesting.OneBolt();
        var barrage = new FrostBarrage(new Random(1));
        barrage.BeginBarrage(stats);

        MageTesting.Cast(barrage, stats, field, 0.7f);

        Assert.True(target.IsChilled);
        Assert.Equal(0.1f, target.ChillSlow, 4);   // the item's, not the Mage's own 20%
    }

    [Fact]
    public void TheFrostBarrage_StillChills_AndNeverBurns()
    {
        var field = MageTesting.QuietField();
        var target = MageTesting.Sturdy(field, 8f);
        var stats = MageTesting.With();
        stats.Tree.Projectiles = 1 - MageStats.BaseProjectiles;
        var barrage = new FrostBarrage(new Random(1));
        barrage.BeginBarrage(stats);

        MageTesting.Cast(barrage, stats, field, 0.7f);

        Assert.True(target.IsChilled);
        Assert.Empty(barrage.Flames.Burns);
    }

    [Fact]
    public void Crates_DontBurn()
    {
        var field = MageTesting.QuietField();
        var crate = field.Spawn(new Vector3D<float>(5f, 0f, 0f), EnemyKind.Crate);
        var flames = new Flames();

        flames.Ignite(crate, 10f, PyroTesting.With());

        Assert.Empty(flames.Burns);
    }
}

public class HeatTests
{
    [Fact]
    public void EveryCast_BuildsHeat_WhichCoolsAndAddsDamage()
    {
        var field = MageTesting.QuietField();
        var stats = PyroTesting.With((PyromancyTree.Heat, 1));
        var flames = new Flames();

        for (int i = 1; i <= 4; i++)
        {
            flames.Cast(Vector3D<float>.Zero, i, stats, field);
        }

        Assert.Equal(40f, flames.Heat, 3);
        Assert.Equal(40f, stats.Heat, 3);
        Assert.Equal(MageStats.BaseBoltDamage * 1.2f, stats.BoltDamage, 3);

        PyroTesting.Burn(flames, stats, field, 2f);
        Assert.Equal(40f - 2f * MageStats.BaseHeatCooling, flames.Heat, 2);
    }

    [Fact]
    public void WithoutTheMajor_ThereIsNoHeat()
    {
        var stats = PyroTesting.With();
        var flames = new Flames();

        flames.Cast(Vector3D<float>.Zero, 1, stats, MageTesting.QuietField());

        Assert.Equal(0f, flames.Heat);
        Assert.Equal(MageStats.BaseBoltDamage, stats.BoltDamage, 3);
    }

    [Fact]
    public void At100_TheMageOverheats_ThenTheHeatIsGone()
    {
        var field = MageTesting.QuietField();
        var stats = PyroTesting.With((PyromancyTree.Heat, 1));
        var flames = new Flames();
        for (int i = 1; i <= 10; i++)
        {
            flames.Cast(Vector3D<float>.Zero, i, stats, field);
        }

        Assert.False(flames.CanCast);
        Assert.Equal(MageStats.OverheatSeconds, flames.OverheatLeft, 3);

        PyroTesting.Burn(flames, stats, field, MageStats.OverheatSeconds - 0.1f);
        Assert.False(flames.CanCast);
        Assert.Equal(MageStats.MaxHeat, flames.Heat);   // it doesn't cool through an overheat

        PyroTesting.Burn(flames, stats, field, 0.2f);
        Assert.True(flames.CanCast);
        Assert.Equal(0f, flames.Heat);
    }

    [Fact]
    public void AnOverheat_HoldsTheBarrage()
    {
        var field = MageTesting.QuietField();
        MageTesting.Sturdy(field, 15f);
        var stats = PyroTesting.With((PyromancyTree.Heat, 1));
        var barrage = new FrostBarrage(new Random(1));
        for (int i = 1; i <= 10; i++)
        {
            barrage.Flames.Cast(Vector3D<float>.Zero, i, stats, field);   // overheated
        }

        MageTesting.Cast(barrage, stats, field, 1f);
        Assert.Equal(0, barrage.Barrages);   // past the first barrage's time, but held

        MageTesting.Cast(barrage, stats, field, 0.6f + FrostBarrage.FirstBarrage);   // the overheat over, its clock runs again
        Assert.Equal(1, barrage.Barrages);
    }

    [Fact]
    public void Blinking_VentsTheHeat_EvenInAnOverheat()
    {
        var health = new PlayerHealth(100f);
        var mage = PyroTesting.Mage(health, (PyromancyTree.Heat, 1));
        var field = MageTesting.QuietField();
        for (int i = 1; i <= 10; i++)
        {
            mage.Barrage.Flames.Cast(Vector3D<float>.Zero, i, mage.Stats, field);
        }

        Assert.Equal("OVERHEATED", mage.Meter!.Value.Label);

        mage.Blinked(Vector3D<float>.Zero, health);

        Assert.Equal(0f, mage.Barrage.Flames.Heat);
        Assert.True(mage.Barrage.Flames.CanCast);
        Assert.Equal(0f, mage.Stats.Heat);
        Assert.Equal(health.Max, health.Current);   // no Cauterise
    }

    [Fact]
    public void TheMeter_ShowsTheHeat_OnlyWithTheHeatMajor()
    {
        var health = new PlayerHealth(100f);
        var mage = PyroTesting.Mage(health, (PyromancyTree.Heat, 1));
        for (int i = 1; i <= 3; i++)
        {
            mage.Barrage.Flames.Cast(Vector3D<float>.Zero, i, mage.Stats, MageTesting.QuietField());
        }

        var (label, fill) = mage.Meter!.Value;
        Assert.Equal("HEAT 30%", label);
        Assert.Equal(0.3f, fill, 3);
        Assert.Equal(("HEAT 30%", 0.3f), ((ArenaMaster.Game.Classes.IHeroClass)mage).Meter);   // what the HUD sees

        Assert.Null(PyroTesting.Mage(health).Meter);
    }

    [Fact]
    public void Cauterise_HealsWhatTheBlinkVents()
    {
        var health = new PlayerHealth(100f);
        var mage = PyroTesting.Mage(health, (PyromancyTree.Heat, 1), (PyromancyTree.Cauterise, 1));
        for (int i = 1; i <= 6; i++)
        {
            mage.Barrage.Flames.Cast(Vector3D<float>.Zero, i, mage.Stats, MageTesting.QuietField());
        }

        health.TakeDamage(40f);
        mage.Blinked(Vector3D<float>.Zero, health);

        Assert.Equal(MageStats.BaseMaxHealth - 40f + 60f / MageStats.CauteriseHeatPerHealth, health.Current, 3);
    }

    [Fact]
    public void Inferno_KeepsTheCastingGoing_AndBurnsEverythingNear()
    {
        var field = MageTesting.QuietField();
        var near = MageTesting.Sturdy(field, 3f);
        var far = MageTesting.Sturdy(field, 7f);
        var stats = PyroTesting.With((PyromancyTree.Heat, 1), (PyromancyTree.Inferno, 1));
        var flames = new Flames();
        for (int i = 1; i <= 10; i++)
        {
            flames.Cast(Vector3D<float>.Zero, i, stats, field);
        }

        Assert.True(flames.InInferno);
        Assert.True(flames.CanCast);

        var hits = PyroTesting.Burn(flames, stats, field, MageStats.OverheatSeconds + 0.1f).Where(h => h.Source == FrostSource.Inferno).ToList();

        Assert.Equal(3, hits.Count);   // twice a second for 1.5 s
        Assert.All(hits, h => Assert.Equal(near, h.Enemy));
        Assert.All(hits, h => Assert.Equal(MageStats.BaseBoltDamage * 1.5f, h.Damage, 3));   // a bolt's damage, at full heat
        Assert.DoesNotContain(far, flames.Burns.Keys);
        Assert.False(flames.InInferno);
        Assert.Equal(0f, flames.Heat);
    }

    [Fact]
    public void WithoutInferno_AnOverheatHurtsNothing()
    {
        var field = MageTesting.QuietField();
        MageTesting.Sturdy(field, 3f);
        var stats = PyroTesting.With((PyromancyTree.Heat, 1));
        var flames = new Flames();
        for (int i = 1; i <= 10; i++)
        {
            flames.Cast(Vector3D<float>.Zero, i, stats, field);
        }

        Assert.Empty(PyroTesting.Burn(flames, stats, field, 2f));
    }
}

public class PyromancyMajorTests
{
    [Fact]
    public void Combustion_BurstsABurningEnemyThatDies()
    {
        var field = MageTesting.QuietField();
        var burning = field.Spawn(new Vector3D<float>(10f, 0f, 0f));
        var beside = MageTesting.Sturdy(field, 11.5f);
        var far = MageTesting.Sturdy(field, 16f);
        var stats = PyroTesting.With((PyromancyTree.Combustion, 1));
        var flames = new Flames();
        flames.Ignite(burning, 5f, stats);

        field.Damage(burning, 1000f);   // killed by anything
        var hits = PyroTesting.Burn(flames, stats, field, 0.1f);

        var burst = Assert.Single(hits, h => h.Source == FrostSource.Combustion);
        Assert.Equal(beside, burst.Enemy);
        Assert.Equal(MageStats.BaseBoltDamage, burst.Damage, 3);
        Assert.True(flames.IsBurning(beside));
        Assert.DoesNotContain(hits, h => h.Enemy == far);
        Assert.Single(flames.Blasts);
    }

    [Fact]
    public void WithoutCombustion_ABurningEnemyJustDies()
    {
        var field = MageTesting.QuietField();
        var burning = field.Spawn(new Vector3D<float>(10f, 0f, 0f));
        MageTesting.Sturdy(field, 11.5f);
        var stats = PyroTesting.With();
        var flames = new Flames();
        flames.Ignite(burning, 5f, stats);

        field.Damage(burning, 1000f);

        Assert.Empty(PyroTesting.Burn(flames, stats, field, 0.5f));
        Assert.Equal(1, flames.BurningDeaths);
    }

    [Fact]
    public void Wildfire_SpreadsABurn_ToTheNearestEnemyNotBurning_OnceASecond()
    {
        var field = MageTesting.QuietField();
        var first = MageTesting.Sturdy(field, 10f);
        var near = MageTesting.Sturdy(field, 13f);
        var far = MageTesting.Sturdy(field, 10f, 6f);
        var stats = PyroTesting.With((PyromancyTree.Wildfire, 1));
        var flames = new Flames();
        flames.Ignite(first, 20f, stats);

        PyroTesting.Burn(flames, stats, field, 0.9f);
        Assert.False(flames.IsBurning(near));

        PyroTesting.Burn(flames, stats, field, 0.2f);
        Assert.True(flames.IsBurning(near));
        Assert.Equal(flames.Burns[first].PerSecond, flames.Burns[near].PerSecond, 4);

        PyroTesting.Burn(flames, stats, field, 1.5f);
        Assert.False(flames.IsBurning(far));   // 6 m from the one burning, 6.7 from the other
    }

    [Fact]
    public void WithoutWildfire_BurnsStayPut()
    {
        var field = MageTesting.QuietField();
        var first = MageTesting.Sturdy(field, 10f);
        var near = MageTesting.Sturdy(field, 12f);
        var stats = PyroTesting.With();
        var flames = new Flames();
        flames.Ignite(first, 20f, stats);

        PyroTesting.Burn(flames, stats, field, 2.5f);

        Assert.False(flames.IsBurning(near));
    }

    [Fact]
    public void FireWalk_LeavesALineOfFireAlongTheBlink()
    {
        var field = MageTesting.QuietField();
        var onTheLine = MageTesting.Sturdy(field, 2f, 0.5f);
        var offTheLine = MageTesting.Sturdy(field, 2f, 3f);
        var health = new PlayerHealth(100f);
        var mage = PyroTesting.Mage(health, (PyromancyTree.FireWalk, 1));
        var flames = mage.Barrage.Flames;

        mage.Blinked(Vector3D<float>.Zero, health);
        flames.ExtendLine(new Vector3D<float>(4.5f, 0f, 0f));
        flames.CloseLine();
        var line = Assert.Single(flames.Ground);
        Assert.True(line.IsLine);

        var hits = PyroTesting.Burn(flames, mage.Stats, field, MageStats.FireWalkSeconds + 0.2f).Where(h => h.Source == FrostSource.Ground).ToList();

        Assert.All(hits, h => Assert.Equal(onTheLine, h.Enemy));
        Assert.Equal(MageStats.BaseBoltDamage * MageStats.FireWalkSeconds, hits.Sum(h => h.Damage), 2);   // a bolt's damage a second
        Assert.True(flames.IsBurning(onTheLine));
        Assert.False(flames.IsBurning(offTheLine));
        Assert.Empty(flames.Ground);
    }

    [Fact]
    public void WithoutFireWalk_ABlinkLeavesNoFire()
    {
        var health = new PlayerHealth(100f);
        var mage = PyroTesting.Mage(health);

        mage.Blinked(Vector3D<float>.Zero, health);

        Assert.Empty(mage.Barrage.Flames.Ground);
    }

    [Fact]
    public void Fireball_BurstsAroundTheOneHit_SettingThemBurning()
    {
        var field = MageTesting.QuietField();
        var target = MageTesting.Sturdy(field, 8f);
        var beside = MageTesting.Sturdy(field, 9.2f, 0.5f);
        var far = MageTesting.Sturdy(field, 8f, 5f);
        var stats = PyroTesting.OneBolt((PyromancyTree.Fireball, 1));
        var barrage = new FrostBarrage(new Random(1));
        barrage.BeginBarrage(stats);

        var hits = MageTesting.Cast(barrage, stats, field, 0.7f);

        var burst = Assert.Single(hits, h => h.Source == FrostSource.Fireball);
        Assert.Equal(beside, burst.Enemy);
        Assert.Equal(MageStats.BaseBoltDamage * MageStats.BaseFireballShare, burst.Damage, 3);
        Assert.True(barrage.Flames.IsBurning(beside));
        Assert.False(barrage.Flames.IsBurning(far));
    }

    [Fact]
    public void Scorch_HitsBurningEnemiesHarder()
    {
        foreach (var (scorch, expected) in new[] { (true, 1f + MageStats.ScorchBonus), (false, 1f) })
        {
            var field = MageTesting.QuietField();
            var target = MageTesting.Sturdy(field, 8f);
            var stats = scorch ? PyroTesting.OneBolt((PyromancyTree.Scorch, 1)) : PyroTesting.OneBolt();
            var barrage = new FrostBarrage(new Random(1));
            barrage.Flames.Ignite(target, 1f, stats);
            barrage.BeginBarrage(stats);

            var bolt = Assert.Single(MageTesting.Cast(barrage, stats, field, 0.7f), h => h.Source == FrostSource.Bolt);

            Assert.Equal(MageStats.BaseBoltDamage * expected, bolt.Damage, 3);
        }
    }

    [Fact]
    public void FlameWard_BurnsTheNextBlowAway_ThenComesBack()
    {
        var field = MageTesting.QuietField();
        var attacker = field.Spawn(new Vector3D<float>(0.8f, 0f, 0f));
        attacker.Health = 10_000f;
        var health = new PlayerHealth(100f);
        var mage = PyroTesting.Mage(health, (PyromancyTree.FlameWard, 1));
        Assert.Equal(1f, mage.BlockChance);

        bool struck = false;
        for (float t = 0f; t < 3f && !struck; t += MageTesting.Step)
        {
            mage.Fight(MageTesting.Step, Vector3D<float>.Zero, MageTesting.Aim, stunned: true, field, MageTesting.FlatGround, health, new DamageNumbers());
            health.Update(MageTesting.Step);
            field.Update(MageTesting.Step, new PlayerTarget(Vector3D<float>.Zero, true, health, new PlayerCondition(), mage.BlockChance), MageTesting.FlatGround);
            mage.AfterBlows(Vector3D<float>.Zero, field, health, new DamageNumbers());
            struck = field.Strikes.Count > 0;
        }

        Assert.True(struck);
        Assert.Equal(health.Max, health.Current);   // burned away
        Assert.True(mage.Barrage.Flames.IsBurning(attacker));
        Assert.Equal("FLAME WARD", mage.Status);
        Assert.Equal(0f, mage.BlockChance);   // spent: the next blow lands

        for (float t = 0f; t < MageStats.BaseFlameWardInterval + 0.1f; t += 0.1f)
        {
            mage.Fight(0.1f, Vector3D<float>.Zero, MageTesting.Aim, stunned: true, MageTesting.QuietField(), MageTesting.FlatGround, health, new DamageNumbers());
        }

        Assert.Equal(1f, mage.BlockChance);
    }

    [Fact]
    public void WithoutFlameWard_OnlyTheItemsBlock()
    {
        var health = new PlayerHealth(100f);
        Assert.Equal(0f, PyroTesting.Mage(health).BlockChance);

        var frost = new MageClass(new Random(1));
        frost.UseTree(new Dictionary<string, int> { [PyromancyTree.FlameWard] = 1 });   // Frost active: the fire tree's ranks don't count
        Assert.Equal(0f, frost.BlockChance);
    }

    [Fact]
    public void LivingFlame_GrowsABurnWithItsAge_AndANewBurnKeepsIt()
    {
        var field = MageTesting.QuietField();
        var target = MageTesting.Sturdy(field, 8f);
        var stats = PyroTesting.With((PyromancyTree.LivingFlame, 1));
        var flames = new Flames();
        flames.Ignite(target, 20f, stats);

        var hits = PyroTesting.Burn(flames, stats, field, 2.1f);
        float tick = 20f * MageStats.BaseBurnShare / MageStats.BaseBurnDuration * MageStats.BurnTick;
        Assert.Equal(8, hits.Count);
        Assert.Equal(tick * (1f + MageStats.LivingFlameGrowth * 0.25f), hits[0].Damage, 2);
        Assert.Equal(tick * (1f + MageStats.LivingFlameGrowth * 2f), hits[^1].Damage, 2);

        flames.Ignite(target, 20f, stats);
        Assert.Equal(2.1f, flames.Burns[target].Burned, 1);   // still as old
        Assert.Equal(12, flames.Burns[target].TicksLeft);  // and its time made up again
    }

    [Fact]
    public void LivingFlame_StopsGrowing_After8Seconds()
    {
        var field = MageTesting.QuietField();
        var target = MageTesting.Sturdy(field, 8f);
        var stats = PyroTesting.With((PyromancyTree.LivingFlame, 1));
        var flames = new Flames();
        flames.Ignite(target, 20f, stats);

        for (int i = 0; i < 5; i++)
        {
            PyroTesting.Burn(flames, stats, field, 2f);
            flames.Ignite(target, 20f, stats);
        }

        var last = PyroTesting.Burn(flames, stats, field, 0.3f).Last();
        float tick = 20f * MageStats.BaseBurnShare / MageStats.BaseBurnDuration * MageStats.BurnTick;
        Assert.Equal(tick * (1f + MageStats.LivingFlameGrowth * MageStats.LivingFlameMaxSeconds), last.Damage, 2);
    }

    [Fact]
    public void WithoutLivingFlame_ABurnStaysTheSame()
    {
        var field = MageTesting.QuietField();
        var target = MageTesting.Sturdy(field, 8f);
        var stats = PyroTesting.With();
        var flames = new Flames();
        flames.Ignite(target, 20f, stats);

        var hits = PyroTesting.Burn(flames, stats, field, 3f);

        Assert.Equal(hits[0].Damage, hits[^1].Damage, 4);
    }

    [Fact]
    public void Meteor_FallsOnTheThickestCrowd_EveryFourthBarrage()
    {
        var field = MageTesting.QuietField();
        var crowd = new[] { MageTesting.Sturdy(field, 10f), MageTesting.Sturdy(field, 11f), MageTesting.Sturdy(field, 10f, 1f) };
        var alone = MageTesting.Sturdy(field, -2f, -8f);
        var stats = PyroTesting.With((PyromancyTree.Meteor, 1));
        var flames = new Flames();

        flames.Cast(Vector3D<float>.Zero, 3, stats, field);
        Assert.Empty(flames.Meteors);

        flames.Cast(Vector3D<float>.Zero, 4, stats, field);
        var meteor = Assert.Single(flames.Meteors);
        Assert.Contains(crowd, e => e.Position == meteor.Target);

        var hits = PyroTesting.Burn(flames, stats, field, MageStats.MeteorFall + 0.05f);
        var struck = hits.Where(h => h.Source == FrostSource.Meteor).ToList();
        Assert.Equal(3, struck.Count);
        Assert.All(struck, h => Assert.Contains(h.Enemy, crowd));
        Assert.All(struck, h => Assert.Equal(MageStats.BaseBoltDamage * MageStats.MeteorDamage, h.Damage, 3));
        Assert.DoesNotContain(hits, h => h.Enemy == alone);

        var crater = Assert.Single(flames.Ground);   // the ground left burning
        Assert.False(crater.IsLine);
        var burnt = PyroTesting.Burn(flames, stats, field, MageStats.MeteorGroundSeconds + 0.1f).Where(h => h.Source == FrostSource.Ground).ToList();
        Assert.Equal(3 * 6, burnt.Count);   // a bite every half second for 3 s, on each
        Assert.Empty(flames.Ground);
    }

    [Fact]
    public void WithoutMeteor_NoMeteorFalls()
    {
        var field = MageTesting.QuietField();
        MageTesting.Sturdy(field, 10f);
        var flames = new Flames();

        flames.Cast(Vector3D<float>.Zero, 4, PyroTesting.With(), field);

        Assert.Empty(flames.Meteors);
    }

    [Fact]
    public void AFireBarrage_CastsAndBurnsThroughAWholeFight()
    {
        // Every major at once, through the class: nothing throws, and the fire does its work.
        var ranks = PyromancyTree.Tree.Nodes.Select(n => (n.Id, n.MaxRanks)).ToArray();
        var health = new PlayerHealth(100f);
        var mage = PyroTesting.Mage(health, ranks);
        var field = new EnemyField(new Random(3)) { TargetCount = 0 };
        for (int i = 0; i < 12; i++)
        {
            var enemy = field.Spawn(new Vector3D<float>(6f + i % 4, 0f, -3f + i / 4 * 2f));
            enemy.Health = 3000f;
        }

        var numbers = new DamageNumbers();
        for (float t = 0f; t < 12f; t += MageTesting.Step)
        {
            mage.Fight(MageTesting.Step, Vector3D<float>.Zero, MageTesting.Aim, stunned: false, field, MageTesting.FlatGround, health, numbers);
            mage.AfterBlows(Vector3D<float>.Zero, field, health, numbers);
            if (Math.Abs(t - 5f) < MageTesting.Step / 2f)
            {
                mage.Blinked(Vector3D<float>.Zero, health);
            }
        }

        Assert.True(mage.Barrage.Barrages >= 4);
        Assert.True(mage.Barrage.Flames.BurningDeaths > 0);
        Assert.All(field.Enemies, e => Assert.False(e.IsChilled));
    }
}

public class PyromancyCardTests
{
    private static HashSet<MageUpgrade?> Offered(MageStats stats) =>
        Enumerable.Range(0, 200).SelectMany(seed => MageUpgrades.Roll(stats, new Random(seed))).Select(c => c.Upgrade).ToHashSet();

    [Fact]
    public void TheColdsCards_OnlyComeUp_WithFrost()
    {
        var fire = PyroTesting.With((PyromancyTree.Heat, 1), (PyromancyTree.Combustion, 1), (PyromancyTree.FlameWard, 1));
        fire.Tree = FrostBonuses.From(new Dictionary<string, int> { [FrostTree.FrostShield] = 1, [FrostTree.FrostBlast] = 1, [FrostTree.DeepFreeze] = 1 });

        var offered = Offered(fire);
        Assert.DoesNotContain(MageUpgrade.NumbingCold, offered);
        Assert.DoesNotContain(MageUpgrade.GlacialSpikes, offered);
        Assert.DoesNotContain(MageUpgrade.DeepChill, offered);
        Assert.DoesNotContain(MageUpgrade.GlacialWard, offered);
        Assert.DoesNotContain(MageUpgrade.ConcussiveFrost, offered);
        Assert.DoesNotContain(MageUpgrade.BlastPower, offered);
        foreach (var card in new[] { MageUpgrade.FanTheFlames, MageUpgrade.LastingEmbers, MageUpgrade.CoolingBreath, MageUpgrade.BlastingAsh, MageUpgrade.RekindledWard })
        {
            Assert.Contains(card, offered);
        }
    }

    [Fact]
    public void TheFiresCards_OnlyComeUp_WithPyromancy()
    {
        var frost = MageTesting.With((FrostTree.FrostShield, 1));
        frost.Pyro = PyromancyBonuses.From(new Dictionary<string, int> { [PyromancyTree.Heat] = 1, [PyromancyTree.Combustion] = 1, [PyromancyTree.FlameWard] = 1 });

        var offered = Offered(frost);
        foreach (var card in new[] { MageUpgrade.FanTheFlames, MageUpgrade.LastingEmbers, MageUpgrade.CoolingBreath, MageUpgrade.BlastingAsh, MageUpgrade.RekindledWard })
        {
            Assert.DoesNotContain(card, offered);
        }

        Assert.Contains(MageUpgrade.NumbingCold, offered);
        Assert.Contains(MageUpgrade.GlacialWard, offered);
    }

    [Fact]
    public void TheMajorsCards_NeedTheirMajors()
    {
        var plain = PyroTesting.With();

        Assert.True(MageUpgrades.Offered(MageUpgrade.FanTheFlames, plain));
        Assert.True(MageUpgrades.Offered(MageUpgrade.LastingEmbers, plain));
        Assert.False(MageUpgrades.Offered(MageUpgrade.CoolingBreath, plain));
        Assert.False(MageUpgrades.Offered(MageUpgrade.BlastingAsh, plain));
        Assert.False(MageUpgrades.Offered(MageUpgrade.RekindledWard, plain));
        Assert.True(MageUpgrades.Offered(MageUpgrade.CoolingBreath, PyroTesting.With((PyromancyTree.Heat, 1))));
        Assert.True(MageUpgrades.Offered(MageUpgrade.BlastingAsh, PyroTesting.With((PyromancyTree.Combustion, 1))));
        Assert.True(MageUpgrades.Offered(MageUpgrade.RekindledWard, PyroTesting.With((PyromancyTree.FlameWard, 1))));
    }

    [Fact]
    public void TheSharedCards_ReadRightForTheElement()
    {
        var fire = MageUpgrades.Roll(PyroTesting.With(), new Random(1), count: 100);
        var frost = MageUpgrades.Roll(new MageStats(), new Random(1), count: 100);

        var searing = Assert.Single(fire, c => c.Upgrade == MageUpgrade.IceShards);
        Assert.Equal("Searing Bolts", searing.Name);
        Assert.Equal("+20% fire damage", searing.Description);
        Assert.Equal("+1 Fire Barrage projectile", Assert.Single(fire, c => c.Upgrade == MageUpgrade.SplinterBolt).Description);
        Assert.Equal("Ice Shards", Assert.Single(frost, c => c.Upgrade == MageUpgrade.IceShards).Name);
        Assert.Equal("+1 Frost Barrage projectile", Assert.Single(frost, c => c.Upgrade == MageUpgrade.SplinterBolt).Description);

        var words = new[] { "ice", "frost", "cold", "chill", "froze", "freeze", "winter", "glacial", "shatter" };
        foreach (var card in fire)
        {
            Assert.All(words, w => Assert.DoesNotContain(w, (card.Name + " " + card.Description).ToLowerInvariant()));
        }
    }

    [Fact]
    public void TheFireCards_DoWhatTheySay()
    {
        var stats = PyroTesting.With((PyromancyTree.Heat, 1), (PyromancyTree.Combustion, 1), (PyromancyTree.FlameWard, 1));
        stats.Increase(MageUpgrade.FanTheFlames);
        stats.Increase(MageUpgrade.LastingEmbers);
        stats.Increase(MageUpgrade.CoolingBreath);
        stats.Increase(MageUpgrade.BlastingAsh);
        stats.Increase(MageUpgrade.RekindledWard);
        stats.Increase(MageUpgrade.IceShards);

        Assert.Equal(MageStats.BaseBurnShare * 1.2f, stats.BurnShare, 4);
        Assert.Equal(MageStats.BaseBurnDuration + 0.5f, stats.BurnDuration, 4);
        Assert.Equal(MageStats.BaseHeatCooling + 1.5f, stats.HeatCooling, 4);
        Assert.Equal(stats.BoltDamage * 1.25f, stats.CombustionDamage, 3);
        Assert.Equal(MageStats.BaseCombustionRadius * 1.1f, stats.CombustionRadius, 3);
        Assert.Equal(MageStats.BaseFlameWardInterval - 1.5f, stats.FlameWardInterval, 3);
        Assert.Equal(MageStats.BaseBoltDamage * 1.2f, stats.BoltDamage, 3);   // Searing Bolts
    }

    [Fact]
    public void AllMaxed_WithPyromancy_OffersTheHeal()
    {
        var stats = PyroTesting.With((PyromancyTree.Heat, 1), (PyromancyTree.Combustion, 1), (PyromancyTree.FlameWard, 1));
        foreach (var info in MageUpgrades.All)
        {
            for (int i = 0; i < info.MaxLevel; i++)
            {
                stats.Increase(info.Upgrade);
            }
        }

        Assert.Null(Assert.Single(MageUpgrades.Roll(stats, new Random(1))).Upgrade);
    }
}
