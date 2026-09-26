using ArenaMaster.Game.Progression;

namespace ArenaMaster.Game.Delve;

/// <summary>What a Delve node pays when it is cleared.</summary>
internal enum DelveNodeKind
{
    /// <summary>A heavy purse of silver.</summary>
    Currency,

    /// <summary>A chance at a piece of gear (see <see cref="DelveRules.ArmouryGearChance"/>), besides the cache's silver.</summary>
    Armoury,

    /// <summary>Experience for the active passive tree.</summary>
    Knowledge,

    /// <summary>Items: two rolls at a boss chest's odds.</summary>
    Relic,

    /// <summary>A fight with the Hollow King Unbound in the arena, for Delve Marks, an item, silver and a chance at gear. Every <see cref="DelveMap.BossEvery"/>th floor.</summary>
    Boss,
}

/// <summary>
/// One node on the Delve chart: a run to take on floor <paramref name="Depth"/>, in column <paramref name="Slot"/> (left to right on the chart), paying out its
/// <paramref name="Kind"/> when cleared.
/// </summary>
internal sealed record DelveNode(int Depth, int Slot, DelveNodeKind Kind)
{
    public string Id => $"{Depth}-{Slot}";

    public bool IsBoss => Kind == DelveNodeKind.Boss;

    public string Name => DelveMap.NameOf(Kind);
}

/// <summary>
/// The Delve's chart: every floor down from depth 1, each with <see cref="NodesPerFloor"/> nodes of mixed kinds, and on every <see cref="BossEvery"/>th floor one more,
/// the Boss node. A floor is the same every time it is asked for (seeded by its depth), so the chart doesn't reshuffle between visits. Pure.
/// </summary>
internal static class DelveMap
{
    public const int NodesPerFloor = 3;
    public const int BossEvery = 5;

    private static readonly DelveNodeKind[] RunKinds = { DelveNodeKind.Currency, DelveNodeKind.Armoury, DelveNodeKind.Knowledge, DelveNodeKind.Relic };

    public static bool IsBossFloor(int depth) => depth % BossEvery == 0;

    /// <summary>
    /// The nodes of floor <paramref name="depth"/> (1 and down): <see cref="NodesPerFloor"/> run nodes of different kinds, picked the same way every time for the
    /// same depth; on a boss floor, the Boss node after them.
    /// </summary>
    public static IReadOnlyList<DelveNode> Floor(int depth)
    {
        var random = new Random(depth * 7919 + 17);
        var kinds = RunKinds.OrderBy(_ => random.Next()).Take(NodesPerFloor).ToList();
        var nodes = kinds.Select((kind, slot) => new DelveNode(depth, slot, kind)).ToList();
        if (IsBossFloor(depth))
        {
            nodes.Add(new DelveNode(depth, NodesPerFloor, DelveNodeKind.Boss));
        }

        return nodes;
    }

    /// <summary>The node with <paramref name="id"/> ("depth-slot"), or null if there is none.</summary>
    public static DelveNode? Find(string id)
    {
        var parts = id.Split('-');
        if (parts.Length != 2 || !int.TryParse(parts[0], out int depth) || !int.TryParse(parts[1], out int slot) || depth < 1)
        {
            return null;
        }

        return Floor(depth).FirstOrDefault(n => n.Slot == slot);
    }

    public static string NameOf(DelveNodeKind kind) => kind switch
    {
        DelveNodeKind.Currency => "Currency Delve",
        DelveNodeKind.Armoury => "Armoury Delve",
        DelveNodeKind.Knowledge => "Knowledge Delve",
        DelveNodeKind.Relic => "Relic Delve",
        _ => "Delve Boss",
    };

    public static string RewardOf(DelveNodeKind kind) => kind switch
    {
        DelveNodeKind.Currency => "A heavy purse of silver in the cache.",
        DelveNodeKind.Armoury => "A chance at a piece of gear you don't own yet, and silver either way.",
        DelveNodeKind.Knowledge => "A large sum of experience for your active passive tree.",
        DelveNodeKind.Relic => "Two items at a boss chest's odds.",
        _ => "Delve Marks, an item and silver, and a good chance at a piece of gear. The Hollow King Unbound waits in the arena: no swarm, just the two of you.",
    };
}

/// <summary>Where the player has got to in the Delve, saved in the <see cref="Profile"/>.</summary>
internal sealed class DelveSave
{
    /// <summary>The deepest floor open: every floor from 1 down to this one can be run.</summary>
    public int Deepest { get; set; } = 1;

    /// <summary>The ids of the nodes cleared. A cleared node is done; the floor's others stay open.</summary>
    public List<string> Cleared { get; set; } = new();

    /// <summary>Delve Marks: the currency only Delve bosses give.</summary>
    public long Marks { get; set; }

    public int BossesSlain { get; set; }

    /// <summary>Node kind -> how many of that kind have been cleared.</summary>
    public Dictionary<string, int> ClearedByKind { get; set; } = new();
}

/// <summary>What clearing a node pays. <paramref name="GearChance"/> is the chance (0 to 1) the cache also holds a piece of gear: never a sure thing, so a piece
/// the player is after may take a few runs.</summary>
internal sealed record DelveReward(long Silver, long TreeExperience, int Items, float GearChance, long Marks);

/// <summary>The Delve's rules: which floors and nodes are open, what clearing one does, how hard a floor is and what it pays. Pure.</summary>
internal static class DelveRules
{
    /// <summary>Whether <paramref name="node"/> can be run: its floor is open and it hasn't been cleared.</summary>
    public static bool IsOpen(DelveSave save, DelveNode node) => node.Depth <= save.Deepest && !save.Cleared.Contains(node.Id);

    public static bool IsCleared(DelveSave save, DelveNode node) => save.Cleared.Contains(node.Id);

    /// <summary>Marks <paramref name="node"/> cleared and opens the floor below it. Clearing any node on a floor opens the next.</summary>
    public static void Clear(DelveSave save, DelveNode node)
    {
        if (!save.Cleared.Contains(node.Id))
        {
            save.Cleared.Add(node.Id);
            string kind = node.Kind.ToString();
            save.ClearedByKind[kind] = save.ClearedByKind.GetValueOrDefault(kind) + 1;
        }

        save.Deepest = Math.Max(save.Deepest, node.Depth + 1);
        if (node.IsBoss)
        {
            save.BossesSlain++;
        }
    }

    /// <summary>The chance an Armoury node's cache holds a piece of gear, and a Boss node's.</summary>
    public const float ArmouryGearChance = 0.35f;
    public const float BossGearChance = 0.5f;

    /// <summary>Whether this cache holds gear: a roll against <paramref name="reward"/>'s chance.</summary>
    public static bool RollsGear(DelveReward reward, Random random) => reward.GearChance > 0f && random.NextDouble() < reward.GearChance;

    /// <summary>The boss tier of a floor: 1 for depths 1-5, 2 for 6-10, and so on.</summary>
    public static int Tier(int depth) => (Math.Max(1, depth) - 1) / DelveMap.BossEvery + 1;

    /// <summary>What everything on floor <paramref name="depth"/> has its health multiplied by, on top of the run's own ramp.</summary>
    public static float HealthMultiplier(int depth) => 1f + 0.15f * (Math.Max(1, depth) - 1);

    /// <summary>What everything on floor <paramref name="depth"/> has its damage multiplied by, on top of the run's own ramp.</summary>
    public static float DamageMultiplier(int depth) => 1f + 0.06f * (Math.Max(1, depth) - 1);

    /// <summary>The level the player starts a Boss node's fight at (picking every level's upgrade first): 15 at depth 5, 2 more each boss floor after.</summary>
    public static int ArenaLevel(int depth) => 15 + 2 * (Tier(depth) - 1);

    /// <summary>The Hollow King Unbound's health on floor <paramref name="depth"/>.</summary>
    public static float ArenaBossHealth(int depth) => Combat.DelveBosses.HollowKingUnbound.MaxHealth * HealthMultiplier(depth);

    /// <summary>What clearing <paramref name="node"/> pays. Deeper pays more.</summary>
    public static DelveReward Reward(DelveNode node)
    {
        int depth = node.Depth;
        long silver = 80 + 30 * depth;
        return node.Kind switch
        {
            DelveNodeKind.Currency => new DelveReward(silver * 4, 0, 0, 0f, 0),
            DelveNodeKind.Armoury => new DelveReward(silver * 2, 0, 0, ArmouryGearChance, 0),
            DelveNodeKind.Knowledge => new DelveReward(silver, 500 + 150L * depth, 0, 0f, 0),
            DelveNodeKind.Relic => new DelveReward(silver, 0, 2, 0f, 0),
            _ => new DelveReward(silver * 3, 0, 1, BossGearChance, 1 + Tier(depth)),
        };
    }

    /// <summary>The silver a cache pays instead of gear when its roll comes up but every piece is owned already.</summary>
    public const long NoGearSilver = 400;
}
