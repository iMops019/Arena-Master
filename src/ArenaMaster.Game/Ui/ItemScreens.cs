using System.Numerics;
using ArenaMaster.Game.Items;
using ArenaMaster.Game.Progression;
using ImGuiNET;

namespace ArenaMaster.Game.Ui;

/// <summary>What choosing a loadout means: at most <see cref="MaxItems"/> different items, each brought with every copy owned.</summary>
internal static class Loadout
{
    public const int MaxItems = 5;

    /// <summary>Drops anything from the saved loadout that isn't an item, isn't owned any more, or is past the limit.</summary>
    public static void Sanitize(Profile profile)
    {
        profile.Loadout = profile.Loadout
            .Where(id => ItemCatalog.All.Any(i => i.Id == id) && profile.CountOf(id) > 0)
            .Distinct()
            .Take(MaxItems)
            .ToList();
    }

    /// <summary>Adds <paramref name="id"/> to the loadout, or takes it out if it is in. False if adding would pass the limit.</summary>
    public static bool Toggle(Profile profile, string id)
    {
        if (profile.Loadout.Remove(id))
        {
            return true;
        }

        if (profile.Loadout.Count >= MaxItems || profile.CountOf(id) <= 0)
        {
            return false;
        }

        profile.Loadout.Add(id);
        return true;
    }

    /// <summary>The items a run starts with: every copy owned of each item in the loadout.</summary>
    public static IEnumerable<RunItem> ItemsToBring(Profile profile) =>
        profile.Loadout
            .Select(id => ItemCatalog.All.FirstOrDefault(i => i.Id == id))
            .Where(i => i is not null)
            .SelectMany(i => Enumerable.Repeat(i!, profile.CountOf(i!.Id)));
}

/// <summary>A grid of item cards, shared by the Item Chest and the loadout screen.</summary>
internal static class ItemGrid
{
    public enum CardLook
    {
        Owned,
        Selected,
        Unfound,
    }

    /// <summary>Draws <paramref name="items"/> as cards filling the rest of the window (or <paramref name="height"/> of it) and returns the one clicked, if any.</summary>
    public static RunItem? Draw(IReadOnlyList<RunItem> items, Func<RunItem, int> countOf, Func<RunItem, CardLook> look, float height = 0f)
    {
        float scale = UiTheme.Scale;
        var origin = ImGui.GetCursorScreenPos();
        float width = ImGui.GetContentRegionAvail().X;
        float areaHeight = height > 0f ? height : ImGui.GetContentRegionAvail().Y;
        int columns = Math.Max(2, (int)(width / (230f * scale)));
        int rows = Math.Max(1, (items.Count + columns - 1) / columns);
        float gap = 12f * scale;
        float cardWidth = (width - gap * (columns - 1)) / columns;
        float cardHeight = MathF.Min(118f * scale, (areaHeight - gap * (rows - 1)) / rows);

        RunItem? clicked = null;
        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var min = origin + new Vector2((i % columns) * (cardWidth + gap), (i / columns) * (cardHeight + gap));
            var max = min + new Vector2(cardWidth, cardHeight);
            ImGui.SetCursorScreenPos(min);
            ImGui.PushID(item.Id);
            bool pressed = ImGui.InvisibleButton("card", max - min);
            bool hovered = ImGui.IsItemHovered();
            ImGui.PopID();
            if (pressed)
            {
                clicked = item;
            }

            DrawCard(item, countOf(item), look(item), min, max, hovered);
        }

        ImGui.SetCursorScreenPos(origin + new Vector2(0f, rows * (cardHeight + gap)));
        ImGui.Dummy(new Vector2(width, 1f));
        return clicked;
    }

    private static void DrawCard(RunItem item, int count, CardLook look, Vector2 min, Vector2 max, bool hovered)
    {
        float scale = UiTheme.Scale;
        float pad = 12f * scale;
        var rarity = UiTheme.Rarity(item.Rarity);
        var draw = ImGui.GetWindowDrawList();

        if (look == CardLook.Unfound)
        {
            UiTheme.Card(min, max, UiTheme.WithAlpha(UiTheme.PanelRaised, 0.5f), UiTheme.Line);
            UiTheme.Text(min + new Vector2(pad, pad), item.Rarity.ToString().ToUpperInvariant(), UiTheme.WithAlpha(rarity, 0.35f), 0.62f);
            UiTheme.Text(min + new Vector2(pad, pad + ImGui.GetFontSize() * 0.85f), "Not found yet", UiTheme.Faint, 0.95f);
            return;
        }

        var border = look == CardLook.Selected ? UiTheme.Teal : hovered ? UiTheme.WithAlpha(rarity, 0.9f) : UiTheme.WithAlpha(rarity, 0.4f);
        var fill = look == CardLook.Selected ? UiTheme.TealDeep : hovered ? new Vector4(0.1f, 0.15f, 0.18f, 1f) : UiTheme.PanelRaised;
        UiTheme.Card(min, max, fill, border, look == CardLook.Selected ? 2.5f : 1.5f);
        draw.AddRectFilled(min + new Vector2(pad, 0f), new Vector2(max.X - pad, min.Y + 3f * scale), UiTheme.U32(rarity));   // the rarity stripe

        UiTheme.Text(min + new Vector2(pad, pad), item.Rarity.ToString().ToUpperInvariant(), rarity, 0.62f);
        UiTheme.Text(min + new Vector2(pad, pad + ImGui.GetFontSize() * 0.85f), item.Name, UiTheme.Ink, 1.02f);
        UiTheme.Text(min + new Vector2(pad, pad + ImGui.GetFontSize() * 2.2f), item.Description, UiTheme.Muted, 0.8f, max.X - min.X - 2f * pad);

        string badge = $"x{count}";
        float badgeWidth = UiTheme.TextWidth(badge, 0.95f);
        UiTheme.Text(new Vector2(max.X - pad - badgeWidth, min.Y + pad), badge, look == CardLook.Selected ? UiTheme.Teal : UiTheme.BrassHi, 0.95f);
    }
}

/// <summary>The Item Chest at camp: every item there is, how many of each the player owns, and the ones still to be found.</summary>
internal sealed class ItemChestScreen : GameScreen
{

    /// <summary>Draws the chest. Returns true when the player closes it.</summary>
    public bool Draw(Profile profile)
    {
        if (!IsOpen)
        {
            return false;
        }

        MarkDrawn();
        UiTheme.BeginScreen("##itemchest", 0.78f, 0.84f);
        var all = ItemCatalog.All.OrderByDescending(i => i.Rarity).ThenBy(i => i.Name).ToList();
        int kinds = all.Count(i => profile.CountOf(i.Id) > 0);
        int total = profile.Stash.Values.Sum();
        UiTheme.Header("Camp · Item Chest", "Your items", $"{kinds} of {all.Count} found  ·  {total} in the chest");

        float buttonHeight = 40f * UiTheme.Scale;
        ItemGrid.Draw(all, i => profile.CountOf(i.Id), i => profile.CountOf(i.Id) > 0 ? ItemGrid.CardLook.Owned : ItemGrid.CardLook.Unfound,
            ImGui.GetContentRegionAvail().Y - buttonHeight - 16f * UiTheme.Scale);

        bool close = Footer("Items found in a run come here when you pick them up. Choose which to bring at the departure gate.", buttonHeight);
        UiTheme.EndScreen();
        if (close || ClosedByKey(ImGuiKey.E))
        {
            Close();
            return true;
        }

        return false;
    }

    private static bool Footer(string note, float buttonHeight)
    {
        var start = ImGui.GetCursorScreenPos();
        float width = ImGui.GetContentRegionAvail().X;
        float buttonWidth = 150f * UiTheme.Scale;
        UiTheme.Text(start + new Vector2(0f, buttonHeight * 0.3f), note, UiTheme.Muted, 0.8f, width - buttonWidth - 20f);
        ImGui.SetCursorScreenPos(start + new Vector2(width - buttonWidth, 0f));
        return UiTheme.Button("Close  [E]", new Vector2(buttonWidth, buttonHeight));
    }
}

/// <summary>
/// The loadout screen at the departure gate: pick up to <see cref="Loadout.MaxItems"/> different items from the chest to bring on the run, each with every copy
/// owned, then begin. The choice is remembered for next time.
/// </summary>
internal sealed class LoadoutScreen : GameScreen
{
    public enum Result
    {
        None,
        Close,
        Begin,
    }

    public Result Draw(Profile profile)
    {
        if (!IsOpen)
        {
            return Result.None;
        }

        MarkDrawn();
        float scale = UiTheme.Scale;
        UiTheme.BeginScreen("##loadout", 0.78f, 0.84f);
        UiTheme.Header("Camp · Departure gate", "Choose your loadout", $"{profile.Loadout.Count} / {Loadout.MaxItems} items");

        DrawSlots(profile);

        var owned = ItemCatalog.All.Where(i => profile.CountOf(i.Id) > 0).OrderByDescending(i => i.Rarity).ThenBy(i => i.Name).ToList();
        float buttonHeight = 44f * scale;
        float gridHeight = ImGui.GetContentRegionAvail().Y - buttonHeight - 16f * scale;
        if (owned.Count == 0)
        {
            var at = ImGui.GetCursorScreenPos();
            UiTheme.Text(at + new Vector2(0f, 20f * scale), "Your chest is empty. Items you find in runs will show up here.", UiTheme.Muted, 1f);
            ImGui.Dummy(new Vector2(1f, gridHeight));
        }
        else if (ItemGrid.Draw(owned, i => profile.CountOf(i.Id),
                     i => profile.Loadout.Contains(i.Id) ? ItemGrid.CardLook.Selected : ItemGrid.CardLook.Owned, gridHeight) is { } clicked)
        {
            Loadout.Toggle(profile, clicked.Id);
        }

        var start = ImGui.GetCursorScreenPos();
        float width = ImGui.GetContentRegionAvail().X;
        float beginWidth = 200f * scale, backWidth = 130f * scale, clearWidth = 120f * scale;
        UiTheme.Text(start + new Vector2(0f, buttonHeight * 0.28f), "Every copy you own of a chosen item comes along. Items you find during the run are yours too.",
            UiTheme.Muted, 0.8f, width - beginWidth - backWidth - clearWidth - 40f * scale);

        var result = Result.None;
        ImGui.SetCursorScreenPos(start + new Vector2(width - beginWidth - backWidth - clearWidth - 20f * scale, 0f));
        if (UiTheme.Button("Clear", new Vector2(clearWidth, buttonHeight), enabled: profile.Loadout.Count > 0))
        {
            profile.Loadout.Clear();
        }

        ImGui.SameLine(0f, 10f * scale);
        if (UiTheme.Button("Back  [E]", new Vector2(backWidth, buttonHeight)))
        {
            result = Result.Close;
        }

        ImGui.SameLine(0f, 10f * scale);
        if (UiTheme.Button("Begin run", new Vector2(beginWidth, buttonHeight), primary: true))
        {
            result = Result.Begin;
        }

        UiTheme.EndScreen();
        if (result == Result.None && ClosedByKey(ImGuiKey.E))
        {
            result = Result.Close;
        }

        if (result != Result.None)
        {
            Close();
        }

        return result;
    }

    /// <summary>The five loadout slots across the top: what is chosen, and how many copies come with it.</summary>
    private static void DrawSlots(Profile profile)
    {
        float scale = UiTheme.Scale;
        var origin = ImGui.GetCursorScreenPos();
        float width = ImGui.GetContentRegionAvail().X;
        float gap = 10f * scale, height = 54f * scale;
        float slotWidth = (width - gap * (Loadout.MaxItems - 1)) / Loadout.MaxItems;

        for (int i = 0; i < Loadout.MaxItems; i++)
        {
            var min = origin + new Vector2(i * (slotWidth + gap), 0f);
            var max = min + new Vector2(slotWidth, height);
            if (i < profile.Loadout.Count && ItemCatalog.All.FirstOrDefault(x => x.Id == profile.Loadout[i]) is { } item)
            {
                var rarity = UiTheme.Rarity(item.Rarity);
                UiTheme.Card(min, max, UiTheme.TealDeep, rarity, 2f);
                UiTheme.Text(min + new Vector2(10f * scale, 8f * scale), item.Name, UiTheme.Ink, 0.9f, slotWidth - 60f * scale);
                string count = $"x{profile.CountOf(item.Id)}";
                UiTheme.Text(new Vector2(max.X - 10f * scale - UiTheme.TextWidth(count, 0.9f), min.Y + 8f * scale), count, UiTheme.BrassHi, 0.9f);
                UiTheme.Text(min + new Vector2(10f * scale, 30f * scale), item.Rarity.ToString(), rarity, 0.65f);
            }
            else
            {
                var draw = ImGui.GetWindowDrawList();
                draw.AddRect(min, max, UiTheme.U32(UiTheme.Line), 8f * scale, ImDrawFlags.None, 1.5f * scale);
                UiTheme.Text(min + new Vector2(10f * scale, height * 0.32f), $"Slot {i + 1} · empty", UiTheme.Faint, 0.8f);
            }
        }

        ImGui.Dummy(new Vector2(width, height + 14f * scale));
    }
}
