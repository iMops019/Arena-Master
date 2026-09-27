using System.Numerics;
using ArenaMaster.Game.Items;
using ArenaMaster.Game.Progression;
using ImGuiNET;

namespace ArenaMaster.Game.Ui;

/// <summary>
/// The bounty board at camp: every bounty, what it asks, what it pays and unlocks, and which are done - the open ones first, in a list that scrolls when they
/// don't all fit.
/// </summary>
internal sealed class BountyBoardScreen : GameScreen
{
    /// <summary>Draws the board. Returns true when the player closes it.</summary>
    public bool Draw(Profile profile)
    {
        if (!IsOpen)
        {
            return false;
        }

        MarkDrawn();
        float scale = UiTheme.Scale;
        UiTheme.BeginScreen("##bounties", 0.7f, 0.84f);
        int done = Bounties.All.Count(b => profile.HasBounty(b.Id));
        UiTheme.Header("Camp · Bounty board", "Bounties", $"{done} of {Bounties.All.Count} done  ·  {profile.Silver:N0} silver");

        var listStart = ImGui.GetCursorScreenPos();
        float fullWidth = ImGui.GetContentRegionAvail().X;
        float buttonHeight = 40f * scale;
        float listHeight = ImGui.GetContentRegionAvail().Y - buttonHeight - 16f * scale;
        ImGui.BeginChild("##bountylist", new Vector2(fullWidth, listHeight), ImGuiChildFlags.None, ImGuiWindowFlags.None);

        var origin = ImGui.GetCursorScreenPos();
        float width = ImGui.GetContentRegionAvail().X - 14f * scale;   // room for the scrollbar
        float rowHeight = 58f * scale;
        float font = ImGui.GetFontSize();
        var ordered = Bounties.All.OrderBy(b => profile.HasBounty(b.Id)).ToList();   // stable: the board's own order within open and done

        for (int i = 0; i < ordered.Count; i++)
        {
            var bounty = ordered[i];
            bool complete = profile.HasBounty(bounty.Id);
            var min = origin + new Vector2(0f, i * rowHeight);
            var max = min + new Vector2(width, rowHeight - 6f * scale);
            UiTheme.Card(min, max, complete ? UiTheme.WithAlpha(UiTheme.PanelRaised, 0.5f) : UiTheme.PanelRaised, complete ? UiTheme.Line : UiTheme.WithAlpha(UiTheme.Brass, 0.5f));

            float pad = 12f * scale;
            UiTheme.Text(min + new Vector2(pad, pad * 0.6f), bounty.Name, complete ? UiTheme.Muted : UiTheme.Ink, 0.95f);
            UiTheme.Text(min + new Vector2(pad, pad * 0.6f + font * 1.05f), bounty.Task, UiTheme.Muted, 0.75f);

            string reward = $"{bounty.Silver:N0} silver";
            if (bounty.UnlocksItem is { } id && ItemCatalog.All.FirstOrDefault(x => x.Id == id) is { } item)
            {
                reward += $"  +  unlocks {item.Name}";
            }

            string status = complete ? "DONE" : reward;
            var statusColor = complete ? UiTheme.Teal : UiTheme.BrassHi;
            UiTheme.Text(new Vector2(max.X - pad - UiTheme.TextWidth(status, 0.8f), min.Y + pad * 0.6f), status, statusColor, 0.8f);
        }

        ImGui.SetCursorScreenPos(origin + new Vector2(0f, ordered.Count * rowHeight));
        ImGui.Dummy(new Vector2(width, 1f));
        ImGui.EndChild();

        ImGui.SetCursorScreenPos(listStart + new Vector2(0f, listHeight + 10f * scale));
        var start = ImGui.GetCursorScreenPos();
        float closeWidth = 150f * scale;
        UiTheme.Text(start + new Vector2(0f, buttonHeight * 0.3f), "Bounties are checked at the end of every run, win or lose. Each pays once. Scroll for more.",
            UiTheme.Muted, 0.8f, fullWidth - closeWidth - 20f * scale);
        ImGui.SetCursorScreenPos(start + new Vector2(fullWidth - closeWidth, 0f));
        bool close = UiTheme.Button("Close  [E]", new Vector2(closeWidth, buttonHeight));
        UiTheme.EndScreen();

        if (close || ClosedByKey(ImGuiKey.E))
        {
            Close();
            return true;
        }

        return false;
    }
}

/// <summary>The quartermaster at camp: permanent upgrades, each rank bought with silver.</summary>
internal sealed class QuartermasterScreen : GameScreen
{
    /// <summary>Set when something is bought, so the caller knows to save.</summary>
    public bool Changed { get; set; }

    /// <summary>Set when the player asked to sell gear: the stall has closed, and the caller opens the <see cref="GearSaleScreen"/>.</summary>
    public bool WantsSale { get; set; }

    /// <summary>Draws the stall: the upgrades, and the Battle Elixirs under them (the active class's tree at <paramref name="treeLevel"/>). Returns true when the player closes it.</summary>
    public bool Draw(Profile profile, int treeLevel)
    {
        if (!IsOpen)
        {
            return false;
        }

        MarkDrawn();
        float scale = UiTheme.Scale;
        UiTheme.BeginScreen("##quartermaster", 0.7f, 0.8f);
        UiTheme.Header("Camp · Quartermaster", "Supplies", $"{profile.Silver:N0} silver");

        var origin = ImGui.GetCursorScreenPos();
        float width = ImGui.GetContentRegionAvail().X;
        float buttonHeight = 40f * scale;
        float gap = 12f * scale;
        int count = Shop.All.Count;
        float elixirHeight = 118f * scale;
        float elixirBlock = elixirHeight + ImGui.GetFontSize() * 1.6f + gap;
        float cardHeight = MathF.Min(120f * scale, (ImGui.GetContentRegionAvail().Y - elixirBlock - buttonHeight - 20f * scale - gap * count) / count);
        float font = ImGui.GetFontSize();

        for (int i = 0; i < count; i++)
        {
            var upgrade = Shop.All[i];
            int rank = profile.ShopRank(upgrade.Id);
            var min = origin + new Vector2(0f, i * (cardHeight + gap));
            var max = min + new Vector2(width, cardHeight);
            float pad = 14f * scale;
            UiTheme.Card(min, max, UiTheme.PanelRaised, rank >= upgrade.MaxRank ? UiTheme.WithAlpha(UiTheme.Brass, 0.8f) : UiTheme.Line);

            UiTheme.Text(min + new Vector2(pad, pad * 0.8f), upgrade.Name, UiTheme.Ink, 1.05f);
            UiTheme.Text(min + new Vector2(pad, pad * 0.8f + font * 1.25f), upgrade.Description, UiTheme.Muted, 0.78f, width * 0.6f);

            // Rank pips under the text.
            var draw = ImGui.GetWindowDrawList();
            float pipWidth = 22f * scale, pipGap = 5f * scale;
            var pipAt = new Vector2(min.X + pad, max.Y - pad - 6f * scale);
            for (int r = 0; r < upgrade.MaxRank; r++)
            {
                var p0 = pipAt + new Vector2(r * (pipWidth + pipGap), 0f);
                draw.AddRectFilled(p0, p0 + new Vector2(pipWidth, 6f * scale), UiTheme.U32(r < rank ? UiTheme.Brass : UiTheme.Line), 2f * scale);
            }

            float buyWidth = 190f * scale;
            var buyAt = new Vector2(max.X - pad - buyWidth, min.Y + (cardHeight - buttonHeight) * 0.5f);
            ImGui.SetCursorScreenPos(buyAt);
            ImGui.PushID(upgrade.Id);
            string? why = Shop.WhyNotBuy(profile, upgrade);
            string label = Shop.NextCost(profile, upgrade) is { } cost ? $"Buy  ·  {cost:N0} silver" : "Fully bought";
            if (UiTheme.Button(label, new Vector2(buyWidth, buttonHeight), primary: why is null, enabled: why is null) && Shop.Buy(profile, upgrade))
            {
                Changed = true;
            }

            ImGui.PopID();
            if (why is not null && rank < upgrade.MaxRank)
            {
                UiTheme.Text(new Vector2(buyAt.X, buyAt.Y + buttonHeight + 3f * scale), why, UiTheme.Warn, 0.65f);
            }
        }

        DrawElixirs(profile, treeLevel, origin + new Vector2(0f, count * (cardHeight + gap)), width, elixirHeight, buttonHeight);

        ImGui.SetCursorScreenPos(origin + new Vector2(0f, count * (cardHeight + gap) + elixirBlock + 4f * scale));
        var start = ImGui.GetCursorScreenPos();
        float closeWidth = 150f * scale;
        float sellWidth = 170f * scale;
        if (UiTheme.Button("Sell gear", new Vector2(sellWidth, buttonHeight)))
        {
            WantsSale = true;
        }

        UiTheme.Text(start + new Vector2(sellWidth + 16f * scale, buttonHeight * 0.3f), "Silver comes at the end of every run, and from selling spare gear.",
            UiTheme.Muted, 0.8f, width - closeWidth - sellWidth - 36f * scale);
        ImGui.SetCursorScreenPos(start + new Vector2(width - closeWidth, 0f));
        bool close = UiTheme.Button("Close  [E]", new Vector2(closeWidth, buttonHeight));
        UiTheme.EndScreen();

        if (close || WantsSale || ClosedByKey(ImGuiKey.E))
        {
            Close();
            return true;
        }

        return false;
    }

    /// <summary>The Battle Elixirs: a card each, side by side, bought once for the next run and greyed until it is over, or refused to a class grown too strong.</summary>
    private void DrawElixirs(Profile profile, int treeLevel, Vector2 at, float width, float height, float buttonHeight)
    {
        float scale = UiTheme.Scale;
        float font = ImGui.GetFontSize();
        float gap = 12f * scale;
        float pad = 12f * scale;
        UiTheme.Text(at, "Battle Elixirs", UiTheme.Brass, 1.0f);
        UiTheme.Text(at + new Vector2(170f * scale, font * 0.1f),
            $"For the next run only · one of each · until the class's tree reaches level {Elixirs.TooHighFrom}", UiTheme.Muted, 0.75f);
        at += new Vector2(0f, font * 1.6f);

        int count = Elixirs.All.Count;
        float cardWidth = (width - gap * (count - 1)) / count;
        for (int i = 0; i < count; i++)
        {
            var elixir = Elixirs.All[i];
            bool bought = Elixirs.Bought(profile, elixir);
            string? why = Elixirs.WhyNotBuy(profile, elixir, treeLevel);
            bool greyed = bought || treeLevel >= Elixirs.TooHighFrom;
            var min = at + new Vector2(i * (cardWidth + gap), 0f);
            var max = min + new Vector2(cardWidth, height);
            UiTheme.Card(min, max, greyed ? UiTheme.Panel : UiTheme.PanelRaised, bought ? UiTheme.WithAlpha(UiTheme.Brass, 0.8f) : UiTheme.Line);
            var ink = greyed ? UiTheme.Muted : UiTheme.Ink;
            UiTheme.Text(min + new Vector2(pad, pad * 0.7f), elixir.Name, ink, 0.95f);
            UiTheme.Text(min + new Vector2(pad, pad * 0.7f + font * 1.15f), elixir.Description, UiTheme.Muted, 0.72f, cardWidth - 2f * pad);

            float buyHeight = buttonHeight * 0.85f;
            var buyAt = new Vector2(min.X + pad, max.Y - pad - buyHeight);
            ImGui.SetCursorScreenPos(buyAt);
            ImGui.PushID(elixir.Id);
            string label = bought ? "Bought" : treeLevel >= Elixirs.TooHighFrom ? Elixirs.TooHighLevel : $"Buy  ·  {elixir.Cost:N0} silver";
            if (UiTheme.Button(label, new Vector2(cardWidth - 2f * pad, buyHeight), primary: why is null, enabled: why is null)
                && Elixirs.Buy(profile, elixir, treeLevel))
            {
                Changed = true;
            }

            ImGui.PopID();
            if (why is not null && !bought && treeLevel < Elixirs.TooHighFrom)
            {
                UiTheme.Text(new Vector2(buyAt.X, buyAt.Y - font * 0.95f), why, UiTheme.Warn, 0.65f);
            }
        }
    }
}
