using System.Numerics;
using ArenaMaster.Game.Items;
using ArenaMaster.Game.Progression;
using ImGuiNET;

namespace ArenaMaster.Game.Ui;

internal enum RunEnding
{
    Won,
    Slain,
    ReturnedToCamp,

    /// <summary>A Delve node's boss slain and his cache opened.</summary>
    DelveCleared,
}

/// <summary>What a Delve run was, and for a cleared node, what its cache paid.</summary>
internal sealed record DelveOutcome(
    int Depth,
    string NodeName,
    bool Cleared,
    long CacheSilver = 0,
    long TreeExperience = 0,
    long Marks = 0,
    Gear.GearPiece? Gear = null,
    IReadOnlyList<RunItem>? Items = null);

/// <summary>How a run went, for the summary at its end.</summary>
internal sealed record RunSummary(
    RunEnding Ending,
    float Seconds,
    int Level,
    int Kills,
    IReadOnlyList<RunItem> Found,
    long TreeExperience,
    int TreeLevelsGained,
    int TreeLevel,
    string TreeName,
    long Silver,
    IReadOnlyList<Bounty> Bounties,
    DelveOutcome? Delve = null);

/// <summary>The screen at the end of a run - won, slain, or back to camp early: the run's numbers, the items found, what the tree earned. Then back to camp.</summary>
internal sealed class RunSummaryScreen : GameScreen
{
    private RunSummary? _summary;

    public void Open(RunSummary summary)
    {
        _summary = summary;
        Open();
    }

    /// <summary>Draws the summary. Returns true when the player heads back to camp.</summary>
    public bool Draw()
    {
        if (!IsOpen || _summary is not { } s)
        {
            return false;
        }

        MarkDrawn();
        float scale = UiTheme.Scale;
        UiTheme.BeginScreen("##summary", 0.5f, 0.8f);
        var origin = ImGui.GetCursorScreenPos();
        float width = ImGui.GetContentRegionAvail().X;
        float font = ImGui.GetFontSize();

        var (title, color) = s.Ending switch
        {
            RunEnding.Won => ("VICTORY", UiTheme.BrassHi),
            RunEnding.Slain => ("YOU WERE SLAIN", new Vector4(0.95f, 0.36f, 0.34f, 1f)),
            RunEnding.DelveCleared => ($"DEPTH {s.Delve?.Depth} CLEARED", UiTheme.Unique),
            _ => ("BACK TO CAMP", UiTheme.Teal),
        };
        UiTheme.Text(origin + new Vector2((width - UiTheme.TextWidth(title, 2f)) * 0.5f, 0f), title, color, 2f);
        var y = origin.Y + font * 2.8f;

        void Row(string label, string value, Vector4 valueColor)
        {
            UiTheme.Text(new Vector2(origin.X, y), label, UiTheme.Muted, 0.9f);
            UiTheme.Text(new Vector2(origin.X + width - UiTheme.TextWidth(value, 0.95f), y), value, valueColor, 0.95f);
            y += font * 1.25f;
        }

        if (s.Delve is { } delve)
        {
            string where = $"{delve.NodeName}  ·  depth {delve.Depth}";
            UiTheme.Text(new Vector2(origin.X + (width - UiTheme.TextWidth(where, 0.85f)) * 0.5f, y - font * 0.9f), where, UiTheme.Muted, 0.85f);
            y += font * 0.5f;
        }

        Row("Survived", $"{(int)s.Seconds / 60:00}:{(int)s.Seconds % 60:00}", UiTheme.Ink);
        Row("Level reached", s.Level.ToString(), UiTheme.Ink);
        Row("Kills", s.Kills.ToString("N0"), UiTheme.Ink);
        Row("Silver", $"+{s.Silver:N0}", UiTheme.BrassHi);
        Row($"{s.TreeName} experience", $"+{s.TreeExperience:N0}", UiTheme.Teal);
        if (s.TreeLevelsGained > 0)
        {
            Row($"{s.TreeName} level", $"{s.TreeLevel}  (+{s.TreeLevelsGained}, spend at the target)", UiTheme.BrassHi);
        }

        if (s.Delve is { Cleared: true } cache)
        {
            if (cache.CacheSilver > 0)
            {
                Row("Delve cache", $"+{cache.CacheSilver:N0} silver", UiTheme.BrassHi);
            }

            if (cache.TreeExperience > 0)
            {
                Row("Delve cache", $"+{cache.TreeExperience:N0} {s.TreeName} experience", UiTheme.Teal);
            }

            if (cache.Marks > 0)
            {
                Row("Delve Marks", $"+{cache.Marks:N0}", UiTheme.Unique);
            }

            if (cache.Gear is { } piece)
            {
                Row("Gear found", piece.Name, UiTheme.Unique);
            }
        }

        foreach (var bounty in s.Bounties)
        {
            string unlock = bounty.UnlocksItem is { } id ? $"  ·  unlocks {ItemCatalog.All.First(i => i.Id == id).Name}" : "";
            UiTheme.Text(new Vector2(origin.X, y), $"Bounty done: {bounty.Name}  (+{bounty.Silver:N0} silver{unlock})", UiTheme.BrassHi, 0.8f, width);
            y += font * 1.05f;
        }

        y += font * 0.4f;
        UiTheme.Text(new Vector2(origin.X, y), s.Found.Count == 0 ? "NO ITEMS FOUND" : $"ITEMS FOUND ({s.Found.Count}), NOW IN YOUR CHEST", UiTheme.Muted, 0.65f);
        y += font * 1f;
        foreach (var group in s.Found.GroupBy(i => i).OrderByDescending(g => g.Key.Rarity).Take(8))
        {
            string name = group.Count() > 1 ? $"{group.Key.Name}  x{group.Count()}" : group.Key.Name;
            UiTheme.Text(new Vector2(origin.X, y), name, UiTheme.Rarity(group.Key.Rarity), 0.85f);
            y += font * 1.05f;
        }

        float buttonHeight = 46f * scale;
        var bottom = ImGui.GetWindowPos().Y + ImGui.GetWindowSize().Y - 20f * scale - buttonHeight;
        ImGui.SetCursorScreenPos(new Vector2(origin.X, bottom));
        bool back = UiTheme.Button("Back to camp", new Vector2(width, buttonHeight), primary: true);
        UiTheme.EndScreen();

        if (back || ClosedByKey(ImGuiKey.E) || ClosedByKey(ImGuiKey.Enter))
        {
            _summary = null;
            Close();
            return true;
        }

        return false;
    }
}
