using System.Numerics;
using ImGuiNET;

namespace ArenaMaster.Game.Ui;

/// <summary>
/// The fade to black between camp and a run: the picture fades out, the move happens while the screen is black (with the name of where the player is going
/// on it), and the picture fades back in. The timing is pure (<see cref="Advance"/>, tested); <see cref="Draw"/> puts the black over everything with ImGui.
/// </summary>
internal sealed class ScreenFade
{
    public const float OutSeconds = 0.45f;
    public const float HoldSeconds = 0.4f;
    public const float InSeconds = 0.75f;

    /// <summary>What a frame of the fade did.</summary>
    public enum Step
    {
        /// <summary>Nothing to act on: not fading, or partway through.</summary>
        None,

        /// <summary>The screen just went fully black and the move was made.</summary>
        Dark,

        /// <summary>The picture is fully back: the fade is over.</summary>
        Done,
    }

    private enum Phase
    {
        Idle,
        Out,
        Hold,
        In,
    }

    private Phase _phase = Phase.Idle;
    private float _elapsed;
    private Action? _atDark;

    public bool Active => _phase != Phase.Idle;

    /// <summary>Where the player is going, shown while the screen is dark.</summary>
    public string Title { get; private set; } = "";

    /// <summary>How black the screen is, 0 (clear) to 1.</summary>
    public float Alpha => _phase switch
    {
        Phase.Out => Math.Clamp(_elapsed / OutSeconds, 0f, 1f),
        Phase.Hold => 1f,
        Phase.In => 1f - Math.Clamp(_elapsed / InSeconds, 0f, 1f),
        _ => 0f,
    };

    /// <summary>Fades out, runs <paramref name="atDark"/> once the screen is black, and fades back in. Starting over a fade already running replaces it.</summary>
    public void Start(string title, Action atDark)
    {
        Title = title;
        _atDark = atDark;
        _phase = Phase.Out;
        _elapsed = 0f;
    }

    /// <summary>Starts from black, as if a move had just been made: the picture fades in with <paramref name="title"/> on it.</summary>
    public void Reveal(string title)
    {
        Title = title;
        _atDark = null;
        _phase = Phase.Hold;
        _elapsed = 0f;
    }

    /// <summary>Stops at once, clear, without making the move.</summary>
    public void Cancel()
    {
        _phase = Phase.Idle;
        _atDark = null;
    }

    /// <summary>Moves the fade on by <paramref name="seconds"/> of real time. At most one step happens per call.</summary>
    public Step Advance(float seconds)
    {
        _elapsed += Math.Max(0f, seconds);
        switch (_phase)
        {
            case Phase.Out when _elapsed >= OutSeconds:
                _phase = Phase.Hold;
                _elapsed = 0f;
                var move = _atDark;
                _atDark = null;
                move?.Invoke();
                return Step.Dark;
            case Phase.Hold when _elapsed >= HoldSeconds:
                _phase = Phase.In;
                _elapsed = 0f;
                return Step.None;
            case Phase.In when _elapsed >= InSeconds:
                _phase = Phase.Idle;
                _elapsed = 0f;
                return Step.Done;
            default:
                return Step.None;
        }
    }

    /// <summary>Draws the black over the whole screen, over the game's own screens too, with the title in its middle while the screen is dark enough to read it.</summary>
    public void Draw()
    {
        if (!Active)
        {
            return;
        }

        var display = ImGui.GetIO().DisplaySize;
        var draw = ImGui.GetForegroundDrawList();
        float alpha = Alpha;
        draw.AddRectFilled(Vector2.Zero, display, UiTheme.U32(new Vector4(0.01f, 0.012f, 0.02f, alpha)));

        float textAlpha = Math.Clamp((alpha - 0.6f) / 0.4f, 0f, 1f);
        if (textAlpha <= 0f || Title.Length == 0)
        {
            return;
        }

        var font = ImGui.GetFont();
        float size = ImGui.GetFontSize() * 2.2f * UiTheme.Scale;
        var extent = ImGui.CalcTextSize(Title) * (size / ImGui.GetFontSize());
        var at = new Vector2((display.X - extent.X) * 0.5f, display.Y * 0.46f - extent.Y * 0.5f);
        draw.AddText(font, size, at, UiTheme.U32(UiTheme.WithAlpha(UiTheme.BrassHi, textAlpha)), Title);
        float rule = extent.X * 0.6f;
        float y = at.Y + extent.Y + 10f * UiTheme.Scale;
        draw.AddLine(new Vector2((display.X - rule) * 0.5f, y), new Vector2((display.X + rule) * 0.5f, y), UiTheme.U32(UiTheme.WithAlpha(UiTheme.Brass, textAlpha * 0.7f)), 1.5f * UiTheme.Scale);
    }
}
