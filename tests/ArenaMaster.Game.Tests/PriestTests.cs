using ArenaMaster.Game.Combat;
using ArenaMaster.Game.Items;
using ArenaMaster.Game.Priest;
using ArenaMaster.Game.Progression;
using Silk.NET.Maths;

namespace ArenaMaster.Game.Tests;

internal static class PriestTesting
{
    public const float Step = 1f / 60f;

    public static readonly Vector3D<float> Hand = new(0f, 1.3f, 0f);
    public static readonly Vector3D<float> North = new(0f, 0f, 1f);

    public static float? FlatGround(float x, float z) => MathF.Abs(x) < 200f && MathF.Abs(z) < 200f ? 0f : null;

    public static EnemyField QuietField() => new(new Random(1)) { TargetCount = 0 };

    /// <summary>No crits, for exact numbers.</summary>
    public static PriestStats With(params (string Id, int Ranks)[] ranks) =>
        new() { Tree = UnholyBonuses.From(ranks.ToDictionary(r => r.Id, r => r.Ranks)), Items = new ItemBonuses { CritChance = -1f } };

    /// <summary>A ghoul that won't die of a hit, standing at (<paramref name="x"/>, <paramref name="z"/>).</summary>
    public static Enemy Sturdy(EnemyField field, float x, float z)
    {
        var enemy = field.Spawn(new Vector3D<float>(x, 0f, z));
        enemy.Health = 10_000f;
        return enemy;
    }

    /// <summary>Runs <paramref name="skulls"/> at the origin facing north for <paramref name="seconds"/>, casting or not.</summary>
    public static List<PriestHit> Run(PlagueSkulls skulls, PriestStats stats, EnemyField field, float seconds, bool canCast = false)
    {
        var hits = new List<PriestHit>();
        for (float t = 0f; t < seconds - 1e-4f; t += Step)
        {
            skulls.Update(Step, Hand, Vector3D<float>.Zero, North, stats, field, FlatGround, canCast, hits);
        }

        return hits;
    }

    /// <summary>A cast now: its skulls leave the wand (over half a second, for more than one), and then no more casts.</summary>
    public static List<PriestHit> Fire(PlagueSkulls skulls, PriestStats stats, EnemyField field)
    {
        skulls.Cast(stats);
        return Run(skulls, stats, field, 0.5f, canCast: true);
    }

    public static PriestClass Priest(params (string Id, int Ranks)[] ranks)
    {
        var priest = new PriestClass(new Random(3));
        priest.UseTree(ranks.ToDictionary(r => r.Id, r => r.Ranks));
        priest.BeginRun(new ItemBonuses { CritChance = -1f }, new PlayerHealth(100f));
        return priest;
    }
}

public class UnholyTreeTests
{
    [Fact]
    public void TheTree_IsWellFormed()
    {
        var tree = UnholyTree.Tree;
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
        Assert.Equal(14, nodes.Count(n => n.Major));
        Assert.Equal(7, tree.TierLevels.Count);
    }

    [Fact]
    public void DeathAndDecay_IsATierThreeMajor_AndPestilenceIsThere()
    {
        var decay = UnholyTree.Tree.Node(UnholyTree.DeathAndDecay);
        Assert.Equal("Death and Decay", decay.Name);
        Assert.True(decay.Major);
        Assert.Equal(3, decay.Tier);
        Assert.Contains("25%", decay.Text);

        var pestilence = UnholyTree.Tree.Node(UnholyTree.Pestilence);
        Assert.Equal("Pestilence", pestilence.Name);
        Assert.True(pestilence.Major);
    }

    [Fact]
    public void EachLane_EndsInACapstone()
    {
        var capstones = UnholyTree.Tree.Nodes.Where(n => n.Tier == 7).ToList();
        Assert.All(capstones, n => Assert.True(n.Major));
        Assert.Equal(UnholyTree.Tree.Lanes.Select(l => l.Name).OrderBy(n => n), capstones.Select(n => n.Lane).OrderBy(n => n));
    }

    [Fact]
    public void Ranks_AddUpIntoTheBonuses()
    {
        var bonuses = UnholyBonuses.From(new Dictionary<string, int> { ["unholymight"] = 3, ["splinters"] = 2, [UnholyTree.VirulentStrain] = 1, ["gravecloth"] = 2 });
        Assert.Equal(0.3f, bonuses.SkullDamage, 4);
        Assert.Equal(0.3f, bonuses.PlagueDamage, 4);
        Assert.Equal(2f, bonuses.Pierce);
        Assert.True(bonuses.VirulentStrain);
        Assert.False(bonuses.Pestilence);
        Assert.Equal(0.96f * 0.96f, bonuses.DamageTaken, 4);
    }
}

public class PriestStatsTests
{
    [Fact]
    public void TheSkullShield_BlocksSevenPercent_ToStartWith()
    {
        Assert.Equal(0.07f, new PriestStats().BlockChance, 4);
        Assert.Equal(0f, new PriestStats().DecayChance);
        Assert.Equal(0.25f, PriestTesting.With((UnholyTree.DeathAndDecay, 1)).DecayChance, 4);
        Assert.Equal(1f, PriestTesting.With((UnholyTree.DeathAndDecay, 1), (UnholyTree.MouthOfTheGrave, 1)).DecayChance);
    }

    [Fact]
    public void Plague_StacksToThree_OrFiveWithVirulentStrain()
    {
        Assert.Equal(3, new PriestStats().PlagueStacks);
        Assert.Equal(5, PriestTesting.With((UnholyTree.VirulentStrain, 1)).PlagueStacks);
        Assert.Equal(2f, new PriestStats().PlagueDuration);
        Assert.Equal(2.2f, new PriestStats().SkullLife);
    }

    [Fact]
    public void TheItemsSpeakPriest()
    {
        var stats = new PriestStats { Items = new ItemBonuses { Chains = 1, Projectiles = 1, PlagueStacks = 1, Range = 0.2f } };
        Assert.Equal(PriestStats.BasePierce + 1, stats.Pierce);
        Assert.Equal(2, stats.Skulls);
        Assert.Equal(4, stats.PlagueStacks);
        Assert.Equal(PriestStats.BaseSkullLife * 1.2f, stats.SkullLife, 4);
    }
}

public class PlagueSkullTests
{
    [Fact]
    public void ItWaitsForAnEnemyInRange_ThenCasts()
    {
        var field = PriestTesting.QuietField();
        var skulls = new PlagueSkulls(new Random(1));
        var stats = PriestTesting.With();

        PriestTesting.Run(skulls, stats, field, 2f, canCast: true);
        Assert.Equal(0, skulls.Casts);

        PriestTesting.Sturdy(field, 0f, 15f);
        PriestTesting.Run(skulls, stats, field, PriestTesting.Step * 2, canCast: true);
        Assert.Equal(1, skulls.Casts);
        Assert.Single(skulls.Skulls);
    }

    [Fact]
    public void TheSkull_HuntsThroughTheCrowd_PiercingOneAfterAnother_NeverTheSameTwice()
    {
        var field = PriestTesting.QuietField();
        var line = new[] { (0f, 3f), (1.5f, 5.5f), (-1.5f, 8f), (1.5f, 10.5f), (-1.5f, 13f), (0f, 15.5f), (1.5f, 18f) }
            .Select(p => PriestTesting.Sturdy(field, p.Item1, p.Item2)).ToList();
        var skulls = new PlagueSkulls(new Random(1));
        var stats = PriestTesting.With();

        var hits = PriestTesting.Fire(skulls, stats, field);
        hits.AddRange(PriestTesting.Run(skulls, stats, field, 3f));

        var struck = hits.Where(h => h.Source == PriestSource.Skull).Select(h => h.Enemy).ToList();
        Assert.Equal(1 + PriestStats.BasePierce, struck.Count);         // the first, and four more
        Assert.Equal(struck.Count, struck.Distinct().Count());
        Assert.All(hits.Where(h => h.Source == PriestSource.Skull), h => Assert.Equal(stats.SkullDamage, h.Damage, 3));
        Assert.Empty(skulls.Skulls);                                     // its pierces spent, it is gone
    }

    [Fact]
    public void ACrate_IsTargetedAndStruck_LikeAnEnemy()
    {
        var field = PriestTesting.QuietField();
        var crate = field.Spawn(new Vector3D<float>(3f, 0f, 6f), EnemyKind.Crate);
        crate.Health = 10_000f;
        var skulls = new PlagueSkulls(new Random(1));
        var stats = PriestTesting.With();

        var hits = PriestTesting.Run(skulls, stats, field, 1f, canCast: true);   // a crate alone is enough to cast at
        hits.AddRange(PriestTesting.Run(skulls, stats, field, 1.5f));

        Assert.Equal(1, skulls.Casts);
        Assert.Contains(hits, h => h.Enemy == crate && h.Source == PriestSource.Skull);
    }

    [Fact]
    public void ACrateInTheWay_IsStruck_NotFlownThrough()
    {
        var field = PriestTesting.QuietField();
        var crate = field.Spawn(new Vector3D<float>(0f, 0f, 4f), EnemyKind.Crate);
        crate.Health = 10_000f;
        PriestTesting.Sturdy(field, 0f, 8f);
        var skulls = new PlagueSkulls(new Random(1));
        var stats = PriestTesting.With();

        var hits = PriestTesting.Fire(skulls, stats, field);
        hits.AddRange(PriestTesting.Run(skulls, stats, field, 1.5f));

        Assert.Contains(hits, h => h.Enemy == crate && h.Source == PriestSource.Skull);
    }

    [Fact]
    public void AnUnspentSkull_FadesAfterItsLife()
    {
        var field = PriestTesting.QuietField();
        PriestTesting.Sturdy(field, 0f, 18f);
        var skulls = new PlagueSkulls(new Random(1));
        var stats = PriestTesting.With();
        PriestTesting.Fire(skulls, stats, field);

        PriestTesting.Run(skulls, stats, field, stats.SkullLife - 0.6f);
        Assert.Single(skulls.Skulls);
        PriestTesting.Run(skulls, stats, field, 0.2f);
        Assert.Empty(skulls.Skulls);
    }

    [Fact]
    public void AStruckEnemy_IsPlagued_TakingThePlaguesDamageOverTwoSeconds()
    {
        var field = PriestTesting.QuietField();
        var enemy = PriestTesting.Sturdy(field, 0f, 3f);
        var skulls = new PlagueSkulls(new Random(1));
        var stats = PriestTesting.With();

        var hits = PriestTesting.Fire(skulls, stats, field);
        hits.AddRange(PriestTesting.Run(skulls, stats, field, 3f));

        Assert.Equal(1, hits.Count(h => h.Source == PriestSource.Skull));
        float plague = hits.Where(h => h.Source == PriestSource.Plague && h.Enemy == enemy).Sum(h => h.Damage);
        Assert.Equal(stats.PlagueDamage, plague, stats.PlagueDamage * 0.13f);   // within a tick
        Assert.InRange(plague / stats.SkullDamage, 1.8f, 2.6f);                // a high dot: over twice the hit
        Assert.Equal(0, skulls.StacksOn(enemy));                               // run its course
    }

    [Fact]
    public void Plague_Stacks_UpToItsCap_AndMoreOnlyRefreshes()
    {
        var field = PriestTesting.QuietField();
        var enemy = PriestTesting.Sturdy(field, 0f, 3f);
        var skulls = new PlagueSkulls(new Random(1));
        var stats = PriestTesting.With();
        for (int i = 0; i < 5; i++)
        {
            skulls.Infect(enemy, stats);
        }

        Assert.Equal(3, skulls.StacksOn(enemy));

        var virulent = PriestTesting.With((UnholyTree.VirulentStrain, 1));
        var other = PriestTesting.Sturdy(field, 3f, 3f);
        for (int i = 0; i < 7; i++)
        {
            skulls.Infect(other, virulent);
        }

        Assert.Equal(5, skulls.StacksOn(other));

        // Three stacks deal three times one (four ticks in a second).
        var hits = PriestTesting.Run(skulls, stats, field, 1.05f);
        float oneStackPerSecond = stats.PlagueDamage / stats.PlagueDuration;
        Assert.Equal(3f * oneStackPerSecond, hits.Where(h => h.Enemy == enemy).Sum(h => h.Damage), oneStackPerSecond * 0.4f);
    }

    [Fact]
    public void Pestilence_SpreadsEveryStack_ToTheNearestThree_WhenAPlaguedEnemyDies()
    {
        var field = PriestTesting.QuietField();
        var victim = PriestTesting.Sturdy(field, 0f, 10f);
        var near = new[] { PriestTesting.Sturdy(field, 1.5f, 10f), PriestTesting.Sturdy(field, -1.5f, 10f), PriestTesting.Sturdy(field, 0f, 12f) };
        var fourth = PriestTesting.Sturdy(field, 0f, 13.5f);
        var far = PriestTesting.Sturdy(field, 8f, 10f);
        var skulls = new PlagueSkulls(new Random(1));
        var stats = PriestTesting.With((UnholyTree.Pestilence, 1));
        skulls.Infect(victim, stats);
        skulls.Infect(victim, stats);

        field.Damage(victim, 1e6f);
        PriestTesting.Run(skulls, stats, field, PriestTesting.Step);

        Assert.All(near, e => Assert.Equal(2, skulls.StacksOn(e)));
        Assert.Equal(0, skulls.StacksOn(fourth));
        Assert.Equal(0, skulls.StacksOn(far));
        Assert.Equal(3, skulls.Leaps.Count);
        Assert.Equal(1, skulls.PlaguedDeaths);
    }

    [Fact]
    public void WithoutPestilence_ThePlagueDiesWithIt()
    {
        var field = PriestTesting.QuietField();
        var victim = PriestTesting.Sturdy(field, 0f, 10f);
        var near = PriestTesting.Sturdy(field, 1.5f, 10f);
        var skulls = new PlagueSkulls(new Random(1));
        var stats = PriestTesting.With();
        skulls.Infect(victim, stats);

        field.Damage(victim, 1e6f);
        PriestTesting.Run(skulls, stats, field, PriestTesting.Step);

        Assert.Equal(0, skulls.StacksOn(near));
    }

    [Fact]
    public void Epidemic_PlaguesThoseAroundTheStruck()
    {
        var field = PriestTesting.QuietField();
        var struck = PriestTesting.Sturdy(field, 0f, 3f);
        var beside = PriestTesting.Sturdy(field, 1.8f, 3.5f);
        var away = PriestTesting.Sturdy(field, 7f, 3f);
        var skulls = new PlagueSkulls(new Random(1));
        var stats = PriestTesting.With((UnholyTree.Epidemic, 1));
        stats.Items.Chains = -PriestStats.BasePierce;   // it stops in the first it strikes

        PriestTesting.Fire(skulls, stats, field);

        Assert.True(skulls.StacksOn(struck) > 0);
        Assert.True(skulls.StacksOn(beside) > 0);
        Assert.Equal(0, skulls.StacksOn(away));
    }

    [Fact]
    public void TwinSkullsAndLegion_FireMoreSkulls()
    {
        var field = PriestTesting.QuietField();
        PriestTesting.Sturdy(field, 0f, 18f);
        var skulls = new PlagueSkulls(new Random(1));

        PriestTesting.Fire(skulls, PriestTesting.With((UnholyTree.TwinSkulls, 1), (UnholyTree.Legion, 1)), field);

        Assert.Equal(3, skulls.Skulls.Count);
    }

    [Fact]
    public void GnashingSkulls_FeedOnTheirKills()
    {
        var field = PriestTesting.QuietField();
        var weak = field.Spawn(new Vector3D<float>(0f, 0f, 3f));
        weak.Health = 1f;
        PriestTesting.Sturdy(field, 0f, 20f);
        var skulls = new PlagueSkulls(new Random(1));
        var stats = PriestTesting.With((UnholyTree.GnashingSkulls, 1));

        PriestTesting.Fire(skulls, stats, field);

        var skull = Assert.Single(skulls.Skulls);
        Assert.False(weak.IsAlive);
        Assert.Equal(stats.Pierce, skull.PierceLeft);                           // one spent, one gained
        Assert.Equal(stats.SkullDamage * (1f + PriestStats.GnashingDamage), skull.Damage, 3);
    }

    [Fact]
    public void DeathAndDecay_LaysAConeOfRot_ThatRotsWhatIsInIt_ForThreeSeconds()
    {
        var field = PriestTesting.QuietField();
        var inFront = PriestTesting.Sturdy(field, 0.5f, 4f);
        var wide = PriestTesting.Sturdy(field, 3.5f, 2.5f);        // 54 degrees off: at the cone's edge, 110 wide
        var behind = PriestTesting.Sturdy(field, 0f, -3f);
        var beyond = PriestTesting.Sturdy(field, 0f, 9f);
        var skulls = new PlagueSkulls(new Random(1));
        var stats = PriestTesting.With((UnholyTree.DeathAndDecay, 1));

        skulls.SpewDecay(Vector3D<float>.Zero, 0f, stats);
        var hits = PriestTesting.Run(skulls, stats, field, 3.5f);

        float Taken(Enemy e) => hits.Where(h => h.Enemy == e && h.Source == PriestSource.Rot).Sum(h => h.Damage);
        Assert.Equal(PriestStats.DecayConeDamage * PriestStats.DecayConeSeconds, Taken(inFront), 1f);
        Assert.True(Taken(wide) > 0f);
        Assert.Equal(0f, Taken(behind));
        Assert.Equal(0f, Taken(beyond));
        Assert.Empty(skulls.Patches);
    }

    [Fact]
    public void GraveSoil_Slows_AndNecropolis_PlaguesWhatStandsInRot()
    {
        var field = PriestTesting.QuietField();
        var enemy = PriestTesting.Sturdy(field, 0f, 3f);
        var skulls = new PlagueSkulls(new Random(1));
        var stats = PriestTesting.With((UnholyTree.GraveSoil, 1), (UnholyTree.Necropolis, 1));

        skulls.RotAt(new Vector3D<float>(0f, 0f, 3f), stats);
        PriestTesting.Run(skulls, stats, field, 0.2f);

        Assert.True(enemy.IsChilled);
        Assert.True(skulls.StacksOn(enemy) > 0);
        Assert.Equal(stats.DecayTime(PriestStats.StepRotSeconds), 2f * PriestStats.StepRotSeconds, 3);   // Necropolis: twice as long
    }

    [Fact]
    public void TheAuraOfDecay_RotsWhatIsClose()
    {
        var field = PriestTesting.QuietField();
        var close = PriestTesting.Sturdy(field, 2f, 0f);
        var far = PriestTesting.Sturdy(field, 8f, 0f);
        var skulls = new PlagueSkulls(new Random(1));
        var stats = PriestTesting.With((UnholyTree.AuraOfDecay, 1));

        var hits = PriestTesting.Run(skulls, stats, field, 2f);

        Assert.Equal(2f * PriestStats.AuraDamage, hits.Where(h => h.Enemy == close).Sum(h => h.Damage), PriestStats.AuraDamage * 0.3f);
        Assert.DoesNotContain(hits, h => h.Enemy == far);
    }
}

public class PriestClassTests
{
    private static List<Strike> Blocks(Enemy attacker, int count) => Enumerable.Range(0, count).Select(_ => new Strike(attacker, 10f, Blocked: true)).ToList();

    [Fact]
    public void ABlock_SpewsDeathAndDecay_AQuarterOfTheTime()
    {
        var field = PriestTesting.QuietField();
        var attacker = PriestTesting.Sturdy(field, 0f, 1f);
        var priest = PriestTesting.Priest((UnholyTree.DeathAndDecay, 1));
        int spews = 0;
        for (int i = 0; i < 800; i++)
        {
            priest.AnswerStrikes(Blocks(attacker, 1), Vector3D<float>.Zero, 0f, field, new PlayerHealth(100f), new DamageNumbers());
            spews += priest.Skulls.Sprays.Count;
            priest.Skulls.Reset();
        }

        Assert.InRange(spews / 800f, 0.2f, 0.3f);
    }

    [Fact]
    public void WithoutDeathAndDecay_ABlockSpewsNothing()
    {
        var field = PriestTesting.QuietField();
        var attacker = PriestTesting.Sturdy(field, 0f, 1f);
        var priest = PriestTesting.Priest();

        priest.AnswerStrikes(Blocks(attacker, 50), Vector3D<float>.Zero, 0f, field, new PlayerHealth(100f), new DamageNumbers());

        Assert.Empty(priest.Skulls.Patches);
    }

    [Fact]
    public void BoneArmour_RaisesABarrier_OnEveryBlock_UpToItsCap()
    {
        var field = PriestTesting.QuietField();
        var attacker = PriestTesting.Sturdy(field, 0f, 1f);
        var priest = PriestTesting.Priest((UnholyTree.BoneArmour, 1));
        var health = new PlayerHealth(100f);

        priest.AnswerStrikes(Blocks(attacker, 1), Vector3D<float>.Zero, 0f, field, health, new DamageNumbers());
        Assert.Equal(8f, health.Barrier, 3);

        priest.AnswerStrikes(Blocks(attacker, 10), Vector3D<float>.Zero, 0f, field, health, new DamageNumbers());
        Assert.Equal(25f, health.Barrier, 3);
    }

    [Fact]
    public void SoulHarvest_HealsForEveryPlaguedDeath()
    {
        var field = PriestTesting.QuietField();
        var victim = PriestTesting.Sturdy(field, 0f, 30f);
        var priest = PriestTesting.Priest((UnholyTree.SoulHarvest, 1));
        var health = new PlayerHealth(200f);
        health.TakeDamage(100f);
        priest.Skulls.Infect(victim, priest.Stats);

        field.Damage(victim, 1e6f);
        priest.Fight(PriestTesting.Step, PriestTesting.Hand, Vector3D<float>.Zero, PriestTesting.North, stunned: false, field, PriestTesting.FlatGround, health,
            new DamageNumbers());

        Assert.Equal(102f, health.Current, 3);   // 1% of 200
    }

    [Fact]
    public void TheLevelUpPool_OffersTheRotOnlyWithDeathAndDecay()
    {
        var plain = new PriestStats();
        var withDecay = PriestTesting.With((UnholyTree.DeathAndDecay, 1));
        Assert.False(PriestUpgrades.Offered(PriestUpgrade.SpreadingRot, plain));
        Assert.True(PriestUpgrades.Offered(PriestUpgrade.SpreadingRot, withDecay));
        Assert.True(PriestUpgrades.Offered(PriestUpgrade.WickedSkull, plain));

        var choices = PriestUpgrades.Roll(plain, new Random(1));
        Assert.Equal(3, choices.Count);
        Assert.Equal(3, choices.Select(c => c.Upgrade).Distinct().Count());
    }
}

public class PriestItemTests
{
    private static ItemBonuses With(params string[] ids)
    {
        var bonuses = new ItemBonuses();
        foreach (var id in ids)
        {
            ItemCatalog.All.Single(i => i.Id == id).ApplyOne(bonuses);
        }

        return bonuses;
    }

    [Fact]
    public void ThePriestsItems_DoSomethingForEveryone_AndMoreForThePriest()
    {
        var mask = new PriestStats { Items = With("plague_doctors_mask") };
        Assert.Equal(4, mask.PlagueStacks);
        Assert.Equal(0.92f, mask.DamageTaken, 4);

        var whisper = new PriestStats { Items = With("whispering_skull") };
        Assert.Equal(PriestStats.BasePierce + 1, whisper.Pierce);

        var bell = new PriestStats { Items = With("sepulchre_bell") };
        Assert.Equal(PriestStats.DecayConeSeconds + 2f, bell.DecayTime(PriestStats.DecayConeSeconds), 3);
        Assert.Equal(0.15f, bell.BlockChance, 3);

        var dust = new PriestStats { Items = With("grave_dust") };
        Assert.True(dust.PlagueDamage > new PriestStats().PlagueDamage * 1.13f);
    }

    [Fact]
    public void TheBoneboundAegis_RaisesABoneBarrier_ForThePriest()
    {
        var field = PriestTesting.QuietField();
        var attacker = PriestTesting.Sturdy(field, 0f, 1f);
        var priest = PriestTesting.Priest();
        priest.Stats.Items = With("bonebound_aegis");
        var health = new PlayerHealth(100f);

        priest.AnswerStrikes(new[] { new Strike(attacker, 10f, Blocked: true) }, Vector3D<float>.Zero, 0f, field, health, new DamageNumbers());

        Assert.Equal(10f, health.Barrier, 3);
    }

    [Fact]
    public void ThePriestsEpicsAndLegendary_AreLockedBehindItsBounties()
    {
        foreach (var (item, bounty) in new[] { ("tome_of_pestilence", "plaguebringer"), ("sepulchre_bell", "lord_of_decay"), ("bonebound_aegis", "deep_priest") })
        {
            Assert.Equal(item, Bounties.All.Single(b => b.Id == bounty).UnlocksItem);
        }

        var profile = new Profile();
        var run = new RunRecord(Kills: 1500, ElitesKilled: 0, BossesKilled: 0, Seconds: 600f, Won: false, Level: 10, ClassId: "priest");
        Bounties.Settle(run, profile, new TreeProgress(UnholyTree.Tree, new TreeSave()));
        Assert.True(profile.HasBounty("plaguebringer"));
    }
}
