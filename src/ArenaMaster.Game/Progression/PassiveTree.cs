namespace ArenaMaster.Game.Progression;

/// <summary>
/// One node of a passive tree. <see cref="X"/> and the tier's height place it on the tree screen (a 940 x 1010 layout, growing upward). A node with a single rank and
/// <see cref="Major"/> set changes how the class plays; the rest are numbers. <see cref="Text"/> is its description, with <c>{Stat}</c> replaced by that stat's value
/// at the node's rank. A node not yet <see cref="Playable"/> shows on the tree but can't be taken: its effect isn't in the game yet.
/// </summary>
internal sealed record TreeNode(
    string Id,
    string Name,
    int Tier,
    float X,
    string Lane,
    int MaxRanks,
    string Text,
    IReadOnlyList<string> Parents,
    IReadOnlyDictionary<string, float> Stats,
    bool Major = false,
    bool Playable = true)
{
    /// <summary>The description at <paramref name="ranks"/> ranks (at least one, so an untaken node still reads with real numbers).</summary>
    public string Describe(int ranks)
    {
        int r = Math.Max(1, ranks);
        string text = Text;
        foreach (var (stat, perRank) in Stats)
        {
            float value = MathF.Round(perRank * r * 10f) / 10f;
            text = text.Replace("{" + stat + "}", value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        return text;
    }
}

/// <summary>
/// A class's passive tree: its nodes, the tree level each tier opens at, its lanes (named, with where their label sits across the tree screen's layout), and the
/// stats counted in plain numbers rather than percentages (for the build totals).
/// </summary>
internal sealed record TreeDefinition(
    string Id,
    string Name,
    IReadOnlyList<TreeNode> Nodes,
    IReadOnlyList<int> TierLevels,
    IReadOnlyList<(string Name, float X)> Lanes,
    IReadOnlySet<string> FlatStats)
{
    public TreeNode Node(string id) => Nodes.First(n => n.Id == id);

    /// <summary>The tree level tier <paramref name="tier"/> (from 1) opens at.</summary>
    public int LevelFor(int tier) => TierLevels[tier - 1];
}

/// <summary>
/// The rules of a passive tree, over one tree's saved progress: experience to levels (slow on purpose), one point per level up to <see cref="MaxLevel"/>, and which
/// ranks can be taken or refunded. A node can be taken once the tree has reached its tier's level and a parent of it has a rank (the first tier has no parents).
/// A rank can be refunded unless doing so would leave a taken node cut off from the first tier. Respec is free.
/// </summary>
internal sealed class TreeProgress
{
    public const int MaxLevel = 50;

    public TreeProgress(TreeDefinition tree, TreeSave save)
    {
        Tree = tree;
        Save = save;
    }

    public TreeDefinition Tree { get; }

    public TreeSave Save { get; }

    /// <summary>Experience to go from tree level <paramref name="level"/> to the next: 200 + 60 x level^1.6.</summary>
    public static long RequiredFor(int level) => (long)MathF.Round(200f + 60f * MathF.Pow(level, 1.6f));

    /// <summary>All the experience it takes to reach <paramref name="level"/> from level 1.</summary>
    public static long TotalFor(int level)
    {
        long total = 0;
        for (int n = 1; n < level; n++)
        {
            total += RequiredFor(n);
        }

        return total;
    }

    public int Level
    {
        get
        {
            int level = 1;
            long left = Save.Experience;
            while (level < MaxLevel && left >= RequiredFor(level))
            {
                left -= RequiredFor(level);
                level++;
            }

            return level;
        }
    }

    /// <summary>Experience gathered toward the next level (0 at the cap).</summary>
    public long IntoLevel => Level >= MaxLevel ? 0 : Save.Experience - TotalFor(Level);

    public int Points => Level;

    public int Spent => Save.Ranks.Values.Sum();

    public int FreePoints => Points - Spent;

    public int RankOf(string nodeId) => Save.Ranks.GetValueOrDefault(nodeId);

    /// <summary>Adds experience. Returns how many levels that gained.</summary>
    public int AddExperience(long amount)
    {
        int before = Level;
        Save.Experience += Math.Max(0, amount);
        return Level - before;
    }

    /// <summary>Why a rank of <paramref name="node"/> can't be taken right now, or null if it can.</summary>
    public string? WhyNotTake(TreeNode node)
    {
        int ranks = RankOf(node.Id);
        if (ranks >= node.MaxRanks)
        {
            return node.MaxRanks == 1 ? "Taken." : "Fully ranked.";
        }

        if (!node.Playable)
        {
            return "Coming soon: this node isn't in the game yet.";
        }

        if (Level < Tree.LevelFor(node.Tier))
        {
            return $"Unlocks at tree level {Tree.LevelFor(node.Tier)}.";
        }

        if (node.Parents.Count > 0 && !node.Parents.Any(p => RankOf(p) > 0))
        {
            return "Take a node below it that leads here first.";
        }

        return FreePoints <= 0 ? "No points left. Level the tree in a run." : null;
    }

    /// <summary>Why a rank of <paramref name="node"/> can't be refunded right now, or null if it can.</summary>
    public string? WhyNotRefund(TreeNode node)
    {
        int ranks = RankOf(node.Id);
        if (ranks == 0)
        {
            return "No ranks to refund.";
        }

        if (ranks == 1 && !StaysConnected(without: node.Id))
        {
            return "Nodes above depend on this one. Refund them first.";
        }

        return null;
    }

    public bool Take(TreeNode node)
    {
        if (WhyNotTake(node) is not null)
        {
            return false;
        }

        Save.Ranks[node.Id] = RankOf(node.Id) + 1;
        return true;
    }

    public bool Refund(TreeNode node)
    {
        if (WhyNotRefund(node) is not null)
        {
            return false;
        }

        int left = RankOf(node.Id) - 1;
        if (left > 0)
        {
            Save.Ranks[node.Id] = left;
        }
        else
        {
            Save.Ranks.Remove(node.Id);
        }

        return true;
    }

    /// <summary>Every point back, free.</summary>
    public void RefundAll() => Save.Ranks.Clear();

    /// <summary>Whether every node with ranks would still reach the first tier through nodes with ranks, if <paramref name="without"/> had none.</summary>
    private bool StaysConnected(string without)
    {
        bool Has(string id) => id != without && RankOf(id) > 0;
        var reached = new HashSet<string>();
        bool grew = true;
        while (grew)
        {
            grew = false;
            foreach (var node in Tree.Nodes)
            {
                if (Has(node.Id) && !reached.Contains(node.Id) && (node.Parents.Count == 0 || node.Parents.Any(reached.Contains)))
                {
                    reached.Add(node.Id);
                    grew = true;
                }
            }
        }

        return Tree.Nodes.All(n => !Has(n.Id) || reached.Contains(n.Id));
    }
}
