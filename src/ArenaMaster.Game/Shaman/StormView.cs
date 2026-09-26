using ArenaMaster.Game.Combat;
using CEngine.Core;
using Silk.NET.Maths;

namespace ArenaMaster.Game.Shaman;

/// <summary>
/// Puts <see cref="RollingLightning"/> on screen as engine crowds: the balls (flickering, the supercell a big one), each arc as a jagged run of short bright
/// segments that jumps about every frame, the zaps' rings spreading over the ground, and sparks crackling at the base of each charged tree.
/// </summary>
internal sealed class StormView
{
    public const string BallModel = "lightning_ball.glb";
    public const string SegmentModel = "lightning_arc.glb";
    public const string ZapModel = "lightning_zap.glb";

    /// <summary>An arc is cut into pieces about this long, each nudged sideways by up to this much.</summary>
    private const float SegmentLength = 0.8f;
    private const float Jitter = 0.35f;

    private readonly Random _jitter = new(11);
    private readonly List<CrowdInstance> _balls = new();
    private readonly List<CrowdInstance> _segments = new();
    private readonly List<CrowdInstance> _zaps = new();
    private float _time;

    public void Sync(EngineWindow window, RollingLightning storm, float deltaSeconds)
    {
        _time += deltaSeconds;

        _balls.Clear();
        foreach (var ball in storm.Balls)
        {
            float flicker = 0.55f + 0.45f * MathF.Abs(MathF.Sin(_time * 37f + ball.Age * 11f));
            _balls.Add(new CrowdInstance(ball.Position, _time * 9f + ball.Age * 3f, ball.Radius / ShamanStats.BaseBallRadius, ball.Age * 7f, Flash: flicker));
        }

        foreach (var rod in storm.Rods)
        {
            for (int i = 0; i < 3; i++)
            {
                var at = rod.Position + new Vector3D<float>(Wobble() * 0.5f, 0.3f + 0.4f * i + Wobble() * 0.2f, Wobble() * 0.5f);
                _balls.Add(new CrowdInstance(at, _time * 20f + i, 0.35f, 0f, Flash: 0.8f));
            }
        }

        _segments.Clear();
        foreach (var arc in storm.Arcs)
        {
            AddArc(arc.From, arc.To);
        }

        _zaps.Clear();
        foreach (var zap in storm.Zaps)
        {
            float t = Math.Clamp(zap.Age / RollingLightning.ZapSeconds, 0f, 1f);
            _zaps.Add(new CrowdInstance(zap.Centre + new Vector3D<float>(0f, 0.12f, 0f), _time * 5f, zap.Radius * (0.35f + 0.65f * t), Flash: 1f - t));
        }

        window.SetCrowd(BallModel, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_balls));
        window.SetCrowd(SegmentModel, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_segments));
        window.SetCrowd(ZapModel, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_zaps));
    }

    public void Clear(EngineWindow window)
    {
        window.SetCrowd(BallModel, ReadOnlySpan<CrowdInstance>.Empty);
        window.SetCrowd(SegmentModel, ReadOnlySpan<CrowdInstance>.Empty);
        window.SetCrowd(ZapModel, ReadOnlySpan<CrowdInstance>.Empty);
    }

    /// <summary>A jagged bolt from <paramref name="from"/> to <paramref name="to"/>: the straight line cut into pieces, the inner joints nudged sideways at random.</summary>
    private void AddArc(Vector3D<float> from, Vector3D<float> to)
    {
        float length = Vector3D.Distance(from, to);
        int pieces = Math.Max(2, (int)MathF.Ceiling(length / SegmentLength));
        var previous = from;
        for (int i = 1; i <= pieces; i++)
        {
            var next = from + (to - from) * (i / (float)pieces);
            if (i < pieces)
            {
                next += new Vector3D<float>(Wobble(), Wobble(), Wobble()) * Jitter;
            }

            var piece = next - previous;
            float pieceLength = piece.Length;
            if (pieceLength > 1e-3f)
            {
                var (yaw, pitch) = Geometry.YawPitch(piece);
                _segments.Add(new CrowdInstance((previous + next) * 0.5f, yaw, pieceLength, pitch, Flash: 1f));
            }

            previous = next;
        }
    }

    private float Wobble() => (float)_jitter.NextDouble() * 2f - 1f;
}
