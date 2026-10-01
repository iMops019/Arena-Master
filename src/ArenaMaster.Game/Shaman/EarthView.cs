using ArenaMaster.Game.Combat;
using CEngine.Core;
using Silk.NET.Maths;

namespace ArenaMaster.Game.Shaman;

/// <summary>
/// Puts <see cref="RollingStone"/> on screen as engine crowds. Each stone is a rough boulder turning over as it goes. Each quake is a ring of churned earth
/// spreading from where it struck; each Aftershock crack a jagged split in the ground, shaking harder as it comes to burst, and its burst rock spikes thrusting up
/// and sinking back. A shattered stone flings chunks of rubble out round it. Each totem rises out of the ground where the surge began, a ring on the ground
/// showing how far it draws enemies from, and sinks when its time is up. A rift is a run of split ground from one landing to the next.
/// </summary>
internal sealed class EarthView
{
    public const string StoneModel = "earth_stone.glb";
    public const string QuakeModel = "earth_quake.glb";
    public const string CrackModel = "earth_crack.glb";
    public const string SpikesModel = "earth_spikes.glb";
    public const string ChunkModel = "earth_chunk.glb";
    public const string TotemModel = "earth_totem.glb";
    public const string TotemRingModel = "earth_totem_ring.glb";
    public const string RiftModel = "earth_rift.glb";

    /// <summary>Every model the view draws, for clearing and for the tests.</summary>
    public static readonly string[] Models = { StoneModel, QuakeModel, CrackModel, SpikesModel, ChunkModel, TotemModel, TotemRingModel, RiftModel };

    /// <summary>A rift is drawn as pieces about this long (the model's length).</summary>
    private const float RiftPiece = 1f;

    /// <summary>How long a totem takes to rise, and to sink at the end.</summary>
    private const float TotemRise = 0.25f;
    private const float TotemSink = 0.4f;

    /// <summary>How tall the totem model is, and the spikes (at scale 1), so each can be drawn coming up out of the ground.</summary>
    private const float TotemHeight = 1.6f;
    private const float SpikesHeight = 1.1f;

    /// <summary>How many chunks of rubble a shattered stone flings out.</summary>
    private const int RubbleChunks = 9;

    private readonly Random _jitter = new(23);
    private readonly Dictionary<string, List<CrowdInstance>> _crowds = Models.ToDictionary(m => m, _ => new List<CrowdInstance>());
    private float _time;

    public void Sync(EngineWindow window, RollingStone stones, float deltaSeconds, Func<float, float, float?>? groundAt = null)
    {
        _time += deltaSeconds;
        foreach (var list in _crowds.Values)
        {
            list.Clear();
        }

        foreach (var stone in stones.Stones)
        {
            // Turned over as it goes: the way it heads, and rolled on by how far it has come.
            var (yaw, _) = Geometry.YawPitch(new Vector3D<float>(stone.Velocity.X, 0f, stone.Velocity.Z) + new Vector3D<float>(0f, 0f, 1e-4f));
            float speed = MathF.Sqrt(stone.Velocity.X * stone.Velocity.X + stone.Velocity.Z * stone.Velocity.Z);
            float roll = stone.Age * speed / MathF.Max(0.1f, stone.Radius);
            _crowds[StoneModel].Add(new CrowdInstance(stone.Position, yaw, stone.Radius / ShamanStats.BaseBallRadius, roll));
        }

        foreach (var quake in stones.Quakes)
        {
            float t = Math.Clamp(quake.Age / RollingStone.QuakeSeconds, 0f, 1f);
            _crowds[QuakeModel].Add(new CrowdInstance(quake.Centre + new Vector3D<float>(0f, 0.05f, 0f), quake.Centre.X * 3f, quake.Radius * (0.3f + 0.7f * t),
                Flash: 0.25f * (1f - t)));
        }

        foreach (var crack in stones.Cracks)
        {
            // Shaking harder as it comes to burst.
            float due = 1f - Math.Clamp(crack.Left / ShamanStats.CrackDelay, 0f, 1f);
            float shake = 0.04f * due * due;
            var at = crack.Position + new Vector3D<float>(Wobble() * shake, 0.03f, Wobble() * shake);
            _crowds[CrackModel].Add(new CrowdInstance(at, crack.Yaw, crack.Radius * (0.6f + 0.3f * due), Flash: 0.3f * due * due));
        }

        foreach (var burst in stones.Bursts)
        {
            float t = Math.Clamp(burst.Age / RollingStone.BurstSeconds, 0f, 1f);
            if (burst.Rubble)
            {
                AddRubble(burst, t);
            }
            else
            {
                // Rock spikes thrusting up out of the ground and sinking back.
                float up = t < 0.3f ? t / 0.3f : 1f - (t - 0.3f) / 0.7f;
                float scale = burst.Radius * 0.6f;
                var at = burst.Centre - new Vector3D<float>(0f, SpikesHeight * scale * (1f - up), 0f);
                _crowds[SpikesModel].Add(new CrowdInstance(at, burst.Centre.Z * 5f, scale, Flash: 0.2f * (1f - t)));
            }
        }

        foreach (var totem in stones.Totems)
        {
            float rise = Math.Clamp(totem.Age / TotemRise, 0f, 1f) * Math.Clamp(totem.Left / TotemSink, 0f, 1f);
            var at = totem.Position - new Vector3D<float>(0f, TotemHeight * (1f - rise), 0f);
            _crowds[TotemModel].Add(new CrowdInstance(at, totem.Position.X * 7f));
            _crowds[TotemRingModel].Add(new CrowdInstance(totem.Position + new Vector3D<float>(0f, 0.06f, 0f), _time * 0.4f, totem.Reach * rise,
                Flash: 0.15f + 0.1f * MathF.Sin(_time * 6f)));
        }

        foreach (var rift in stones.Rifts)
        {
            AddRift(rift, groundAt);
        }

        foreach (var (model, list) in _crowds)
        {
            window.SetCrowd(model, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(list));
        }
    }

    public void Clear(EngineWindow window)
    {
        foreach (var model in Models)
        {
            window.SetCrowd(model, ReadOnlySpan<CrowdInstance>.Empty);
        }
    }

    /// <summary>A shattered stone's chunks, flung out round it in low arcs and falling back.</summary>
    private void AddRubble(StoneBurst burst, float t)
    {
        for (int i = 0; i < RubbleChunks; i++)
        {
            float angle = MathF.Tau * i / RubbleChunks + burst.Centre.X;
            float reach = burst.Radius * (0.7f + 0.3f * ((i * 37) % 10) / 10f) * t;
            float height = 4f * t * (1f - t) * (0.8f + 0.1f * (i % 3));
            var at = burst.Centre + new Vector3D<float>(MathF.Sin(angle) * reach, 0.1f + height, MathF.Cos(angle) * reach);
            _crowds[ChunkModel].Add(new CrowdInstance(at, angle + t * 6f, 1f - 0.4f * t, t * 9f + i));
        }
    }

    /// <summary>A rift as pieces of split ground laid along it, set on the ground, sinking away as it closes.</summary>
    private void AddRift(StoneRift rift, Func<float, float, float?>? groundAt)
    {
        var way = rift.To - rift.From;
        float length = new Vector2D<float>(way.X, way.Z).Length;
        if (length < 1e-3f)
        {
            return;
        }

        float yaw = MathF.Atan2(way.X, way.Z);
        float open = Math.Clamp(rift.Age / 0.15f, 0f, 1f) * Math.Clamp(rift.Left / 0.3f, 0f, 1f);
        int pieces = Math.Max(1, (int)MathF.Ceiling(length / RiftPiece));
        for (int i = 0; i < pieces; i++)
        {
            var at = rift.From + way * ((i + 0.5f) / pieces);
            if (groundAt?.Invoke(at.X, at.Z) is { } ground)
            {
                at.Y = ground;
            }

            _crowds[RiftModel].Add(new CrowdInstance(at + new Vector3D<float>(0f, 0.03f - 0.2f * (1f - open), 0f), yaw + 0.15f * ((i % 3) - 1),
                (length / pieces) / RiftPiece * (0.5f + 0.5f * open)));
        }
    }

    private float Wobble() => (float)_jitter.NextDouble() * 2f - 1f;
}
