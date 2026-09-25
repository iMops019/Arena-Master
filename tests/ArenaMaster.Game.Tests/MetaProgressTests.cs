using ArenaMaster.Game.Items;
using ArenaMaster.Game.Progression;
using ArenaMaster.Game.Ranger;
using ArenaMaster.Game.Ui;

namespace ArenaMaster.Game.Tests;

public class RunRewardTests
{
    [Fact]
    public void ARunEarnsSilver_ForKillsElitesBossesTimeAndAWin()
    {
        var lost = new RunRecord(Kills: 250, ElitesKilled: 2, BossesKilled: 0, Seconds: 11.5f * 60f, Won: false, Level: 20);
        var won = lost with { BossesKilled = 3, Seconds = 30f * 60f, Won = true };

        Assert.Equal(25 + 10 + 11 * 3, RunRewards.Silver(lost));
        Assert.Equal(25 + 10 + 180 + 30 * 3 + 250, RunRewards.Silver(won));
    }
}

public class ShopTests
{
    [Fact]
    public void Buying_TakesTheSilver_AndRaisesTheRank()
    {
        var profile = new Profile { Silver = 1000 };
        var reroll = Shop.Get(Shop.Reroll);

        Assert.True(Shop.Buy(profile, reroll));
        Assert.Equal(1000 - reroll.Costs[0], profile.Silver);
        Assert.Equal(1, Shop.RerollsPerRun(profile));
        Assert.Equal(reroll.Costs[1], Shop.NextCost(profile, reroll));
    }

    [Fact]
    public void Buying_NeedsEnoughSilver_AndStopsAtTheMax()
    {
        var profile = new Profile { Silver = 10 };
        var banish = Shop.Get(Shop.Banish);

        Assert.False(Shop.Buy(profile, banish));
        Assert.Contains("more silver", Shop.WhyNotBuy(profile, banish));

        profile.Silver = 1_000_000;
        while (Shop.Buy(profile, banish))
        {
        }

        Assert.Equal(banish.MaxRank, Shop.BanishesPerRun(profile));
        Assert.Null(Shop.NextCost(profile, banish));
    }

    [Fact]
    public void BiggerPack_RaisesTheLoadoutLimit()
    {
        var profile = new Profile { Silver = 1_000_000 };
        foreach (var item in ItemCatalog.All.Take(8))
        {
            profile.AddToStash(item.Id);
        }

        Assert.Equal(Loadout.MaxItems, Loadout.Limit(profile));
        Shop.Buy(profile, Shop.Get(Shop.PackSlot));
        Shop.Buy(profile, Shop.Get(Shop.PackSlot));

        Assert.Equal(Loadout.MaxItems + 2, Loadout.Limit(profile));
        Assert.Equal(7, ItemCatalog.All.Take(8).Count(i => Loadout.Toggle(profile, i.Id)));
    }

    [Fact]
    public void LuckyCharm_ShiftsTheOddsTowardRarerItems()
    {
        var profile = new Profile();
        Assert.Equal(RarityWeights.World, Shop.Lucky(RarityWeights.World, profile));

        profile.Shop[Shop.Luck] = 2;
        var lucky = Shop.Lucky(RarityWeights.World, profile);

        Assert.Equal(RarityWeights.World.Common, lucky.Common);
        Assert.Equal(RarityWeights.World.Rare * 1.3f, lucky.Rare, 3);
        Assert.Equal(RarityWeights.World.Legendary * 2f, lucky.Legendary, 3);
    }
}

public class BountyTests
{
    private static TreeProgress Tree(Profile profile) => new(SharpshooterTree.Tree, profile.Tree(SharpshooterTree.ClassId, SharpshooterTree.TreeId));

    private static RunRecord Run(int kills = 0, int elites = 0, int bosses = 0, float seconds = 60f, bool won = false) => new(kills, elites, bosses, seconds, won, 5);

    [Fact]
    public void ABountyPaysOnce_WhenARunMeetsIt()
    {
        var profile = new Profile();

        var first = Bounties.Settle(Run(kills: 150), profile, Tree(profile));
        var again = Bounties.Settle(Run(kills: 150), profile, Tree(profile));

        Assert.Contains(first, b => b.Id == "first_hunt");
        Assert.DoesNotContain(again, b => b.Id == "first_hunt");
        Assert.Equal(Bounties.All.Single(b => b.Id == "first_hunt").Silver, profile.Silver);
    }

    [Fact]
    public void LifetimeBounties_CountAcrossRuns()
    {
        var profile = new Profile();
        for (int i = 0; i < 9; i++)
        {
            Bounties.Settle(Run(kills: 1000), profile, Tree(profile));
        }

        Assert.False(profile.HasBounty("thousand_cuts"));
        Bounties.Settle(Run(kills: 1000), profile, Tree(profile));
        Assert.True(profile.HasBounty("thousand_cuts"));
        Assert.Equal(10, profile.Lifetime.Runs);
    }

    [Fact]
    public void TheEpicsAndLegendaries_StartLocked_AndTheirBountiesUnlockThem()
    {
        var profile = new Profile();
        var moon = ItemCatalog.All.Single(i => i.Id == "hunters_moon");

        Assert.All(ItemCatalog.All.Where(i => i.Rarity >= ItemRarity.Epic), i => Assert.False(Bounties.IsUnlocked(i, profile), $"{i.Name} should start locked"));
        Assert.All(ItemCatalog.All.Where(i => i.Rarity < ItemRarity.Epic), i => Assert.True(Bounties.IsUnlocked(i, profile)));

        Bounties.Settle(Run(bosses: 1), profile, Tree(profile));   // Regicide
        Assert.True(Bounties.IsUnlocked(moon, profile));
    }

    [Fact]
    public void ALockedRarity_FallsBackToTheNextOneDown_SoARollAlwaysGivesAnUnlockedItem()
    {
        var profile = new Profile();
        var random = new Random(1);

        for (int i = 0; i < 300; i++)
        {
            var item = ItemCatalog.Roll(random, RarityWeights.Boss, x => Bounties.IsUnlocked(x, profile));   // boss odds: only epics and legendaries
            Assert.True(Bounties.IsUnlocked(item, profile));
            Assert.Equal(ItemRarity.Rare, item.Rarity);
        }
    }

    [Fact]
    public void ChestsAndDrops_OnlyGiveUnlockedItems()
    {
        var profile = new Profile();
        var loot = new LootField(new Random(3)) { Available = item => Bounties.IsUnlocked(item, profile) };

        for (int i = 0; i < 200; i++)
        {
            Assert.True(Bounties.IsUnlocked(loot.DropItem(Silk.NET.Maths.Vector3D<float>.Zero).Item, profile));
        }
    }
}

public class RerollAndBanishTests
{
    [Fact]
    public void ABanishedUpgrade_IsNeverOfferedAgainThatRun()
    {
        var stats = new RangerStats();
        var banished = new HashSet<RangerUpgrade> { RangerUpgrade.SharpenedTips, RangerUpgrade.QuickDraw };

        for (int seed = 0; seed < 100; seed++)
        {
            Assert.DoesNotContain(RangerUpgrades.Roll(stats, new Random(seed), excluded: banished), c => c.Upgrade is { } u && banished.Contains(u));
        }
    }

    [Fact]
    public void Banishing_ReplacesThatCard_WithAFreshUpgrade()
    {
        var stats = new RangerStats();
        var random = new Random(4);
        var choices = RangerUpgrades.Roll(stats, random);
        var struck = choices[1].Upgrade!.Value;
        var banished = new HashSet<RangerUpgrade> { struck };

        var replaced = RangerUpgrades.Replace(choices, 1, stats, random, banished);

        Assert.Equal(3, replaced.Count);
        Assert.Equal(choices[0], replaced[0]);
        Assert.Equal(choices[2], replaced[2]);
        Assert.DoesNotContain(replaced, c => c.Upgrade == struck);
        Assert.Equal(3, replaced.Select(c => c.Upgrade).Distinct().Count());
    }

    [Fact]
    public void WithEverythingBanished_TheOfferIsTheHeal()
    {
        var all = RangerUpgrades.All.Select(u => u.Upgrade).ToHashSet();

        var choice = Assert.Single(RangerUpgrades.Roll(new RangerStats(), new Random(1), excluded: all));

        Assert.Null(choice.Upgrade);
    }
}
