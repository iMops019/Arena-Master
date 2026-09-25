using CEngine.Core;
using Silk.NET.Maths;

namespace ArenaMaster.Game.Camp;

/// <summary>The things at camp the player can walk up to and use.</summary>
internal enum CampStation
{
    /// <summary>The stash chest: see every item owned.</summary>
    Stash,

    /// <summary>The archery target: the passive tree.</summary>
    Tree,

    /// <summary>The departure gate: choose a loadout and start a run.</summary>
    Gate,
}

/// <summary>
/// Where camp is and what is in it: a clearing of packed earth in a corner of the map (painted with the engine's bed colour, which keeps trees and plants off it),
/// a fire in the middle, a tent, and the three stations. Runs are played around the middle of the map, well away from it.
/// </summary>
internal static class CampLayout
{
    public static readonly Vector2D<float> Centre = new(-190f, 190f);

    public const float ClearingRadius = 18f;

    /// <summary>How close (flat) to a station the player must stand to use it.</summary>
    public const float Reach = 3.4f;

    public const int FireId = 7001;

    /// <summary>Where runs start: the middle of the map.</summary>
    public static readonly Vector2D<float> RunStart = new(0f, 0f);

    /// <summary>Where the player stands on arriving at camp, a few steps from the fire, facing it (camera yaw in degrees).</summary>
    public static readonly Vector2D<float> Spawn = Centre + new Vector2D<float>(0f, -5f);
    public const float SpawnYawDegrees = 90f;

    public static readonly (CampStation Station, string Model, Vector2D<float> Offset, string Prompt)[] Stations =
    {
        (CampStation.Stash, "camp_stash.glb", new(-6.5f, 2f), "Open the item chest"),
        (CampStation.Tree, "camp_target.glb", new(6.5f, 2f), "Passive tree"),
        (CampStation.Gate, "camp_gate.glb", new(0f, 11f), "Choose a loadout and set out"),
    };

    /// <summary>Scenery with no use: the tent and the fire pit.</summary>
    public static readonly (string Model, Vector2D<float> Offset)[] Scenery =
    {
        ("camp_tent.glb", new(-5f, -8f)),
        ("camp_firepit.glb", new(0f, 0f)),
    };

    public static bool InClearing(float x, float z) => Vector2D.Distance(new Vector2D<float>(x, z), Centre) <= ClearingRadius;

    public static bool InCamp(Vector3D<float> feet) => Vector2D.Distance(new Vector2D<float>(feet.X, feet.Z), Centre) <= ClearingRadius + 8f;

    /// <summary>The station within reach of <paramref name="feet"/>, the nearest if more than one, or null.</summary>
    public static (CampStation Station, string Prompt)? StationNear(Vector3D<float> feet)
    {
        (CampStation, string)? best = null;
        float bestDistance = Reach;
        foreach (var (station, _, offset, prompt) in Stations)
        {
            var at = Centre + offset;
            float distance = Vector2D.Distance(new Vector2D<float>(feet.X, feet.Z), at);
            if (distance <= bestDistance)
            {
                best = (station, prompt);
                bestDistance = distance;
            }
        }

        return best;
    }

    /// <summary>Camp's props, to hand the engine at startup: each turned to face the fire. The stash and target are solid; the gate is walked through.</summary>
    public static IEnumerable<PropPlacement> Props(Terrain terrain)
    {
        foreach (var (station, model, offset, _) in Stations)
        {
            yield return Place(terrain, model, offset, station == CampStation.Gate ? PropCollision.None : PropCollision.Box);
        }

        foreach (var (model, offset) in Scenery)
        {
            yield return Place(terrain, model, offset, model == "camp_tent.glb" ? PropCollision.Box : PropCollision.None);
        }
    }

    /// <summary>Paints the clearing (keeping trees and plants out of it).</summary>
    public static void PaintClearing(Terrain terrain) => terrain.PaintShape((x, z) => InClearing(x, z), TerrainPalette.Bed);

    public static Vector3D<float> Ground(Terrain terrain, Vector2D<float> at) =>
        new(at.X, terrain.TryGetHeight(at.X, at.Y, out float height) ? height : 0f, at.Y);

    private static PropPlacement Place(Terrain terrain, string model, Vector2D<float> offset, PropCollision collision)
    {
        var at = Centre + offset;
        float yaw = offset.LengthSquared > 1e-4f ? MathF.Atan2(-offset.X, -offset.Y) : 0f;   // +Z of the model toward the fire
        return new PropPlacement(model, Ground(terrain, at), yaw, 1f, Collision: collision);
    }
}
