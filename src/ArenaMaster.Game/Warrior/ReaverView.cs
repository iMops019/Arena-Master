using CEngine.Core;
using Silk.NET.Maths;

namespace ArenaMaster.Game.Warrior;

/// <summary>
/// Puts <see cref="ThrownAxes"/> on screen as engine crowds: each axe in the air spinning end over end at the height of the hand, the Axe Storm's two axes whirling
/// round the Warrior over a faint red ring, pools of blood where bleeding enemies died (shrinking away as they dry), and drops of blood falling from everything that
/// bleeds, more for every stack. The drops are the view's own particles: pure show, no effect on the fight. The hero's model still holds both its axes while they
/// are thrown (a stand-in).
/// </summary>
internal sealed class ReaverView
{
    public const string AxeModel = "reaver_axe.glb";
    public const string PoolModel = "blood_pool.glb";
    public const string DropModel = "blood_drop.glb";
    public const string StormRingModel = "axe_storm_ring.glb";

    /// <summary>How high over the ground the axes fly.</summary>
    private const float AxeHeight = 1.05f;

    /// <summary>How big an axe is drawn, against the model's size (a hand axe about 0.75 m long).</summary>
    private const float AxeSize = 1.15f;

    /// <summary>Drops a second from a bleeding enemy: some for bleeding at all, more for every stack.</summary>
    private const float DropsPerEnemy = 1.5f;
    private const float DropsPerStack = 1.2f;

    /// <summary>At most this many drops at once, however much is bleeding.</summary>
    private const int MaxDrops = 700;

    private const float Gravity = 9f;

    /// <summary>A pool shrinks away over its last this-many seconds.</summary>
    private const float PoolDrying = 0.8f;

    private sealed class Drop
    {
        public Vector3D<float> Position;
        public Vector3D<float> Velocity;
        public float Size;
    }

    private readonly Random _random = new(29);
    private readonly List<Drop> _drops = new();
    private readonly List<CrowdInstance> _axes = new();
    private readonly List<CrowdInstance> _pools = new();
    private readonly List<CrowdInstance> _dropped = new();
    private readonly List<CrowdInstance> _ring = new();
    private float _time;

    public void Sync(EngineWindow window, ThrownAxes throws, float deltaSeconds, Func<float, float, float?> groundAt)
    {
        _time += deltaSeconds;
        _axes.Clear();
        _pools.Clear();
        _dropped.Clear();
        _ring.Clear();

        foreach (var axe in throws.Axes)
        {
            float y = (groundAt(axe.Position.X, axe.Position.Z) ?? axe.Position.Y) + AxeHeight;
            _axes.Add(new CrowdInstance(new Vector3D<float>(axe.Position.X, y, axe.Position.Z), MathF.Atan2(axe.Heading.X, axe.Heading.Z), AxeSize, axe.Spin, Flash: 0.15f));
        }

        if (throws.Storming)
        {
            var centre = throws.Owner;
            for (int k = 0; k < 2; k++)
            {
                float angle = throws.StormAngle + k * MathF.PI;
                float x = centre.X + MathF.Sin(angle) * WarriorStats.StormRadius;
                float z = centre.Z + MathF.Cos(angle) * WarriorStats.StormRadius;
                float y = (groundAt(x, z) ?? centre.Y) + AxeHeight;
                _axes.Add(new CrowdInstance(new Vector3D<float>(x, y, z), angle + MathF.PI * 0.5f, AxeSize, _time * ThrownAxes.SpinRate, Flash: 0.3f));
            }

            float pulse = 0.5f + 0.5f * MathF.Sin(_time * 10f);
            _ring.Add(new CrowdInstance(new Vector3D<float>(centre.X, (groundAt(centre.X, centre.Z) ?? centre.Y) + 0.08f, centre.Z), 0f, WarriorStats.StormRadius,
                Flash: 0.3f + 0.3f * pulse));
        }

        foreach (var pool in throws.Pools)
        {
            float size = WarriorStats.PoolRadius * MathF.Min(1f, pool.Left / PoolDrying) * MathF.Min(1f, 0.4f + 3f * (WarriorStats.PoolSeconds - pool.Left));
            float y = (groundAt(pool.Centre.X, pool.Centre.Z) ?? pool.Centre.Y) + 0.03f;
            _pools.Add(new CrowdInstance(new Vector3D<float>(pool.Centre.X, y, pool.Centre.Z), pool.Centre.X * 1.7f, size, Flash: 0.1f));
        }

        foreach (var (enemy, wound) in throws.Wounds)
        {
            if (!enemy.IsAlive || wound.Stacks.Count == 0)
            {
                continue;
            }

            float due = (DropsPerEnemy + DropsPerStack * wound.Stacks.Count) * deltaSeconds;
            for (; due > 0f && _drops.Count < MaxDrops; due -= 1f)
            {
                if (due < 1f && _random.NextDouble() > due)
                {
                    break;
                }

                float a = (float)_random.NextDouble() * MathF.Tau;
                float r = enemy.Kind.Radius * (0.3f + 0.6f * (float)_random.NextDouble());
                _drops.Add(new Drop
                {
                    Position = enemy.Position + new Vector3D<float>(MathF.Sin(a) * r, enemy.Kind.Height * (0.35f + 0.4f * (float)_random.NextDouble()), MathF.Cos(a) * r),
                    Velocity = new Vector3D<float>(MathF.Sin(a) * 0.6f, 0.4f + 0.8f * (float)_random.NextDouble(), MathF.Cos(a) * 0.6f),
                    Size = 0.8f + 0.5f * (float)_random.NextDouble(),
                });
            }
        }

        for (int i = _drops.Count - 1; i >= 0; i--)
        {
            var drop = _drops[i];
            drop.Velocity -= new Vector3D<float>(0f, Gravity * deltaSeconds, 0f);
            drop.Position += drop.Velocity * deltaSeconds;
            float ground = groundAt(drop.Position.X, drop.Position.Z) ?? float.MinValue;
            if (drop.Position.Y <= ground)
            {
                _drops.RemoveAt(i);
                continue;
            }

            _dropped.Add(new CrowdInstance(drop.Position, 0f, drop.Size, Flash: 0.2f));
        }

        window.SetCrowd(AxeModel, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_axes));
        window.SetCrowd(PoolModel, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_pools));
        window.SetCrowd(DropModel, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_dropped));
        window.SetCrowd(StormRingModel, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_ring));
    }

    public void Clear(EngineWindow window)
    {
        _drops.Clear();
        foreach (var model in new[] { AxeModel, PoolModel, DropModel, StormRingModel })
        {
            window.SetCrowd(model, ReadOnlySpan<CrowdInstance>.Empty);
        }
    }
}
