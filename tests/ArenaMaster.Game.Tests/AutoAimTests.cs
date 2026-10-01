using ArenaMaster.Game.Combat;
using ArenaMaster.Game.Shaman;
using ArenaMaster.Game.Warrior;
using Silk.NET.Maths;

namespace ArenaMaster.Game.Tests;

/// <summary>Every class aims on its own now: the nearest enemy that a hero faces, and the Shaman's and the Warrior's attacks waiting for something to hit.</summary>
public class AutoAimTests
{
    private const float Step = 1f / 60f;

    private static EnemyField QuietField() => new(new Random(1)) { TargetCount = 0 };

    [Fact]
    public void Nearest_IsTheClosestEnemy_InAnyDirection()
    {
        var field = QuietField();
        field.Spawn(new Vector3D<float>(0f, 0f, 9f));
        var behind = field.Spawn(new Vector3D<float>(0f, 0f, -4f));

        Assert.Same(behind, field.Nearest(Vector3D<float>.Zero, 15f));
        Assert.Null(field.Nearest(Vector3D<float>.Zero, 2f));
    }

    [Fact]
    public void Nearest_TakesACrate_OnlyWhenThereIsNoEnemy()
    {
        var field = QuietField();
        var crate = field.Spawn(new Vector3D<float>(2f, 0f, 0f), EnemyKind.Crate);
        Assert.Same(crate, field.Nearest(Vector3D<float>.Zero, 15f));

        var ghoul = field.Spawn(new Vector3D<float>(12f, 0f, 0f));
        Assert.Same(ghoul, field.Nearest(Vector3D<float>.Zero, 15f));
    }

    [Fact]
    public void TheAxes_Wait_WithNothingInReach_ThenSwingAtOnce()
    {
        var axes = new CleaveAxes(new Random(1));
        var stats = new WarriorStats();
        var field = QuietField();
        var hits = new List<CleaveHit>();
        for (int i = 0; i < 120; i++)
        {
            axes.Update(Step, Vector3D<float>.Zero, null, stats, field, canSwing: true, hits);
        }

        Assert.Empty(axes.Waves);
        Assert.Equal(0, axes.Swings);
        Assert.Equal(0f, axes.SwingIn);

        axes.Update(Step, Vector3D<float>.Zero, 0f, stats, field, canSwing: true, hits);
        Assert.Equal(1, axes.Swings);
    }

    [Fact]
    public void TheStorm_Waits_WithNoTarget_ThenThrowsAtOnce()
    {
        var storm = new RollingLightning(new Random(1));
        var stats = new ShamanStats();
        var field = QuietField();
        var hits = new List<StormHit>();
        for (int i = 0; i < 180; i++)
        {
            storm.Update(Step, ShamanTesting.Hand, Vector3D<float>.Zero, null, false, stats, field, ShamanTesting.FlatGround, ShamanTesting.NoObstacles, true, hits);
        }

        Assert.Empty(storm.Balls);
        Assert.Equal(0, storm.Casts);

        storm.Update(Step, ShamanTesting.Hand, Vector3D<float>.Zero, new Vector3D<float>(0f, 0f, 10f), false, stats, field, ShamanTesting.FlatGround,
            ShamanTesting.NoObstacles, true, hits);
        Assert.Equal(1, storm.Casts);
    }

    [Fact]
    public void LobSight_PicksTheNearestInThrowingRange_AndKeepsIt()
    {
        var field = QuietField();
        var stats = new ShamanStats();
        field.Spawn(new Vector3D<float>(0f, 0f, stats.ThrowRange + 5f));
        var first = field.Spawn(new Vector3D<float>(0f, 0f, 12f));
        var sight = new LobSight();
        sight.Update(field, Vector3D<float>.Zero, stats.ThrowRange, Step);
        Assert.Same(first, sight.Target);

        field.Spawn(new Vector3D<float>(0f, 0f, -11f));   // a little nearer: not enough to switch
        sight.Update(field, Vector3D<float>.Zero, stats.ThrowRange, Step);
        Assert.Same(first, sight.Target);

        var close = field.Spawn(new Vector3D<float>(4f, 0f, 0f));
        sight.Update(field, Vector3D<float>.Zero, stats.ThrowRange, Step);
        Assert.Same(close, sight.Target);
    }

    [Fact]
    public void LobSight_LeadsAWalkingEnemy_ByTheBallsFlight()
    {
        var field = QuietField();
        var stats = new ShamanStats();
        var ghoul = field.Spawn(new Vector3D<float>(0f, 0f, 15f));
        var sight = new LobSight();
        const float Speed = 3f;   // walking in toward the Shaman
        for (int i = 0; i < 90; i++)
        {
            ghoul.Position += new Vector3D<float>(0f, 0f, -Speed * Step);
            sight.Update(field, Vector3D<float>.Zero, stats.ThrowRange, Step);
        }

        var aim = sight.AimPoint(ShamanTesting.Hand, stats)!.Value;
        float flight = RollingLightning.FlightTime(ShamanTesting.Hand, aim, stats);
        var there = ghoul.Position + new Vector3D<float>(0f, 0f, -Speed * flight);

        Assert.True(aim.Z < ghoul.Position.Z - 1f, "the throw isn't led");
        Assert.True(Vector3D.Distance(aim, there) < 0.3f, $"thrown {Vector3D.Distance(aim, there):0.00} m from where it will be");
    }
}
