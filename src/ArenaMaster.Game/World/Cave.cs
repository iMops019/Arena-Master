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

/// <summary>
/// The Delve's cave, the ghouls' lair (the user's, 2026-09-28: runs go underground, down a stairway at camp): all of the map but camp's corner, walled in by rock that
/// rises past its roof - round the map's edge, and in a high ridge between camp's forest and the cave - with pillars of rock holding the roof up. Its floor is bare stone
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

    public const float CeilingGlow = 0.32f;
    public const string StalagmiteModel = "cave_stalagmite.glb";
    public const string CrystalModel = "cave_crystals.glb";
    public const string BonesModel = "cave_bones.glb";
    public const string LanternModel = "cave_lantern.glb";
    public const string TunnelModel = "cave_tunnel.glb";

    /// <summary>The steepest ground the player can walk up (rise over run): the walls and pillars are far steeper; the mounds and camp's hills are far gentler.</summary>
    public const float PlayerMaxSlope = 1.4f;

    /// <summary>Round camp: its forest and hills, open to the sky, out to this far from camp's centre; then the ridge, whose far side is the cave's wall.</summary>
    public const float SurfaceRadius = 88f;

    public const float RidgeRadius = 120f;

    /// <summary>How wide the rock is round the map's edge, before the noise that makes it ragged.</summary>
    public const float RimWidth = 16f;

    /// <summary>How far the walls' faces lean: the rock is all wall this far in from where it starts to rise.</summary>
    public const float FaceWidth = 7f;

    /// <summary>The pillars of rock from the floor to the roof: (x, z, radius), each flaring out a few metres at its foot.</summary>
    public static readonly (float X, float Z, float Radius)[] Pillars =
    {
        (70f, -10f, 7f), (-62f, 22f, 9f), (22f, 72f, 6f), (-110f, -60f, 8f), (112f, 82f, 7f), (152f, -40f, 10f), (-30f, -150f, 8f), (62f, -120f, 6f),
        (-150f, -10f, 7f), (140f, 172f, 9f), (205f, 40f, 8f), (-205f, -130f, 9f), (98f, -205f, 7f), (-60f, 115f, 6f), (10f, -210f, 8f), (-150f, -205f, 7f),
        (35f, 170f, 7f), (-205f, 35f, 6f), (210f, 170f, 7f),
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

    /// <summary>How much of the way a spot is to being wall, 0 (floor) to 1 (solid rock up to the roof and past it): the map's rim, camp's ridge, the pillars.</summary>
    public static float Wall(float x, float z)
    {
        float wall = 0f;

        // Camp's side of anything: a long mountainside rising from its forest, not a cliff.
        float fromCamp = Vector2D.Distance(new Vector2D<float>(x, z), CampLayout.Centre);
        float campSide = Ramp(SurfaceRadius - 36f, SurfaceRadius, fromCamp);

        // The rim: a ragged band of rock round the map's edge (only round the cave: camp's corner ends as it always did).
        float edge = 256f - MathF.Max(MathF.Abs(x), MathF.Abs(z));
        float rim = RimWidth + 6f * Noise(x * 0.03f, z * 0.03f, 1f);
        wall = MathF.Max(wall, Ramp(rim, rim - FaceWidth, edge) * campSide);

        // The ridge between camp and the cave: a wall on the cave's side.
        float outer = RidgeRadius + 8f * Noise(x * 0.025f, z * 0.025f, 2f);
        wall = MathF.Max(wall, Ramp(outer, outer - FaceWidth, fromCamp) * campSide);

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

    /// <summary>Whether a spot is too steep to walk: a wall, a pillar, the mound over the tunnel. Enemies, gems and crates keep off it.</summary>
    public static bool Blocked(float x, float z) => Wall(x, z) >= 0.25f || Near(x, z, Mound, MoundFoot - 2f);

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
