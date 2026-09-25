using ArenaMaster.Game.Combat;
using ArenaMaster.Game.Ranger;
using Silk.NET.Maths;

namespace ArenaMaster.Game.Tests;

public class ExperienceTests
{
    [Fact]
    public void EachLevel_NeedsFiveMoreThanTheLast()
    {
        Assert.Equal(5, Experience.RequiredFor(1));
        Assert.Equal(10, Experience.RequiredFor(2));
        Assert.Equal(15, Experience.RequiredFor(3));
    }

    [Fact]
    public void ABigHaul_CanLevelUpSeveralTimes_AndKeepsTheRemainder()
    {
        var experience = new Experience();

        int gained = experience.Add(5 + 10 + 3);   // through level 2 and 3, 3 toward 4

        Assert.Equal(2, gained);
        Assert.Equal(3, experience.Level);
        Assert.Equal(3, experience.Current);
        Assert.Equal(0.2f, experience.Progress, 4);
    }
}

public class XpGemTests
{
    private static readonly Vector3D<float> PlayerFeet = Vector3D<float>.Zero;

    [Fact]
    public void AGemOutOfReach_StaysPut()
    {
        var gems = new XpGemField();
        var gem = gems.Drop(new Vector3D<float>(10f, 0f, 0f), 1);
        var start = gem.Position;

        var collected = new List<XpGem>();
        int gained = gems.Update(0.5f, PlayerFeet, pickupRadius: 3f, collected);

        Assert.Equal(0, gained);
        Assert.Equal(start, gem.Position);
        Assert.False(gem.Attracted);
    }

    [Fact]
    public void AGemInReach_FliesInAndIsCollected()
    {
        var gems = new XpGemField();
        gems.Drop(new Vector3D<float>(2.5f, 0f, 0f), 3);

        var collected = new List<XpGem>();
        int gained = 0;
        for (int i = 0; i < 60 && gems.Gems.Count > 0; i++)
        {
            gained += gems.Update(1f / 60f, PlayerFeet, pickupRadius: 3f, collected);
        }

        Assert.Equal(3, gained);
        Assert.Empty(gems.Gems);
        Assert.Single(collected);
    }

    [Fact]
    public void AnAttractedGem_KeepsChasingAPlayerWhoRunsOff()
    {
        var gems = new XpGemField();
        var gem = gems.Drop(new Vector3D<float>(2f, 0f, 0f), 1);
        gems.Update(1f / 60f, PlayerFeet, pickupRadius: 3f, new List<XpGem>());
        Assert.True(gem.Attracted);

        var farAway = new Vector3D<float>(-20f, 0f, 0f);
        int gained = 0;
        for (int i = 0; i < 120 && gems.Gems.Count > 0; i++)
        {
            gained += gems.Update(1f / 60f, farAway, pickupRadius: 3f, new List<XpGem>());
        }

        Assert.Equal(1, gained);
    }
}

public class RangerUpgradeTests
{
    [Fact]
    public void ARoll_OffersThreeDifferentUpgrades()
    {
        var choices = RangerUpgrades.Roll(new RangerStats(), new Random(3));

        Assert.Equal(3, choices.Count);
        Assert.Equal(3, choices.Select(c => c.Upgrade).Distinct().Count());
        Assert.All(choices, c => Assert.Equal(1, c.NewLevel));
    }

    [Fact]
    public void AMaxedUpgrade_IsNoLongerOffered()
    {
        var stats = new RangerStats();
        var info = RangerUpgrades.Info(RangerUpgrade.PiercingArrows);
        for (int i = 0; i < info.MaxLevel + 2; i++)
        {
            stats.Increase(RangerUpgrade.PiercingArrows);   // past the max: stays at the max
        }

        Assert.Equal(info.MaxLevel, stats.LevelOf(RangerUpgrade.PiercingArrows));
        for (int seed = 0; seed < 50; seed++)
        {
            Assert.DoesNotContain(RangerUpgrades.Roll(stats, new Random(seed)), c => c.Upgrade == RangerUpgrade.PiercingArrows);
        }
    }

    [Fact]
    public void WithEverythingMaxed_TheOnlyOfferIsAHeal()
    {
        var stats = new RangerStats();
        foreach (var info in RangerUpgrades.All)
        {
            for (int i = 0; i < info.MaxLevel; i++)
            {
                stats.Increase(info.Upgrade);
            }
        }

        var choice = Assert.Single(RangerUpgrades.Roll(stats, new Random(1)));
        Assert.Null(choice.Upgrade);
    }

    [Fact]
    public void Upgrades_ChangeTheNumbersTheyNamed()
    {
        var stats = new RangerStats();
        stats.Increase(RangerUpgrade.SharpenedTips);
        stats.Increase(RangerUpgrade.SharpenedTips);
        stats.Increase(RangerUpgrade.SplitShot);
        stats.Increase(RangerUpgrade.Vitality);

        Assert.Equal(RangerStats.BaseDamage * 1.4f, stats.Damage, 3);
        Assert.Equal(2, stats.ArrowsPerShot);
        Assert.Equal(RangerStats.BaseMaxHealth + 20f, stats.MaxHealth);
        Assert.Equal(RangerStats.BaseFireInterval, stats.FireInterval);   // untouched

        stats.Reset();
        Assert.Equal(RangerStats.BaseDamage, stats.Damage);
    }
}

public class RangerShotTests
{
    private static float? FlatGround(float x, float z) => MathF.Abs(x) < 200f && MathF.Abs(z) < 200f ? 0f : null;

    private static EnemyField QuietField() => new(new Random(1)) { TargetCount = 0 };

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 2)]
    [InlineData(5, 3)]
    public void APiercingArrow_GoesThroughAsManyAsItsPierceAllows(int pierce, int expectedHits)
    {
        var enemies = QuietField();
        var ghouls = new[] { 5f, 8f, 11f }.Select(x => enemies.Spawn(new Vector3D<float>(x, 0f, 0f))).ToList();
        var arrows = new RangerArrows(new Random(1));
        arrows.Fire(new Vector3D<float>(0f, 1f, 0f), new Vector3D<float>(1f, 0f, 0f), speed: 50f, range: 60f, damage: 5f, pierce: pierce);

        var hits = new List<ArrowHit>();
        for (int i = 0; i < 30; i++)
        {
            hits.AddRange(arrows.Update(1f / 60f, enemies, FlatGround, new List<Arrow>()));
        }

        Assert.Equal(expectedHits, hits.Count);
        Assert.Equal(expectedHits, hits.Select(h => h.Enemy).Distinct().Count());   // never the same one twice
        Assert.Equal(ghouls.Take(expectedHits), hits.Select(h => h.Enemy));          // nearest first
    }

    [Fact]
    public void ACertainCrit_DoublesTheDamage()
    {
        var enemies = QuietField();
        var ghoul = enemies.Spawn(new Vector3D<float>(5f, 0f, 0f));
        var arrows = new RangerArrows(new Random(1));
        arrows.Fire(new Vector3D<float>(0f, 1f, 0f), new Vector3D<float>(1f, 0f, 0f), speed: 50f, range: 60f, damage: 10f, critChance: 1f);

        var hit = Assert.Single(arrows.Update(0.2f, enemies, FlatGround, new List<Arrow>()));

        Assert.True(hit.Crit);
        Assert.Equal(10f * RangerStats.BaseCritMultiplier, hit.Damage);
        Assert.Equal(EnemyKind.Ghoul.MaxHealth - 20f, ghoul.Health);
    }

    [Fact]
    public void ASplitShot_FansEvenlyAroundTheAim()
    {
        var aim = Vector3D.Normalize(new Vector3D<float>(0.2f, -0.1f, 1f));

        var fan = RangerBow.Fan(aim, 3, 10f).ToList();

        Assert.Equal(3, fan.Count);
        Assert.Equal(aim, fan[1]);
        float Degrees(Vector3D<float> a, Vector3D<float> b) =>
            MathF.Acos(Math.Clamp(Vector3D.Dot(Vector3D.Normalize(new Vector3D<float>(a.X, 0f, a.Z)), Vector3D.Normalize(new Vector3D<float>(b.X, 0f, b.Z))), -1f, 1f)) * 180f / MathF.PI;
        Assert.Equal(10f, Degrees(fan[0], fan[1]), 2);
        Assert.Equal(10f, Degrees(fan[1], fan[2]), 2);
        Assert.All(fan, d => Assert.Equal(aim.Y, d.Y, 5));   // the fan is flat: same climb for every arrow
    }

    [Fact]
    public void Kills_AreReportedOnceForDrops()
    {
        var enemies = QuietField();
        var ghoul = enemies.Spawn(new Vector3D<float>(5f, 0f, 0f));

        enemies.Damage(ghoul, 1000f);

        Assert.Equal(new[] { ghoul }, enemies.TakeNewlyKilled());
        Assert.Empty(enemies.TakeNewlyKilled());
    }
}
