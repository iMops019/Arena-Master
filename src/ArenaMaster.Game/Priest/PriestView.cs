using CEngine.Core;
using Silk.NET.Maths;

namespace ArenaMaster.Game.Priest;

/// <summary>
/// Puts <see cref="PlagueSkulls"/> and the Priest's death and decay on screen as engine crowds. Each skull grins along its way, wrapped in a slow swirl of
/// ghostly wisps and trailing green motes and dark ash. Everything Plagued sheds motes, more for every stack. Rot lies on the ground as dark, glowing pools
/// (a wedge of them for Death and Decay) with motes bubbling up out of it; the spray itself bursts out of the Skull Shield as a gush of motes along its cone. The
/// Skull Shield and the wand's skull smoulder all the while, Pestilence's leaps streak from the dead to the living, and Rotting Step leaves in a puff of rot and
/// comes back in another. The motes are the view's own particles: pure show, no effect on the fight.
/// </summary>
internal sealed class PriestView
{
    public const string SkullModel = "plague_skull.glb";
    public const string WispModel = "plague_wisp.glb";
    public const string MoteModel = "plague_mote.glb";
    public const string AshModel = "rot_ash.glb";
    public const string PoolModel = "rot_pool.glb";

    /// <summary>How big a skull (and its wisps) is drawn, against the model's 0.34 m.</summary>
    private const float SkullSize = 0.75f;

    /// <summary>At most this many motes at once, however much is rotting.</summary>
    private const int MaxMotes = 1400;

    /// <summary>Motes a second: from each skull, each stack of Plague, each square metre of rot, the shield and the wand.</summary>
    private const float SkullMotes = 45f;
    private const float PlagueMotes = 4f;
    private const float RotMotes = 0.7f;
    private const float ShieldMotes = 14f;
    private const float WandMotes = 9f;

    /// <summary>Death and Decay's pools are laid this far apart over its cone.</summary>
    private const float PoolSpacing = 1.35f;

    private sealed class Mote
    {
        public Vector3D<float> Position;
        public Vector3D<float> Velocity;
        public float Age;
        public float Life;
        public float Size;
        public bool Ash;
        public float Spin;
    }

    private readonly Random _random = new(23);
    private readonly List<Mote> _motes = new();
    private readonly List<CrowdInstance> _skulls = new();
    private readonly List<CrowdInstance> _wisps = new();
    private readonly List<CrowdInstance> _glow = new();
    private readonly List<CrowdInstance> _ash = new();
    private readonly List<CrowdInstance> _pools = new();
    private readonly HashSet<RotSpray> _sprayed = new();
    private float _time;

    /// <summary>
    /// Draws this frame. <paramref name="feet"/> and <paramref name="facing"/> (flat) place the Priest's shield and wand; <paramref name="fighting"/> is false at
    /// camp, where only the shield and wand smoulder.
    /// </summary>
    public void Sync(EngineWindow window, PlagueSkulls skulls, float deltaSeconds, Func<float, float, float?> groundAt, Vector3D<float> feet,
        Vector3D<float> facing, bool fighting = true)
    {
        _time += deltaSeconds;
        var right = new Vector3D<float>(facing.Z, 0f, -facing.X);

        // The Skull Shield on the left arm, and the skull on the wand in the right hand, smouldering.
        Emit(ShieldMotes * deltaSeconds, () => feet + right * -0.42f + facing * 0.42f + new Vector3D<float>(0f, 1.0f, 0f), 0.22f, rise: 0.5f);
        Emit(WandMotes * deltaSeconds, () => feet + right * 0.36f + facing * 0.5f + new Vector3D<float>(0f, 1.3f, 0f), 0.07f, rise: 0.4f);

        _skulls.Clear();
        _wisps.Clear();
        _pools.Clear();
        if (fighting)
        {
            foreach (var skull in skulls.Skulls)
            {
                float yaw = MathF.Atan2(skull.Heading.X, skull.Heading.Z);
                float bob = MathF.Sin(skull.Age * 9f) * 0.08f;
                var at = skull.Position + new Vector3D<float>(0f, bob, 0f);
                float flash = 0.35f + 0.25f * MathF.Abs(MathF.Sin(skull.Age * 7f));
                _skulls.Add(new CrowdInstance(at, yaw + 0.25f * MathF.Sin(skull.Age * 5f), SkullSize, 0.15f * MathF.Sin(skull.Age * 6f), Flash: flash));
                _wisps.Add(new CrowdInstance(at, _time * 3f + skull.Age, SkullSize * (1f + 0.12f * MathF.Sin(_time * 8f)), _time * 1.7f, Flash: 0.5f));
                _wisps.Add(new CrowdInstance(at, -_time * 2.3f, SkullSize * 0.8f, -_time * 2.1f + 1f, Flash: 0.4f));
                var behind = at - skull.Heading * 0.2f;
                Emit(SkullMotes * deltaSeconds, () => behind + Jitter(0.14f), 0.1f, rise: 0.35f, ashShare: 0.4f);
            }

            foreach (var (enemy, infection) in skulls.Infections)
            {
                if (!enemy.IsAlive)
                {
                    continue;
                }

                float height = enemy.Kind.Height;
                Emit(PlagueMotes * infection.Stacks.Count * deltaSeconds,
                    () => enemy.Position + new Vector3D<float>(0f, height * (0.3f + 0.7f * (float)_random.NextDouble()), 0f) + Jitter(enemy.Kind.Radius), 0.1f,
                    rise: 0.8f, ashShare: 0.25f);
            }

            foreach (var patch in skulls.Patches)
            {
                AddPools(patch, groundAt, deltaSeconds);
            }

            foreach (var spray in skulls.Sprays)
            {
                if (_sprayed.Add(spray))
                {
                    Gush(spray);
                }
            }

            _sprayed.RemoveWhere(s => !skulls.Sprays.Contains(s));
        }

        MoveMotes(deltaSeconds);
        window.SetCrowd(SkullModel, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_skulls));
        window.SetCrowd(WispModel, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_wisps));
        window.SetCrowd(PoolModel, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_pools));
        window.SetCrowd(MoteModel, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_glow));
        window.SetCrowd(AshModel, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_ash));
    }

    /// <summary>A puff of rot at <paramref name="at"/> (Rotting Step vanishing, or coming back).</summary>
    public void Puff(Vector3D<float> at)
    {
        for (int i = 0; i < 40; i++)
        {
            var way = Vector3D.Normalize(new Vector3D<float>(Signed(), 0.4f + (float)_random.NextDouble(), Signed()));
            Add(at + new Vector3D<float>(0f, 0.2f + 1.4f * (float)_random.NextDouble(), 0f), way * (1.5f + 2.5f * (float)_random.NextDouble()),
                0.5f + 0.4f * (float)_random.NextDouble(), 0.14f, ash: i % 2 == 0);
        }
    }

    public void Clear(EngineWindow window)
    {
        _motes.Clear();
        _sprayed.Clear();
        foreach (var model in new[] { SkullModel, WispModel, PoolModel, MoteModel, AshModel })
        {
            window.SetCrowd(model, ReadOnlySpan<CrowdInstance>.Empty);
        }
    }

    /// <summary>
    /// A patch of rot as pools on the ground: one for a circle, a spread of them over a wedge, swelling as it comes and shrinking as it goes, and motes bubbling up
    /// from it.
    /// </summary>
    private void AddPools(RotPatch patch, Func<float, float, float?> groundAt, float deltaSeconds)
    {
        float age = patch.Lifetime - patch.Left;
        float grow = MathF.Min(1f, age / 0.3f) * MathF.Min(1f, patch.Left / 0.6f);
        float glow = 0.2f + 0.15f * MathF.Sin(_time * 3f + patch.Centre.X);
        float area = patch.HalfArc * patch.Radius * patch.Radius;
        if (patch.HalfArc >= MathF.PI)
        {
            _pools.Add(new CrowdInstance(OnGround(patch.Centre, groundAt, 0.05f), patch.Centre.X * 3f, patch.Radius * grow, Flash: glow));
        }
        else
        {
            int index = 0;
            for (float r = PoolSpacing * 0.6f; r < patch.Radius; r += PoolSpacing)
            {
                int across = Math.Max(1, (int)MathF.Ceiling(2f * patch.HalfArc * r / PoolSpacing));
                for (int i = 0; i < across; i++, index++)
                {
                    float yaw = patch.Yaw - patch.HalfArc + (i + 0.5f) / across * 2f * patch.HalfArc;
                    var at = patch.Centre + new Vector3D<float>(MathF.Sin(yaw) * r, 0f, MathF.Cos(yaw) * r);
                    float size = PoolSpacing * (0.65f + 0.2f * MathF.Sin(index * 2.3f)) * grow;
                    _pools.Add(new CrowdInstance(OnGround(at, groundAt, 0.05f + 0.002f * (index % 5)), index * 1.7f, size, Flash: glow));
                }
            }
        }

        Emit(RotMotes * area * deltaSeconds, () => RandomIn(patch, groundAt), 0.1f, rise: 0.9f, ashShare: 0.5f);
    }

    /// <summary>Death and Decay gushing from the shield: motes flung out along its cone.</summary>
    private void Gush(RotSpray spray)
    {
        for (int i = 0; i < 90; i++)
        {
            float yaw = spray.Yaw + Signed() * spray.HalfArc;
            float speed = spray.Reach * (1.1f + 1.2f * (float)_random.NextDouble());
            var way = new Vector3D<float>(MathF.Sin(yaw), 0.15f + 0.2f * (float)_random.NextDouble(), MathF.Cos(yaw));
            Add(spray.Origin + new Vector3D<float>(0f, 0.9f, 0f), way * speed, 0.35f + 0.3f * (float)_random.NextDouble(), 0.16f, ash: i % 3 == 0);
        }
    }

    private void MoveMotes(float deltaSeconds)
    {
        _glow.Clear();
        _ash.Clear();
        for (int i = _motes.Count - 1; i >= 0; i--)
        {
            var mote = _motes[i];
            mote.Age += deltaSeconds;
            if (mote.Age >= mote.Life)
            {
                _motes.RemoveAt(i);
                continue;
            }

            mote.Position += mote.Velocity * deltaSeconds;
            float t = mote.Age / mote.Life;
            float size = mote.Size * (t < 0.2f ? t / 0.2f : 1f - (t - 0.2f) / 0.8f * 0.9f);
            var copy = new CrowdInstance(mote.Position, mote.Spin + mote.Age * 4f, size, mote.Age * 3f, Flash: mote.Ash ? 0f : 0.6f * (1f - t));
            (mote.Ash ? _ash : _glow).Add(copy);
        }
    }

    /// <summary>Emits about <paramref name="count"/> motes (a fraction carries as a chance) at <paramref name="where"/>, drifting up at about <paramref name="rise"/>.</summary>
    private void Emit(float count, Func<Vector3D<float>> where, float size, float rise, float ashShare = 0.3f)
    {
        int whole = (int)count;
        if (_random.NextDouble() < count - whole)
        {
            whole++;
        }

        for (int i = 0; i < whole; i++)
        {
            Add(where(), new Vector3D<float>(Signed() * 0.25f, rise * (0.6f + 0.8f * (float)_random.NextDouble()), Signed() * 0.25f),
                0.7f + 0.8f * (float)_random.NextDouble(), size * (0.6f + 0.8f * (float)_random.NextDouble()), ash: _random.NextDouble() < ashShare);
        }
    }

    private void Add(Vector3D<float> at, Vector3D<float> velocity, float life, float size, bool ash)
    {
        if (_motes.Count >= MaxMotes)
        {
            return;
        }

        _motes.Add(new Mote
        {
            Position = at,
            Velocity = velocity,
            Life = life,
            Size = size,
            Ash = ash,
            Spin = (float)_random.NextDouble() * MathF.Tau,
        });
    }

    private Vector3D<float> RandomIn(RotPatch patch, Func<float, float, float?> groundAt)
    {
        float r = patch.Radius * MathF.Sqrt((float)_random.NextDouble());
        float yaw = patch.HalfArc >= MathF.PI ? (float)_random.NextDouble() * MathF.Tau : patch.Yaw + Signed() * patch.HalfArc;
        return OnGround(patch.Centre + new Vector3D<float>(MathF.Sin(yaw) * r, 0f, MathF.Cos(yaw) * r), groundAt, 0.1f);
    }

    private static Vector3D<float> OnGround(Vector3D<float> at, Func<float, float, float?> groundAt, float lift) =>
        new(at.X, (groundAt(at.X, at.Z) ?? at.Y) + lift, at.Z);

    private Vector3D<float> Jitter(float by) => new(Signed() * by, Signed() * by, Signed() * by);

    private float Signed() => (float)_random.NextDouble() * 2f - 1f;
}
