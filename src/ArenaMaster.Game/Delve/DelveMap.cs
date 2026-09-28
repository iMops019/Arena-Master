using ArenaMaster.Game.Items;
using ArenaMaster.Game.Progression;

namespace ArenaMaster.Game.Delve;

/// <summary>What a Delve node pays when it is cleared.</summary>
internal enum DelveNodeKind
{
    /// <summary>A heavy purse of silver.</summary>
    Currency,

    /// <summary>Experience for the active passive tree.</summary>
    Knowledge,

    /// <summary>An item at an elite chest's odds (rare or better).</summary>
    Relic,

    /// <summary>No King: hold out for 10 minutes and the cache appears. A modest purse and some tree experience. Most of a floor's nodes are these.</summary>
    Descent,
}

/// <summary>
/// One node on the Delve chart: a run to take on floor <paramref name="Depth"/>, in column <paramref name="Slot"/> (left to right on the chart), paying out its
/// <paramref name="Kind"/> when cleared.
/// </summary>
internal sealed record DelveNode(int Depth, int Slot, DelveNodeKind Kind)
{
    public string Id => $"{Depth}-{Slot}";

    /// <summary>Whether its run ends with the Hollow King at 10:00 (every node but a Descent).</summary>
    public bool HasKing => Kind != DelveNodeKind.Descent;

    public string Name => DelveMap.NameOf(Kind);
}

/// <summary>
/// The Delve's chart: every floor down from depth 1, each with <see cref="NodesPerFloor"/> nodes - some Descents (no King) and some King nodes of different kinds.
/// A floor is the same every time it is asked for (seeded by its depth), so the chart doesn't reshuffle between visits. The Delve is for going down; gear comes
/// from the boss hunt (<see cref="BossHunt"/>), a fight of its own. Pure.
/// </summary>
internal static class DelveMap
{
    public const int NodesPerFloor = 5;

    /// <summary>How many of a floor's nodes have a King: this many or one more, the rest Descents.</summary>
    public const int FewestKingNodes = 2;

    /// <summary>The kinds of node with a King at the end. A floor never has two of the same.</summary>
    private static readonly DelveNodeKind[] KingKinds = { DelveNodeKind.Currency, DelveNodeKind.Knowledge, DelveNodeKind.Relic };

    /// <summary>
    /// The nodes of floor <paramref name="depth"/> (1 and down): <see cref="NodesPerFloor"/> run nodes, 2 or 3 with a King (of different kinds) and the rest
    /// Descents, mixed along the row. Picked the same way every time for the same depth.
    /// </summary>
    public static IReadOnlyList<DelveNode> Floor(int depth)
    {
        var random = new Random(depth * 7919 + 17);
        int kings = FewestKingNodes + random.Next(2);
        var kinds = KingKinds.OrderBy(_ => random.Next()).Take(kings)
            .Concat(Enumerable.Repeat(DelveNodeKind.Descent, NodesPerFloor))
            .Take(NodesPerFloor)
            .OrderBy(_ => random.Next())
            .ToList();
        return kinds.Select((kind, slot) => new DelveNode(depth, slot, kind)).ToList();
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
        DelveNodeKind.Knowledge => "Knowledge Delve",
        DelveNodeKind.Relic => "Relic Delve",
        _ => "Descent",
    };

    public static string RewardOf(DelveNodeKind kind) => kind switch
    {
        DelveNodeKind.Currency => "A heavy purse of silver in the cache.",
        DelveNodeKind.Knowledge => "A large sum of experience for your active passive tree.",
        DelveNodeKind.Relic => "An item, rare or better.",
        _ => "No King: hold out for 10 minutes and the cache appears. A modest purse of silver and some tree experience.",
    };
}

/// <summary>Where the player has got to in the Delve, saved in the <see cref="Profile"/>.</summary>
internal sealed class DelveSave
{
    /// <summary>The deepest floor open: every floor from 1 down to this one can be run.</summary>
    public int Deepest { get; set; } = 1;

    /// <summary>The ids of the nodes cleared. A cleared node is done; the floor's others stay open.</summary>
    public List<string> Cleared { get; set; } = new();

    /// <summary>Delve Marks: the currency only the boss hunt gives.</summary>
    public long Marks { get; set; }

    /// <summary>How many times the Hollow King Unbound has been slain (on a boss hunt; before those, on the old Boss nodes).</summary>
    public int BossesSlain { get; set; }

    /// <summary>How many times the Marauder Unbound has been slain.</summary>
    public int MaraudersSlain { get; set; }

    /// <summary>Node kind -> how many of that kind have been cleared.</summary>
    public Dictionary<string, int> ClearedByKind { get; set; } = new();
}

/// <summary>What clearing a node pays: <paramref name="Items"/> items rolled at <paramref name="ItemOdds"/>.</summary>
internal sealed record DelveReward(long Silver, long TreeExperience, int Items, RarityWeights ItemOdds = default);

/// <summary>The Delve's rules: which floors and nodes are open, what clearing one does, how hard a floor is and what it pays. Pure.</summary>
internal static class DelveRules
{
    /// <summary>Whether <paramref name="node"/> can be run: its floor is open and it hasn't been cleared. A cleared node is locked for good: no farming it.</summary>
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
    }

    /// <summary>
    /// What everything on floor <paramref name="depth"/> has its health multiplied by, on top of the run's own ramp: +8% a floor (+15% until 2026-09-27, when the
    /// user found depth 8 and past a wall).
    /// </summary>
    public static float HealthMultiplier(int depth) => 1f + 0.08f * (Math.Max(1, depth) - 1);

    /// <summary>What everything on floor <paramref name="depth"/> has its damage multiplied by, on top of the run's own ramp: +4% a floor (was +6%).</summary>
    public static float DamageMultiplier(int depth) => 1f + 0.04f * (Math.Max(1, depth) - 1);

    /// <summary>What clearing <paramref name="node"/> pays. Deeper pays more.</summary>
    public static DelveReward Reward(DelveNode node)
    {
        int depth = node.Depth;
        long silver = 80 + 30 * depth;
        return node.Kind switch
        {
            DelveNodeKind.Currency => new DelveReward(silver * 4, 0, 0),
            DelveNodeKind.Knowledge => new DelveReward(silver, 1000 + 250L * depth, 0),
            DelveNodeKind.Relic => new DelveReward(silver, 0, 1, RarityWeights.Elite),
            _ => new DelveReward(silver, 400 + 100L * depth, 0),
        };
    }
}

/// <summary>
/// One boss of the boss hunt, and what a fight with him is: the enemy, how much tougher than his base numbers he is on a hunt, the level the player starts at,
/// and what his cache pays (a chance of gear and of an item, silver and Delve Marks). <paramref name="LookDepth"/> is the Delve band whose sky the arena has.
/// </summary>
internal sealed record HuntBoss(string Id, Combat.EnemyKind Kind, Combat.EnemyScaling Scaling, int StartLevel, float GearChance, float ItemChance, long Silver,
    long Marks, int LookDepth, string Fight)
{
    public string Name => Kind.Name;

    public float Health => Kind.MaxHealth * Scaling.Health;

    /// <summary>Whether this kill's cache holds gear: pure luck, every kill its own roll.</summary>
    public bool RollsGear(Random random) => random.NextDouble() < GearChance;

    public bool RollsItem(Random random) => random.NextDouble() < ItemChance;
}

/// <summary>
/// The boss hunts: a boss alone in his arena, a fight of its own, taken from the departure gate as often as the player likes - the way to hunt for gear (the
/// user's call, 2026-09-27: finding gear in Armoury and Boss nodes deep in the Delve was too rare and too hard). The Hollow King Unbound is open from the start;
/// the Marauder Unbound, a tier up (the user's, 2026-09-28), opens once the King has been slain. Each is always the same fight, for now. Pure.
/// </summary>
internal static class BossHunt
{
    /// <summary>
    /// The Hollow King Unbound: 45,500 health (the old depth-5 Boss node's was 41,600), hitting 30% harder, from level 16. A 25% chance of gear a kill (20% until
    /// the belt, amulet and ring slots came, 2026-09-27).
    /// </summary>
    public static readonly HuntBoss HollowKing = new("hollow_king", Combat.DelveBosses.HollowKingUnbound, new(1.75f, 1.3f, 1f), StartLevel: 16,
        GearChance: 0.25f, ItemChance: 0.10f, Silver: 150, Marks: 1, LookDepth: 5,
        "Him alone, in four stages: leaps, shockwaves, charges, fire from the sky, the Crown and the Line of Fire.");

    /// <summary>
    /// The Marauder Unbound, a tier up: 75,000 health (59,500 at first; the user raised it, 2026-09-28), hitting 35% harder, from level 21 (five more picks than the King; 18 at first). A 35% chance of gear a kill, normal rolls (the user's
    /// call), and more silver and marks.
    /// </summary>
    public static readonly HuntBoss Marauder = new("marauder", Combat.DelveBosses.MarauderUnbound, new(2.5f, 1.35f, 1f), StartLevel: 21,
        GearChance: 0.35f, ItemChance: 0.10f, Silver: 250, Marks: 2, LookDepth: 5,
        "A giant with a two-handed axe: swings, a quick cleave to dash out of, slams, a rift across the arena and a whirlwind. At 35% he roars (nothing hurts him) and fights on in a Frenzied Rage.");

    public static readonly IReadOnlyList<HuntBoss> All = new[] { HollowKing, Marauder };

    public static HuntBoss? Find(string id) => All.FirstOrDefault(b => b.Id == id);

    /// <summary>How many times <paramref name="boss"/> has been slain.</summary>
    public static int Slain(DelveSave save, HuntBoss boss) => boss == Marauder ? save.MaraudersSlain : save.BossesSlain;

    /// <summary>Counts a kill of <paramref name="boss"/>.</summary>
    public static void CountKill(DelveSave save, HuntBoss boss)
    {
        if (boss == Marauder)
        {
            save.MaraudersSlain++;
        }
        else
        {
            save.BossesSlain++;
        }
    }

    /// <summary>Whether <paramref name="boss"/> can be hunted: the King always, the Marauder once the King has been slain.</summary>
    public static bool IsOpen(DelveSave save, HuntBoss boss) => boss != Marauder || save.BossesSlain >= 1;

    /// <summary>Why <paramref name="boss"/> can't be hunted yet, or null if he can.</summary>
    public static string? WhyLocked(DelveSave save, HuntBoss boss) => IsOpen(save, boss) ? null : "Locked: slay the Hollow King Unbound first.";
}
