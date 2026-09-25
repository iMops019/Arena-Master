using ArenaMaster.Game.Combat;
using ArenaMaster.Game.Items;
using ArenaMaster.Game.Paladin;
using ArenaMaster.Game.Progression;
using Silk.NET.Maths;

namespace ArenaMaster.Game.Tests;

internal static class PaladinTesting
{
    public const float Step = 1f / 60f;

    public static float? FlatGround(float x, float z) => MathF.Abs(x) < 200f && MathF.Abs(z) < 200f ? 0f : null;

    public static EnemyField QuietField() => new(new Random(1)) { TargetCount = 0 };

    public static PaladinStats With(params (string Id, int Ranks)[] ranks) =>
        new() { Tree = DefianceBonuses.From(ranks.ToDictionary(r => r.Id, r => r.Ranks)) };

    /// <summary>A ghoul that won't die of the first hit, standing <paramref name="x"/> m out along +X.</summary>
    public static Enemy Sturdy(EnemyField field, float x, float z = 0f)
    {
        var enemy = field.Spawn(new Vector3D<float>(x, 0f, z));
        enemy.Health = 10_000f;
        return enemy;
    }

    /// <summary>Runs the holy light at the origin for <paramref name="seconds"/>.</summary>
    public static List<HolyHit> Shine(HolyLight light, PaladinStats stats, EnemyField field, float seconds, bool canCast = true)
    {
        var hits = new List<HolyHit>();
        for (float t = 0f; t < seconds - 1e-4f; t += Step)
        {
            light.Update(Step, Vector3D<float>.Zero, stats, field, canCast, hits);
        }

        return hits;
    }
}

public class DefianceTreeTests
{
    private static TreeNode Node(string id) => DefianceTree.Tree.Node(id);

    [Fact]
    public void TheTree_IsWellFormed()
    {
        var tree = DefianceTree.Tree;
        var nodes = tree.Nodes;
        Assert.InRange(nodes.Count, 30, 45);
        Assert.Equal(nodes.Count, nodes.Select(n => n.Id).Distinct().Count());
        Assert.All(nodes, n => Assert.All(n.Parents, p => Assert.True(tree.Node(p).Tier < n.Tier, $"{n.Id} has a parent at or above its tier")));
        Assert.All(nodes.Where(n => n.Tier > 1), n => Assert.NotEmpty(n.Parents));
        Assert.All(nodes.Where(n => !n.Major), n => Assert.Contains("{", n.Text));
        Assert.All(nodes, n => Assert.DoesNotContain("{", n.Describe(1)));   // every number named is one the node has
        Assert.All(nodes, n => Assert.Contains(n.Lane, tree.Lanes.Select(l => l.Name)));
        Assert.All(nodes, n => Assert.InRange(n.X, 60f, 920f));
        Assert.All(nodes, n => Assert.True(n.Playable, $"{n.Name} is still marked coming soon"));
        Assert.Equal(2, nodes.Count(n => n.Tier == 1));
        Assert.Equal(7, tree.TierLevels.Count);
    }

    [Fact]
    public void EachLane_EndsInACapstone()
    {
        foreach (var (lane, _) in DefianceTree.Tree.Lanes)
        {
            Assert.Single(DefianceTree.Tree.Nodes, n => n.Tier == 7 && n.Lane == lane && n.Major);
        }
    }

    [Fact]
    public void ThornsAreUnlocked_ByATierTwoMajor_ThatTheThornsNodesGrowFrom()
    {
        var crown = Node(DefianceTree.CrownOfThorns);
        Assert.True(crown.Major);
        Assert.Equal(2, crown.Tier);
        Assert.Contains(DefianceTree.CrownOfThorns, Node("barbed").Parents);
        Assert.Contains(DefianceTree.CrownOfThorns, Node("quicken").Parents);
    }

    [Fact]
    public void EveryMinorsStats_ChangeTheBonuses()
    {
        var none = System.Text.Json.JsonSerializer.Serialize(DefianceBonuses.From(new Dictionary<string, int>()), new System.Text.Json.JsonSerializerOptions { IncludeFields = true });
        foreach (var node in DefianceTree.Tree.Nodes)
        {
            var one = System.Text.Json.JsonSerializer.Serialize(DefianceBonuses.From(new Dictionary<string, int> { [node.Id] = 1 }), new System.Text.Json.JsonSerializerOptions { IncludeFields = true });
            Assert.True(one != none, $"{node.Name} does nothing");
        }
    }

    [Fact]
    public void Descriptions_ShowTheNumbersAtTheirRank()
    {
        Assert.Equal("+30% Holy Nova damage and +15% nova frequency.", Node("zeal").Describe(3));
        Assert.Equal("Holy circles last 3 s longer.", Node("lingering").Describe(3));
    }
}

public class PaladinStatsTests
{
    [Fact]
    public void TheBaseNumbers()
    {
        var stats = new PaladinStats();

        Assert.Equal(PaladinStats.BaseNovaDamage, stats.NovaDamage);
        Assert.Equal(PaladinStats.BaseNovaInterval, stats.NovaInterval);
        Assert.Equal(PaladinStats.BaseMaxHealth, stats.MaxHealth);
        Assert.Equal(PaladinStats.BaseBlockChance, stats.BlockChance(false, false));
        Assert.Equal(0f, stats.Thorns);
    }

    [Fact]
    public void TreeUpgradesAndItems_AddUp_ThenMultipliersMultiply()
    {
        var stats = PaladinTesting.With(("zeal", 5), ("holyfire", 5));   // +50% +60%
        stats.Increase(PaladinUpgrade.HolyWrath);                          // +20%
        var items = new ItemBonuses { Damage = 0.1f, DamageMultiplier = 1.5f };
        stats.Items = items;

        Assert.Equal(PaladinStats.BaseNovaDamage * 2.4f * 1.5f, stats.NovaDamage, 3);
    }

    [Fact]
    public void BlockChance_GrowsStandingStill_AndInSanctuary_UpToTheCap()
    {
        var stats = PaladinTesting.With(("braced", 3), (DefianceTree.Sanctuary, 1));
        float moving = stats.BlockChance(false, false);

        Assert.Equal(moving + 0.15f, stats.BlockChance(true, false), 4);
        Assert.Equal(moving + 0.15f + PaladinStats.SanctuaryBlock, stats.BlockChance(true, true), 4);

        var capped = PaladinTesting.With(("shieldwall", 5), ("stalwart", 5), ("braced", 3), ("tower", 3), (DefianceTree.Sanctuary, 1));
        for (int i = 0; i < 5; i++)
        {
            capped.Increase(PaladinUpgrade.ShieldTraining);
        }

        Assert.Equal(PaladinStats.MaxBlockChance, capped.BlockChance(true, true));
    }

    [Fact]
    public void Thorns_NeedTheCrown_AndGrowWithTheirNodes()
    {
        var crown = PaladinTesting.With((DefianceTree.CrownOfThorns, 1));
        var barbed = PaladinTesting.With((DefianceTree.CrownOfThorns, 1), ("barbed", 5), ("spite", 2));
        var withoutCrown = PaladinTesting.With(("barbed", 5));

        Assert.Equal(PaladinStats.BaseThorns, crown.Thorns);
        Assert.Equal((PaladinStats.BaseThorns + 20f) * 1.5f, barbed.Thorns, 3);
        Assert.Equal(0f, withoutCrown.Thorns);
    }

    [Fact]
    public void IronBriars_AddAShareOfMaxHealth_ToThorns()
    {
        var stats = PaladinTesting.With((DefianceTree.CrownOfThorns, 1), ("ironbriars", 3));

        Assert.Equal(PaladinStats.BaseThorns + 0.06f * PaladinStats.BaseMaxHealth, stats.Thorns, 3);
    }

    [Fact]
    public void Sanctuary_CutsDamageTaken_OnlyInACircle()
    {
        var stats = PaladinTesting.With((DefianceTree.Sanctuary, 1));

        Assert.Equal(1f, stats.DamageTaken(false));
        Assert.Equal(PaladinStats.SanctuaryDamageTaken, stats.DamageTaken(true));
    }

    [Fact]
    public void DesperatePrayer_RaisesRegeneration_OnlyWhenLow()
    {
        var stats = PaladinTesting.With(("mending", 5), ("desperate", 2));

        Assert.Equal(1.5f, stats.Regeneration(false), 4);
        Assert.Equal(1.5f * 2f, stats.Regeneration(true), 4);
    }
}

public class PaladinUpgradeTests
{
    [Fact]
    public void BarbedPlating_OnlyComesUp_OnceThornsAreUnlocked()
    {
        var plain = new PaladinStats();
        var thorned = PaladinTesting.With((DefianceTree.CrownOfThorns, 1));

        for (int seed = 0; seed < 50; seed++)
        {
            Assert.DoesNotContain(PaladinUpgrades.Roll(plain, new Random(seed)), c => c.Upgrade == PaladinUpgrade.BarbedPlating);
        }

        Assert.Contains(Enumerable.Range(0, 50).SelectMany(seed => PaladinUpgrades.Roll(thorned, new Random(seed))), c => c.Upgrade == PaladinUpgrade.BarbedPlating);
    }

    [Fact]
    public void ARoll_OffersThreeDifferentUpgrades_AndAHealOnceAllAreMaxed()
    {
        var stats = PaladinTesting.With((DefianceTree.CrownOfThorns, 1));
        Assert.Equal(3, PaladinUpgrades.Roll(stats, new Random(3)).Select(c => c.Upgrade).Distinct().Count());

        foreach (var info in PaladinUpgrades.All)
        {
            for (int i = 0; i < info.MaxLevel; i++)
            {
                stats.Increase(info.Upgrade);
            }
        }

        Assert.Null(Assert.Single(PaladinUpgrades.Roll(stats, new Random(1))).Upgrade);
    }

    [Fact]
    public void ABanish_ReplacesTheCard_WithOneNotAlreadyOffered()
    {
        var stats = new PaladinStats();
        var random = new Random(5);
        var choices = PaladinUpgrades.Roll(stats, random);
        var struck = choices[1].Upgrade!.Value;

        var replaced = PaladinUpgrades.Replace(choices, 1, stats, random, new HashSet<PaladinUpgrade> { struck });

        Assert.DoesNotContain(replaced, c => c.Upgrade == struck);
        Assert.Equal(3, replaced.Select(c => c.Upgrade).Distinct().Count());
    }
}

public class HolyNovaTests
{
    [Fact]
    public void TheNova_HitsEverythingInReach_AndNothingBeyond()
    {
        var field = PaladinTesting.QuietField();
        var near = PaladinTesting.Sturdy(field, 3f);
        var edge = PaladinTesting.Sturdy(field, 0f, PaladinStats.BaseNovaRadius + 0.2f);   // its body still reaches in
        var far = PaladinTesting.Sturdy(field, PaladinStats.BaseNovaRadius + 2f);
        var light = new HolyLight(new Random(1));

        var hits = PaladinTesting.Shine(light, new PaladinStats(), field, HolyLight.FirstNova + 0.01f);

        Assert.Equal(1, light.Novas);
        Assert.Contains(hits, h => h.Enemy == near && h.Source == HolySource.Nova);
        Assert.Contains(hits, h => h.Enemy == edge);
        Assert.DoesNotContain(hits, h => h.Enemy == far);
    }

    [Fact]
    public void NovasCome_AtTheirInterval()
    {
        var light = new HolyLight(new Random(1));

        PaladinTesting.Shine(light, new PaladinStats(), PaladinTesting.QuietField(), HolyLight.FirstNova + 3f * PaladinStats.BaseNovaInterval + 0.05f);

        Assert.Equal(4, light.Novas);
    }

    [Fact]
    public void AStun_HoldsTheNova()
    {
        var light = new HolyLight(new Random(1));

        PaladinTesting.Shine(light, new PaladinStats(), PaladinTesting.QuietField(), 3f, canCast: false);

        Assert.Equal(0, light.Novas);
    }

    [Fact]
    public void EachNova_LeavesACircle_ThatBurnsWhatIsInIt_ThenFades()
    {
        var field = PaladinTesting.QuietField();
        var inside = PaladinTesting.Sturdy(field, 1f);
        var light = new HolyLight(new Random(1));
        var stats = new PaladinStats();

        PaladinTesting.Shine(light, stats, field, HolyLight.FirstNova + 0.01f);
        Assert.Single(light.Circles);
        Assert.Equal(1, light.CirclesAround(Vector3D<float>.Zero));

        var burn = PaladinTesting.Shine(light, stats, field, 1.2f).Where(h => h.Source == HolySource.Circle).ToList();
        Assert.Equal(2, burn.Count);   // a tick every half second
        Assert.All(burn, h => Assert.Equal(PaladinStats.BaseCircleDps * HolyLight.CircleTick, h.Damage, 3));
        Assert.All(burn, h => Assert.Equal(inside, h.Enemy));

        var quiet = new PaladinStats();
        var alone = new HolyLight(new Random(1));
        PaladinTesting.Shine(alone, quiet, PaladinTesting.QuietField(), HolyLight.FirstNova + 0.01f);
        PaladinTesting.Shine(alone, quiet, PaladinTesting.QuietField(), PaladinStats.BaseCircleDuration + 0.05f, canCast: false);
        Assert.Empty(alone.Circles);
    }

    [Fact]
    public void EchoingNova_BurstsAgain_ForLess()
    {
        var field = PaladinTesting.QuietField();
        var target = PaladinTesting.Sturdy(field, 2f);
        var light = new HolyLight(new Random(1));
        var stats = PaladinTesting.With((DefianceTree.EchoingNova, 1));
        stats.Items = new ItemBonuses { CritChance = -1f };   // no crits, for exact numbers

        var hits = PaladinTesting.Shine(light, stats, field, HolyLight.FirstNova + PaladinStats.EchoDelay + 0.05f).Where(h => h.Source == HolySource.Nova).ToList();

        Assert.Equal(2, hits.Count);
        Assert.Equal(1, light.Novas);
        Assert.Equal(hits[0].Damage * PaladinStats.EchoDamage, hits[1].Damage, 3);
    }

    [Fact]
    public void RadiantAvatar_MakesEveryEighthNova_AGreatOne()
    {
        var field = PaladinTesting.QuietField();
        var target = PaladinTesting.Sturdy(field, 2f);
        var light = new HolyLight(new Random(1));
        var stats = PaladinTesting.With((DefianceTree.RadiantAvatar, 1));
        stats.Items = new ItemBonuses { CritChance = -1f };
        var hits = new List<HolyHit>();

        for (int i = 0; i < 8; i++)
        {
            light.CastNova(Vector3D<float>.Zero, stats, field, hits);
        }

        Assert.All(hits.Take(7), h => Assert.Equal(PaladinStats.BaseNovaDamage, h.Damage, 3));
        Assert.Equal(PaladinStats.BaseNovaDamage * PaladinStats.AvatarDamage, hits[7].Damage, 3);
        Assert.Equal(PaladinStats.BaseCircleRadius * PaladinStats.AvatarCircle, light.Circles[^1].BaseRadius, 3);
    }

    [Fact]
    public void Resonance_BurstsFromEveryCircle_WithEachNova()
    {
        var field = PaladinTesting.QuietField();
        var byTheOldCircle = PaladinTesting.Sturdy(field, 20f);
        var light = new HolyLight(new Random(1));
        var stats = PaladinTesting.With((DefianceTree.Resonance, 1));
        stats.Items = new ItemBonuses { CritChance = -1f };
        var hits = new List<HolyHit>();

        light.CastNova(new Vector3D<float>(20f, 0f, 0f), stats, field, hits);   // a circle out there
        hits.Clear();
        light.CastNova(Vector3D<float>.Zero, stats, field, hits);                // far from it: only the circle's burst reaches

        var hit = Assert.Single(hits);
        Assert.Equal(byTheOldCircle, hit.Enemy);
        Assert.Equal(PaladinStats.BaseNovaDamage * PaladinStats.ResonanceDamage, hit.Damage, 3);
    }

    [Fact]
    public void WrathOfTheMany_HitsHarder_TheMoreItHits()
    {
        var field = PaladinTesting.QuietField();
        for (int i = 0; i < 10; i++)
        {
            PaladinTesting.Sturdy(field, 1f + 0.3f * i);
        }

        var stats = PaladinTesting.With((DefianceTree.WrathOfTheMany, 1));
        stats.Items = new ItemBonuses { CritChance = -1f };
        var hits = new List<HolyHit>();
        new HolyLight(new Random(1)).CastNova(Vector3D<float>.Zero, stats, field, hits);

        Assert.Equal(10, hits.Count);
        Assert.All(hits, h => Assert.Equal(PaladinStats.BaseNovaDamage * 1.2f, h.Damage, 3));
    }

    [Fact]
    public void ExpandingLight_GrowsACircle_ToTwiceItsSize()
    {
        var light = new HolyLight(new Random(1));
        var stats = PaladinTesting.With((DefianceTree.ExpandingLight, 1));
        light.CastNova(Vector3D<float>.Zero, stats, PaladinTesting.QuietField(), new List<HolyHit>());
        var circle = light.Circles[0];

        circle.Age = circle.Lifetime * 0.5f;
        Assert.Equal(circle.BaseRadius * 1.5f, circle.Radius, 3);
        circle.Age = circle.Lifetime;
        Assert.Equal(circle.BaseRadius * 2f, circle.Radius, 3);
    }

    [Fact]
    public void Elites_TakeSmitersBonus()
    {
        var field = PaladinTesting.QuietField();
        var brute = field.Spawn(new Vector3D<float>(2f, 0f, 0f), EnemyKind.Brute);
        brute.Health = 10_000f;
        var stats = PaladinTesting.With(("smiter", 2));
        stats.Items = new ItemBonuses { CritChance = -1f };
        var hits = new List<HolyHit>();

        new HolyLight(new Random(1)).CastNova(Vector3D<float>.Zero, stats, field, hits);

        Assert.Equal(PaladinStats.BaseNovaDamage * 1.3f, Assert.Single(hits).Damage, 3);
    }
}

public class ThornsTests
{
    [Fact]
    public void Thorns_StrikeWhatTouchesThePaladin_OnlyOnceUnlocked()
    {
        var field = PaladinTesting.QuietField();
        var touching = PaladinTesting.Sturdy(field, EnemyKind.Ghoul.Radius + EnemyField.PlayerRadius);
        var near = PaladinTesting.Sturdy(field, 2.5f);
        var stats = PaladinTesting.With((DefianceTree.CrownOfThorns, 1));

        var hits = PaladinTesting.Shine(new HolyLight(new Random(1)), stats, field, 0.8f, canCast: false).Where(h => h.Source == HolySource.Thorns).ToList();
        var none = PaladinTesting.Shine(new HolyLight(new Random(1)), new PaladinStats(), field, 1f, canCast: false).Where(h => h.Source == HolySource.Thorns);

        Assert.Equal(2, hits.Count);   // at once, then half a second later
        Assert.All(hits, h => Assert.Equal(touching, h.Enemy));
        Assert.All(hits, h => Assert.Equal(PaladinStats.BaseThorns, h.Damage, 3));
        Assert.Empty(none);
    }

    [Fact]
    public void CrownOfBriars_ReachesFurther_AndHitsHarder()
    {
        var field = PaladinTesting.QuietField();
        var near = PaladinTesting.Sturdy(field, 2.5f);
        var stats = PaladinTesting.With((DefianceTree.CrownOfThorns, 1), (DefianceTree.CrownOfBriars, 1));

        var hits = PaladinTesting.Shine(new HolyLight(new Random(1)), stats, field, 0.1f, canCast: false).Where(h => h.Source == HolySource.Thorns).ToList();

        Assert.Equal(near, Assert.Single(hits).Enemy);
        Assert.Equal(PaladinStats.BaseThorns * (1f + PaladinStats.BriarsBonus), hits[0].Damage, 3);
    }
}

public class BlockTests
{
    private static void Run(EnemyField field, PlayerHealth health, PlayerCondition condition, float blockChance, float seconds, List<Strike> strikes)
    {
        for (float t = 0f; t < seconds; t += PaladinTesting.Step)
        {
            health.Update(PaladinTesting.Step);
            condition.Update(PaladinTesting.Step);
            field.Update(PaladinTesting.Step, new PlayerTarget(Vector3D<float>.Zero, true, health, condition, blockChance), PaladinTesting.FlatGround);
            strikes.AddRange(field.Strikes);
        }
    }

    [Fact]
    public void ACertainBlock_TurnsEveryBlowAside_AndTheBlowsAreStillCounted()
    {
        var field = PaladinTesting.QuietField();
        field.Spawn(new Vector3D<float>(1f, 0f, 0f));
        var health = new PlayerHealth(100f);
        var strikes = new List<Strike>();

        Run(field, health, new PlayerCondition(), 1f, 3f, strikes);

        Assert.Equal(100f, health.Current);
        Assert.Equal(0f, health.HurtFlash);
        Assert.NotEmpty(strikes);
        Assert.All(strikes, s => Assert.True(s.Blocked));
    }

    [Fact]
    public void WithoutAShield_BlowsLand_AndAreCounted()
    {
        var field = PaladinTesting.QuietField();
        field.Spawn(new Vector3D<float>(1f, 0f, 0f));
        var health = new PlayerHealth(100f);
        var strikes = new List<Strike>();

        Run(field, health, new PlayerCondition(), 0f, 3f, strikes);

        Assert.True(health.Current < 100f);
        Assert.NotEmpty(strikes);
        Assert.All(strikes, s => Assert.False(s.Blocked));
        Assert.Equal(100f - health.Current, strikes.Sum(s => s.Damage), 3);
    }

    [Fact]
    public void ABlockedSlam_NeitherShovesNorStuns()
    {
        var field = PaladinTesting.QuietField();
        var brute = field.Spawn(new Vector3D<float>(5f, 0f, 0f), EnemyKind.Brute);
        brute.AttackCooldown = 0f;
        var condition = new PlayerCondition();
        var strikes = new List<Strike>();

        Run(field, new PlayerHealth(1000f), condition, 1f, 4f, strikes);

        Assert.Contains(strikes, s => s.Attacker == brute);
        Assert.False(condition.IsStunned);
        Assert.Equal(Vector3D<float>.Zero, condition.Knockback);
    }

    [Fact]
    public void ALastStand_LeavesOneHealth_Once()
    {
        var health = new PlayerHealth(50f) { LastStands = 1 };

        Assert.True(health.TakeDamage(80f));
        Assert.Equal(1f, health.Current);
        Assert.True(health.TakeLastStand());
        Assert.False(health.TakeLastStand());

        health.Update(1f);
        health.TakeDamage(80f);
        Assert.True(health.IsDead);
    }
}

public class PaladinAnswerTests
{
    private static PaladinClass Paladin(params (string Id, int Ranks)[] ranks)
    {
        var paladin = new PaladinClass(new Random(1));
        paladin.UseTree(ranks.ToDictionary(r => r.Id, r => r.Ranks));
        paladin.BeginRun(new ItemBonuses { CritChance = -1f }, new PlayerHealth(100f));
        return paladin;
    }

    [Fact]
    public void ABlock_BashesTheAttacker_AndHeals()
    {
        var field = PaladinTesting.QuietField();
        var attacker = PaladinTesting.Sturdy(field, 1f);
        var paladin = Paladin(("bash", 2), ("guard", 2));
        var health = new PlayerHealth(100f);
        health.TakeDamage(50f);
        float before = attacker.Health;

        paladin.AnswerStrikes(new[] { new Strike(attacker, 10f, Blocked: true) }, Vector3D<float>.Zero, field, health, new DamageNumbers());

        Assert.Equal(before - 24f, attacker.Health, 3);
        Assert.Equal(53f, health.Current, 3);
        Assert.Equal("BLOCKED", paladin.Status);
    }

    [Fact]
    public void Retribution_PaysBlowsBack_Double()
    {
        var field = PaladinTesting.QuietField();
        var attacker = PaladinTesting.Sturdy(field, 1f);
        var paladin = Paladin((DefianceTree.Retribution, 1));
        float before = attacker.Health;

        paladin.AnswerStrikes(new[] { new Strike(attacker, 10f, Blocked: false) }, Vector3D<float>.Zero, field, new PlayerHealth(100f), new DamageNumbers());

        Assert.Equal(before - 20f, attacker.Health, 3);
    }

    [Fact]
    public void HolyBastion_BurstsANova_OnABlock()
    {
        var field = PaladinTesting.QuietField();
        var attacker = PaladinTesting.Sturdy(field, 1f);
        var bystander = PaladinTesting.Sturdy(field, -3f);
        var paladin = Paladin((DefianceTree.HolyBastion, 1));

        paladin.AnswerStrikes(new[] { new Strike(attacker, 10f, Blocked: true) }, Vector3D<float>.Zero, field, new PlayerHealth(100f), new DamageNumbers());

        Assert.Equal(10_000f - PaladinStats.BaseNovaDamage * PaladinStats.BastionDamage, bystander.Health, 3);
    }

    [Fact]
    public void ShieldOfFaith_MakesTheNextBlockCertain_ThenRecharges()
    {
        var field = PaladinTesting.QuietField();
        var attacker = PaladinTesting.Sturdy(field, 1f);
        var paladin = Paladin((DefianceTree.ShieldOfFaith, 1));
        Assert.Equal(1f, paladin.BlockChance);

        paladin.AnswerStrikes(new[] { new Strike(attacker, 10f, Blocked: true) }, Vector3D<float>.Zero, field, new PlayerHealth(100f), new DamageNumbers());
        Assert.Equal(PaladinStats.BaseBlockChance, paladin.BlockChance, 4);

        var health = new PlayerHealth(100f);
        for (float t = 0f; t < PaladinStats.FaithInterval + 0.1f; t += 0.1f)
        {
            paladin.Fight(0.1f, Vector3D<float>.Zero, Vector3D<float>.Zero, false, stunned: true, field, health, new DamageNumbers());
        }

        Assert.Equal(1f, paladin.BlockChance);
    }

    [Fact]
    public void UnbrokenVow_GivesOneLastStand_ThatHeals()
    {
        var field = PaladinTesting.QuietField();
        var paladin = new PaladinClass(new Random(1));
        paladin.UseTree(new Dictionary<string, int> { [DefianceTree.UnbrokenVow] = 1 });
        var health = new PlayerHealth(100f);
        paladin.BeginRun(new ItemBonuses(), health);
        Assert.Equal(1, health.LastStands);

        health.TakeDamage(1000f);
        paladin.AnswerStrikes(Array.Empty<Strike>(), Vector3D<float>.Zero, field, health, new DamageNumbers());

        Assert.False(health.IsDead);
        Assert.Equal(1f + PaladinStats.BaseMaxHealth * PaladinStats.VowHeal, health.Current, 3);
        Assert.Equal("UNBROKEN VOW", paladin.Status);
    }

    [Fact]
    public void StandingInACircle_Heals_OncePerCircleWithConsecratedGround()
    {
        var field = PaladinTesting.QuietField();
        var plain = Paladin();
        var consecrated = Paladin((DefianceTree.ConsecratedGround, 1));
        foreach (var paladin in new[] { plain, consecrated })
        {
            paladin.Light.CastNova(Vector3D<float>.Zero, paladin.Stats, field, new List<HolyHit>());
            paladin.Light.CastNova(Vector3D<float>.Zero, paladin.Stats, field, new List<HolyHit>());
        }

        var plainHealth = new PlayerHealth(100f);
        plainHealth.TakeDamage(50f);
        var consecratedHealth = new PlayerHealth(100f);
        consecratedHealth.TakeDamage(50f);

        plain.Fight(1f, Vector3D<float>.Zero, Vector3D<float>.Zero, false, stunned: true, field, plainHealth, new DamageNumbers());
        consecrated.Fight(1f, Vector3D<float>.Zero, Vector3D<float>.Zero, false, stunned: true, field, consecratedHealth, new DamageNumbers());

        Assert.True(plain.InCircle);
        Assert.Equal(50f + PaladinStats.BaseCircleHealing, plainHealth.Current, 3);
        Assert.Equal(50f + 2f * PaladinStats.BaseCircleHealing, consecratedHealth.Current, 3);
    }
}
