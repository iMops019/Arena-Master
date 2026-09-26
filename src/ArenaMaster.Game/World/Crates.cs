using ArenaMaster.Game.Combat;
using CEngine.Core;
using Silk.NET.Maths;

namespace ArenaMaster.Game.World;

/// <summary>What a broken crate can leave behind. The content decides what each does when it is picked up.</summary>
internal enum PickupKind
{
    /// <summary>Every experience gem on the map flies to the player.</summary>
    Magnet,

    /// <summary>A small heal.</summary>
    Apple,

    /// <summary>A big heal.</summary>
    Roast,

    /// <summary>A pouch of silver, added to the run's.</summary>
    Silver,

    /// <summary>A blast that hurts every enemy near the player.</summary>
    Bomb,

    /// <summary>A while of much faster attacks.</summary>
    Frenzy,

    /// <summary>Not a pickup: the crate drops a big experience gem instead.</summary>
    BigGem,
}

/// <summary>A pickup lying where a crate broke, waiting to be walked over.</summary>
internal sealed class Pickup
{
    public int Id { get; init; }

    public PickupKind Kind { get; init; }

    /// <summary>The ground it sits on.</summary>
    public Vector3D<float> Position { get; init; }

    public float Age { get; set; }
}

/// <summary>
/// Breakable crates and what they leave: every so often a crate turns up on the ground somewhere around the player (never too close, never too many), standing on
/// the enemy field as a <see cref="EnemyKind.Crate"/> so any attack breaks it. A broken crate leaves a random pickup - a magnet, food, silver, a bomb, a frenzy
/// potion - or a big gem; walking over a pickup takes it. Crates left far behind quietly go. Pure - no engine calls; <see cref="CrateView"/> draws the pickups.
/// </summary>
internal sealed class CrateField
{
    /// <summary>The first crate comes this soon into a run, then one every so often while there are fewer than the most allowed.</summary>
    public const float FirstCrate = 20f;
    public const float CrateInterval = 12f;
    public const int MaxCrates = 8;

    /// <summary>A crate turns up this near and this far from the player; one left further behind than this is taken away.</summary>
    public const float MinDistance = 12f;
    public const float MaxDistance = 32f;
    public const float DespawnDistance = 70f;

    /// <summary>How close the player has to walk to take a pickup.</summary>
    public const float CollectRadius = 1.4f;

    /// <summary>How long a bomb's blast shows, for the view.</summary>
    public const float BlastSeconds = 0.4f;

    /// <summary>What a crate leaves, and how often: relative weights.</summary>
    public static readonly IReadOnlyList<(PickupKind Kind, float Weight)> Odds = new[]
    {
        (PickupKind.Apple, 20f),
        (PickupKind.Roast, 12f),
        (PickupKind.Silver, 20f),
        (PickupKind.Magnet, 14f),
        (PickupKind.BigGem, 14f),
        (PickupKind.Bomb, 10f),
        (PickupKind.Frenzy, 10f),
    };

    private readonly Random _random;
    private readonly List<Pickup> _pickups = new();
    private readonly List<(Vector3D<float> Centre, float Radius, float Age)> _blasts = new();
    private float _crateIn = FirstCrate;
    private int _nextId = 1;

    public CrateField(Random random) => _random = random;

    public IReadOnlyList<Pickup> Pickups => _pickups;

    /// <summary>Bomb blasts spreading right now, for the view.</summary>
    public IReadOnlyList<(Vector3D<float> Centre, float Radius, float Age)> Blasts => _blasts;

    /// <summary>One frame: a new crate when one is due, crates left far behind taken away, the pickups and blasts ageing.</summary>
    public void Update(float deltaSeconds, Vector3D<float> playerFeet, EnemyField enemies, Func<float, float, float?> groundAt)
    {
        var crates = enemies.Enemies.Where(e => e.IsAlive && e.Kind.IsProp).ToList();
        foreach (var crate in crates)
        {
            Geometry.FlatDirection(playerFeet, crate.Position, out float distance);
            if (distance > DespawnDistance)
            {
                enemies.Vanish(crate);
            }
        }

        _crateIn -= deltaSeconds;
        if (_crateIn <= 0f)
        {
            _crateIn = CrateInterval;
            if (crates.Count < MaxCrates)
            {
                PlaceCrate(playerFeet, enemies, groundAt);
            }
        }

        foreach (var pickup in _pickups)
        {
            pickup.Age += deltaSeconds;
        }

        for (int i = _blasts.Count - 1; i >= 0; i--)
        {
            var blast = _blasts[i];
            blast.Age += deltaSeconds;
            if (blast.Age >= BlastSeconds)
            {
                _blasts.RemoveAt(i);
            }
            else
            {
                _blasts[i] = blast;
            }
        }
    }

    /// <summary>
    /// A crate broke at <paramref name="at"/> (its feet): rolls what it leaves. A pickup is put on the ground there - except a big gem, which the caller drops as
    /// experience. Returns what it rolled.
    /// </summary>
    public PickupKind Break(Vector3D<float> at)
    {
        var kind = Roll(_random);
        if (kind != PickupKind.BigGem)
        {
            _pickups.Add(new Pickup { Id = _nextId++, Kind = kind, Position = at });
        }

        return kind;
    }

    /// <summary>Takes (and returns) every pickup the player at <paramref name="playerFeet"/> is standing on.</summary>
    public List<PickupKind> Collect(Vector3D<float> playerFeet)
    {
        var taken = new List<PickupKind>();
        for (int i = _pickups.Count - 1; i >= 0; i--)
        {
            Geometry.FlatDirection(playerFeet, _pickups[i].Position, out float distance);
            if (distance <= CollectRadius && MathF.Abs(_pickups[i].Position.Y - playerFeet.Y) < 2f)
            {
                taken.Add(_pickups[i].Kind);
                _pickups.RemoveAt(i);
            }
        }

        return taken;
    }

    /// <summary>Shows a bomb's blast of <paramref name="radius"/> at <paramref name="centre"/>.</summary>
    public void AddBlast(Vector3D<float> centre, float radius) => _blasts.Add((centre, radius, 0f));

    /// <summary>Everything off the ground (a run ended), and the first crate's clock back to the start. Crates on the enemy field go with the field.</summary>
    public void Clear()
    {
        _pickups.Clear();
        _blasts.Clear();
        _crateIn = FirstCrate;
    }

    /// <summary>A random pickup by <see cref="Odds"/>.</summary>
    public static PickupKind Roll(Random random)
    {
        float total = Odds.Sum(o => o.Weight);
        float pick = (float)random.NextDouble() * total;
        foreach (var (kind, weight) in Odds)
        {
            if ((pick -= weight) < 0f)
            {
                return kind;
            }
        }

        return Odds[^1].Kind;
    }

    private void PlaceCrate(Vector3D<float> playerFeet, EnemyField enemies, Func<float, float, float?> groundAt)
    {
        for (int attempt = 0; attempt < 8; attempt++)
        {
            float angle = (float)_random.NextDouble() * MathF.Tau;
            float distance = MinDistance + (float)_random.NextDouble() * (MaxDistance - MinDistance);
            float x = playerFeet.X + MathF.Sin(angle) * distance;
            float z = playerFeet.Z + MathF.Cos(angle) * distance;
            if (groundAt(x, z) is { } ground)
            {
                var crate = enemies.Spawn(new Vector3D<float>(x, ground, z), EnemyKind.Crate);
                crate.Yaw = (float)_random.NextDouble() * MathF.Tau;
                return;
            }
        }
    }
}

/// <summary>
/// Draws the crates' pickups - each kind one engine crowd, floating over the ground, turning and pulsing so they catch the eye - and a bomb's blast spreading out.
/// The crates themselves are drawn with the enemies.
/// </summary>
internal sealed class CrateView
{
    public const string BlastModel = "bomb_blast.glb";

    private static readonly Dictionary<PickupKind, string> Models = new()
    {
        [PickupKind.Magnet] = "pickup_magnet.glb",
        [PickupKind.Apple] = "pickup_apple.glb",
        [PickupKind.Roast] = "pickup_roast.glb",
        [PickupKind.Silver] = "pickup_silver.glb",
        [PickupKind.Bomb] = "pickup_bomb.glb",
        [PickupKind.Frenzy] = "pickup_frenzy.glb",
    };

    private readonly Dictionary<string, List<CrowdInstance>> _crowds = Models.Values.ToDictionary(m => m, _ => new List<CrowdInstance>());
    private readonly List<CrowdInstance> _blasts = new();
    private float _time;

    public void Sync(EngineWindow window, CrateField crates, float deltaSeconds)
    {
        _time += deltaSeconds;
        foreach (var list in _crowds.Values)
        {
            list.Clear();
        }

        foreach (var pickup in crates.Pickups)
        {
            float bob = 0.55f + 0.12f * MathF.Sin(_time * 3f + pickup.Id);
            float pulse = 0.25f + 0.25f * MathF.Sin(_time * 5f + pickup.Id);
            _crowds[Models[pickup.Kind]].Add(new CrowdInstance(pickup.Position + new Vector3D<float>(0f, bob, 0f), _time * 1.8f + pickup.Id, 1f, 0f, Flash: pulse));
        }

        _blasts.Clear();
        foreach (var (centre, radius, age) in crates.Blasts)
        {
            float t = Math.Clamp(age / CrateField.BlastSeconds, 0f, 1f);
            _blasts.Add(new CrowdInstance(centre + new Vector3D<float>(0f, 0.2f + 0.5f * t, 0f), 0f, radius * (0.2f + 0.8f * (1f - (1f - t) * (1f - t))), Flash: 1f - t));
        }

        foreach (var (model, list) in _crowds)
        {
            window.SetCrowd(model, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(list));
        }

        window.SetCrowd(BlastModel, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_blasts));
    }

    public void Clear(EngineWindow window)
    {
        foreach (var model in Models.Values)
        {
            window.SetCrowd(model, ReadOnlySpan<CrowdInstance>.Empty);
        }

        window.SetCrowd(BlastModel, ReadOnlySpan<CrowdInstance>.Empty);
    }
}
