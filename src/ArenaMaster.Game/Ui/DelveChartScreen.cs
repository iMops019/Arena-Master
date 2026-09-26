using System.Numerics;
using ArenaMaster.Game.Combat;
using ArenaMaster.Game.Delve;
using ArenaMaster.Game.Progression;
using ImGuiNET;

namespace ArenaMaster.Game.Ui;

/// <summary>
/// The Delve chart, at the departure gate: the floors going down from depth 1, each with its nodes, joined to the floor below; cleared nodes ticked off, floors not
/// yet open dark. Picking a node shows what it pays and how hard it is; from there the player sets out on it (through the loadout), or takes a classic 30-minute
/// run instead.
/// </summary>
internal sealed class DelveChartScreen : GameScreen
{
    public enum Choice
    {
        None,
        Close,
        Delve,
        Classic,
    }

    /// <summary>How many floors past the deepest open one are shown, dark.</summary>
    private const int FloorsAhead = 2;

    private DelveNode? _selected;
    private bool _scrollToDeepest;

    public new void Open()
    {
        base.Open();
        _selected = null;
        _scrollToDeepest = true;
    }

    public static Vector4 ColourOf(DelveNodeKind kind) => kind switch
    {
        DelveNodeKind.Currency => UiTheme.BrassHi,
        DelveNodeKind.Armoury => UiTheme.Unique,
        DelveNodeKind.Knowledge => UiTheme.Teal,
        DelveNodeKind.Relic => UiTheme.Rarity(Items.ItemRarity.Epic),
        _ => new Vector4(0.92f, 0.3f, 0.28f, 1f),
    };

    /// <summary>Draws the chart. Returns what the player chose, and for a Delve, which node.</summary>
    public (Choice Choice, DelveNode? Node) Draw(Profile profile)
    {
        if (!IsOpen)
        {
            return (Choice.None, null);
        }

        MarkDrawn();
        var save = profile.Delve;
        _selected ??= DefaultNode(save);

        float scale = UiTheme.Scale;
        UiTheme.BeginScreen("##delve", 0.86f, 0.88f);
        UiTheme.Header("Camp · Departure gate", "The Delve", $"Deepest: depth {save.Deepest}   ·   {save.Marks:N0} Delve Marks");

        float width = ImGui.GetContentRegionAvail().X;
        float buttonHeight = 44f * scale;
        float bodyHeight = ImGui.GetContentRegionAvail().Y - buttonHeight - 14f * scale;
        float chartWidth = width * 0.58f;

        ImGui.BeginChild("##chart", new Vector2(chartWidth, bodyHeight), ImGuiChildFlags.None, ImGuiWindowFlags.None);
        ImGui.SetWindowFontScale(1.25f);   // a child scales on top of its parent: larger than the screen's own text, the chart being the point of it
        DrawChart(save, chartWidth);
        ImGui.EndChild();

        ImGui.SameLine(0f, 18f * scale);
        var choice = Choice.None;
        ImGui.BeginChild("##details", new Vector2(width - chartWidth - 18f * scale, bodyHeight), ImGuiChildFlags.None, ImGuiWindowFlags.NoScrollbar);
        ImGui.SetWindowFontScale(1.25f);
        if (_selected is { } node && DrawDetails(save, node))
        {
            choice = Choice.Delve;
        }

        ImGui.EndChild();

        var start = ImGui.GetCursorScreenPos();
        float classicWidth = 260f * scale;
        float closeWidth = 150f * scale;
        if (UiTheme.Button("Classic run  ·  30 minutes", new Vector2(classicWidth, buttonHeight)))
        {
            choice = Choice.Classic;
        }

        UiTheme.Text(start + new Vector2(classicWidth + 16f * scale, buttonHeight * 0.3f), "The old way: survive 30:00, three Kings, no Delve reward.", UiTheme.Muted, 0.75f);
        ImGui.SetCursorScreenPos(start + new Vector2(width - closeWidth, 0f));
        if (UiTheme.Button("Close  [E]", new Vector2(closeWidth, buttonHeight)) || (choice == Choice.None && ClosedByKey(ImGuiKey.E)))
        {
            choice = Choice.Close;
        }

        UiTheme.EndScreen();

        if (choice != Choice.None)
        {
            Close();
        }

        return (choice, choice == Choice.Delve ? _selected : null);
    }

    /// <summary>The node picked when the chart opens: the first open one on the deepest floor that has one, or the very first node.</summary>
    private static DelveNode DefaultNode(DelveSave save)
    {
        for (int depth = save.Deepest; depth >= 1; depth--)
        {
            if (DelveMap.Floor(depth).FirstOrDefault(n => DelveRules.IsOpen(save, n)) is { } open)
            {
                return open;
            }
        }

        return DelveMap.Floor(1)[0];
    }

    private void DrawChart(DelveSave save, float width)
    {
        float scale = UiTheme.Scale;
        float font = ImGui.GetFontSize();
        float rowHeight = 92f * scale;
        float labelWidth = 150f * scale;
        float radius = 17f * scale;
        int last = save.Deepest + FloorsAhead;
        var draw = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        float nodesWidth = width - labelWidth - 30f * scale;

        Vector2 Centre(DelveNode node, int count) =>
            origin + new Vector2(labelWidth + nodesWidth * (node.Slot + 0.5f) / count, (node.Depth - 1) * rowHeight + rowHeight * 0.5f);

        // The paths first, under the nodes: every node to every node on the floor below.
        for (int depth = 1; depth < last; depth++)
        {
            var here = DelveMap.Floor(depth);
            var below = DelveMap.Floor(depth + 1);
            bool lit = depth + 1 <= save.Deepest;
            foreach (var a in here)
            {
                foreach (var b in below)
                {
                    draw.AddLine(Centre(a, here.Count), Centre(b, below.Count), UiTheme.U32(UiTheme.WithAlpha(lit ? UiTheme.Faint : UiTheme.Line, 0.6f)), 1.2f * scale);
                }
            }
        }

        for (int depth = 1; depth <= last; depth++)
        {
            var floor = DelveMap.Floor(depth);
            bool open = depth <= save.Deepest;
            float rowTop = origin.Y + (depth - 1) * rowHeight;
            var band = DelveBands.For(depth);
            if (band.FromDepth == depth && depth > 1)
            {
                draw.AddLine(new Vector2(origin.X, rowTop), new Vector2(origin.X + width - 20f * scale, rowTop), UiTheme.U32(UiTheme.WithAlpha(UiTheme.Brass, 0.35f)), 1f);
            }

            UiTheme.Text(new Vector2(origin.X + 4f * scale, rowTop + rowHeight * 0.5f - font * 0.9f), $"DEPTH {depth}", open ? UiTheme.Ink : UiTheme.Faint, 0.95f);
            UiTheme.Text(new Vector2(origin.X + 4f * scale, rowTop + rowHeight * 0.5f + font * 0.15f), band.Name, open ? UiTheme.Muted : UiTheme.Locked, 0.68f);

            foreach (var node in floor)
            {
                var centre = Centre(node, floor.Count);
                bool cleared = DelveRules.IsCleared(save, node);
                bool selected = _selected == node;
                var colour = ColourOf(node.Kind);
                var fill = !open ? UiTheme.Locked : cleared ? UiTheme.WithAlpha(colour, 0.25f) : UiTheme.WithAlpha(colour, 0.85f);
                float r = node.IsBoss ? radius * 1.25f : radius;

                if (selected)
                {
                    draw.AddCircle(centre, r + 6f * scale, UiTheme.U32(UiTheme.Ink), 32, 2f * scale);
                }

                if (node.IsBoss)
                {
                    // A diamond for a boss.
                    draw.AddQuadFilled(centre + new Vector2(0f, -r), centre + new Vector2(r, 0f), centre + new Vector2(0f, r), centre + new Vector2(-r, 0f), UiTheme.U32(fill));
                    draw.AddQuad(centre + new Vector2(0f, -r), centre + new Vector2(r, 0f), centre + new Vector2(0f, r), centre + new Vector2(-r, 0f),
                        UiTheme.U32(open ? colour : UiTheme.Faint), 2f * scale);
                }
                else
                {
                    draw.AddCircleFilled(centre, r, UiTheme.U32(fill), 32);
                    draw.AddCircle(centre, r, UiTheme.U32(open ? colour : UiTheme.Faint), 32, 2f * scale);
                }

                string glyph = !open ? "?" : cleared ? "x" : node.Kind switch
                {
                    DelveNodeKind.Currency => "$",
                    DelveNodeKind.Armoury => "A",
                    DelveNodeKind.Knowledge => "K",
                    DelveNodeKind.Relic => "R",
                    _ => "B",
                };
                var glyphColour = !open ? UiTheme.Faint : cleared ? UiTheme.WithAlpha(colour, 0.9f) : new Vector4(0.05f, 0.06f, 0.08f, 1f);
                UiTheme.Text(centre - new Vector2(UiTheme.TextWidth(glyph, 0.95f) * 0.5f, font * 0.48f), glyph, glyphColour, 0.95f);

                ImGui.SetCursorScreenPos(centre - new Vector2(r, r));
                ImGui.PushID(node.Id);
                if (ImGui.InvisibleButton("##node", new Vector2(r * 2f, r * 2f)))
                {
                    _selected = node;
                }

                ImGui.PopID();
            }
        }

        ImGui.SetCursorScreenPos(origin + new Vector2(0f, last * rowHeight));
        ImGui.Dummy(new Vector2(1f, 10f * scale));
        if (_scrollToDeepest)
        {
            _scrollToDeepest = false;
            ImGui.SetScrollY(MathF.Max(0f, (save.Deepest - 2) * rowHeight));
        }
    }

    /// <summary>What the picked node is, pays and asks. True when the player sets out on it.</summary>
    private static bool DrawDetails(DelveSave save, DelveNode node)
    {
        float scale = UiTheme.Scale;
        float font = ImGui.GetFontSize();
        var origin = ImGui.GetCursorScreenPos();
        float width = ImGui.GetContentRegionAvail().X;
        float height = ImGui.GetContentRegionAvail().Y;
        UiTheme.Card(origin, origin + new Vector2(width, height), UiTheme.PanelRaised, UiTheme.WithAlpha(ColourOf(node.Kind), 0.6f));
        float pad = 18f * scale;
        float y = origin.Y + pad;
        float x = origin.X + pad;
        float inner = width - 2f * pad;
        var band = DelveBands.For(node.Depth);

        UiTheme.Text(new Vector2(x, y), $"DEPTH {node.Depth}  ·  {band.Name.ToUpperInvariant()}", UiTheme.Muted, 0.7f);
        y += font * 1.1f;
        UiTheme.Text(new Vector2(x, y), node.Name, ColourOf(node.Kind), 1.5f);
        y += font * 2.1f;

        bool open = DelveRules.IsOpen(save, node);
        bool cleared = DelveRules.IsCleared(save, node);
        string status = cleared ? "Cleared." : open ? "Open." : $"Locked: clear a node on depth {node.Depth - 1} to open this floor.";
        UiTheme.Text(new Vector2(x, y), status, cleared ? UiTheme.Muted : open ? UiTheme.Teal : UiTheme.Warn, 0.85f, inner);
        y += font * 1.6f;

        UiTheme.Text(new Vector2(x, y), "REWARD", UiTheme.Muted, 0.65f);
        y += font * 0.95f;
        UiTheme.Text(new Vector2(x, y), DelveMap.RewardOf(node.Kind), UiTheme.Ink, 0.85f, inner);
        y += font * 2.2f;
        var reward = DelveRules.Reward(node);
        var lines = new List<string> { $"{reward.Silver:N0} silver in the cache" };
        if (reward.TreeExperience > 0)
        {
            lines.Add($"{reward.TreeExperience:N0} passive tree experience");
        }

        if (reward.Items > 0)
        {
            lines.Add(reward.Items == 1 ? "1 item" : $"{reward.Items} items");
        }

        if (reward.GearChance > 0f)
        {
            lines.Add($"{reward.GearChance * 100f:0}% chance of a piece of gear");
        }

        if (reward.Marks > 0)
        {
            lines.Add($"{reward.Marks} Delve Marks");
        }

        foreach (string line in lines)
        {
            UiTheme.Text(new Vector2(x, y), "·  " + line, UiTheme.BrassHi, 0.85f);
            y += font * 1.1f;
        }

        y += font * 0.6f;
        UiTheme.Text(new Vector2(x, y), "THE FLOOR", UiTheme.Muted, 0.65f);
        y += font * 0.95f;
        float health = (DelveRules.HealthMultiplier(node.Depth) - 1f) * 100f;
        float damage = (DelveRules.DamageMultiplier(node.Depth) - 1f) * 100f;
        string toughness = node.Depth == 1 ? "Enemies at their plain strength." : $"Enemies +{health:0}% health, +{damage:0}% damage.";
        UiTheme.Text(new Vector2(x, y), toughness, UiTheme.Ink, 0.8f, inner);
        y += font * 1.1f;
        string how = node.IsBoss
            ? $"The arena: the Hollow King Unbound alone, {DelveRules.ArenaBossHealth(node.Depth):N0} health, in three stages. You start at level {DelveRules.ArenaLevel(node.Depth)} and pick your upgrades first."
            : $"About 10 minutes: a Brute at 5:00, the Hollow King and two Brutes at 10:00, three more Brutes when he is at half. The swarm grows as a classic run does to {DelveDirector.PeakMinutes(node.Depth):0}:00. Slay the King and open his cache.";
        UiTheme.Text(new Vector2(x, y), how, UiTheme.Muted, 0.78f, inner);

        float buttonHeight = 46f * scale;
        ImGui.SetCursorScreenPos(new Vector2(x, origin.Y + height - pad - buttonHeight));
        string label = cleared ? "Already cleared" : open ? "Choose loadout and descend" : "Locked";
        return UiTheme.Button(label, new Vector2(inner, buttonHeight), primary: open, enabled: open);
    }
}
