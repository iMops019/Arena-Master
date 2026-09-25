using System.Numerics;
using ArenaMaster.Game.Items;
using ImGuiNET;

namespace ArenaMaster.Game.Ui;

internal enum RunEnding
{
    Won,
    Slain,
    ReturnedToCamp,
}

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
    string TreeName);

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
        UiTheme.BeginScreen("##summary", 0.5f, 0.72f);
        var origin = ImGui.GetCursorScreenPos();
        float width = ImGui.GetContentRegionAvail().X;
        float font = ImGui.GetFontSize();

        var (title, color) = s.Ending switch
        {
            RunEnding.Won => ("VICTORY", UiTheme.BrassHi),
            RunEnding.Slain => ("YOU WERE SLAIN", new Vector4(0.95f, 0.36f, 0.34f, 1f)),
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

        Row("Survived", $"{(int)s.Seconds / 60:00}:{(int)s.Seconds % 60:00}", UiTheme.Ink);
        Row("Level reached", s.Level.ToString(), UiTheme.Ink);
        Row("Kills", s.Kills.ToString("N0"), UiTheme.Ink);
        Row($"{s.TreeName} experience", $"+{s.TreeExperience:N0}", UiTheme.Teal);
        if (s.TreeLevelsGained > 0)
        {
            Row($"{s.TreeName} level", $"{s.TreeLevel}  (+{s.TreeLevelsGained}, spend at the target)", UiTheme.BrassHi);
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
