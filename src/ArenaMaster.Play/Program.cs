using ArenaMaster.Game;
using CEngine.Core;

var content = new ArenaMasterContent();
var window = new EngineWindow
{
    Title = "Arena Master",
    Width = 1280,
    Height = 720,
    Mode = EngineMode.Play,
    StartOnTitleScreen = true,
    BuiltInPanels = false,   // no survival inventory, build menu or player menu in this game
    MapsMenu = false,        // the world is the game's own; an engine map would replace it
    GameContent = content,
};

window.AddTitleMenuButton("New Game", content.RequestNewGame);   // start over from nothing, behind a confirmation
window.Run();
