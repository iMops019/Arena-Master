using System.Numerics;
using ArenaMaster.Game.Gear;
using ArenaMaster.Game.Progression;
using ImGuiNET;

namespace ArenaMaster.Game.Ui;

/// <summary>
/// The armour stand at camp: what is worn in each of the three slots, and every piece of gear, each a unique drawn in the unique orange with its effect and a line of
/// flavour. Clicking an owned piece wears it (or takes it off if it is worn); pieces not found yet show only as undiscovered. Gear comes from Armoury and Boss Delves.
/// </summary>
internal sealed class GearScreen : GameScreen
{
    /// <summary>Set when the player changed what is worn; the content saves and clears it.</summary>
    public bool Changed { get; set; }

    /// <summary>Draws the screen. Returns true when it closes.</summary>
    public bool Draw(Profile profile)
    {
        if (!IsOpen)
        {
            return false;
        }

        MarkDrawn();
        float scale = UiTheme.Scale;
        UiTheme.BeginScreen("##gear", 0.86f, 0.88f);
        UiTheme.Header("Camp · Armour stand", "Gear", $"{profile.Gear.Owned.Count} / {GearCatalog.All.Count} found");

        float width = ImGui.GetContentRegionAvail().X;
        float buttonHeight = 42f * scale;
        float gap = 14f * scale;
        var slots = Enum.GetValues<GearSlot>();
        float columnWidth = (width - gap * (slots.Length - 1)) / slots.Length;
        float bodyHeight = ImGui.GetContentRegionAvail().Y - buttonHeight - 14f * scale;
        var origin = ImGui.GetCursorScreenPos();
        float font = ImGui.GetFontSize();

        for (int c = 0; c < slots.Length; c++)
        {
            var slot = slots[c];
            float x = origin.X + c * (columnWidth + gap);
            float y = origin.Y;
            UiTheme.Text(new Vector2(x, y), GearCatalog.SlotName(slot).ToUpperInvariant(), UiTheme.Muted, 0.9f);
            var worn = GearCatalog.WornIn(profile, slot);
            UiTheme.Text(new Vector2(x + columnWidth - UiTheme.TextWidth(worn is null ? "Empty" : "Worn", 0.9f), y), worn is null ? "Empty" : "Worn",
                worn is null ? UiTheme.Faint : UiTheme.Unique, 0.9f);
            y += font * 1.4f;

            var pieces = GearCatalog.All.Where(p => p.Slot == slot).ToList();
            float cardHeight = (bodyHeight - font * 1.4f - gap * (pieces.Count - 1)) / pieces.Count;
            foreach (var piece in pieces)
            {
                var min = new Vector2(x, y);
                if (DrawCard(profile, piece, min, new Vector2(columnWidth, cardHeight)))
                {
                    if (worn == piece)
                    {
                        GearCatalog.TakeOff(profile, slot);
                    }
                    else
                    {
                        GearCatalog.Wear(profile, piece);
                    }

                    Changed = true;
                }

                y += cardHeight + gap;
            }
        }

        ImGui.SetCursorScreenPos(origin + new Vector2(0f, bodyHeight + 14f * scale));
        var start = ImGui.GetCursorScreenPos();
        float closeWidth = 150f * scale;
        UiTheme.Text(start + new Vector2(0f, buttonHeight * 0.3f),
            "Gear is found in Armoury and Boss Delves. Worn gear counts on every run. Click a piece to wear it, or to take it off.", UiTheme.Muted, 0.78f,
            width - closeWidth - 20f * scale);
        ImGui.SetCursorScreenPos(start + new Vector2(width - closeWidth, 0f));
        bool close = UiTheme.Button("Close  [E]", new Vector2(closeWidth, buttonHeight));
        UiTheme.EndScreen();

        if (close || ClosedByKey(ImGuiKey.E))
        {
            Close();
            return true;
        }

        return false;
    }

    /// <summary>One piece's card. True if it was clicked (an owned piece).</summary>
    private static bool DrawCard(Profile profile, GearPiece piece, Vector2 min, Vector2 size)
    {
        float scale = UiTheme.Scale;
        float font = ImGui.GetFontSize();
        bool owned = GearCatalog.Owns(profile, piece);
        bool worn = GearCatalog.WornIn(profile, piece.Slot) == piece;
        var max = min + size;
        float pad = 12f * scale;

        ImGui.SetCursorScreenPos(min);
        ImGui.PushID(piece.Id);
        bool clicked = ImGui.InvisibleButton("##card", size) && owned;
        bool hovered = ImGui.IsItemHovered() && owned;
        ImGui.PopID();

        var fill = worn ? UiTheme.UniqueDeep : hovered ? new Vector4(0.1f, 0.14f, 0.17f, 1f) : UiTheme.PanelRaised;
        var border = !owned ? UiTheme.Line : worn ? UiTheme.Unique : UiTheme.WithAlpha(UiTheme.Unique, 0.45f);
        UiTheme.Card(min, max, fill, border, worn ? 2.5f : 1.5f);

        var draw = ImGui.GetWindowDrawList();
        if (!owned)
        {
            UiTheme.Text(min + new Vector2(pad, pad), "Undiscovered", UiTheme.Faint, 1.1f);
            UiTheme.Text(min + new Vector2(pad, pad + font * 1.5f), $"A {GearCatalog.SlotName(piece.Slot).ToLowerInvariant()} not found yet.", UiTheme.Faint, 0.9f,
                size.X - 2f * pad);
            return false;
        }

        // A small orange diamond, the unique's mark, before the name.
        var mark = min + new Vector2(pad + 6f * scale, pad + font * 0.55f);
        float m = 6f * scale;
        draw.AddQuadFilled(mark + new Vector2(0f, -m), mark + new Vector2(m, 0f), mark + new Vector2(0f, m), mark + new Vector2(-m, 0f), UiTheme.U32(UiTheme.Unique));
        UiTheme.Text(min + new Vector2(pad + 18f * scale, pad), piece.Name, UiTheme.Unique, 1.2f);
        if (worn)
        {
            UiTheme.Text(new Vector2(max.X - pad - UiTheme.TextWidth("WORN", 0.8f), min.Y + pad + font * 0.2f), "WORN", UiTheme.Unique, 0.8f);
        }

        float wrap = size.X - 2f * pad;
        UiTheme.Text(min + new Vector2(pad, pad + font * 1.6f), piece.Effect, UiTheme.Ink, 1f, wrap);
        UiTheme.Text(min + new Vector2(pad, max.Y - min.Y - pad - font * 1.9f), $"\"{piece.Flavour}\"", UiTheme.WithAlpha(UiTheme.Unique, 0.65f), 0.85f, wrap);
        return clicked;
    }
}
