using ArenaMaster.Game.Camp;
using CEngine.Core;
using Silk.NET.Maths;

namespace ArenaMaster.Game.Delve;

/// <summary>
/// Where a Boss node is fought: a ring of bare earth walled in by the palisade, in a corner of the map far from both camp and the run area, sized for the player and
/// the Hollow King Unbound and nothing else. Braziers burn round its edge and banners hang by the way in. The player arrives at the south edge; the King waits
/// in the middle.
/// </summary>
internal static class BossArena
{
    public static readonly Vector2D<float> Centre = new(190f, -190f);

    /// <summary>From the centre to the wall's inner face.</summary>
    public const float Radius = 25f;

    /// <summary>The bare ground: out past the wall, so nothing grows against it.</summary>
    public const float ClearingRadius = Radius + 4f;

    public const int FireIdBase = 7101;

    /// <summary>Where the player stands on arriving, and where the King waits.</summary>
    public static readonly Vector2D<float> Start = Centre + new Vector2D<float>(0f, -Radius + 5f);

    public static Vector2D<float> BossStart => Centre;

    /// <summary>How many wall pieces go round: enough to close the ring.</summary>
    public static int PieceCount => (int)MathF.Ceiling(MathF.Tau * (Radius + 0.3f) / CampWalls.PieceLength);

    /// <summary>Every piece of the ring wall and a post at every other join, as (model, offset from the centre, yaw, collision), each piece facing in.</summary>
    public static IEnumerable<(string Model, Vector2D<float> Offset, float Yaw, PropCollision Collision)> WallPieces()
    {
        int count = PieceCount;
        float ring = Radius + 0.3f;
        for (int i = 0; i < count; i++)
        {
            float angle = MathF.Tau * i / count;
            var outward = new Vector2D<float>(MathF.Cos(angle), MathF.Sin(angle));
            yield return (CampWalls.PieceModel, outward * ring, MathF.Atan2(-outward.X, -outward.Y), PropCollision.Box);
            if (i % 2 == 0)
            {
                float join = angle + MathF.PI / count;
                yield return (CampWalls.PostModel, new Vector2D<float>(MathF.Cos(join), MathF.Sin(join)) * (ring + 0.1f), 0f, PropCollision.Cylinder);
            }
        }
    }

    /// <summary>The braziers round the edge (each has a fire lit on it) and the banners by the way in.</summary>
    public static readonly (string Model, Vector2D<float> Offset, float YawDegrees, PropCollision Collision)[] Decor =
    {
        ("camp_brazier.glb", new(0f, 22f), 0f, PropCollision.Cylinder),
        ("camp_brazier.glb", new(22f, 0f), 0f, PropCollision.Cylinder),
        ("camp_brazier.glb", new(-22f, 0f), 0f, PropCollision.Cylinder),
        ("camp_brazier.glb", new(15.5f, 15.5f), 0f, PropCollision.Cylinder),
        ("camp_brazier.glb", new(-15.5f, 15.5f), 0f, PropCollision.Cylinder),
        ("camp_banner.glb", new(-3.5f, -23.2f), 0f, PropCollision.Cylinder),
        ("camp_banner.glb", new(3.5f, -23.2f), 0f, PropCollision.Cylinder),
    };

    /// <summary>The fires on the braziers: (id, offset from the centre, height above the ground, size).</summary>
    public static IEnumerable<(int Id, Vector2D<float> Offset, float Height, float Scale)> Fires() =>
        Decor.Where(d => d.Model == "camp_brazier.glb").Select((d, i) => (FireIdBase + i, d.Offset, 1.14f, 0.6f));

    public static bool InClearing(float x, float z) => Vector2D.Distance(new Vector2D<float>(x, z), Centre) <= ClearingRadius;

    /// <summary>The arena's props, placed once play starts.</summary>
    public static IEnumerable<PropPlacement> Props(Terrain terrain)
    {
        foreach (var (model, offset, yaw, collision) in WallPieces())
        {
            yield return new PropPlacement(model, Ground(terrain, Centre + offset), yaw, 1f, Collision: collision);
        }

        foreach (var (model, offset, yawDegrees, collision) in Decor)
        {
            yield return new PropPlacement(model, Ground(terrain, Centre + offset), yawDegrees * MathF.PI / 180f, 1f, Collision: collision);
        }
    }

    /// <summary>Paints the arena floor bare (keeping trees and plants out), with a pale ring marking its middle.</summary>
    public static void ShapeGround(Terrain terrain)
    {
        terrain.PaintShape((x, z) => InClearing(x, z), TerrainPalette.Bed);
        terrain.PaintShape((x, z) =>
        {
            float d = Vector2D.Distance(new Vector2D<float>(x, z), Centre);
            return d is > 7f and < 8.2f or > 17f and < 18f;
        }, TerrainPalette.BeachSand);
    }

    public static Vector3D<float> Ground(Terrain terrain, Vector2D<float> at) => CampLayout.Ground(terrain, at);
}
