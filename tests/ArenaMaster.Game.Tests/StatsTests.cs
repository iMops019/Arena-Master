using ArenaMaster.Game.Combat;
using ArenaMaster.Game.Progression;
using ArenaMaster.Game.Ui;
using Silk.NET.Maths;

namespace ArenaMaster.Game.Tests;

public class StatsTests
{
    private static Strike Blow(EnemyKind kind, bool blocked = false) => new(new Enemy(1, kind, Vector3D<float>.Zero, EnemyScaling.None), 10f, blocked);

    [Fact]
    public void ATally_CountsKillsByKindAndRarity()
    {
        var tally = new RunTally();
        tally.Begin();
        tally.Kill(EnemyKind.Ghoul, MonsterRarity.Normal);
        tally.Kill(EnemyKind.Ghoul, MonsterRarity.Magic);
        tally.Kill(EnemyKind.Brute, MonsterRarity.Legendary);

        Assert.Equal(2, tally.KillsByKind["Ghoul"]);
        Assert.Equal(1, tally.KillsByKind["Ghoul Brute"]);
        Assert.Equal(1, tally.KillsByRarity["Magic"]);
        Assert.Equal(1, tally.KillsByRarity["Legendary"]);
        Assert.False(tally.KillsByRarity.ContainsKey("Normal"));
    }

    [Fact]
    public void AHit_EndsTheKillStreak_ButABlockDoesNot()
    {
        var tally = new RunTally();
        tally.Begin();
        for (int i = 0; i < 5; i++)
        {
            tally.Kill(EnemyKind.Ghoul, MonsterRarity.Normal);
        }

        tally.Struck(Blow(EnemyKind.Ghoul, blocked: true));
        tally.Kill(EnemyKind.Ghoul, MonsterRarity.Normal);
        Assert.Equal(6, tally.Streak);

        tally.Struck(Blow(EnemyKind.GhoulMage));
        tally.Kill(EnemyKind.Ghoul, MonsterRarity.Normal);
        Assert.Equal(1, tally.Streak);
        Assert.Equal(6, tally.BestStreak);
        Assert.Equal(1, tally.Blocks);
        Assert.Equal(1, tally.HitsTaken);
        Assert.Equal("Ghoul Mage", tally.LastHitBy);
    }

    [Fact]
    public void Settling_AddsTheRunToTheTotalsTheBestsAndItsClass()
    {
        var stats = new StatsRecord();
        var tally = new RunTally();
        tally.Begin();
        tally.Kill(EnemyKind.Ghoul, MonsterRarity.Normal);
        tally.Struck(Blow(EnemyKind.CrossbowGhoul));
        tally.Slain = true;
        tally.DamageDealt = 5000f;
        tally.Silver = 120;
        stats.Settle(new RunRecord(300, 2, 0, 600f, false, 20, "mage"), tally);

        tally.Begin();
        tally.DamageDealt = 1000f;
        stats.Settle(new RunRecord(100, 0, 1, 1800f, true, 30, "mage"), tally);

        Assert.Equal(1, stats.Deaths);
        Assert.Equal(300, stats.MostKills);
        Assert.Equal(30, stats.HighestLevel);
        Assert.Equal(2400.0, stats.SecondsPlayed);
        Assert.Equal(6000.0, stats.DamageDealt);
        Assert.Equal(5000f, stats.MostDamage);
        Assert.Equal(2, stats.ElitesKilled);
        Assert.Equal(1, stats.BossesKilled);
        Assert.Equal(1, stats.DeathsBy["Crossbow Ghoul"]);
        Assert.Equal(120, stats.SilverEarned);
        Assert.Equal(19 + 29, stats.LevelUps);

        var mage = stats.Class("mage");
        Assert.Equal(2, mage.Runs);
        Assert.Equal(1, mage.Wins);
        Assert.Equal(1, mage.Deaths);
        Assert.Equal(400, mage.Kills);
        Assert.Equal(300, mage.MostKills);
    }

    [Fact]
    public void OnlyADeath_IsPutDownToWhatLandedTheLastBlow()
    {
        var stats = new StatsRecord();
        var tally = new RunTally();
        tally.Begin();
        tally.Struck(Blow(EnemyKind.Brute));
        tally.Returned = true;
        stats.Settle(new RunRecord(10, 0, 0, 60f, false, 2, "ranger"), tally);

        Assert.Empty(stats.DeathsBy);
        Assert.Null(stats.Nemesis());
        Assert.Equal(1, stats.ReturnedToCamp);
    }

    [Fact]
    public void AClearedDelveNode_CountsForTheFastestClearAndItsClassesDeepest()
    {
        var stats = new StatsRecord();
        var tally = new RunTally();
        tally.Begin();
        stats.Settle(new RunRecord(500, 0, 1, 640f, false, 18, "priest", Depth: 7, DelveCleared: true), tally);
        tally.Begin();
        stats.Settle(new RunRecord(500, 0, 1, 700f, false, 18, "priest", Depth: 4, DelveCleared: true), tally);

        Assert.Equal(640f, stats.FastestDelveClear);
        Assert.Equal(2, stats.DelveRuns);
        Assert.Equal(7, stats.Class("priest").DeepestCleared);
        Assert.Equal(2, stats.Class("priest").NodesCleared);
    }

    [Fact]
    public void TheMostPlayedClass_IsTheOneWithTheMostRuns_TimeBreakingATie()
    {
        var stats = new StatsRecord();
        Assert.Null(stats.MostPlayedClass());

        var tally = new RunTally();
        tally.Begin();
        stats.Settle(new RunRecord(0, 0, 0, 100f, false, 1, "ranger"), tally);
        stats.Settle(new RunRecord(0, 0, 0, 300f, false, 1, "shaman"), tally);
        Assert.Equal("shaman", stats.MostPlayedClass());

        stats.Settle(new RunRecord(0, 0, 0, 10f, false, 1, "ranger"), tally);
        Assert.Equal("ranger", stats.MostPlayedClass());
    }

    [Fact]
    public void Stats_LastInTheSave()
    {
        string path = Path.Combine(Path.GetTempPath(), $"am-stats-{Guid.NewGuid():N}.json");
        try
        {
            var profile = new Profile();
            profile.Stats.KillsByKind["Ghoul"] = 42;
            profile.Stats.Class("warrior").Runs = 3;
            profile.Stats.BestStreak = 77;
            ProfileStore.Save(profile, path);

            var loaded = ProfileStore.Load(path);
            Assert.Equal(42, loaded.Stats.KillsOf("Ghoul"));
            Assert.Equal(3, loaded.Stats.Class("warrior").Runs);
            Assert.Equal(77, loaded.Stats.BestStreak);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void TheBestiary_HasEveryCreatureOnce_AndNoCrate()
    {
        Assert.Equal(EnemyKind.Foes.Count, EnemyKind.Foes.Select(k => k.Name).Distinct().Count());
        Assert.DoesNotContain(EnemyKind.Foes, k => k.IsProp);
        Assert.All(EnemyKind.Foes, Assert.NotNull);
    }

    [Fact]
    public void BigNumbersAndLongTimes_AreWrittenShort()
    {
        Assert.Equal("950", StatsScreen.Big(950));
        Assert.Equal("12.4K", StatsScreen.Big(12_400));
        Assert.Equal("3.1M", StatsScreen.Big(3_100_000));
        Assert.Equal("12m 05s", StatsScreen.Duration(725));
        Assert.Equal("2h 03m", StatsScreen.Duration(7380));
    }
}
