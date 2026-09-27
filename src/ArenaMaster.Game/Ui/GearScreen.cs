using System.Numerics;
using ArenaMaster.Game.Delve;
using ArenaMaster.Game.Gear;
using ArenaMaster.Game.Progression;
using ImGuiNET;

namespace ArenaMaster.Game.Ui;

/// <summary>
/// The armour stand at camp: the three slots side by side, each listing every copy of gear owned for it - its name in the unique orange, how well it rolled, and
/// each stat's number with the range it can roll in - and the pieces not found yet. Clicking a copy wears it (or takes it off if it is worn). Each column scrolls.
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
        UiTheme.BeginScreen("##gear", 0.9f, 0.9f);
        int copies = GearCatalog.Owned(profile).Count();
        UiTheme.Header("Camp · Armour stand", "Gear", $"{GearCatalog.PiecesFound(profile)} / {GearCatalog.All.Count} pieces found  ·  {copies} {(copies == 1 ? "copy" : "copies")} held");

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
            UiTheme.Text(new Vector2(x, origin.Y), GearCatalog.SlotName(slot).ToUpperInvariant(), UiTheme.Muted, 0.9f);
            var worn = GearCatalog.WornIn(profile, slot);
            string state = worn is null ? "Empty" : "Worn: " + GearCatalog.PieceOf(worn).Name;
            UiTheme.Text(new Vector2(x + columnWidth - UiTheme.TextWidth(state, 0.8f), origin.Y + font * 0.1f), state, worn is null ? UiTheme.Faint : UiTheme.Unique, 0.8f);

            ImGui.SetCursorScreenPos(new Vector2(x, origin.Y + font * 1.4f));
            ImGui.BeginChild($"##slot{c}", new Vector2(columnWidth, bodyHeight - font * 1.4f), ImGuiChildFlags.None, ImGuiWindowFlags.None);
            var clicked = DrawColumn(profile, slot, worn, columnWidth - 14f * scale);
            ImGui.EndChild();
            if (clicked is not null)
            {
                if (worn == clicked)
                {
                    GearCatalog.TakeOff(profile, slot);
                }
                else
                {
                    GearCatalog.Wear(profile, clicked);
                }

                Changed = true;
            }
        }

        ImGui.SetCursorScreenPos(origin + new Vector2(0f, bodyHeight + 14f * scale));
        var start = ImGui.GetCursorScreenPos();
        float closeWidth = 150f * scale;
        UiTheme.Text(start + new Vector2(0f, buttonHeight * 0.3f),
            $"Gear comes from the boss hunt at the departure gate: {BossHunt.GearChance * 100f:0}% a kill, any piece, every stat rolled in its range. Click a copy to wear it, or to take it off. Sell spare copies at the quartermaster.",
            UiTheme.Muted, 0.78f, width - closeWidth - 20f * scale);
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

    /// <summary>One slot's list: every copy owned, piece by piece and best roll first, then one card for the pieces not found yet. Returns the copy clicked, if any.</summary>
    private static GearItem? DrawColumn(Profile profile, GearSlot slot, GearItem? worn, float width)
    {
        float scale = UiTheme.Scale;
        float font = ImGui.GetFontSize();
        float gap = 8f * scale;
        var origin = ImGui.GetCursorScreenPos();
        float y = origin.Y;
        GearItem? clicked = null;
        var pieces = GearCatalog.All.Where(p => p.Slot == slot).ToList();
        foreach (var piece in pieces)
        {
            foreach (var item in profile.Gear.Items.Where(i => i.Piece == piece.Id).OrderByDescending(GearCatalog.Quality))
            {
                float height = CardHeight(piece);
                var min = new Vector2(origin.X, y);
                if (DrawCopy(item, worn == item, min, new Vector2(width, height)))
                {
                    clicked = item;
                }

                y += height + gap;
            }
        }

        int missing = pieces.Count(p => !GearCatalog.Owns(profile, p));
        if (missing > 0)
        {
            var min = new Vector2(origin.X, y);
            var max = min + new Vector2(width, font * 2.6f);
            UiTheme.Card(min, max, UiTheme.Panel, UiTheme.Line);
            string text = missing == 1 ? "1 piece not found yet" : $"{missing} pieces not found yet";
            UiTheme.Text(min + new Vector2(12f * scale, font * 0.8f), text, UiTheme.Faint, 0.9f);
            y = max.Y + gap;
        }

        ImGui.SetCursorScreenPos(new Vector2(origin.X, y));
        ImGui.Dummy(new Vector2(width, 1f));
        return clicked;
    }

    /// <summary>How tall a copy's card is: its name, a line a stat, and its flavour.</summary>
    public static float CardHeight(GearPiece piece) => ImGui.GetFontSize() * (3.3f + 1.05f * piece.Stats.Count) + 16f * UiTheme.Scale;

    /// <summary>A copy's card: name, how well it rolled, its stats and its flavour, lit if worn. True if it was clicked.</summary>
    private static bool DrawCopy(GearItem item, bool worn, Vector2 min, Vector2 size)
    {
        float scale = UiTheme.Scale;
        var max = min + size;
        ImGui.SetCursorScreenPos(min);
        ImGui.PushID(item.Id);
        bool clicked = ImGui.InvisibleButton("##copy", size);
        bool hovered = ImGui.IsItemHovered();
        ImGui.PopID();

        var fill = worn ? UiTheme.UniqueDeep : hovered ? new Vector4(0.1f, 0.14f, 0.17f, 1f) : UiTheme.PanelRaised;
        UiTheme.Card(min, max, fill, worn ? UiTheme.Unique : UiTheme.WithAlpha(UiTheme.Unique, 0.45f), worn ? 2.5f : 1.5f);
        float y = DrawHeading(item, min, size.X, worn ? "WORN" : "");
        y = DrawStats(item, new Vector2(min.X + 12f * scale, y), size.X - 24f * scale);
        var piece = GearCatalog.PieceOf(item);
        UiTheme.Text(new Vector2(min.X + 12f * scale, y + 2f * scale), $"\"{piece.Flavour}\"", UiTheme.WithAlpha(UiTheme.Unique, 0.6f), 0.72f, size.X - 24f * scale);
        return clicked;
    }

    /// <summary>A copy's name with the unique's diamond, its roll quality and a <paramref name="tag"/> at the right. Returns where the next line starts.</summary>
    public static float DrawHeading(GearItem item, Vector2 min, float width, string tag)
    {
        float scale = UiTheme.Scale;
        float font = ImGui.GetFontSize();
        float pad = 12f * scale;
        var draw = ImGui.GetWindowDrawList();
        var mark = min + new Vector2(pad + 6f * scale, pad + font * 0.5f);
        float m = 6f * scale;
        draw.AddQuadFilled(mark + new Vector2(0f, -m), mark + new Vector2(m, 0f), mark + new Vector2(0f, m), mark + new Vector2(-m, 0f), UiTheme.U32(UiTheme.Unique));
        UiTheme.Text(min + new Vector2(pad + 18f * scale, pad), GearCatalog.PieceOf(item).Name, UiTheme.Unique, 1.05f);

        float quality = GearCatalog.Quality(item);
        string rolled = $"{quality * 100f:0}%";
        string right = tag.Length > 0 ? $"{tag}  ·  {rolled}" : rolled;
        UiTheme.Text(new Vector2(min.X + width - pad - UiTheme.TextWidth(right, 0.8f), min.Y + pad + font * 0.15f), right, QualityColour(quality), 0.8f);
        return min.Y + pad + font * 1.45f;
    }

    /// <summary>A copy's stats, a line each: the rolled line, and the range it rolls in after it. Returns where the next line starts.</summary>
    public static float DrawStats(GearItem item, Vector2 at, float width, float size = 0.82f)
    {
        float font = ImGui.GetFontSize();
        var stats = GearCatalog.PieceOf(item).Stats;
        float y = at.Y;
        for (int i = 0; i < stats.Count; i++)
        {
            var stat = stats[i];
            float value = GearCatalog.Roll(item, i);
            string line = stat.Line(value);
            UiTheme.Text(new Vector2(at.X, y), line, UiTheme.Ink, size, width);
            if (stat.Rolls)
            {
                string range = $"({stat.Range})";
                float x = MathF.Min(at.X + UiTheme.TextWidth(line, size) + 8f * UiTheme.Scale, at.X + width - UiTheme.TextWidth(range, size * 0.9f));
                UiTheme.Text(new Vector2(x, y + font * 0.05f), range, QualityColour(stat.Quality(value)), size * 0.9f);
            }

            y += font * 1.05f;
        }

        return y;
    }

    /// <summary>The colour for how well something rolled: grey for poor, white for fair, brass for good, unique orange for (near) perfect.</summary>
    public static Vector4 QualityColour(float quality) => quality switch
    {
        >= 0.95f => UiTheme.Unique,
        >= 0.7f => UiTheme.BrassHi,
        >= 0.35f => UiTheme.Ink,
        _ => UiTheme.Muted,
    };
}

/// <summary>
/// The quartermaster buying gear: every copy owned and not worn, piece by piece, with how well it rolled, its stats and what he pays (more for a better roll). A
/// sale takes two clicks - the button asks to be sure first - and can't be undone. Worn copies are listed at the top to compare against, and aren't for sale.
/// </summary>
internal sealed class GearSaleScreen : GameScreen
{
    public enum Result
    {
        None,
        Back,
        Close,
    }

    /// <summary>Set when something was sold, so the caller knows to save.</summary>
    public bool Changed { get; set; }

    /// <summary>The copy whose Sell button has been clicked once, waiting for the second.</summary>
    private string? _confirming;

    public new void Open()
    {
        base.Open();
        _confirming = null;
    }

    public Result Draw(Profile profile)
    {
        if (!IsOpen)
        {
            return Result.None;
        }

        MarkDrawn();
        float scale = UiTheme.Scale;
        float font = ImGui.GetFontSize();
        UiTheme.BeginScreen("##gearsale", 0.74f, 0.88f);
        UiTheme.Header("Camp · Quartermaster", "Sell gear", $"{profile.Silver:N0} silver");

        // What is worn, to compare against.
        float width = ImGui.GetContentRegionAvail().X;
        var at = ImGui.GetCursorScreenPos();
        var worn = GearCatalog.Worn(profile).ToList();
        string wornLine = worn.Count == 0 ? "Nothing worn." : "Worn:  " + string.Join("   ·   ",
            worn.Select(item => $"{GearCatalog.PieceOf(item).Name} ({GearCatalog.Quality(item) * 100f:0}%)"));
        UiTheme.Text(at, wornLine, UiTheme.Unique, 0.8f, width);
        ImGui.SetCursorScreenPos(at + new Vector2(0f, font * 1.5f));

        float buttonHeight = 40f * scale;
        float listHeight = ImGui.GetContentRegionAvail().Y - buttonHeight - 16f * scale;
        var listStart = ImGui.GetCursorScreenPos();
        ImGui.BeginChild("##salelist", new Vector2(width, listHeight), ImGuiChildFlags.None, ImGuiWindowFlags.None);
        float inner = ImGui.GetContentRegionAvail().X - 14f * scale;
        var origin = ImGui.GetCursorScreenPos();
        float y = origin.Y;
        var forSale = GearCatalog.Owned(profile).Where(item => !GearCatalog.IsWorn(profile, item))
            .OrderBy(item => GearCatalog.PieceOf(item).Slot).ThenBy(item => GearCatalog.PieceOf(item).Name).ThenByDescending(GearCatalog.Quality).ToList();
        GearItem? sold = null;
        foreach (var item in forSale)
        {
            var piece = GearCatalog.PieceOf(item);
            float height = font * (1.9f + 1.0f * piece.Stats.Count) + 16f * scale;
            var min = new Vector2(origin.X, y);
            var max = min + new Vector2(inner, height);
            UiTheme.Card(min, max, UiTheme.PanelRaised, UiTheme.WithAlpha(UiTheme.Unique, 0.4f));

            // Better than the copy of the same piece worn?
            var wornHere = GearCatalog.WornIn(profile, piece.Slot);
            string tag = wornHere is not null && wornHere.Piece == item.Piece && GearCatalog.Quality(item) > GearCatalog.Quality(wornHere) ? "BETTER THAN WORN" : "";
            float sellWidth = 210f * scale;
            float textWidth = inner - sellWidth - 24f * scale;
            float next = GearScreen.DrawHeading(item, min, textWidth, tag);
            GearScreen.DrawStats(item, new Vector2(min.X + 12f * scale, next), textWidth - 24f * scale, 0.78f);

            ImGui.SetCursorScreenPos(new Vector2(max.X - 12f * scale - sellWidth, min.Y + (height - buttonHeight) * 0.5f));
            ImGui.PushID(item.Id);
            bool sure = _confirming == item.Id;
            long price = GearCatalog.SellPrice(item);
            if (UiTheme.Button(sure ? $"Sure?  Sell for {price:N0}" : $"Sell  ·  {price:N0} silver", new Vector2(sellWidth, buttonHeight), primary: sure))
            {
                if (sure)
                {
                    sold = item;
                }
                else
                {
                    _confirming = item.Id;
                }
            }

            ImGui.PopID();
            y = max.Y + 8f * scale;
        }

        if (forSale.Count == 0)
        {
            UiTheme.Text(origin + new Vector2(0f, font * 0.5f), "Nothing to sell: every copy you own is worn. Spare copies from the boss hunt turn up here.", UiTheme.Muted, 0.85f, inner);
            y += font * 2f;
        }

        ImGui.SetCursorScreenPos(new Vector2(origin.X, y));
        ImGui.Dummy(new Vector2(inner, 1f));
        ImGui.EndChild();

        if (sold is not null && GearCatalog.Sell(profile, sold))
        {
            _confirming = null;
            Changed = true;
        }

        ImGui.SetCursorScreenPos(listStart + new Vector2(0f, listHeight + 10f * scale));
        var start = ImGui.GetCursorScreenPos();
        float backWidth = 190f * scale, closeWidth = 150f * scale;
        UiTheme.Text(start + new Vector2(backWidth + 16f * scale, buttonHeight * 0.3f),
            $"He pays {GearCatalog.SellFloor}-{GearCatalog.SellCeiling} silver, more for better rolls. A sale can't be undone. Worn copies aren't for sale.",
            UiTheme.Muted, 0.75f, width - backWidth - closeWidth - 40f * scale);
        var result = Result.None;
        if (UiTheme.Button("Back to supplies", new Vector2(backWidth, buttonHeight)))
        {
            result = Result.Back;
        }

        ImGui.SetCursorScreenPos(start + new Vector2(width - closeWidth, 0f));
        if (UiTheme.Button("Close  [E]", new Vector2(closeWidth, buttonHeight)) || ClosedByKey(ImGuiKey.E))
        {
            result = Result.Close;
        }

        UiTheme.EndScreen();
        if (result != Result.None)
        {
            Close();
        }

        return result;
    }
}
