using ArenaMaster.Game.Combat;
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
    public void TheCave_IsChambersAndTunnels_WithRockPastTheRoofBetweenThem()
    {
        // The map's edge, the rock between the chambers, and camp's side of the rock.
        foreach (var (x, z) in new[] { (0f, 250f), (0f, -250f), (250f, 0f), (-250f, -60f), (250f, -250f), (100f, 100f), (-100f, -100f), (-75f, 190f), (-190f, 90f) })
        {
            Assert.True(CaveLayout.Blocked(x, z), $"no rock at ({x}, {z})");
            Assert.True(Height(x, z) > CaveLayout.CeilingHeight + 5f, $"the rock at ({x}, {z}) is only {Height(x, z)} high");
        }

        foreach (var (px, pz, _) in CaveLayout.Pillars)
        {
            Assert.True(CaveLayout.Blocked(px, pz));
            Assert.True(Height(px, pz) > CaveLayout.CeilingHeight + 5f, "a pillar holds the roof up");
        }

        // A tunnel is floor down its middle and rock either side.
        foreach (var passage in CaveLayout.Passages)
        {
            var middle = (passage.From + passage.To) * 0.5f;
            var across = Vector2D.Normalize(new Vector2D<float>(passage.To.Y - passage.From.Y, passage.From.X - passage.To.X));
            Assert.False(CaveLayout.Blocked(middle.X, middle.Y), $"the tunnel at {middle} is blocked");
            Assert.True(CaveLayout.Blocked(middle.X + across.X * (passage.HalfWidth * 2f + 10f), middle.Y + across.Y * (passage.HalfWidth * 2f + 10f))
                || CaveLayout.ChamberAt(middle.X + across.X * (passage.HalfWidth * 2f + 10f), middle.Y + across.Y * (passage.HalfWidth * 2f + 10f)) is not null,
                $"the tunnel at {middle} has no wall");
        }
    }

    [Fact]
    public void EveryChamber_CanBeReachedFromTheStart_AlongTheFloor()
    {
        var flow = new CaveFlow(CaveLayout.Grid);
        var start = CampLayout.RunStart;
        flow.Update(new Vector3D<float>(start.X, 0f, start.Y), 1f);
        foreach (var chamber in CaveLayout.Chambers)
        {
            // Its middle, or near it (a pillar may stand in the middle).
            float best = new[] { (0f, 0f), (12f, 0f), (-12f, 0f), (0f, 12f), (0f, -12f) }
                .Min(o => flow.Distance(chamber.Centre.X + o.Item1, chamber.Centre.Y + o.Item2));
            Assert.True(float.IsFinite(best), $"{chamber.Name} can't be reached");
            Assert.True(best >= Vector2D.Distance(chamber.Centre, start) - 30f);   // along the floor, never shorter than straight
        }

        Assert.Equal("The Landing", CaveLayout.ChamberAt(start.X, start.Y)?.Name);
        Assert.Equal("The Pit", CaveLayout.ChamberAt(BossArena.Centre.X, BossArena.Centre.Y)?.Name);
    }

    [Fact]
    public void AnEnemyBehindARock_IsSteeredRoundIt_AndOneInTheOpenComesStraight()
    {
        var flow = new CaveFlow(CaveLayout.Grid);
        var (px, pz, radius) = CaveLayout.Pillars[1];   // (-42, -32) in the Landing
        var player = new Vector3D<float>(px + radius + 12f, 0f, pz);
        flow.Update(player, 1f);

        Assert.Null(flow.Steer(new Vector3D<float>(0f, 0f, 20f), new Vector3D<float>(10f, 0f, 20f)));   // a clear line: straight

        var enemy = new Vector3D<float>(px - radius - 12f, 0f, pz);   // the pillar right between them
        Assert.NotNull(flow.Steer(enemy, player));
        for (int step = 0; step < 200 && flow.Steer(enemy, player) is { } way; step++)
        {
            enemy += way * 0.5f;
            Assert.False(CaveLayout.Blocked(enemy.X, enemy.Z), "it walked into the rock");
        }

        Assert.True(CaveLayout.Grid.ClearLine(new Vector2D<float>(enemy.X, enemy.Z), new Vector2D<float>(player.X, player.Z)));   // round the pillar, in sight
    }

    [Fact]
    public void TheWay_IsWorkedOutAgainOnlyWhenThePlayerChangesCell_AndNotTooOften()
    {
        var flow = new CaveFlow(CaveLayout.Grid);
        flow.Update(new Vector3D<float>(0f, 0f, 10f), 1f);
        Assert.Equal(1, flow.Refreshes);
        flow.Update(new Vector3D<float>(0.3f, 0f, 10.2f), 0.01f);       // the same cell
        Assert.Equal(1, flow.Refreshes);
        flow.Update(new Vector3D<float>(8f, 0f, 10f), 0.05f);           // a new cell, too soon
        Assert.Equal(1, flow.Refreshes);
        flow.Update(new Vector3D<float>(8f, 0f, 10f), CaveFlow.RefreshSeconds);
        Assert.Equal(2, flow.Refreshes);
        Assert.Equal(0f, flow.Distance(8f, 10f));
        Assert.True(float.IsPositiveInfinity(flow.Distance(100f, 100f)));   // in the rock
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
    public void TheCavesLights_AreItsCrystalsLanternsFungusPoolsAndFires_AndTheNearestFewAreLit()
    {
        var lights = CaveLayout.Lights(World.Value);
        int glowing = CaveLayout.Things().Count(t => t.Thing is CaveThing.Crystals or CaveThing.Lantern);
        Assert.Equal(glowing + CaveLife.Fungus().Count + CaveLife.Pools.Length + CaveLayout.Braziers.Length + BossArena.Fires().Count(), lights.Count);

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

/// <summary>The enemy field's hooks for walls: steering round them, and spawning only where there is a way to the player.</summary>
public class EnemySteeringTests
{
    private static float? Flat(float x, float z) => 0f;

    [Fact]
    public void ASteeredEnemy_WalksTheWayItIsSteered_AndDoesNotAttackUntilItHasAClearLine()
    {
        var field = new EnemyField(new Random(1)) { TargetCount = 0, Steer = (_, _) => new Vector3D<float>(1f, 0f, 0f) };
        var brute = field.Spawn(Vector3D<float>.Zero, EnemyKind.Brute);
        brute.AttackCooldown = 0f;
        var player = new PlayerHealth(1_000f);
        var feet = new Vector3D<float>(0f, 0f, 6f);   // in reach of its lunge
        for (int i = 0; i < 30; i++)
        {
            field.Update(1f / 60f, new PlayerTarget(feet, true, player, new PlayerCondition()), Flat);
        }

        Assert.Null(brute.Attack);
        Assert.True(brute.Position.X > 1f && MathF.Abs(brute.Position.Z) < 0.2f);

        field.Steer = (_, _) => null;   // a clear line: it comes at the player, and attacks
        for (int i = 0; i < 30 && brute.Attack is null; i++)
        {
            field.Update(1f / 60f, new PlayerTarget(feet, true, player, new PlayerCondition()), Flat);
        }

        Assert.NotNull(brute.Attack);
    }

    [Fact]
    public void Spawns_KeepToWhereTheFilterAllows()
    {
        var field = new EnemyField(new Random(2)) { TargetCount = 40, SpawnInterval = 0f, SpawnFilter = (x, _) => x > 0f };
        var player = new PlayerHealth(1_000_000f);
        for (int i = 0; i < 20; i++)
        {
            field.Update(1f / 60f, new PlayerTarget(Vector3D<float>.Zero, true, player, new PlayerCondition()), Flat);
        }

        Assert.True(field.Enemies.Count > 10);
        Assert.All(field.Enemies, e => Assert.True(e.Position.X > 0f - 2f));   // (they may have taken a step since)
    }
}

/// <summary>What lives in the cave (World/CaveLife): pools and their drips, fungus, bats and wisps.</summary>
public class CaveLifeTests
{
    [Fact]
    public void ThePools_LieInDipsOnTheFloor_AndTheDropsFallIntoThem_ThenRipple()
    {
        foreach (var (at, radius) in CaveLife.Pools)
        {
            Assert.True(CaveLayout.IsFloor(at.X, at.Y), $"a pool at {at} is off the floor");
            Assert.True(CaveLife.PoolDip(at.X, at.Y) > 0.8f);
            Assert.Equal(0f, CaveLife.PoolDip(at.X + radius + 2f, at.Y));
            Assert.False(CaveLayout.Things().Any(t => Vector2D.Distance(t.At, at) < radius + 2f), "something stands in a pool");
        }

        var drips = CaveLife.Drips();
        Assert.True(drips.Count >= CaveLife.Pools.Length * 2);
        Assert.All(drips, d => Assert.True(CaveLife.Pools.Any(p => Vector2D.Distance(d.At, p.At) < p.Radius)));

        // A drop falls, lands on the water, and a ripple spreads where it landed.
        var (spot, period, offset) = drips[0];
        float start = period - offset;   // when this drop starts to fall
        var drops = new List<CrowdInstance>();
        var ripples = new List<CrowdInstance>();
        CaveLife.Drops(start + 0.05f, drops, ripples);
        var drop = drops.Single(d => MathF.Abs(d.Position.X - spot.X) < 1e-3f && MathF.Abs(d.Position.Z - spot.Y) < 1e-3f);
        Assert.True(drop.Position.Y > CaveLife.WaterHeight + CaveLife.DropHeight - 0.5f);

        CaveLife.Drops(start + CaveLife.FallSeconds + 0.3f, drops, ripples);
        Assert.DoesNotContain(drops, d => MathF.Abs(d.Position.X - spot.X) < 1e-3f && MathF.Abs(d.Position.Z - spot.Y) < 1e-3f);
        Assert.Contains(ripples, r => MathF.Abs(r.Position.X - spot.X) < 1e-3f && r.Scale > 0.3f);
    }

    [Fact]
    public void TheFungus_GrowsWhereTheFloorMeetsTheRock()
    {
        var fungus = CaveLife.Fungus();
        Assert.True(fungus.Count > 50);
        Assert.All(fungus, f =>
        {
            Assert.InRange(CaveLayout.FloorDistance(f.At.X, f.At.Y), -2.5f, 0.2f);
            Assert.False(CaveLayout.OnSurface(f.At.X, f.At.Y));
        });
    }

    [Fact]
    public void TheBats_CircleInTheBigHalls_UnderTheRoof_AndTheWispsDriftThroughEveryChamber()
    {
        var up = new List<CrowdInstance>();
        var down = new List<CrowdInstance>();
        CaveLife.Bats(12.3f, up, down);
        Assert.Equal(CaveLayout.Chambers.Sum(CaveLife.BatsIn), up.Count + down.Count);
        Assert.True(up.Count > 0 && down.Count > 0);   // flapping, not all in step
        Assert.All(up.Concat(down), b =>
        {
            Assert.True(b.Position.Y < CaveLayout.CeilingHeight - 6f);
            Assert.NotNull(CaveLayout.ChamberAt(b.Position.X, b.Position.Z) ?? (CaveLayout.Blocked(b.Position.X, b.Position.Z) ? null : CaveLayout.Chambers[0]));
        });

        var green = new List<CrowdInstance>();
        var violet = new List<CrowdInstance>();
        CaveLife.Wisps(40f, (_, _) => CaveLayout.FloorHeight, green, violet);
        Assert.Equal(CaveLayout.Chambers.Sum(CaveLife.WispsIn), green.Count + violet.Count);
        Assert.True(green.Count + violet.Count >= 30);
        Assert.True(violet.Count > 0);
        foreach (var chamber in CaveLayout.Chambers)
        {
            Assert.True(CaveLife.WispsIn(chamber) >= 1, $"no wisps in {chamber.Name}");
        }
    }
}
