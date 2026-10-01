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

public class BowSightTests
{
    private static readonly Vector3D<float> Bow = new(0f, 1.3f, 0f);
    private const float Range = 60f;
    private const float Frame = 1f / 60f;

    private static EnemyField QuietField() => new(new Random(1)) { TargetCount = 0 };

    [Fact]
    public void Sight_PicksTheNearestEnemy_InAnyDirection()
    {
        var enemies = QuietField();
        enemies.Spawn(new Vector3D<float>(0f, 0f, 20f));
        var behind = enemies.Spawn(new Vector3D<float>(-3f, 0f, -8f));   // behind the Ranger: the camera doesn't matter
        enemies.Spawn(new Vector3D<float>(15f, 0f, 0f));

        var sight = new BowSight();
        sight.Update(enemies.Enemies, Bow, Range, Frame);

        Assert.Same(behind, sight.Target);
    }

    [Fact]
    public void Sight_HasNoTarget_WithNothingInRange()
    {
        var enemies = QuietField();
        enemies.Spawn(new Vector3D<float>(0f, 0f, Range + 5f));

        var sight = new BowSight();
        sight.Update(enemies.Enemies, Bow, Range, Frame);

        Assert.Null(sight.Target);
        Assert.Null(sight.AimPoint(Bow, 50f));
    }

    [Fact]
    public void Sight_PassesOverAnEnemyItCannotSee()
    {
        var enemies = QuietField();
        var hidden = enemies.Spawn(new Vector3D<float>(0f, 0f, 6f));
        var seen = enemies.Spawn(new Vector3D<float>(0f, 0f, -12f));

        var sight = new BowSight();
        sight.Update(enemies.Enemies, Bow, Range, Frame, clear: (_, to) => to.Z < 0f);   // a wall in front

        Assert.Same(seen, sight.Target);
        Assert.NotSame(hidden, sight.Target);
    }

    [Fact]
    public void Sight_ShootsCratesOnlyWhenThereIsNothingToFight()
    {
        var enemies = QuietField();
        var crate = enemies.Spawn(new Vector3D<float>(0f, 0f, 4f), EnemyKind.Crate);
        var sight = new BowSight();

        sight.Update(enemies.Enemies, Bow, Range, Frame);
        Assert.Same(crate, sight.Target);

        var ghoul = enemies.Spawn(new Vector3D<float>(0f, 0f, 30f));
        sight.Update(enemies.Enemies, Bow, Range, Frame);
        Assert.Same(ghoul, sight.Target);
    }

    [Fact]
    public void Sight_KeepsItsTarget_UntilAnotherIsMuchNearer()
    {
        var enemies = QuietField();
        var first = enemies.Spawn(new Vector3D<float>(0f, 0f, 20f));
        var sight = new BowSight();
        sight.Update(enemies.Enemies, Bow, Range, Frame);

        var slightlyNearer = enemies.Spawn(new Vector3D<float>(0f, 0f, -18f));
        sight.Update(enemies.Enemies, Bow, Range, Frame);
        Assert.Same(first, sight.Target);   // no flicking between two at much the same distance

        var muchNearer = enemies.Spawn(new Vector3D<float>(5f, 0f, 0f));
        sight.Update(enemies.Enemies, Bow, Range, Frame);
        Assert.Same(muchNearer, sight.Target);
        Assert.NotSame(slightlyNearer, sight.Target);
    }

    [Fact]
    public void Sight_MovesOn_WhenItsTargetDies()
    {
        var enemies = QuietField();
        var first = enemies.Spawn(new Vector3D<float>(0f, 0f, 10f));
        var next = enemies.Spawn(new Vector3D<float>(0f, 0f, 25f));
        var sight = new BowSight();
        sight.Update(enemies.Enemies, Bow, Range, Frame);

        first.Health = 0f;
        sight.Update(enemies.Enemies, Bow, Range, Frame);

        Assert.Same(next, sight.Target);
    }

    [Fact]
    public void AimPoint_IsTheMiddleOfAStandingTarget()
    {
        var enemies = QuietField();
        var ghoul = enemies.Spawn(new Vector3D<float>(4f, 0f, 20f));
        var sight = new BowSight();
        sight.Update(enemies.Enemies, Bow, Range, Frame);
        sight.Update(enemies.Enemies, Bow, Range, Frame);

        var aim = sight.AimPoint(Bow, 50f)!.Value;

        Assert.Equal(BowSight.Middle(ghoul), aim);
        Assert.True(aim.Y > ghoul.Position.Y, "aimed at its feet");
    }

    [Fact]
    public void AimPoint_LeadsAMovingTarget_SoTheArrowMeetsIt()
    {
        var enemies = QuietField();
        var ghoul = enemies.Spawn(new Vector3D<float>(0f, 0f, 25f));
        var sight = new BowSight();
        const float Speed = 5f;   // walking across the line of fire
        for (int i = 0; i < 60; i++)
        {
            ghoul.Position += new Vector3D<float>(Speed * Frame, 0f, 0f);
            sight.Update(enemies.Enemies, Bow, Range, Frame);
        }

        const float ArrowSpeed = 50f;
        var aim = sight.AimPoint(Bow, ArrowSpeed)!.Value;
        float flight = Vector3D.Distance(Bow, aim) / ArrowSpeed;
        var there = BowSight.Middle(ghoul) + new Vector3D<float>(Speed * flight, 0f, 0f);

        Assert.True(aim.X > BowSight.Middle(ghoul).X + 1f, "the shot isn't led");
        Assert.True(Vector3D.Distance(aim, there) < 0.2f, $"aimed {Vector3D.Distance(aim, there):0.00} m from where it will be");
    }
}
