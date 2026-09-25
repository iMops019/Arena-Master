using System.Diagnostics;
using ArenaMaster.Game.Combat;
using ArenaMaster.Game.Ranger;
using Silk.NET.Maths;

namespace ArenaMaster.Game.Tests;

public class SwarmTests
{
    private static float? FlatGround(float x, float z) => MathF.Abs(x) < 250f && MathF.Abs(z) < 250f ? 0f : null;

    private static PlayerTarget Player(Vector3D<float> feet) => new(feet, true, new PlayerHealth(1_000_000f), new PlayerCondition());

    [Fact]
    public void TheGrid_FindsNeighboursAcrossCellEdges_AndLeavesFarOnesOut()
    {
        var field = new EnemyField(new Random(1)) { TargetCount = 0 };
        var a = field.Spawn(new Vector3D<float>(3.9f, 0f, 3.9f));   // either side of a cell corner
        var b = field.Spawn(new Vector3D<float>(4.1f, 0f, 4.1f));
        var far = field.Spawn(new Vector3D<float>(40f, 0f, 40f));
        var grid = new EnemyGrid();

        grid.Rebuild(field.Enemies);
        var near = grid.Near(4f, 4f, 1f).ToList();

        Assert.Contains(a, near);
        Assert.Contains(b, near);
        Assert.DoesNotContain(far, near);
    }

    [Fact]
    public void TheGrid_FindsWhatASegmentPassesThrough()
    {
        var field = new EnemyField(new Random(1)) { TargetCount = 0 };
        var onPath = field.Spawn(new Vector3D<float>(10f, 0f, 0.5f));
        var offPath = field.Spawn(new Vector3D<float>(10f, 0f, 30f));
        var grid = new EnemyGrid();

        grid.Rebuild(field.Enemies);
        var found = grid.AlongSegment(new Vector3D<float>(0f, 1f, 0f), new Vector3D<float>(20f, 1f, 0f), 1f).ToList();

        Assert.Contains(onPath, found);
        Assert.DoesNotContain(offPath, found);
    }

    [Fact]
    public void AnArrow_StillFindsAnEnemyThatJustMoved()
    {
        var field = new EnemyField(new Random(1)) { TargetCount = 0 };
        var enemy = field.Spawn(new Vector3D<float>(10f, 0f, 0f));
        field.Update(1f / 60f, Player(new Vector3D<float>(-50f, 0f, 0f)), FlatGround);   // it walks a little toward the player (inside the leash)

        var hit = field.FirstHit(new Vector3D<float>(0f, 1f, 0f), new Vector3D<float>(20f, 1f, 0f), 0.1f, out _);

        Assert.Same(enemy, hit);
    }

    [Fact]
    public void AFarBehindField_FillsUpSeveralPerFrame()
    {
        var field = new EnemyField(new Random(1)) { TargetCount = 100, SpawnInterval = 0.001f };

        field.Update(1f / 60f, Player(Vector3D<float>.Zero), FlatGround);

        Assert.InRange(field.AliveCount, 2, 10);   // more than one, but a capped burst, not all 100 at once
    }

    [Fact]
    public void ASwarm_KeepsItsSpacing()
    {
        var field = new EnemyField(new Random(4)) { TargetCount = 0 };
        for (int i = 0; i < 60; i++)
        {
            field.Spawn(new Vector3D<float>(20f + (i % 6) * 0.1f, 0f, (i / 6) * 0.1f));   // piled almost on top of each other
        }

        for (int i = 0; i < 180; i++)
        {
            field.Update(1f / 60f, Player(new Vector3D<float>(-60f, 0f, 0f)), FlatGround);
        }

        var positions = field.Enemies.Select(e => e.Position).ToList();
        int stacked = 0;
        for (int i = 0; i < positions.Count; i++)
        {
            for (int j = i + 1; j < positions.Count; j++)
            {
                if (Vector3D.Distance(positions[i], positions[j]) < 0.2f)
                {
                    stacked++;
                }
            }
        }

        Assert.True(stacked < 3, $"{stacked} pairs still stacked on each other");
    }

    [Fact]
    public void AFullSwarm_UpdatesQuickly()
    {
        var field = new EnemyField(new Random(2)) { TargetCount = RunDirector.MaxFodder, SpawnInterval = 0.0001f };
        var player = Player(Vector3D<float>.Zero);
        for (int i = 0; i < 120; i++)
        {
            field.Update(1f / 60f, player, FlatGround);   // fill it
        }

        Assert.Equal(RunDirector.MaxFodder, field.AliveCount);

        var arrows = new RangerArrows(new Random(3));
        var watch = Stopwatch.StartNew();
        for (int frame = 0; frame < 120; frame++)
        {
            for (int a = 0; a < 5; a++)
            {
                float angle = frame * 0.37f + a;
                arrows.Fire(new Vector3D<float>(0f, 1.3f, 0f), new Vector3D<float>(MathF.Sin(angle), 0f, MathF.Cos(angle)), 50f, 60f, 1f, pierce: 2, chains: 1);
            }

            field.Update(1f / 60f, player, FlatGround);
            arrows.Update(1f / 60f, field, FlatGround, new List<Arrow>());
        }

        watch.Stop();

        // Two seconds of play with 300 enemies and a few hundred arrows in the air: well under the two seconds it simulates, leaving the frame for drawing.
        Assert.True(watch.ElapsedMilliseconds < 1000, $"took {watch.ElapsedMilliseconds} ms for 120 frames");
    }

    [Fact]
    public void GemsPastTheCap_MergeInsteadOfPilingUp()
    {
        var gems = new XpGemField();
        for (int i = 0; i < XpGemField.MaxGems + 50; i++)
        {
            gems.Drop(new Vector3D<float>(100f + i % 20, 0f, 100f + i / 20), 1);
        }

        Assert.Equal(XpGemField.MaxGems, gems.Gems.Count);
        Assert.Equal(XpGemField.MaxGems + 50, gems.Gems.Sum(g => g.Value));   // no experience lost
    }

    [Fact]
    public void DamageNumbers_AreCapped()
    {
        var numbers = new DamageNumbers();
        for (int i = 0; i < DamageNumbers.MaxShown * 3; i++)
        {
            numbers.Add(Vector3D<float>.Zero, i, kill: false);
        }

        Assert.Equal(DamageNumbers.MaxShown, numbers.Count);
    }
}
