using CEngine.Core;
using Silk.NET.Maths;

namespace ArenaMaster.Game.Warrior;

/// <summary>
/// Puts <see cref="CleaveAxes"/> on screen as one engine crowd: each wave's front drawn as a curved band of short glowing pieces laid along its arc over the ground,
/// bright while it rolls out and fading once it has. However far it reaches and however wide it sweeps, the pieces stay about the same length.
/// </summary>
internal sealed class WarriorView
{
    public const string WaveModel = "cleave_wave.glb";

    /// <summary>The band is cut into pieces about this long.</summary>
    private const float PieceLength = 0.7f;

    /// <summary>At most this many pieces for one wave.</summary>
    private const int MaxPieces = 240;

    private readonly List<CrowdInstance> _pieces = new();

    public void Sync(EngineWindow window, CleaveAxes axes, Func<float, float, float?> groundAt)
    {
        _pieces.Clear();
        foreach (var wave in axes.Waves)
        {
            if (wave.Delay > 0f)
            {
                continue;
            }

            float front = wave.Front;
            float arc = 2f * wave.HalfArc * front;
            int count = Math.Clamp((int)MathF.Ceiling(arc / PieceLength), 3, MaxPieces);
            float length = arc / count;
            float t = wave.Age / WarriorStats.TravelTime;
            float flash = t <= 1f ? 1f - 0.4f * t : 0.6f * MathF.Max(0f, 1f - (wave.Age - WarriorStats.TravelTime) / CleaveWave.FadeTime);
            for (int i = 0; i < count; i++)
            {
                float yaw = wave.Yaw - wave.HalfArc + (i + 0.5f) / count * 2f * wave.HalfArc;
                float x = wave.Origin.X + MathF.Sin(yaw) * front;
                float z = wave.Origin.Z + MathF.Cos(yaw) * front;
                float y = (groundAt(x, z) ?? wave.Origin.Y) + 0.15f;
                _pieces.Add(new CrowdInstance(new Vector3D<float>(x, y, z), yaw, length * 1.08f, Flash: flash));
            }
        }

        window.SetCrowd(WaveModel, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_pieces));
    }

    public void Clear(EngineWindow window) => window.SetCrowd(WaveModel, ReadOnlySpan<CrowdInstance>.Empty);
}
