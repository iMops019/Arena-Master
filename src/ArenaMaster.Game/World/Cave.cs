using ArenaMaster.Game.Camp;
using ArenaMaster.Game.Delve;
using CEngine.Core;
using Silk.NET.Maths;

namespace ArenaMaster.Game.World;

/// <summary>What stands on the cave floor: a spike of rock, a cluster of glowing crystals, a heap of bones, or a ghoul lantern.</summary>
internal enum CaveThing
{
    Stalagmite,
    Crystals,
    Bones,
    Lantern,
}

/// <summary>A cave chamber: a hall of floor, roughly an ellipse <paramref name="RadiusX"/> by <paramref name="RadiusZ"/> round <paramref name="Centre"/>, its edge ragged.</summary>
internal sealed record CaveChamber(string Name, Vector2D<float> Centre, float RadiusX, float RadiusZ);

/// <summary>A tunnel of floor from <paramref name="From"/> to <paramref name="To"/>, <paramref name="HalfWidth"/> either side of the line, its walls ragged.</summary>
internal sealed record CavePassage(Vector2D<float> From, Vector2D<float> To, float HalfWidth);

/// <summary>
/// The Delve's cave, the ghouls' lair (the user's, 2026-09-28: runs go underground, down a stairway at camp): in all of the map but camp's corner, a set of chambers
/// - big halls, medium ones and small grottos - joined by tunnels (the user's call, 2026-09-29: areas that lead into other areas, not one big square), everything else
/// rock that rises past the roof, with pillars holding the roof up in the halls. Camp's side of the rock is a long mountainside over its forest. Its floor is bare stone
/// (painted <see cref="TerrainPalette.CaveFloor"/>, so nothing grows on it and rubble gathers), and the roof (<see cref="CeilingModel"/>) is put up over it while the
/// player is underground. Runs start at the mouth of the tunnel the stairs come down (<see cref="Tunnel"/>, at the middle of the map); the boss arena is in the
/// far corner. It is lit by what glows in it: crystals, ghoul lanterns and the fires by the tunnel (see <see cref="Lights"/>). Pure: the shapes and the dressing are
/// worked out here, and <c>ArenaMasterContent.Cave.cs</c> puts them in the world.
/// </summary>
internal static class CaveLayout
{
    /// <summary>The cave floor's height (the map's base ground: hills stand on it as mounds of rock).</summary>
    public const float FloorHeight = 18f;

    /// <summary>Where the roof's model is put: its underside is a few metres either side of this (<c>tools/cave_models.py</c>), about 34 m above the floor.</summary>
    public const float CeilingHeight = FloorHeight + 34f;

    /// <summary>How high the walls and pillars are raised: past the roof (and its lumps), not much more - camp sees the ridge as a mountainside.</summary>
    public const float WallHeight = 48f;

    /// <summary>The roof: drawn as a crowd of one that glows a little of its own (<see cref="CeilingGlow"/>), so the rock overhead shows faintly and fades into the dark.</summary>
    public const string CeilingModel = "cave_ceiling.glb";

    public const float CeilingGlow = 0.14f;
    public const string StalagmiteModel = "cave_stalagmite.glb";
    public const string CrystalModel = "cave_crystals.glb";
    public const string BonesModel = "cave_bones.glb";
    public const string LanternModel = "cave_lantern.glb";
    public const string TunnelModel = "cave_tunnel.glb";

    /// <summary>The steepest ground the player can walk up (rise over run): the walls and pillars are far steeper; the mounds and camp's hills are far gentler.</summary>
    public const float PlayerMaxSlope = 1.4f;

    /// <summary>Round camp: its forest and hills, open to the sky, out to this far from camp's centre; the rock beyond rises from its valley as a mountainside.</summary>
    public const float SurfaceRadius = 88f;

    /// <summary>How far the walls' faces lean: the rock is all wall this far out from the floor's edge.</summary>
    public const float FaceWidth = 7f;

    /// <summary>
    /// The cave's chambers. The Landing (the biggest, where runs start at the tunnel's mouth) is at the middle of the map; the boss arena has a chamber of its own in
    /// the south-east corner, the Pit.
    /// </summary>
    public static readonly CaveChamber[] Chambers =
    {
        new("The Landing", new(0f, 5f), 78f, 70f),
        new("The Bone Hall", new(160f, 45f), 52f, 60f),
        new("The Gnawing Hall", new(40f, 165f), 48f, 42f),
        new("The Feasting Grotto", new(175f, 175f), 36f, 34f),
        new("The Western Warren", new(-150f, -30f), 42f, 36f),
        new("The Drip Grotto", new(-72f, 110f), 26f, 24f),
        new("The Charnel Hall", new(-150f, -165f), 50f, 45f),
        new("The Southern Warren", new(20f, -170f), 42f, 38f),
        new("The Ossuary", new(100f, -95f), 26f, 26f),
        new("The Pit", new(190f, -190f), 40f, 40f),
    };

    /// <summary>The tunnels between the chambers: some ways round, so the cave loops.</summary>
    public static readonly CavePassage[] Passages =
    {
        new(new(60f, 20f), new(115f, 40f), 8f),          // the Landing to the Bone Hall
        new(new(20f, 60f), new(35f, 128f), 7f),          // the Landing to the Gnawing Hall
        new(new(85f, 170f), new(142f, 175f), 6f),        // the Gnawing Hall to the Feasting Grotto
        new(new(165f, 100f), new(172f, 144f), 6f),       // the Bone Hall to the Feasting Grotto
        new(new(-65f, -5f), new(-112f, -25f), 7f),       // the Landing to the Western Warren
        new(new(-135f, 2f), new(-82f, 92f), 5f),         // the Western Warren to the Drip Grotto
        new(new(-58f, 95f), new(-35f, 58f), 5f),         // the Drip Grotto back to the Landing
        new(new(-150f, -62f), new(-150f, -125f), 6f),    // the Western Warren to the Charnel Hall
        new(new(-102f, -168f), new(-20f, -170f), 7f),    // the Charnel Hall to the Southern Warren
        new(new(5f, -62f), new(15f, -135f), 8f),         // the Landing to the Southern Warren
        new(new(50f, -45f), new(82f, -78f), 6f),         // the Landing to the Ossuary
        new(new(118f, -112f), new(160f, -162f), 7f),     // the Ossuary to the Pit
        new(new(140f, -10f), new(112f, -75f), 5f),       // the Bone Hall to the Ossuary
    };

    /// <summary>The pillars of rock from the floor to the roof, in the halls: (x, z, radius), each flaring out a few metres at its foot.</summary>
    public static readonly (float X, float Z, float Radius)[] Pillars =
    {
        (45f, 30f, 6f), (-42f, -32f, 7f), (-35f, 40f, 5f), (48f, -38f, 5f), (160f, 45f, 8f), (145f, 5f, 4f), (40f, 168f, 6f), (-150f, -165f, 7f), (-165f, -135f, 4f),
        (-150f, -30f, 5f), (25f, -175f, 5f), (175f, 175f, 4f),
    };

    /// <summary>
    /// The tunnel's mouth, where the stairs from camp come out and runs start: at the foot of a steep mound of rock, facing north (+Z, into the cave), the player stepping
    /// out a few metres in front of it at <see cref="CampLayout.RunStart"/>.
    /// </summary>
    public static readonly Vector2D<float> Tunnel = new(0f, -4f);

    /// <summary>The mound the tunnel comes out of: all <see cref="MoundHeight"/> high within <see cref="MoundTop"/> of its middle, falling steeply to the floor at <see cref="MoundFoot"/>.</summary>
    public static readonly Vector2D<float> Mound = new(0f, -20.5f);

    public const float MoundTop = 9f;
    public const float MoundFoot = 15f;
    public const float MoundHeight = 14f;

    /// <summary>The braziers either side of the tunnel's mouth, with fires on them: (offset from the tunnel, fire id).</summary>
    public static readonly (Vector2D<float> Offset, int FireId)[] Braziers = { (new(-4.8f, 2.2f), 7201), (new(4.8f, 2.2f), 7202) };

    public const float BrazierFireHeight = 1.14f;

    /// <summary>
    /// How far a spot is from the cave's floor (its chambers and tunnels): negative on the floor, as deep in as it is; positive in the rock. The edges are ragged.
    /// </summary>
    public static float FloorDistance(float x, float z)
    {
        float best = float.MaxValue;
        foreach (var chamber in Chambers)
        {
            float dx = (x - chamber.Centre.X) / chamber.RadiusX, dz = (z - chamber.Centre.Y) / chamber.RadiusZ;
            float e = MathF.Sqrt(dx * dx + dz * dz);
            float size = MathF.Min(chamber.RadiusX, chamber.RadiusZ);
            best = MathF.Min(best, (e - 1f - 0.07f * Noise(x * 0.04f, z * 0.04f, chamber.Centre.X)) * size);
        }

        foreach (var passage in Passages)
        {
            float d = SegmentDistance(x, z, passage.From, passage.To);
            best = MathF.Min(best, d - passage.HalfWidth * (1f + 0.25f * Noise(x * 0.09f, z * 0.09f, passage.From.Y)));
        }

        return best;
    }

    /// <summary>
    /// How much of the way a spot is to being wall, 0 (floor) to 1 (solid rock up to the roof and past it): everything off the chambers and tunnels, and the pillars.
    /// On camp's side the rock rises as a long mountainside from its forest, not a cliff.
    /// </summary>
    public static float Wall(float x, float z)
    {
        float fromCamp = Vector2D.Distance(new Vector2D<float>(x, z), CampLayout.Centre);
        float campSide = Ramp(SurfaceRadius - 36f, SurfaceRadius, fromCamp);
        float wall = Ramp(0f, FaceWidth, FloorDistance(x, z)) * campSide;

        foreach (var (px, pz, radius) in Pillars)
        {
            float d = Vector2D.Distance(new Vector2D<float>(x, z), new Vector2D<float>(px, pz));
            if (d < radius + 6f)
            {
                float r = radius * (1f + 0.15f * Noise(x * 0.2f, z * 0.2f, px));
                wall = MathF.Max(wall, Ramp(r + 5f, r, d));
            }
        }

        return wall;
    }

    /// <summary>Whether a spot is on camp's side of the ridge: the surface, open to the sky.</summary>
    public static bool OnSurface(float x, float z) => Vector2D.Distance(new Vector2D<float>(x, z), CampLayout.Centre) < SurfaceRadius;

    /// <summary>Whether a spot is the cave's floor: not the surface, not in its walls (a little of a wall's foot counts, as the ground there is still walkable).</summary>
    public static bool IsFloor(float x, float z) => !OnSurface(x, z) && Wall(x, z) < 0.25f;

    /// <summary>
    /// Whether a spot is too steep to walk: a wall, a pillar, the mound over the tunnel (worked out once on a <see cref="GridCell"/> grid, as it is asked all the time).
    /// Enemies, gems and crates keep off it.
    /// </summary>
    public static bool Blocked(float x, float z) => !Grid.Walkable(x, z);

    /// <summary>The same worked out from the shapes (what the grid is made from).</summary>
    public static bool BlockedAt(float x, float z) => OnSurface(x, z) || Wall(x, z) >= 0.25f || Near(x, z, Mound, MoundFoot - 2f);

    /// <summary>The walkable grid's cell, in metres.</summary>
    public const float GridCell = 2f;

    /// <summary>Which spots of the cave can be walked on, a cell each (<see cref="GridCell"/>): what <see cref="Blocked"/> reads, and what enemies find their way through.</summary>
    public static CaveGrid Grid => _grid ??= CaveGrid.Build(GridCell, BlockedAt);

    private static CaveGrid? _grid;

    /// <summary>The chamber a spot is in (among those whose ellipse it is inside, the one whose middle is nearest), or null in a tunnel or the rock.</summary>
    public static CaveChamber? ChamberAt(float x, float z) =>
        Chambers.Where(c => Square((x - c.Centre.X) / c.RadiusX) + Square((z - c.Centre.Y) / c.RadiusZ) <= 1f)
            .OrderBy(c => Vector2D.Distance(c.Centre, new Vector2D<float>(x, z))).FirstOrDefault();

    private static float Square(float v) => v * v;

    private static float SegmentDistance(float x, float z, Vector2D<float> a, Vector2D<float> b)
    {
        var ab = b - a;
        var p = new Vector2D<float>(x, z);
        float t = Math.Clamp(Vector2D.Dot(p - a, ab) / MathF.Max(1e-6f, ab.LengthSquared), 0f, 1f);
        return Vector2D.Distance(p, a + ab * t);
    }

    /// <summary>
    /// Raises the walls and the pillars out of the ground, and the mound the tunnel comes out of; paints everything underground the cave's stone, so nothing grows
    /// there and rubble gathers. Camp's corner is left as it is.
    /// </summary>
    public static void ShapeGround(Terrain terrain)
    {
        terrain.Reshape((x, z, height) =>
        {
            float wall = Wall(x, z);
            float raised = wall > 0f ? height + (FloorHeight + WallHeight - height) * Smooth(wall) : height;
            float hill = MoundHeight * Smooth(Ramp(MoundFoot, MoundTop, Vector2D.Distance(new Vector2D<float>(x, z), Mound)));
            return MathF.Max(raised, height + hill);
        });

        terrain.PaintShape((x, z) => !OnSurface(x, z), TerrainPalette.CaveFloor);
    }

    /// <summary>
    /// What stands about the cave floor, scattered over it on a jittered grid (the same every time): stalagmites, crystal clusters, heaps of bones and ghoul lanterns,
    /// each (thing, where, turn, size). None in the walls, round the tunnel's mouth or in the boss arena.
    /// </summary>
    public static IReadOnlyList<(CaveThing Thing, Vector2D<float> At, float Yaw, float Scale)> Things() => _things ??= Scatter();

    private static List<(CaveThing, Vector2D<float>, float, float)>? _things;

    private const float ScatterCell = 21f;

    private static List<(CaveThing, Vector2D<float>, float, float)> Scatter()
    {
        var things = new List<(CaveThing, Vector2D<float>, float, float)>();
        for (float cz = -256f; cz < 256f; cz += ScatterCell)
        {
            for (float cx = -256f; cx < 256f; cx += ScatterCell)
            {
                // Two things a cell, each somewhere in it.
                for (int k = 0; k < 2; k++)
                {
                    float x = cx + ScatterCell * Hash(cx, cz, 1 + k * 7);
                    float z = cz + ScatterCell * Hash(cx, cz, 2 + k * 7);
                    if (!IsFloor(x, z) || Wall(x, z) > 0.02f || Near(x, z, CampLayout.RunStart, 14f) || Near(x, z, Mound, MoundFoot + 2f)
                        || Near(x, z, BossArena.Centre, BossArena.Radius + 6f))
                    {
                        continue;
                    }

                    float roll = Hash(cx, cz, 3 + k * 7);
                    var thing = roll < 0.3f ? CaveThing.Stalagmite : roll < 0.62f ? CaveThing.Crystals : roll < 0.82f ? CaveThing.Bones : CaveThing.Lantern;
                    float yaw = Hash(cx, cz, 4 + k * 7) * MathF.Tau;
                    float scale = thing switch
                    {
                        CaveThing.Stalagmite => 0.6f + 1.2f * Hash(cx, cz, 5 + k * 7),
                        CaveThing.Crystals => 0.8f + 0.9f * Hash(cx, cz, 5 + k * 7),
                        _ => 0.9f + 0.3f * Hash(cx, cz, 5 + k * 7),
                    };
                    things.Add((thing, new Vector2D<float>(x, z), yaw, scale));
                }
            }
        }

        return things;
    }

    /// <summary>What lights the cave: every crystal cluster and lantern, and the fires by the tunnel and in the arena - (where, colour, reach). The nearest few are lit.</summary>
    public static IReadOnlyList<(Vector3D<float> At, Vector3D<float> Colour, float Radius)> Lights(Terrain terrain)
    {
        var lights = new List<(Vector3D<float>, Vector3D<float>, float)>();
        foreach (var (thing, at, _, scale) in Things())
        {
            if (thing == CaveThing.Crystals)
            {
                lights.Add((Ground(terrain, at) + new Vector3D<float>(0f, 1.2f * scale, 0f), CrystalLight, 8f + 2f * scale));
            }
            else if (thing == CaveThing.Lantern)
            {
                lights.Add((Ground(terrain, at) + new Vector3D<float>(0f, 1.9f, 0f), LanternLight, 11f));
            }
        }

        foreach (var (offset, _) in Braziers)
        {
            lights.Add((Ground(terrain, Tunnel + offset) + new Vector3D<float>(0f, BrazierFireHeight + 0.6f, 0f), FireLight, 16f));
        }

        foreach (var (_, offset, height, _) in BossArena.Fires())
        {
            lights.Add((Ground(terrain, BossArena.Centre + offset) + new Vector3D<float>(0f, height + 0.6f, 0f), FireLight, 16f));
        }

        return lights;
    }

    public static readonly Vector3D<float> CrystalLight = new(0.55f, 0.36f, 1.1f);
    public static readonly Vector3D<float> LanternLight = new(0.3f, 1.05f, 0.45f);
    public static readonly Vector3D<float> FireLight = new(1.25f, 0.62f, 0.25f);

    /// <summary>How many of the cave's lights are lit at once, the nearest ones (the engine lights at most 32, some of them its own: the wisps' and the shots').</summary>
    public const int LitAtOnce = 22;

    /// <summary>A light further off than this is left dark: the dark has all but hidden it by then.</summary>
    public const float LightReach = 90f;

    /// <summary>The indices of the <paramref name="count"/> lights nearest <paramref name="from"/>, within <see cref="LightReach"/>.</summary>
    public static List<int> Nearest(IReadOnlyList<(Vector3D<float> At, Vector3D<float> Colour, float Radius)> lights, Vector3D<float> from, int count)
    {
        var near = new List<(float Distance, int Index)>();
        for (int i = 0; i < lights.Count; i++)
        {
            float d = Vector3D.DistanceSquared(lights[i].At, from);
            if (d <= LightReach * LightReach)
            {
                near.Add((d, i));
            }
        }

        near.Sort((a, b) => a.Distance.CompareTo(b.Distance));
        return near.Take(count).Select(n => n.Index).ToList();
    }

    public static Vector3D<float> Ground(Terrain terrain, Vector2D<float> at) => CampLayout.Ground(terrain, at);

    private static bool Near(float x, float z, Vector2D<float> at, float within) => Vector2D.Distance(new Vector2D<float>(x, z), at) < within;

    /// <summary>0 at <paramref name="from"/>, 1 at <paramref name="to"/> (either way round), straight in between and held beyond.</summary>
    private static float Ramp(float from, float to, float value) => Math.Clamp((value - from) / (to - from), 0f, 1f);

    private static float Smooth(float t) => t * t * (3f - 2f * t);

    /// <summary>Smooth noise in -1..1 (a few sines at odd angles): enough to make an edge ragged.</summary>
    private static float Noise(float x, float z, float seed) =>
        (MathF.Sin(x * 1.7f + seed) * MathF.Cos(z * 1.3f - seed * 0.7f) + 0.6f * MathF.Sin(x * 3.1f - z * 2.3f + seed * 1.9f)) / 1.6f;

    /// <summary>A steady number in 0..1 for a cell and a salt.</summary>
    private static float Hash(float x, float z, int salt)
    {
        float v = MathF.Sin(x * 12.9898f + z * 78.233f + salt * 37.719f) * 43758.5453f;
        return v - MathF.Floor(v);
    }
}
