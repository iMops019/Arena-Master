using ArenaMaster.Game.Camp;
using ArenaMaster.Game.Delve;
using ArenaMaster.Game.World;
using CEngine.Core;
using Silk.NET.Maths;

namespace ArenaMaster.Game.Tests;

/// <summary>The Delve's cave (World/CaveLayout) and the stairs down to it at camp.</summary>
public class CaveTests
{
    private static Terrain Shaped()
    {
        var terrain = Terrain.CreateFlat(513, 512f, 60f, CaveLayout.FloorHeight);
        CaveLayout.ShapeGround(terrain);
        CampLayout.ShapeGround(terrain);
        BossArena.ShapeGround(terrain);
        return terrain;
    }

    private static readonly Lazy<Terrain> World = new(Shaped);

    private static float Height(float x, float z) => World.Value.TryGetHeight(x, z, out float h) ? h : float.NaN;

    [Fact]
    public void RunsStart_OnOpenFloor_InFrontOfTheTunnel_AndTheArenaIsFloorToo()
    {
        var start = CampLayout.RunStart;
        Assert.True(CaveLayout.IsFloor(start.X, start.Y));
        Assert.False(CaveLayout.Blocked(start.X, start.Y));
        Assert.True(CaveLayout.Tunnel.Y < start.Y);   // the player steps out of it, north, into the cave
        Assert.Equal(CaveLayout.FloorHeight, Height(start.X, start.Y), 1);

        // The run area round the start is open: most of a 60 m circle is floor.
        int open = 0, all = 0;
        for (float a = 0f; a < MathF.Tau; a += 0.2f)
        {
            for (float r = 10f; r <= 60f; r += 10f)
            {
                all++;
                open += CaveLayout.Blocked(MathF.Sin(a) * r, MathF.Cos(a) * r) ? 0 : 1;
            }
        }

        Assert.True(open > all * 0.8f, $"only {open} of {all} spots round the start are open");

        foreach (float r in new[] { 0f, 10f, BossArena.Radius - 2f })
        {
            Assert.True(CaveLayout.IsFloor(BossArena.Centre.X + r, BossArena.Centre.Y), $"the arena at {r} m");
        }
    }

    [Fact]
    public void TheCave_IsWalledIn_RoundTheEdge_AndFromCamp_WithWallsPastTheRoof()
    {
        foreach (var (x, z) in new[] { (0f, 250f), (0f, -250f), (250f, 0f), (-250f, -60f), (250f, -250f) })
        {
            Assert.True(CaveLayout.Blocked(x, z), $"no wall at ({x}, {z})");
            Assert.True(Height(x, z) > CaveLayout.CeilingHeight + 5f, $"the wall at ({x}, {z}) is only {Height(x, z)} high");
        }

        // Between camp and the cave, all the way round: the ridge.
        for (float a = 0f; a < MathF.Tau; a += 0.1f)
        {
            var ridge = CampLayout.Centre + new Vector2D<float>(MathF.Cos(a), MathF.Sin(a)) * (CaveLayout.RidgeRadius - 12f);
            if (MathF.Abs(ridge.X) < 250f && MathF.Abs(ridge.Y) < 250f)
            {
                Assert.True(Height(ridge.X, ridge.Y) > CaveLayout.CeilingHeight, $"the ridge is low at {a:0.0}");
            }
        }

        foreach (var (px, pz, _) in CaveLayout.Pillars)
        {
            Assert.True(CaveLayout.Blocked(px, pz));
            Assert.True(Height(px, pz) > CaveLayout.CeilingHeight + 5f, "a pillar holds the roof up");
        }
    }

    [Fact]
    public void TheCaveFloor_GrowsNothing_ButCampStaysGreen()
    {
        var terrain = World.Value;
        Assert.True(terrain.IsCaveFloor(0f, 40f));
        Assert.True(terrain.IsBare(-100f, -100f));
        Assert.False(terrain.IsCaveFloor(CampLayout.Centre.X + 30f, CampLayout.Centre.Y - 30f));   // the woods round camp
        Assert.True(CaveLayout.OnSurface(CampLayout.Centre.X, CampLayout.Centre.Y));
        Assert.False(CaveLayout.OnSurface(0f, 0f));
    }

    [Fact]
    public void WhatStandsAbout_IsOnTheFloor_ClearOfTheStartTheTunnelAndTheArena()
    {
        var things = CaveLayout.Things();
        Assert.True(things.Count > 200);
        foreach (var kind in Enum.GetValues<CaveThing>())
        {
            Assert.Contains(things, t => t.Thing == kind);
        }

        foreach (var (_, at, _, scale) in things)
        {
            Assert.True(CaveLayout.IsFloor(at.X, at.Y) && CaveLayout.Wall(at.X, at.Y) <= 0.02f, $"something in a wall at {at}");
            Assert.True(Vector2D.Distance(at, CampLayout.RunStart) >= 14f);
            Assert.True(Vector2D.Distance(at, CaveLayout.Mound) >= CaveLayout.MoundFoot);
            Assert.True(Vector2D.Distance(at, BossArena.Centre) >= BossArena.Radius + 6f);
            Assert.InRange(scale, 0.5f, 2f);
        }

        Assert.Same(things, CaveLayout.Things());   // worked out once, the same every time
    }

    [Fact]
    public void TheCavesLights_AreItsCrystalsLanternsAndFires_AndTheNearestFewAreLit()
    {
        var lights = CaveLayout.Lights(World.Value);
        int glowing = CaveLayout.Things().Count(t => t.Thing is CaveThing.Crystals or CaveThing.Lantern);
        Assert.Equal(glowing + CaveLayout.Braziers.Length + BossArena.Fires().Count(), lights.Count);

        var start = CaveLayout.Ground(World.Value, CampLayout.RunStart);
        var lit = CaveLayout.Nearest(lights, start, CaveLayout.LitAtOnce);
        Assert.Equal(CaveLayout.LitAtOnce, lit.Count);
        Assert.All(lit, i => Assert.True(Vector3D.Distance(lights[i].At, start) <= CaveLayout.LightReach));
        float farthestLit = lit.Max(i => Vector3D.Distance(lights[i].At, start));
        Assert.DoesNotContain(Enumerable.Range(0, lights.Count).Except(lit), i => Vector3D.Distance(lights[i].At, start) < farthestLit - 1e-3f);
        Assert.True(CaveLayout.LitAtOnce + 5 <= 32);   // room left for the engine's own wisps and shots

        Assert.Empty(CaveLayout.Nearest(lights, new Vector3D<float>(0f, 500f, 0f), 5));   // none reach up there
    }

    [Fact]
    public void TheStairsDown_AreDugIntoCamp_AtTheirFootByTheTunnel_AndLevelWithCampAtTheirHead()
    {
        var mouth = CampLayout.Centre + Descent.Mouth;
        float camp = Height(CampLayout.Centre.X, CampLayout.Centre.Y + 5f);
        // (Within the metre grid's reach: the ground between its points is a slope.)
        Assert.Equal(camp - Descent.Drop, Height(mouth.X, mouth.Y - 0.3f), 0.2f);        // the foot
        Assert.Equal(camp - Descent.Drop / 2f, Height(mouth.X, mouth.Y - 0.6f - Descent.Run / 2f), 0.2f);   // half way up
        Assert.Equal(camp, Height(mouth.X, mouth.Y - Descent.Run - 1f), 0.2f);           // the head
        Assert.Equal(0f, Descent.Depth(Descent.HalfWidth + 0.5f, Descent.Mouth.Y - 2f));  // not beside them

        var gate = CampLayout.Stations.Single(s => s.Station == CampStation.Gate);
        Assert.Equal(Descent.Model, gate.Model);
        var foot = new Vector3D<float>(mouth.X, 0f, mouth.Y - 1.5f);
        Assert.Equal(CampStation.Gate, CampLayout.StationNear(foot)?.Station);   // the Delve chart opens at the foot of the stairs
    }

    [Fact]
    public void TheCavesModels_AreInTheAssets()
    {
        string models = Path.Combine(EngineAssets.RepoRoot, "assets", "models");
        foreach (string file in new[] { CaveLayout.CeilingModel, CaveLayout.StalagmiteModel, CaveLayout.CrystalModel, CaveLayout.BonesModel, CaveLayout.LanternModel,
                     "cave_lantern_fire.glb", CaveLayout.TunnelModel, Descent.Model })
        {
            Assert.True(File.Exists(Path.Combine(models, file)), $"{file} is missing");
        }
    }
}
