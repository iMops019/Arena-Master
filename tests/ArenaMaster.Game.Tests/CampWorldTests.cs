using System.Numerics;
using ArenaMaster.Game.Camp;
using ArenaMaster.Game.Ui;
using CEngine.Core;
using Silk.NET.Maths;

namespace ArenaMaster.Game.Tests;

public class CampWallTests
{
    private static Vector2D<float> Gate => CampLayout.Stations.Single(s => s.Station == CampStation.Gate).Offset;

    [Fact]
    public void TheWall_GoesAllTheWayRound_WithOnlyTheGateInItsGap()
    {
        var pieces = CampWalls.Pieces().ToList();
        Assert.Equal(CampWalls.Sides, pieces.Count(p => p.Model == CampWalls.PostModel));
        Assert.Equal(CampWalls.Sides * CampWalls.PiecesPerSide - CampWalls.GatePieces, pieces.Count(p => p.Model == CampWalls.PieceModel));

        // Walk round the wall line a step at a time: every point is covered by a piece, a corner post, or the gate (7 m wide, in the middle of the north side).
        for (int step = 0; step < 720; step++)
        {
            float angle = MathF.Tau * step / 720f;
            var direction = new Vector2D<float>(MathF.Cos(angle), MathF.Sin(angle));

            // Where that direction meets the octagon: through the side whose middle is nearest in angle.
            int side = (int)MathF.Round((angle - MathF.PI / 2f) / (MathF.Tau / CampWalls.Sides));
            float sideAngle = CampWalls.SideAngle(side);
            float along = CampWalls.Apothem / MathF.Cos(angle - sideAngle);
            var point = direction * along;

            bool onPiece = pieces.Any(p => p.Model == CampWalls.PieceModel && Vector2D.Distance(p.Offset, point) <= CampWalls.PieceLength / 2f + 0.01f);
            bool onPost = pieces.Any(p => p.Model == CampWalls.PostModel && Vector2D.Distance(p.Offset, point) <= 0.4f);
            bool inGate = MathF.Abs(point.X - Gate.X) <= 3.5f + 0.01f && MathF.Abs(point.Y - Gate.Y) < 0.1f;
            Assert.True(onPiece || onPost || inGate, $"a gap in the wall at {point}");
        }
    }

    [Fact]
    public void TheGate_StandsInTheWall_FacingTheSpawnAcrossTheFire()
    {
        Assert.Equal(0f, Gate.X, 3);
        Assert.Equal(CampWalls.Apothem, Gate.Y, 3);
        var spawn = CampLayout.Spawn - CampLayout.Centre;
        Assert.True(spawn.Y < 0f);   // the spawn looks at the fire, and the gate beyond it
    }

    [Fact]
    public void Everything_IsInsideTheWall_AndTheClearingReachesPastIt()
    {
        // How far out toward the wall a point is: its distance along the outward direction of whichever side it is nearest.
        static float Outward(Vector2D<float> at) => Enumerable.Range(0, CampWalls.Sides)
            .Max(side => at.X * MathF.Cos(CampWalls.SideAngle(side)) + at.Y * MathF.Sin(CampWalls.SideAngle(side)));

        float inside = CampWalls.Apothem - 1.5f;   // clear of the wall's rails with room for the piece itself
        foreach (var (station, _, offset, _) in CampLayout.Stations.Where(s => s.Station != CampStation.Gate))
        {
            Assert.True(Outward(offset) < inside, $"{station} is too near the wall");
        }

        foreach (var (model, offset, _, _) in CampLayout.Decor)
        {
            Assert.True(Outward(offset) < inside, $"{model} at {offset} is too near the wall");
        }

        Assert.True(CampLayout.ClearingRadius > CampWalls.Circumradius + 0.5f);
        var post = CampWalls.Pieces().First(p => p.Model == CampWalls.PostModel).Offset + CampLayout.Centre;
        Assert.True(CampLayout.InClearing(post.X, post.Y));
    }

    [Fact]
    public void TheDressing_KeepsClearOfTheStations_TheSpawnAndTheFire()
    {
        foreach (var (model, offset, _, _) in CampLayout.Decor)
        {
            foreach (var (station, _, at, _) in CampLayout.Stations)
            {
                Assert.True(Vector2D.Distance(offset, at) > 2f, $"{model} at {offset} crowds the {station}");
            }

            Assert.True(Vector2D.Distance(offset, CampLayout.Spawn - CampLayout.Centre) > 2.5f, $"{model} is on the spawn");
            Assert.True(offset.Length > 2.2f, $"{model} is in the fire");
            Assert.False(CampLayout.OnPath(offset.X, offset.Y) && model != "camp_bedroll.glb", $"{model} is on a path");
        }
    }

    [Fact]
    public void EveryCampModel_IsInTheAssets()
    {
        string models = Path.Combine(EngineAssets.RepoRoot, "assets", "models");
        var files = CampLayout.Decor.Select(d => d.Model)
            .Concat(CampLayout.Stations.Select(s => s.Model))
            .Concat(new[] { CampWalls.PieceModel, CampWalls.PostModel, "camp_firepit.glb", QuartermasterNpc.Model })
            .Distinct();
        foreach (string file in files)
        {
            Assert.True(File.Exists(Path.Combine(models, file)), $"{file} is missing");
        }
    }

    [Fact]
    public void FiresBurn_OnEachBrazierAndTheCookingFire_EachWithItsOwnId()
    {
        var fires = CampLayout.SmallFires().ToList();
        Assert.Equal(CampLayout.Decor.Count(d => d.Model is "camp_brazier.glb" or "camp_cookpot.glb"), fires.Count);
        Assert.Equal(fires.Count, fires.Select(f => f.Id).Distinct().Count());
        Assert.DoesNotContain(CampLayout.FireId, fires.Select(f => f.Id));
        Assert.True(fires.Count + 1 <= 16);   // the engine's cap on fires
    }
}

public class QuartermasterTests
{
    [Fact]
    public void HeStandsBehindTheCounter_FacingTheWayTheStallFaces()
    {
        var stall = CampLayout.Stations.Single(s => s.Station == CampStation.Quartermaster).Offset;
        var (offset, yaw) = QuartermasterNpc.Stand();
        Assert.True(Vector2D.Distance(offset, stall) < 1f);
        Assert.True(offset.Length > stall.Length);   // the far side of the counter from the fire
        Assert.Equal(CampLayout.FacingFire(stall), yaw, 4);
    }

    [Fact]
    public void Turn_MovesAModelsFront_TheWayItsYawFaces()
    {
        float yaw = 0.7f;
        var front = CampLayout.Turn(new Vector2D<float>(0f, 1f), yaw);
        Assert.Equal(MathF.Sin(yaw), front.X, 4);
        Assert.Equal(MathF.Cos(yaw), front.Y, 4);
    }

    [Fact]
    public void HisModel_IsSkinned_WithALoopingIdleThatMoves()
    {
        var model = SkinnedModel.Load(Path.Combine(EngineAssets.RepoRoot, "assets", "models", QuartermasterNpc.Model));
        Assert.Equal(8, model.JointCount);
        var idle = model.Clips[QuartermasterNpc.IdleClip];
        Assert.Equal(QuartermasterNpc.IdleSeconds, idle.Duration, 2);

        var start = new Matrix4x4[model.JointCount];
        var end = new Matrix4x4[model.JointCount];
        var lookingAway = new Matrix4x4[model.JointCount];
        idle.Sample(0f, start);
        idle.Sample(idle.Duration, end);
        idle.Sample(2f, lookingAway);
        for (int j = 0; j < model.JointCount; j++)
        {
            AssertClose(start[j], end[j]);   // it loops without a jump
        }

        const int head = 3;
        Assert.True(Vector3.Distance(Vector3.Transform(new Vector3(0f, 1.6f, 0.13f), start[head]), Vector3.Transform(new Vector3(0f, 1.6f, 0.13f), lookingAway[head])) > 0.03f);
    }

    [Fact]
    public void ClipTime_WrapsRoundTheLoop()
    {
        Assert.Equal(1f, QuartermasterNpc.ClipTime(QuartermasterNpc.IdleSeconds + 1f), 4);
        Assert.Equal(0.5f, QuartermasterNpc.ClipTime(0.5f), 4);
    }

    private static void AssertClose(Matrix4x4 a, Matrix4x4 b)
    {
        for (int r = 0; r < 4; r++)
        {
            for (int c = 0; c < 4; c++)
            {
                Assert.Equal(a[r, c], b[r, c], 3);
            }
        }
    }
}

public class ScreenFadeTests
{
    [Fact]
    public void Travel_FadesOut_MakesTheMoveInTheDark_ThenFadesIn()
    {
        var fade = new ScreenFade();
        int moves = 0;
        fade.Start("Camp", () => moves++);
        Assert.True(fade.Active);
        Assert.Equal(0f, fade.Alpha, 3);

        Assert.Equal(ScreenFade.Step.None, fade.Advance(ScreenFade.OutSeconds / 2f));
        Assert.Equal(0.5f, fade.Alpha, 2);
        Assert.Equal(0, moves);

        Assert.Equal(ScreenFade.Step.Dark, fade.Advance(ScreenFade.OutSeconds));
        Assert.Equal(1, moves);
        Assert.Equal(1f, fade.Alpha, 3);

        Assert.Equal(ScreenFade.Step.None, fade.Advance(ScreenFade.HoldSeconds));   // held black, now fading in
        Assert.Equal(ScreenFade.Step.None, fade.Advance(ScreenFade.InSeconds / 2f));
        Assert.InRange(fade.Alpha, 0.4f, 0.6f);
        Assert.Equal(ScreenFade.Step.Done, fade.Advance(ScreenFade.InSeconds));
        Assert.False(fade.Active);
        Assert.Equal(0f, fade.Alpha);
        Assert.Equal(1, moves);   // only ever once
    }

    [Fact]
    public void ALongFrame_TakesOneStepAtATime()
    {
        var fade = new ScreenFade();
        fade.Start("Into the Wilds", () => { });
        Assert.Equal(ScreenFade.Step.Dark, fade.Advance(10f));
        Assert.True(fade.Active);
        Assert.Equal(1f, fade.Alpha);
    }

    [Fact]
    public void Reveal_StartsBlack_AndCancel_StopsWithoutTheMove()
    {
        var fade = new ScreenFade();
        fade.Reveal("Camp");
        Assert.Equal(1f, fade.Alpha);
        Assert.Equal("Camp", fade.Title);

        bool moved = false;
        fade.Start("Camp", () => moved = true);
        fade.Cancel();
        Assert.False(fade.Active);
        Assert.Equal(ScreenFade.Step.None, fade.Advance(5f));
        Assert.False(moved);
    }
}
