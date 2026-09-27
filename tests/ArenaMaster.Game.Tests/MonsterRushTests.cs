using ArenaMaster.Game.Combat;
using Silk.NET.Maths;

namespace ArenaMaster.Game.Tests;

public class MonsterRushTests
{
    private const float Step = 1f / 60f;

    private static float? FlatGround(float x, float z) => MathF.Abs(x) < 200f && MathF.Abs(z) < 200f ? 0f : null;

    /// <summary>Runs <paramref name="rush"/> for <paramref name="seconds"/>, returning when each rush began.</summary>
    private static List<float> Run(MonsterRush rush, float seconds, Func<float, bool>? allowed = null, float from = 0f)
    {
        var starts = new List<float>();
        for (float t = from; t < from + seconds; t += Step)
        {
            if (rush.Update(Step, allowed?.Invoke(t) ?? true))
            {
                starts.Add(t);
            }
        }

        return starts;
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void TheFirstRush_ComesBetweenThreeAndFiveMinutes_AndLastsThirtySeconds(int seed)
    {
        var rush = new MonsterRush(new Random(seed));
        float t = 0f;
        while (!rush.Update(Step, allowed: true))
        {
            t += Step;
            Assert.True(t < MonsterRush.FirstMax + 1f, "no rush by 5:00");
        }

        Assert.InRange(t, MonsterRush.FirstMin - Step, MonsterRush.FirstMax);
        Run(rush, MonsterRush.Duration - 0.1f);
        Assert.True(rush.Active);
        Run(rush, 0.2f);
        Assert.False(rush.Active);
    }

    [Fact]
    public void TheNext_ComesTwoAndAHalfToFourAndAHalfMinutesAfterTheLastEnded()
    {
        var rush = new MonsterRush(new Random(4));
        var starts = Run(rush, 30f * 60f);

        Assert.InRange(starts.Count, 4, 8);
        for (int i = 1; i < starts.Count; i++)
        {
            Assert.InRange(starts[i] - starts[i - 1] - MonsterRush.Duration, MonsterRush.GapMin - 0.1f, MonsterRush.GapMax + 0.1f);
        }
    }

    [Fact]
    public void ARushDue_WaitsWhileItIsntAllowed_ThenStartsAtOnce()
    {
        var rush = new MonsterRush(new Random(1));
        float bossGone = MonsterRush.FirstMax + 60f;

        var starts = Run(rush, bossGone + 1f, allowed: t => t >= bossGone);

        Assert.Equal(bossGone, Assert.Single(starts), 1);
    }

    [Fact]
    public void TheCrowd_BuildsOverTheRamp_ToTwiceThePlusExtra_AndIsCapped()
    {
        Assert.Equal(75, MonsterRush.Crowd(75, 0f));
        Assert.InRange(MonsterRush.Crowd(75, MonsterRush.RampSeconds / 2f), 127, 128);
        Assert.Equal(180, MonsterRush.Crowd(75, MonsterRush.RampSeconds));
        Assert.Equal(180, MonsterRush.Crowd(75, 25f));
        Assert.Equal(MonsterRush.MaxCrowd, MonsterRush.Crowd(300, 25f));
    }

    [Fact]
    public void OnlyDuringARush_TheFieldsNumbersAreRaised()
    {
        var rush = new MonsterRush(new Random(1));
        var field = new EnemyField(new Random(1)) { TargetCount = 75, SpawnInterval = 0.3f };

        rush.Apply(field);
        Assert.Equal(75, field.TargetCount);
        Assert.Equal(0.3f, field.SpawnInterval);

        rush.Start();
        Run(rush, 10f);
        rush.Apply(field);
        Assert.Equal(180, field.TargetCount);
        Assert.Equal(MonsterRush.SpawnInterval, field.SpawnInterval);
    }

    [Fact]
    public void InARush_TheFieldFloodsWithMonsters()
    {
        var field = new EnemyField(new Random(1)) { TargetCount = 75, SpawnInterval = 0f };
        var rush = new MonsterRush(new Random(1));
        var health = new PlayerHealth(1e9f);
        var condition = new PlayerCondition();
        var player = new PlayerTarget(Vector3D<float>.Zero, true, health, condition);
        for (int i = 0; i < 300; i++)   // the field filled to the director's numbers
        {
            field.Update(Step, player, FlatGround);
        }

        int before = field.AliveCount;
        rush.Start();
        for (float t = 0f; t < MonsterRush.RampSeconds + 2f; t += Step)
        {
            field.TargetCount = 75;       // what the director asks each frame
            field.SpawnInterval = 0.3f;
            rush.Update(Step, allowed: true);
            rush.Apply(field);
            field.Update(Step, player, FlatGround);
        }

        Assert.Equal(75, before);
        Assert.InRange(field.AliveCount, 175, 180);
    }
}
