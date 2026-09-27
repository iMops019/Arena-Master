using ArenaMaster.Game.Combat;
using CEngine.Core;
using Silk.NET.Maths;

namespace ArenaMaster.Game.Shaman;

/// <summary>
/// Puts <see cref="RollingLightning"/> on screen as engine crowds. Each ball is a round, pulsing core of plasma inside a shell of static that is turned to a new
/// angle every frame (so it crackles), with thin sparks snapping off its surface - and down to the ground as it comes near it. Each bounce discharges into the
/// ground: bolts running out along it from where it landed, under the zap's spreading ring. Each fork is a jagged run of short bright segments that jumps about
/// every frame, and sparks crackle at the base of each charged tree.
/// </summary>
internal sealed class StormView
{
    public const string BallModel = "lightning_ball.glb";
    public const string StaticModel = "lightning_static.glb";
    public const string SegmentModel = "lightning_arc.glb";
    public const string SparkModel = "lightning_spark.glb";
    public const string ZapModel = "lightning_zap.glb";

    /// <summary>A fork is cut into pieces about this long, each nudged sideways by up to this much; a spark into shorter, finer ones.</summary>
    private const float SegmentLength = 0.8f;
    private const float Jitter = 0.35f;
    private const float SparkPiece = 0.25f;
    private const float SparkJitter = 0.12f;

    /// <summary>How long after a bounce its discharge runs along the ground, and in how many bolts.</summary>
    private const float DischargeSeconds = 0.16f;
    private const int DischargeBolts = 7;

    /// <summary>A ball this near the ground (below its middle) sparks down to it.</summary>
    private const float GroundSparkHeight = 1.4f;

    private readonly Random _jitter = new(11);
    private readonly List<CrowdInstance> _balls = new();
    private readonly List<CrowdInstance> _static = new();
    private readonly List<CrowdInstance> _segments = new();
    private readonly List<CrowdInstance> _sparks = new();
    private readonly List<CrowdInstance> _zaps = new();
    private float _time;

    public void Sync(EngineWindow window, RollingLightning storm, float deltaSeconds, Func<float, float, float?>? groundAt = null)
    {
        _time += deltaSeconds;
        _balls.Clear();
        _static.Clear();
        _segments.Clear();
        _sparks.Clear();

        foreach (var ball in storm.Balls)
        {
            float size = ball.Radius / ShamanStats.BaseBallRadius;
            float pulse = 1f + 0.07f * MathF.Sin(_time * 31f + ball.Age * 5f) + 0.04f * MathF.Sin(_time * 53f);
            float flicker = 0.6f + 0.4f * MathF.Abs(MathF.Sin(_time * 41f + ball.Age * 13f));
            _balls.Add(new CrowdInstance(ball.Position, _time * 6f + ball.Age * 3f, size * pulse, ball.Age * 9f, Flash: flicker));

            // The static shell, twice over at new angles each frame, swelling and shrinking: it never sits still.
            for (int i = 0; i < 2; i++)
            {
                _static.Add(new CrowdInstance(ball.Position, Unit() * MathF.Tau, size * (0.9f + 0.35f * Unit()), Unit() * MathF.Tau, Flash: 0.7f + 0.3f * Unit()));
            }

            // Sparks snapping off its surface.
            int sparks = 2 + _jitter.Next(3);
            for (int i = 0; i < sparks; i++)
            {
                var way = Vector3D.Normalize(new Vector3D<float>(Wobble(), Wobble(), Wobble()) + new Vector3D<float>(0f, 1e-3f, 0f));
                var from = ball.Position + way * ball.Radius * 0.9f;
                AddBolt(_sparks, from, from + way * ball.Radius * (1.2f + 2f * Unit()), SparkPiece, SparkJitter);
            }

            // Near the ground, it arcs down to it.
            if (groundAt?.Invoke(ball.Position.X, ball.Position.Z) is { } ground && ball.Position.Y - ground < GroundSparkHeight && Unit() < 0.7f)
            {
                var below = new Vector3D<float>(ball.Position.X + Wobble() * 0.6f, ground + 0.03f, ball.Position.Z + Wobble() * 0.6f);
                AddBolt(_sparks, ball.Position - new Vector3D<float>(0f, ball.Radius * 0.8f, 0f), below, SparkPiece, SparkJitter);
            }
        }

        foreach (var rod in storm.Rods)
        {
            for (int i = 0; i < 3; i++)
            {
                var at = rod.Position + new Vector3D<float>(Wobble() * 0.5f, 0.3f + 0.4f * i + Wobble() * 0.2f, Wobble() * 0.5f);
                _static.Add(new CrowdInstance(at, Unit() * MathF.Tau, 0.4f + 0.3f * Unit(), Unit() * MathF.Tau, Flash: 0.8f));
            }

            AddBolt(_sparks, rod.Position + new Vector3D<float>(0f, 0.1f, 0f), rod.Position + new Vector3D<float>(Wobble() * 0.4f, 1.2f + Unit(), Wobble() * 0.4f), SparkPiece, SparkJitter);
        }

        foreach (var arc in storm.Arcs)
        {
            AddBolt(_segments, arc.From, arc.To, SegmentLength, Jitter);
        }

        _zaps.Clear();
        foreach (var zap in storm.Zaps)
        {
            float t = Math.Clamp(zap.Age / RollingLightning.ZapSeconds, 0f, 1f);
            _zaps.Add(new CrowdInstance(zap.Centre + new Vector3D<float>(0f, 0.12f, 0f), _time * 5f, zap.Radius * (0.35f + 0.65f * t), Flash: 1f - t));
            if (zap.Age < DischargeSeconds)
            {
                // The bounce discharging into the ground: bolts running out along it, as far as the ring has spread.
                float reach = zap.Radius * (0.45f + 0.55f * zap.Age / DischargeSeconds);
                var centre = zap.Centre + new Vector3D<float>(0f, 0.08f, 0f);
                for (int i = 0; i < DischargeBolts; i++)
                {
                    float angle = MathF.Tau * (i + Unit() * 0.6f) / DischargeBolts;
                    float r = reach * (0.6f + 0.4f * Unit());
                    var end = centre + new Vector3D<float>(MathF.Sin(angle) * r, 0.02f, MathF.Cos(angle) * r);
                    if (groundAt?.Invoke(end.X, end.Z) is { } ground)
                    {
                        end.Y = ground + 0.08f;
                    }

                    AddBolt(i % 2 == 0 ? _segments : _sparks, centre, end, SparkPiece * 1.6f, SparkJitter * 1.5f, flat: true);
                }
            }
        }

        window.SetCrowd(BallModel, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_balls));
        window.SetCrowd(StaticModel, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_static));
        window.SetCrowd(SegmentModel, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_segments));
        window.SetCrowd(SparkModel, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_sparks));
        window.SetCrowd(ZapModel, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_zaps));
    }

    public void Clear(EngineWindow window)
    {
        foreach (var model in new[] { BallModel, StaticModel, SegmentModel, SparkModel, ZapModel })
        {
            window.SetCrowd(model, ReadOnlySpan<CrowdInstance>.Empty);
        }
    }

    /// <summary>
    /// A jagged bolt from <paramref name="from"/> to <paramref name="to"/> into <paramref name="into"/>: the straight line cut into pieces about
    /// <paramref name="piece"/> long, the inner joints nudged by up to <paramref name="jitter"/> (only sideways along the ground if <paramref name="flat"/>).
    /// </summary>
    private void AddBolt(List<CrowdInstance> into, Vector3D<float> from, Vector3D<float> to, float piece, float jitter, bool flat = false)
    {
        float length = Vector3D.Distance(from, to);
        int pieces = Math.Max(2, (int)MathF.Ceiling(length / piece));
        var previous = from;
        for (int i = 1; i <= pieces; i++)
        {
            var next = from + (to - from) * (i / (float)pieces);
            if (i < pieces)
            {
                next += new Vector3D<float>(Wobble(), flat ? 0f : Wobble(), Wobble()) * jitter;
            }

            var step = next - previous;
            float stepLength = step.Length;
            if (stepLength > 1e-3f)
            {
                var (yaw, pitch) = Geometry.YawPitch(step);
                into.Add(new CrowdInstance((previous + next) * 0.5f, yaw, stepLength, pitch, Flash: 1f));
            }

            previous = next;
        }
    }

    private float Wobble() => (float)_jitter.NextDouble() * 2f - 1f;

    private float Unit() => (float)_jitter.NextDouble();
}
