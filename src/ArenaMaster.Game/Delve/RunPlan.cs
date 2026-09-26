namespace ArenaMaster.Game.Delve;

/// <summary>What kind of run the player set out on.</summary>
internal enum RunKind
{
    /// <summary>The classic run: survive 30:00 in the middle of the map, three Kings on the way.</summary>
    Classic,

    /// <summary>A Delve node: about 10 minutes, one King, his cache the prize.</summary>
    Delve,

    /// <summary>A Boss node: the Hollow King Unbound, alone, in the arena.</summary>
    Arena,
}

/// <summary>The run the player set out on: its kind, and for a Delve or a Boss node, which node.</summary>
internal sealed record RunPlan(RunKind Kind, DelveNode? Node)
{
    public static readonly RunPlan Classic = new(RunKind.Classic, null);

    public static RunPlan For(DelveNode node) => new(node.IsBoss ? RunKind.Arena : RunKind.Delve, node);

    /// <summary>The floor's depth, 0 for a classic run.</summary>
    public int Depth => Node?.Depth ?? 0;

    public bool IsDelve => Node is not null;

    /// <summary>What the fade into the run shows.</summary>
    public string Title => Node is { } node ? $"Depth {node.Depth}  ·  {DelveBands.For(node.Depth).Name}" : "Into the Wilds";

    /// <summary>Where the loadout screen says the run is going.</summary>
    public string Destination => Node is { } node ? $"Depth {node.Depth} {node.Name}" : "A classic run";
}
