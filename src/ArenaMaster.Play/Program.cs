using ArenaMaster.Game;
using CEngine.Core;

var window = new EngineWindow
{
    Title = "Arena Master",
    Width = 1280,
    Height = 720,
    Mode = EngineMode.Play,
    StartOnTitleScreen = true,
    BuiltInPanels = false,   // no survival inventory, build menu or player menu in this game
    MapsMenu = false,        // the world is the game's own; an engine map would replace it
    GameContent = new ArenaMasterContent(),
};

window.Run();
