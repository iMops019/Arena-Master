using System.Numerics;
using ImGuiNET;

namespace ArenaMaster.Game.Ui;

/// <summary>One card on the level-up screen: an upgrade and the level it would reach, or (<paramref name="IsHeal"/>) the heal offered once everything is maxed.</summary>
internal sealed record LevelUpCard(string Name, string Description, int NewLevel, int MaxLevel, bool IsHeal = false);

/// <summary>What the player did on the level-up screen: took a card or banished one (each by its place in the row), or rerolled the lot.</summary>
internal sealed record LevelUpAction(int? Take = null, bool Reroll = false, int? Banish = null);

/// <summary>
/// The level-up screen: the world is paused behind it (the content sets <c>EngineWindow.GamePaused</c>), and the player picks one of the offered upgrades by clicking
/// its card or pressing 1, 2 or 3 - or, with rerolls and banishes bought from the Quartermaster, rerolls all three (R) or banishes one. Drawn with ImGui from
/// <c>IGameContent.DrawOverlay</c>.
/// </summary>
internal sealed class LevelUpScreen
{
    /// <summary>Choices can't be taken for this long after the screen opens, so a key or click already under way doesn't pick one by accident.</summary>
    private const double ArmDelaySeconds = 0.35;

    private static readonly ImGuiKey[] NumberKeys = { ImGuiKey._1, ImGuiKey._2, ImGuiKey._3, ImGuiKey._4 };

    private IReadOnlyList<LevelUpCard> _choices = Array.Empty<LevelUpCard>();
    private int _level;
    private int _rerolls;
    private int _banishes;
    private double _openedAt = double.NegativeInfinity;
    private bool _stampOpenTime;

    public bool IsOpen { get; private set; }

    /// <summary>Shows <paramref name="choices"/> for <paramref name="level"/>, with <paramref name="rerolls"/> and <paramref name="banishes"/> left this run.</summary>
    public void Open(IReadOnlyList<LevelUpCard> choices, int level, int rerolls = 0, int banishes = 0)
    {
        _choices = choices;
        _level = level;
        _rerolls = rerolls;
        _banishes = banishes;
        IsOpen = true;
        _stampOpenTime = true;
    }

    /// <summary>New choices on the open screen (after a reroll or a banish), with what is left of each.</summary>
    public void Refresh(IReadOnlyList<LevelUpCard> choices, int rerolls, int banishes)
    {
        _choices = choices;
        _rerolls = rerolls;
        _banishes = banishes;
    }

    public void Close() => IsOpen = false;

    /// <summary>Draws the screen and returns what the player did this frame, if anything. Taking a card closes it; a reroll or banish leaves it open for <see cref="Refresh"/>.</summary>
    public LevelUpAction? Draw()
    {
        if (!IsOpen)
        {
            return null;
        }

        if (_stampOpenTime)
        {
            _openedAt = ImGui.GetTime();
            _stampOpenTime = false;
        }

        bool armed = ImGui.GetTime() - _openedAt >= ArmDelaySeconds;
        var display = ImGui.GetIO().DisplaySize;
        float scale = Math.Clamp(display.Y / 720f, 0.75f, 3f);

        ImGui.GetBackgroundDrawList().AddRectFilled(Vector2.Zero, display, ImGui.ColorConvertFloat4ToU32(new Vector4(0.02f, 0.03f, 0.06f, 0.6f)));

        float cardWidth = 250f * scale;
        float cardHeight = 190f * scale;
        float gap = 24f * scale;
        float totalWidth = _choices.Count * cardWidth + (_choices.Count - 1) * gap;
        bool extras = _rerolls > 0 || _banishes > 0;
        var windowSize = new Vector2(MathF.Max(totalWidth, 3 * 250f * scale + 48f * scale) + 48f * scale, cardHeight + (extras ? 215f : 150f) * scale);

        ImGui.SetNextWindowPos(display * 0.5f, ImGuiCond.Always, new Vector2(0.5f, 0.5f));
        ImGui.SetNextWindowSize(windowSize, ImGuiCond.Always);
        ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(0.07f, 0.09f, 0.08f, 0.94f));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 10f * scale);
        ImGui.Begin("##levelup", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoScrollbar);
        ImGui.SetWindowFontScale(scale);

        CenteredText("LEVEL UP!", 1.7f, scale, new Vector4(1f, 0.84f, 0.35f, 1f));
        CenteredText($"Level {_level}  -  choose one", 1f, scale, new Vector4(0.85f, 0.88f, 0.85f, 1f));
        ImGui.Dummy(new Vector2(0f, 10f * scale));

        LevelUpAction? action = null;
        float rowStart = (ImGui.GetWindowWidth() - totalWidth) * 0.5f;
        ImGui.SetCursorPosX(rowStart);
        for (int i = 0; i < _choices.Count; i++)
        {
            if (i > 0)
            {
                ImGui.SameLine(0f, gap);
            }

            if (DrawCard(i, _choices[i], new Vector2(cardWidth, cardHeight), scale, armed))
            {
                action = new LevelUpAction(Take: i);
            }
        }

        // A banish button under each card that can be banished (not the heal), while there are banishes left.
        if (_banishes > 0)
        {
            ImGui.SetCursorPosX(rowStart);
            for (int i = 0; i < _choices.Count; i++)
            {
                if (i > 0)
                {
                    ImGui.SameLine(0f, gap);
                }

                ImGui.PushID(100 + i);
                bool canBanish = !_choices[i].IsHeal && armed;
                if (UiTheme.Button("Banish", new Vector2(cardWidth, 30f * scale), enabled: canBanish))
                {
                    action = new LevelUpAction(Banish: i);
                }

                ImGui.PopID();
            }
        }

        ImGui.Dummy(new Vector2(0f, 8f * scale));
        if (_rerolls > 0)
        {
            float rerollWidth = 220f * scale;
            ImGui.SetCursorPosX((ImGui.GetWindowWidth() - rerollWidth) * 0.5f);
            if (UiTheme.Button($"Reroll  [R]  ·  {_rerolls} left", new Vector2(rerollWidth, 34f * scale), enabled: armed))
            {
                action = new LevelUpAction(Reroll: true);
            }
        }

        string hint = "Click a card, or press 1 / 2 / 3" + (_banishes > 0 ? $"   ·   {_banishes} banish{(_banishes == 1 ? "" : "es")} left: strikes it from this run" : "");
        CenteredText(hint, 0.8f, scale, new Vector4(0.6f, 0.65f, 0.6f, 1f));

        if (armed && action is null)
        {
            for (int i = 0; i < _choices.Count && i < NumberKeys.Length; i++)
            {
                if (ImGui.IsKeyPressed(NumberKeys[i], false))
                {
                    action = new LevelUpAction(Take: i);
                }
            }

            if (_rerolls > 0 && ImGui.IsKeyPressed(ImGuiKey.R, false))
            {
                action = new LevelUpAction(Reroll: true);
            }
        }

        ImGui.End();
        ImGui.PopStyleVar();
        ImGui.PopStyleColor();

        if (action?.Take is not null)
        {
            IsOpen = false;
        }

        return action;
    }

    /// <summary>One card: the number key, the upgrade's name, its level, what it does. The whole card is the button.</summary>
    private static bool DrawCard(int index, LevelUpCard choice, Vector2 size, float scale, bool armed)
    {
        var start = ImGui.GetCursorScreenPos();
        ImGui.PushID(index);
        bool clicked = ImGui.InvisibleButton("card", size) && armed;
        bool hovered = ImGui.IsItemHovered();
        ImGui.PopID();

        var draw = ImGui.GetWindowDrawList();
        var fill = hovered && armed ? new Vector4(0.2f, 0.32f, 0.22f, 1f) : new Vector4(0.13f, 0.19f, 0.15f, 1f);
        var border = hovered && armed ? new Vector4(0.95f, 0.8f, 0.35f, 1f) : new Vector4(0.35f, 0.5f, 0.38f, 1f);
        draw.AddRectFilled(start, start + size, ImGui.ColorConvertFloat4ToU32(fill), 8f * scale);
        draw.AddRect(start, start + size, ImGui.ColorConvertFloat4ToU32(border), 8f * scale, ImDrawFlags.None, 2f * scale);

        float pad = 14f * scale;
        float fontSize = ImGui.GetFontSize();
        var white = ImGui.ColorConvertFloat4ToU32(new Vector4(0.95f, 0.96f, 0.94f, 1f));
        var gold = ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 0.84f, 0.35f, 1f));
        var muted = ImGui.ColorConvertFloat4ToU32(new Vector4(0.7f, 0.76f, 0.7f, 1f));

        draw.AddText(ImGui.GetFont(), fontSize * 0.8f, start + new Vector2(pad, pad), muted, $"[{index + 1}]");
        draw.AddText(ImGui.GetFont(), fontSize * 1.15f, start + new Vector2(pad, pad + fontSize * 1.1f), white, choice.Name);

        string level = choice.IsHeal ? "" : choice.NewLevel == 1 ? "NEW" : $"Level {choice.NewLevel} / {choice.MaxLevel}";
        draw.AddText(ImGui.GetFont(), fontSize * 0.85f, start + new Vector2(pad, pad + fontSize * 2.5f), gold, level);

        draw.AddText(ImGui.GetFont(), fontSize * 0.9f, start + new Vector2(pad, pad + fontSize * 3.9f), white, choice.Description, size.X - 2f * pad);
        return clicked;
    }

    /// <summary>A centred line at <paramref name="size"/> times the screen's own scale (<paramref name="scale"/>), in <paramref name="color"/>.</summary>
    private static void CenteredText(string text, float size, float scale, Vector4 color)
    {
        ImGui.SetWindowFontScale(scale * size);
        float width = ImGui.CalcTextSize(text).X;
        ImGui.SetCursorPosX(MathF.Max((ImGui.GetWindowWidth() - width) * 0.5f, 0f));
        ImGui.TextColored(color, text);
        ImGui.SetWindowFontScale(scale);
    }
}
