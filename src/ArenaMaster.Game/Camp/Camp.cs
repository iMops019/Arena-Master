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

    /// <summary>The stairs down to the Delve (where the departure gate was): the Delve chart, then a loadout, then a run.</summary>
    Gate,

    /// <summary>The bounty board: the one-time challenges, done and still to do.</summary>
    Bounties,

    /// <summary>The quartermaster's stall: permanent upgrades bought with silver.</summary>
    Quartermaster,

    /// <summary>The weapon rack: choose the class to play.</summary>
    Classes,

    /// <summary>The armour stand: the gear, and what is worn.</summary>
    Gear,

    /// <summary>The lectern with the camp ledger: the player's stats over every run.</summary>
    Ledger,
}

/// <summary>
/// Where camp is and what is in it: a clearing of packed earth in a corner of the map (painted with the engine's bed colour, which keeps trees and plants off it),
/// walled in by a palisade (<see cref="CampWalls"/>) with the stairs down to the Delve set in it (<see cref="Descent"/>; the departure gate until 2026-09-28), a fire
/// in the middle, the stations, the quartermaster behind his stall, and the camp's dressing (<see cref="Decor"/>). Low hills ring it outside the wall, so it sits in a
/// hollow of its own, and beyond them the rock of the cave's ridge (<c>World/CaveLayout</c>). Runs are played underground, in the cave: going down and coming back
/// up goes through a fade to black (<c>Ui/ScreenFade</c>), so camp reads as its own small place.
/// </summary>
internal static class CampLayout
{
    public static readonly Vector2D<float> Centre = new(-190f, 190f);

    /// <summary>The bare ground: out past the palisade's corners, so no tree grows against the wall.</summary>
    public const float ClearingRadius = 21f;

    /// <summary>How close (flat) to a station the player must stand to use it.</summary>
    public const float Reach = 3.4f;

    public const int FireId = 7001;

    /// <summary>Where runs start: the middle of the map, in the cave, in front of the tunnel the stairs come down (<c>CaveLayout.Tunnel</c>).</summary>
    public static readonly Vector2D<float> RunStart = new(0f, 0f);

    /// <summary>Where the player stands on arriving at camp, a few steps from the fire, facing it (camera yaw in degrees).</summary>
    public static readonly Vector2D<float> Spawn = Centre + new Vector2D<float>(0f, -5f);
    public const float SpawnYawDegrees = 90f;

    public static readonly (CampStation Station, string Model, Vector2D<float> Offset, string Prompt)[] Stations =
    {
        (CampStation.Stash, "camp_stash.glb", new(-6.5f, 2f), "Open the item chest"),
        (CampStation.Tree, "camp_target.glb", new(6.5f, 2f), "Passive tree"),
        (CampStation.Gate, Descent.Model, new(0f, CampWalls.Apothem), "The stairs down to the Delve: choose where to go"),
        (CampStation.Bounties, "camp_board.glb", new(8.5f, -5f), "Read the bounty board"),
        (CampStation.Quartermaster, "camp_stall.glb", new(-9.5f, -3f), "Visit the quartermaster"),
        (CampStation.Classes, "camp_rack.glb", new(4f, -10f), "Choose your class"),
        (CampStation.Gear, "camp_armorstand.glb", new(-4.5f, 8.5f), "Wear your gear"),
        (CampStation.Ledger, "camp_lectern.glb", new(4.5f, 8.5f), "Read the ledger: your records"),
    };

    /// <summary>
    /// The camp's dressing, placed on the first frame of play (<see cref="DecorProps"/>): there is more than one of most of these, which the engine's one-per-file
    /// startup props can't do. A null yaw turns the piece to face the fire. <see cref="Braziers"/> and <see cref="CookFire"/> also have a fire lit on them.
    /// </summary>
    public static readonly (string Model, Vector2D<float> Offset, float? YawDegrees, PropCollision Collision)[] Decor =
    {
        // Round the fire: two benches and the tents behind the spawn, a bedroll by each
        ("camp_bench.glb", new(-2.7f, 0.9f), null, PropCollision.Box),
        ("camp_bench.glb", new(2.7f, 0.9f), null, PropCollision.Box),
        ("camp_tent.glb", new(-5f, -8f), null, PropCollision.Box),
        ("camp_tent.glb", new(-10.5f, -8.5f), null, PropCollision.Box),
        ("camp_tent.glb", new(-1.5f, -13f), null, PropCollision.Box),
        ("camp_bedroll.glb", new(-3.4f, -5.2f), 150f, PropCollision.None),
        ("camp_bedroll.glb", new(-8.6f, -6.4f), 125f, PropCollision.None),
        ("camp_cookpot.glb", new(-6.5f, -12.2f), 20f, PropCollision.Cylinder),
        ("camp_sacks.glb", new(-4.2f, -12.6f), 0f, PropCollision.Box),

        // The quartermaster's goods, round his stall
        ("camp_barrel.glb", new(-12.2f, -0.6f), 0f, PropCollision.Cylinder),
        ("camp_barrel.glb", new(-12.6f, 0.2f), 0f, PropCollision.Cylinder),
        ("camp_crates.glb", new(-11.8f, -6.2f), 60f, PropCollision.Box),
        ("camp_sacks.glb", new(-7.6f, -4.6f), 30f, PropCollision.Box),

        // The practice yard behind the tree target: straw bales and a training dummy
        ("camp_dummy.glb", new(10.2f, 5.6f), null, PropCollision.Cylinder),
        ("camp_dummy.glb", new(12.4f, 1.8f), null, PropCollision.Cylinder),
        ("camp_haybale.glb", new(9.4f, 8.6f), 30f, PropCollision.Box),
        ("camp_haybale.glb", new(10.6f, 9.2f), 55f, PropCollision.Box),
        ("camp_haybale.glb", new(13.6f, 4.4f), 100f, PropCollision.Box),

        // The north-west corner: the supply cart, crates and barrels
        ("camp_cart.glb", new(-10f, 10f), 140f, PropCollision.Box),
        ("camp_crates.glb", new(-12.8f, 6f), 80f, PropCollision.Box),
        ("camp_barrel.glb", new(-7.2f, 12.4f), 0f, PropCollision.Cylinder),
        ("camp_barrel.glb", new(-6.4f, 12.9f), 0f, PropCollision.Cylinder),

        // The well, north-east; the woodpile, south-east; lanterns by the board and the rack
        ("camp_well.glb", new(6.5f, 11f), 20f, PropCollision.Cylinder),
        ("camp_woodpile.glb", new(10.8f, -9f), -50f, PropCollision.Box),
        ("camp_lantern.glb", new(10.6f, -2.8f), -120f, PropCollision.Cylinder),
        ("camp_lantern.glb", new(6.2f, -9.8f), -60f, PropCollision.Cylinder),
        ("camp_lantern.glb", new(-5.2f, 4.4f), 120f, PropCollision.Cylinder),

        // The stairs down: a banner either side, and braziers lighting the way at the top of them
        ("camp_banner.glb", new(-5.2f, 14.6f), 180f, PropCollision.Cylinder),
        ("camp_banner.glb", new(5.2f, 14.6f), 180f, PropCollision.Cylinder),
        ("camp_brazier.glb", new(-3.5f, 11.8f), 0f, PropCollision.Cylinder),
        ("camp_brazier.glb", new(3.5f, 11.8f), 0f, PropCollision.Cylinder),
        ("camp_brazier.glb", new(-12.4f, 3.6f), 0f, PropCollision.Cylinder),
        ("camp_brazier.glb", new(13.4f, -4.2f), 0f, PropCollision.Cylinder),
    };

    /// <summary>The fires burning on the braziers, just above their coals, and on the cooking fire: (id, offset from the centre, height above the ground, size).</summary>
    public static IEnumerable<(int Id, Vector2D<float> Offset, float Height, float Scale)> SmallFires()
    {
        int id = FireId + 1;
        foreach (var (model, offset, _, _) in Decor)
        {
            if (model == "camp_brazier.glb")
            {
                yield return (id++, offset, 1.14f, 0.55f);
            }
            else if (model == "camp_cookpot.glb")
            {
                yield return (id++, offset, 0.05f, 0.45f);
            }
        }
    }

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

    /// <summary>Camp's one-of-a-kind props, to hand the engine at startup: the stations, each turned to face the fire, and the fire pit. All are solid.</summary>
    public static IEnumerable<PropPlacement> Props(Terrain terrain)
    {
        foreach (var (_, model, offset, _) in Stations)
        {
            // The stairs are walked down: nothing of them is solid (the dug ground is the way down, the palisade either side the wall).
            yield return Place(terrain, model, offset, FacingFire(offset), model == Descent.Model ? PropCollision.None : PropCollision.Box);
        }

        yield return Place(terrain, "camp_firepit.glb", Vector2D<float>.Zero, 0f, PropCollision.None);
    }

    /// <summary>The palisade and the camp's dressing, placed once play starts: any number of copies of a model, each solid as its entry says.</summary>
    public static IEnumerable<PropPlacement> DecorProps(Terrain terrain)
    {
        foreach (var (model, offset, yaw, collision) in CampWalls.Pieces())
        {
            yield return Place(terrain, model, offset, yaw, collision);
        }

        foreach (var (model, offset, yawDegrees, collision) in Decor)
        {
            float yaw = yawDegrees is { } degrees ? degrees * MathF.PI / 180f : FacingFire(offset);
            yield return Place(terrain, model, offset, yaw, collision);
        }
    }

    /// <summary>
    /// Shapes the ground round camp: the clearing, with sandy paths (a ring round the fire and a road up to the gate) that keep plants off as well, and a ring of
    /// low wooded hills outside the palisade, so the camp sits in a hollow of its own.
    /// </summary>
    public static void ShapeGround(Terrain terrain)
    {
        for (int i = 0; i < 11; i++)
        {
            float angle = MathF.Tau * i / 11f + 0.3f;
            float distance = 41f + 4f * MathF.Sin(i * 2.3f);
            float radius = 17f + 3f * MathF.Cos(i * 1.7f);
            float height = 4.5f + 2f * MathF.Sin(i * 3.1f + 1f);
            terrain.ApplyBrush(Centre.X + MathF.Cos(angle) * distance, Centre.Y + MathF.Sin(angle) * distance, radius, height);
        }

        Descent.DigGround(terrain);
        terrain.PaintShape((x, z) => InClearing(x, z), TerrainPalette.Bed);
        terrain.PaintShape((x, z) => OnPath(x - Centre.X, z - Centre.Y), TerrainPalette.BeachSand);
    }

    /// <summary>The camp's paths, as an offset from the centre: a ring round the fire, and the road from it up to the gate.</summary>
    public static bool OnPath(float x, float z)
    {
        float fromFire = MathF.Sqrt(x * x + z * z);
        return (fromFire is > 3.9f and < 5.3f) || (MathF.Abs(x) < 1.4f && z > 4f && z < CampWalls.Apothem);
    }

    public static Vector3D<float> Ground(Terrain terrain, Vector2D<float> at) =>
        new(at.X, terrain.TryGetHeight(at.X, at.Y, out float height) ? height : 0f, at.Y);

    /// <summary>A model's yaw that turns its front (+Z) toward the fire from <paramref name="offset"/>.</summary>
    public static float FacingFire(Vector2D<float> offset) => offset.LengthSquared > 1e-4f ? MathF.Atan2(-offset.X, -offset.Y) : 0f;

    /// <summary>Where a point <paramref name="local"/> of a model turned by <paramref name="yaw"/> ends up (its +Z facing (sin yaw, cos yaw)).</summary>
    public static Vector2D<float> Turn(Vector2D<float> local, float yaw) =>
        new(local.X * MathF.Cos(yaw) + local.Y * MathF.Sin(yaw), -local.X * MathF.Sin(yaw) + local.Y * MathF.Cos(yaw));

    private static PropPlacement Place(Terrain terrain, string model, Vector2D<float> offset, float yaw, PropCollision collision) =>
        new(model, Ground(terrain, Centre + offset), yaw, 1f, Collision: collision);
}

/// <summary>
/// The stairs down to the Delve, where camp's gate was (the user's, 2026-09-28): a stair cut going <see cref="Drop"/> down between stone walls toward the north wall,
/// ending in the stone face of a tunnel's mouth that fills the palisade's gap, a mound of earth over it outside. The ground is dug to follow the stairs, so the player
/// walks down them; at the foot, by the tunnel, is where the Delve chart is opened. The sizes are the model's own (<c>DESCENT_*</c> in <c>tools/cave_models.py</c>).
/// </summary>
internal static class Descent
{
    public const string Model = "camp_descent.glb";

    /// <summary>How far down the stairs go, and how far back from the tunnel's mouth (toward the fire) they start.</summary>
    public const float Drop = 3.5f;
    public const float Run = 8f;

    /// <summary>From the middle of the stairs to the dug ground's edge, under the stone walls either side.</summary>
    public const float HalfWidth = 2f;

    /// <summary>Where the tunnel's mouth is, off the camp's centre: in the palisade's gap, on the north side.</summary>
    public static Vector2D<float> Mouth => new(0f, CampWalls.Apothem);

    /// <summary>How far below camp's ground the dug stairs are at a spot (offset from camp's centre), 0 off them: all the way down at the mouth, rising to nothing at the top.</summary>
    public static float Depth(float x, float z)
    {
        float back = Mouth.Y - z;   // how far from the mouth toward the fire
        if (MathF.Abs(x) > HalfWidth || back < -2f || back > Run + 0.6f)   // on into the tunnel, under its face, so the dark of it is clear of the ground
        {
            return 0f;
        }

        return Drop * Math.Clamp(1f - (back - 0.6f) / Run, 0f, 1f);
    }

    /// <summary>Digs the stairs into the ground, and heaps a mound of earth outside the palisade over the tunnel.</summary>
    public static void DigGround(Terrain terrain)
    {
        var mound = CampLayout.Centre + Mouth + new Vector2D<float>(0f, 7.5f);
        terrain.ApplyBrush(mound.X, mound.Y, 6.5f, 5f);
        terrain.Reshape((x, z, height) => height - Depth(x - CampLayout.Centre.X, z - CampLayout.Centre.Y));
    }
}

/// <summary>
/// The palisade round camp: an octagon of wall pieces with a thick post at each corner and the stairs down to the Delve filling the middle of its north side (the one the
/// spawn faces). Every piece faces into camp. The piece and gate sizes are the models' own (<c>WALL_SEGMENT</c> and <c>GATE_WIDTH</c> in
/// <c>tools/make_placeholder_models.py</c>).
/// </summary>
internal static class CampWalls
{
    public const int Sides = 8;
    public const int PiecesPerSide = 4;
    public const float PieceLength = 3.5f;

    /// <summary>The tunnel's mouth at the foot of the stairs down takes the place of this many pieces in the middle of its side.</summary>
    public const int GatePieces = 2;

    public const string PieceModel = "camp_wall.glb";
    public const string PostModel = "camp_wall_post.glb";

    public const float SideLength = PiecesPerSide * PieceLength;

    /// <summary>From the centre to the middle of a side: where the gate stands.</summary>
    public static readonly float Apothem = SideLength / 2f / MathF.Tan(MathF.PI / Sides);

    /// <summary>From the centre to a corner post.</summary>
    public static readonly float Circumradius = SideLength / 2f / MathF.Sin(MathF.PI / Sides);

    /// <summary>The direction (angle from +X toward +Z) of side <paramref name="side"/>'s middle; side 0 is north (+Z), where the stairs down are.</summary>
    public static float SideAngle(int side) => MathF.PI / 2f + MathF.Tau * side / Sides;

    /// <summary>Every piece of wall and every corner post, as (model, offset from the centre, yaw, collision). The stairs down are a station, placed with those.</summary>
    public static IEnumerable<(string Model, Vector2D<float> Offset, float Yaw, PropCollision Collision)> Pieces()
    {
        for (int side = 0; side < Sides; side++)
        {
            float angle = SideAngle(side);
            var outward = new Vector2D<float>(MathF.Cos(angle), MathF.Sin(angle));
            var along = new Vector2D<float>(-outward.Y, outward.X);
            float yaw = MathF.Atan2(-outward.X, -outward.Y);   // +Z of the piece toward the middle of camp
            for (int piece = 0; piece < PiecesPerSide; piece++)
            {
                if (side == 0 && IsGatePiece(piece))
                {
                    continue;
                }

                float slide = (piece - (PiecesPerSide - 1) / 2f) * PieceLength;
                yield return (PieceModel, outward * Apothem + along * slide, yaw, PropCollision.Box);
            }

            float corner = angle + MathF.PI / Sides;
            yield return (PostModel, new Vector2D<float>(MathF.Cos(corner), MathF.Sin(corner)) * Circumradius, 0f, PropCollision.Cylinder);
        }
    }

    private static bool IsGatePiece(int piece)
    {
        int first = (PiecesPerSide - GatePieces) / 2;
        return piece >= first && piece < first + GatePieces;
    }
}

/// <summary>
/// The quartermaster: a person behind the stall, there to be looked at. His model is skinned with one looping clip, which the content plays for as long as the
/// player is at camp.
/// </summary>
internal static class QuartermasterNpc
{
    public const string Model = "camp_quartermaster.glb";
    public const string IdleClip = "Idle";

    /// <summary>How long the idle clip lasts (<c>QM_IDLE_SECONDS</c> in <c>tools/make_placeholder_models.py</c>).</summary>
    public const float IdleSeconds = 6f;

    /// <summary>Where he stands in the stall's own frame: behind the counter, a little off its middle.</summary>
    private static readonly Vector2D<float> BehindCounter = new(0.15f, -0.62f);

    /// <summary>His place (offset from the camp's centre) and yaw: behind the stall's counter, facing out of it the way the stall faces.</summary>
    public static (Vector2D<float> Offset, float Yaw) Stand()
    {
        var stall = CampLayout.Stations.First(s => s.Station == CampStation.Quartermaster).Offset;
        float yaw = CampLayout.FacingFire(stall);
        return (stall + CampLayout.Turn(BehindCounter, yaw), yaw);
    }

    /// <summary>Where the idle clip is <paramref name="seconds"/> after he started it: it loops.</summary>
    public static float ClipTime(float seconds) => seconds % IdleSeconds;
}
