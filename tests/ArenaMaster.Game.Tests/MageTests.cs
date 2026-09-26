using ArenaMaster.Game.Combat;
using ArenaMaster.Game.Items;
using ArenaMaster.Game.Mage;
using ArenaMaster.Game.Progression;
using Silk.NET.Maths;

namespace ArenaMaster.Game.Tests;

internal static class MageTesting
{
    public const float Step = 1f / 60f;

    public static readonly Vector3D<float> Staff = new(0f, 1.55f, 0f);

    public static readonly Vector3D<float> Aim = Vector3D<float>.UnitX;

    public static float? FlatGround(float x, float z) => MathF.Abs(x) < 200f && MathF.Abs(z) < 200f ? 0f : null;

    public static EnemyField QuietField() => new(new Random(1)) { TargetCount = 0 };

    /// <summary>Stats from <paramref name="ranks"/> in the Frost tree, with crits turned off so the numbers are exact.</summary>
    public static MageStats With(params (string Id, int Ranks)[] ranks) =>
        new() { Tree = FrostBonuses.From(ranks.ToDictionary(r => r.Id, r => r.Ranks)), Items = new ItemBonuses { CritChance = -1f } };

    public static Enemy Sturdy(EnemyField field, float x, float z = 0f, EnemyKind? kind = null)
    {
        var enemy = field.Spawn(new Vector3D<float>(x, 0f, z), kind);
        enemy.Health = 10_000f;
        return enemy;
    }

    /// <summary>Runs the barrage from a Mage at the origin, facing +X, for <paramref name="seconds"/>. The enemies stand still.</summary>
    public static List<FrostHit> Cast(FrostBarrage barrage, MageStats stats, EnemyField field, float seconds, bool canCast = true)
    {
        var hits = new List<FrostHit>();
        for (float t = 0f; t < seconds - 1e-4f; t += Step)
        {
            barrage.Update(Step, Staff, Vector3D<float>.Zero, Aim, stats, field, FlatGround, canCast, hits);
        }

        return hits;
    }
}

public class FrostTreeTests
{
    private static TreeNode Node(string id) => FrostTree.Tree.Node(id);

    [Fact]
    public void TheTree_IsWellFormed()
    {
        var tree = FrostTree.Tree;
        var nodes = tree.Nodes;
        Assert.InRange(nodes.Count, 30, 45);
        Assert.Equal(nodes.Count, nodes.Select(n => n.Id).Distinct().Count());
        Assert.All(nodes, n => Assert.All(n.Parents, p => Assert.True(tree.Node(p).Tier < n.Tier, $"{n.Id} has a parent at or above its tier")));
        Assert.All(nodes.Where(n => n.Tier > 1), n => Assert.NotEmpty(n.Parents));
        Assert.All(nodes.Where(n => !n.Major), n => Assert.Contains("{", n.Text));
        Assert.All(nodes, n => Assert.DoesNotContain("{", n.Describe(1)));
        Assert.All(nodes, n => Assert.Contains(n.Lane, tree.Lanes.Select(l => l.Name)));
        Assert.All(nodes, n => Assert.InRange(n.X, 60f, 920f));
        Assert.All(nodes, n => Assert.True(n.Playable, $"{n.Name} is still marked coming soon"));
        Assert.Equal(2, nodes.Count(n => n.Tier == 1));
        foreach (var (lane, _) in tree.Lanes)
        {
            Assert.Single(nodes, n => n.Tier == 7 && n.Lane == lane && n.Major);
        }
    }

    [Fact]
    public void FrostShield_AndFrostBlast_AreMajors_AndThereAreExtraProjectileNodes()
    {
        Assert.True(Node(FrostTree.FrostShield).Major);
        Assert.True(Node(FrostTree.FrostBlast).Major);
        Assert.Equal(2, FrostTree.Tree.Nodes.Count(n => n.Stats.ContainsKey(FrostTree.Projectiles)));
        Assert.Equal(MageStats.BaseProjectiles + 4, MageTesting.With(("extrashard", 2), ("volley", 2)).Projectiles);
    }

    [Fact]
    public void EveryNode_ChangesTheBonuses()
    {
        var options = new System.Text.Json.JsonSerializerOptions { IncludeFields = true };
        var none = System.Text.Json.JsonSerializer.Serialize(FrostBonuses.From(new Dictionary<string, int>()), options);
        foreach (var node in FrostTree.Tree.Nodes)
        {
            var one = System.Text.Json.JsonSerializer.Serialize(FrostBonuses.From(new Dictionary<string, int> { [node.Id] = 1 }), options);
            Assert.True(one != none, $"{node.Name} does nothing");
        }
    }

    [Fact]
    public void Descriptions_ShowTheNumbersAtTheirRank()
    {
        Assert.Equal("+30% cold damage and +9% chill.", Node("coldhands").Describe(3));
        Assert.Equal("Frost Barrage fires 2 more projectiles.", Node("extrashard").Describe(2));
    }
}

public class MageStatsTests
{
    [Fact]
    public void TheBaseNumbers()
    {
        var stats = new MageStats();

        Assert.Equal(3, stats.Projectiles);
        Assert.Equal(MageStats.BaseBoltDamage, stats.BoltDamage);
        Assert.Equal(MageStats.BaseBarrageInterval, stats.BarrageInterval);
        Assert.Equal(MageStats.BaseMaxHealth, stats.MaxHealth);
        Assert.Equal(MageStats.BaseChill, stats.Chill);
        Assert.Equal(0f, stats.FreezeChance);
    }

    [Fact]
    public void UpgradesTreeAndItems_AddUp_ThenMultipliersMultiply()
    {
        var stats = MageTesting.With(("coldhands", 5), ("bitter", 5));   // +50% +60%
        stats.Increase(MageUpgrade.IceShards);                           // +20%
        stats.Items = new ItemBonuses { Damage = 0.1f, DamageMultiplier = 1.5f };
        stats.Increase(MageUpgrade.SplinterBolt);

        Assert.Equal(MageStats.BaseBoltDamage * 2.4f * 1.5f, stats.BoltDamage, 3);
        Assert.Equal(MageStats.BaseProjectiles + 1, stats.Projectiles);
    }

    [Fact]
    public void Chill_IsCapped()
    {
        var stats = MageTesting.With(("coldhands", 5), ("numbing", 4), ("wintersbite", 3));
        for (int i = 0; i < 4; i++)
        {
            stats.Increase(MageUpgrade.NumbingCold);
        }

        Assert.Equal(MageStats.MaxChill, stats.Chill);
    }

    [Fact]
    public void GlacialFortress_AndRapidReforming_BringTheShieldBackSooner()
    {
        Assert.Equal(MageStats.BaseShieldInterval / 1.3f, MageTesting.With(("reforming", 3)).ShieldInterval, 3);
        Assert.Equal(MageStats.BaseShieldInterval / 1.5f, MageTesting.With((FrostTree.GlacialFortress, 1)).ShieldInterval, 3);
    }
}

public class MageUpgradeTests
{
    [Fact]
    public void ShieldAndBlastUpgrades_OnlyComeUp_WithTheirMajors()
    {
        var plain = new MageStats();
        var both = MageTesting.With((FrostTree.FrostShield, 1), (FrostTree.FrostBlast, 1));

        for (int seed = 0; seed < 60; seed++)
        {
            Assert.DoesNotContain(MageUpgrades.Roll(plain, new Random(seed)), c => c.Upgrade is MageUpgrade.GlacialWard or MageUpgrade.ConcussiveFrost);
        }

        var offered = Enumerable.Range(0, 60).SelectMany(seed => MageUpgrades.Roll(both, new Random(seed))).Select(c => c.Upgrade).ToHashSet();
        Assert.Contains(MageUpgrade.GlacialWard, offered);
        Assert.Contains(MageUpgrade.ConcussiveFrost, offered);
    }

    [Fact]
    public void AllMaxed_OffersTheHeal()
    {
        var stats = MageTesting.With((FrostTree.FrostShield, 1), (FrostTree.FrostBlast, 1));
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

public class FrostBarrageTests
{
    [Fact]
    public void ABarrage_WaitsForAnEnemyInRange()
    {
        var field = MageTesting.QuietField();
        MageTesting.Sturdy(field, MageStats.BaseTargetRange + 5f);
        var barrage = new FrostBarrage(new Random(1));

        MageTesting.Cast(barrage, new MageStats(), field, 3f);

        Assert.Equal(0, barrage.Barrages);
        Assert.Equal(0f, barrage.BarrageIn);
    }

    [Fact]
    public void TheThreeBolts_LeaveOneAfterAnother()
    {
        var field = MageTesting.QuietField();
        MageTesting.Sturdy(field, 20f);
        var barrage = new FrostBarrage(new Random(1));
        var stats = new MageStats();

        MageTesting.Cast(barrage, stats, field, FrostBarrage.FirstBarrage + 0.01f);
        Assert.Equal(1, barrage.Barrages);
        Assert.Single(barrage.Bolts);   // the first is away, the rest still to come
        Assert.Equal(2, barrage.Queued);

        MageTesting.Cast(barrage, stats, field, 1.2f * MageStats.BoltStagger);
        Assert.Equal(2, barrage.Bolts.Count);

        MageTesting.Cast(barrage, stats, field, 1.2f * MageStats.BoltStagger);
        Assert.Equal(3, barrage.Bolts.Count);
        Assert.Equal(0, barrage.Queued);
    }

    [Fact]
    public void TheBolts_SpreadOverEnemies_ThatOneBoltKills()
    {
        var field = MageTesting.QuietField();
        var enemies = new[]
        {
            field.Spawn(new Vector3D<float>(8f, 0f, 0f)), field.Spawn(new Vector3D<float>(6f, 0f, 6f)), field.Spawn(new Vector3D<float>(-7f, 0f, -2f)),
        };
        foreach (var enemy in enemies)
        {
            enemy.Health = MageStats.BaseBoltDamage;
        }

        var barrage = new FrostBarrage(new Random(1));
        var hits = MageTesting.Cast(barrage, MageTesting.With(), field, FrostBarrage.FirstBarrage + 1.5f).Where(h => h.Source == FrostSource.Bolt).ToList();

        Assert.Equal(3, hits.Count);
        Assert.Equal(3, hits.Select(h => h.Enemy).Distinct().Count());   // one each, all round the Mage
        Assert.All(enemies, e => Assert.False(e.IsAlive));
    }

    [Fact]
    public void TheBolts_KillWhatTheyAimAt_BeforeMovingOn()
    {
        var field = MageTesting.QuietField();
        var first = field.Spawn(new Vector3D<float>(7f, 0f, 0f));
        first.Health = 2f * MageStats.BaseBoltDamage;   // two bolts
        var tough = MageTesting.Sturdy(field, 0f, -12f);
        var barrage = new FrostBarrage(new Random(1));

        var hits = MageTesting.Cast(barrage, MageTesting.With(), field, FrostBarrage.FirstBarrage + 1.5f).Where(h => h.Source == FrostSource.Bolt).ToList();

        Assert.Equal(2, hits.Count(h => h.Enemy == first));
        Assert.Equal(1, hits.Count(h => h.Enemy == tough));   // the one left over goes to the next
        Assert.False(first.IsAlive);
    }

    [Fact]
    public void AStun_HoldsTheBarrage_BoltsStillToLeaveIncluded()
    {
        var field = MageTesting.QuietField();
        MageTesting.Sturdy(field, 15f);
        var barrage = new FrostBarrage(new Random(1));
        var stats = new MageStats();
        MageTesting.Cast(barrage, stats, field, FrostBarrage.FirstBarrage + 0.01f);

        MageTesting.Cast(barrage, stats, field, 1f, canCast: false);

        Assert.Equal(MageStats.BaseProjectiles - 1, barrage.Queued);
    }

    [Fact]
    public void FrostBlast_HurtsTheEnemiesAroundTheOneHit()
    {
        var field = MageTesting.QuietField();
        var target = MageTesting.Sturdy(field, 8f);
        var beside = MageTesting.Sturdy(field, 9.2f, 0.5f);
        var far = MageTesting.Sturdy(field, 8f, 5f);
        var stats = MageTesting.With((FrostTree.FrostBlast, 1));
        stats.Tree.Projectiles = 1 - MageStats.BaseProjectiles;   // one bolt, at the nearest
        var barrage = new FrostBarrage(new Random(1));
        barrage.BeginBarrage(stats);

        var hits = MageTesting.Cast(barrage, stats, field, 1.5f);

        var blasts = hits.Where(h => h.Source == FrostSource.Blast).ToList();
        Assert.NotEmpty(blasts);
        Assert.DoesNotContain(blasts, h => h.Enemy == target);   // the one hit takes the bolt, not its own blast
        Assert.DoesNotContain(hits, h => h.Enemy == far && h.Source == FrostSource.Blast);
        Assert.All(blasts.Where(h => h.Enemy == beside), h => Assert.Equal(MageStats.BaseBoltDamage * MageStats.BaseBlastDamage, h.Damage, 3));
    }

    [Fact]
    public void Shatter_HitsFrozenEnemiesHarder()
    {
        var field = MageTesting.QuietField();
        var frozen = MageTesting.Sturdy(field, 8f);
        frozen.Freeze(10f);
        var stats = MageTesting.With((FrostTree.Shatter, 1));
        var barrage = new FrostBarrage(new Random(1));
        barrage.BeginBarrage(stats);

        var hits = MageTesting.Cast(barrage, stats, field, 1.5f);

        Assert.All(hits, h => Assert.Equal(MageStats.BaseBoltDamage * (1f + MageStats.ShatterBonus), h.Damage, 3));
    }

    [Fact]
    public void DeepFreeze_FreezesChilledEnemies_ButNeverABoss()
    {
        var field = MageTesting.QuietField();
        var ghoul = MageTesting.Sturdy(field, 0f);
        var king = MageTesting.Sturdy(field, 0.5f, 0f, EnemyKind.HollowKing);
        var stats = MageTesting.With((FrostTree.DeepFreeze, 1), ("permafrost", 3));
        var barrage = new FrostBarrage(new Random(3));
        var hits = new List<FrostHit>();

        for (int i = 0; i < 200 && !ghoul.IsFrozen; i++)
        {
            barrage.Burst(Vector3D<float>.Zero, 3f, 1f, stats, field, hits, FrostSource.Blast);
        }

        Assert.True(ghoul.IsFrozen);
        Assert.Equal(stats.FreezeDuration, ghoul.FrozenFor, 3);
        Assert.False(king.IsFrozen);
    }

    [Fact]
    public void WithoutDeepFreeze_NothingFreezes()
    {
        var field = MageTesting.QuietField();
        var ghoul = MageTesting.Sturdy(field, 0f);
        var barrage = new FrostBarrage(new Random(3));

        for (int i = 0; i < 200; i++)
        {
            barrage.Burst(Vector3D<float>.Zero, 3f, 1f, new MageStats(), field, new List<FrostHit>(), FrostSource.Blast);
        }

        Assert.False(ghoul.IsFrozen);
        Assert.True(ghoul.IsChilled);
    }

    [Fact]
    public void SplittingIce_SplitsAKillingBolt_IntoTwoThatSeekOthers()
    {
        var field = MageTesting.QuietField();
        var weak = field.Spawn(new Vector3D<float>(8f, 0f, 0f));
        weak.Health = 1f;
        var other = MageTesting.Sturdy(field, 11f, 2f);
        var another = MageTesting.Sturdy(field, 11f, -2f);
        var stats = MageTesting.With((FrostTree.SplittingIce, 1));
        var barrage = new FrostBarrage(new Random(1));
        var hits = new List<FrostHit>();

        // One bolt only: straight at the weak one.
        stats.Tree.Projectiles = 1 - MageStats.BaseProjectiles;
        barrage.BeginBarrage(stats);
        hits.AddRange(MageTesting.Cast(barrage, stats, field, 1.5f));

        Assert.Contains(hits, h => h.Enemy == weak && h.Killed);
        var halves = hits.Where(h => h.Enemy == other || h.Enemy == another).ToList();
        Assert.Equal(2, halves.Count);
        Assert.All(halves, h => Assert.Equal(MageStats.BaseBoltDamage * MageStats.SplitDamage, h.Damage, 3));
    }

    [Fact]
    public void Comet_EndsEveryFourthBarrage_AtTheToughestEnemy()
    {
        var field = MageTesting.QuietField();
        var small = MageTesting.Sturdy(field, 6f);
        var brute = MageTesting.Sturdy(field, 10f, 0f, EnemyKind.Brute);
        brute.Health = 50_000f;
        var stats = MageTesting.With((FrostTree.Comet, 1));
        var barrage = new FrostBarrage(new Random(1));

        for (int i = 0; i < 3; i++)
        {
            barrage.BeginBarrage(stats);
            MageTesting.Cast(barrage, stats, field, 0.6f);
        }

        Assert.DoesNotContain(barrage.Bolts, b => b.IsComet);
        barrage.BeginBarrage(stats);
        var hits = MageTesting.Cast(barrage, stats, field, 2f);

        var comet = Assert.Single(hits, h => h.Damage >= MageStats.BaseBoltDamage * MageStats.CometDamage - 0.01f);
        Assert.Equal(brute, comet.Enemy);
    }

    [Fact]
    public void Blizzard_BitesEverythingNearTheMage()
    {
        var field = MageTesting.QuietField();
        var near = MageTesting.Sturdy(field, 4f);
        var far = MageTesting.Sturdy(field, 7f);
        field.Spawn(new Vector3D<float>(50f, 0f, 0f));   // out of the barrage's reach too, so only the blizzard acts
        var stats = MageTesting.With((FrostTree.Blizzard, 1));
        var barrage = new FrostBarrage(new Random(1));

        var hits = MageTesting.Cast(barrage, stats, field, 0.8f, canCast: false).Where(h => h.Source == FrostSource.Blizzard).ToList();

        Assert.Equal(2, hits.Count);
        Assert.All(hits, h => Assert.Equal(near, h.Enemy));
        Assert.All(hits, h => Assert.Equal(MageStats.BaseBoltDamage * FrostBarrage.BlizzardTick, h.Damage, 3));
        Assert.True(near.IsChilled);
        Assert.False(far.IsChilled);
    }
}

public class ChillAndFreezeTests
{
    private static void Walk(EnemyField field, float seconds)
    {
        var health = new PlayerHealth(10_000f);
        for (float t = 0f; t < seconds; t += MageTesting.Step)
        {
            health.Update(MageTesting.Step);
            field.Update(MageTesting.Step, new PlayerTarget(Vector3D<float>.Zero, true, health, new PlayerCondition()), MageTesting.FlatGround);
        }
    }

    [Fact]
    public void AChilledEnemy_WalksSlower_UntilTheChillWearsOff()
    {
        var field = MageTesting.QuietField();
        var chilled = field.Spawn(new Vector3D<float>(20f, 0f, 0f));
        chilled.Chill(10f, 0.5f);

        Walk(field, 1f);

        Assert.InRange(chilled.Position.X, 20f - EnemyKind.Ghoul.Speed * 0.55f, 20f - EnemyKind.Ghoul.Speed * 0.45f);

        chilled.ChilledFor = 0f;
        float before = chilled.Position.X;
        Walk(field, 1f);
        Assert.InRange(before - chilled.Position.X, EnemyKind.Ghoul.Speed * 0.95f, EnemyKind.Ghoul.Speed * 1.05f);
    }

    [Fact]
    public void AStrongerChill_WinsOverAWeakerOne()
    {
        var enemy = new Enemy(1, EnemyKind.Ghoul, Vector3D<float>.Zero, EnemyScaling.None);
        enemy.Chill(2f, 0.5f);
        enemy.Chill(3f, 0.2f);

        Assert.Equal(0.5f, enemy.ChillSlow);
        Assert.Equal(3f, enemy.ChilledFor);
    }

    [Fact]
    public void AFrozenEnemy_NeitherMovesNorClaws()
    {
        var field = MageTesting.QuietField();
        var enemy = field.Spawn(new Vector3D<float>(0.8f, 0f, 0f));
        enemy.Freeze(2f);
        var health = new PlayerHealth(100f);

        for (float t = 0f; t < 1.5f; t += MageTesting.Step)
        {
            health.Update(MageTesting.Step);
            field.Update(MageTesting.Step, new PlayerTarget(Vector3D<float>.Zero, true, health, new PlayerCondition()), MageTesting.FlatGround);
        }

        Assert.Equal(0.8f, enemy.Position.X, 3);
        Assert.Equal(100f, health.Current);
        Assert.True(enemy.IsFrozen);
    }
}

public class BarrierTests
{
    [Fact]
    public void ABarrier_TakesTheBlowFirst_WithoutAFlash()
    {
        var health = new PlayerHealth(100f) { Barrier = 30f };

        Assert.True(health.TakeDamage(20f));
        Assert.Equal(100f, health.Current);
        Assert.Equal(10f, health.Barrier, 3);
        Assert.Equal(0f, health.HurtFlash);

        health.Update(1f);
        health.TakeDamage(25f);
        Assert.Equal(0f, health.Barrier);
        Assert.Equal(85f, health.Current, 3);
    }
}

public class FrostShieldTests
{
    private static MageClass Mage(PlayerHealth health, params (string Id, int Ranks)[] ranks)
    {
        var mage = new MageClass(new Random(1));
        mage.UseTree(ranks.ToDictionary(r => r.Id, r => r.Ranks));
        mage.BeginRun(new ItemBonuses { CritChance = -1f }, health);
        return mage;
    }

    private static void Run(MageClass mage, PlayerHealth health, EnemyField field, float seconds)
    {
        for (float t = 0f; t < seconds; t += 0.1f)
        {
            mage.Fight(0.1f, Vector3D<float>.Zero, MageTesting.Aim, stunned: true, field, MageTesting.FlatGround, health, new DamageNumbers());
        }
    }

    [Fact]
    public void TheShield_FormsOnItsOwn_ThenWearsOff_AndFormsAgain()
    {
        var health = new PlayerHealth(100f);
        var mage = Mage(health, (FrostTree.FrostShield, 1));
        var field = MageTesting.QuietField();

        Run(mage, health, field, 2.05f);
        Assert.True(mage.ShieldUp);
        Assert.Equal(MageStats.ShieldFlat + MageStats.ShieldShare * MageStats.BaseMaxHealth, health.Barrier, 3);

        Run(mage, health, field, MageStats.BaseShieldDuration);
        Assert.False(mage.ShieldUp);
        Assert.Equal(0f, health.Barrier);

        Run(mage, health, field, MageStats.BaseShieldInterval + 0.1f);
        Assert.True(mage.ShieldUp);
    }

    [Fact]
    public void WithoutTheNode_NoShieldForms()
    {
        var health = new PlayerHealth(100f);
        var mage = Mage(health);

        Run(mage, health, MageTesting.QuietField(), 30f);

        Assert.False(mage.ShieldUp);
        Assert.Equal(0f, health.Barrier);
    }

    [Fact]
    public void GlacialFortress_HoldsTheShield_UntilItBreaks()
    {
        var health = new PlayerHealth(100f);
        var mage = Mage(health, (FrostTree.FrostShield, 1), (FrostTree.GlacialFortress, 1));

        Run(mage, health, MageTesting.QuietField(), 30f);

        Assert.True(mage.ShieldUp);
    }

    [Fact]
    public void ABrokenShield_BurstsWithShatteringWard()
    {
        var field = MageTesting.QuietField();
        var near = MageTesting.Sturdy(field, 3f);
        var health = new PlayerHealth(100f);
        var mage = Mage(health, (FrostTree.FrostShield, 1), (FrostTree.ShatteringWard, 1));
        Run(mage, health, field, 2.05f);

        health.TakeDamage(1000f);
        mage.AfterBlows(Vector3D<float>.Zero, field, health, new DamageNumbers());

        Assert.False(mage.ShieldUp);
        Assert.Equal(10_000f - MageStats.BaseBoltDamage * MageStats.WardBurstShare, near.Health, 3);
        Assert.True(near.IsChilled);
    }

    [Fact]
    public void IceBlock_SavesOnce_HealsAndShields()
    {
        var health = new PlayerHealth(100f);
        var mage = Mage(health, (FrostTree.IceBlock, 1));
        Assert.Equal(1, health.LastStands);

        health.TakeDamage(1000f);
        mage.AfterBlows(Vector3D<float>.Zero, MageTesting.QuietField(), health, new DamageNumbers());

        Assert.False(health.IsDead);
        Assert.Equal(1f + MageStats.BaseMaxHealth * MageStats.IceBlockHeal, health.Current, 3);
        Assert.Equal(MageStats.BaseMaxHealth * MageStats.IceBlockShield, health.Barrier, 3);
        Assert.True(mage.ShieldUp);
        Assert.Equal("ICE BLOCK", mage.Status);
    }
}
