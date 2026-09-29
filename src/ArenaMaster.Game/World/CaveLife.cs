using CEngine.Core;
using Silk.NET.Maths;

namespace ArenaMaster.Game.World;

/// <summary>
/// What lives in the cave besides the ghouls (the user's, 2026-09-29): pools of brackish water with drops falling into them from the dark above and ripples
/// spreading, clusters of glowing fungus at the foot of the walls, bats circling high in the halls, and wisps drifting through every chamber. Where each is, and
/// where the moving ones are at a moment - pure; <c>ArenaMasterContent.Cave.cs</c> draws them as crowds.
/// </summary>
internal static class CaveLife
{
    public const string PoolModel = "cave_pool.glb";
    public const string RippleModel = "cave_ripple.glb";
    public const string DropModel = "cave_drop.glb";
    public const string FungusModel = "cave_fungus.glb";
    public const string BatUpModel = "cave_bat_up.glb";
    public const string BatDownModel = "cave_bat_down.glb";
    public const string WispModel = "cave_wisp.glb";
    public const string VioletWispModel = "cave_wisp_violet.glb";

    /// <summary>The pools of brackish water: (where, radius). Each lies in a shallow dip in a chamber's floor.</summary>
    public static readonly (Vector2D<float> At, float Radius)[] Pools =
    {
        (new(-72f, 110f), 8f), (new(-165f, -45f), 6f), (new(-15f, 55f), 6f), (new(0f, -160f), 7f), (new(-125f, -185f), 7f), (new(65f, 150f), 6f),
        (new(175f, 80f), 7f), (new(160f, 185f), 5f),
    };

    /// <summary>How deep a pool's dip is at its middle, and how far below the floor its water lies.</summary>
    public const float PoolDepth = 0.9f;
    public const float WaterBelowFloor = 0.3f;

    /// <summary>How far down a spot is dug for a pool (0 off them): a smooth dip, deepest in the middle, shelving out a little past the water's edge.</summary>
    public static float PoolDip(float x, float z)
    {
        float dip = 0f;
        foreach (var (at, radius) in Pools)
        {
            float d = Vector2D.Distance(new Vector2D<float>(x, z), at);
            float t = Math.Clamp(1f - d / (radius + 1.5f), 0f, 1f);
            dip = MathF.Max(dip, PoolDepth * t * t * (3f - 2f * t));
        }

        return dip;
    }

    public static bool NearPool(float x, float z, float margin) => Pools.Any(p => Vector2D.Distance(new Vector2D<float>(x, z), p.At) < p.Radius + margin);

    /// <summary>The height of the pools' water.</summary>
    public const float WaterHeight = CaveLayout.FloorHeight - WaterBelowFloor;

    /// <summary>How high above the floor the drops start falling (from the dark under the roof), and how hard they fall.</summary>
    public const float DropHeight = 22f;
    public const float Gravity = 9.8f;

    /// <summary>Seconds a drop takes to fall to the water.</summary>
    public static float FallSeconds => MathF.Sqrt(2f * DropHeight / Gravity);

    /// <summary>How long a ripple spreads, and how far.</summary>
    public const float RippleSeconds = 1.2f;
    public const float RippleReach = 1.8f;

    /// <summary>Where the drops fall into the pools: (where, seconds between drops, when in that time it falls). Two or three a pool, each its own rhythm.</summary>
    public static IReadOnlyList<(Vector2D<float> At, float Period, float Offset)> Drips() => _drips ??= MakeDrips();

    private static List<(Vector2D<float>, float, float)>? _drips;

    private static List<(Vector2D<float>, float, float)> MakeDrips()
    {
        var drips = new List<(Vector2D<float>, float, float)>();
        for (int p = 0; p < Pools.Length; p++)
        {
            var (at, radius) = Pools[p];
            int count = 2 + p % 2;
            for (int k = 0; k < count; k++)
            {
                float angle = Hash(p, k, 1) * MathF.Tau;
                float out_ = radius * 0.55f * Hash(p, k, 2);
                float period = FallSeconds + RippleSeconds + 0.4f + 2f * Hash(p, k, 3);
                drips.Add((at + new Vector2D<float>(MathF.Cos(angle), MathF.Sin(angle)) * out_, period, period * Hash(p, k, 4)));
            }
        }

        return drips;
    }

    /// <summary>The drops falling and the ripples spreading at <paramref name="time"/> seconds, into the two lists (cleared first).</summary>
    public static void Drops(float time, List<CrowdInstance> drops, List<CrowdInstance> ripples)
    {
        drops.Clear();
        ripples.Clear();
        float fall = FallSeconds;
        foreach (var (at, period, offset) in Drips())
        {
            float t = ((time + offset) % period + period) % period;
            if (t < fall)
            {
                float y = WaterHeight + DropHeight - 0.5f * Gravity * t * t;
                drops.Add(new CrowdInstance(new Vector3D<float>(at.X, y, at.Y), 0f, 1f + 0.8f * (t / fall)));   // stretched the faster it falls
            }
            else if (t < fall + RippleSeconds)
            {
                float age = (t - fall) / RippleSeconds;
                var surface = new Vector3D<float>(at.X, WaterHeight + 0.03f, at.Y);
                ripples.Add(new CrowdInstance(surface, 0f, 0.08f + RippleReach * age, Flash: 1f - age));
                if (age > 0.25f)
                {
                    ripples.Add(new CrowdInstance(surface, 0f, 0.08f + RippleReach * (age - 0.25f) * 0.8f, Flash: 1f - age));   // a second ring after it
                }
            }
        }
    }

    /// <summary>The fungus: clusters at the foot of the walls, the same every time - (where, turn, size).</summary>
    public static IReadOnlyList<(Vector2D<float> At, float Yaw, float Scale)> Fungus() => _fungus ??= ScatterFungus();

    private static List<(Vector2D<float>, float, float)>? _fungus;

    private const float FungusCell = 7f;

    private static List<(Vector2D<float>, float, float)> ScatterFungus()
    {
        var fungus = new List<(Vector2D<float>, float, float)>();
        for (float cz = -256f; cz < 256f; cz += FungusCell)
        {
            for (float cx = -256f; cx < 256f; cx += FungusCell)
            {
                float x = cx + FungusCell * Hash(cx, cz, 5), z = cz + FungusCell * Hash(cx, cz, 6);
                if (CaveLayout.OnSurface(x, z) || Hash(cx, cz, 7) > 0.55f)
                {
                    continue;
                }

                // Only where the floor meets the rock: just short of the wall's foot.
                float fromFloor = CaveLayout.FloorDistance(x, z);
                if (fromFloor is < -2.5f or > 0.2f || CaveLayout.Wall(x, z) > 0.03f || NearPool(x, z, 1f))
                {
                    continue;
                }

                fungus.Add((new Vector2D<float>(x, z), Hash(cx, cz, 8) * MathF.Tau, 0.7f + 0.9f * Hash(cx, cz, 9)));
            }
        }

        return fungus;
    }

    /// <summary>How many bats circle in a chamber of its size (none in the small grottos).</summary>
    public static int BatsIn(CaveChamber chamber) => MathF.Min(chamber.RadiusX, chamber.RadiusZ) >= 36f ? 4 + (int)(MathF.Min(chamber.RadiusX, chamber.RadiusZ) / 12f) : 0;

    /// <summary>The bats at <paramref name="time"/>, each circling high in its chamber, wings up or down as it flaps, into the two lists (cleared first).</summary>
    public static void Bats(float time, List<CrowdInstance> wingsUp, List<CrowdInstance> wingsDown)
    {
        wingsUp.Clear();
        wingsDown.Clear();
        for (int c = 0; c < CaveLayout.Chambers.Length; c++)
        {
            var chamber = CaveLayout.Chambers[c];
            int count = BatsIn(chamber);
            for (int k = 0; k < count; k++)
            {
                float radius = MathF.Min(chamber.RadiusX, chamber.RadiusZ) * (0.25f + 0.35f * Hash(c, k, 11));
                float speed = (0.35f + 0.3f * Hash(c, k, 12)) * (k % 2 == 0 ? 1f : -1f);
                float angle = Hash(c, k, 13) * MathF.Tau + time * speed;
                float wobble = 3f * MathF.Sin(time * 0.7f + k * 1.9f);
                float height = CaveLayout.FloorHeight + 8f + 8f * Hash(c, k, 14) + 1.5f * MathF.Sin(time * 1.3f + k);
                var at = new Vector3D<float>(chamber.Centre.X + MathF.Cos(angle) * (radius + wobble), height, chamber.Centre.Y + MathF.Sin(angle) * (radius + wobble));
                float yaw = MathF.Atan2(-MathF.Sin(angle) * MathF.Sign(speed), MathF.Cos(angle) * MathF.Sign(speed));   // facing the way it flies round
                var bat = new CrowdInstance(at, yaw, 1.6f, 0.15f * MathF.Sin(time * 2f + k));
                (MathF.Sin(time * 14f + k * 2.3f) > 0f ? wingsUp : wingsDown).Add(bat);
            }
        }
    }

    /// <summary>How many wisps drift in a chamber: one for about every 1,400 square metres of it, at least one.</summary>
    public static int WispsIn(CaveChamber chamber) => Math.Max(1, (int)MathF.Round(MathF.PI * chamber.RadiusX * chamber.RadiusZ / 1400f));

    /// <summary>
    /// The wisps at <paramref name="time"/>: each drifting in slow loops round a spot of its own in its chamber, a few metres above the ground (from
    /// <paramref name="groundAt"/>), bobbing; green ones and violet ones, into the two lists (cleared first).
    /// </summary>
    public static void Wisps(float time, Func<float, float, float> groundAt, List<CrowdInstance> green, List<CrowdInstance> violet)
    {
        green.Clear();
        violet.Clear();
        for (int c = 0; c < CaveLayout.Chambers.Length; c++)
        {
            var chamber = CaveLayout.Chambers[c];
            int count = WispsIn(chamber);
            for (int k = 0; k < count; k++)
            {
                float a = Hash(c, k, 21) * MathF.Tau, r = 0.6f * MathF.Sqrt(Hash(c, k, 22));
                float homeX = chamber.Centre.X + MathF.Cos(a) * r * chamber.RadiusX, homeZ = chamber.Centre.Y + MathF.Sin(a) * r * chamber.RadiusZ;
                float loop = 4f + 6f * Hash(c, k, 23), rate = 0.12f + 0.12f * Hash(c, k, 24), phase = Hash(c, k, 25) * MathF.Tau;
                float x = homeX + loop * MathF.Sin(time * rate + phase), z = homeZ + loop * 0.7f * MathF.Sin(time * rate * 1.7f + phase * 0.5f);
                float y = groundAt(x, z) + 2.5f + 3f * Hash(c, k, 26) + 0.5f * MathF.Sin(time * 0.9f + phase);
                float twinkle = 0.3f + 0.3f * MathF.Sin(time * 2.3f + phase * 3f);
                (k % 3 == 2 ? violet : green).Add(new CrowdInstance(new Vector3D<float>(x, y, z), time * 0.5f + phase, 1.5f, Flash: twinkle));
            }
        }
    }

    private static float Hash(float x, float z, int salt)
    {
        float v = MathF.Sin(x * 12.9898f + z * 78.233f + salt * 37.719f) * 43758.5453f;
        return v - MathF.Floor(v);
    }
}
