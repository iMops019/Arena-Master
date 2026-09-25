using CEngine.Core;
using Silk.NET.Maths;

namespace ArenaMaster.Game.Items;

/// <summary>A treasure chest on the ground. Walking into it opens it and gives an item rolled with its <see cref="Weights"/>.</summary>
internal sealed class Chest
{
    public Chest(int id, Vector3D<float> position, RarityWeights weights)
    {
        Id = id;
        Position = position;
        Weights = weights;
    }

    public int Id { get; }

    public Vector3D<float> Position { get; }

    public RarityWeights Weights { get; }

    public bool Opened { get; set; }

    /// <summary>Seconds since it was opened - it pops and vanishes.</summary>
    public float OpenedFor { get; set; }
}

/// <summary>An item lying on the ground (a fodder enemy's rare drop), already rolled so its orb shows its rarity. Walking into it picks it up.</summary>
internal sealed class ItemPickup
{
    public ItemPickup(int id, Vector3D<float> position, RunItem item)
    {
        Id = id;
        Position = position;
        Item = item;
    }

    public int Id { get; }

    public Vector3D<float> Position { get; }

    public RunItem Item { get; }
}

/// <summary>
/// The loot on the map: chests that turn up around the player every so often, the chests elites and bosses leave, and items fodder enemies now and then drop.
/// Walking into a chest opens it; walking into an item picks it up. Pure simulation - <see cref="LootView"/> draws it.
/// </summary>
internal sealed class LootField
{
    /// <summary>A chance in this many that a fodder enemy drops an item: 1 in 200.</summary>
    public const float FodderDropChance = 0.005f;

    public const float FirstChestAt = 40f;
    public const float ChestInterval = 60f;
    public const int MaxWorldChests = 3;
    public const float ChestMinDistance = 18f;
    public const float ChestMaxDistance = 45f;

    /// <summary>How close (flat) the player has to come to open a chest or pick up an item.</summary>
    public const float ChestReach = 1.6f;
    public const float PickupReach = 1.2f;

    /// <summary>How long an opened chest stays (popping) before it goes.</summary>
    public const float OpenDuration = 0.5f;

    private readonly List<Chest> _chests = new();
    private readonly List<ItemPickup> _pickups = new();
    private readonly Random _random;
    private int _nextId = 1;
    private float _chestTimer = FirstChestAt;

    public LootField(Random random) => _random = random;

    public IReadOnlyList<Chest> Chests => _chests;

    public IReadOnlyList<ItemPickup> Pickups => _pickups;

    /// <summary>Which items chests and drops can give (the ones unlocked so far). All, unless set.</summary>
    public Func<RunItem, bool> Available { get; set; } = _ => true;

    /// <summary>What a source's odds become before a roll (the Quartermaster's Lucky Charm). Unchanged, unless set.</summary>
    public Func<RarityWeights, RarityWeights> Luck { get; set; } = weights => weights;

    public Chest DropChest(Vector3D<float> position, RarityWeights weights)
    {
        var chest = new Chest(_nextId++, position, weights);
        _chests.Add(chest);
        return chest;
    }

    /// <summary>Drops an item rolled from the world's odds at <paramref name="position"/>.</summary>
    public ItemPickup DropItem(Vector3D<float> position)
    {
        var pickup = new ItemPickup(_nextId++, position, ItemCatalog.Roll(_random, Luck(RarityWeights.World), Available));
        _pickups.Add(pickup);
        return pickup;
    }

    /// <summary>Whether a fodder kill drops an item this time.</summary>
    public bool RollFodderDrop() => _random.NextDouble() < FodderDropChance;

    /// <summary>
    /// One frame: maybe a new chest turns up near the player; chests and items the player walks into are opened or picked up. Returns what the player got; the chests
    /// and items now gone are added to <paramref name="goneChests"/> and <paramref name="gonePickups"/> so their view can go too.
    /// </summary>
    public List<RunItem> Update(float deltaSeconds, Vector3D<float> playerFeet, Func<float, float, float?> groundAt, List<Chest> goneChests, List<ItemPickup> gonePickups)
    {
        var obtained = new List<RunItem>();

        _chestTimer -= deltaSeconds;
        if (_chestTimer <= 0f)
        {
            _chestTimer = ChestInterval;
            if (_chests.Count(c => !c.Opened && c.Weights == RarityWeights.World) < MaxWorldChests && TryPickChestSpot(playerFeet, groundAt, out var spot))
            {
                DropChest(spot, RarityWeights.World);
            }
        }

        foreach (var chest in _chests)
        {
            if (chest.Opened)
            {
                chest.OpenedFor += deltaSeconds;
                if (chest.OpenedFor >= OpenDuration)
                {
                    goneChests.Add(chest);
                }
            }
            else if (Touching(chest.Position, playerFeet, ChestReach))
            {
                chest.Opened = true;
                obtained.Add(ItemCatalog.Roll(_random, Luck(chest.Weights), Available));
            }
        }

        foreach (var pickup in _pickups)
        {
            if (Touching(pickup.Position, playerFeet, PickupReach))
            {
                obtained.Add(pickup.Item);
                gonePickups.Add(pickup);
            }
        }

        _chests.RemoveAll(goneChests.Contains);
        _pickups.RemoveAll(gonePickups.Contains);
        return obtained;
    }

    public void Clear(List<Chest> goneChests, List<ItemPickup> gonePickups)
    {
        goneChests.AddRange(_chests);
        gonePickups.AddRange(_pickups);
        _chests.Clear();
        _pickups.Clear();
        _chestTimer = FirstChestAt;
    }

    private static bool Touching(Vector3D<float> thing, Vector3D<float> playerFeet, float reach)
    {
        float dx = thing.X - playerFeet.X, dz = thing.Z - playerFeet.Z;
        return dx * dx + dz * dz <= reach * reach && MathF.Abs(thing.Y - playerFeet.Y) < 2.5f;
    }

    private bool TryPickChestSpot(Vector3D<float> playerFeet, Func<float, float, float?> groundAt, out Vector3D<float> spot)
    {
        for (int attempt = 0; attempt < 8; attempt++)
        {
            float angle = (float)_random.NextDouble() * MathF.Tau;
            float distance = ChestMinDistance + (float)_random.NextDouble() * (ChestMaxDistance - ChestMinDistance);
            float x = playerFeet.X + MathF.Sin(angle) * distance;
            float z = playerFeet.Z + MathF.Cos(angle) * distance;
            if (groundAt(x, z) is { } ground)
            {
                spot = new Vector3D<float>(x, ground, z);
                return true;
            }
        }

        spot = default;
        return false;
    }
}

/// <summary>Draws the <see cref="LootField"/>: chests with a beam of light over them, and spinning item orbs in their rarity's colour.</summary>
internal sealed class LootView
{
    public const string ChestModel = "chest_placeholder.glb";
    public const string BeamModel = "loot_beam.glb";

    private readonly Dictionary<(int Id, bool Beam), int> _props = new();
    private float _time;

    public static string OrbModel(ItemRarity rarity) => $"item_{rarity.ToString().ToLowerInvariant()}.glb";

    public void Sync(EngineWindow window, LootField field, IEnumerable<Chest> goneChests, IEnumerable<ItemPickup> gonePickups, float deltaSeconds)
    {
        _time += deltaSeconds;

        foreach (int id in goneChests.Select(c => c.Id).Concat(gonePickups.Select(p => p.Id)))
        {
            Remove(window, (id, false));
            Remove(window, (id, true));
        }

        foreach (var chest in field.Chests)
        {
            float yaw = chest.Id * 1.3f;
            if (chest.Opened)
            {
                float t = chest.OpenedFor / LootField.OpenDuration;   // a pop: swell, then shrink away
                Place(window, (chest.Id, false), new PropPlacement(ChestModel, chest.Position, yaw, (1f + 0.4f * t) * (1f - t)));
                Remove(window, (chest.Id, true));
            }
            else
            {
                Place(window, (chest.Id, false), new PropPlacement(ChestModel, chest.Position, yaw, 1f));
                Place(window, (chest.Id, true), new PropPlacement(BeamModel, chest.Position, 0f, 1f));
            }
        }

        foreach (var pickup in field.Pickups)
        {
            var position = pickup.Position + new Vector3D<float>(0f, 0.7f + 0.12f * MathF.Sin(_time * 2.5f + pickup.Id), 0f);
            Place(window, (pickup.Id, false), new PropPlacement(OrbModel(pickup.Item.Rarity), position, _time * 1.8f, 1.1f));
        }
    }

    private void Place(EngineWindow window, (int, bool) key, PropPlacement placement)
    {
        if (_props.TryGetValue(key, out int id))
        {
            window.SetPlacedProp(id, placement);
        }
        else
        {
            _props[key] = window.PlaceProp(placement);
        }
    }

    private void Remove(EngineWindow window, (int, bool) key)
    {
        if (_props.Remove(key, out int id))
        {
            window.RemovePlacedProp(id);
        }
    }
}
