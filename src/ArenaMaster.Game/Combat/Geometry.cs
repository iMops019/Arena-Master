using Silk.NET.Maths;

namespace ArenaMaster.Game.Combat;

internal static class Geometry
{
    /// <summary>
    /// The closest distance between segment p1-q1 and segment p2-q2, and where along the first segment (0 at p1, 1 at q1) the closest point lies.
    /// The standard clamped closest-points method (Ericson, Real-Time Collision Detection, 5.1.9).
    /// </summary>
    public static float SegmentDistance(Vector3D<float> p1, Vector3D<float> q1, Vector3D<float> p2, Vector3D<float> q2, out float s)
    {
        const float Epsilon = 1e-8f;
        var d1 = q1 - p1;
        var d2 = q2 - p2;
        var r = p1 - p2;
        float a = Vector3D.Dot(d1, d1);
        float e = Vector3D.Dot(d2, d2);
        float f = Vector3D.Dot(d2, r);
        float t;

        if (a <= Epsilon && e <= Epsilon)
        {
            s = 0f;
            return (p1 - p2).Length;
        }

        if (a <= Epsilon)
        {
            s = 0f;
            t = Math.Clamp(f / e, 0f, 1f);
        }
        else
        {
            float c = Vector3D.Dot(d1, r);
            if (e <= Epsilon)
            {
                t = 0f;
                s = Math.Clamp(-c / a, 0f, 1f);
            }
            else
            {
                float b = Vector3D.Dot(d1, d2);
                float denominator = a * e - b * b;
                s = denominator > Epsilon ? Math.Clamp((b * f - c * e) / denominator, 0f, 1f) : 0f;
                t = (b * s + f) / e;

                if (t < 0f)
                {
                    t = 0f;
                    s = Math.Clamp(-c / a, 0f, 1f);
                }
                else if (t > 1f)
                {
                    t = 1f;
                    s = Math.Clamp((b - c) / a, 0f, 1f);
                }
            }
        }

        var closest1 = p1 + d1 * s;
        var closest2 = p2 + d2 * t;
        return (closest1 - closest2).Length;
    }

    /// <summary>Flat (XZ) direction from <paramref name="from"/> to <paramref name="to"/>, and the flat distance; zero direction if they are on top of each other.</summary>
    public static Vector3D<float> FlatDirection(Vector3D<float> from, Vector3D<float> to, out float distance)
    {
        var flat = new Vector3D<float>(to.X - from.X, 0f, to.Z - from.Z);
        distance = flat.Length;
        return distance > 1e-5f ? flat / distance : Vector3D<float>.Zero;
    }

    /// <summary>The yaw (models face +Z) and nose-down pitch that point a model along <paramref name="direction"/>.</summary>
    public static (float Yaw, float Pitch) YawPitch(Vector3D<float> direction)
    {
        float length = direction.Length;
        if (length < 1e-6f)
        {
            return (0f, 0f);
        }

        var d = direction / length;
        return (MathF.Atan2(d.X, d.Z), -MathF.Asin(Math.Clamp(d.Y, -1f, 1f)));
    }
}
