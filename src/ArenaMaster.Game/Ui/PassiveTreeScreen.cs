using System.Numerics;
using ArenaMaster.Game.Progression;
using ImGuiNET;

namespace ArenaMaster.Game.Ui;

/// <summary>
/// The passive tree at camp: the whole tree drawn as a graph growing upward (tiers open with the tree's level, lit lines along the ranks taken), and a panel for the
/// node picked - what it does, its ranks, take and refund - with the build's totals under it. Respec is free. Click a node to pick it; double-click takes a rank.
/// </summary>
internal sealed class PassiveTreeScreen : GameScreen
{
    // The tree's layout space: nodes carry an X in it, tiers a Y (see TierY), and it is fitted into the canvas.
    private const float LayoutWidth = 940f;
    private const float LayoutTop = 70f;
    private const float LayoutBottom = 990f;

    private static readonly float[] TierY = { 935f, 810f, 685f, 560f, 435f, 310f, 185f };

    private string? _selected;
    private TreeDefinition? _shown;

    /// <summary>Set whenever a rank is taken or refunded, so the caller knows to save.</summary>
    public bool Changed { get; set; }

    /// <summary>Draws the tree. Returns true when the player closes it.</summary>
    public bool Draw(TreeProgress progress, string className)
    {
        if (!IsOpen)
        {
            return false;
        }

        MarkDrawn();
        var tree = progress.Tree;
        if (_shown != tree)
        {
            _shown = tree;   // a different class's tree: its own first node, not the last one's pick
            _selected = tree.Nodes[0].Id;
        }

        float scale = UiTheme.Scale;

        UiTheme.BeginScreen("##passivetree", 0.92f, 0.9f);
        string points = progress.FreePoints == 1 ? "1 point to spend" : $"{progress.FreePoints} points to spend";
        UiTheme.Header($"Camp · {className} · Passive tree", tree.Name, $"Tree level {progress.Level}  ·  {points}");
        DrawExperience(progress);

        var origin = ImGui.GetCursorScreenPos();
        var avail = ImGui.GetContentRegionAvail();
        float sideWidth = 320f * scale;
        float gap = 18f * scale;
        var canvasMin = origin;
        var canvasMax = origin + new Vector2(avail.X - sideWidth - gap, avail.Y);

        DrawCanvas(progress, canvasMin, canvasMax);
        bool close = DrawSide(progress, new Vector2(canvasMax.X + gap, origin.Y), new Vector2(origin.X + avail.X, origin.Y + avail.Y));

        UiTheme.EndScreen();
        if (close || ClosedByKey(ImGuiKey.E))
        {
            Close();
            return true;
        }

        return false;
    }

    private static void DrawExperience(TreeProgress progress)
    {
        float scale = UiTheme.Scale;
        var at = ImGui.GetCursorScreenPos();
        float width = ImGui.GetContentRegionAvail().X;
        var draw = ImGui.GetWindowDrawList();
        bool capped = progress.Level >= TreeProgress.MaxLevel;
        float fraction = capped ? 1f : (float)progress.IntoLevel / TreeProgress.RequiredFor(progress.Level);
        draw.AddRectFilled(at, at + new Vector2(width, 6f * scale), UiTheme.U32(UiTheme.Line), 3f * scale);
        draw.AddRectFilled(at, at + new Vector2(width * fraction, 6f * scale), UiTheme.U32(UiTheme.Teal), 3f * scale);
        string text = capped ? "The tree is at its cap." : $"{progress.IntoLevel:N0} / {TreeProgress.RequiredFor(progress.Level):N0} experience to level {progress.Level + 1}. The active tree earns experience in every run.";
        UiTheme.Text(at + new Vector2(0f, 10f * scale), text, UiTheme.Muted, 0.72f);
        ImGui.Dummy(new Vector2(width, 34f * scale));
    }

    private void DrawCanvas(TreeProgress progress, Vector2 min, Vector2 max)
    {
        var tree = progress.Tree;
        float scale = UiTheme.Scale;
        var draw = ImGui.GetWindowDrawList();
        draw.AddRectFilled(min, max, UiTheme.U32(new Vector4(0.05f, 0.075f, 0.09f, 1f)), 10f * scale);

        // Fit the layout into the canvas, keeping its proportions.
        float fit = MathF.Min((max.X - min.X) / LayoutWidth, (max.Y - min.Y) / (LayoutBottom - LayoutTop));
        var offset = new Vector2(min.X + ((max.X - min.X) - LayoutWidth * fit) * 0.5f, min.Y + ((max.Y - min.Y) - (LayoutBottom - LayoutTop) * fit) * 0.5f);
        Vector2 At(float x, float y) => offset + new Vector2(x, y - LayoutTop) * fit;
        Vector2 NodeAt(TreeNode n) => At(n.X, TierY[n.Tier - 1]);

        for (int t = 0; t < TierY.Length; t++)
        {
            bool open = progress.Level >= tree.TierLevels[t];
            var left = At(90f, TierY[t]);
            draw.AddLine(left, At(LayoutWidth - 10f, TierY[t]), UiTheme.U32(new Vector4(0.1f, 0.14f, 0.17f, 1f)), 1f);
            UiTheme.Text(At(10f, TierY[t] - 18f), $"TIER {t + 1}", open ? UiTheme.Muted : UiTheme.Faint, 0.6f);
            UiTheme.Text(At(10f, TierY[t] + 2f), $"Lv {tree.TierLevels[t]}", open ? UiTheme.Muted : UiTheme.Faint, 0.6f);
        }

        foreach (var (name, x) in tree.Lanes)
        {
            string lane = name.ToUpperInvariant();
            var at = At(x, LayoutTop + 10f);
            UiTheme.Text(at - new Vector2(UiTheme.TextWidth(lane, 0.7f) * 0.5f, 0f), lane, UiTheme.Muted, 0.7f);
        }

        foreach (var node in tree.Nodes)
        {
            foreach (var parentId in node.Parents)
            {
                var parent = tree.Node(parentId);
                bool from = progress.RankOf(parentId) > 0, to = progress.RankOf(node.Id) > 0;
                var color = from && to ? UiTheme.Brass : from ? new Vector4(0.18f, 0.35f, 0.33f, 1f) : UiTheme.Line;
                draw.AddLine(NodeAt(parent), NodeAt(node), UiTheme.U32(color), (from && to ? 3f : 2f) * scale);
            }
        }

        float radius = 22f * fit;
        var mouse = ImGui.GetMousePos();
        bool canvasHovered = ImGui.IsWindowHovered() && mouse.X >= min.X && mouse.X <= max.X && mouse.Y >= min.Y && mouse.Y <= max.Y;
        TreeNode? hovered = null;

        foreach (var node in tree.Nodes)
        {
            var centre = NodeAt(node);
            int ranks = progress.RankOf(node.Id);
            string state = ranks >= node.MaxRanks ? "maxed" : ranks > 0 ? "taken" : progress.WhyNotTake(node) is null ? "open" : "closed";
            var (fill, stroke) = state switch
            {
                "maxed" => (UiTheme.Brass, UiTheme.BrassHi),
                "taken" => (UiTheme.BrassDeep, UiTheme.BrassHi),
                "open" => (UiTheme.TealDeep, UiTheme.Teal),
                _ => (new Vector4(0.05f, 0.08f, 0.09f, 1f), node.Playable ? UiTheme.Locked : UiTheme.WithAlpha(UiTheme.Locked, 0.6f)),
            };

            bool isHovered = canvasHovered && Vector2.Distance(mouse, centre) <= radius * 1.25f;
            if (isHovered)
            {
                hovered = node;
            }

            float thickness = (isHovered ? 3f : 2f) * scale;
            if (node.Major)
            {
                float d = radius * 1.3f;
                Vector2 N = centre + new Vector2(0f, -d), E = centre + new Vector2(d, 0f), S = centre + new Vector2(0f, d), W = centre + new Vector2(-d, 0f);
                draw.AddQuadFilled(N, E, S, W, UiTheme.U32(fill));
                draw.AddQuad(N, E, S, W, UiTheme.U32(stroke), thickness);
                if (node.Id == _selected)
                {
                    float o = d + 7f * fit;
                    draw.AddQuad(centre + new Vector2(0f, -o), centre + new Vector2(o, 0f), centre + new Vector2(0f, o), centre + new Vector2(-o, 0f), UiTheme.U32(UiTheme.Ink), 1.5f * scale);
                }
            }
            else
            {
                draw.AddCircleFilled(centre, radius, UiTheme.U32(fill), 32);
                draw.AddCircle(centre, radius, UiTheme.U32(stroke), 32, thickness);
                if (node.Id == _selected)
                {
                    draw.AddCircle(centre, radius + 6f * fit, UiTheme.U32(UiTheme.Ink), 32, 1.5f * scale);
                }
            }

            string initials = string.Concat(node.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(w => char.ToUpperInvariant(w[0])));
            var initialsColor = state == "maxed" ? new Vector4(0.16f, 0.1f, 0.02f, 1f) : state == "closed" ? UiTheme.Faint : UiTheme.Ink;
            float initialsSize = 0.75f * MathF.Max(0.8f, fit * 1.4f);
            UiTheme.Text(centre - new Vector2(UiTheme.TextWidth(initials, initialsSize) * 0.5f, ImGui.GetFontSize() * initialsSize * 0.5f), initials, initialsColor, initialsSize);

            string label = !node.Playable && ranks == 0 ? "SOON"
                : state == "closed" && progress.Level < progress.Tree.LevelFor(node.Tier) ? $"Lv {progress.Tree.LevelFor(node.Tier)}"
                : node.Major ? (ranks > 0 ? "TAKEN" : "MAJOR")
                : $"{ranks}/{node.MaxRanks}";
            var labelColor = state is "taken" or "maxed" ? UiTheme.BrassHi : state == "open" ? UiTheme.Teal : UiTheme.Faint;
            float labelY = centre.Y + (node.Major ? radius * 1.3f : radius) + 3f * scale;
            UiTheme.Text(new Vector2(centre.X - UiTheme.TextWidth(label, 0.6f) * 0.5f, labelY), label, labelColor, 0.6f);
        }

        if (hovered is not null)
        {
            ImGui.SetTooltip(hovered.Name);
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                _selected = hovered.Id;
            }

            if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left) && progress.Take(hovered))
            {
                Changed = true;
            }
        }
    }

    /// <summary>The picked node's panel, then the build's totals. Returns true if Close was clicked.</summary>
    private bool DrawSide(TreeProgress progress, Vector2 min, Vector2 max)
    {
        float scale = UiTheme.Scale;
        float pad = 16f * scale;
        float width = max.X - min.X - 2f * pad;
        var node = progress.Tree.Node(_selected!);
        int ranks = progress.RankOf(node.Id);
        float font = ImGui.GetFontSize();
        var y = min.Y + pad;

        UiTheme.Card(min, max, UiTheme.PanelRaised, UiTheme.Line);

        UiTheme.Text(new Vector2(min.X + pad, y), $"TIER {node.Tier} · UNLOCKS AT LEVEL {progress.Tree.LevelFor(node.Tier)}", UiTheme.Muted, 0.6f);
        y += font * 0.9f;
        UiTheme.Text(new Vector2(min.X + pad, y), node.Name, UiTheme.Ink, 1.35f, width);
        y += font * 1.7f;
        string kind = node.Major ? "MAJOR NODE" : $"MINOR · MAX {node.MaxRanks}";
        UiTheme.Text(new Vector2(min.X + pad, y), kind, node.Major ? UiTheme.BrassHi : UiTheme.Muted, 0.62f);
        UiTheme.Text(new Vector2(min.X + pad + UiTheme.TextWidth(kind, 0.62f) + 12f * scale, y), node.Lane.ToUpperInvariant(), UiTheme.Teal, 0.62f);
        y += font * 1.1f;

        UiTheme.Text(new Vector2(min.X + pad, y), node.Describe(ranks), UiTheme.Ink, 0.85f, width);
        y += font * 2.7f;

        // Rank pips
        var draw = ImGui.GetWindowDrawList();
        float pipGap = 5f * scale;
        float pipWidth = (width - pipGap * (node.MaxRanks - 1)) / node.MaxRanks;
        for (int i = 0; i < node.MaxRanks; i++)
        {
            var p = new Vector2(min.X + pad + i * (pipWidth + pipGap), y);
            draw.AddRectFilled(p, p + new Vector2(pipWidth, 6f * scale), UiTheme.U32(i < ranks ? UiTheme.Brass : UiTheme.Line), 2f * scale);
        }

        y += 14f * scale;
        string next = !node.Playable ? "Coming soon: shown so you can plan around it."
            : node.Major ? "One rank. Changes how you play."
            : ranks == 0 ? $"Per rank: {node.Describe(1)}"
            : ranks < node.MaxRanks ? $"Next rank: {node.Describe(ranks + 1)}"
            : "Fully ranked.";
        UiTheme.Text(new Vector2(min.X + pad, y), next, UiTheme.Muted, 0.72f, width);
        y += font * 1.9f;

        float buttonHeight = 38f * scale;
        ImGui.SetCursorScreenPos(new Vector2(min.X + pad, y));
        string? whyNotRefund = progress.WhyNotRefund(node);
        string? whyNotTake = progress.WhyNotTake(node);
        if (UiTheme.Button("- Refund", new Vector2(width * 0.38f, buttonHeight), enabled: whyNotRefund is null) && progress.Refund(node))
        {
            Changed = true;
        }

        ImGui.SameLine(0f, width * 0.04f);
        if (UiTheme.Button("+ Take rank", new Vector2(width * 0.58f, buttonHeight), primary: true, enabled: whyNotTake is null) && progress.Take(node))
        {
            Changed = true;
        }

        y += buttonHeight + 6f * scale;
        if (whyNotTake is not null && ranks < node.MaxRanks)
        {
            UiTheme.Text(new Vector2(min.X + pad, y), whyNotTake, UiTheme.Warn, 0.68f, width);
        }

        y += font * 1.5f;
        draw.AddLine(new Vector2(min.X + pad, y), new Vector2(max.X - pad, y), UiTheme.U32(UiTheme.Line), 1f);
        y += 10f * scale;

        UiTheme.Text(new Vector2(min.X + pad, y), "BUILD TOTALS", UiTheme.Muted, 0.6f);
        y += font * 0.95f;
        foreach (var (stat, value) in Totals(progress))
        {
            string amount = (value > 0 ? "+" : "") + MathF.Round(value * 10f) / 10f + (progress.Tree.FlatStats.Contains(stat) ? "" : "%");
            UiTheme.Text(new Vector2(min.X + pad, y), stat, UiTheme.Ink, 0.7f);
            UiTheme.Text(new Vector2(max.X - pad - UiTheme.TextWidth(amount, 0.7f), y), amount, UiTheme.BrassHi, 0.7f);
            y += font * 0.85f;
        }

        var majors = progress.Tree.Nodes.Where(n => n.Major && progress.RankOf(n.Id) > 0).Select(n => n.Name).ToList();
        if (majors.Count > 0)
        {
            y += 4f * scale;
            UiTheme.Text(new Vector2(min.X + pad, y), "Majors: " + string.Join(", ", majors), UiTheme.BrassHi, 0.7f, width);
        }

        ImGui.SetCursorScreenPos(new Vector2(min.X + pad, max.Y - pad - buttonHeight));
        if (UiTheme.Button("Refund all (free)", new Vector2(width * 0.55f, buttonHeight), enabled: progress.Spent > 0))
        {
            progress.RefundAll();
            Changed = true;
        }

        ImGui.SameLine(0f, width * 0.04f);
        return UiTheme.Button("Close  [E]", new Vector2(width * 0.41f, buttonHeight));
    }

    private static IEnumerable<(string Stat, float Value)> Totals(TreeProgress progress)
    {
        var sums = new Dictionary<string, float>();
        foreach (var node in progress.Tree.Nodes)
        {
            int ranks = progress.RankOf(node.Id);
            foreach (var (stat, perRank) in node.Stats)
            {
                if (ranks > 0)
                {
                    sums[stat] = sums.GetValueOrDefault(stat) + perRank * ranks;
                }
            }
        }

        return sums.Select(kv => (kv.Key, kv.Value));
    }
}
