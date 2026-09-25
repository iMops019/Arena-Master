using ArenaMaster.Game.Combat;
using ArenaMaster.Game.Ranger;
using Silk.NET.Maths;

namespace ArenaMaster.Game.Tests;

public class RangerArrowTests
{
    private static float? FlatGround(float x, float z) => MathF.Abs(x) < 200f && MathF.Abs(z) < 200f ? 0f : null;

    private static EnemyField QuietField() => new(new Random(1)) { TargetCount = 0 };

    [Fact]
    public void AnArrow_HitsTheEnemyInItsPath_EvenInOneLongFrame()
    {
        var enemies = QuietField();
        var ghoul = enemies.Spawn(new Vector3D<float>(10f, 0f, 0f));
        var arrows = new RangerArrows(new Random(1));
        arrows.Fire(new Vector3D<float>(0f, 1.2f, 0f), new Vector3D<float>(1f, 0f, 0f), speed: 50f, range: 60f, damage: 12f);

        var gone = new List<Arrow>();
        var hits = arrows.Update(0.5f, enemies, FlatGround, gone);   // 25 m in one step: straight through where the ghoul stands

        var hit = Assert.Single(hits);
        Assert.Same(ghoul, hit.Enemy);
        Assert.Equal(EnemyKind.Ghoul.MaxHealth - 12f, ghoul.Health);
        Assert.Single(gone);
        Assert.Empty(arrows.Arrows);
    }

    [Fact]
    public void AMissedArrow_SticksInTheGround_ThenGoes()
    {
        var arrows = new RangerArrows(new Random(1));
        arrows.Fire(new Vector3D<float>(0f, 1f, 0f), new Vector3D<float>(1f, -0.5f, 0f), speed: 20f, range: 60f, damage: 12f);

        var gone = new List<Arrow>();
        for (int i = 0; i < 30; i++)
        {
            arrows.Update(1f / 60f, QuietField(), FlatGround, gone);
        }

        var arrow = Assert.Single(arrows.Arrows);
        Assert.True(arrow.Stuck);
        var tip = arrow.Position + arrow.Heading * 0.48f;   // the model's tip is 0.48 m ahead of its middle
        Assert.True(tip.Y <= 0f, $"tip at y = {tip.Y} is not in the ground");
        Assert.True(arrow.Position.Y > -0.2f, "the arrow is buried whole");

        for (float t = 0f; t < RangerArrows.StuckLifetime + 0.1f; t += 1f / 60f)
        {
            arrows.Update(1f / 60f, QuietField(), FlatGround, gone);
        }

        Assert.Empty(arrows.Arrows);
    }

    [Fact]
    public void AnArrow_DropsOutAtTheEndOfItsRange()
    {
        var arrows = new RangerArrows(new Random(1));
        arrows.Fire(new Vector3D<float>(0f, 5f, 0f), new Vector3D<float>(0f, 0f, 1f), speed: 50f, range: 30f, damage: 12f);

        var gone = new List<Arrow>();
        for (int i = 0; i < 40; i++)
        {
            arrows.Update(1f / 60f, QuietField(), FlatGround, gone);   // 0.67 s: past the 0.6 s a 30 m range allows
        }

        Assert.Empty(arrows.Arrows);
        Assert.Single(gone);
    }
}

public class RangerAimTests
{
    private static readonly Vector3D<float> Origin = new(0f, 1.3f, 0f);
    private static readonly Vector3D<float> Ahead = new(0f, 0f, 1f);

    [Fact]
    public void Aim_ConvergesOnTheCrosshairTarget()
    {
        var target = new Vector3D<float>(3f, 0.5f, 30f);

        var direction = RangerBow.AimDirection(Origin, target, cameraFront: Vector3D.Normalize(new Vector3D<float>(0.1f, -0.1f, 1f)), aimFlat: Ahead);

        var expected = Vector3D.Normalize(target - Origin);
        Assert.Equal(expected.X, direction.X, 4);
        Assert.Equal(expected.Y, direction.Y, 4);
        Assert.Equal(expected.Z, direction.Z, 4);
    }

    [Fact]
    public void Aim_FallsBackToTheCameraDirection_WhenTheTargetIsBehindOrUnderfoot()
    {
        var cameraFront = Vector3D.Normalize(new Vector3D<float>(0f, -0.6f, 1f));

        Assert.Equal(cameraFront, RangerBow.AimDirection(Origin, new Vector3D<float>(0f, 0f, -4f), cameraFront, Ahead));   // behind the Ranger
        Assert.Equal(cameraFront, RangerBow.AimDirection(Origin, new Vector3D<float>(0f, 0f, 0.5f), cameraFront, Ahead));  // at its feet
    }
}
