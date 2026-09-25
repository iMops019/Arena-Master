using System.Numerics;
using ArenaMaster.Game.Items;
using ArenaMaster.Game.Progression;
using ImGuiNET;

namespace ArenaMaster.Game.Ui;

/// <summary>The bounty board at camp: every bounty, what it asks, what it pays and unlocks, and which are done.</summary>
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

        var origin = ImGui.GetCursorScreenPos();
        float width = ImGui.GetContentRegionAvail().X;
        float buttonHeight = 40f * scale;
        float rowHeight = MathF.Min(58f * scale, (ImGui.GetContentRegionAvail().Y - buttonHeight - 20f * scale) / Bounties.All.Count);
        float font = ImGui.GetFontSize();

        for (int i = 0; i < Bounties.All.Count; i++)
        {
            var bounty = Bounties.All[i];
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

        ImGui.SetCursorScreenPos(origin + new Vector2(0f, Bounties.All.Count * rowHeight + 8f * scale));
        var start = ImGui.GetCursorScreenPos();
        float closeWidth = 150f * scale;
        UiTheme.Text(start + new Vector2(0f, buttonHeight * 0.3f), "Bounties are checked at the end of every run, win or lose. Each pays once.", UiTheme.Muted, 0.8f,
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
}

/// <summary>The quartermaster at camp: permanent upgrades, each rank bought with silver.</summary>
internal sealed class QuartermasterScreen : GameScreen
{
    /// <summary>Set when something is bought, so the caller knows to save.</summary>
    public bool Changed { get; set; }

    /// <summary>Draws the stall. Returns true when the player closes it.</summary>
    public bool Draw(Profile profile)
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
        float cardHeight = MathF.Min(120f * scale, (ImGui.GetContentRegionAvail().Y - buttonHeight - 20f * scale - gap * (count - 1)) / count);
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

        ImGui.SetCursorScreenPos(origin + new Vector2(0f, count * (cardHeight + gap) + 4f * scale));
        var start = ImGui.GetCursorScreenPos();
        float closeWidth = 150f * scale;
        UiTheme.Text(start + new Vector2(0f, buttonHeight * 0.3f), "Silver comes at the end of every run: kills, elites, bosses, time survived, and a bonus for a win.",
            UiTheme.Muted, 0.8f, width - closeWidth - 20f * scale);
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
}
