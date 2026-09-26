using ArenaMaster.Game.Combat;
using CEngine.Core;
using Silk.NET.Maths;

namespace ArenaMaster.Game.Mage;

/// <summary>
/// Puts the Mage's frost on screen as engine crowds: the bolts (the comet a big one), each burst's pale ring spreading over the ground, and ice shards - a tight
/// ring circling the Mage while the Frost Shield holds, and a wide, low swirl while the Blizzard blows.
/// </summary>
internal sealed class FrostView
{
    public const string BoltModel = "frost_bolt.glb";
    public const string BurstModel = "frost_blast.glb";
    public const string ShardModel = "frost_shard.glb";

    private const int ShieldShards = 8;
    private const int BlizzardShards = 24;

    private readonly List<CrowdInstance> _bolts = new();
    private readonly List<CrowdInstance> _bursts = new();
    private readonly List<CrowdInstance> _shards = new();
    private float _time;

    public void Sync(EngineWindow window, FrostBarrage barrage, Vector3D<float> feet, bool shieldUp, bool blizzard, float blizzardRadius, float deltaSeconds)
    {
        _time += deltaSeconds;

        _bolts.Clear();
        foreach (var bolt in barrage.Bolts)
        {
            var (yaw, pitch) = Geometry.YawPitch(bolt.Heading);
            _bolts.Add(new CrowdInstance(bolt.Position, yaw, bolt.Scale, pitch));
        }

        _bursts.Clear();
        foreach (var burst in barrage.Bursts)
        {
            float t = Math.Clamp(burst.Age / FrostBarrage.BurstDuration, 0f, 1f);
            float spread = 1f - (1f - t) * (1f - t);
            _bursts.Add(new CrowdInstance(burst.Centre + new Vector3D<float>(0f, 0.15f + 0.3f * t, 0f), 0f, burst.Radius * (0.3f + 0.7f * spread), Flash: 1f - t));
        }

        _shards.Clear();
        if (shieldUp)
        {
            for (int i = 0; i < ShieldShards; i++)
            {
                float angle = _time * 2.2f + MathF.Tau * i / ShieldShards;
                var at = feet + new Vector3D<float>(MathF.Sin(angle) * 0.85f, 0.9f + 0.15f * MathF.Sin(_time * 3f + i), MathF.Cos(angle) * 0.85f);
                _shards.Add(new CrowdInstance(at, angle, 1f, 0f, Flash: 0.3f));
            }
        }

        if (blizzard)
        {
            for (int i = 0; i < BlizzardShards; i++)
            {
                float radius = 1.5f + (blizzardRadius - 1.5f) * ((i * 7 % BlizzardShards) / (float)BlizzardShards);
                float angle = _time * (3.5f - radius * 0.35f) + MathF.Tau * i / BlizzardShards;
                var at = feet + new Vector3D<float>(MathF.Sin(angle) * radius, 0.3f + 0.25f * (i % 4), MathF.Cos(angle) * radius);
                _shards.Add(new CrowdInstance(at, angle + MathF.PI / 2f, 0.7f, 0.6f));
            }
        }

        window.SetCrowd(BoltModel, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_bolts));
        window.SetCrowd(BurstModel, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_bursts));
        window.SetCrowd(ShardModel, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_shards));
    }

    public void Clear(EngineWindow window)
    {
        window.SetCrowd(BoltModel, ReadOnlySpan<CrowdInstance>.Empty);
        window.SetCrowd(BurstModel, ReadOnlySpan<CrowdInstance>.Empty);
        window.SetCrowd(ShardModel, ReadOnlySpan<CrowdInstance>.Empty);
    }
}
