using ArenaMaster.Game.Combat;
using ArenaMaster.Game.Items;
using ArenaMaster.Game.Mage;
using ArenaMaster.Game.Paladin;
using ArenaMaster.Game.Progression;
using ArenaMaster.Game.Ranger;
using ArenaMaster.Game.Shaman;
using Silk.NET.Maths;

namespace ArenaMaster.Game.Tests;

public class NewLevelUpTests
{
    [Fact]
    public void ThePaladinsNewUpgrades_DoWhatTheySay()
    {
        var stats = new PaladinStats { Tree = DefianceBonuses.From(new Dictionary<string, int> { [DefianceTree.CrownOfThorns] = 1, ["bash"] = 1 }) };
        stats.Increase(PaladinUpgrade.SanctifiedGround);
        stats.Increase(PaladinUpgrade.ShieldSlam);
        stats.Increase(PaladinUpgrade.ZealotsEye);
        stats.Increase(PaladinUpgrade.Steadfast);
        stats.Increase(PaladinUpgrade.BrambleMail);

        Assert.Equal(PaladinStats.BaseCircleHealing * 1.25f, stats.CircleHealing, 3);
        Assert.Equal(12f + 15f, stats.BashDamage, 3);
        Assert.Equal(PaladinStats.BaseCritChance + 0.06f, stats.CritChance, 3);
        Assert.Equal(stats.BlockChance(false, false) + 0.05f, stats.BlockChance(true, false), 3);
        Assert.Equal(PaladinStats.BaseThornsInterval / 1.2f, stats.ThornsInterval, 3);
    }

    [Fact]
    public void BrambleMail_OnlyComesUp_WithThorns()
    {
        var plain = new PaladinStats();
        var thorned = new PaladinStats { Tree = DefianceBonuses.From(new Dictionary<string, int> { [DefianceTree.CrownOfThorns] = 1 }) };

        Assert.False(PaladinUpgrades.Offered(PaladinUpgrade.BrambleMail, plain));
        Assert.True(PaladinUpgrades.Offered(PaladinUpgrade.BrambleMail, thorned));
        Assert.True(PaladinUpgrades.Offered(PaladinUpgrade.SanctifiedGround, plain));
    }

    [Fact]
    public void TheMagesNewUpgrades_DoWhatTheySay()
    {
        var stats = new MageStats { Tree = FrostBonuses.From(new Dictionary<string, int> { [FrostTree.DeepFreeze] = 1, [FrostTree.FrostBlast] = 1 }) };
        stats.Increase(MageUpgrade.GlacialSpikes);
        stats.Increase(MageUpgrade.IceArmor);
        stats.Increase(MageUpgrade.Shatterpoint);
        stats.Increase(MageUpgrade.DeepChill);
        stats.Increase(MageUpgrade.BlastPower);

        Assert.Equal(1.2f, stats.ChilledMultiplier, 3);
        Assert.Equal(0.94f, stats.DamageTaken, 3);
        Assert.Equal(1.15f, stats.EliteMultiplier, 3);
        Assert.Equal(MageStats.BaseFreezeChance + 0.03f, stats.FreezeChance, 3);
        Assert.Equal(MageStats.BaseFreezeDuration + 0.3f, stats.FreezeDuration, 3);
        Assert.Equal(MageStats.BaseBlastDamage * 1.25f, stats.BlastShare, 3);
    }

    [Fact]
    public void DeepChillAndBlastPower_NeedTheirMajors()
    {
        var plain = new MageStats();

        Assert.False(MageUpgrades.Offered(MageUpgrade.DeepChill, plain));
        Assert.False(MageUpgrades.Offered(MageUpgrade.BlastPower, plain));
        Assert.True(MageUpgrades.Offered(MageUpgrade.GlacialSpikes, plain));
    }

    [Fact]
    public void TheShamansNewUpgrades_DoWhatTheySay()
    {
        var stats = new ShamanStats { Tree = AlignmentBonuses.From(new Dictionary<string, int> { [AlignmentTree.LightningRod] = 1 }) };
        stats.Increase(ShamanUpgrade.StormBolt);
        stats.Increase(ShamanUpgrade.Conductive);
        stats.Increase(ShamanUpgrade.HeavySphere);
        stats.Increase(ShamanUpgrade.Insulation);
        stats.Increase(ShamanUpgrade.RodMastery);

        Assert.Equal(ShamanStats.BaseLifetime + 1f, stats.Lifetime, 3);
        Assert.Equal(stats.BallDamage * ShamanStats.BaseForkShare * 1.2f, stats.ForkDamage, 3);
        Assert.Equal(ShamanStats.BaseBallRadius * 1.2f, stats.BallRadius, 3);
        Assert.Equal(0.94f, stats.DamageTaken, 3);
        Assert.Equal(ShamanStats.RodSeconds + 1.5f, stats.RodDuration, 3);
        Assert.Equal(stats.BallDamage * ShamanStats.RodShare * 1.2f, stats.RodDamage, 3);
    }

    [Fact]
    public void RodMastery_NeedsLightningRod_AndEveryPoolStillRollsThreeDifferentCards()
    {
        Assert.False(ShamanUpgrades.Offered(ShamanUpgrade.RodMastery, new ShamanStats()));

        for (int seed = 0; seed < 20; seed++)
        {
            Assert.Equal(3, PaladinUpgrades.Roll(new PaladinStats(), new Random(seed)).Select(c => c.Upgrade).Distinct().Count());
            Assert.Equal(3, MageUpgrades.Roll(new MageStats(), new Random(seed)).Select(c => c.Upgrade).Distinct().Count());
            Assert.Equal(3, ShamanUpgrades.Roll(new ShamanStats(), new Random(seed)).Select(c => c.Upgrade).Distinct().Count());
        }
    }

    [Fact]
    public void EveryClass_OffersSomethingForAttackOrCastSpeed()
    {
        Assert.Contains(RangerUpgrades.All, u => u.Description.Contains("attack speed"));
        Assert.Contains(PaladinUpgrades.All, u => u.Description.Contains("nova frequency"));
        Assert.Contains(MageUpgrades.All, u => u.Description.Contains("cast speed"));
        Assert.Contains(ShamanUpgrades.All, u => u.Description.Contains("cast speed"));
    }
}

public class StormItemTests
{
    private static ItemBonuses With(params string[] ids)
    {
        var inventory = new ItemInventory();
        foreach (var id in ids)
        {
            inventory.Add(ItemCatalog.All.Single(i => i.Id == id));
        }

        return inventory.Bonuses;
    }

    [Fact]
    public void AChain_IsAForkAnArrowChainOrAPierce_OrNovaDamage()
    {
        var coil = With("conductors_coil");

        Assert.Equal(ShamanStats.BaseForks + 1, new ShamanStats { Items = coil }.Forks);
        Assert.Equal(1, new RangerStats { Items = coil }.Chains);
        Assert.Equal(1, new MageStats { Items = coil }.Pierce);
        Assert.Equal(PaladinStats.BaseNovaDamage * 1.1f, new PaladinStats { Items = coil }.NovaDamage, 3);
    }

    [Fact]
    public void TheHorn_MultipliesEveryClassesAreas()
    {
        var horn = With("stormcallers_horn");

        Assert.Equal(PaladinStats.BaseNovaRadius * 1.3f, new PaladinStats { Items = horn }.NovaRadius, 3);
        Assert.Equal(MageStats.BaseBlastRadius * 1.3f, new MageStats { Items = horn }.BlastRadius, 3);
        Assert.Equal(ShamanStats.BaseZapRadius * 1.3f, new ShamanStats { Items = horn }.ZapRadius, 3);
        Assert.Equal(RangerStats.RainRadius * 1.3f, new RangerStats { Items = horn }.RainPatch, 3);
    }

    [Fact]
    public void TheGroundingCharm_SoftensEnemyShots()
    {
        var field = new EnemyField(new Random(1)) { TargetCount = 0, RangedDamageTaken = With("grounding_charm").RangedDamageTaken };
        field.Spawn(new Vector3D<float>(10f, 0f, 0f), EnemyKind.CrossbowGhoul).AttackCooldown = 0f;
        var health = new PlayerHealth(100f);
        var strikes = new List<Strike>();

        for (float t = 0f; t < 2.5f; t += 1f / 60f)
        {
            health.Update(1f / 60f);
            field.Update(1f / 60f, new PlayerTarget(Vector3D<float>.Zero, true, health, new PlayerCondition()), (_, _) => 0f);
            strikes.AddRange(field.Strikes);
        }

        Assert.Equal(EnemyKind.CrossbowGhoul.Attacks[0].Damage * 0.75f, Assert.Single(strikes).Damage, 3);
    }

    [Fact]
    public void TheThunderstone_StrikesTheNearestEnemy_EverySixSeconds()
    {
        var field = new EnemyField(new Random(1)) { TargetCount = 0 };
        var near = field.Spawn(new Vector3D<float>(4f, 0f, 0f));
        near.Health = 1000f;
        var far = field.Spawn(new Vector3D<float>(9f, 0f, 0f));
        far.Health = 1000f;
        var effects = new ItemEffects();
        var hits = new List<ItemHit>();
        var items = With("thunderstone");

        for (float t = 0f; t < 2f * ItemEffects.SkyStrikeInterval + 0.3f; t += 0.1f)
        {
            effects.Update(0.1f, Vector3D<float>.Zero, items, field, new PlayerHealth(100f), false, hits);
        }

        Assert.Equal(2, hits.Count);
        Assert.All(hits, h => Assert.Equal(near, h.Enemy));
        Assert.All(hits, h => Assert.Equal(40f, h.Damage, 3));
    }

    [Fact]
    public void TheShamansEpicAndLegendary_AreUnlockedByShamanBounties()
    {
        var profile = new Profile();
        var tree = new TreeProgress(AlignmentTree.Tree, new TreeSave());
        var horn = ItemCatalog.All.Single(i => i.Id == "stormcallers_horn");
        var crown = ItemCatalog.All.Single(i => i.Id == "crown_of_storms");
        Assert.False(Bounties.IsUnlocked(horn, profile));
        Assert.False(Bounties.IsUnlocked(crown, profile));

        Bounties.Settle(new RunRecord(700, 0, 0, 600f, false, 20, "mage"), profile, tree);
        Assert.False(Bounties.IsUnlocked(horn, profile));   // not as the Shaman

        Bounties.Settle(new RunRecord(700, 0, 0, 600f, false, 20, "shaman"), profile, tree);
        Assert.True(Bounties.IsUnlocked(horn, profile));
        Assert.False(Bounties.IsUnlocked(crown, profile));
    }
}
