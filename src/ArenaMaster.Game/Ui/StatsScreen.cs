using System.Numerics;
using ArenaMaster.Game.Classes;
using ArenaMaster.Game.Combat;
using ArenaMaster.Game.Items;
using ArenaMaster.Game.Progression;
using ImGuiNET;

namespace ArenaMaster.Game.Ui;

/// <summary>
/// The camp ledger: the player's stats over every run, on four pages - the overview (totals and bests), the classes (each one's own record, the most played
/// marked), the bestiary (every kind of enemy, how many were slain and how often it killed the player) and the Delve. Each page scrolls.
/// </summary>
internal sealed class StatsScreen : GameScreen
{
    private enum Page
    {
        Overview,
        Classes,
        Bestiary,
        Delve,
    }

    private static readonly (Page Page, string Label)[] Pages =
    {
        (Page.Overview, "Overview"), (Page.Classes, "Classes"), (Page.Bestiary, "Bestiary"), (Page.Delve, "The Delve"),
    };

    private static readonly Vector4 MagicColour = new(0.45f, 0.65f, 1f, 1f);
    private static readonly Vector4 RareColour = new(1f, 0.9f, 0.35f, 1f);

    private Page _page = Page.Overview;

    /// <summary>One number on a page: what it is, the number, and a line under it (or none).</summary>
    private readonly record struct Tile(string Label, string Value, string Note = "", bool Highlight = false);

    /// <summary>Draws the ledger. Returns true when the player closes it.</summary>
    public bool Draw(Profile profile, IReadOnlyList<IHeroClass> classes)
    {
        if (!IsOpen)
        {
            return false;
        }

        MarkDrawn();
        float scale = UiTheme.Scale;
        UiTheme.BeginScreen("##stats", 0.76f, 0.88f);
        UiTheme.Header("Camp · Ledger", "Your records", $"{profile.Lifetime.Runs:N0} runs  ·  {Duration(profile.Stats.SecondsPlayed)} on runs");

        // The page tabs.
        float tabHeight = 34f * scale, tabWidth = 150f * scale, tabGap = 8f * scale;
        var tabsAt = ImGui.GetCursorScreenPos();
        for (int i = 0; i < Pages.Length; i++)
        {
            ImGui.SetCursorScreenPos(tabsAt + new Vector2(i * (tabWidth + tabGap), 0f));
            if (UiTheme.Button(Pages[i].Label, new Vector2(tabWidth, tabHeight), primary: _page == Pages[i].Page))
            {
                _page = Pages[i].Page;
            }
        }

        ImGui.SetCursorScreenPos(tabsAt + new Vector2(0f, tabHeight + 12f * scale));
        float fullWidth = ImGui.GetContentRegionAvail().X;
        float buttonHeight = 40f * scale;
        float listHeight = ImGui.GetContentRegionAvail().Y - buttonHeight - 16f * scale;
        var listStart = ImGui.GetCursorScreenPos();
        ImGui.BeginChild("##statspage", new Vector2(fullWidth, listHeight), ImGuiChildFlags.None, ImGuiWindowFlags.None);
        float width = ImGui.GetContentRegionAvail().X - 14f * scale;   // room for the scrollbar
        switch (_page)
        {
            case Page.Overview:
                DrawOverview(profile, classes, width);
                break;
            case Page.Classes:
                DrawClasses(profile, classes, width);
                break;
            case Page.Bestiary:
                DrawBestiary(profile.Stats, width);
                break;
            case Page.Delve:
                DrawDelve(profile, width);
                break;
        }

        ImGui.EndChild();

        ImGui.SetCursorScreenPos(listStart + new Vector2(0f, listHeight + 10f * scale));
        var start = ImGui.GetCursorScreenPos();
        float closeWidth = 150f * scale;
        UiTheme.Text(start + new Vector2(0f, buttonHeight * 0.3f), "Added to at the end of every run, however it ends. Scroll for more.", UiTheme.Muted, 0.8f,
            fullWidth - closeWidth - 20f * scale);
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

    private static void DrawOverview(Profile profile, IReadOnlyList<IHeroClass> classes, float width)
    {
        var stats = profile.Stats;
        var lifetime = profile.Lifetime;
        string mostPlayed = "None yet", mostPlayedNote = "";
        if (stats.MostPlayedClass() is { } id && classes.FirstOrDefault(c => c.Id == id) is { } hero)
        {
            var record = stats.Class(id);
            mostPlayed = hero.Name;
            mostPlayedNote = $"{record.Runs:N0} runs, {Duration(record.Seconds)}";
        }

        Tiles("Highlights", width, new Tile[]
        {
            new("Enemies slain", $"{lifetime.Kills:N0}", "over every run", true),
            new("Most kills in a run", $"{stats.MostKills:N0}", "", true),
            new("Longest kill streak", $"{stats.BestStreak:N0}", "kills without being hit", true),
            new("Most played class", mostPlayed, mostPlayedNote, true),
        });

        Tiles("Runs", width, new Tile[]
        {
            new("Runs set out", $"{lifetime.Runs:N0}"),
            new("Won", $"{lifetime.Wins:N0}", "survived to 30:00"),
            new("Died", $"{stats.Deaths:N0}"),
            new("Went back to camp", $"{stats.ReturnedToCamp:N0}"),
            new("Time on runs", Duration(stats.SecondsPlayed)),
            new("Longest run", Clock(lifetime.LongestRun)),
            new("Highest level", stats.HighestLevel > 0 ? $"{stats.HighestLevel}" : "-", "in one run"),
            new("Level-ups", $"{stats.LevelUps:N0}"),
        });

        var nemesis = stats.Nemesis();
        Tiles("Combat", width, new Tile[]
        {
            new("Elites slain", $"{stats.ElitesKilled:N0}"),
            new("Bosses slain", $"{stats.BossesKilled:N0}"),
            new("Damage dealt", Big(stats.DamageDealt)),
            new("Most damage in a run", Big(stats.MostDamage)),
            new("Health lost", Big(stats.HealthLost)),
            new("Hits taken", $"{stats.HitsTaken:N0}"),
            new("Blows blocked", $"{stats.Blocks:N0}"),
            new("Your bane", nemesis is { } bane ? bane.Kind : "No one yet", nemesis is { } b ? $"killed you {Times(b.Times)}" : ""),
        });

        int kinds = ItemCatalog.All.Count(item => profile.CountOf(item.Id) > 0);
        Tiles("Spoils", width, new Tile[]
        {
            new("Silver earned", $"{stats.SilverEarned:N0}", $"{profile.Silver:N0} in hand"),
            new("Items found", $"{stats.ItemsFound:N0}", $"{kinds} of {ItemCatalog.All.Count} kinds owned"),
            new("Gear found", $"{stats.GearFound:N0}", $"{Gear.GearCatalog.PiecesFound(profile)} of {Gear.GearCatalog.All.Count} pieces owned"),
            new("Bounties done", $"{profile.Bounties.Count} of {Bounties.All.Count}"),
            new("Crates broken", $"{stats.CratesBroken:N0}"),
            new("Monster Rushes", $"{stats.Rushes:N0}", "lived through or not"),
            new("Rerolls used", $"{stats.Rerolls:N0}"),
            new("Banishes used", $"{stats.Banishes:N0}"),
        });
    }

    private static void DrawClasses(Profile profile, IReadOnlyList<IHeroClass> classes, float width)
    {
        float scale = UiTheme.Scale;
        float font = ImGui.GetFontSize();
        string? mostPlayed = profile.Stats.MostPlayedClass();
        var origin = ImGui.GetCursorScreenPos();
        float cardHeight = 118f * scale, gap = 10f * scale, pad = 14f * scale;
        (string Label, Func<ClassRecord, string> Value)[] columns =
        {
            ("Runs", r => $"{r.Runs:N0}"),
            ("Won", r => $"{r.Wins:N0}"),
            ("Died", r => $"{r.Deaths:N0}"),
            ("Kills", r => $"{r.Kills:N0}"),
            ("Time", r => Duration(r.Seconds)),
            ("Best kills", r => $"{r.MostKills:N0}"),
            ("Best level", r => r.HighestLevel > 0 ? $"{r.HighestLevel}" : "-"),
            ("Best streak", r => $"{r.BestStreak:N0}"),
            ("Delve clears", r => $"{r.NodesCleared:N0}"),
            ("Deepest", r => r.DeepestCleared > 0 ? $"{r.DeepestCleared}" : "-"),
        };

        for (int i = 0; i < classes.Count; i++)
        {
            var hero = classes[i];
            var record = profile.Stats.Classes.GetValueOrDefault(hero.Id) ?? new ClassRecord();
            bool top = hero.Id == mostPlayed;
            var min = origin + new Vector2(0f, i * (cardHeight + gap));
            var max = min + new Vector2(width, cardHeight);
            UiTheme.Card(min, max, UiTheme.PanelRaised, top ? UiTheme.WithAlpha(UiTheme.Brass, 0.8f) : UiTheme.Line);

            var tree = new TreeProgress(hero.Tree, profile.Tree(hero.Id, hero.Tree.Id));
            UiTheme.Text(min + new Vector2(pad, pad * 0.7f), hero.Name, record.Runs > 0 ? UiTheme.Ink : UiTheme.Muted, 1.2f);
            string treeLine = $"{tree.Tree.Name}  ·  Lv {tree.Level}";
            UiTheme.Text(min + new Vector2(pad + UiTheme.TextWidth(hero.Name, 1.2f) + 14f * scale, pad * 0.7f + font * 0.3f), treeLine, UiTheme.BrassHi, 0.8f);
            if (top)
            {
                const string badge = "MOST PLAYED";
                UiTheme.Text(new Vector2(max.X - pad - UiTheme.TextWidth(badge, 0.75f), min.Y + pad * 0.7f + font * 0.3f), badge, UiTheme.Brass, 0.75f);
            }

            float columnWidth = (width - pad * 2f) / columns.Length;
            float labelY = min.Y + cardHeight - pad - font * 2.1f;
            for (int c = 0; c < columns.Length; c++)
            {
                float x = min.X + pad + c * columnWidth;
                UiTheme.Text(new Vector2(x, labelY), columns[c].Label.ToUpperInvariant(), UiTheme.Muted, 0.62f);
                UiTheme.Text(new Vector2(x, labelY + font * 0.8f), columns[c].Value(record), record.Runs > 0 ? UiTheme.Ink : UiTheme.Faint, 1.05f);
            }
        }

        ImGui.SetCursorScreenPos(origin + new Vector2(0f, classes.Count * (cardHeight + gap)));
        ImGui.Dummy(new Vector2(width, 1f));
    }

    private static void DrawBestiary(StatsRecord stats, float width)
    {
        float scale = UiTheme.Scale;
        float font = ImGui.GetFontSize();
        var draw = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        float rowHeight = 50f * scale, pad = 12f * scale;
        long most = Math.Max(1, EnemyKind.Foes.Max(kind => stats.KillsOf(kind.Name)));
        float y = origin.Y;
        foreach (var kind in EnemyKind.Foes)
        {
            long kills = stats.KillsOf(kind.Name);
            int deaths = stats.DeathsBy.GetValueOrDefault(kind.Name);
            var min = new Vector2(origin.X, y);
            var max = min + new Vector2(width, rowHeight - 6f * scale);
            UiTheme.Card(min, max, kills > 0 ? UiTheme.PanelRaised : UiTheme.WithAlpha(UiTheme.PanelRaised, 0.5f), UiTheme.Line);

            var tierColour = kind.Tier switch { EnemyTier.Boss => UiTheme.Unique, EnemyTier.Elite => UiTheme.BrassHi, _ => UiTheme.Muted };
            UiTheme.Text(min + new Vector2(pad, pad * 0.45f), kind.Name, kills > 0 ? UiTheme.Ink : UiTheme.Muted, 0.95f);
            UiTheme.Text(min + new Vector2(pad, pad * 0.45f + font * 1.05f), TierName(kind.Tier), tierColour, 0.7f);

            // How many, as a bar against the most-slain kind.
            float barX = min.X + width * 0.34f, barWidth = width * 0.36f, barY = min.Y + (rowHeight - 6f * scale) * 0.5f - 4f * scale;
            draw.AddRectFilled(new Vector2(barX, barY), new Vector2(barX + barWidth, barY + 8f * scale), UiTheme.U32(UiTheme.Line), 3f * scale);
            if (kills > 0)
            {
                float filled = MathF.Max(6f * scale, barWidth * kills / most);
                draw.AddRectFilled(new Vector2(barX, barY), new Vector2(barX + filled, barY + 8f * scale), UiTheme.U32(UiTheme.Teal), 3f * scale);
            }

            string count = kills > 0 ? $"{kills:N0} slain" : "Not yet slain";
            UiTheme.Text(new Vector2(barX + barWidth + 14f * scale, min.Y + pad * 0.45f), count, kills > 0 ? UiTheme.Ink : UiTheme.Faint, 0.9f);
            if (deaths > 0)
            {
                string killed = $"killed you {Times(deaths)}";
                UiTheme.Text(new Vector2(max.X - pad - UiTheme.TextWidth(killed, 0.75f), min.Y + pad * 0.45f + font * 1.05f), killed, UiTheme.Warn, 0.75f);
            }

            y += rowHeight;
        }

        ImGui.SetCursorScreenPos(new Vector2(origin.X, y + 8f * scale));
        Tiles("By rarity", width, new Tile[]
        {
            new("Magic slain", $"{stats.KillsOf(MonsterRarity.Magic):N0}"),
            new("Rare slain", $"{stats.KillsOf(MonsterRarity.Rare):N0}"),
            new("Legendary slain", $"{stats.KillsOf(MonsterRarity.Legendary):N0}"),
            new("Elites and bosses", $"{stats.ElitesKilled + stats.BossesKilled:N0}"),
        }, new[] { MagicColour, RareColour, UiTheme.Unique, UiTheme.BrassHi });
    }

    private static void DrawDelve(Profile profile, float width)
    {
        var delve = profile.Delve;
        var stats = profile.Stats;
        Tiles("The Delve", width, new Tile[]
        {
            new("Deepest floor open", $"{delve.Deepest}", "", true),
            new("Nodes cleared", $"{delve.Cleared.Count:N0}", "", true),
            new("Delve runs", $"{stats.DelveRuns:N0}", "", true),
            new("Fastest clear", stats.FastestDelveClear > 0f ? Clock(stats.FastestDelveClear) : "-", "", true),
        });

        Tiles("The boss hunt", width, new Tile[]
        {
            new("Hunts", $"{stats.BossHunts:N0}"),
            new("Hollow King Unbound", $"{delve.BossesSlain:N0}", "times slain"),
            new("Marauder Unbound", $"{delve.MaraudersSlain:N0}", "times slain"),
            new("Fiend", $"{delve.FiendsSlain:N0}", "times slain"),
            new("Fastest kill", stats.FastestBossKill > 0f ? Clock(stats.FastestBossKill) : "-"),
            new("Delve Marks", $"{delve.Marks:N0}", "held"),
        });

        var kinds = delve.ClearedByKind.OrderByDescending(kv => kv.Value).Select(kv => new Tile($"{kv.Key} nodes", $"{kv.Value:N0}", "cleared")).ToArray();
        if (kinds.Length > 0)
        {
            Tiles("Cleared, by kind of node", width, kinds);
        }
    }

    /// <summary>A titled grid of number tiles, four to a row, advancing the cursor past it. <paramref name="colours"/> colours each tile's number, if given.</summary>
    private static void Tiles(string title, float width, IReadOnlyList<Tile> tiles, IReadOnlyList<Vector4>? colours = null)
    {
        float scale = UiTheme.Scale;
        float font = ImGui.GetFontSize();
        const int PerRow = 4;
        float gap = 10f * scale, pad = 12f * scale, tileHeight = 78f * scale;
        float tileWidth = (width - gap * (PerRow - 1)) / PerRow;

        var origin = ImGui.GetCursorScreenPos();
        UiTheme.Text(origin, title.ToUpperInvariant(), UiTheme.Muted, 0.75f);
        float top = origin.Y + font * 1.1f;
        for (int i = 0; i < tiles.Count; i++)
        {
            var tile = tiles[i];
            var min = new Vector2(origin.X + i % PerRow * (tileWidth + gap), top + i / PerRow * (tileHeight + gap));
            var max = min + new Vector2(tileWidth, tileHeight);
            UiTheme.Card(min, max, tile.Highlight ? UiTheme.BrassDeep : UiTheme.PanelRaised, tile.Highlight ? UiTheme.WithAlpha(UiTheme.Brass, 0.7f) : UiTheme.Line);
            UiTheme.Text(min + new Vector2(pad, pad * 0.7f), tile.Label, UiTheme.Muted, 0.72f);
            var colour = colours is not null && i < colours.Count ? colours[i] : tile.Highlight ? UiTheme.BrassHi : UiTheme.Ink;
            UiTheme.Text(min + new Vector2(pad, pad * 0.7f + font * 0.95f), tile.Value, colour, 1.35f);
            if (tile.Note.Length > 0)
            {
                UiTheme.Text(min + new Vector2(pad, max.Y - min.Y - pad * 0.6f - font * 0.7f), tile.Note, UiTheme.Faint, 0.65f);
            }
        }

        int rows = (tiles.Count + PerRow - 1) / PerRow;
        ImGui.SetCursorScreenPos(new Vector2(origin.X, top + rows * (tileHeight + gap) + 10f * scale));
        ImGui.Dummy(new Vector2(width, 1f));
    }

    private static string TierName(EnemyTier tier) => tier switch
    {
        EnemyTier.Boss => "Boss",
        EnemyTier.Elite => "Elite",
        _ => "Fodder",
    };

    private static string Times(int n) => n == 1 ? "once" : n == 2 ? "twice" : $"{n:N0} times";

    private static string Clock(float seconds) => $"{(int)seconds / 60:00}:{(int)seconds % 60:00}";

    /// <summary>A long stretch of time: "3h 07m", or "12m 40s" under an hour.</summary>
    internal static string Duration(double seconds)
    {
        long whole = (long)seconds;
        return whole >= 3600 ? $"{whole / 3600}h {whole % 3600 / 60:00}m" : $"{whole / 60}m {whole % 60:00}s";
    }

    /// <summary>A big number in a few characters: 950, 12.4K, 3.1M.</summary>
    internal static string Big(double value) => value switch
    {
        >= 1_000_000_000 => $"{value / 1_000_000_000:0.#}B",
        >= 1_000_000 => $"{value / 1_000_000:0.#}M",
        >= 10_000 => $"{value / 1_000:0.#}K",
        _ => $"{value:N0}",
    };
}
