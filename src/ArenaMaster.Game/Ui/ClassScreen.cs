using System.Numerics;
using ArenaMaster.Game.Classes;
using ArenaMaster.Game.Progression;
using ImGuiNET;

namespace ArenaMaster.Game.Ui;

/// <summary>
/// The class rack at camp: a card for each class - how it fights, its passive tree and how far that tree has come - and a button to play it. The choice is saved
/// with the profile; each class keeps its own tree, and the item chest is shared.
/// </summary>
internal sealed class ClassScreen : GameScreen
{
    /// <summary>Draws the screen. Returns whether the player closed it, and the class they chose this frame, if any.</summary>
    public (bool Closed, IHeroClass? Chosen) Draw(IReadOnlyList<IHeroClass> classes, IHeroClass current, Profile profile)
    {
        if (!IsOpen)
        {
            return (false, null);
        }

        MarkDrawn();
        float scale = UiTheme.Scale;
        UiTheme.BeginScreen("##classes", MathF.Min(0.92f, 0.3f + 0.2f * classes.Count), 0.7f);
        UiTheme.Header("Camp · Weapon rack", "Choose your class", $"Playing the {current.Name}");

        var origin = ImGui.GetCursorScreenPos();
        var avail = ImGui.GetContentRegionAvail();
        float buttonHeight = 40f * scale;
        float gap = 18f * scale;
        float cardWidth = (avail.X - gap * (classes.Count - 1)) / classes.Count;
        float cardHeight = avail.Y - buttonHeight - 18f * scale;
        IHeroClass? chosen = null;

        for (int i = 0; i < classes.Count; i++)
        {
            var hero = classes[i];
            bool playing = hero == current;
            var min = origin + new Vector2(i * (cardWidth + gap), 0f);
            var max = min + new Vector2(cardWidth, cardHeight);
            if (DrawCard(hero, playing, new TreeProgress(hero.Tree, profile.Tree(hero.Id, hero.Tree.Id)), min, max, i))
            {
                chosen = hero;
            }
        }

        ImGui.SetCursorScreenPos(origin + new Vector2(0f, cardHeight + 14f * scale));
        UiTheme.Text(ImGui.GetCursorScreenPos() + new Vector2(0f, buttonHeight * 0.28f),
            "Each class has its own attacks, level-up upgrades and passive tree. The item chest is shared.", UiTheme.Muted, 0.8f, avail.X - 180f * scale);
        ImGui.SetCursorScreenPos(origin + new Vector2(avail.X - 150f * scale, cardHeight + 14f * scale));
        bool close = UiTheme.Button("Close  [E]", new Vector2(150f * scale, buttonHeight));

        UiTheme.EndScreen();
        if (close || ClosedByKey(ImGuiKey.E))
        {
            Close();
            return (true, chosen);
        }

        return (false, chosen);
    }

    /// <summary>One class's card. True if its button was clicked.</summary>
    private static bool DrawCard(IHeroClass hero, bool playing, TreeProgress tree, Vector2 min, Vector2 max, int index)
    {
        float scale = UiTheme.Scale;
        float pad = 18f * scale;
        float width = max.X - min.X - 2f * pad;
        float font = ImGui.GetFontSize();
        UiTheme.Card(min, max, playing ? UiTheme.TealDeep : UiTheme.PanelRaised, playing ? UiTheme.Teal : UiTheme.Line, playing ? 2f : 1.5f);

        var y = min.Y + pad;
        UiTheme.Text(new Vector2(min.X + pad, y), playing ? "PLAYING" : "CLASS", playing ? UiTheme.Teal : UiTheme.Muted, 0.65f);
        y += font * 0.9f;
        UiTheme.Text(new Vector2(min.X + pad, y), hero.Name, UiTheme.Ink, 1.6f);
        y += font * 2.1f;
        UiTheme.Text(new Vector2(min.X + pad, y), hero.Summary, UiTheme.Ink, 0.85f, width);
        y += ImGui.CalcTextSize(hero.Summary, width / 0.85f).Y * 0.85f + font * 0.8f;

        int free = tree.FreePoints;
        UiTheme.Text(new Vector2(min.X + pad, y), "PASSIVE TREE", UiTheme.Muted, 0.6f);
        y += font * 0.85f;
        UiTheme.Text(new Vector2(min.X + pad, y), $"{tree.Tree.Name}  ·  Lv {tree.Level}" + (free > 0 ? $"  ·  {free} to spend" : ""), UiTheme.BrassHi, 0.9f);

        float buttonHeight = 38f * scale;
        ImGui.SetCursorScreenPos(new Vector2(min.X + pad, max.Y - pad - buttonHeight));
        ImGui.PushID(index);
        bool clicked = UiTheme.Button(playing ? "Playing" : $"Play the {hero.Name}", new Vector2(width, buttonHeight), primary: !playing, enabled: !playing);
        ImGui.PopID();
        return clicked;
    }
}
