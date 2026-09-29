using ArenaMaster.Game.Camp;
using ArenaMaster.Game.Combat;
using ArenaMaster.Game.Delve;
using ArenaMaster.Game.World;
using Silk.NET.Maths;

namespace ArenaMaster.Game.Tests;

/// <summary>The Stirring (World/Stirring) and the mini bosses it wakes.</summary>
public class StirringTests
{
    private static readonly (Vector2D<float>, string?) Somewhere = (new Vector2D<float>(100f, 100f), "The Bone Hall");

    [Fact]
    public void TheFirst_StirsAFewMinutesIn_WakesWhenTheCountRunsOut_AndTheNextWaitsForItToDie()
    {
        var stirring = new Stirring(new Random(4));
        Assert.Equal(StirringPhase.Waiting, stirring.Phase);
        Assert.InRange(stirring.NextIn, Stirring.FirstMin, Stirring.FirstMax);

        Assert.Equal(StirringNews.None, stirring.Update(stirring.NextIn - 1f, allowed: true, () => Somewhere));
        Assert.Equal(StirringNews.None, stirring.Update(2f, allowed: false, () => Somewhere));   // due, but not allowed (a boss on the field)
        Assert.Equal(StirringNews.None, stirring.Update(0.1f, allowed: true, () => null));        // due, but nowhere to stir yet
        Assert.Equal(StirringNews.Began, stirring.Update(0.1f, allowed: true, () => Somewhere));
        Assert.Equal(StirringPhase.Stirring, stirring.Phase);
        Assert.Contains(stirring.Kind!, MiniBosses.All);
        Assert.Equal(Somewhere.Item1, stirring.Spot);
        Assert.Equal(Stirring.StirSeconds, stirring.WakesIn);

        Assert.Equal(StirringNews.None, stirring.Update(Stirring.StirSeconds - 1f, true, () => Somewhere));
        Assert.Equal(StirringNews.Woke, stirring.Update(1.5f, true, () => Somewhere));

        var boss = new Enemy(1, stirring.Kind!, Vector3D<float>.Zero, EnemyScaling.None);
        stirring.Woken(boss);
        Assert.Equal(StirringPhase.Awake, stirring.Phase);
        Assert.InRange(stirring.NextIn, Stirring.GapMin, Stirring.GapMax);

        stirring.Update(Stirring.GapMax + 1f, true, () => Somewhere);
        Assert.Equal(StirringPhase.Awake, stirring.Phase);   // due, but the last is still alive
        boss.Health = 0f;
        stirring.Update(0.1f, true, () => Somewhere);
        Assert.Equal(StirringPhase.Waiting, stirring.Phase);
        Assert.Equal(StirringNews.Began, stirring.Update(0.1f, true, () => Somewhere));   // and then the next
    }

    [Fact]
    public void ItStirs_InAnotherChamber_AFairWayAlongTheFloor_NeverInTheBossArena()
    {
        var flow = new CaveFlow(CaveLayout.Grid);
        var start = CampLayout.RunStart;
        flow.Update(new Vector3D<float>(start.X, 0f, start.Y), 1f);
        for (int seed = 0; seed < 20; seed++)
        {
            var stirring = new Stirring(new Random(seed));
            var pick = stirring.PickSpot(start, flow.Distance, (x, z) => !CaveLayout.Blocked(x, z));
            Assert.NotNull(pick);
            var (spot, where) = pick!.Value;
            Assert.False(CaveLayout.Blocked(spot.X, spot.Y));
            Assert.NotEqual("The Landing", where);
            Assert.NotEqual(Stirring.ArenaChamber, where);
            Assert.InRange(flow.Distance(spot.X, spot.Y), Stirring.NearestPath, Stirring.FarthestPath);
        }
    }

    [Fact]
    public void TheMiniBosses_AreTheirOwn_LighterThanAnyBossHunt_TougherThanAnElite_AndPayNoGear()
    {
        foreach (var kind in MiniBosses.All)
        {
            Assert.Contains(kind, EnemyKind.Foes);
            Assert.Equal(EnemyTier.Elite, kind.Tier);
            Assert.True(kind.DrawScale > 1.3f);
            Assert.InRange(kind.MaxHealth, EnemyKind.Brute.MaxHealth * 5f, BossHunt.HollowKing.Health / 10f);
            Assert.True(kind.Attacks.Count >= 2);
            Assert.DoesNotContain(kind, new[] { DelveBosses.HollowKingUnbound, DelveBosses.MarauderUnbound, DelveBosses.Fiend, EnemyKind.HollowKing });
        }

        var reward = MiniBosses.Reward;
        Assert.Equal(100, reward.TreeExperience);
        Assert.True(reward.Silver > 0 && reward.MarkChance is > 0f and < 0.5f && reward.ItemChance is > 0f and < 1f);
    }

    [Fact]
    public void AMiniBossDrawnBig_StridesToMatch()
    {
        var clips = new Dictionary<string, float> { [EnemyMotion.IdleClip] = 3f, [EnemyMotion.WalkClip] = 2f, [EnemyMotion.DieClip] = 1f };
        var motion = new EnemyMotion();
        var hound = new Enemy(1, MiniBosses.GutterHound, Vector3D<float>.Zero, EnemyScaling.None);
        motion.Update(hound, 1f / 60f, clips);
        hound.Position = new Vector3D<float>(0f, 0f, 0.1f);
        motion.Update(hound, 1f / 60f, clips);
        hound.Position = new Vector3D<float>(0f, 0f, 0.1f + 1.7f * 0.5f);   // 0.85 m on at its size: half a metre of its model's stride
        var pose = motion.Update(hound, 1f / 60f, clips);
        Assert.Equal(EnemyMotion.WalkClip, pose.Clip);
        Assert.Equal((0.1f + 0.85f) / MiniBosses.GutterHound.DrawScale, pose.Time, 2);
    }
}
