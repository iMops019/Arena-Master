using System.Numerics;
using ImGuiNET;

namespace ArenaMaster.Game.Ui;

/// <summary>A yes-or-no question over the world, for anything that can't be undone from inside the game. Only a click answers it - no key confirms by accident.</summary>
internal sealed class ConfirmScreen : GameScreen
{
    public enum Answer
    {
        None,
        Confirm,
        Cancel,
    }

    private readonly string _title;
    private readonly string _message;
    private readonly string _confirmLabel;

    public ConfirmScreen(string title, string message, string confirmLabel)
    {
        _title = title;
        _message = message;
        _confirmLabel = confirmLabel;
    }

    /// <summary>Draws the question. Returns the answer on the frame one is given (the screen closes itself then), otherwise <see cref="Answer.None"/>.</summary>
    public Answer Draw()
    {
        if (!IsOpen)
        {
            return Answer.None;
        }

        MarkDrawn();
        float scale = UiTheme.Scale;
        UiTheme.BeginScreen("##confirm", 0.42f, 0.36f);
        var origin = ImGui.GetCursorScreenPos();
        float width = ImGui.GetContentRegionAvail().X;
        float font = ImGui.GetFontSize();

        UiTheme.Text(origin, _title.ToUpperInvariant(), UiTheme.Warn, 1.5f);
        UiTheme.Text(origin + new Vector2(0f, font * 2.2f), _message, UiTheme.Ink, 0.9f, width);

        float buttonHeight = 44f * scale;
        float buttonWidth = (width - 12f * scale) * 0.5f;
        var bottom = ImGui.GetWindowPos().Y + ImGui.GetWindowSize().Y - 20f * scale - buttonHeight;
        ImGui.SetCursorScreenPos(new Vector2(origin.X, bottom));
        var answer = Answer.None;
        if (UiTheme.Button("Cancel", new Vector2(buttonWidth, buttonHeight)))
        {
            answer = Answer.Cancel;
        }

        ImGui.SameLine(0f, 12f * scale);
        ImGui.PushStyleColor(ImGuiCol.Button, UiTheme.Warn);
        if (UiTheme.Button(_confirmLabel, new Vector2(buttonWidth, buttonHeight)))
        {
            answer = Answer.Confirm;
        }

        ImGui.PopStyleColor();
        UiTheme.EndScreen();

        if (answer != Answer.None)
        {
            Close();
        }

        return answer;
    }
}
