using System.Numerics;
using ArenaMaster.Game.Items;
using ImGuiNET;

namespace ArenaMaster.Game.Ui;

/// <summary>
/// The look shared by the game's own screens (camp, level-up, run summary): night slate panels, brass for what is taken, glacier teal for what can be taken,
/// the rarity colours for items. Everything is drawn with ImGui from <c>IGameContent.DrawOverlay</c> and scales with the window's height.
/// </summary>
internal static class UiTheme
{
    public static readonly Vector4 Backdrop = new(0.02f, 0.03f, 0.05f, 0.72f);
    public static readonly Vector4 Panel = new(0.063f, 0.094f, 0.114f, 0.97f);
    public static readonly Vector4 PanelRaised = new(0.082f, 0.125f, 0.153f, 1f);
    public static readonly Vector4 Line = new(0.133f, 0.188f, 0.227f, 1f);
    public static readonly Vector4 Ink = new(0.90f, 0.93f, 0.94f, 1f);
    public static readonly Vector4 Muted = new(0.54f, 0.61f, 0.65f, 1f);
    public static readonly Vector4 Faint = new(0.34f, 0.40f, 0.44f, 1f);
    public static readonly Vector4 Brass = new(0.89f, 0.66f, 0.29f, 1f);
    public static readonly Vector4 BrassHi = new(1f, 0.84f, 0.54f, 1f);
    public static readonly Vector4 BrassDeep = new(0.23f, 0.16f, 0.07f, 1f);
    public static readonly Vector4 Teal = new(0.33f, 0.82f, 0.76f, 1f);
    public static readonly Vector4 TealDeep = new(0.07f, 0.23f, 0.22f, 1f);
    public static readonly Vector4 Locked = new(0.2f, 0.255f, 0.29f, 1f);
    public static readonly Vector4 Warn = new(0.94f, 0.48f, 0.35f, 1f);

    /// <summary>A unique's colour: gear and the Delve's own rewards.</summary>
    public static readonly Vector4 Unique = new(1f, 0.56f, 0.18f, 1f);
    public static readonly Vector4 UniqueDeep = new(0.24f, 0.11f, 0.03f, 1f);

    /// <summary>How much bigger than design size (a 720-pixel-high window) to draw.</summary>
    public static float Scale => Math.Clamp(ImGui.GetIO().DisplaySize.Y / 720f, 0.75f, 3f);

    public static uint U32(Vector4 color) => ImGui.ColorConvertFloat4ToU32(color);

    public static Vector4 WithAlpha(Vector4 color, float alpha) => color with { W = color.W * alpha };

    public static Vector4 Rarity(ItemRarity rarity) => rarity switch
    {
        ItemRarity.Rare => new Vector4(0.45f, 0.65f, 1f, 1f),
        ItemRarity.Epic => new Vector4(0.78f, 0.45f, 1f, 1f),
        ItemRarity.Legendary => new Vector4(1f, 0.72f, 0.2f, 1f),
        _ => new Vector4(0.86f, 0.87f, 0.85f, 1f),
    };

    /// <summary>
    /// Dims the world and opens a centred, undecorated window of <paramref name="widthFraction"/> x <paramref name="heightFraction"/> of the screen. Pair with <see cref="EndScreen"/>.
    /// </summary>
    public static void BeginScreen(string id, float widthFraction, float heightFraction)
    {
        var display = ImGui.GetIO().DisplaySize;
        ImGui.GetBackgroundDrawList().AddRectFilled(Vector2.Zero, display, U32(Backdrop));
        ImGui.SetNextWindowPos(display * 0.5f, ImGuiCond.Always, new Vector2(0.5f, 0.5f));
        ImGui.SetNextWindowSize(new Vector2(display.X * widthFraction, display.Y * heightFraction), ImGuiCond.Always);
        ImGui.PushStyleColor(ImGuiCol.WindowBg, Panel);
        ImGui.PushStyleColor(ImGuiCol.Border, Line);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 12f * Scale);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(24f, 20f) * Scale);
        ImGui.Begin(id, ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
        ImGui.SetWindowFontScale(Scale * 0.8f);
    }

    public static void EndScreen()
    {
        ImGui.End();
        ImGui.PopStyleVar(2);
        ImGui.PopStyleColor(2);
    }

    /// <summary>Text at <paramref name="position"/> (screen pixels) at <paramref name="size"/> times the current font size.</summary>
    public static void Text(Vector2 position, string text, Vector4 color, float size = 1f, float wrapWidth = 0f)
    {
        var draw = ImGui.GetWindowDrawList();
        if (wrapWidth > 0f)
        {
            draw.AddText(ImGui.GetFont(), ImGui.GetFontSize() * size, position, U32(color), text, wrapWidth);
        }
        else
        {
            draw.AddText(ImGui.GetFont(), ImGui.GetFontSize() * size, position, U32(color), text);
        }
    }

    /// <summary>How wide <paramref name="text"/> is at <paramref name="size"/> times the current font size.</summary>
    public static float TextWidth(string text, float size = 1f) => ImGui.CalcTextSize(text).X * size;

    /// <summary>The screen's title row: a small label over a large name, and a note at the right.</summary>
    public static void Header(string eyebrow, string title, string note = "")
    {
        var origin = ImGui.GetCursorScreenPos();
        float width = ImGui.GetContentRegionAvail().X;
        Text(origin, eyebrow.ToUpperInvariant(), Muted, 0.7f);
        Text(origin + new Vector2(0f, ImGui.GetFontSize() * 0.9f), title, Ink, 1.7f);
        if (note.Length > 0)
        {
            Text(origin + new Vector2(width - TextWidth(note, 0.85f), ImGui.GetFontSize() * 1.5f), note, Muted, 0.85f);
        }

        ImGui.Dummy(new Vector2(width, ImGui.GetFontSize() * 3f));
        var draw = ImGui.GetWindowDrawList();
        var y = ImGui.GetCursorScreenPos().Y;
        draw.AddLine(new Vector2(origin.X, y), new Vector2(origin.X + width, y), U32(Line), 1f);
        ImGui.Dummy(new Vector2(width, 8f * Scale));
    }

    /// <summary>A button in the screens' style: <paramref name="primary"/> is the teal call to action. False (and drawn faded) when not <paramref name="enabled"/>.</summary>
    public static bool Button(string label, Vector2 size, bool primary = false, bool enabled = true)
    {
        ImGui.PushStyleColor(ImGuiCol.Button, primary ? Teal : PanelRaised);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, primary ? new Vector4(0.45f, 0.9f, 0.84f, 1f) : new Vector4(0.12f, 0.18f, 0.22f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, primary ? new Vector4(0.25f, 0.7f, 0.65f, 1f) : Line);
        ImGui.PushStyleColor(ImGuiCol.Text, primary ? new Vector4(0.02f, 0.14f, 0.13f, 1f) : Ink);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 6f * Scale);
        ImGui.BeginDisabled(!enabled);
        bool clicked = ImGui.Button(label, size);
        ImGui.EndDisabled();
        ImGui.PopStyleVar();
        ImGui.PopStyleColor(4);
        return clicked && enabled;
    }

    /// <summary>A rounded card outline and fill at a screen rectangle.</summary>
    public static void Card(Vector2 min, Vector2 max, Vector4 fill, Vector4 border, float thickness = 1.5f)
    {
        var draw = ImGui.GetWindowDrawList();
        draw.AddRectFilled(min, max, U32(fill), 8f * Scale);
        draw.AddRect(min, max, U32(border), 8f * Scale, ImDrawFlags.None, thickness * Scale);
    }

    /// <summary>True on the frame a key goes down.</summary>
    public static bool KeyPressed(ImGuiKey key) => ImGui.IsKeyPressed(key, false);
}

/// <summary>
/// A screen that opens over the world and can be closed with the key that opened it. The key only counts once the screen has been up a moment, so the press that
/// opened it doesn't also close it.
/// </summary>
internal abstract class GameScreen
{
    private const double KeyArmSeconds = 0.25;

    private double _openedAt = double.MaxValue;
    private bool _stampOpenTime;

    public bool IsOpen { get; private set; }

    public void Open()
    {
        IsOpen = true;
        _stampOpenTime = true;
    }

    public void Close() => IsOpen = false;

    /// <summary>Call once per frame the screen is drawn, before asking <see cref="ClosedByKey"/>.</summary>
    protected void MarkDrawn()
    {
        if (_stampOpenTime)
        {
            _openedAt = ImGui.GetTime();
            _stampOpenTime = false;
        }
    }

    /// <summary>True when <paramref name="key"/> goes down, once the screen has been open long enough.</summary>
    protected bool ClosedByKey(ImGuiKey key) => ImGui.GetTime() - _openedAt >= KeyArmSeconds && UiTheme.KeyPressed(key);
}
