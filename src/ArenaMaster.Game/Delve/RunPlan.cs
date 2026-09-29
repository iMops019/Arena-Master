namespace ArenaMaster.Game.Delve;

/// <summary>What kind of run the player set out on.</summary>
internal enum RunKind
{
    /// <summary>The classic run: survive 30:00 in the middle of the map, three Kings on the way.</summary>
    Classic,

    /// <summary>A Delve node: about 10 minutes, one King, his cache the prize.</summary>
    Delve,

    /// <summary>A boss hunt: one boss (<see cref="RunPlan.Boss"/>), alone, in the arena.</summary>
    Arena,
}

/// <summary>The run the player set out on: its kind, and for a Delve node, which node; for a boss hunt, which boss.</summary>
internal sealed record RunPlan(RunKind Kind, DelveNode? Node, HuntBoss? Boss = null)
{
    public static readonly RunPlan Classic = new(RunKind.Classic, null);

    public static RunPlan For(DelveNode node) => new(RunKind.Delve, node);

    public static RunPlan Hunt(HuntBoss boss) => new(RunKind.Arena, null, boss);

    /// <summary>The boss of a hunt (the Hollow King Unbound if a hunt somehow has none).</summary>
    public HuntBoss HuntBoss => Boss ?? BossHunt.HollowKing;

    /// <summary>The floor's depth, 0 for a classic run or the boss hunt.</summary>
    public int Depth => Node?.Depth ?? 0;

    /// <summary>A run that ends with a Delve cache: a Delve node, or the boss hunt.</summary>
    public bool IsDelve => Kind != RunKind.Classic;

    /// <summary>What the fade into the run shows.</summary>
    public string Title => Kind switch
    {
        RunKind.Arena => $"The Arena  ·  {HuntBoss.Name}",
        _ when Node is { } node => $"Depth {node.Depth}  ·  {DelveBands.For(node.Depth).Name}",
        _ => "Into the Dark",   // down the stairs, into the cave
    };

    /// <summary>Where the loadout screen says the run is going.</summary>
    public string Destination => Kind switch
    {
        RunKind.Arena => $"The boss hunt: {HuntBoss.Name}",
        _ when Node is { } node => $"Depth {node.Depth} {node.Name}",
        _ => "A classic run",
    };
}
